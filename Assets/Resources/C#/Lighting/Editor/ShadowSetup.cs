// Menu: TimeKiller/Setup/49 - Cast 2D Shadows (walls, props, characters)
//        TimeKiller/Setup/49b - Remove 2D Shadows
//
// Gives the castle occlusion. Before this, ShadowCaster2D count in
// CastleWingLDtk was zero and every Light2D had shadowsEnabled = false: a torch
// lit both sides of a wall equally and a pillar standing under one threw nothing.
//
// WHY ShapeProvider AND NOT A BAKED SHAPE. ShadowCaster2D can either store its
// own hand-edited outline (ShapeEditor) or read one live from a Collider2D
// (ShapeProvider). This uses ShapeProvider on purpose: the user hand-tunes
// colliders, and a baked outline would silently go stale the moment he nudged
// one. The shadow shape is therefore always exactly the collision shape, for free.
//
// WHY THERE IS REFLECTION IN HERE. URP exposes no public API for the shape
// source: `ShadowCaster2D.ShadowCastingSources` and
// `ShadowShape2DProvider_Collider2D` are both `internal`, and the fields are
// private-serialized. Verified against com.unity.render-pipelines.universal
// 17.4.0. The reflection is isolated in Handles() and fails LOUDLY rather than
// silently doing nothing, so a URP upgrade that moves these produces an error
// telling you exactly what to look at instead of a scene with no shadows.
//
// THE TRAP THAT WOULD MAKE THIS LOOK BROKEN. All 18 torches carry
// FlickerLight2D, which writes Light2D.intensity every frame from its own
// `baseIntensity`. A script that set only Light2D.intensity would appear to work
// in the editor viewport and be silently discarded the instant you pressed Play.
// This writes BOTH, which is why ShadowConfig carries two torch numbers.
//
// Safe to re-run: every caster is found-or-updated in place, never destroyed and
// rebuilt, and nothing is repositioned — the user's hand-placed props are only
// read. At the default torchBoost of 1.0 the light values written are identical
// to the authored ones, so a run changes nothing except adding the casters.
//
// Removable: Setup/49b strips every caster and switches the lights back off,
// returning the scene to exactly its pre-shadow state.
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.Tilemaps;

namespace TimeKiller.Lighting.EditorTools
{
    public static class ShadowSetup
    {
        const string ConfigPath = "Assets/Resources/C#/Lighting/Configs/ShadowConfig.asset";

        [MenuItem("TimeKiller/Setup/49 - Cast 2D Shadows (walls, props, characters)")]
        public static void Build()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogError("[TimeKiller Setup] In Play Mode a setup script half-completes. Exit Play and re-run.");
                return;
            }

            var config = LoadOrCreateConfig();

            int shapeProvider; System.Type providerType;
            if (!Handles(out shapeProvider, out providerType)) return;

            // Collect the HOSTS first, not the colliders. ShadowCaster2D is
            // [DisallowMultipleComponent], so an object carrying several colliders
            // — HallColliders has seven, Pots has three — can only host one caster
            // and every other collider on it would silently cast nothing. The
            // extras get a child each. Found the expensive way: the 20x1 hall wall
            // was the collider that lost.
            var hosts = new List<GameObject>();
            var seen = new HashSet<GameObject>();
            int walls = 0, props = 0, chars = 0, skipped = 0;
            foreach (var col in Object.FindObjectsByType<Collider2D>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var kind = Classify(col, config);
                if (kind == Kind.Skip) { skipped++; continue; }
                if (kind == Kind.Wall && !config.walls) { skipped++; continue; }
                if (kind == Kind.Prop && !config.props) { skipped++; continue; }
                if (kind == Kind.Character && !config.characters) { skipped++; continue; }

                if (seen.Add(col.gameObject)) hosts.Add(col.gameObject);
                if (kind == Kind.Wall) walls++; else if (kind == Kind.Prop) props++; else chars++;
            }

            int extras = 0;
            foreach (var host in hosts)
            {
                // GetComponents order is stable, unlike FindObjectsByType, so which
                // collider lands on the host and which on a child stays the same
                // across runs and the scene diff does not churn.
                var eligible = new List<Collider2D>();
                foreach (var col in host.GetComponents<Collider2D>())
                    if (Classify(col, config) != Kind.Skip) eligible.Add(col);
                if (eligible.Count == 0) continue;

                Wire(host, eligible[0], config, shapeProvider, providerType);
                for (int i = 1; i < eligible.Count; i++)
                {
                    Wire(ShadowChild(host, i), eligible[i], config, shapeProvider, providerType);
                    extras++;
                }
            }

