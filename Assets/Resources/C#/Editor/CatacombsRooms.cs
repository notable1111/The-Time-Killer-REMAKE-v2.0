// Loads CatacombsRooms.json — the room rectangles emitted by
// Tools/MapPipeline/mapv3_catacombs.py alongside the map itself.
//
// WHY THIS EXISTS: every other setup script in this project hard-codes world
// coordinates that were hand-tuned for the castle map (clocks at (-7, 29.45),
// wardrobes at (14.5, 9.6), 12 patrol waypoints...). That is fine for a map
// nobody regenerates, but it means a placement can silently end up inside a
// wall. For the catacombs, positions are DERIVED from the same rectangles that
// carved the floor, so a spawn point is walkable by construction.
//
// Coordinates are world space, anchored on the Floor tilemap's min cell at
// (CatacombsSceneSetup.FloorWorldMinX, FloorWorldMinY). A room rect (x, y, w, h)
// covers cells x..x+w-1 and y..y+h-1, i.e. world area [x, x+w] x [y, y+h].
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace TimeKiller.EditorTools
{
    [System.Serializable]
    public class CatacombsRoom
    {
        public string name;
        public int x, y, w, h;

        public Vector2 Center => new Vector2(x + w / 2f, y + h / 2f);
        public Rect WorldRect => new Rect(x, y, w, h);
        public bool Contains(int cx, int cy) =>
            cx >= x && cx < x + w && cy >= y && cy < y + h;
    }

    [System.Serializable]
    public class CatacombsRooms
    {
        public const string JsonPath = "Assets/Resources/Assets/Maps/CatacombsRooms.json";

        public string level;
        public int cellWidth, cellHeight;
        public List<CatacombsRoom> roomList = new List<CatacombsRoom>();
        public int[] floorX = new int[0];
        public int[] floorY = new int[0];
        public int[] decorX = new int[0];
        public int[] decorY = new int[0];

        HashSet<Vector2Int> _floor;
        HashSet<Vector2Int> _decor;

        /// <summary>
        /// Cells occupied by a wall-set sarcophagus niche. Purely visual (decor
        /// carries no collider), but a clock standing inside a tomb looks broken,
        /// so gameplay placement reserves these up front.
        /// </summary>
        public HashSet<Vector2Int> Decor
        {
            get
            {
                if (_decor == null)
                {
                    _decor = new HashSet<Vector2Int>();
                    for (int i = 0; i < decorX.Length && i < decorY.Length; i++)
                        _decor.Add(new Vector2Int(decorX[i], decorY[i]));
                }
                return _decor;
            }
        }

        public static CatacombsRooms Load()
        {
            if (!File.Exists(JsonPath))
            {
                Debug.LogError($"[TimeKiller Setup] {JsonPath} missing — run: python Tools/MapPipeline/mapv3_catacombs.py");
                return null;
            }
            var data = JsonUtility.FromJson<CatacombsRooms>(File.ReadAllText(JsonPath));
            if (data == null || data.roomList.Count == 0)
            {
                Debug.LogError($"[TimeKiller Setup] {JsonPath} parsed empty — regenerate it.");
                return null;
            }
            return data;
        }

        public CatacombsRoom Room(string name)
        {
            var room = roomList.FirstOrDefault(r => r.name == name);
            if (room == null)
                Debug.LogError($"[TimeKiller Setup] No room '{name}' in {JsonPath}. Known: {string.Join(", ", roomList.Select(r => r.name))}");
            return room;
        }

        public Vector3 Center(string name)
        {
            var room = Room(name);
            return room == null ? Vector3.zero : (Vector3)room.Center;
        }

        /// <summary>Every walkable cell, as integer cell coordinates.</summary>
        public HashSet<Vector2Int> Floor
        {
            get
            {
                if (_floor == null)
                {
                    _floor = new HashSet<Vector2Int>();
                    for (int i = 0; i < floorX.Length && i < floorY.Length; i++)
                        _floor.Add(new Vector2Int(floorX[i], floorY[i]));
                }
                return _floor;
            }
        }

        /// <summary>True when the world point sits on a walkable cell.</summary>
        public bool IsWalkable(Vector2 world) =>
            Floor.Contains(new Vector2Int(Mathf.FloorToInt(world.x), Mathf.FloorToInt(world.y)));

        /// <summary>
        /// A floor cell in <paramref name="name"/> whose NORTH neighbour is solid
        /// rock, with one clear cell either side — i.e. a real 3-wide wall segment
        /// an object can stand against.
        ///
        /// This is deliberately NOT "the room's top row": a room's top edge is
        /// often an opening into the next room (west_crypt's north edge is the
        /// doorway to cistern), so anchoring there would put a clock in a
        /// corridor mouth or half inside a wall. Every candidate is checked
        /// against the real floor set instead.
        ///
        /// Authoring rule from ARCHITECTURE.md: objects go on NORTH walls,
        /// because south walls render an Overhead band that draws over anything
        /// standing there.
        /// </summary>
        /// <param name="t">0 = prefer the room's west/south end, 1 = east/north end.</param>
        /// <returns>World position at the cell's centre-bottom, or null if the
        /// room has no valid wall segment on that side.</returns>
        public Vector2? NorthWallAnchor(string name, float t = 0.5f,
                                        HashSet<Vector2Int> exclude = null) =>
            WallAnchor(name, Vector2Int.up, t, exclude);

        /// <summary>
        /// Like <see cref="NorthWallAnchor"/> but falls back to the east and west
        /// walls when a room has no usable north wall — loop_east, for instance,
        /// opens into loop_north along its entire north side.
        ///
        /// South walls are never used: they render an Overhead band that draws
        /// over anything standing there (ARCHITECTURE.md).
        /// </summary>
        /// <param name="exclude">Cells already claimed by another prop. Without
        /// this, two props with different bias values collapse onto the same
        /// best cell in rooms that have few valid wall segments — which is how
        /// a wardrobe ended up standing inside a clock.</param>
        public Vector2? Anchor(string name, float t = 0.5f,
                               HashSet<Vector2Int> exclude = null)
        {
            var dirs = new[] { Vector2Int.up, Vector2Int.right, Vector2Int.left };
            foreach (var d in dirs)
            {
                var hit = WallAnchor(name, d, t, exclude, quiet: true);
                if (hit != null) return hit;
            }
            Debug.LogError($"[TimeKiller Setup] Room '{name}' has no free north/east/west wall segment (all candidates already claimed?).");
            return null;
        }

        /// <summary>
        /// A floor cell in the room whose neighbour in <paramref name="dir"/> is
        /// solid rock across a 3-cell span, with clear floor on both flanks.
        ///
        /// This is deliberately NOT "the room's edge row": a room's edge is often
        /// an opening into the next room (west_crypt's north edge is the doorway
        /// to cistern), so anchoring there would put a clock in a corridor mouth
        /// or half inside a wall. Every candidate is checked against the real
        /// floor set instead.
        /// </summary>
        public Vector2? WallAnchor(string name, Vector2Int dir, float t = 0.5f,
                                   HashSet<Vector2Int> exclude = null, bool quiet = false)
        {
            var room = Room(name);
            if (room == null) return null;

            // Flank direction is perpendicular to the wall normal.
            var flank = new Vector2Int(dir.y, dir.x);
            Vector2Int best = new Vector2Int(int.MinValue, 0);
            float bestScore = float.MaxValue;

            // Preferred position along the wall, and how "deep" a candidate is
            // from the room's edge on the wall's side (shallower is better).
            bool horizontalWall = dir.y != 0;
            float wantAlong = horizontalWall
                ? room.x + Mathf.Lerp(1f, room.w - 2f, Mathf.Clamp01(t))
                : room.y + Mathf.Lerp(1f, room.h - 2f, Mathf.Clamp01(t));

            for (int cy = room.y; cy < room.y + room.h; cy++)
                for (int cx = room.x; cx < room.x + room.w; cx++)
                {
                    var c = new Vector2Int(cx, cy);
                    if (!Floor.Contains(c)) continue;
                    if (exclude != null && exclude.Contains(c)) continue;
                    // Solid rock across the 3-cell span behind the object, so its
                    // ~2-unit-wide collider cannot clip a doorway.
                    if (Floor.Contains(c + dir)) continue;
                    if (Floor.Contains(c + dir - flank)) continue;
                    if (Floor.Contains(c + dir + flank)) continue;
                    // Standing room on both flanks so the player can walk past.
                    if (!Floor.Contains(c - flank)) continue;
                    if (!Floor.Contains(c + flank)) continue;

                    float along = horizontalWall ? cx : cy;
                    float depth = horizontalWall
                        ? (dir.y > 0 ? room.y + room.h - 1 - cy : cy - room.y)
                        : (dir.x > 0 ? room.x + room.w - 1 - cx : cx - room.x);
                    float score = Mathf.Abs(along - wantAlong) + depth * 2f;
                    if (score < bestScore) { bestScore = score; best = c; }
                }

            if (best.x == int.MinValue)
            {
                if (!quiet)
                    Debug.LogError($"[TimeKiller Setup] Room '{name}' has no 3-wide wall segment facing {dir}.");
                return null;
            }
            // x centred on the cell. y sits LOW in the cell so a tall sprite reads
            // as standing ON the floor with its body overlapping the wall behind it.
            // At +0.45 the clock's 2.33-unit sprite ran from +0.31 to +2.64 above the
            // cell — three quarters of it floating up the wall, which is exactly what
            // "the clock is located improperly on the wall" was.
            return new Vector2(best.x + 0.5f, best.y + 0.12f);
        }
    }
}
