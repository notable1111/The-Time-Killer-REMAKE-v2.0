// A probability field over the map: "where did the player go?" The maniac's
// search uses it to hunt intelligently instead of sweeping fixed points.
//   Seed    — on losing sight, drop high probability at the last-seen cell,
//             biased forward along the direction the player was fleeing.
//   Step    — each update the belief spreads to adjacent walkable cells (the
//             player could have moved there) and decays a little; uncertainty
//             grows and flows along corridors.
//   Observe — cells the maniac can currently SEE, but the player isn't in, drop
//             to zero: he's confirmed them empty, so belief collapses away from
//             where he's already looked and concentrates on the unchecked.
//   Best    — the highest-probability walkable cell = where to hunt next.
// Pure (walkability + line-of-sight come in as delegates), so it unit-tests
// without a scene. Coarse 1-unit cells — he needs the right *area*, not a pixel.
using System;
using UnityEngine;

namespace TimeKiller.Maniac
{
    public class PlayerBeliefMap
    {
        readonly Vector2 origin;
        readonly float cell;
        readonly int cols, rows;
        readonly float[] prob;
        readonly float[] scratch;
        readonly Func<Vector2, bool> walkable; // world point -> walkable

        public PlayerBeliefMap(Bounds bounds, float cellSize, Func<Vector2, bool> walkable)
        {
            cell = cellSize;
            this.walkable = walkable;
            origin = new Vector2(Mathf.Floor(bounds.min.x), Mathf.Floor(bounds.min.y));
            cols = Mathf.CeilToInt(bounds.size.x / cell) + 2;
            rows = Mathf.CeilToInt(bounds.size.y / cell) + 2;
            prob = new float[cols * rows];
            scratch = new float[cols * rows];
        }

        Vector2 CellCenter(int x, int y) => origin + new Vector2((x + 0.5f) * cell, (y + 0.5f) * cell);
        bool In(int x, int y) => x >= 0 && x < cols && y >= 0 && y < rows;
        bool Walk(int x, int y) => In(x, y) && walkable(CellCenter(x, y));

        /// Drop belief at the last-seen spot, weighted forward along the flee
        /// direction — a long trail, so he COMMITS to hunting where you ran, not
        /// just where he lost you.
        public void Seed(Vector2 lastSeen, Vector2 fleeDir, int forwardCells = 8)
        {
            Array.Clear(prob, 0, prob.Length);
            Stamp(lastSeen, 1f);
            if (fleeDir.sqrMagnitude > 0.001f)
            {
                Vector2 dir = fleeDir.normalized;
                for (int k = 1; k <= forwardCells; k++)
                    Stamp(lastSeen + dir * (k * cell), 1f - 0.08f * k); // long, slow fade ahead
            }
            Normalize();
        }

        void Stamp(Vector2 world, float weight)
        {
            if (weight <= 0f) return;
            var c = WorldToCell(world);
            if (Walk(c.x, c.y)) prob[c.y * cols + c.x] += weight;
        }

        Vector2Int WorldToCell(Vector2 world)
        {
            Vector2 l = (world - origin) / cell;
            return new Vector2Int(Mathf.FloorToInt(l.x), Mathf.FloorToInt(l.y));
        }

        /// Spread to walkable neighbours + decay. keep = how much stays put.
        public void Step(float keep = 0.6f, float decay = 0.98f)
        {
            float spread = (1f - keep) / 4f;
            for (int y = 0; y < rows; y++)
                for (int x = 0; x < cols; x++)
                {
                    int i = y * cols + x;
                    if (!Walk(x, y)) { scratch[i] = 0f; continue; }
                    float v = prob[i] * keep;
                    v += Neighbor(x + 1, y) * spread + Neighbor(x - 1, y) * spread
                       + Neighbor(x, y + 1) * spread + Neighbor(x, y - 1) * spread;
                    scratch[i] = v * decay;
                }
            Array.Copy(scratch, prob, prob.Length);
        }

        // A blocked neighbour reflects its probability back (the player couldn't go into a wall).
        float Neighbor(int x, int y) => Walk(x, y) ? prob[y * cols + x] : 0f;

        /// Zero out cells the eye can see and the player isn't in — he's checked there.
        public void Observe(Vector2 eye, Vector2 facing, float range, float coneHalfDeg,
                            Func<Vector2, Vector2, bool> hasLineOfSight)
        {
            float rangeSq = range * range;
            Vector2 f = facing.sqrMagnitude > 0.001f ? facing.normalized : Vector2.down;
            for (int y = 0; y < rows; y++)
                for (int x = 0; x < cols; x++)
                {
                    int i = y * cols + x;
                    if (prob[i] <= 0f) continue;
                    Vector2 cc = CellCenter(x, y);
                    Vector2 to = cc - eye;
                    if (to.sqrMagnitude > rangeSq) continue;
                    if (Vector2.Angle(f, to) > coneHalfDeg) continue;
                    if (hasLineOfSight == null || hasLineOfSight(eye, cc))
                        prob[i] = 0f; // seen, empty -> he knows you're not here
                }
        }

        /// Highest-probability walkable cell. Returns while ANY belief remains, so
        /// he keeps ranging (as Observe collapses cells he's checked, the peak moves
        /// outward to the unchecked frontier) — give-up is the brain's memory timer,
        /// not a hard mass floor that used to freeze him in place.
        public bool BestTarget(out Vector2 world, float minMass = 0.0002f)
        {
            int best = -1; float bestP = minMass;
            for (int i = 0; i < prob.Length; i++)
                if (prob[i] > bestP) { bestP = prob[i]; best = i; }
            if (best < 0) { world = Vector2.zero; return false; }
            world = CellCenter(best % cols, best / cols);
            return true;
        }

        /// Highest-belief cell at least minDist from 'from', so he STRIDES to a real
        /// destination and scans there instead of micro-stepping cell by cell —
        /// reads as a confident hunter. Falls back to the global best when nothing
        /// far enough carries belief.
        public bool BestTargetBeyond(Vector2 from, float minDist, out Vector2 world, float minMass = 0.0002f)
        {
            int best = -1; float bestP = minMass; float minDistSq = minDist * minDist;
            for (int i = 0; i < prob.Length; i++)
            {
                if (prob[i] <= minMass) continue;
                Vector2 c = CellCenter(i % cols, i / cols);
                if ((c - from).sqrMagnitude < minDistSq) continue;
                if (prob[i] > bestP) { bestP = prob[i]; best = i; }
            }
            if (best >= 0) { world = CellCenter(best % cols, best / cols); return true; }
            return BestTarget(out world, minMass); // nothing far enough — take the global best
        }

        public float TotalMass()
        {
            float m = 0f;
            for (int i = 0; i < prob.Length; i++) m += prob[i];
            return m;
        }

        void Normalize()
        {
            float m = TotalMass();
            if (m <= 0f) return;
            for (int i = 0; i < prob.Length; i++) prob[i] /= m;
        }
    }
}
