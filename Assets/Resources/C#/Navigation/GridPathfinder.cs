// A* over a WalkabilityGrid: 8-directional, corner-cutting forbidden, octile
// heuristic. A line-of-sight "string pull" pass then collapses the stair-step
// cell path into a few natural straight-line waypoints, so the maniac walks
// like a person rather than tracing the grid. Returns world-space waypoints.
//
// BODY WIDTH (2026-07-25). Two reasons characters used to scrape along walls,
// both fixed here, and both fixed in a way that can never make a route fail:
//
//  1. The string pull tested a HAIRLINE: one cell-wide Bresenham line between
//     the two waypoints. A diagonal shortcut whose centre line just clears a
//     corner still drags a 0.55-wide body through it. It now demands the whole
//     body width of clearance — and when a shortcut is refused the smoother
//     simply keeps the corner, i.e. falls back to the raw A* cells, which are
//     body-safe by construction (every node was sampled with the body box).
//     Stricter smoothing can only ever ADD waypoints, never lose a path.
//
//  2. A* had no reason to stay off walls: hugging one costs exactly what
//     walking down the middle costs, and diagonals made hugging cheaper. A
//     small penalty on low-clearance cells now buys the corridor's centre.
//     It is a COST, not a block, so a one-tile doorway is still taken when it
//     is the only way through. The octile heuristic stays admissible because
//     the penalty is never negative.
using System.Collections.Generic;
using UnityEngine;

namespace TimeKiller.Navigation
{
    public class GridPathfinder
    {
        readonly WalkabilityGrid grid;

        // How many cells of clearance this agent's body actually needs. A cell's
        // clearance counts whole cells to the nearest BLOCKED NODE, and the wall
        // surface may sit half a sample box nearer than that node's centre — so
        // the body radius alone would under-count. Minimum 1: a walkable cell.
        readonly int requiredCells;
        readonly int preferredCells;   // the roomier margin A* pays a little to keep

        const float Sqrt2 = 1.41421356f;
        const float HugPenalty = 0.35f;  // extra cost per missing cell of margin, per step

        /// bodyRadius = half the agent's collider width, in world units. 0 keeps
        /// the old hairline behaviour (used by callers that have no body).
        public GridPathfinder(WalkabilityGrid grid, float bodyRadius = 0f)
        {
            this.grid = grid;
            float needed = bodyRadius + grid.SampleBox * 0.5f;
            requiredCells = Mathf.Max(1, Mathf.CeilToInt(needed / grid.CellSize));
            preferredCells = requiredCells + 1;
        }

        static readonly Vector2Int[] Dirs =
        {
            new Vector2Int(1, 0), new Vector2Int(-1, 0), new Vector2Int(0, 1), new Vector2Int(0, -1),
            new Vector2Int(1, 1), new Vector2Int(1, -1), new Vector2Int(-1, 1), new Vector2Int(-1, -1),
        };

        /// Cells of clearance this agent needs — NavDebugView paints anything
        /// below it as "walkable, but this body would scrape".
        public int RequiredClearanceCells => requiredCells;

        // Null when no path exists. Otherwise a list of world points ending at goalWorld.
        // Binary-heap open set + lazy deletion (a node may sit in the heap more than
        // once; stale pops are skipped via the closed set) — O(E log V), fast even on
        // the fine 0.5-spacing grid.
        public List<Vector2> FindPath(Vector2 startWorld, Vector2 goalWorld)
        {
            Vector2Int start = grid.NearestWalkable(startWorld);
            Vector2Int goal = grid.NearestWalkable(goalWorld);
            if (start == goal) return new List<Vector2> { goalWorld };

            var open = new MinHeap();
            var came = new Dictionary<Vector2Int, Vector2Int>();
            var g = new Dictionary<Vector2Int, float> { [start] = 0f };
            var closed = new HashSet<Vector2Int>();
            open.Push(start, Heur(start, goal));

            while (open.Count > 0)
            {
                Vector2Int cur = open.Pop();
                if (cur == goal) return Reconstruct(came, cur, goalWorld);
                if (!closed.Add(cur)) continue; // stale duplicate — already expanded

                foreach (var d in Dirs)
                {
                    var nb = cur + d;
                    if (!grid.Walkable(nb.x, nb.y) || closed.Contains(nb)) continue;
                    if (d.x != 0 && d.y != 0 &&
                        (!grid.Walkable(cur.x + d.x, cur.y) || !grid.Walkable(cur.x, cur.y + d.y)))
                        continue; // no cutting across a wall corner
                    float step = (d.x != 0 && d.y != 0) ? Sqrt2 : 1f;
                    float tentative = g[cur] + step + HugCost(nb);
                    if (!g.TryGetValue(nb, out var gn) || tentative < gn)
                    {
                        came[nb] = cur;
                        g[nb] = tentative;
                        open.Push(nb, tentative + Heur(nb, goal));
                    }
                }
            }
            return null;
        }

