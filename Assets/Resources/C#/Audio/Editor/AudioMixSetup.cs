// Menu: TimeKiller/Setup/40 - Create Audio Mix (one place for the balance).
//
// Creates AudioMixConfig.asset with the measured defaults. There is nothing to
// attach: AudioMix loads this from the Resources root itself, so it applies in
// every scene without a component to forget.
//
// Re-running NEVER overwrites an existing asset. The whole point of this config
// is that it is tuned by ear in Play Mode, and a setup script that resets the
// faders every time it runs would destroy exactly the work it exists to hold.
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace TimeKiller.Audio.EditorTools
{
    public static class AudioMixSetup
    {
        const string ConfigPath = "Assets/Resources/C#/Audio/Configs/AudioMixConfig.asset";

        [MenuItem("TimeKiller/Setup/40 - Create Audio Mix (one place for the balance)")]
        public static void Build()
        {
            if (TimeKiller.EditorTools.SetupGuard.Blocked("40 - Create Audio Mix (one place for the balance)")) return;

            var existing = AssetDatabase.LoadAssetAtPath<AudioMixConfig>(ConfigPath);
            if (existing != null)
            {
                Debug.Log($"[TimeKiller Setup] 40 - Audio Mix: config already exists at {ConfigPath} — " +
                          $"left untouched ({existing.channels?.Length ?? 0} channels, master {existing.masterLevel:0.00}). " +
                          "Delete the asset by hand if you want the measured defaults back.");
                Selection.activeObject = existing;
                return;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(ConfigPath)!);
            var config = ScriptableObject.CreateInstance<AudioMixConfig>();
            config.channels = AudioMixConfig.DefaultChannels();
            AssetDatabase.CreateAsset(config, ConfigPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[TimeKiller Setup] 40 - Audio Mix: created {ConfigPath} with " +
                      $"{config.channels.Length} channels, master {config.masterLevel:0.00}.\n" +
                      "  Tune it live in Play Mode — ScriptableObject edits persist.");
            Selection.activeObject = config;
            AttachMusicEq();
        }

        /// The EQ has to live ON the music AudioSources, because OnAudioFilterRead
        /// only sees the stream of the source it is attached to. Separate menu
        /// item as well as part of Build(), so it can be re-run on a scene whose
        /// mix config already exists (Build() returns early in that case).
        [MenuItem("TimeKiller/Setup/40b - Attach Music EQ to the music sources")]
        public static void AttachMusicEq()
        {
            if (TimeKiller.EditorTools.SetupGuard.Blocked("40b - Attach Music EQ to the music sources")) return;

            var director = Object.FindAnyObjectByType<AudioDirector>();
            if (director == null)
            {
                Debug.LogWarning("[TimeKiller Setup] 40b - no AudioDirector in the open scene; " +
                                 "run Setup/24 first, then this.");
                return;
            }
            int added = 0, existing = 0;
            foreach (var source in director.GetComponentsInChildren<AudioSource>(true))
            {
                // Music only. The sting source must stay dry — the stings measured
                // 5% below 60Hz and 34% in 300-800, so they are not part of the
                // problem and high-passing them would only thin them out.
                if (source.gameObject.name.IndexOf("music", System.StringComparison.OrdinalIgnoreCase) < 0) continue;
                if (source.GetComponent<MusicEq>() != null) { existing++; continue; }
                Undo.AddComponent<MusicEq>(source.gameObject);
                added++;
            }
            EditorUtility.SetDirty(director);
            EditorSceneManager.MarkSceneDirty(director.gameObject.scene);
            Debug.Log($"[TimeKiller Setup] 40b - Music EQ: {added} attached, {existing} already present.");
        }
    }
}