            int lit = EnableLights(config);
            int torches = ApplyTorchStrength(config);

            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);

            var sb = new StringBuilder("[TimeKiller Setup] 2D shadows on '").Append(scene.name).Append("':\n");
            sb.Append("  colliders casting — walls ").Append(walls).Append(", props ").Append(props)
              .Append(", characters ").Append(chars).Append("   (skipped ").Append(skipped).Append(")\n");
            sb.Append("  of those, ").Append(extras)
              .Append(" needed a '").Append(ChildPrefix).Append("N' child, because their object already hosts a caster\n");
            sb.Append("  point lights casting: ").Append(lit)
              .Append("   intensity ").Append(config.shadowIntensity.ToString("0.00"))
              .Append(", softness ").Append(config.shadowSoftness.ToString("0.00")).Append('\n');
            sb.Append("  torchBoost ").Append(config.torchBoost.ToString("0.00"))
              .Append(" applied to ").Append(torches).Append(" torches — Light2D ")
              .Append((config.torchLightIntensity * config.torchBoost).ToString("0.00"))
              .Append(", FlickerLight2D base ").Append((config.torchFlickerBase * config.torchBoost).ToString("0.00")).Append('\n');
            if (Mathf.Approximately(config.torchBoost, 1f))
                sb.Append("  NOTE: torchBoost 1.0 leaves the lighting exactly as authored. Run\n")
                  .Append("        TimeKiller/Verify/Shadow Audit for the structural check, then judge the\n")
                  .Append("        contrast in PLAY — edit-mode renders do not reflect Light2D changes.");
            Debug.Log(sb.ToString());
        }

        [MenuItem("TimeKiller/Setup/49b - Remove 2D Shadows")]
        public static void Remove()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogError("[TimeKiller Setup] Exit Play Mode first.");
                return;
            }

            int removed = 0, children = 0;
            foreach (var caster in Object.FindObjectsByType<ShadowCaster2D>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                // An overflow child exists only to carry a caster, so it goes with it.
                if (caster.gameObject.name.StartsWith(ChildPrefix))
                {
                    Undo.DestroyObjectImmediate(caster.gameObject);
                    children++; removed++;
                    continue;
                }
                Undo.DestroyObjectImmediate(caster);
                removed++;
            }
            // Sweep any orphaned children whose caster was already gone.
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (t != null && t.gameObject.name.StartsWith(ChildPrefix))
                {
                    Undo.DestroyObjectImmediate(t.gameObject);
                    children++;
                }

            int off = 0;
            foreach (var light in Object.FindObjectsByType<Light2D>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (!light.shadowsEnabled) continue;
                Undo.RecordObject(light, "Remove 2D Shadows");
                light.shadowsEnabled = false;
                EditorUtility.SetDirty(light);
                off++;
            }

            // Put the torches back to the authored strength, so removing shadows
            // also removes the boost that only existed to make them read.
            var config = AssetDatabase.LoadAssetAtPath<ShadowConfig>(ConfigPath);
            int torches = 0;
            if (config != null)
            {
                var restore = ScriptableObject.CreateInstance<ShadowConfig>();   // pristine defaults
                restore.torchBoost = 1f;
                restore.torchLightIntensity = config.torchLightIntensity;
                restore.torchFlickerBase = config.torchFlickerBase;
                torches = ApplyTorchStrength(restore);
                Object.DestroyImmediate(restore);
            }

            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
                UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            Debug.Log($"[TimeKiller Setup] 2D shadows removed: {removed} caster(s) stripped ({children} overflow child object(s) deleted), " +
                      $"{off} light(s) switched back off, {torches} torch(es) returned to authored strength. " +
                      "The scene renders exactly as it did before Setup/49.");
        }

        // ---------------------------------------------------------------- parts

        enum Kind { Skip, Wall, Prop, Character }

        static Kind Classify(Collider2D col, ShadowConfig config)
        {
            // Triggers are volumes, not objects. CameraBounds is a 66x52 trigger
            // that would otherwise shadow the entire map.
            if (col.isTrigger) return Kind.Skip;
            if (config.neverCast != null && config.neverCast.Contains(col.gameObject.name)) return Kind.Skip;

            // A TilemapCollider2D feeding a CompositeCollider2D holds no geometry
            // of its own — the composite owns it. Casting from both double-draws.
            if (col is TilemapCollider2D) return Kind.Skip;

            var composite = col as CompositeCollider2D;
            if (composite != null)
            {
                // The LDtk import puts a composite on every tilemap layer, but only
                // Collision/IntGrid actually has paths. Floor and Rug come back
                // pathCount 0, and casting from a floor would black out the room.
                if (composite.pathCount == 0) return Kind.Skip;
                return Kind.Wall;
            }

            string name = col.gameObject.name;
            if (name == "Player" || name == "Maniac") return Kind.Character;
            if (name == "HallColliders") return Kind.Wall;
            return Kind.Prop;
        }

        /// Resolves the two URP internals this needs. Returns false and logs a
        /// precise error if a URP upgrade has moved them.
        static bool Handles(out int shapeProvider, out System.Type providerType)
        {
            shapeProvider = -1;
            providerType = null;

            var casterType = typeof(ShadowCaster2D);
            var sourceEnum = casterType.GetNestedType("ShadowCastingSources", BindingFlags.NonPublic | BindingFlags.Public);
            if (sourceEnum == null || !sourceEnum.IsEnum)
            {
                Debug.LogError("[TimeKiller Setup] URP changed: ShadowCaster2D.ShadowCastingSources not found. " +
                               "Verified against URP 17.4.0. Nothing was modified.");
                return false;
            }
            shapeProvider = System.Convert.ToInt32(System.Enum.Parse(sourceEnum, "ShapeProvider"));

            providerType = casterType.Assembly.GetType("UnityEngine.Rendering.Universal.ShadowShape2DProvider_Collider2D");
            if (providerType == null)
            {
                Debug.LogError("[TimeKiller Setup] URP changed: ShadowShape2DProvider_Collider2D not found. " +
                               "Verified against URP 17.4.0. Nothing was modified.");
                return false;
            }
            return true;
        }

        /// The child that hosts an overflow caster. Found-or-created, never
        /// rebuilt, and pinned to the parent's transform so the collider's shape
        /// lands in exactly the same place it would on the parent itself.
        const string ChildPrefix = "__Shadow_";

        static GameObject ShadowChild(GameObject host, int index)
        {
            string name = ChildPrefix + index;
            var existing = host.transform.Find(name);
            if (existing != null) return existing.gameObject;

            var child = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(child, "Cast 2D Shadows");
            child.transform.SetParent(host.transform, false);
            child.transform.localPosition = Vector3.zero;
            child.transform.localRotation = Quaternion.identity;
            child.transform.localScale = Vector3.one;
            return child;
        }

        /// Find-or-add the caster on `host` and point it at `col`. Never destroys.
        static void Wire(GameObject host, Collider2D col, ShadowConfig config, int shapeProvider, System.Type providerType)
        {
            var caster = host.GetComponent<ShadowCaster2D>();
            if (caster == null) caster = Undo.AddComponent<ShadowCaster2D>(host);

            var so = new SerializedObject(caster);
            so.FindProperty("m_ShadowCastingSource").intValue = shapeProvider;
            so.FindProperty("m_ShadowShape2DComponent").objectReferenceValue = col;
            so.FindProperty("m_ShadowShape2DProvider").managedReferenceValue = System.Activator.CreateInstance(providerType);
            so.FindProperty("m_CastingOption").intValue = (int)ShadowCaster2D.ShadowCastingOptions.CastShadow;
            so.FindProperty("m_CastsShadows").boolValue = true;

            // Apply to every sorting layer the project defines, so a caster keeps
            // working if art later moves to a new layer.
            var layers = so.FindProperty("m_ApplyToSortingLayers");
            var all = SortingLayer.layers;
            layers.arraySize = all.Length;
            for (int i = 0; i < all.Length; i++) layers.GetArrayElementAtIndex(i).intValue = all[i].id;

            so.ApplyModifiedProperties();
        }

        static int EnableLights(ShadowConfig config)
        {
            int count = 0;
            foreach (var light in Object.FindObjectsByType<Light2D>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                // A global light has no position, so URP cannot cast from it.
                if (light.lightType == Light2D.LightType.Global) continue;
                if (config.castingLightNames != null && config.castingLightNames.Length > 0 &&
                    !config.castingLightNames.Any(n => light.gameObject.name.Contains(n))) continue;

                Undo.RecordObject(light, "Cast 2D Shadows");
                light.shadowsEnabled = true;
                light.shadowIntensity = config.shadowIntensity;
                light.shadowSoftness = config.shadowSoftness;
                EditorUtility.SetDirty(light);
                count++;
            }
            return count;
        }

        /// Writes the torch strength to BOTH the serialized light (what the editor
        /// viewport shows) and FlickerLight2D.baseIntensity (what Play mode shows).
        /// Absolute values, not a multiply-in-place, so re-running is idempotent.
        static int ApplyTorchStrength(ShadowConfig config)
        {
            float lightValue = config.torchLightIntensity * config.torchBoost;
            float flickerValue = config.torchFlickerBase * config.torchBoost;

            int count = 0;
            foreach (var flicker in Object.FindObjectsByType<FlickerLight2D>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var light = flicker.GetComponent<Light2D>();
                if (light == null) continue;

                Undo.RecordObject(light, "Torch Strength");
                light.intensity = lightValue;
                EditorUtility.SetDirty(light);

                var so = new SerializedObject(flicker);
                so.FindProperty("baseIntensity").floatValue = flickerValue;
                so.ApplyModifiedProperties();
                count++;
            }
            return count;
        }

        static ShadowConfig LoadOrCreateConfig()
        {
            var config = AssetDatabase.LoadAssetAtPath<ShadowConfig>(ConfigPath);
            if (config != null) return config;

            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(ConfigPath));
            config = ScriptableObject.CreateInstance<ShadowConfig>();
            AssetDatabase.CreateAsset(config, ConfigPath);
            AssetDatabase.SaveAssets();
            Debug.Log($"[TimeKiller Setup] Created {ConfigPath}");
            return config;
        }
    }
}
