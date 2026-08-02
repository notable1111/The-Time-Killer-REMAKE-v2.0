// Menu: TimeKiller/Setup/45 - Add the Maniac Director (two-brain AI).
//
// Creates DirectorConfig.asset and puts a ManiacDirector in the scene. There is
// nothing to wire to the maniac: the Director speaks on the Core bus and he
// listens, so neither holds a reference to the other and either can be deleted
// without the other noticing.
//
// Idempotent, and it never overwrites an existing config — the hint error is the
// dial that decides whether this feature is honest, and it is meant to be tuned
// by ear in Play Mode.
using System.IO;
using TimeKiller.Director;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace TimeKiller.DirectorTools
{
    public static class DirectorSetup
    {
        const string ConfigPath = "Assets/Resources/C#/Director/Configs/DirectorConfig.asset";
        const string ObjectName = "ManiacDirector";

        [MenuItem("TimeKiller/Setup/45 - Add the Maniac Director (two-brain AI)")]
        public static void Build()
        {
            var config = AssetDatabase.LoadAssetAtPath<DirectorConfig>(ConfigPath);
            if (config == null)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(ConfigPath)!);
                config = ScriptableObject.CreateInstance<DirectorConfig>();
                AssetDatabase.CreateAsset(config, ConfigPath);
                AssetDatabase.SaveAssets();
                Debug.Log($"[TimeKiller Setup] 45 - created {ConfigPath}");
            }
            else Debug.Log("[TimeKiller Setup] 45 - DirectorConfig already exists — keeping your tuning");

            var go = GameObject.Find(ObjectName);
            if (go == null)
            {
                go = new GameObject(ObjectName);
                Undo.RegisterCreatedObjectUndo(go, "Create ManiacDirector");
            }
            var director = go.GetComponent<ManiacDirector>();
            if (director == null) director = Undo.AddComponent<ManiacDirector>(go);
            director.Init(config);

            EditorUtility.SetDirty(go);
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log($"[TimeKiller Setup] 45 - Director ready on '{EditorSceneManager.GetActiveScene().name}'.\n" +
                      $"  hints after {config.hintAfterQuietSeconds:0}s of quiet, error {config.hintError:0}u " +
                      $"(his sightRange is 7 — the error MUST stay well above it or he is cheating).\n" +
                      $"  F1 line 'Director' shows quiet time, hints issued and what he has learned.");
            Selection.activeObject = go;
        }
    }
}
