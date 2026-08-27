// Menu: TimeKiller/Setup/13 - Patch Hall Walls (texture fix).
// Surgical repaint of two visual problems — touches ONLY tilemap cells,
// never colliders or props (the team's manual scene tuning stays intact):
//  1. North wall: the two empty black rows (y=15,16) between brick face and
//     top trim become textured dark brick.
//  2. South band: the muddled brick rows under the trim (y=-2,-3) become the
//     same clean dark brick mass.
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace TimeKiller.EditorTools
{
    public static class HallWallPatch
    {
        const string MainSheet = "Assets/Resources/Outsource/RF Castle/Sliced/mainlevbuild.png";
        const string TileFolder = "Assets/Resources/C#/Environment/Configs/CastleTiles";
        const int HallW = 16;

        [MenuItem("TimeKiller/Setup/13 - Patch Hall Walls (texture fix)")]
        public static void Patch()
        {
            if (TimeKiller.EditorTools.SetupGuard.Blocked("13 - Patch Hall Walls (texture fix)")) return;

            var hall = GameObject.Find("CastleHall");
            var wallFace = hall != null ? hall.transform.Find("WallFace")?.GetComponent<Tilemap>() : null;
            var overhead = hall != null ? hall.transform.Find("Overhead")?.GetComponent<Tilemap>() : null;
            if (wallFace == null || overhead == null)
            {
                Debug.LogError("[TimeKiller Setup] CastleHall WallFace/Overhead tilemaps not found.");
                return;
            }

            var lookup = BuildLookup(MainSheet);
            if (lookup == null) return;

            // Dark textured brick patch on the sheet: cols 8..11, rows 24..25.
            // North wall: fill the two black rows with visible texture.
            for (int x = 0; x < HallW; x++)
            {
                SetTile(wallFace, lookup, 8 + (x % 4), 24, x, 15);
                SetTile(wallFace, lookup, 8 + (x % 4), 25, x, 16);
            }

            // South band: clean dark-brick mass under the trim (trim at y=-1 stays).
            for (int x = -2; x <= HallW + 1; x++)
            {
                SetTile(overhead, lookup, 8 + (Mod(x, 4)), 24, x, -2);
                SetTile(overhead, lookup, 8 + (Mod(x, 4)), 25, x, -3);
            }

            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(hall.scene);
            Debug.Log("[TimeKiller Setup] Wall patch applied: north empty band + south band now textured dark brick. No colliders touched.");
        }

        static int Mod(int a, int m) => ((a % m) + m) % m;

        static readonly Dictionary<string, Tile> cache = new Dictionary<string, Tile>();

        static void SetTile(Tilemap map, Dictionary<string, Sprite> lookup, int col, int rowTop, int worldX, int worldY)
        {
            string key = $"m_{col}_{rowTop}";
            if (!cache.TryGetValue(key, out var tile) || tile == null)
            {
                if (!lookup.TryGetValue($"{col},{rowTop}", out var sprite))
                {
                    Debug.LogWarning($"[TimeKiller Setup] Sheet cell {col},{rowTop} is empty — skipped.");
                    return;
                }
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
                cache[key] = tile;
            }
            map.SetTile(new Vector3Int(worldX, worldY, 0), tile);
        }

        static Dictionary<string, Sprite> BuildLookup(string sheetPath)
        {
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(sheetPath);
            if (texture == null)
            {
                Debug.LogError($"[TimeKiller Setup] Sheet not found: {sheetPath} — was RF Castle moved?");
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
    }
}
