// A boolean walkable/blocked grid sampled from the physics world at NODE points
// aligned to the world integer lines (spacing = cell, default 0.5). Alignment
// matters: this map's wall colliders are thin strips sitting ON integer coords,
// so a coarse grid sampling at half-integer cell centers misses them entirely.
// A node is BLOCKED when a static, non-trigger collider (wall or big furniture)
// overlaps it; dynamic bodies (player, maniac) are ignored so they never block
// themselves. Sampled once at startup — call Rebuild if the world opens (doors).
//
// CONNECTIVITY (2026-07-24): after sampling, we flood-fill from a known-reachable
// seed (the maniac) and BLOCK every walkable cell not connected to it. Reason:
// the empty space OUTSIDE the castle has no wall colliders, so it sampled as
// "walkable" — 71% of the grid was unreachable void. The belief-map search then
// targeted those voids -> FindPath null -> the maniac froze. Dropping the void
// fixes pathing AND the search in one move.
//
// CLEARANCE FIELD (2026-07-25): walkable/blocked alone says nothing about HOW
// close to a wall a cell is, so A* had no reason to prefer a corridor's middle
// and the path smoother happily cut past corners the body could not clear —
// that is why characters ground along walls. We now also store, per cell, the
// distance in cells to the nearest blocked cell. GridPathfinder uses it to
// penalise wall-hugging and to widen its line-of-sight test to the real body;
// NavDebugView paints it so a human can see the same thing.
using System.Collections.Generic;
using UnityEngine;

namespace TimeKiller.Navigation
{
    public class WalkabilityGrid
    {
        readonly Vector2 origin;   // world position of node (0,0) — on an integer line
        readonly float cell;       // node spacing
        readonly float sampleBox;  // the overlap box each node was tested with
        readonly int cols, rows;
        readonly bool[] walkable;
        readonly bool[] solid;      // a real collider sits here, as opposed to merely unreachable
        readonly byte[] clearCells; // cells to the nearest blocked cell (0 = blocked itself)

        public int Cols => cols;
        public int Rows => rows;
        public float CellSize => cell;
        public Vector2 Origin => origin;

        /// The overlap box size nodes were sampled with. A node being walkable only
        /// means a box THIS big fits there — the real wall surface can sit up to
        /// half of it nearer than the blocked node's centre, which is why callers
        /// converting a body radius into cells must add half of this.
        public float SampleBox => sampleBox;

        // ALL layers are sampled on purpose: a solid collider blocks the maniac's
        // body no matter what layer it's on (his own body collides with all
        // layers too). The player/maniac (dynamic) and camera zones (triggers)
        // are filtered out below, so no LayerMask is needed — and none can be
        // misconfigured to silently drop walls (that bug cost us once).
        public WalkabilityGrid(Bounds worldBounds, float cellSize, float clearance, Vector2 reachableSeed)
        {
            cell = cellSize;
            sampleBox = clearance;
            origin = new Vector2(Mathf.Floor(worldBounds.min.x), Mathf.Floor(worldBounds.min.y));
            cols = Mathf.CeilToInt(worldBounds.size.x / cell) + 2;
            rows = Mathf.CeilToInt(worldBounds.size.y / cell) + 2;
            walkable = new bool[cols * rows];
            solid = new bool[cols * rows];
            var box = new Vector2(clearance, clearance);
            for (int y = 0; y < rows; y++)
                for (int x = 0; x < cols; x++)
                {
                    int i = y * cols + x;
                    solid[i] = Blocked(CellCenter(x, y), box);
                    walkable[i] = !solid[i];
                }

            KeepReachableFrom(reachableSeed);
            clearCells = BuildClearanceField();
        }

        // Multi-source BFS out of every blocked cell at once (8-connected, so the
        // result is Chebyshev distance): every wall starts at 0 and each ring of
        // walkable neighbours is one deeper. Runs AFTER the flood-fill on purpose —
        // the dropped void counts as wall, so cells beside the castle's outer edge
        // are correctly reported as tight instead of wide open.
        byte[] BuildClearanceField()
        {
            var field = new byte[cols * rows];
            var frontier = new Queue<Vector2Int>();
            for (int y = 0; y < rows; y++)
                for (int x = 0; x < cols; x++)
                {
                    int i = y * cols + x;
                    if (!walkable[i]) { field[i] = 0; frontier.Enqueue(new Vector2Int(x, y)); }
                    else if (x == 0 || y == 0 || x == cols - 1 || y == rows - 1)
                    {
                        field[i] = 1; frontier.Enqueue(new Vector2Int(x, y)); // off-grid counts as wall
                    }
                    else field[i] = byte.MaxValue;
                }

            while (frontier.Count > 0)
            {
                var c = frontier.Dequeue();
                int d = field[c.y * cols + c.x];
                if (d >= byte.MaxValue - 1) continue;
                for (int dy = -1; dy <= 1; dy++)
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        if (dx == 0 && dy == 0) continue;
                        int nx = c.x + dx, ny = c.y + dy;
                        if (nx < 0 || ny < 0 || nx >= cols || ny >= rows) continue;
                        int ni = ny * cols + nx;
                        if (field[ni] <= d + 1) continue;
                        field[ni] = (byte)(d + 1);
                        frontier.Enqueue(new Vector2Int(nx, ny));
                    }
            }
            return field;
        }

