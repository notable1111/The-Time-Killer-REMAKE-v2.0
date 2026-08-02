// Menu: TimeKiller/Verify/Maniac Chase Grind (measure, don't guess).
//
// THE SUSPICION THIS EXISTS TO SETTLE (2026-08-02): while ChaseState has
// CanSeePlayer it beelines with Motor.MoveTo — raw steering, no pathfinding —
// on the reasoning that "if he can see you, line of sight is clear". But sight
// is a CENTRE-TO-CENTRE linecast and his body is a real capsule roughly 0.6u
// across. Through a doorway seen at an angle, or past a pillar corner, the
// centre line can be clear when the body's swept volume is not. If that happens
// often, he presses into the geometry for as long as he can see you, which is
// exactly the "chases stupidly around walls" look A* was supposed to have ended.
//
// It might also essentially never happen. That is the point of measuring: the
// fix (fall back to the navigator when the beeline stalls) is only worth its
// complexity if the number says so.
//
// WHAT IT MEASURES. Over random pairs of walkable points where he COULD see the
// player — within sightRange, centre linecast clear through sightBlockers —
// sweep his actual body capsule along the straight line he would beeline down,
// and report how often it cannot get there.
//
//   eyes  = config.sightBlockers, matching ManiacPerception exactly.
//   body  = ALL layers minus triggers and dynamic bodies, matching
//           WalkabilityGrid's reasoning: a solid collider stops him whatever
//           layer it is on, and the player/maniac must never count as walls.
//
// The cone is deliberately NOT applied. He aims where he moves, so during a
// chase he is facing the player by construction; requiring the cone here would
// only shrink the sample without changing what it says.
//
// Pure read-only: creates nothing, dirties nothing, and works in EditMode so
// the number is repeatable rather than dependent on how a playthrough went.
// Writes Temp/ChaseGrindReport.txt as well as logging, because read_console is
// not a reliable channel out of this editor.
using System.Collections.Generic;
using System.IO;
using System.Text;
using TimeKiller.Navigation;
using UnityEditor;
using UnityEngine;

namespace TimeKiller.Maniac.EditorTools
{
    public static class ChaseGrindProbe
    {
        const int Seed = 20260802;      // fixed: the same scene must give the same number
        const int TargetPairs = 4000;
        const float MinRange = 0.8f;    // closer than this he is already swinging
        const float StallFraction = 0.95f;  // got less than this far = did not arrive
        const float BodyShrink = 0.95f; // grazing a corridor wall is sliding, not grinding
        const string ReportPath = "Temp/ChaseGrindReport.txt";

        [MenuItem("TimeKiller/Verify/Maniac Chase Grind (measure, don't guess)")]
        public static void Run()
        {
            string report = Report();
            Directory.CreateDirectory(Path.GetDirectoryName(ReportPath));
            File.WriteAllText(ReportPath, report);
            Debug.Log(report);
        }

