// Walking, for the bot. ScriptedInputSource steers straight at its Target via
// delta.normalized, which grinds along the first wall on any non-convex route —
// so the bot never sets Target to a destination, only to the next WAYPOINT of
// an A* path, which is always in clear line of sight.
//
// Reuses the maniac's own Navigation code (WalkabilityGrid + GridPathfinder)
// unchanged; only the clearance differs, because the player's body is slimmer
// than his and must fit through doorways he has to squeeze past.
//
// Owns a stuck detector: if the body has not moved while a path is active
// (physics shove, a prop that is not on the grid, a closed exit door), it
// repaths once, then reports Failed so the pilot can pick a different goal
// instead of pressing into geometry for the rest of the run.
using System.Collections.Generic;
using TimeKiller.Navigation;
using UnityEngine;

namespace TimeKiller.Testing
{
    public class BotPath
    {
        public enum Result { Following, Arrived, Failed }

        readonly WalkabilityGrid grid;
        readonly GridPathfinder finder;
        readonly Bounds bounds;

        List<Vector2> path;
        int index;
        Vector2 destination;
        Vector2 lastPos;
        float stillSince;
        int repathsAtThisSpot;

        float nextRepath;

        const float WaypointTolerance = 0.35f;
        const float ArriveTolerance = 0.28f;
        const float StuckSeconds = 1.2f;
        const float StuckDistance = 0.12f;
        // Mirrors ManiacNavigator.repathInterval. The maniac's follower has always
        // self-healed on a timer; this one planned once per goal and followed that
        // plan forever, so any drift was permanent.
        const float RepathInterval = 0.4f;

        public Bounds WorldBounds => bounds;
        public Vector2 Destination => destination;
        public bool HasPath => path != null;

        // ---- wall-stuck instrumentation --------------------------------------
        // Body-width-aware pathfinding was verified offline, by sweeping the
        // maniac's collider over 3000 candidate shortcuts. These counters are how
        // the same claim gets tested in a LIVE run, where the maniac shoves, props
        // sit off the grid, and the exit door opens a wall the grid sampled solid.
        //
        // Three numbers, because they fail differently and a single one would
        // hide the interesting case: a body that scrapes constantly but always
        // frees itself has high WedgedSeconds and zero GiveUps, while one that
        // wedges rarely but fatally has the opposite shape. Only the second
        // costs the bot a goal.
        public int StuckEvents { get; private set; }     // times the 1.2s no-movement threshold tripped
        public int StuckGiveUps { get; private set; }    // ...of those, ones that abandoned the destination
        public float WedgedSeconds { get; private set; } // in-game seconds spent confirmed-wedged
        public float TravelSeconds { get; private set; } // in-game seconds with a path active (the denominator)

        /// For NavDebugView — the bot's own picture of the map, painted on F3.
        public WalkabilityGrid Grid => grid;
        public GridPathfinder Finder => finder;
        public IReadOnlyList<Vector2> CurrentPath => path;

        // clearance = the box each node is tested with; bodyRadius = half the
        // player capsule (0.55 x 0.4, see PlayerSetup), which is what decides
        // whether a smoothed shortcut actually fits. Two different jobs — the
        // first asks "can I stand here", the second "can I walk through there".
        public BotPath(Vector2 reachableSeed, float clearance = 0.55f, float bodyRadius = 0.275f)
        {
            bounds = ComputeWorldBounds();
            grid = new WalkabilityGrid(bounds, 0.5f, clearance, reachableSeed);
            finder = new GridPathfinder(grid, bodyRadius);
        }

        public bool IsWalkable(Vector2 world) => grid.IsWalkable(world);
        public Vector2 SnapToWalkable(Vector2 world)
        {
            var c = grid.NearestWalkable(world);
            return grid.CellCenter(c.x, c.y);
        }

        /// Plan a route. False = no route exists (the caller must pick another goal).
        public bool SetDestination(Vector2 from, Vector2 goal)
        {
            repathsAtThisSpot = 0;   // a NEW goal earns a fresh retry budget
            return Plan(from, goal);
        }

        // The same planning, minus the budget reset. Split out because the stuck
        // handler below repaths at the SAME spot for the SAME goal and must not
        // refund its own retry — that is exactly the bug this split fixes.
        bool Plan(Vector2 from, Vector2 goal)
        {
            destination = goal;
            var p = finder.FindPath(from, goal);
            if (p == null || p.Count == 0) { path = null; return false; }
            path = p;
            index = 0;
            lastPos = from;
            stillSince = Time.time;
            nextRepath = Time.time + RepathInterval;
            return true;
        }

        // Maintenance repath: re-plan from where the body ACTUALLY is, on a timer.
        //
        // Deliberately NOT Plan(): that nulls the path on failure and resets
        // stillSince. Nulling would throw away a working route over a momentary
        // grid hiccup, and resetting stillSince every 0.4s would hold the 1.2s
        // stuck detector permanently open — the maintenance would silently disable
        // the very instrument that catches wedging.
        void MaintainPath(Vector2 pos)
        {
            nextRepath = Time.time + RepathInterval;
            var p = finder.FindPath(pos, destination);
            if (p == null || p.Count == 0) return;   // keep what we have; the stuck handler owns real failure
            path = p;
            index = 0;
        }

