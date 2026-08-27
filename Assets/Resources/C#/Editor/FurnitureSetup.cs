// Menu: TimeKiller/Setup/26 - Map v2 Furniture (kitchen/armory/library).
// Menu: TimeKiller/Setup/27 - Armory Wardrobe (additive hiding spot).
//
// Places the AI-generated furniture props (Assets/Resources/Assets/Furniture,
// processed by Tools/ArtPipeline/process_props.py) into the three map-v2
// rooms. Lived-in density, MIXED colliders (team decision 2026-07-23):
// big furniture blocks movement, small dressing is walk-through.
//
// PROTECTED: like HidingSpots, the Furniture root is user-draggable after the
// first run — if it exists, this menu refuses to rebuild it. Delete the
// Furniture object manually for a full re-place.
using System.Collections.Generic;
using TimeKiller.Hiding;
using TimeKiller.Lighting;
using UnityEditor;
using UnityEngine;

namespace TimeKiller.EditorTools
{
    public static class FurnitureSetup
    {
        const string SpriteFolder = "Assets/Resources/Assets/Furniture";
        const string BlobPath = "Assets/Resources/Assets/Shadows/blob_shadow.png";
        const string LitMatPath = "Packages/com.unity.render-pipelines.universal/Runtime/Materials/Sprite-Lit-Default.mat";
        const string UnlitMatPath = "Packages/com.unity.render-pipelines.universal/Runtime/Materials/Sprite-Unlit-Default.mat";
        const string WardrobeFolder = "Assets/Resources/Assets/Hiding";

        // name, base position (pivot = bottom-center), solid?, optional flicker light.
        readonly struct Prop
        {
            public readonly string Sprite; public readonly Vector2 Pos;
            public readonly bool Solid; public readonly bool Candle;
            public Prop(string sprite, float x, float y, bool solid, bool candle = false)
            { Sprite = sprite; Pos = new Vector2(x, y); Solid = solid; Candle = candle; }
        }

        // Room interiors (world units, from mapv2_generate.py):
        //   Kitchen x 26-38, y -12..-2 (corridor mouth x 30-33 north, passage west y -8..-6)
        //   Armory  x 40-50, y 0..10   (corridor mouth west wall y 4-7)
        //   Library x 40-52, y 20..30  (corridor mouth west wall y 24-27)
        // Corridor mouths and the existing hand-placed wardrobes stay clear.
        static readonly Prop[] Props =
        {
            // ---- Kitchen ----
            new Prop("kitchen_hearth",   35.5f, -2.55f, solid: true),
            new Prop("kitchen_shelf",    27.6f, -2.45f, solid: true),
            new Prop("kitchen_table",    31.8f, -7.8f,  solid: true),
            new Prop("kitchen_bench",    31.8f, -9.0f,  solid: true),
            new Prop("kitchen_block",    36.6f, -9.2f,  solid: true),
            new Prop("kitchen_barrel",   26.8f, -3.2f,  solid: true),
            new Prop("kitchen_crates",   37.2f, -11.4f, solid: true),
            new Prop("kitchen_firewood", 37.2f, -3.4f,  solid: false),
            new Prop("kitchen_sacks",    27.0f, -11.2f, solid: false),
            new Prop("kitchen_stool",    34.0f, -6.6f,  solid: false),
            // ---- Armory ----
            new Prop("armory_weaponrack", 42.0f, 9.45f, solid: true),
            new Prop("armory_spearrack",  44.8f, 9.45f, solid: true),
            new Prop("armory_armorstand", 47.5f, 9.45f, solid: true),
            new Prop("armory_chest",      49.0f, 6.0f,  solid: true),
            new Prop("armory_anvil",      45.0f, 4.8f,  solid: true),
            new Prop("armory_grindstone", 46.9f, 2.8f,  solid: true),
            new Prop("armory_dummy",      41.6f, 0.9f,  solid: true),
            new Prop("armory_arrowbarrel", 49.2f, 0.9f, solid: false),
            new Prop("armory_shield",     43.8f, 0.75f, solid: false),
            // ---- Library ----
            new Prop("library_bookcase_full", 41.6f, 29.45f, solid: true),
            new Prop("library_bookcase_lean", 47.6f, 29.45f, solid: true),
            new Prop("library_bookcase_full", 49.9f, 29.45f, solid: true),
            new Prop("library_desk",       45.0f, 26.0f,  solid: true),
            new Prop("library_armchair",   47.4f, 25.3f,  solid: true),
            new Prop("library_lectern",    51.2f, 27.0f,  solid: true),
            new Prop("library_globe",      51.3f, 22.5f,  solid: false),
            new Prop("library_candelabra", 40.9f, 22.0f,  solid: false, candle: true),
            new Prop("library_bookstack",  44.2f, 21.0f,  solid: false),
            new Prop("library_sidetable",  49.0f, 20.8f,  solid: false),
        };

