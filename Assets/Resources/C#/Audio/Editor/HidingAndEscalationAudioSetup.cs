// Creates and wires the config assets behind the self-installing audio
// features: the hiding muffle, the escalation cue, and the world-noise voice.
// Each loads its config from Resources and DOES NOT INSTALL AT ALL when that
// asset is missing, so this script IS the install.
//
// NO SCENE IS TOUCHED, deliberately. All three self-install via
// RuntimeInitializeOnLoadMethod: the scenes here are hand-tuned and protected,
// three sessions share one Editor, and a feature that needs no scene edit cannot
// collide with anybody or miss a level. Catacombs spent today missing 23 systems
// precisely because they were scene-object features that its setup pass never
// received; these cannot go the same way.
//
// Idempotent and non-destructive: an existing asset keeps every value already
// tuned by ear and only has genuinely empty fields filled in.
using System.Collections.Generic;
using TimeKiller.EditorTools;
using UnityEditor;
using UnityEngine;

namespace TimeKiller.Audio
{
    public static class HidingAndEscalationAudioSetup
    {
        const string ConfigFolder = "Assets/Resources/C#/Audio/Configs";
        const string MufflePath = ConfigFolder + "/WorldMuffleConfig.asset";
        const string EscalationPath = ConfigFolder + "/EscalationVoiceConfig.asset";
        const string EscalationClip = "Assets/Resources/Assets/Effects/Sfx/maniac_escalate_far.wav";
        const string NoisePath = ConfigFolder + "/WorldNoiseAudioConfig.asset";
        const string NoiseClip = "Assets/Resources/Assets/Effects/Sfx/clock_working.wav";

        [MenuItem("TimeKiller/Setup/57 - Self-installing audio (muffle, escalation, world noise)")]
        public static void Run()
        {
            if (SetupGuard.Blocked("57 - Self-installing audio")) return;

            var report = new System.Text.StringBuilder("[TimeKiller Setup] 57 - self-installing audio\n");

            var muffle = FindOrCreate<WorldMuffleConfig>(MufflePath, report);
            report.AppendLine($"   muffle: cutoff {muffle.cutoffHz:0} Hz, close {muffle.closeSeconds:0.00}s, "
                            + $"open {muffle.openSeconds:0.00}s, spatial >= {muffle.spatialThreshold:0.00}");

            var escalation = FindOrCreate<EscalationVoiceConfig>(EscalationPath, report);

            // Only ever ADD the shipped cue, and only when the pool is empty.
            // A pool someone has already curated is theirs.
            if (escalation.cues == null || escalation.cues.Length == 0)
            {
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(EscalationClip);
                if (clip == null)
                {
                    report.AppendLine($"   MISSING cue clip at {EscalationClip} — escalation stays silent.");
                }
                else
                {
                    var so = new SerializedObject(escalation);
                    var cues = so.FindProperty("cues");
                    cues.arraySize = 1;
                    cues.GetArrayElementAtIndex(0).objectReferenceValue = clip;
                    so.ApplyModifiedPropertiesWithoutUndo();
                    EditorUtility.SetDirty(escalation);
                    report.AppendLine($"   escalation cue <- {clip.name} ({clip.length:0.00}s)");
                }
            }
            else report.AppendLine($"   escalation already has {escalation.cues.Length} cue(s) — left alone");

            var noise = FindOrCreate<WorldNoiseAudioConfig>(NoisePath, report);
            if (noise.clips == null || noise.clips.Length == 0)
            {
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(NoiseClip);
                if (clip == null) report.AppendLine($"   MISSING noise clip at {NoiseClip} — world noises stay silent.");
                else
                {
                    var so = new SerializedObject(noise);
                    var clips = so.FindProperty("clips");
                    clips.arraySize = 1;
                    clips.GetArrayElementAtIndex(0).objectReferenceValue = clip;
                    so.ApplyModifiedPropertiesWithoutUndo();
                    EditorUtility.SetDirty(noise);
                    report.AppendLine($"   world noise <- {clip.name} ({clip.length:0.00}s) at volume {noise.volume:0.00}, "
                                    + $"AlwaysHeard skipped ({(noise.playAlwaysHeard ? "OFF" : "ON")}: ExitDoor voices itself)");
                }
            }
            else report.AppendLine($"   world noise already has {noise.clips.Length} clip(s) — left alone");

            // ONE-TIME MIGRATION, keyed to the exact value it replaces.
            //
            // 0.45 was my own first default and it was wrong: measured in a real
            // session the repair pulse landed 15-20 dB under the player's own
            // footsteps, because the level was being multiplied by the event's
            // Loudness (0.3 from ClockRepair) as though that were a volume. It is
            // not — the struct documents it as scaling the listener's HEARING
            // RADIUS. Only a config still sitting on that exact old default is
            // touched, so anything the user has since tuned by ear is left alone.
            if (Mathf.Approximately(noise.volume, 0.45f))
            {
                var so = new SerializedObject(noise);
                so.FindProperty("volume").floatValue = 0.85f;
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(noise);
                report.AppendLine("   world noise volume 0.45 -> 0.85 (migration: the old value was scaled by "
                                + "Loudness as if it were a volume; +13.1 dB at ClockRepair's 0.3)");
            }
            else report.AppendLine($"   world noise volume {noise.volume:0.00} is not the old default — left alone");

            ImportAsGameSfx(EscalationClip, report);
            ImportAsGameSfx(NoiseClip, report);

            AssetDatabase.SaveAssets();
            report.AppendLine("   No scene touched: all three self-install from Resources at runtime.");
            Debug.LogWarning(report.ToString());
        }

