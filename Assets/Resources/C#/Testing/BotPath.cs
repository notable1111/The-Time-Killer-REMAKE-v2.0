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

        const float WaypointTolerance = 0.35f;
        const float ArriveTolerance = 0.28f;
        const float StuckSeconds = 1.2f;
        const float StuckDistance = 0.12f;

        public Bounds WorldBounds => bounds;
        public Vector2 Destination => destination;
        public bool HasPath => path != null;

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
            destination = goal;
            var p = finder.FindPath(from, goal);
            if (p == null || p.Count == 0) { path = null; return false; }
            path = p;
            index = 0;
            lastPos = from;
            stillSince = Time.time;
            repathsAtThisSpot = 0;
            return true;
        }

        public void Clear() => path = null;

        /// Advance along the path. Returns the waypoint to walk at in `waypoint`.
        public Result Tick(Vector2 pos, out Vector2 waypoint)
        {
            waypoint = pos;
            if (path == null) return Result.Failed;

            while (index < path.Count - 1 && Vector2.Distance(pos, path[index]) <= WaypointTolerance)
                index++;

            bool last = index >= path.Count - 1;
            if (last && Vector2.Distance(pos, path[index]) <= ArriveTolerance)
            {
                path = null;
                return Result.Arrived;
            }

            // Stuck: moving input, body not moving. One free repath, then give up
            // on this destination — grinding a wall forever is the failure mode
            // this whole class exists to avoid.
            if (Vector2.Distance(pos, lastPos) > StuckDistance) { lastPos = pos; stillSince = Time.time; }
            else if (Time.time - stillSince > StuckSeconds)
            {
                stillSince = Time.time;
                if (++repathsAtThisSpot > 1) { path = null; return Result.Failed; }
                if (!SetDestination(pos, destination)) return Result.Failed;
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