        /// Distance in cells from (x,y) to the nearest blocked cell. 0 = blocked.
        /// Out of bounds reads as 0 so callers treat the world edge as solid.
        public int ClearanceCells(int x, int y) => InBounds(x, y) ? clearCells[y * cols + x] : 0;

        /// True when a real collider sits here. The other reason a cell is not
        /// walkable is that the flood-fill CUT IT OFF — the empty space outside
        /// the castle has no colliders at all and is most of the blocked map.
        /// Telling the two apart matters: "wall" and "nothing there, but you
        /// can't get to it" are different facts about the level.
        public bool IsSolid(int x, int y) => InBounds(x, y) && solid[y * cols + x];

        // Flood-fill (4-neighbour, which matches A*'s no-corner-cut connectivity)
        // from the seed; block every walkable cell it can't reach — the void.
        void KeepReachableFrom(Vector2 seedWorld)
        {
            var seed = NearestWalkable(seedWorld);
            if (!Walkable(seed.x, seed.y)) return; // no floor near the seed — leave the grid untouched

            var reached = new bool[cols * rows];
            var frontier = new Queue<Vector2Int>();
            reached[seed.y * cols + seed.x] = true;
            frontier.Enqueue(seed);
            while (frontier.Count > 0)
            {
                var c = frontier.Dequeue();
                Visit(c.x + 1, c.y, reached, frontier);
                Visit(c.x - 1, c.y, reached, frontier);
                Visit(c.x, c.y + 1, reached, frontier);
                Visit(c.x, c.y - 1, reached, frontier);
            }

            for (int i = 0; i < walkable.Length; i++)
                if (walkable[i] && !reached[i]) walkable[i] = false;
        }

        void Visit(int x, int y, bool[] reached, Queue<Vector2Int> frontier)
        {
            if (x < 0 || y < 0 || x >= cols || y >= rows) return;
            int i = y * cols + x;
            if (reached[i] || !walkable[i]) return;
            reached[i] = true;
            frontier.Enqueue(new Vector2Int(x, y));
        }

        static bool Blocked(Vector2 center, Vector2 box)
        {
            var hits = Physics2D.OverlapBoxAll(center, box, 0f);
            foreach (var h in hits)
            {
                if (h == null || h.isTrigger) continue;
                var rb = h.attachedRigidbody;
                if (rb != null && rb.bodyType != RigidbodyType2D.Static) continue; // dynamic = player/maniac
                return true;
            }
            return false;
        }

        // Node world position — sampled ON the grid lines (no half-cell offset),
        // so integer-aligned wall strips land on nodes instead of between them.
        public Vector2 CellCenter(int x, int y) => origin + new Vector2(x * cell, y * cell);

        public bool InBounds(int x, int y) => x >= 0 && x < cols && y >= 0 && y < rows;
        public bool Walkable(int x, int y) => InBounds(x, y) && walkable[y * cols + x];

        /// Walkability at a world point (for other systems — e.g. the search belief map).
        public bool IsWalkable(Vector2 world) { var c = WorldToCell(world); return Walkable(c.x, c.y); }

        public Vector2Int WorldToCell(Vector2 world)
        {
            Vector2 local = (world - origin) / cell;
            return new Vector2Int(Mathf.RoundToInt(local.x), Mathf.RoundToInt(local.y));
        }

        // Nearest walkable cell to a world point (rings outward) — for when a
        // target sits inside a wall (player pressed into a corner, etc.).
        public Vector2Int NearestWalkable(Vector2 world, int maxRadius = 8)
        {
            var c = WorldToCell(world);
            if (Walkable(c.x, c.y)) return c;
            for (int r = 1; r <= maxRadius; r++)
                for (int dy = -r; dy <= r; dy++)
                    for (int dx = -r; dx <= r; dx++)
                    {
                        if (Mathf.Abs(dx) != r && Mathf.Abs(dy) != r) continue; // ring perimeter only
                        if (Walkable(c.x + dx, c.y + dy)) return new Vector2Int(c.x + dx, c.y + dy);
                    }
            return c;
        }
    }
}
