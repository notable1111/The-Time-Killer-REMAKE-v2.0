// Menu: TimeKiller/Setup/24 - Setup Audio Director (tension music).
// Creates AudioConfig.asset, wires the PSX pack tracks (royalty-free,
// credited in README) into the four layers + stings, and builds the
// AudioDirector object with its three AudioSources.
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace TimeKiller.Audio.EditorTools
{
    public static class AudioSetup
    {
        const string ConfigPath = "Assets/Resources/C#/Audio/Configs/AudioConfig.asset";
        const string MusicRoot = "Assets/Resources/Outsource/Audio/PSXHorrorMusic/Pack/PSX Horror Music Pack & SFX";

        [MenuItem("TimeKiller/Setup/24 - Setup Audio Director (tension music)")]
        public static void Build()
        {
            var config = AssetDatabase.LoadAssetAtPath<AudioConfig>(ConfigPath);
            if (config == null)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(ConfigPath)!);
                config = ScriptableObject.CreateInstance<AudioConfig>();
                AssetDatabase.CreateAsset(config, ConfigPath);
            }

            config.calmTracks = Clips("Ambience Tracks",
                "Creepy Ambience 1 master", "Creepy Ambience 2 Master", "Creepy Ambience 3 Master",
                "Creepy Ambience 4 Track", "Creepy Ambience 5 track");
            config.tenseTracks = Clips("Ambience Tracks",
                "Tense Ambience 1 Master", "Tense Ambience 2", "Tense Ambience 3 Track ", "Tense Ambience 4");
            config.chaseTracks = Clips("Chase Tracks",
                "Chase Track 1 Master", "Chase Track 2 Master", "Chase Track 3 Master");
            config.safeTracks = Clips("Safe Room Tracks",
                "Safe Room- Safe Point Track Master", "Safe Room Track 2 Master", "Safe Room Track 3");
            config.spottedStings = Clips("SFX", "Jumpscare Sfx", "Jumpscare Sfx 2", "Jumpscare SFX 3");
            config.heardRiser = Clip("SFX", "Dark Riser");
            config.deathSting = Clip("SFX", "Death Sfx");
            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();

            int wired = config.calmTracks.Length + config.tenseTracks.Length + config.chaseTracks.Length
                + config.safeTracks.Length + config.spottedStings.Length;
            if (wired < 15)
                Debug.LogWarning($"[TimeKiller Setup] Only {wired} tracks wired — check the PSX pack paths.");

            var old = GameObject.Find("AudioDirector");
            if (old != null) Undo.DestroyObjectImmediate(old);
            var root = new GameObject("AudioDirector");
            Undo.RegisterCreatedObjectUndo(root, "Audio Director");

            var director = root.AddComponent<AudioDirector>();
            var so = new SerializedObject(director);
            so.FindProperty("config").objectReferenceValue = config;
            so.FindProperty("musicA").objectReferenceValue = Source(root, "MusicA");
            so.FindProperty("musicB").objectReferenceValue = Source(root, "MusicB");
            so.FindProperty("stings").objectReferenceValue = Source(root, "Stings");
            so.ApplyModifiedPropertiesWithoutUndo();

            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(root.scene);
            Debug.Log($"[TimeKiller Setup] Audio director ready: {wired} tracks across Calm/Tense/Chase/Safe + stings. The music is your threat radar now.");
        }

        static AudioSource Source(GameObject root, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root.transform, false);
            var source = go.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            return source;
        }

        static AudioClip[] Clips(string folder, params string[] names) =>
            names.Select(n => Clip(folder, n)).Where(c => c != null).ToArray();

        static AudioClip Clip(string folder, string name)
        {
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>($"{MusicRoot}/{folder}/{name}.mp3");
            if (clip == null) Debug.LogWarning($"[TimeKiller Setup] Track not found: {folder}/{name}.mp3");
            return clip;
        }
    }
}