        public static string Report()
        {
            var sb = new StringBuilder();
            sb.AppendLine("[ChaseGrindProbe] scene = " +
                UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);

            var maniac = Object.FindAnyObjectByType<ManiacController>();
            if (maniac == null) return sb.AppendLine("FAIL: no ManiacController in the open scene.").ToString();

            var cfg = maniac.Config;
            if (cfg == null) return sb.AppendLine("FAIL: the maniac has no ManiacConfig assigned.").ToString();

            var body = maniac.GetComponent<CapsuleCollider2D>();
            if (body == null) return sb.AppendLine("FAIL: the maniac has no CapsuleCollider2D to sweep.").ToString();

            // His real footprint in WORLD units: the collider is authored small and
            // the transform scales him up (1.5x), so the raw size is not what the
            // world sees. Measuring the wrong one here would invent or hide grinds.
            Vector2 scale = new Vector2(Mathf.Abs(maniac.transform.lossyScale.x),
                                        Mathf.Abs(maniac.transform.lossyScale.y));
            Vector2 bodySize = Vector2.Scale(body.size, scale) * BodyShrink;
            sb.AppendLine($"body capsule = {bodySize.x:F2} x {bodySize.y:F2} world units " +
                          $"(collider {body.size.x:F2}x{body.size.y:F2} * scale {scale.x:F2}, " +
                          $"shrunk {BodyShrink:P0} so grazing does not read as grinding)");
            sb.AppendLine($"sightRange = {cfg.sightRange}, sightBlockers = {cfg.sightBlockers.value}");

            float bodyRadius = 0.45f;
            var grid = BuildGrid(maniac, sb, ref bodyRadius);
            if (grid == null) return sb.ToString();

            // The whole fix rests on this: when the straight line fails, does the
            // navigator actually have a route to hand back? If it does not, the
            // fallback is decoration and the honest answer is that the geometry,
            // not the beeline, is the problem.
            var finder = new GridPathfinder(grid, bodyRadius);

            var open = WalkableWorldPoints(grid);
            sb.AppendLine($"walkable sample points = {open.Count}");
            if (open.Count < 2) return sb.AppendLine("FAIL: not enough walkable floor to sample.").ToString();

            Physics2D.SyncTransforms();

            var eyeFilter = new ContactFilter2D { useTriggers = false };
            eyeFilter.SetLayerMask(cfg.sightBlockers);
            var hits = new List<RaycastHit2D>(16);

            Random.State prior = Random.state;
            Random.InitState(Seed);

            int considered = 0, seeable = 0, stalled = 0, navRescued = 0;
            float worstFraction = 1f;
            var examples = new List<string>();
            var byDistance = new int[8];   // stalls bucketed by how far apart they were
            var seenByDistance = new int[8];

            // Generous attempt budget: two random floor cells are usually far
            // apart, so most draws are rejected by the range filter before they
            // ever count. The report prints `considered` so a thin sample cannot
            // hide behind a confident-looking percentage.
            for (int attempt = 0; attempt < TargetPairs * 40 && considered < TargetPairs; attempt++)
            {
                Vector2 from = open[Random.Range(0, open.Count)];
                Vector2 to = open[Random.Range(0, open.Count)];
                float distance = Vector2.Distance(from, to);
                if (distance < MinRange || distance > cfg.sightRange) continue;
                considered++;

                // EYES: exactly ManiacPerception.HasLineOfSight — if this fails he
                // never sets CanSeePlayer and never beelines, so it is not a grind.
                if (Blocked(from, to, eyeFilter, hits)) continue;
                seeable++;

                int bucket = Mathf.Clamp(Mathf.FloorToInt(distance), 0, 7);
                seenByDistance[bucket]++;

                // BODY: can the capsule actually travel that same straight line?
                Vector2 dir = (to - from) / distance;
                float reached = SweepFraction(from, bodySize, dir, distance, maniac.transform);
                if (reached >= StallFraction) continue;

                stalled++;
                byDistance[bucket]++;
                if (reached < worstFraction) worstFraction = reached;

                var route = finder.FindPath(from, to);
                bool rescued = route != null && route.Count > 0;
                if (rescued) navRescued++;
                if (examples.Count < 12)
                    examples.Add($"    from ({from.x:F1}, {from.y:F1}) to ({to.x:F1}, {to.y:F1})" +
                                 $" — {distance:F1}u apart, body stops at {reached:P0}" +
                                 $", navigator {(rescued ? "HAS a route" : "has NO route")}");
            }

            Random.state = prior;

            sb.AppendLine();
            sb.AppendLine($"pairs considered (in range)        = {considered}");
            sb.AppendLine($"of those, he can SEE the player    = {seeable}");
            if (seeable == 0) return sb.AppendLine("FAIL: no line of sight anywhere — check sightBlockers.").ToString();

            float pct = 100f * stalled / seeable;
            sb.AppendLine($"of THOSE, the beeline is blocked   = {stalled}  ({pct:F2}%)");
            sb.AppendLine($"worst case: body reaches only {worstFraction:P0} of the way");
            if (stalled > 0)
                sb.AppendLine($"of the blocked ones, the navigator HAS a route = {navRescued}" +
                              $"  ({100f * navRescued / stalled:F2}%)  <- what the fallback can actually save");

            sb.AppendLine();
            sb.AppendLine("blocked share by separation:");
            for (int i = 0; i < 8; i++)
            {
                if (seenByDistance[i] == 0) continue;
                float p = 100f * byDistance[i] / seenByDistance[i];
                sb.AppendLine($"  {i}-{i + 1}u : {byDistance[i],5} / {seenByDistance[i],5}  ({p:F2}%)");
            }

            if (examples.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("examples (paste into the scene view to look):");
                foreach (var e in examples) sb.AppendLine(e);
            }

            sb.AppendLine();
            sb.AppendLine("VERDICT: " + Verdict(pct));
            return sb.ToString();
        }

