// Menu: TimeKiller/Setup/36 - Setup Player Heartbeat.
//
// Creates HeartbeatConfig.asset if missing and puts a PlayerHeartbeat + its own
// AudioSource on the Player. Safe to re-run: it reuses whatever is already there
// rather than duplicating, so it cannot damage a hand-tuned scene.
//
// It touches nothing but the Player. The old "Heartbeat" and "HiddenHeartbeat"
// children that HealthVfx and HidingVfx used to create are cleared out by
// Setup/37 instead — deleting scene objects is kept in its own menu item so it
// is always something you asked for.
using System.Linq;
using TimeKiller.Heartbeat;
using TimeKiller.Player;
using UnityEditor;
using UnityEngine;

namespace TimeKiller.EditorTools
{
    public static class HeartbeatSetup
    {
        const string ConfigPath = "Assets/Resources/C#/Heartbeat/Configs/HeartbeatConfig.asset";
        // The "dry" variant, chosen by ear 2026-07-28: near-pure sub thud with no
        // noise layer, 265ms so it has fully decayed before the next beat even at
        // nearBpm 160. The old HealthVfx/heartbeat.wav is superseded.
        const string ClipPath = "Assets/Resources/Assets/Heartbeat/heartbeat_dry.wav";
        const string BreathConfigPath = "Assets/Resources/C#/Heartbeat/Configs/BreathingConfig.asset";
        const string BreathLoopPath = "Assets/Resources/Outsource/Audio/PSXHorrorSFX/pack itchio PSX/sfx/Creepy Events Sounds/strong breathe person.wav";
        // A SINGLE breath cut out of the PSX pack's "Deep breath.mp3" and
        // normalised (2026-07-28). The source was unusable as-is: it peaked at
        // 0.115 — about 31dB below the heartbeat, so it could never be heard even
        // at full volume — and it was really five breath cycles over six seconds
        // with a silent half-second lead-in, not one deep breath.
        const string GaspPath = "Assets/Resources/Assets/Heartbeat/breath_recovery.wav";

        // Breath one-shots, cut from the pack's continuous takes by
        // Tools/AudioPipeline/slice_breaths.py. THIS is the voice switch: set it
        // to "strong" (4 inhale / 3 exhale, deeper and more effortful) or
        // "sleeping" (9 / 9, far more variation but measurably hissier —
        // zero-crossing rate ~0.49 against strong's ~0.10), then re-run Setup/36.
        // The two are never blended: they are different people in different
        // rooms, and mixing them breathes like two players.
        const string BreathFolder = "Assets/Resources/Assets/Heartbeat/Breaths";
        const string BreathVoice = "strong";

        [MenuItem("TimeKiller/Setup/36 - Setup Player Heartbeat")]
        public static void Build()
        {
            var player = Object.FindAnyObjectByType<PlayerController>();
            if (player == null)
            {
                Debug.LogError("[TimeKiller Setup] No PlayerController in the scene — run Setup/4 first. Nothing was created.");
                return;
            }

            var config = AssetDatabase.LoadAssetAtPath<HeartbeatConfig>(ConfigPath);
            if (config == null)
            {
                System.IO.Directory.CreateDirectory("Assets/Resources/C#/Heartbeat/Configs");
                config = ScriptableObject.CreateInstance<HeartbeatConfig>();
                AssetDatabase.CreateAsset(config, ConfigPath);
                Debug.Log($"[TimeKiller Setup] Created {ConfigPath}");
            }

            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(ClipPath);
            if (clip == null)
                Debug.LogWarning($"[TimeKiller Setup] No heartbeat clip at {ClipPath} — the heart will be silent until one is assigned.");

            // Its own child, so the 2D heartbeat can never be affected by any
            // spatial-blend settings living on the player's other sources.
            var existing = player.GetComponentInChildren<PlayerHeartbeat>();
            GameObject host;
            if (existing != null)
            {
                host = existing.gameObject;
            }
            else
            {
                host = new GameObject("PlayerHeartbeat");
                host.transform.SetParent(player.transform, false);
            }

            var source = host.GetComponent<AudioSource>();
            if (source == null) source = host.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = false;
            source.spatialBlend = 0f;   // it is YOUR heart: fully 2D, never positional

            var heart = host.GetComponent<PlayerHeartbeat>();
            if (heart == null) heart = host.AddComponent<PlayerHeartbeat>();
            heart.Init(config, clip);

            var so = new SerializedObject(heart);
            so.FindProperty("config").objectReferenceValue = config;
            so.FindProperty("beatClip").objectReferenceValue = clip;
            so.ApplyModifiedPropertiesWithoutUndo();

            BuildBreathing(player);

            EditorUtility.SetDirty(heart);
            EditorUtility.SetDirty(host);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(player.gameObject.scene);
            Debug.Log("[TimeKiller Setup] Player heartbeat + breathing ready. Distance drives the rate " +
                      "continuously; Suspicious/Detected and your own sprinting raise floors under it; " +
                      "Detected ducks the score away. Breathing follows the heart and holds while you hide. " +
                      "Watch the 'Heart' and 'Breath' lines on F1.");
        }

