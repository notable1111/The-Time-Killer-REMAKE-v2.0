// Menu: TimeKiller/Setup/39 - Setup Voices (maniac presence + player reactions).
//
// Creates ManiacVoiceConfig.asset and PlayerVoiceConfig.asset, attaches
// ManiacVoice to the maniac and PlayerVoice to the player, and wires whatever
// usable clips the licensed packs already contain.
//
// FILLS EMPTY SLOTS ONLY. Every pool is left alone if something is already in
// it. The custom voice set is being generated separately and will be dropped
// into these same fields — a re-run of this script must never be the thing that
// deletes it. That also makes the script safe to run twice, which is the rule
// for everything under Setup/.
using System.Collections.Generic;
using System.IO;
using TimeKiller.Maniac;
using TimeKiller.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace TimeKiller.Audio.EditorTools
{
    public static class VoiceSetup
    {
        const string ManiacConfigPath = "Assets/Resources/C#/Maniac/Configs/ManiacVoiceConfig.asset";
        const string PlayerConfigPath = "Assets/Resources/C#/Player/Configs/PlayerVoiceConfig.asset";
        const string PsxSfx = "Assets/Resources/Outsource/Audio/PSXHorrorSFX/pack itchio PSX/sfx";
        const string Echo = "Assets/Resources/Outsource/Audio/EchoChambersAmbience";
        const string StepsRoot = "Assets/Resources/Assets/ManiacVoice";
        const string VoiceRoot = "Assets/Resources/Assets/Voices";

        [MenuItem("TimeKiller/Setup/39 - Setup Voices (maniac presence + player reactions)")]
        public static void Build()
        {
            var log = new List<string>();
            var maniacConfig = LoadOrCreate<ManiacVoiceConfig>(ManiacConfigPath, log);
            var playerConfig = LoadOrCreate<PlayerVoiceConfig>(PlayerConfigPath, log);

            // --- The maniac's breath: the one slot the packs can actually fill ---
            // 5.16s of low demon breathing. It is a stand-in, not the final voice:
            // it was authored as a one-shot, so its loop point is untested.
            if (maniacConfig.breathLoop == null)
            {
                maniacConfig.breathLoop = Clip($"{PsxSfx}/Creepy Events Sounds/demon breathing.wav");
                log.Add(maniacConfig.breathLoop != null
                    ? "maniac breathLoop <- 'demon breathing.wav' (stand-in)"
                    : "maniac breathLoop <- NOTHING FOUND");
            }

            // The generated voice set. No owned pack contains a growl, grunt,
            // gasp or cry, so these were made with Higgsfield seed_audio and put
            // through Tools/AudioPipeline/process_voice.py — trimmed, mono, and
            // loudness-matched to -12 dBFS RMS. That last step is not cosmetic:
            // raw output spanned -12.4 to -52.2 dB, a 40 dB spread that would
            // have left half the set inaudible next to the other half.
            Fill(ref maniacConfig.spottedGrowls, log, "maniac spottedGrowls", "maniac_spotted", 3);
            Fill(ref maniacConfig.lostYouGrowls, log, "maniac lostYouGrowls", "maniac_lost", 3);
            Fill(ref maniacConfig.attackRoars, log, "maniac attackRoars", "maniac_attack", 3);
            Fill(ref maniacConfig.idleMutters, log, "maniac idleMutters", "maniac_mutter", 4);
            // Footsteps could not be taken from the pack directly: every "steps"
            // file in it is a walking SEQUENCE (tunnel steps 8.1s, mud steps
            // 31.6s) and this system triggers one clip per stride, so wiring one
            // would stack eight-second walks on top of each other. These eight
            // are single steps cut out of 'tunnel steps.wav' by
            // Tools/AudioPipeline/slice_footsteps.py and peak-normalised — the
            // measured stride in that recording was 0.486s with no missed steps.
            if (maniacConfig.footstepClips == null || maniacConfig.footstepClips.Length == 0)
            {
                var steps = new List<AudioClip>();
                for (int i = 1; i <= 8; i++)
                {
                    var step = AssetDatabase.LoadAssetAtPath<AudioClip>($"{StepsRoot}/maniac_step_{i}.wav");
                    if (step != null) steps.Add(step);
                }
                maniacConfig.footstepClips = steps.ToArray();
                log.Add($"maniac footstepClips <- {steps.Count} sliced step(s) from {StepsRoot}");
            }
            else ReportEmpty(log, "maniac footstepClips", maniacConfig.footstepClips);

            Fill(ref playerConfig.painGrunts, log, "player painGrunts", "player_pain", 3);
            Fill(ref playerConfig.painGruntsCritical, log, "player painGruntsCritical", "player_paincrit", 3);
            Fill(ref playerConfig.deathCries, log, "player deathCries", "player_death", 2);
            Fill(ref playerConfig.spottedGasps, log, "player spottedGasps", "player_gasp", 3);
            Fill(ref playerConfig.repairEfforts, log, "player repairEfforts", "player_repair", 3);

            EditorUtility.SetDirty(maniacConfig);
            EditorUtility.SetDirty(playerConfig);

            // --- Attach the components ---
            var maniac = Object.FindAnyObjectByType<ManiacController>();
            if (maniac == null) log.Add("NO ManiacController in the open scene — component not attached");
            else Attach(maniac.gameObject, maniacConfig, log);

            var player = Object.FindAnyObjectByType<PlayerController>();
            if (player == null) log.Add("NO PlayerController in the open scene — component not attached");
            else Attach(player.gameObject, playerConfig, log);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            if (maniac != null || player != null)
                EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

            Debug.Log("[TimeKiller Setup] 39 - Voices:\n  " + string.Join("\n  ", log));
        }

        static void Attach(GameObject host, ManiacVoiceConfig config, List<string> log)
        {
            var voice = host.GetComponent<ManiacVoice>();
            if (voice == null)
            {
                Undo.RegisterFullObjectHierarchyUndo(host, "Add ManiacVoice");
                voice = Undo.AddComponent<ManiacVoice>(host);
                log.Add($"ManiacVoice added to '{host.name}'");
            }
            else log.Add($"ManiacVoice already on '{host.name}' — reusing");
            voice.Init(config);
            EditorUtility.SetDirty(host);
        }

        static void Attach(GameObject host, PlayerVoiceConfig config, List<string> log)
        {
            var voice = host.GetComponent<PlayerVoice>();
            if (voice == null)
            {
                Undo.RegisterFullObjectHierarchyUndo(host, "Add PlayerVoice");
                voice = Undo.AddComponent<PlayerVoice>(host);
                log.Add($"PlayerVoice added to '{host.name}'");
            }
            else log.Add($"PlayerVoice already on '{host.name}' — reusing");
            voice.Init(config);
            EditorUtility.SetDirty(host);
        }

        static T LoadOrCreate<T>(string path, List<string> log) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null) { log.Add($"{typeof(T).Name} exists — keeping its current values"); return asset; }
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            log.Add($"{typeof(T).Name} created at {path}");
            return asset;
        }

        static AudioClip Clip(string path)
        {
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            if (clip == null) Debug.LogWarning($"[TimeKiller Setup] missing audio clip: {path}");
            return clip;
        }

        /// Fills a pool from `<prefix>_1..n` — but ONLY if it is empty, so a
        /// re-run can never wipe a set that has since been replaced or re-tuned.
        static void Fill(ref AudioClip[] pool, List<string> log, string label, string prefix, int count)
        {
            if (pool != null && pool.Length > 0)
            {
                log.Add($"{label}: {pool.Length} clip(s) already wired — left untouched");
                return;
            }
            var found = new List<AudioClip>();
            for (int i = 1; i <= count; i++)
            {
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>($"{VoiceRoot}/{prefix}_{i}.wav");
                if (clip != null) found.Add(clip);
            }
            pool = found.ToArray();
            log.Add(found.Count == count
                ? $"{label} <- {found.Count} clip(s)"
                : $"{label} <- {found.Count}/{count} clip(s)  ** MISSING SOME **");
        }

        static void ReportEmpty(List<string> log, string label, AudioClip[] pool)
        {
            if (pool == null || pool.Length == 0) log.Add($"{label}: EMPTY — needs the generated voice set");
            else log.Add($"{label}: {pool.Length} clip(s) already wired — left untouched");
        }
    }
}