        [MenuItem("TimeKiller/Setup/26 - Map v2 Furniture (kitchen, armory, library)")]
        public static void Build()
        {
            if (TimeKiller.EditorTools.SetupGuard.Blocked("26 - Map v2 Furniture (kitchen, armory, library)")) return;

            // PROTECTED: same rule as HidingSpots — never clobber hand-tuned placement.
            if (GameObject.Find("Furniture") != null)
            {
                Debug.Log("[TimeKiller Setup] Furniture exists — placement PRESERVED (hand-tuned). " +
                          "Delete the Furniture object manually for a full re-place.");
                return;
            }

            var lit = AssetDatabase.LoadAssetAtPath<Material>(LitMatPath);
            var unlit = AssetDatabase.LoadAssetAtPath<Material>(UnlitMatPath);
            var blob = AssetDatabase.LoadAssetAtPath<Sprite>(BlobPath);

            var sprites = new Dictionary<string, Sprite>();
            int missing = 0;
            foreach (var prop in Props)
            {
                if (sprites.ContainsKey(prop.Sprite)) continue;
                var s = ImportProp($"{SpriteFolder}/{prop.Sprite}.png");
                if (s == null) { Debug.LogError($"[TimeKiller Setup] Missing sprite {prop.Sprite}.png"); missing++; }
                else sprites[prop.Sprite] = s;
            }
            if (missing > 0) { Debug.LogError($"[TimeKiller Setup] {missing} sprites missing — nothing built."); return; }

            var root = new GameObject("Furniture");
            Undo.RegisterCreatedObjectUndo(root, "Furniture");

            foreach (var prop in Props)
            {
                var sprite = sprites[prop.Sprite];
                var go = new GameObject(prop.Sprite);
                go.transform.SetParent(root.transform, false);
                go.transform.position = prop.Pos; // bottom-center pivot -> Y-sorts by feet
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = sprite;
                if (lit != null) sr.sharedMaterial = lit;
                go.AddComponent<UnityEngine.Rendering.SortingGroup>(); // order 0, custom Y axis decides

                float w = sprite.bounds.size.x;
                if (blob != null)
                {
                    var shadow = new GameObject("Shadow");
                    shadow.transform.SetParent(go.transform, false);
                    shadow.transform.localPosition = new Vector3(0f, 0.05f, 0f);
                    shadow.transform.localScale = new Vector3(w * 1.25f, 0.7f, 1f);
                    var ssr = shadow.AddComponent<SpriteRenderer>();
                    ssr.sprite = blob;
                    ssr.color = new Color(0f, 0f, 0f, 0.5f);
                    ssr.sortingOrder = -1;
                    if (unlit != null) ssr.sharedMaterial = unlit;
                }

                if (prop.Solid)
                {
                    // Base-only collider: you collide with the feet of the
                    // furniture, not its painted height (2.5D rule).
                    var box = go.AddComponent<BoxCollider2D>();
                    box.size = new Vector2(Mathf.Max(0.4f, w * 0.8f), 0.45f);
                    box.offset = new Vector2(0f, 0.22f);
                }

                if (prop.Candle)
                {
                    var lightGo = new GameObject("CandleLight");
                    lightGo.transform.SetParent(go.transform, false);
                    lightGo.transform.localPosition = new Vector3(0f, sprite.bounds.size.y * 0.9f, 0f);
                    var l = lightGo.AddComponent<UnityEngine.Rendering.Universal.Light2D>();
                    l.lightType = UnityEngine.Rendering.Universal.Light2D.LightType.Point;
                    l.color = new Color(1f, 0.62f, 0.28f);
                    l.intensity = 0.8f;
                    l.pointLightInnerRadius = 0.3f;
                    l.pointLightOuterRadius = 3.2f;
                    l.falloffIntensity = 0.85f;
                    lightGo.AddComponent<FlickerLight2D>();
                }
            }

            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(root.scene);
            Debug.Log($"[TimeKiller Setup] {Props.Length} props placed across kitchen/armory/library " +
                      "(starting layout — drag freely, placement is preserved from now on).");
        }