        // Thresholds stated up front so the answer is not argued backwards from
        // whatever number happens to come out.
        static string Verdict(float pct) =>
            pct < 1f ? $"{pct:F2}% — the suspicion is WRONG. Sight implies a passable straight line " +
                       "almost always; a nav fallback would add a code path that never runs. Do not fix."
          : pct < 5f ? $"{pct:F2}% — real but rare. Worth a cheap stall-detector fallback, not a rewrite."
          : $"{pct:F2}% — the suspicion is CORRECT and it is common. The beeline needs to " +
            "detect a stall and hand over to the navigator.";

        static WalkabilityGrid BuildGrid(ManiacController maniac, StringBuilder sb, ref float bodyRadius)
        {
            var nav = maniac.GetComponent<ManiacNavigator>();
            if (nav == null) { sb.AppendLine("FAIL: the maniac has no ManiacNavigator."); return null; }

            // Read the navigator's OWN serialized values rather than assuming the
            // defaults — the probe must sample the world he actually navigates.
            float clearance = 0.9f;
            var so = new SerializedObject(nav);
            var prop = so.FindProperty("clearance");
            if (prop != null) clearance = prop.floatValue;
            var radiusProp = so.FindProperty("bodyRadius");
            if (radiusProp != null) bodyRadius = radiusProp.floatValue;
            sb.AppendLine($"nav clearance = {clearance}, bodyRadius = {bodyRadius}");

            var bounds = WorldBounds();
            return new WalkabilityGrid(bounds, 0.5f, clearance, maniac.transform.position);
        }

        // Same bounds rule as ManiacNavigator.ComputeWorldBounds, which is private.
        static Bounds WorldBounds()
        {
            var maps = Object.FindObjectsByType<UnityEngine.Tilemaps.Tilemap>(
                FindObjectsInactive.Exclude, FindObjectsSortMode.None);
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

        static List<Vector2> WalkableWorldPoints(WalkabilityGrid grid)
        {
            var points = new List<Vector2>(4096);
            for (int y = 0; y < grid.Rows; y++)
                for (int x = 0; x < grid.Cols; x++)
                    if (grid.Walkable(x, y)) points.Add(grid.CellCenter(x, y));
            return points;
        }

        // No root-ignore parameter: the sample points are arbitrary floor, not the
        // live actors, and IsDynamic already excludes both bodies wherever they
        // happen to be standing.
        static bool Blocked(Vector2 from, Vector2 to, ContactFilter2D filter, List<RaycastHit2D> hits)
        {
            int count = Physics2D.Linecast(from, to, filter, hits);
            for (int i = 0; i < count; i++)
            {
                var col = hits[i].collider;
                if (col == null || col.isTrigger) continue;
                if (IsDynamic(col)) continue;
                return true;
            }
            return false;
        }

        /// How far along the straight line his body actually gets, 0..1.
        static float SweepFraction(Vector2 origin, Vector2 size, Vector2 dir,
                                   float distance, Transform maniacRoot)
        {
            var results = Physics2D.CapsuleCastAll(origin, size, CapsuleDirection2D.Vertical,
                                                   0f, dir, distance);
            float nearest = 1f;
            foreach (var h in results)
            {
                var col = h.collider;
                if (col == null || col.isTrigger) continue;
                if (col.transform.root == maniacRoot.root) continue;
                if (IsDynamic(col)) continue;           // the player is not a wall
                if (h.fraction <= 0f) continue;         // started overlapped — not a grind
                float f = h.distance / distance;
                if (f < nearest) nearest = f;
            }
            return nearest;
        }

        // Matches WalkabilityGrid.Blocked: only STATIC geometry counts as a wall.
        static bool IsDynamic(Collider2D col)
        {
            var rb = col.attachedRigidbody;
            return rb != null && rb.bodyType != RigidbodyType2D.Static;
        }
    }
}
