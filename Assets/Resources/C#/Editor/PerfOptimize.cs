// PerfOptimize — one-click scene performance fixes, runnable two ways:
//   menu  TimeKiller/Optimize/Fix Light Perf (open scene)
//   TimeKiller.EditorTools.PerfOptimize.FixLights()  (Claude via unity-mcp execute_code)
// Operates on the CURRENTLY OPEN scene. Idempotent — safe to run repeatedly.
//
// WHY: every Light2D in this project was created with AddComponent<Light2D>(),
// which defaults 2D SHADOWS to ON. But the scene has ZERO ShadowCaster2D
// components, so URP runs a shadow render pass per on-screen light that draws
// nothing — a pure framerate tax. Disabling shadows is lossless here (nothing
// casts a shadow). If real shadow casters are ever added, gate this behind a
// tag/name check instead of blanket-disabling.
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace TimeKiller.EditorTools
{
    public static class PerfOptimize
    {
        [MenuItem("TimeKiller/Optimize/Fix Light Perf (open scene)")]
        public static void RunFromMenu() => Debug.Log("[PerfOptimize]\n" + FixLights());

        /// Disable 2D shadows on every Light2D in the open scene (no casters exist,
        /// so this is visually lossless). Returns a short report. Idempotent.
        public static string FixLights()
        {
            var lights = Object.FindObjectsByType<Light2D>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            int shadowsWere = 0, casters = CountShadowCasters();
            var sb = new StringBuilder();

            foreach (var light in lights)
            {
                var so = new SerializedObject(light);
                var prop = so.FindProperty("m_ShadowsEnabled");
                if (prop == null || !prop.boolValue) continue;
                prop.boolValue = false;
                so.ApplyModifiedProperties();
                EditorUtility.SetDirty(light);
                shadowsWere++;
            }

            if (shadowsWere > 0)
            {
                EditorSceneMarkDirty();
                sb.AppendLine($"Disabled 2D shadows on {shadowsWere}/{lights.Length} lights.");
            }
            else
            {
                sb.AppendLine($"All {lights.Length} lights already have shadows off — nothing to do.");
            }

            sb.AppendLine(casters == 0
                ? "ShadowCaster2D in scene: 0 (so disabling shadows changes nothing visually — pure win)."
                : $"WARNING: {casters} ShadowCaster2D present — some of those shadows may have been intentional. Review in the editor.");
            sb.Append("Save the scene (Ctrl+S) to keep the change.");
            return sb.ToString();
        }

        static int CountShadowCasters()
        {
            // ShadowCaster2D lives in the URP 2D runtime; count by type name so this
            // compiles even though the type is rarely referenced directly here.
            int n = 0;
            foreach (var mb in Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (mb != null && mb.GetType().Name == "ShadowCaster2D") n++;
            return n;
        }

        static void EditorSceneMarkDirty()
        {
            var scene = SceneManager.GetActiveScene();
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
        }
    }
}