        /// True when `pos` is already beyond path[i], measured along the direction
        /// we were travelling INTO it. The radius test alone is not enough: at run
        /// speed 4.5 the bot covers 0.30 units per frame at 4x timeScale against a
        /// 0.35 tolerance, so it can step straight OVER a waypoint without ever
        /// landing inside it. index then never advances, the bot is steered
        /// backwards, and because it IS moving 0.3 each way the stuck detector
        /// (0.12) never fires either — it ping-pongs forever, looking healthy.
        ///
        /// Strictly more permissive than the radius test, so no route can be lost.
        bool PassedWaypoint(Vector2 pos, int i)
        {
            // Direction into this waypoint. With no previous waypoint (i == 0) the
            // outgoing leg is the best available guess on a fresh path.
            Vector2 inDir = i > 0 ? path[i] - path[i - 1] : path[i + 1] - path[i];
            if (inDir.sqrMagnitude < 0.0001f) return true;
            return Vector2.Dot(pos - path[i], inDir) > 0f;
        }

        public void Clear() => path = null;

        /// Advance along the path. Returns the waypoint to walk at in `waypoint`.
        /// `dt` is the caller's own game-time step — this is ticked from the
        /// pilot's FixedUpdate, where Time.deltaTime is the render frame's and
        /// would inflate TravelSeconds by the acceleration factor.
        public Result Tick(Vector2 pos, float dt, out Vector2 waypoint)
        {
            waypoint = pos;
            if (path == null) return Result.Failed;

            while (index < path.Count - 1 &&
                   (Vector2.Distance(pos, path[index]) <= WaypointTolerance || PassedWaypoint(pos, index)))
                index++;

            bool last = index >= path.Count - 1;
            if (last && Vector2.Distance(pos, path[index]) <= ArriveTolerance)
            {
                path = null;
                return Result.Arrived;
            }

            // Self-heal on a timer, but never while running the final leg — a
            // repath at the door would churn the path just as Arrived is about to
            // fire, and arrival is the one moment precision matters.
            if (!last && Time.time >= nextRepath) MaintainPath(pos);

            // Travel time is the DENOMINATOR of the stuck rate, and it matters as
            // much as the count: a run that dies at 0:30 and one that lasts 6:40
            // cannot be compared on raw event counts, and any pathfinding change
            // moves run length too. Game seconds, to match GameFlow.RunSeconds.
            TravelSeconds += dt;

            // Stuck: moving input, body not moving. One free repath, then give up
            // on this destination — grinding a wall forever is the failure mode
            // this whole class exists to avoid.
            if (Vector2.Distance(pos, lastPos) > StuckDistance) { lastPos = pos; stillSince = Time.time; }
            else if (Time.time - stillSince > StuckSeconds)
            {
                StuckEvents++;
                WedgedSeconds += Time.time - stillSince;   // the confirmed-wedged spell
                stillSince = Time.time;
                if (++repathsAtThisSpot > 1) { path = null; StuckGiveUps++; return Result.Failed; }
                if (!Plan(pos, destination)) { StuckGiveUps++; return Result.Failed; }
            }

            waypoint = path[index];
            return Result.Following;
        }

        /// A walkable point roughly `distance` away from `from`, biased along `dir`.
        /// Used by the flee state: sample a fan, keep the best reachable one.
        public bool TryFindOpenPoint(Vector2 from, Vector2 dir, float distance, System.Random rng, out Vector2 point)
        {
            point = from;
            float baseAngle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            float best = float.NegativeInfinity;
            bool found = false;
            for (int i = 0; i < 12; i++)
            {
                float a = (baseAngle + (i - 6) * 15f + (float)rng.NextDouble() * 10f) * Mathf.Deg2Rad;
                var candidate = from + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * distance;
                if (!grid.IsWalkable(candidate)) candidate = SnapToWalkable(candidate);
                if (!grid.IsWalkable(candidate)) continue;
                float score = Vector2.Dot((candidate - from).normalized, dir); // how "away" it really is
                if (score > best) { best = score; point = candidate; found = true; }
            }
            return found;
        }

        // Same rule the maniac's navigator uses: the tilemaps define the world.
        static Bounds ComputeWorldBounds()
        {
            var maps = Object.FindObjectsByType<UnityEngine.Tilemaps.Tilemap>(FindObjectsSortMode.None);
            bool any = false;
            Bounds b = new Bounds();
            foreach (var tm in maps)
            {
                var lb = tm.localBounds;
                Vector3 wc = tm.transform.TransformPoint(lb.center);
                var wb = new Bounds(wc, Vector3.Scale(lb.size, tm.transform.lossyScale));
                if (!any) { b = wb; any = true; } else b.Encapsulate(wb);
            }
            if (!any) b = new Bounds(Vector3.zero, new Vector3(80f, 80f, 0f));
            b.Expand(2f);
            return b;
        }
    }
}
