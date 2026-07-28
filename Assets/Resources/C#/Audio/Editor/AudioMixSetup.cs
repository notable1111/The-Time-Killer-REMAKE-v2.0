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
using UnityEngine;

namespace TimeKiller.Audio.EditorTools
{
    public static class AudioMixSetup
    {
        const string ConfigPath = "Assets/Resources/C#/Audio/Configs/AudioMixConfig.asset";

        [MenuItem("TimeKiller/Setup/40 - Create Audio Mix (one place for the balance)")]
        public static void Build()
        {
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
        }
    }
}
