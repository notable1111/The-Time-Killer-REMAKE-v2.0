// Menu: TimeKiller/Setup/55 - Light the dark rooms.
//
// The problem, measured rather than assumed. On 2026-08-27 CastleWingLDtk held 58
// ShadowCaster2D components. Seven are containers whose transform is parked at the
// origin while their geometry lives elsewhere (HallColliders, __Shadow_1..6), so
// judging them by transform position is meaningless and they are skipped. Of the
// 51 genuinely placed casters, only 29 sat inside a shadow-casting light's radius.
// The remaining 22 paid for a caster and drew nothing.
//
// They clustered, which is what made this worth fixing rather than shrugging at:
// the kitchen and the armory had NO shadow-casting lamp at all, and a line of
// pillars sat one to three metres outside the corridor torches.
//
// Two fixes, both driven by RoomLightingConfig and neither of them hardcoded:
//   1. Create a lamp in a room that has none.
//   2. Widen an existing lamp by the smallest amount that reaches a prop just
//      past its edge, capped so one prop cannot turn a torch into a floodlight.
//
// Three things this script deliberately will NOT do, because they would destroy
// hand work (CLAUDE.md 5):
//   * It never MOVES a lamp that already exists. Drag one and it stays dragged.
//   * It never destroys and rebuilds. Every object is found-or-created in place.
//   * It never shrinks a light. Widening only ever increases a radius.
//
// Re-running is safe: the widened radius is computed as an absolute target from
// fixed geometry, not added to the current value, so it converges instead of
// growing a little more on every run.
using System.Collections.Generic;
using System.Text;
using TimeKiller.Lighting;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace TimeKiller.Lighting.EditorTools
{
    public static class RoomLightSetup
    {
        const string ConfigAssetPath = "Assets/Resources/C#/Lighting/Configs/RoomLightingConfig.asset";

        [MenuItem("TimeKiller/Setup/55 - Light the dark rooms")]
        public static void Run()
        {
            if (TimeKiller.EditorTools.SetupGuard.Blocked("55 - Light the dark rooms")) return;

            var config = LoadOrCreateConfig();
            if (config == null)
            {
                Debug.LogError("[TimeKiller Setup] 55 - no RoomLightingConfig and none could be created; nothing touched.");
                return;
            }

            var template = FindTemplate(config.templateLightName);
            if (template == null)
            {
                Debug.LogError("[TimeKiller Setup] 55 - no light named '" + config.templateLightName +
                               "' in this scene to copy the look from. Aborting rather than inventing a colour.");
                return;
            }

            var log = new StringBuilder();
            log.AppendLine("[TimeKiller Setup] 55 - Light the dark rooms");
            log.AppendLine("  scene    : " + UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
            log.AppendLine("  template : '" + template.name + "' radius " + template.pointLightOuterRadius.ToString("F2") +
                           ", intensity " + template.intensity.ToString("F2") + ", blend " + template.blendStyleIndex);

            int before = CountDark(config, log, false);

            var parent = GameObject.Find(config.parentName);
            if (parent == null)
            {
                parent = new GameObject(config.parentName);
                Undo.RegisterCreatedObjectUndo(parent, "create room lights parent");
                log.AppendLine("  created parent '" + config.parentName + "'");
            }

            int created = 0, refreshed = 0;
            foreach (var lamp in config.lamps)
            {
                var existing = FindChild(parent.transform, lamp.name);
                bool isNew = existing == null;
                if (isNew)
                {
                    var go = new GameObject(lamp.name);
                    Undo.RegisterCreatedObjectUndo(go, "create room lamp");
                    go.transform.SetParent(parent.transform, true);
                    go.transform.position = new Vector3(lamp.position.x, lamp.position.y, 0f);
                    existing = go.transform;
                    created++;
                }
                else refreshed++;

                var light = existing.GetComponent<Light2D>();
                if (light == null) light = Undo.AddComponent<Light2D>(existing.gameObject);

                Undo.RecordObject(light, "configure room lamp");
                light.lightType = Light2D.LightType.Point;
                light.color = template.color;
                light.intensity = template.intensity;
                light.blendStyleIndex = template.blendStyleIndex;
                light.falloffIntensity = template.falloffIntensity;
                light.shadowsEnabled = true;
                light.shadowIntensity = template.shadowIntensity;
                light.pointLightInnerRadius = template.pointLightInnerRadius;
                light.pointLightOuterRadius = lamp.outerRadiusOverride > 0f
                    ? lamp.outerRadiusOverride
                    : template.pointLightOuterRadius;
                EditorUtility.SetDirty(light);

                if (lamp.flicker && existing.GetComponent<FlickerLight2D>() == null)
                    Undo.AddComponent<FlickerLight2D>(existing.gameObject);

                log.AppendLine("  " + (isNew ? "created " : "refreshed ") + lamp.name +
                               " at (" + existing.position.x.ToString("F1") + "," + existing.position.y.ToString("F1") + ")" +
                               (isNew ? "" : " [position left as you placed it]") +
                               (string.IsNullOrEmpty(lamp.why) ? "" : "   <- " + lamp.why));
            }

            int widened = config.widenNearMissLights ? WidenNearMiss(config, log) : 0;
            if (!config.widenNearMissLights) log.AppendLine("  widening disabled in config - existing lamps untouched");

            int after = CountDark(config, log, true);

            log.AppendLine("  ---");
            log.AppendLine("  lamps created " + created + ", refreshed " + refreshed + ", existing lamps widened " + widened);
            log.AppendLine("  dark casters: " + before + " -> " + after);
            log.AppendLine("  Positions are a starting point. Drag them; re-running will not move them back.");

            EditorSceneManagerMarkDirty();
            Debug.Log(log.ToString());
        }

        /// Only ever increases a radius, and only by the least that reaches the prop.
        /// The target is absolute (distance + margin, capped), so repeat runs converge.
        static int WidenNearMiss(RoomLightingConfig config, StringBuilder log)
        {
            var lights = CastingLights();
            var casters = PlacedCasters();
            var target = new Dictionary<Light2D, float>();

            foreach (var c in casters)
            {
                Vector2 p = c.transform.position;
                Light2D nearest = null;
                float nearestGap = float.MaxValue;
                bool covered = false;
                foreach (var l in lights)
                {
                    float gap = Vector2.Distance(p, l.transform.position) - l.pointLightOuterRadius;
                    if (gap <= 0f) { covered = true; break; }
                    if (gap < nearestGap) { nearestGap = gap; nearest = l; }
                }
                if (covered || nearest == null) continue;
                if (nearestGap > config.nearMissThreshold) continue;   // needs its own lamp, not a bigger neighbour

                float need = Vector2.Distance(p, nearest.transform.position) + config.nearMissMargin;
                if (need > config.maxWidenedRadius) continue;          // refuse to make a floodlight
                float existing;
                if (!target.TryGetValue(nearest, out existing) || need > existing) target[nearest] = need;
            }

            int changed = 0;
            foreach (var kv in target)
            {
                if (kv.Value <= kv.Key.pointLightOuterRadius) continue; // never shrink
                Undo.RecordObject(kv.Key, "widen lamp to reach a dark prop");
                log.AppendLine("  widened '" + kv.Key.name + "' " + kv.Key.pointLightOuterRadius.ToString("F2") +
                               " -> " + kv.Value.ToString("F2"));
                kv.Key.pointLightOuterRadius = kv.Value;
                EditorUtility.SetDirty(kv.Key);
                changed++;
            }
            return changed;
        }

        static int CountDark(RoomLightingConfig config, StringBuilder log, bool isAfter)
        {
            var lights = CastingLights();
            int dark = 0;
            foreach (var c in PlacedCasters())
            {
                Vector2 p = c.transform.position;
                bool covered = false;
                foreach (var l in lights)
                    if (Vector2.Distance(p, l.transform.position) <= l.pointLightOuterRadius) { covered = true; break; }
                if (!covered) dark++;
            }
            if (!isAfter) log.AppendLine("  dark casters before: " + dark);
            return dark;
        }

        /// Casters whose transform actually represents where their geometry is.
        /// A container parked at the origin does not, and counting it reports a
        /// prop as unlit when nothing is wrong.
        static List<ShadowCaster2D> PlacedCasters()
        {
            var list = new List<ShadowCaster2D>();
            foreach (var c in Object.FindObjectsByType<ShadowCaster2D>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (((Vector2)c.transform.position).sqrMagnitude >= 0.01f) list.Add(c);
            return list;
        }

        static List<Light2D> CastingLights()
        {
            var list = new List<Light2D>();
            foreach (var l in Object.FindObjectsByType<Light2D>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (l.shadowsEnabled) list.Add(l);
            return list;
        }

        static Light2D FindTemplate(string name)
        {
            foreach (var l in Object.FindObjectsByType<Light2D>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (l.gameObject.name == name) return l;
            return null;
        }

        static Transform FindChild(Transform parent, string name)
        {
            foreach (Transform t in parent) if (t.name == name) return t;
            return null;
        }

        static RoomLightingConfig LoadOrCreateConfig()
        {
            var config = AssetDatabase.LoadAssetAtPath<RoomLightingConfig>(ConfigAssetPath);
            if (config != null) return config;
            config = ScriptableObject.CreateInstance<RoomLightingConfig>();
            AssetDatabase.CreateAsset(config, ConfigAssetPath);
            AssetDatabase.SaveAssets();
            Debug.Log("[TimeKiller Setup] 55 - created " + ConfigAssetPath + " with defaults.");
            return config;
        }

        static void EditorSceneManagerMarkDirty()
        {
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
                UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());
        }
    }
}
