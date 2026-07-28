// Menu: TimeKiller/Setup/36 - Wardrobe Search (maniac checks hiding spots).
// Creates the WardrobeSearchConfig asset if it is missing and attaches
// ManiacWardrobeSearch to the maniac, wired to that config.
//
// Idempotent and additive: re-running finds the existing config and component
// and only fills what is missing, so it is safe over a hand-tuned scene. The
// feature is removable — delete the component and SearchState falls back to
// never checking wardrobes, which is exactly the old behaviour.
using System.IO;
using TimeKiller.Maniac;
using UnityEditor;
using UnityEngine;

namespace TimeKiller.EditorTools
{
    public static class WardrobeSearchSetup
    {
        const string ConfigFolder = "Assets/Resources/C#/Maniac/Configs";
        const string ConfigPath = ConfigFolder + "/WardrobeSearchConfig.asset";

        [MenuItem("TimeKiller/Setup/36 - Wardrobe Search (maniac checks hiding spots)")]
        public static void Generate()
        {
            Directory.CreateDirectory(ConfigFolder);

            var config = AssetDatabase.LoadAssetAtPath<WardrobeSearchConfig>(ConfigPath);
            if (config == null)
            {
                config = ScriptableObject.CreateInstance<WardrobeSearchConfig>();
                AssetDatabase.CreateAsset(config, ConfigPath);
                AssetDatabase.SaveAssets();
                Debug.Log($"[TimeKiller Setup] Created {ConfigPath}");
            }

            var maniac = Object.FindAnyObjectByType<ManiacController>();
            if (maniac == null)
            {
                Debug.LogWarning("[TimeKiller Setup] No Maniac in scene — open a level scene and re-run 36.");
                return;
            }

            var search = maniac.GetComponent<ManiacWardrobeSearch>();
            if (search == null)
            {
                search = Undo.AddComponent<ManiacWardrobeSearch>(maniac.gameObject);
                Debug.Log("[TimeKiller Setup] Added ManiacWardrobeSearch to the Maniac.");
            }

            var so = new SerializedObject(search);
            so.FindProperty("config").objectReferenceValue = config;
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(search);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(maniac.gameObject.scene);

            int spots = Object.FindObjectsByType<TimeKiller.Hiding.HidingSpot>(
                FindObjectsInactive.Include, FindObjectsSortMode.None).Length;
            Debug.Log($"[TimeKiller Setup] Wardrobe search wired: {spots} hiding spots are now checkable "
                      + $"(opens at {config.openBeliefShare:0.00} of his best guess, floor {config.minAbsoluteMass:0.000}, "
                      + $"dread pause {config.dreadPauseSeconds:0.0}s).");
        }
    }
}
