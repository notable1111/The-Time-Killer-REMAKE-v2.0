// Menu: TimeKiller/Setup/6 - Setup Footsteps + Tight Camera.
// Builds the footstep config from the Kenney CC0 step sounds, attaches the
// audio components to the Player, and applies the tight horror framing.
using System.Linq;
using TimeKiller.Player;
using UnityEditor;
using UnityEngine;

namespace TimeKiller.EditorTools
{
    public static class PlayerFootstepSetup
    {
        const string AudioFolder = "Assets/Resources/Outsource/KenneyRPGAudio/Audio";
        const string ConfigPath = "Assets/Resources/C#/Player/Configs/PlayerFootstepConfig.asset";
        const float TightViewSize = 2.4f; // horror framing: small visible area = tension

        [MenuItem("TimeKiller/Setup/6 - Setup Footsteps + Tight Camera")]
        public static void Setup()
        {
            if (TimeKiller.EditorTools.SetupGuard.Blocked("6 - Setup Footsteps + Tight Camera")) return;

            var config = AssetDatabase.LoadAssetAtPath<PlayerFootstepConfig>(ConfigPath);
            if (config == null)
            {
                config = ScriptableObject.CreateInstance<PlayerFootstepConfig>();
                AssetDatabase.CreateAsset(config, ConfigPath);
            }

            config.stepClips = AssetDatabase.FindAssets("footstep t:AudioClip", new[] { AudioFolder })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<AudioClip>)
                .Where(c => c != null)
                .ToArray();
            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();

            if (config.stepClips.Length == 0)
                Debug.LogError("[TimeKiller Setup] No footstep clips found in " + AudioFolder);

            var player = Object.FindAnyObjectByType<PlayerController>();
            if (player == null)
            {
                Debug.LogWarning("[TimeKiller Setup] No Player in scene — run Setup/4 and 5 first.");
                return;
            }

            var audioSource = player.GetComponent<AudioSource>();
            if (audioSource == null) audioSource = Undo.AddComponent<AudioSource>(player.gameObject);
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0f; // player's own steps are heard flat, not positional

            var footsteps = player.GetComponent<PlayerFootsteps>();
            if (footsteps == null) footsteps = Undo.AddComponent<PlayerFootsteps>(player.gameObject);
            var so = new SerializedObject(footsteps);
            so.FindProperty("config").objectReferenceValue = config;
            so.ApplyModifiedPropertiesWithoutUndo();

            if (Camera.main != null) Camera.main.orthographicSize = TightViewSize;

            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(player.gameObject.scene);
            Debug.Log($"[TimeKiller Setup] Footsteps ready ({config.stepClips.Length} step sounds) and camera tightened to {TightViewSize}. Re-run Setup/5 once to add contact frames to the run clips.");
        }
    }
}
