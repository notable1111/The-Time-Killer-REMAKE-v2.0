// Obstacle-aware movement for the maniac. Hand it a destination and it paths
// around walls/furniture (grid A*) and feeds the ManiacMotor one waypoint at a
// time, repathing when the destination moves or the path goes stale. This is
// what stops him jamming on pillars: states call Nav.MoveTo instead of steering
// the motor straight at the target. Removable — states fall back to Motor.MoveTo
// if the navigator or its grid is missing.
using System.Collections.Generic;
using TimeKiller.Maniac;
using UnityEngine;

namespace TimeKiller.Navigation
{
    [RequireComponent(typeof(ManiacMotor))]
    public class ManiacNavigator : MonoBehaviour
    {
        [Tooltip("Overlap box per cell — larger = keeps him further off walls (his body radius). All collider layers count as walls; dynamic bodies and triggers are ignored automatically.")]
        [SerializeField] float clearance = 0.9f;
        [Tooltip("Repath when the destination moves at least this far.")]
        [SerializeField] float repathDistance = 1.0f;
        [Tooltip("Also repath at least this often (chasing a moving target).")]
        [SerializeField] float repathInterval = 0.4f;
        [Tooltip("How close to a path waypoint counts as reached.")]
        [SerializeField] float waypointTolerance = 0.35f;

        WalkabilityGrid grid;
        GridPathfinder finder;
        ManiacMotor motor;
        List<Vector2> path;
        int index;
        Vector2 lastDest;
        float nextRepath;

        public bool Ready => grid != null;
        public int PathCount => path != null ? path.Count : 0;
        public Vector2 CurrentWaypoint => (path != null && index < path.Count) ? path[index] : motor.Position;

        void Awake() => motor = GetComponent<ManiacMotor>();

        void Start() => Rebuild();

        /// Re-sample the world (call after the layout changes — opened doors, etc.).
        public void Rebuild()
        {
            var b = ComputeWorldBounds();
            grid = new WalkabilityGrid(b, 0.5f, clearance); // 0.5 spacing, integer-aligned (catches thin wall strips)
            finder = new GridPathfinder(grid);
        }

        static Bounds ComputeWorldBounds()
        {
            var maps = Object.FindObjectsOfType<UnityEngine.Tilemaps.Tilemap>();
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

        /// Move toward dest, pathing around obstacles. States poll ReachedDestination.
        public void MoveTo(Vector2 dest, float moveSpeed)
        {
            if (grid == null || finder == null) { motor.MoveTo(dest, moveSpeed); return; }

            bool needRepath = path == null
                || Vector2.Distance(dest, lastDest) > repathDistance
                || Time.time >= nextRepath;
            if (needRepath)
            {
                lastDest = dest;
                nextRepath = Time.time + repathInterval;
                var p = finder.FindPath(motor.Position, dest);
                if (p != null && p.Count > 0) { path = p; index = 0; }
                else { motor.MoveTo(dest, moveSpeed); return; } // no path — best effort straight
            }

            if (path == null || index >= path.Count) { motor.MoveTo(dest, moveSpeed); return; }
            while (index < path.Count - 1 && Vector2.Distance(motor.Position, path[index]) <= waypointTolerance)
                index++;
            motor.MoveTo(path[index], moveSpeed);
        }

        public bool ReachedDestination(float tol) =>
            path == null || index >= path.Count
            || Vector2.Distance(motor.Position, path[path.Count - 1]) <= tol;

        public void Stop() { path = null; motor.Stop(); }
    }
}