        // Tiny binary min-heap of (node, priority). Lazy deletion — no decrease-key.
        class MinHeap
        {
            readonly List<Vector2Int> nodes = new List<Vector2Int>();
            readonly List<float> prio = new List<float>();
            public int Count => nodes.Count;

            public void Push(Vector2Int n, float p)
            {
                nodes.Add(n); prio.Add(p);
                int i = nodes.Count - 1;
                while (i > 0)
                {
                    int parent = (i - 1) / 2;
                    if (prio[parent] <= prio[i]) break;
                    Swap(i, parent); i = parent;
                }
            }

            public Vector2Int Pop()
            {
                Vector2Int root = nodes[0];
                int last = nodes.Count - 1;
                nodes[0] = nodes[last]; prio[0] = prio[last];
                nodes.RemoveAt(last); prio.RemoveAt(last);
                int i = 0, count = nodes.Count;
                while (true)
                {
                    int l = 2 * i + 1, r = 2 * i + 2, small = i;
                    if (l < count && prio[l] < prio[small]) small = l;
                    if (r < count && prio[r] < prio[small]) small = r;
                    if (small == i) break;
                    Swap(i, small); i = small;
                }
                return root;
            }

            void Swap(int a, int b)
            {
                (nodes[a], nodes[b]) = (nodes[b], nodes[a]);
                (prio[a], prio[b]) = (prio[b], prio[a]);
            }
        }

        // Walking within `preferredCells` of a wall costs a little extra, growing
        // as the gap shrinks. Along a long wall this accumulates every step, so
        // the open middle wins easily; through a doorway it is paid once or twice
        // and the route still goes through. Never blocks, so never breaks a path.
        float HugCost(Vector2Int c)
        {
            int clear = grid.ClearanceCells(c.x, c.y);
            int missing = preferredCells - clear;
            return missing > 0 ? HugPenalty * missing : 0f;
        }

        static float Heur(Vector2Int a, Vector2Int b)
        {
            int dx = Mathf.Abs(a.x - b.x), dy = Mathf.Abs(a.y - b.y);
            return (dx + dy) + (Sqrt2 - 2f) * Mathf.Min(dx, dy); // octile
        }

        List<Vector2> Reconstruct(Dictionary<Vector2Int, Vector2Int> came, Vector2Int cur, Vector2 goalWorld)
        {
            var cells = new List<Vector2Int> { cur };
            while (came.TryGetValue(cur, out var prev)) { cur = prev; cells.Add(cur); }
            cells.Reverse();

            var pts = new List<Vector2>(cells.Count);
            foreach (var c in cells) pts.Add(grid.CellCenter(c.x, c.y));
            var pulled = StringPull(pts);
            pulled[pulled.Count - 1] = goalWorld; // finish exactly on the requested goal
            return pulled;
        }

        // Keep a corner only where the straight line to the next kept point would
        // cross a wall — collapses long straights into single hops.
        List<Vector2> StringPull(List<Vector2> pts)
        {
            if (pts.Count <= 2) return pts;
            var result = new List<Vector2> { pts[0] };
            int anchor = 0;
            for (int i = 2; i < pts.Count; i++)
            {
                if (!ClearLine(pts[anchor], pts[i]))
                {
                    result.Add(pts[i - 1]);
                    anchor = i - 1;
                }
            }
            result.Add(pts[pts.Count - 1]);
            return result;
        }

        // Bresenham check between two world points — but asking for the BODY's
        // clearance at every step, not merely "is this cell walkable". A shortcut
        // is only taken when the whole body fits along it; otherwise StringPull
        // keeps the corner and we walk the safe A* cells instead.
        //
        // Each segment's own two end cells are exempt: those are cells the raw A*
        // path already occupies, and one of them is often legitimately tight — the
        // player pressed into a corner, a clock bolted to a wall. Judging them
        // would refuse every shortcut near such a spot and un-smooth the whole
        // path for no gain. What matters is the ground the shortcut CROSSES.
        bool ClearLine(Vector2 a, Vector2 b)
        {
            Vector2Int ca = grid.WorldToCell(a), cb = grid.WorldToCell(b);
            int x0 = ca.x, y0 = ca.y, x1 = cb.x, y1 = cb.y;
            int dx = Mathf.Abs(x1 - x0), dy = Mathf.Abs(y1 - y0);
            int sx = x0 < x1 ? 1 : -1, sy = y0 < y1 ? 1 : -1, err = dx - dy;
            bool first = true;
            while (true)
            {
                if (!grid.Walkable(x0, y0)) return false;
                bool ends = first || (x0 == x1 && y0 == y1);
                if (!ends && grid.ClearanceCells(x0, y0) < requiredCells) return false;
                first = false;
                if (x0 == x1 && y0 == y1) break;
                int e2 = 2 * err;
                if (e2 > -dy) { err -= dy; x0 += sx; }
                if (e2 < dx) { err += dx; y0 += sy; }
            }
            return true;
        }
    }
}
