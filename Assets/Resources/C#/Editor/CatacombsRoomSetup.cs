// Menu: TimeKiller/Setup/8 - Build Catacombs Test Room.
// Slices the Rogue Fantasy Catacombs tileset (16x16), creates Floor/Wall tile
// assets, and builds the minimal test space: Room A + corridor + Room B.
// Floor cells are declared as rectangles; every non-floor cell touching a
// floor cell automatically becomes a solid wall — change the rects, rebuild,
// and the walls follow. Walls collide via TilemapCollider2D + Composite.
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TimeKiller.Player;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace TimeKiller.EditorTools
{
    public static class CatacombsRoomSetup
    {
        const string SheetPath = "Assets/Resources/Outsource/RogueFantasyCatacombs/mainlevbuild.png";
        const string TileFolder = "Assets/Resources/C#/Environment/Configs/Tiles";
        const int Cell = 16, PixelsPerUnit = 16;

        // Tile picks from the sheet, addressed as (column, row-from-top) in 16px cells.
        static readonly Vector2Int FloorPick = new Vector2Int(19, 22); // brown cobblestone (readable against the dark rock)
        static readonly Vector2Int WallPick = new Vector2Int(47, 27);  // near-black solid rock

        // Floor areas in tile coords (x, y, width, height), y grows upward.
        static readonly RectInt RoomA = new RectInt(1, 1, 10, 8);
        static readonly RectInt Corridor = new RectInt(11, 4, 10, 3);
        static readonly RectInt RoomB = new RectInt(21, 1, 10, 8);

        [MenuItem("TimeKiller/Setup/8 - Build Catacombs Test Room")]
        public static void Build()
        {
            SliceSheet();
            var floorTile = MakeTile("Floor_Stone", FloorPick);
            var wallTile = MakeTile("Wall_Rock", WallPick);
            if (floorTile == null || wallTile == null) return;

            var existing = GameObject.Find("CatacombsTestRoom");
            if (existing != null)
            {
                Debug.LogWarning("[TimeKiller Setup] CatacombsTestRoom already exists — delete it to rebuild.");
                return;
            }

            var grid = new GameObject("CatacombsTestRoom", typeof(Grid));
            Undo.RegisterCreatedObjectUndo(grid, "Build Catacombs Room");

            var floorMap = NewTilemap(grid.transform, "Floor", 0);
            var wallMap = NewTilemap(grid.transform, "Walls", 1);

            var floorCells = new HashSet<Vector2Int>();
            foreach (var rect in new[] { RoomA, Corridor, RoomB })
                for (int x = rect.xMin; x < rect.xMax; x++)
                    for (int y = rect.yMin; y < rect.yMax; y++)
                        floorCells.Add(new Vector2Int(x, y));

            foreach (var c in floorCells)
                floorMap.SetTile(new Vector3Int(c.x, c.y, 0), floorTile);

            // Every non-floor neighbor of a floor cell becomes solid rock.
            var wallCells = new HashSet<Vector2Int>();
            foreach (var c in floorCells)
                for (int dx = -1; dx <= 1; dx++)
                    for (int dy = -1; dy <= 1; dy++)
                    {
                        var n = new Vector2Int(c.x + dx, c.y + dy);
                        if (!floorCells.Contains(n)) wallCells.Add(n);
                    }
            foreach (var c in wallCells)
                wallMap.SetTile(new Vector3Int(c.x, c.y, 0), wallTile);

            // Solid collision for the wall layer.
            wallMap.gameObject.AddComponent<TilemapCollider2D>().compositeOperation = Collider2D.CompositeOperation.Merge;
            var body = wallMap.gameObject.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Static;
            wallMap.gameObject.AddComponent<CompositeCollider2D>();

            // Drop the player into Room A's center.
            var player = Object.FindAnyObjectByType<PlayerController>();
            if (player != null)
                player.transform.position = new Vector3(RoomA.center.x, RoomA.center.y, 0f);

            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(grid.scene);
            Debug.Log($"[TimeKiller Setup] Catacombs test room built: {floorCells.Count} floor tiles, {wallCells.Count} wall tiles. Player moved to Room A. Press Play and walk into the walls.");
        }

        static Tilemap NewTilemap(Transform parent, string name, int sortingOrder)
        {
            var go = new GameObject(name, typeof(Tilemap), typeof(TilemapRenderer));
            go.transform.SetParent(parent, false);
            go.GetComponent<TilemapRenderer>().sortingOrder = sortingOrder;
            return go.GetComponent<Tilemap>();
        }

        static Tile MakeTile(string name, Vector2Int pick)
        {
            Directory.CreateDirectory(TileFolder);
            string spriteName = $"mainlevbuild_{pick.x}_{pick.y}";
            var sprite = AssetDatabase.LoadAllAssetsAtPath(SheetPath).OfType<Sprite>()
                .FirstOrDefault(s => s.name == spriteName);
            if (sprite == null)
            {
                Debug.LogError($"[TimeKiller Setup] Sprite {spriteName} not found — was the sheet sliced?");
                return null;
            }

            string path = $"{TileFolder}/{name}.asset";
            var tile = AssetDatabase.LoadAssetAtPath<Tile>(path);
            if (tile == null)
            {
                tile = ScriptableObject.CreateInstance<Tile>();
                AssetDatabase.CreateAsset(tile, path);
            }
            tile.sprite = sprite;
            tile.colliderType = Tile.ColliderType.Grid;
            EditorUtility.SetDirty(tile);
            AssetDatabase.SaveAssets();
            return tile;
        }

        static void SliceSheet()
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(SheetPath);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.spritePixelsPerUnit = PixelsPerUnit;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;

            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(SheetPath);
            int cols = texture.width / Cell, rows = texture.height / Cell;

            var factory = new SpriteDataProviderFactories();
            factory.Init();
            var provider = factory.GetSpriteEditorDataProviderFromObject(importer);
            provider.InitSpriteEditorDataProvider();

            // Named by (col, row-from-top) so picks can be read straight off the image.
            var rects = new List<SpriteRect>();
            for (int col = 0; col < cols; col++)
                for (int rowTop = 0; rowTop < rows; rowTop++)
                    rects.Add(new SpriteRect
                    {
                        name = $"mainlevbuild_{col}_{rowTop}",
                        spriteID = GUID.Generate(),
                        rect = new Rect(col * Cell, texture.height - (rowTop + 1) * Cell, Cell, Cell),
                        alignment = (int)SpriteAlignment.Center,
                        pivot = new Vector2(0.5f, 0.5f)
                    });
            provider.SetSpriteRects(rects.ToArray());

            var nameIds = provider.GetDataProvider<ISpriteNameFileIdDataProvider>();
            if (nameIds != null)
                nameIds.SetNameFileIdPairs(rects.Select(r => new SpriteNameFileIdPair(r.name, r.spriteID)).ToList());

            provider.Apply();
            importer.SaveAndReimport();
        }
    }
}
