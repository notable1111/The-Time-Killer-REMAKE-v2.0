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
using System.Collections.Generic;
using UnityEngine;

namespace TimeKiller.Navigation
{
    public class WalkabilityGrid
    {
        readonly Vector2 origin;   // world position of node (0,0) — on an integer line
        readonly float cell;       // node spacing
        readonly int cols, rows;
        readonly bool[] walkable;

        public int Cols => cols;
        public int Rows => rows;

        // ALL layers are sampled on purpose: a solid collider blocks the maniac's
        // body no matter what layer it's on (his own body collides with all
        // layers too). The player/maniac (dynamic) and camera zones (triggers)
        // are filtered out below, so no LayerMask is needed — and none can be
        // misconfigured to silently drop walls (that bug cost us once).
        public WalkabilityGrid(Bounds worldBounds, float cellSize, float clearance, Vector2 reachableSeed)
        {
            cell = cellSize;
            origin = new Vector2(Mathf.Floor(worldBounds.min.x), Mathf.Floor(worldBounds.min.y));
            cols = Mathf.CeilToInt(worldBounds.size.x / cell) + 2;
            rows = Mathf.CeilToInt(worldBounds.size.y / cell) + 2;
            walkable = new bool[cols * rows];
            var box = new Vector2(clearance, clearance);
            for (int y = 0; y < rows; y++)
                for (int x = 0; x < cols; x++)
                    walkable[y * cols + x] = !Blocked(CellCenter(x, y), box);

            KeepReachableFrom(reachableSeed);
        }

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