        static T FindOrCreate<T>(string path, System.Text.StringBuilder report) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null) { report.AppendLine($"   {System.IO.Path.GetFileName(path)} already exists — kept"); return asset; }
            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            report.AppendLine($"   created {path}");
            return asset;
        }

        /// Same import contract the voices use: decompressed on load, because
        /// these fire on the frame the beat happens and a streamed clip arrives
        /// late.
        static void ImportAsGameSfx(string path, System.Text.StringBuilder report)
        {
            var importer = AssetImporter.GetAtPath(path) as AudioImporter;
            if (importer == null) return;
            var settings = importer.defaultSampleSettings;
            bool dirty = false;
            if (settings.loadType != AudioClipLoadType.DecompressOnLoad)
            { settings.loadType = AudioClipLoadType.DecompressOnLoad; dirty = true; }
            if (settings.compressionFormat != AudioCompressionFormat.Vorbis)
            { settings.compressionFormat = AudioCompressionFormat.Vorbis; dirty = true; }
            if (settings.preloadAudioData) { settings.preloadAudioData = false; dirty = true; }
            if (dirty)
            {
                importer.defaultSampleSettings = settings;
                importer.SaveAndReimport();
                report.AppendLine($"   imported {System.IO.Path.GetFileName(path)}");
            }
        }

        [MenuItem("TimeKiller/Setup/57b - Remove the self-installing audio features")]
        public static void Clear()
        {
            if (SetupGuard.Blocked("57b - Remove the self-installing audio features")) return;
            var removed = new List<string>();
            foreach (var path in new[] { MufflePath, EscalationPath, NoisePath })
                if (AssetDatabase.LoadAssetAtPath<ScriptableObject>(path) != null
                    && AssetDatabase.DeleteAsset(path)) removed.Add(System.IO.Path.GetFileName(path));
            AssetDatabase.SaveAssets();
            Debug.LogWarning("[TimeKiller Setup] 57b - deleted " + (removed.Count == 0 ? "nothing" : string.Join(", ", removed))
                + ". Each feature checks for its config at startup, so none of them will install now.");
        }
    }
}
