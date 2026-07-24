// What the bot has actually SEEN. This is the file that keeps the playtest
// honest: the game happily hands out ClockObjective.All, and a bot that reads
// it walks a perfect salesman route to three clocks it has no business knowing
// about — measuring a level nobody will ever play.
//
// So landmarks move from "exists" to "known" only when the bot has had clear
// line of sight to them from inside sightRange, and the map is covered by a
// coarse exploration grid the bot fills in the same way. Until a clock is
// known, the pilot has nothing to path to and has to go LOOK.
//
// Two exceptions, both earned by something the game genuinely broadcasts:
//   - the exit becomes known when every clock is fixed, because ExitDoor
//     publishes an AlwaysHeard WorldNoiseEvent when the gate crashes open;
//   - everything is known from frame one when the profile sets knowsEverything,
//     which is the labelled cheat, not the default.
using System.Collections.Generic;
using TimeKiller.Core;
using TimeKiller.Hiding;
using TimeKiller.Objectives;
using UnityEngine;

namespace TimeKiller.Testing
{
    public class BotMemory
    {
        const float CoarseCell = 3.5f;      // one "room-ish" patch of exploration
        const float ObserveInterval = 0.2f; // how often we re-scan (linecasts are not free)

        readonly BotPath nav;
        readonly float sightRange;
        readonly bool omniscient;
        readonly Transform self;

        readonly HashSet<ClockObjective> knownClocks = new HashSet<ClockObjective>();
        readonly HashSet<HidingSpot> knownSpots = new HashSet<HidingSpot>();

        readonly int cols, rows;
        readonly Vector2 origin;
        readonly bool[] explorable;   // coarse cell has floor in it
        readonly bool[] explored;     // ...and the bot has laid eyes on it
        float nextObserve;

        public ExitDoor KnownExit { get; private set; }
        public bool Omniscient => omniscient;
        public int ClocksKnown => knownClocks.Count;
        public int ExploredCount { get; private set; }
        public int ExplorableCount { get; private set; }

        public BotMemory(Transform self, BotPath nav, float sightRange, bool omniscient)
        {
            this.self = self;
            this.nav = nav;
            this.sightRange = sightRange;
            this.omniscient = omniscient;

            var b = nav.WorldBounds;
            origin = new Vector2(b.min.x, b.min.y);
            cols = Mathf.CeilToInt(b.size.x / CoarseCell) + 1;
            rows = Mathf.CeilToInt(b.size.y / CoarseCell) + 1;
            explorable = new bool[cols * rows];
            explored = new bool[cols * rows];

            for (int y = 0; y < rows; y++)
                for (int x = 0; x < cols; x++)
                    if (HasFloor(Center(x, y))) { explorable[y * cols + x] = true; ExplorableCount++; }

            if (omniscient) LearnEverything();
        }

        // ---- what the bot can act on ----

        public IEnumerable<ClockObjective> KnownUnfixedClocks()
        {
            foreach (var c in knownClocks)
                if (c != null && !c.IsFixed) yield return c;
        }

        public HidingSpot NearestKnownFreeSpot(Vector2 from, Vector2 awayFrom, float maxDistance)
        {
            HidingSpot best = null;
            float bestScore = float.NegativeInfinity;
            foreach (var s in knownSpots)
            {
                if (s == null || s.Occupied) continue;
                float d = Vector2.Distance(from, s.transform.position);
                if (d > maxDistance) continue;
                // Prefer close spots that are not in the maniac's direction.
                float away = Vector2.Dot(((Vector2)s.transform.position - from).normalized,
                                         (from - awayFrom).normalized);
                float score = -d * 0.35f + away * 2f;
                if (score > bestScore) { bestScore = score; best = s; }
            }
            return best;
        }

        /// The gate crashing open is heard map-wide — that is in-fiction knowledge.
        public void HearExitOpen()
        {
            if (KnownExit == null) KnownExit = Object.FindAnyObjectByType<ExitDoor>();
        }

        // ---- looking around ----

        public void Observe()
        {
            if (omniscient) return;
            if (Time.time < nextObserve) return;
            nextObserve = Time.time + ObserveInterval;

            Vector2 eye = self.position;

            foreach (var c in ClockObjective.All)
                if (c != null && !knownClocks.Contains(c) && CanSee(eye, c.transform.position))
                    knownClocks.Add(c);

            foreach (var s in Object.FindObjectsByType<HidingSpot>(FindObjectsSortMode.None))
                if (s != null && !knownSpots.Contains(s) && CanSee(eye, s.transform.position))
                    knownSpots.Add(s);

            if (KnownExit == null)
            {
                var exit = Object.FindAnyObjectByType<ExitDoor>();
                if (exit != null && CanSee(eye, exit.transform.position)) KnownExit = exit;
            }

            MarkSeenCells(eye);
        }