        // The armory is the only room without a wardrobe (kitchen/library kept
        // theirs from Setup/25). Appends ONE WardrobeA child to the existing
        // HidingSpots root — never touches the hand-placed spots.
        [MenuItem("TimeKiller/Setup/27 - Armory Wardrobe (additive hiding spot)")]
        public static void AddArmoryWardrobe()
        {
            if (TimeKiller.EditorTools.SetupGuard.Blocked("27 - Armory Wardrobe (additive hiding spot)")) return;

            var root = GameObject.Find("HidingSpots");
            if (root == null)
            {
                Debug.LogError("[TimeKiller Setup] No HidingSpots in scene — run Setup/25 first.");
                return;
            }
            var armory = new Rect(39f, -1f, 12f, 12f);
            foreach (var spot in root.GetComponentsInChildren<HidingSpot>())
                if (armory.Contains(spot.transform.position))
                {
                    Debug.Log($"[TimeKiller Setup] Armory already has a wardrobe ({spot.name} at {spot.transform.position}) — nothing added.");
                    return;
                }

            var closedA = AssetDatabase.LoadAssetAtPath<Sprite>($"{WardrobeFolder}/wardrobeA_closed.png");
            var ajarA = AssetDatabase.LoadAssetAtPath<Sprite>($"{WardrobeFolder}/wardrobeA_ajar.png");
            var lit = AssetDatabase.LoadAssetAtPath<Material>(LitMatPath);
            if (closedA == null || ajarA == null)
            {
                Debug.LogError("[TimeKiller Setup] Wardrobe sprites missing.");
                return;
            }

            var go = new GameObject("WardrobeA");
            Undo.RegisterCreatedObjectUndo(go, "Armory Wardrobe");
            go.transform.SetParent(root.transform, false);
            go.transform.position = new Vector2(40.9f, 9.45f); // NW corner, clear of the west corridor
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = ajarA;
            if (lit != null) sr.sharedMaterial = lit;
            go.AddComponent<UnityEngine.Rendering.SortingGroup>();
            var box = go.AddComponent<BoxCollider2D>();
            box.size = new Vector2(1.7f, 0.5f);
            box.offset = new Vector2(0f, -1.2f);
            var newSpot = go.AddComponent<HidingSpot>();
            var so = new SerializedObject(newSpot);
            so.FindProperty("closedSprite").objectReferenceValue = closedA;
            so.FindProperty("ajarSprite").objectReferenceValue = ajarA;
            so.ApplyModifiedPropertiesWithoutUndo();

            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(go.scene);
            Debug.Log("[TimeKiller Setup] Armory wardrobe added at (40.9, 9.45) — drag it where you want it.");
        }

        static Sprite ImportProp(string path)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) return null;
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            if (settings.textureType != TextureImporterType.Sprite ||
                settings.spritePixelsPerUnit != 16f ||
                settings.spriteAlignment != (int)SpriteAlignment.BottomCenter)
            {
                settings.textureType = TextureImporterType.Sprite;
                settings.spriteMode = (int)SpriteImportMode.Single;
                settings.spritePixelsPerUnit = 16f;
                settings.filterMode = FilterMode.Point;
                settings.mipmapEnabled = false;
                settings.spriteAlignment = (int)SpriteAlignment.BottomCenter; // feet = position -> Y-sort
                importer.SetTextureSettings(settings);
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }
    }
}
