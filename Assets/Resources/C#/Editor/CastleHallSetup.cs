// Menu: TimeKiller/Setup/9 - Build Castle Hall.
// Assembles a fully dressed 2.5D castle hall from the RF Castle pack's
// pre-sliced 16px sheets. Pieces are addressed as sheet regions
// (column, row-from-top, width, height) read straight off the sheet image and
// painted onto layered tilemaps: floor under rug under walls under player,
// with the south wall band overhead (player walks behind it).
// Also removes the old CatacombsTestRoom and repositions player + camera.
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TimeKiller.Core;
using TimeKiller.Player;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace TimeKiller.EditorTools
{
    public static class CastleHallSetup
    {
        const string MainSheet = "Assets/RF Castle/Sliced/mainlevbuild.png";
        const string DecoSheet = "Assets/RF Castle/Sliced/decorative.png";
        const string LightFolder = "Assets/RF Castle/Animated";
        const string TileFolder = "Assets/Resources/C#/Environment/Configs/CastleTiles";
        const string LightClipPath = "Assets/Resources/C#/Environment/Configs/TorchLight.asset";

        // Hall interior: floor spans x 0..15, y 0..9.
        const int HallW = 16, HallH = 10;

        static Dictionary<string, Sprite> mainLookup, decoLookup;
        static readonly Dictionary<string, Tile> tileCache = new Dictionary<string, Tile>();

        [MenuItem("TimeKiller/Setup/9 - Build Castle Hall")]
        public static void Build()
        {
            if (TimeKiller.EditorTools.SetupGuard.Blocked("9 - Build Castle Hall")) return;

            tileCache.Clear();
            mainLookup = BuildLookup(MainSheet);
            decoLookup = BuildLookup(DecoSheet);
            if (mainLookup == null || decoLookup == null) return;
            Directory.CreateDirectory(TileFolder);

            var old = GameObject.Find("CatacombsTestRoom");
            if (old != null) Undo.DestroyObjectImmediate(old);
            var oldHall = GameObject.Find("CastleHall");
            if (oldHall != null) Undo.DestroyObjectImmediate(oldHall);

            var root = new GameObject("CastleHall", typeof(Grid));
            Undo.RegisterCreatedObjectUndo(root, "Build Castle Hall");

            var floor = NewMap(root.transform, "Floor", -20);
            var rug = NewMap(root.transform, "Rug", -15);
            var wallFace = NewMap(root.transform, "WallFace", -10);
            var wallDecor = NewMap(root.transform, "WallDecor", -9);
            var overhead = NewMap(root.transform, "Overhead", 10);

            // --- Floor: the artist's seamless 8x8 stone patch, tiled whole.
            for (int x = 0; x < HallW; x++)
                for (int y = 0; y < HallH; y++)
                    Set(floor, mainLookup, x % 8, 27 + (y % 8), x, y);

            // --- North wall, bottom-up: 4 brick rows (sheet rows 11..8), the
            // support ledge (row 7), two rows of dark interior, top trim (row 0).
            for (int x = 0; x < HallW; x++)
            {
                for (int i = 0; i < 4; i++)
                    Set(wallFace, mainLookup, 6 + (x % 10), 11 - i, x, 10 + i);
                Set(wallFace, mainLookup, 6 + (x % 10), 7, x, 14);
                Set(wallFace, mainLookup, 10, 2, x, 15);
                Set(wallFace, mainLookup, 10, 2, x, 16);
                Set(wallFace, mainLookup, 6 + (x % 10), 0, x, 17);
            }

            // --- Side walls: same brick texture as the (approved) north wall,
            // two columns thick, with a pillar strip as the inner accent edge.
            for (int y = -3; y <= 17; y++)
            {
                foreach (int x in new[] { -2, -1, HallW, HallW + 1 })
                    Set(wallFace, mainLookup, 7 + Mod(x, 2), 8 + Mod(y, 4), x, y);
                Set(wallDecor, mainLookup, 18, 6 + Mod(17 - y, 6), -1, y);
                Set(wallDecor, mainLookup, 18, 6 + Mod(17 - y, 6), HallW, y);
            }

            // --- South band, drawn over the player: crenellated trim on top of
            // real brick rows — reads as WALL, matching the north treatment.
            for (int x = -2; x <= HallW + 1; x++)
            {
                Set(overhead, mainLookup, 6 + (Mod(x, 10)), 0, x, -1);
                Set(overhead, mainLookup, 6 + (Mod(x, 10)), 8, x, -2);
                Set(overhead, mainLookup, 6 + (Mod(x, 10)), 9, x, -3);
            }

            // --- Window centered on the north wall face.
            PaintRegion(wallDecor, mainLookup, 25, 6, 6, 6, 7, 10);

            // --- Arched doorway (with stairs) on the left of the north wall.
            PaintRegion(wallDecor, mainLookup, 26, 13, 5, 7, 1, 10);

            // --- Dark-teal rug (the moodiest of the pack's eight) in the hall center.
            PaintRegion(rug, decoLookup, 25, 15, 6, 6, 5, 2);

            // --- Red banner right of the window.
            PaintRegion(wallDecor, decoLookup, 0, 0, 3, 4, 13, 12);

            // (Barrels and pots are Y-sorted collidable props — added by Setup/11.)

            AddColliders(root.transform);
            AddTorches(root.transform);

            var player = Object.FindAnyObjectByType<PlayerController>();
            if (player != null) player.transform.position = new Vector3(8f, 4f, 0f);
            if (Camera.main != null) Camera.main.transform.position = new Vector3(8f, 5f, -10f);

            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(root.scene);
            Debug.Log("[TimeKiller Setup] Castle hall built (fully dressed). Press Play.");
        }

        static int Mod(int a, int m) => ((a % m) + m) % m;

        static Tilemap NewMap(Transform parent, string name, int order)
        {
            var go = new GameObject(name, typeof(Tilemap), typeof(TilemapRenderer));
            go.transform.SetParent(parent, false);
            go.GetComponent<TilemapRenderer>().sortingOrder = order;
            return go.GetComponent<Tilemap>();
        }

        // Paint a w*h sheet region so its TOP-LEFT sheet cell lands at world
        // (worldX, worldY + h - 1) — i.e. the region is pasted upright.
        static void PaintRegion(Tilemap map, Dictionary<string, Sprite> lookup,
            int colTL, int rowTL, int w, int h, int worldX, int worldY)
        {
            for (int i = 0; i < w; i++)
                for (int j = 0; j < h; j++)
                    Set(map, lookup, colTL + i, rowTL + j, worldX + i, worldY + (h - 1 - j));
        }

        static void Set(Tilemap map, Dictionary<string, Sprite> lookup, int col, int rowTop, int worldX, int worldY)
        {
            var key = (lookup == mainLookup ? "m" : "d") + $"_{col}_{rowTop}";
            if (!tileCache.TryGetValue(key, out var tile))
            {
                if (!lookup.TryGetValue($"{col},{rowTop}", out var sprite)) return; // empty sheet cell
                string path = $"{TileFolder}/{key}.asset";
                tile = AssetDatabase.LoadAssetAtPath<Tile>(path);
                if (tile == null)
                {
                    tile = ScriptableObject.CreateInstance<Tile>();
                    tile.sprite = sprite;
                    tile.colliderType = Tile.ColliderType.None;
                    AssetDatabase.CreateAsset(tile, path);
                }
                else tile.sprite = sprite;
                tileCache[key] = tile;
            }
            map.SetTile(new Vector3Int(worldX, worldY, 0), tile);
        }

        static Dictionary<string, Sprite> BuildLookup(string sheetPath)
        {
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(sheetPath);
            if (texture == null)
            {
                Debug.LogError($"[TimeKiller Setup] Sheet not found: {sheetPath}");
                return null;
            }
            var dict = new Dictionary<string, Sprite>();
            foreach (var sprite in AssetDatabase.LoadAllAssetsAtPath(sheetPath).OfType<Sprite>())
            {
                int col = (int)sprite.rect.x / 16;
                int rowTop = (texture.height - (int)sprite.rect.y - (int)sprite.rect.height) / 16;
                dict[$"{col},{rowTop}"] = sprite;
            }
            return dict;
        }

        static void AddColliders(Transform root)
        {
            var holder = new GameObject("Colliders");
            holder.transform.SetParent(root, false);
            AddBox(holder, new Vector2(HallW / 2f, 10.5f), new Vector2(HallW + 4, 1f));  // north (at wall face base)
            AddBox(holder, new Vector2(HallW / 2f, -0.55f), new Vector2(HallW + 4, 1.3f)); // south (blocks before the brick band)
            AddBox(holder, new Vector2(-0.45f, 7f), new Vector2(1.3f, 24f));             // west (matches visible wall edge)
            AddBox(holder, new Vector2(HallW + 0.45f, 7f), new Vector2(1.3f, 24f));      // east
        }

        static void AddBox(GameObject holder, Vector2 center, Vector2 size)
        {
            var box = holder.AddComponent<BoxCollider2D>();
            box.offset = center;
            box.size = size;
        }

        static void AddTorches(Transform root)
        {
            // Animated wall lights: lightA01..03 frames played by our SpriteAnimator.
            var frames = new[] { "lightA01", "lightA02", "lightA03" }
                .Select(n => AssetDatabase.LoadAssetAtPath<Sprite>($"{LightFolder}/{n}.png"))
                .Where(s => s != null).ToArray();
            if (frames.Length == 0)
            {
                Debug.LogWarning("[TimeKiller Setup] No light frames found — torches skipped.");
                return;
            }

            var clip = AssetDatabase.LoadAssetAtPath<SpriteAnimationClip>(LightClipPath);
            if (clip == null)
            {
                clip = ScriptableObject.CreateInstance<SpriteAnimationClip>();
                AssetDatabase.CreateAsset(clip, LightClipPath);
            }
            clip.frames = frames;
            clip.framesPerSecond = 6f;
            clip.loop = true;
            EditorUtility.SetDirty(clip);
            AssetDatabase.SaveAssets();

            foreach (float x in new[] { 4.5f, 11.5f })
            {
                var torch = new GameObject("Torch");
                torch.transform.SetParent(root, false);
                torch.transform.position = new Vector3(x, 13f, 0f);
                var renderer = torch.AddComponent<SpriteRenderer>();
                renderer.sprite = frames[0];
                renderer.sortingOrder = -8;
                var animator = torch.AddComponent<SpriteAnimator>();
                var so = new SerializedObject(animator);
                so.FindProperty("playOnStart").objectReferenceValue = clip;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }
    }
}