        void MarkSeenCells(Vector2 eye)
        {
            var c = ToCell(eye);
            int reach = Mathf.CeilToInt(sightRange / CoarseCell);
            for (int dy = -reach; dy <= reach; dy++)
                for (int dx = -reach; dx <= reach; dx++)
                {
                    int x = c.x + dx, y = c.y + dy;
                    if (x < 0 || y < 0 || x >= cols || y >= rows) continue;
                    int i = y * cols + x;
                    if (explored[i] || !explorable[i]) continue;
                    var center = Center(x, y);
                    if (Vector2.Distance(eye, center) > sightRange) continue;
                    if (!CanSee(eye, center)) continue;
                    explored[i] = true;
                    ExploredCount++;
                }
        }

        /// Nearest unexplored patch of floor. routeKnowledge < 1 picks randomly
        /// among the nearest few instead of always the closest — which is what
        /// somebody without the map memorised actually does.
        public bool TryPickExplorationTarget(Vector2 from, float routeKnowledge, System.Random rng, out Vector2 target)
        {
            target = from;
            var candidates = new List<(float d, Vector2 p)>();
            for (int y = 0; y < rows; y++)
                for (int x = 0; x < cols; x++)
                {
                    int i = y * cols + x;
                    if (!explorable[i] || explored[i]) continue;
                    var p = Center(x, y);
                    candidates.Add((Vector2.Distance(from, p), p));
                }
            if (candidates.Count == 0) return false;

            candidates.Sort((a, b) => a.d.CompareTo(b.d));
            int pool = Mathf.Clamp(1 + Mathf.RoundToInt((1f - routeKnowledge) * 10f), 1, candidates.Count);
            target = candidates[rng.Next(pool)].p;
            return true;
        }

        /// Forget one patch as a target (unreachable in practice) so the pilot
        /// does not re-pick it every frame.
        public void GiveUpOn(Vector2 world)
        {
            var c = ToCell(world);
            if (c.x < 0 || c.y < 0 || c.x >= cols || c.y >= rows) return;
            int i = c.y * cols + c.x;
            if (explorable[i] && !explored[i]) { explored[i] = true; ExploredCount++; }
        }

        // ---- helpers ----

        void LearnEverything()
        {
            foreach (var c in ClockObjective.All) if (c != null) knownClocks.Add(c);
            foreach (var s in Object.FindObjectsByType<HidingSpot>(FindObjectsSortMode.None)) knownSpots.Add(s);
            KnownExit = Object.FindAnyObjectByType<ExitDoor>();
            for (int i = 0; i < explored.Length; i++) if (explorable[i]) { explored[i] = true; ExploredCount++; }
        }

        Vector2 Center(int x, int y) => origin + new Vector2((x + 0.5f) * CoarseCell, (y + 0.5f) * CoarseCell);

        Vector2Int ToCell(Vector2 world)
        {
            Vector2 local = (world - origin) / CoarseCell;
            return new Vector2Int(Mathf.FloorToInt(local.x), Mathf.FloorToInt(local.y));
        }

        bool HasFloor(Vector2 center)
        {
            if (nav.IsWalkable(center)) return true;
            // A room's centre can land on a table; probe the quarters before
            // writing the whole patch off as solid rock.
            float q = CoarseCell * 0.28f;
            return nav.IsWalkable(center + new Vector2(q, q)) || nav.IsWalkable(center + new Vector2(-q, q))
                || nav.IsWalkable(center + new Vector2(q, -q)) || nav.IsWalkable(center + new Vector2(-q, -q));
        }

        // Same shape as ManiacPerception.HasLineOfSight: all layers, triggers and
        // our own body excluded. Deliberately NOT excluding furniture — a clock
        // behind a bookcase should stay unnoticed.
        bool CanSee(Vector2 eye, Vector2 point)
        {
            if (Vector2.Distance(eye, point) > sightRange) return false;
            foreach (var hit in Physics2D.LinecastAll(eye, point))
            {
                if (hit.collider == null || hit.collider.isTrigger) continue;
                var root = hit.collider.transform.root;
                if (root == self.root) continue;
                // The thing we are looking AT blocks itself — that is a sighting.
                if (Vector2.Distance(hit.point, point) < 1.2f) continue;
                return false;
            }
            return true;
        }
    }
}
