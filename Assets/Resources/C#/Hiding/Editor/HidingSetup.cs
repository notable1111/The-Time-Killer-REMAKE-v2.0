// Menu: TimeKiller/Setup/25 - Setup Hiding Spots (wardrobes).
// Imports the AI-generated wardrobe sprites (approved 2026-07-23: both looks,
// room-matched), places six wardrobes along north walls across the wing,
// adds PlayerHiding to the Player and the hidden-view overlay + heartbeat.
using System.IO;
using TimeKiller.Player;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace TimeKiller.Hiding.EditorTools
{
    public static class HidingSetup
    {
        const string ConfigPath = "Assets/Resources/C#/Hiding/Configs/HidingConfig.asset";
        const string SpriteFolder = "Assets/Resources/Assets/Hiding";
        const string SlatsPath = "Assets/Resources/Assets/Hiding/hidden_slats.png";
        const string HeartbeatClipPath = "Assets/Resources/Assets/HealthVfx/heartbeat.wav";
        const string LitMatPath = "Packages/com.unity.render-pipelines.universal/Runtime/Materials/Sprite-Lit-Default.mat";

        // (position, style) — style A = flat-top (hall/guard/kitchen),
        // B = gothic crown (chapel/chamber/library). North-wall bases.
        static readonly (Vector2 pos, char style)[] Spots =
        {
            (new Vector2(14.5f, 9.6f), 'A'),   // hall east corner
            (new Vector2(33.5f, 9.6f), 'A'),   // guardroom
            (new Vector2(33.5f, -0.4f), 'A'),  // kitchen
            (new Vector2(-3.5f, 29.6f), 'B'),  // chapel
            (new Vector2(27f, 29.6f), 'B'),    // great chamber
            (new Vector2(44.5f, 29.6f), 'B'),  // library
        };

        [MenuItem("TimeKiller/Setup/25 - Setup Hiding Spots (wardrobes)")]
        public static void Build()
        {
            var closedA = ImportSprite($"{SpriteFolder}/wardrobeA_closed.png");
            var ajarA = ImportSprite($"{SpriteFolder}/wardrobeA_ajar.png");
            var closedB = ImportSprite($"{SpriteFolder}/wardrobeB_closed.png");
            var ajarB = ImportSprite($"{SpriteFolder}/wardrobeB_ajar.png");
            var slats = ImportSprite(SlatsPath);
            if (closedA == null || ajarA == null || closedB == null || ajarB == null || slats == null)
            {
                Debug.LogError("[TimeKiller Setup] Wardrobe/slat sprites missing — nothing built.");
                return;
            }

            var player = Object.FindAnyObjectByType<PlayerController>();
            if (player == null)
            {
                Debug.LogError("[TimeKiller Setup] No Player in scene.");
                return;
            }

            var config = AssetDatabase.LoadAssetAtPath<HidingConfig>(ConfigPath);
            if (config == null)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(ConfigPath)!);
                config = ScriptableObject.CreateInstance<HidingConfig>();
                AssetDatabase.CreateAsset(config, ConfigPath);
            }

            var lit = AssetDatabase.LoadAssetAtPath<Material>(LitMatPath);

            // PROTECTED: the user hand-places wardrobes in the scene (2026-07-23).
            // If HidingSpots exists, NEVER rebuild it — only rewire the player/VFX
            // side below. Full rebuild = delete the HidingSpots object manually
            // first, then rerun this menu.
            var old = GameObject.Find("HidingSpots");
            if (old != null)
            {
                Debug.Log("[TimeKiller Setup] HidingSpots exists — wardrobe placement PRESERVED (hand-tuned). Rewiring player/VFX only.");
                WirePlayerAndVfx(config);
                return;
            }
            var root = new GameObject("HidingSpots");
            Undo.RegisterCreatedObjectUndo(root, "Hiding Spots");

            foreach (var (pos, style) in Spots)
            {
                var go = new GameObject($"Wardrobe{style}");
                go.transform.SetParent(root.transform, false);
                go.transform.position = pos;
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = style == 'A' ? ajarA : ajarB;
                if (lit != null) sr.sharedMaterial = lit;
                go.AddComponent<UnityEngine.Rendering.SortingGroup>(); // Y-sorts with player/props

                var box = go.AddComponent<BoxCollider2D>();          // solid furniture base
                box.size = new Vector2(1.7f, 0.5f);
                box.offset = new Vector2(0f, -1.2f);

                var spot = go.AddComponent<HidingSpot>();
                var so = new SerializedObject(spot);
                so.FindProperty("closedSprite").objectReferenceValue = style == 'A' ? closedA : closedB;
                so.FindProperty("ajarSprite").objectReferenceValue = style == 'A' ? ajarA : ajarB;
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            WirePlayerAndVfx(config);

            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(root.scene);
            Debug.Log($"[TimeKiller Setup] {Spots.Length} wardrobes placed (starting layout — drag them freely, placement is preserved from now on). E = hide/exit.");
        }

        static void WirePlayerAndVfx(HidingConfig config)
        {
            var player = Object.FindAnyObjectByType<PlayerController>();
            if (player == null) return;

            // Player side.
            var hiding = player.GetComponent<PlayerHiding>();
            if (hiding == null) hiding = Undo.AddComponent<PlayerHiding>(player.gameObject);
            var hso = new SerializedObject(hiding);
            hso.FindProperty("config").objectReferenceValue = config;
            hso.ApplyModifiedPropertiesWithoutUndo();

            // Hidden view overlay + proximity heartbeat.
            var vfxOld = GameObject.Find("HidingVfx");
            if (vfxOld != null) Undo.DestroyObjectImmediate(vfxOld);
            var vfxRoot = new GameObject("HidingVfx");
            Undo.RegisterCreatedObjectUndo(vfxRoot, "Hiding Vfx");

            var canvasGo = new GameObject("SlatsCanvas");
            canvasGo.transform.SetParent(vfxRoot.transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 600; // above the blood canvas (500)
            canvasGo.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;

            var imageGo = new GameObject("Slats");
            imageGo.transform.SetParent(canvasGo.transform, false);
            var rect = imageGo.AddComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            var group = imageGo.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.interactable = false;
            group.blocksRaycasts = false;
            var image = imageGo.AddComponent<Image>();
            image.sprite = ImportSprite(SlatsPath);
            image.raycastTarget = false;

            var audioGo = new GameObject("HiddenHeartbeat");
            audioGo.transform.SetParent(vfxRoot.transform, false);
            var heart = audioGo.AddComponent<AudioSource>();
            heart.playOnAwake = false;
            heart.spatialBlend = 0f;

            var vfx = vfxRoot.AddComponent<HidingVfx>();
            var vso = new SerializedObject(vfx);
            vso.FindProperty("config").objectReferenceValue = config;
            vso.FindProperty("overlay").objectReferenceValue = group;
            vso.FindProperty("heartAudio").objectReferenceValue = heart;
            vso.FindProperty("heartbeatClip").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<AudioClip>(HeartbeatClipPath);
            vso.ApplyModifiedPropertiesWithoutUndo();

            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(vfxRoot.scene);
        }

        static Sprite ImportSprite(string path)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) return null;
            if (importer.textureType != TextureImporterType.Sprite || importer.spritePixelsPerUnit != 16f)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = 16f;
                importer.filterMode = FilterMode.Point;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.mipmapEnabled = false;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }
    }
}
