// Menu: TimeKiller/Setup/24 - Setup Audio Director (tension music).
// Creates/updates AudioConfig.asset, wires the user's ear-sorted Horror Sounds
// tracks (2026-07-23) into Dread/Mystery/Investigate/Chase/Safe + Menu, keeps
// the PSX-pack stings, and builds the AudioDirector object with its sources.
// Music root: Outsource/Horror Sounds. Stings root: the PSX pack (unchanged).
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace TimeKiller.Audio.EditorTools
{
    public static class AudioSetup
    {
        const string ConfigPath = "Assets/Resources/C#/Audio/Configs/AudioConfig.asset";
        const string HorrorRoot = "Assets/Resources/Outsource/Horror Sounds";
        const string PsxRoot = "Assets/Resources/Outsource/Audio/PSXHorrorMusic/Pack/PSX Horror Music Pack & SFX";
        const string EchoRoot = "Assets/Resources/Outsource/Audio/EchoChambersAmbience";

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

            // --- Music layers: the user's ear-sort of the Horror Sounds pack ---
            config.dreadTracks = Music(
                "Music/Insidious Fog.wav", "Music/Haunted Manor.wav", "Music/Shadow.wav");
            config.mysteryTracks = Music(
                "Music/Forgotten Asylum.wav", "Music/Entity Behind Glass.wav",
                "Music/Blood Moon Ritual.wav", "Music/Necrotic Decay.wav");
            config.investigateTracks = Music(
                "Music/Abyssal Depths.wav", "Music/Breathing Walls.wav",
                "Horror_Music_Pack_Starter_Kit/WAV_LIGHT_TENSION_LOOP_The_Urge_to_Kill.wav",
                "Music/Deep Creeping Horror.wav", "Music/It's in the Blood.wav");
            config.chaseTracks = Music(
                "Horror_Music_Pack_Starter_Kit/WAV_TENSION_LOOP_The_Wood_Monster.wav",
                "Music/Containment Breach.wav",
                "Horror_Music_Pack_Starter_Kit/WAV_MENU_FULL_Systolic_Menace.wav");
            config.safeTracks = Music(
                "Horror_Music_Pack_Starter_Kit/WAV_AMBIENCE_LOOP_Mirrored_Reflections.wav",
                "AWakingDream_I.wav", "Music/Whispers in the Void.wav");
            config.menuTracks = Music(
                "Music/Toxic Corridor.wav", "Music/Mutation Chamber.wav",
                "Music/Phantom Echoes.wav", "Music/Desolate Wasteland.wav");

            // --- Stings stay on the PSX pack (not part of the music re-sort) ---
            config.spottedStings = new[]
            {
                Psx("SFX", "Jumpscare Sfx"), Psx("SFX", "Jumpscare Sfx 2"), Psx("SFX", "Jumpscare SFX 3"),
            }.Where(c => c != null).ToArray();
            config.heardRiser = Psx("SFX", "Dark Riser");
            config.deathSting = Psx("SFX", "Death Sfx");

            // --- Endgame: the gate is open, the run for the door. CANDIDATES —
            // swap by ear in the AudioConfig inspector; nothing else uses these.
            config.endgameTracks = new[]
            {
                Psx("Chase Tracks", "Chase Track 2 Master"),
                Psx("Combat Tracks", "Combat Track 2 master"),
            }.Where(c => c != null).ToArray();
            config.gateUnlockSting = Echo("Deep Impact_1");
            config.escapeSting = Echo("Whoosh_1");

            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();

            int music = config.dreadTracks.Length + config.mysteryTracks.Length + config.investigateTracks.Length
                + config.chaseTracks.Length + config.safeTracks.Length;
            if (music < 15)
                Debug.LogWarning($"[TimeKiller Setup] Only {music} music tracks wired — check the Horror Sounds paths (WAVs must be imported).");

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
            Debug.Log($"[TimeKiller Setup] Audio director ready: {music} music tracks across "
                + $"Dread({config.dreadTracks.Length})/Mystery({config.mysteryTracks.Length})/"
                + $"Investigate({config.investigateTracks.Length})/Chase({config.chaseTracks.Length})/"
                + $"Safe({config.safeTracks.Length}) + Menu({config.menuTracks.Length}) + stings. "
                + "The music is your threat radar now.");
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

        // Music by path relative to the Horror Sounds root (extension included).
        static AudioClip[] Music(params string[] relPaths) =>
            relPaths.Select(p => LoadAt($"{HorrorRoot}/{p}")).Where(c => c != null).ToArray();

        // Stings from the PSX pack (mp3 in named subfolders).
        static AudioClip Psx(string folder, string name) => LoadAt($"{PsxRoot}/{folder}/{name}.mp3");

        // One-shots from the Echo Chambers pack (wav).
        static AudioClip Echo(string name) => LoadAt($"{EchoRoot}/OneShots/{name}.wav");

        static AudioClip LoadAt(string assetPath)
        {
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(assetPath);
            if (clip == null) Debug.LogWarning($"[TimeKiller Setup] Track not found: {assetPath}");
            return clip;
        }
    }
}
