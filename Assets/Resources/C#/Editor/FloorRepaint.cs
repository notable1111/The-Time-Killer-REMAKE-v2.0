// Menu: TimeKiller/Setup/16 - Repaint Floors (recovery).
// Repaints ONLY the Floor tilemap cells (hall + wing) and the two rugs —
// recovery for the floor-render corruption after the LDtk texture prep.
// Touches no colliders, no other layers.
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace TimeKiller.EditorTools
{
    public static class FloorRepaint
    {
        const string MainSheet = "Assets/Resources/Outsource/RF Castle/Sliced/mainlevbuild.png";
        const string DecoSheet = "Assets/Resources/Outsource/RF Castle/Sliced/decorative.png";
        const string TileFolder = "Assets/Resources/C#/Environment/Configs/CastleTiles";

        static readonly RectInt[] Floors =
        {
            new RectInt(0, 0, 16, 10),   // hall
            new RectInt(16, 4, 2, 3), new RectInt(18, 4, 8, 3), new RectInt(26, 0, 12, 10),
            new RectInt(30, 10, 3, 10), new RectInt(20, 20, 16, 10), new RectInt(2, 22, 18, 3),
            new RectInt(-10, 20, 10, 10), new RectInt(-7, 6, 3, 14), new RectInt(-8, 4, 6, 3),
            new RectInt(-2, 4, 2, 3),
        };

        [MenuItem("TimeKiller/Setup/16 - Repaint Floors (recovery)")]
        public static void Repaint()
        {
            if (TimeKiller.EditorTools.SetupGuard.Blocked("16 - Repaint Floors (recovery)")) return;

            var hall = GameObject.Find("CastleHall");
            var floor = hall?.transform.Find("Floor")?.GetComponent<Tilemap>();
            var rug = hall?.transform.Find("Rug")?.GetComponent<Tilemap>();
            if (floor == null || rug == null) { Debug.LogError("[TimeKiller Setup] Floor/Rug tilemaps not found."); return; }

            var mainLk = Lookup(MainSheet);
            var decoLk = Lookup(DecoSheet);
            if (mainLk == null || decoLk == null) return;

            floor.ClearAllTiles();
            int count = 0;
            foreach (var r in Floors)
                for (int x = r.xMin; x < r.xMax; x++)
                    for (int y = r.yMin; y < r.yMax; y++)
                    {
                        Set(floor, mainLk, "m", Mod(x, 8), 27 + Mod(y, 8), x, y);
                        count++;
                    }

            rug.ClearAllTiles();
            Region(rug, decoLk, "d", 25, 15, 6, 6, 5, 2);
            Region(rug, decoLk, "d", 25, 15, 6, 6, 25, 23);

            floor.RefreshAllTiles();
            rug.RefreshAllTiles();
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(hall.scene);
            Debug.Log($"[TimeKiller Setup] Floors repainted: {count} cells + 2 rugs.");
        }

        static int Mod(int a, int m) => ((a % m) + m) % m;

        static void Region(Tilemap map, Dictionary<string, Sprite> lk, string prefix, int colTL, int rowTL, int w, int h, int wx, int wy)
        {
            for (int i = 0; i < w; i++)
                for (int j = 0; j < h; j++)
                    Set(map, lk, prefix, colTL + i, rowTL + j, wx + i, wy + (h - 1 - j));
        }

        static readonly Dictionary<string, Tile> cache = new Dictionary<string, Tile>();

        static void Set(Tilemap map, Dictionary<string, Sprite> lk, string prefix, int col, int rowTop, int x, int y)
        {
            string key = $"{prefix}_{col}_{rowTop}";
            if (!cache.TryGetValue(key, out var tile) || tile == null)
            {
                if (!lk.TryGetValue($"{col},{rowTop}", out var sprite)) return;
                Directory.CreateDirectory(TileFolder);
                string path = $"{TileFolder}/{key}.asset";
                tile = AssetDatabase.LoadAssetAtPath<Tile>(path);
                if (tile == null)
                {
                    tile = ScriptableObject.CreateInstance<Tile>();
                    tile.sprite = sprite;
                    tile.colliderType = Tile.ColliderType.None;
                    AssetDatabase.CreateAsset(tile, path);
                }
                else
                {
                    tile.sprite = sprite; // re-link in case the reference went stale
                    EditorUtility.SetDirty(tile);
                }
                cache[key] = tile;
            }
            map.SetTile(new Vector3Int(x, y, 0), tile);
        }

        static Dictionary<string, Sprite> Lookup(string sheetPath)
        {
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(sheetPath);
            if (texture == null) { Debug.LogError($"[TimeKiller Setup] Missing sheet {sheetPath}"); return null; }
            var dict = new Dictionary<string, Sprite>();
            foreach (var sprite in AssetDatabase.LoadAllAssetsAtPath(sheetPath).OfType<Sprite>())
            {
                int col = (int)sprite.rect.x / 16;
                int rowTop = (texture.height - (int)sprite.rect.y - (int)sprite.rect.height) / 16;
                dict[$"{col},{rowTop}"] = sprite;
            }
            return dict;
        }
    }
}
