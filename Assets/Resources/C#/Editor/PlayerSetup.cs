// Player scene setup (Phase 1). Menu: TimeKiller/Setup/4 - Create Player In Scene.
// Creates missing config assets, slices the Adventurer sprite sheets into
// frames, then builds a ready-to-play Player object and a top-down camera.
using System.Collections.Generic;
using System.IO;
using TimeKiller.Core;
using TimeKiller.Player;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TimeKiller.EditorTools
{
    public static class PlayerSetup
    {
        const string SpriteFolder = "Assets/Resources/Outsource/AdventurerCharacter";
        const int CellWidth = 96, CellHeight = 80, PixelsPerUnit = 32;

        [MenuItem("TimeKiller/Setup/4 - Create Player In Scene")]
        public static void CreatePlayer()
        {
            var movementConfig = EnsureConfig<PlayerMovementConfig>("Assets/Resources/C#/Player/Configs/PlayerMovementConfig.asset");
            EnsureConfig<CoreConfig>("Assets/Resources/C#/Core/Configs/CoreConfig.asset");
            SliceAllSheets();

            var existing = Object.FindAnyObjectByType<PlayerController>();
            if (existing != null)
            {
                Debug.LogWarning("[TimeKiller Setup] A Player already exists in the scene — nothing created.");
                return;
            }

            var player = new GameObject("Player");
            Undo.RegisterCreatedObjectUndo(player, "Create Player");

            var body = player.AddComponent<Rigidbody2D>();
            body.gravityScale = 0f;                       // top-down: no falling
            body.freezeRotation = true;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

            var collider = player.AddComponent<CapsuleCollider2D>();
            collider.size = new Vector2(0.55f, 0.4f);     // feet-only, so the body can overlap walls visually (top-down depth)
            collider.offset = new Vector2(0f, -0.35f);

            var renderer = player.AddComponent<SpriteRenderer>();
            var idleSprite = LoadFirstSprite(SpriteFolder + "/FREE_Adventurer 2D Pixel Art/Sprites/IDLE/idle_down.png");
            if (idleSprite != null) renderer.sprite = idleSprite;
            else Debug.LogWarning("[TimeKiller Setup] idle_down sprite not found — Player has no visual.");

            player.AddComponent<KeyboardInputSource>();
            player.AddComponent<PlayerFacing>();
            player.AddComponent<PlayerMotor>();
            var controller = player.AddComponent<PlayerController>();

            var so = new SerializedObject(controller);
            so.FindProperty("config").objectReferenceValue = movementConfig;
            so.ApplyModifiedPropertiesWithoutUndo();

            SetupCamera();

            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Debug.Log("[TimeKiller Setup] Player created: Rigidbody2D + capsule collider, sprite, WASD input, state machine. Camera set to top-down. Press Play and use WASD + Shift.");
        }

        static T EnsureConfig<T>(string path) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null) return asset;

            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            Debug.Log($"[TimeKiller Setup] Created config asset: {path}");
            return asset;
        }

        static void SetupCamera()
        {
            var cam = Camera.main;
            if (cam == null) return;
            cam.orthographic = true;
            cam.orthographicSize = 3f;
            cam.transform.position = new Vector3(0f, 0f, -10f);
            cam.backgroundColor = new Color(0.05f, 0.05f, 0.07f); // near-black horror backdrop
        }

        static Sprite LoadFirstSprite(string texturePath)
        {
            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(texturePath))
                if (asset is Sprite sprite) return sprite;
            return null;
        }

        // Slices every Adventurer sheet into CellWidth x CellHeight frames with
        // point filtering (crisp pixels) using the Sprite Editor data API.
        static void SliceAllSheets()
        {
            if (!AssetDatabase.IsValidFolder(SpriteFolder))
            {
                Debug.LogWarning("[TimeKiller Setup] Sprite folder missing: " + SpriteFolder);
                return;
            }

            var factory = new SpriteDataProviderFactories();
            factory.Init();

            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { SpriteFolder }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);

                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Multiple;
                importer.spritePixelsPerUnit = PixelsPerUnit;
                importer.filterMode = FilterMode.Point;
                importer.textureCompression = TextureImporterCompression.Uncompressed;

                // Frame count comes from the texture width (all sheets are one row).
                var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                if (texture == null) continue;
                int frames = Mathf.Max(1, texture.width / CellWidth);
                string baseName = Path.GetFileNameWithoutExtension(path);

                var provider = factory.GetSpriteEditorDataProviderFromObject(importer);
                provider.InitSpriteEditorDataProvider();

                var rects = new List<SpriteRect>();
                for (int i = 0; i < frames; i++)
                {
                    rects.Add(new SpriteRect
                    {
                        name = $"{baseName}_{i}",
                        spriteID = GUID.Generate(),
                        rect = new Rect(i * CellWidth, 0, CellWidth, CellHeight),
                        alignment = (int)SpriteAlignment.Center,
                        pivot = new Vector2(0.5f, 0.5f)
                    });
                }
                provider.SetSpriteRects(rects.ToArray());

                // Unity 2021.2+ also needs the name/fileId table kept in sync.
                var nameIds = provider.GetDataProvider<ISpriteNameFileIdDataProvider>();
                if (nameIds != null)
                {
                    var pairs = new List<SpriteNameFileIdPair>();
                    foreach (var r in rects) pairs.Add(new SpriteNameFileIdPair(r.name, r.spriteID));
                    nameIds.SetNameFileIdPairs(pairs);
                }

                provider.Apply();
                importer.SaveAndReimport();
            }
            Debug.Log("[TimeKiller Setup] Adventurer sheets sliced (96x80 frames, point filter, PPU 32).");
        }
    }
}
