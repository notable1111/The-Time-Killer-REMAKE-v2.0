// Menu: TimeKiller/Setup/19 - Add Player Health (3 HP).
// Creates PlayerHealthConfig.asset if missing, adds PlayerHealth to the Player
// and a CheatHotkeys object to the scene (F5 god mode, F6 refill).
using System.IO;
using TimeKiller.Core;
using UnityEditor;
using UnityEngine;

namespace TimeKiller.Player.EditorTools
{
    public static class PlayerHealthSetup
    {
        const string ConfigPath = "Assets/Resources/C#/Player/Configs/PlayerHealthConfig.asset";

        [MenuItem("TimeKiller/Setup/19 - Add Player Health (3 HP)")]
        public static void Setup()
        {
            if (TimeKiller.EditorTools.SetupGuard.Blocked("19 - Add Player Health (3 HP)")) return;

            var player = Object.FindAnyObjectByType<PlayerController>();
            if (player == null)
            {
                Debug.LogError("[TimeKiller Setup] No Player in scene — run Setup/4 first.");
                return;
            }

            var config = AssetDatabase.LoadAssetAtPath<PlayerHealthConfig>(ConfigPath);
            if (config == null)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(ConfigPath)!);
                config = ScriptableObject.CreateInstance<PlayerHealthConfig>();
                AssetDatabase.CreateAsset(config, ConfigPath);
            }

            var health = player.GetComponent<PlayerHealth>();
            if (health == null) health = Undo.AddComponent<PlayerHealth>(player.gameObject);
            var so = new SerializedObject(health);
            so.FindProperty("config").objectReferenceValue = config;
            so.ApplyModifiedPropertiesWithoutUndo();

            if (Object.FindAnyObjectByType<CheatHotkeys>() == null)
            {
                var cheats = new GameObject("CheatHotkeys");
                Undo.RegisterCreatedObjectUndo(cheats, "Cheat Hotkeys");
                cheats.AddComponent<CheatHotkeys>();
            }

            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(player.gameObject.scene);
            Debug.Log("[TimeKiller Setup] Player health ready: 3 HP, shove + i-frames on hit, respawn-at-spawn on death (v1 rule). Cheats: F5 god, F6 refill.");
        }
    }
}
