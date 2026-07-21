// Menu: TimeKiller/Setup/10 - Setup Follow Camera.
// Adds the simple follow behavior to the main camera, targeting the player.
using TimeKiller.CameraSystem;
using TimeKiller.Player;
using UnityEditor;
using UnityEngine;

namespace TimeKiller.EditorTools
{
    public static class CameraSetup
    {
        const string ConfigPath = "Assets/Resources/C#/Camera/Configs/CameraConfig.asset";

        [MenuItem("TimeKiller/Setup/10 - Setup Follow Camera")]
        public static void Setup()
        {
            var config = AssetDatabase.LoadAssetAtPath<CameraConfig>(ConfigPath);
            if (config == null)
            {
                config = ScriptableObject.CreateInstance<CameraConfig>();
                AssetDatabase.CreateAsset(config, ConfigPath);
                AssetDatabase.SaveAssets();
            }

            var cam = Camera.main;
            var player = Object.FindAnyObjectByType<PlayerController>();
            if (cam == null || player == null)
            {
                Debug.LogError("[TimeKiller Setup] Need both a Main Camera and a Player in the scene.");
                return;
            }

            var follow = cam.GetComponent<CameraFollow>();
            if (follow == null) follow = Undo.AddComponent<CameraFollow>(cam.gameObject);
            follow.Init(player.transform, config);
            EditorUtility.SetDirty(follow);

            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(cam.gameObject.scene);
            Debug.Log("[TimeKiller Setup] Follow camera ready — smoothing tunable in CameraConfig.asset.");
        }
    }
}