        /// Fills a breath pool from the sliced one-shots. Swapping the VOICE is
        /// changing BreathVoice above and re-running this menu item — no code,
        /// no reassigning clips by hand, which is the swappable-content rule.
        static void AssignBreathPool(SerializedObject so, string propertyName, string prefix)
        {
            var property = so.FindProperty(propertyName);
            if (property == null) return;

            var clips = AssetDatabase.FindAssets("t:AudioClip", new[] { BreathFolder })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(p => System.IO.Path.GetFileName(p).StartsWith(prefix))
                .OrderBy(p => p)
                .Select(AssetDatabase.LoadAssetAtPath<AudioClip>)
                .Where(c => c != null)
                .ToArray();

            property.arraySize = clips.Length;
            for (int i = 0; i < clips.Length; i++)
                property.GetArrayElementAtIndex(i).objectReferenceValue = clips[i];

            if (clips.Length == 0)
                Debug.LogWarning($"[TimeKiller Setup] No '{prefix}*' clips in {BreathFolder} — " +
                                 "run Tools/AudioPipeline/slice_breaths.py. Breathing will be SILENT: " +
                                 "the sequencer deliberately does not fall back to the old loop.");
        }

        /// The lungs. Its own child object for the same reason the heart has one:
        /// these are 2D, in-your-head sources and must never inherit spatial
        /// settings from anything else on the player.
        static void BuildBreathing(PlayerController player)
        {
            var breathConfig = AssetDatabase.LoadAssetAtPath<BreathingConfig>(BreathConfigPath);
            if (breathConfig == null)
            {
                System.IO.Directory.CreateDirectory("Assets/Resources/C#/Heartbeat/Configs");
                breathConfig = ScriptableObject.CreateInstance<BreathingConfig>();
                AssetDatabase.CreateAsset(breathConfig, BreathConfigPath);
                Debug.Log($"[TimeKiller Setup] Created {BreathConfigPath}");
            }

            var loop = AssetDatabase.LoadAssetAtPath<AudioClip>(BreathLoopPath);
            var gasp = AssetDatabase.LoadAssetAtPath<AudioClip>(GaspPath);
            if (loop == null) Debug.LogWarning($"[TimeKiller Setup] No breath loop at {BreathLoopPath} — breathing will be silent.");
            if (gasp == null) Debug.LogWarning($"[TimeKiller Setup] No gasp clip at {GaspPath} — the release after a held breath will be silent.");

            var existing = player.GetComponentInChildren<PlayerBreathing>();
            GameObject host = existing != null ? existing.gameObject : new GameObject("PlayerBreathing");
            if (existing == null) host.transform.SetParent(player.transform, false);

            var source = host.GetComponent<AudioSource>();
            if (source == null) source = host.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = true;
            source.spatialBlend = 0f;
            source.volume = 0f;

            var breath = host.GetComponent<PlayerBreathing>();
            if (breath == null) breath = host.AddComponent<PlayerBreathing>();
            breath.Init(breathConfig, loop, gasp);

            var bso = new SerializedObject(breath);
            bso.FindProperty("config").objectReferenceValue = breathConfig;
            bso.FindProperty("breathLoop").objectReferenceValue = loop;
            bso.FindProperty("gaspClip").objectReferenceValue = gasp;
            AssignBreathPool(bso, "inhaleClips", $"{BreathVoice}_inhale_");
            AssignBreathPool(bso, "exhaleClips", $"{BreathVoice}_exhale_");
            bso.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(breath);
            EditorUtility.SetDirty(host);
        }
    }
}
