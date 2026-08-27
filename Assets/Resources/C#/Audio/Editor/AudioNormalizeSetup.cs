// Menu: TimeKiller/Setup/41 - Normalise quiet SFX (stings, breath, steps).
//
// The 2026-07-28 audit measured every wired clip's effective in-game level and
// found the pack SFX are far too quiet to survive the mix:
//
//   heartbeat        -10.7 dB   (synthesised in-house, loud)
//   maniac growl     -14.8 dB   (generated, normalised to -12)
//   maniac BREATH    -30.8 dB   <- the warning that lets you hide
//   maniac footstep  -35.8 dB
//   JUMPSCARE STING  -38.9 dB   <- 28 dB under the heartbeat
//
// The sting is the sharpest contradiction: it is tier 0 in AudioMix, so every
// other channel ducks to make room for it — and then it arrives inaudible. No
// amount of mixing fixes that, because the problem is in the file. Raising
// `stingVolume` cannot either: it is already 0.70, so the whole remaining range
// is +3 dB against a 20 dB shortfall.
//
// So this normalises the SOURCE. It runs through Unity rather than the Python
// pipeline because several of these are .mp3 and Python's `wave` module cannot
// read those, while Unity has already decoded them.
//
// Non-destructive: originals are never touched. Normalised copies are written to
// Assets/Assets/AudioNormalized/ and the configs are re-pointed at them, so the
// licensed pack files stay pristine and this can always be undone by re-wiring.
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace TimeKiller.Audio.EditorTools
{
    public static class AudioNormalizeSetup
    {
        const string OutDir = "Assets/Resources/Assets/AudioNormalized";
        // Same target the generated voices use, so everything shares one scale.
        const float TargetRms = 0.25f;   // about -12 dBFS
        const float Ceiling = 0.95f;

        [MenuItem("TimeKiller/Setup/41 - Normalise quiet SFX (stings, breath, steps)")]
        public static void Build()
        {
            if (TimeKiller.EditorTools.SetupGuard.Blocked("41 - Normalise quiet SFX (stings, breath, steps)")) return;

            Directory.CreateDirectory(OutDir);
            var log = new List<string>();

            var ac = AssetDatabase.LoadAssetAtPath<AudioConfig>("Assets/Resources/C#/Audio/Configs/AudioConfig.asset");
            var mv = AssetDatabase.LoadAssetAtPath<TimeKiller.Maniac.ManiacVoiceConfig>(
                "Assets/Resources/C#/Maniac/Configs/ManiacVoiceConfig.asset");

            if (ac != null)
            {
                ac.spottedStings = NormaliseAll(ac.spottedStings, "sting_spotted", log);
                ac.heardRiser = Normalise(ac.heardRiser, "sting_riser", log);
                ac.deathSting = Normalise(ac.deathSting, "sting_death", log);
                ac.gateUnlockSting = Normalise(ac.gateUnlockSting, "sting_gate", log);
                ac.escapeSting = Normalise(ac.escapeSting, "sting_escape", log);
                EditorUtility.SetDirty(ac);
            }
            if (mv != null)
            {
                mv.breathLoop = Normalise(mv.breathLoop, "maniac_breath_loop", log);
                EditorUtility.SetDirty(mv);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[TimeKiller Setup] 41 - Normalise SFX:\n  " + string.Join("\n  ", log));
        }

        static AudioClip[] NormaliseAll(AudioClip[] pool, string prefix, List<string> log)
        {
            if (pool == null) return null;
            var outp = new AudioClip[pool.Length];
            for (int i = 0; i < pool.Length; i++) outp[i] = Normalise(pool[i], $"{prefix}_{i + 1}", log);
            return outp;
        }

        static AudioClip Normalise(AudioClip source, string outName, List<string> log)
        {
            if (source == null) return null;
            // Already normalised by a previous run — re-normalising a normalised
            // file would slowly compound the limiter.
            if (AssetDatabase.GetAssetPath(source).StartsWith(OutDir))
            {
                log.Add($"{outName}: already normalised — skipped");
                return source;
            }
            if (!source.LoadAudioData()) { log.Add($"{outName}: FAILED to load"); return source; }

            int ch = source.channels, sr = source.frequency, n = source.samples;
            var interleaved = new float[n * ch];
            if (!source.GetData(interleaved, 0)) { log.Add($"{outName}: FAILED GetData"); return source; }

            var mono = new float[n];
            for (int i = 0; i < n; i++)
            {
                float s = 0f;
                for (int c = 0; c < ch; c++) s += interleaved[i * ch + c];
                mono[i] = s / ch;
            }
            source.UnloadAudioData();

            double sum = 0;
            foreach (var s in mono) sum += (double)s * s;
            float rmsBefore = Mathf.Sqrt((float)(sum / Mathf.Max(1, n)));
            if (rmsBefore <= 0f) { log.Add($"{outName}: silent — skipped"); return source; }

            float gain = TargetRms / rmsBefore;
            // Soft knee rather than a hard clamp: raising the body of a quiet clip
            // pushes its transients past full scale, and clamping those squares
            // them off into audible distortion.
            for (int i = 0; i < n; i++)
            {
                float x = mono[i] * gain;
                float a = Mathf.Abs(x);
                // Unity's Mathf has no Tanh — it lives on System.Math (double).
                if (a > Ceiling)
                    x = Mathf.Sign(x) * (Ceiling + (1f - Ceiling)
                        * (float)System.Math.Tanh((a - Ceiling) / (1f - Ceiling)));
                mono[i] = Mathf.Clamp(x, -1f, 1f);
            }
            double after = 0;
            foreach (var s in mono) after += (double)s * s;
            float rmsAfter = Mathf.Sqrt((float)(after / n));

            string path = $"{OutDir}/{outName}.wav";
            WriteWav(path, mono, sr);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var written = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            log.Add($"{outName}: {Db(rmsBefore):0.0} -> {Db(rmsAfter):0.0} dB  (x{gain:0.0})  {source.name}");
            return written != null ? written : source;
        }

        static float Db(float x) => x <= 0f ? -99f : 20f * Mathf.Log10(x);

        /// Minimal 16-bit mono PCM writer. Mono on purpose: these are all either
        /// point sources in the world or sounds inside the player's own head, and
        /// a stereo file on a 3D AudioSource is collapsed by Unity anyway.
        static void WriteWav(string path, float[] samples, int sampleRate)
        {
            using (var stream = new FileStream(path, FileMode.Create))
            using (var w = new BinaryWriter(stream))
            {
                int dataBytes = samples.Length * 2;
                w.Write(new[] { 'R', 'I', 'F', 'F' });
                w.Write(36 + dataBytes);
                w.Write(new[] { 'W', 'A', 'V', 'E' });
                w.Write(new[] { 'f', 'm', 't', ' ' });
                w.Write(16);                                   // PCM chunk size
                w.Write((short)1);                             // PCM
                w.Write((short)1);                             // mono
                w.Write(sampleRate);
                w.Write(sampleRate * 2);                       // byte rate
                w.Write((short)2);                             // block align
                w.Write((short)16);                            // bits
                w.Write(new[] { 'd', 'a', 't', 'a' });
                w.Write(dataBytes);
                foreach (var s in samples) w.Write((short)Mathf.Clamp(s * 32767f, -32768f, 32767f));
            }
        }
    }
}
