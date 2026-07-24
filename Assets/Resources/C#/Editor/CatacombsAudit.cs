// Menu: TimeKiller/Setup/32 - Audit Catacombs Scene.
// Read-only. Proves the map and its escape loop are correct, or says exactly
// what is wrong. Run with Catacombs.unity open, any time the map changes.
//
// The collider test is the important one. Note that LDtkToUnity builds the
// Collision layer with CompositeCollider2D geometryType = Outlines, which
// produces EDGE colliders — there is no fill inside a rock mass. So "is this
// wall cell solid?" cannot be answered with a point query; it always returns
// nothing. The meaningful question is instead:
//
//     starting from the player spawn, can anything reach a place it shouldn't?
//
// This replicates WalkabilityGrid exactly (0.5 spacing on integer-aligned
// nodes, 0.9 clearance box, blocked = non-trigger hit on a null/Static body),
// floods from the spawn, and asserts the reachable set is precisely the
// walkable region: no leak out, no room left unreachable.
using System.Collections.Generic;
using System.Linq;
using TimeKiller.Objectives;
using TimeKiller.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace TimeKiller.EditorTools
{
    public static class CatacombsAudit
    {
        const float NodeSpacing = 0.5f;   // WalkabilityGrid's hard-coded cell size
        const float Clearance = 0.9f;     // ManiacNavigator's serialized default

        [MenuItem("TimeKiller/Setup/32 - Audit Catacombs Scene")]
        public static void Run()
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (scene.name != "Catacombs")
            {
                Debug.LogError("[TimeKiller Audit] Open Assets/Scenes/Catacombs.unity first.");
                return;
            }
            var rooms = CatacombsRooms.Load();
            if (rooms == null) return;

            Physics2D.SyncTransforms();
            var log = new System.Text.StringBuilder();
            int failures = 0;

            failures += AuditColliders(rooms, log);
            failures += AuditRoomAccess(rooms, log);
            failures += AuditCamera(rooms, log);
            failures += AuditProps(rooms, log);
            failures += AuditWiring(scene, log);

            string header = failures == 0
                ? "[TimeKiller Audit] PASS — catacombs map and escape loop are consistent.\n"
                : $"[TimeKiller Audit] {failures} PROBLEM(S) FOUND.\n";
            if (failures == 0) Debug.Log(header + log);
            else Debug.LogError(header + log);
        }

        // ---------- 1. colliders ----------

        static int AuditColliders(CatacombsRooms rooms, System.Text.StringBuilder log)
        {
            var floor = rooms.Floor;
            if (floor.Count == 0) { log.AppendLine("FAIL: no floor cells in room data."); return 1; }

            int minX = floor.Min(c => c.x) - 6, maxX = floor.Max(c => c.x) + 7;
            int minY = floor.Min(c => c.y) - 6, maxY = floor.Max(c => c.y) + 7;
            int w = Mathf.CeilToInt((maxX - minX) / NodeSpacing);
            int h = Mathf.CeilToInt((maxY - minY) / NodeSpacing);

            var free = new bool[w, h];
            for (int i = 0; i < w; i++)
                for (int j = 0; j < h; j++)
                    free[i, j] = !Blocked(new Vector2(minX + i * NodeSpacing, minY + j * NodeSpacing), ignoreProps: true);

            var spawn = rooms.Center("stair_hall");
            int si = Mathf.RoundToInt((spawn.x - minX) / NodeSpacing);
            int sj = Mathf.RoundToInt((spawn.y - minY) / NodeSpacing);
            if (si < 0 || sj < 0 || si >= w || sj >= h || !free[si, sj])
            {
                log.AppendLine("FAIL: player spawn is blocked or out of bounds.");
                return 1;
            }

            var seen = new bool[w, h];
            var q = new Queue<Vector2Int>();
            seen[si, sj] = true; q.Enqueue(new Vector2Int(si, sj));
            var dirs = new[] { Vector2Int.right, Vector2Int.left, Vector2Int.up, Vector2Int.down };
            int reached = 0;
            while (q.Count > 0)
            {
                var n = q.Dequeue(); reached++;
                foreach (var d in dirs)
                {
                    int ni = n.x + d.x, nj = n.y + d.y;
                    if (ni < 0 || nj < 0 || ni >= w || nj >= h) continue;
                    if (seen[ni, nj] || !free[ni, nj]) continue;
                    seen[ni, nj] = true; q.Enqueue(new Vector2Int(ni, nj));
                }
            }

            // Leak: a reachable node outside the closed walkable region.
            int leaks = 0; string firstLeak = null;
            for (int i = 0; i < w; i++)
                for (int j = 0; j < h; j++)
                {
                    if (!seen[i, j]) continue;
                    float px = minX + i * NodeSpacing, py = minY + j * NodeSpacing;
                    if (InsideFloorRegion(floor, px, py)) continue;
                    leaks++;
                    if (firstLeak == null) firstLeak = $"({px}, {py})";
                }

            // Coverage: every walkable cell must be reachable from the spawn.
            int unreachable = 0; string firstUn = null;
            foreach (var c in floor)
            {
                int i = Mathf.RoundToInt((c.x + 0.5f - minX) / NodeSpacing);
                int j = Mathf.RoundToInt((c.y + 0.5f - minY) / NodeSpacing);
                if (i < 0 || j < 0 || i >= w || j >= h || !seen[i, j])
                {
                    unreachable++;
                    if (firstUn == null) firstUn = c.ToString();
                }
            }

            log.AppendLine($"map integrity (props ignored): grid {w}x{h}, {reached} nodes reachable from spawn");
            log.AppendLine($"  leaks out of the walkable region : {leaks}" + (firstLeak != null ? $"  first={firstLeak}" : ""));
            log.AppendLine($"  walkable cells unreachable       : {unreachable}/{floor.Count}" + (firstUn != null ? $"  first={firstUn}" : ""));
            return (leaks > 0 ? 1 : 0) + (unreachable > 0 ? 1 : 0);
        }

        static bool Blocked(Vector2 p, bool ignoreProps = false)
        {
            var hits = Physics2D.OverlapBoxAll(p, new Vector2(Clearance, Clearance), 0f);
            foreach (var h in hits)
            {
                if (h.isTrigger) continue;
                var rb = h.attachedRigidbody;
                if (rb != null && rb.bodyType != RigidbodyType2D.Static) continue;
                if (ignoreProps && IsProp(h.transform)) continue;
                return true;
            }
            return false;
        }

        // A solid prop (clock, wardrobe, locked gate) legitimately makes its own
        // footprint unwalkable. That must NOT be confused with a hole in the map,
        // so map-integrity runs with these ignored.
        static readonly string[] PropRoots = { "Clocks", "HidingSpots", "Furniture" };

        static bool IsProp(Transform t)
        {
            for (var cur = t; cur != null; cur = cur.parent)
                if (System.Array.IndexOf(PropRoots, cur.name) >= 0) return true;
            return false;
        }

        // ---------- 1b. room access with props in place ----------
        // The map can be sound while a badly-placed prop still walls a room off.
        // Every room must keep a reachable cell once props are solid.

        static int AuditRoomAccess(CatacombsRooms rooms, System.Text.StringBuilder log)
        {
            var reachable = FloodFloor(rooms, ignoreProps: false);
            int fails = 0;
            foreach (var room in rooms.roomList)
            {
                int open = 0, total = 0;
                for (int cx = room.x; cx < room.x + room.w; cx++)
                    for (int cy = room.y; cy < room.y + room.h; cy++)
                    {
                        var c = new Vector2Int(cx, cy);
                        if (!rooms.Floor.Contains(c)) continue;
                        total++;
                        if (reachable.Contains(c)) open++;
                    }
                if (total == 0) continue;
                if (open == 0)
                {
                    log.AppendLine($"FAIL: room '{room.name}' is completely walled off by props.");
                    fails++;
                }
                else if (open * 2 < total)
                {
                    log.AppendLine($"WARN: room '{room.name}' only {open}/{total} cells reachable.");
                }
            }
            int blockedByProps = rooms.Floor.Count - reachable.Count;
            log.AppendLine($"room access: all {rooms.roomList.Count} rooms reachable; {blockedByProps} cells occupied by solid props (expected: clocks, wardrobes, locked gate)");
            return fails;
        }

        /// <summary>Walkable cells reachable from the player spawn.</summary>
        static HashSet<Vector2Int> FloodFloor(CatacombsRooms rooms, bool ignoreProps)
        {
            var floor = rooms.Floor;
            int minX = floor.Min(c => c.x) - 6, minY = floor.Min(c => c.y) - 6;
            int maxX = floor.Max(c => c.x) + 7, maxY = floor.Max(c => c.y) + 7;
            int w = Mathf.CeilToInt((maxX - minX) / NodeSpacing);
            int h = Mathf.CeilToInt((maxY - minY) / NodeSpacing);
            var free = new bool[w, h];
            for (int i = 0; i < w; i++)
                for (int j = 0; j < h; j++)
                    free[i, j] = !Blocked(new Vector2(minX + i * NodeSpacing, minY + j * NodeSpacing), ignoreProps);

            var spawn = rooms.Center("stair_hall");
            int si = Mathf.RoundToInt((spawn.x - minX) / NodeSpacing);
            int sj = Mathf.RoundToInt((spawn.y - minY) / NodeSpacing);
            var result = new HashSet<Vector2Int>();
            if (si < 0 || sj < 0 || si >= w || sj >= h || !free[si, sj]) return result;

            var seen = new bool[w, h];
            var q = new Queue<Vector2Int>();
            seen[si, sj] = true; q.Enqueue(new Vector2Int(si, sj));
            var dirs = new[] { Vector2Int.right, Vector2Int.left, Vector2Int.up, Vector2Int.down };
            while (q.Count > 0)
            {
                var n = q.Dequeue();
                foreach (var d in dirs)
                {
                    int ni = n.x + d.x, nj = n.y + d.y;
                    if (ni < 0 || nj < 0 || ni >= w || nj >= h) continue;
                    if (seen[ni, nj] || !free[ni, nj]) continue;
                    seen[ni, nj] = true; q.Enqueue(new Vector2Int(ni, nj));
                }
            }
            foreach (var c in floor)
            {
                int i = Mathf.RoundToInt((c.x + 0.5f - minX) / NodeSpacing);
                int j = Mathf.RoundToInt((c.y + 0.5f - minY) / NodeSpacing);
                if (i >= 0 && j >= 0 && i < w && j < h && seen[i, j]) result.Add(c);
            }
            return result;
        }

        static bool InsideFloorRegion(HashSet<Vector2Int> floor, float px, float py)
        {
            int bx = Mathf.FloorToInt(px), by = Mathf.FloorToInt(py);
            for (int cx = bx - 1; cx <= bx + 1; cx++)
                for (int cy = by - 1; cy <= by + 1; cy++)
                    if (floor.Contains(new Vector2Int(cx, cy))
                        && px >= cx && px <= cx + 1 && py >= cy && py <= cy + 1)
                        return true;
            return false;
        }

        // ---------- 1c. camera bounds ----------
        // CinemachineConfiner2D clamps the camera so the whole VIEWPORT fits
        // inside the bounding shape. If the shape is smaller than the viewport
        // the confiner cannot satisfy it and parks the camera on whatever region
        // is big enough — the player then walks around off-screen. This shipped
        // once (room-sized zones of 8x5 vs a 15x7 viewport); never again.

        static int AuditCamera(CatacombsRooms rooms, System.Text.StringBuilder log)
        {
            var boundsGo = GameObject.Find("CameraBounds");
            if (boundsGo == null) { log.AppendLine("FAIL: CameraBounds missing."); return 1; }
            var composite = boundsGo.GetComponent<CompositeCollider2D>();
            if (composite == null) { log.AppendLine("FAIL: CameraBounds has no CompositeCollider2D."); return 1; }

            var cam = Camera.main;
            if (cam == null) { log.AppendLine("FAIL: no Main Camera."); return 1; }

            // Widest sensible aspect, so the check holds on ultrawide too.
            const float worstAspect = 2.4f;
            float viewH = cam.orthographicSize * 2f;
            float viewW = viewH * worstAspect;
            var b = composite.bounds;

            int fails = 0;
            if (b.size.x < viewW || b.size.y < viewH)
            {
                log.AppendLine($"FAIL: camera bounds {b.size.x:F1}x{b.size.y:F1} are smaller than the viewport "
                             + $"{viewW:F1}x{viewH:F1} (ortho={cam.orthographicSize:F2} @ {worstAspect:F2}) — "
                             + "the confiner will park the camera away from the player.");
                fails++;
            }

            // Every walkable cell must sit inside the bounds, or the camera cannot
            // follow the player into that part of the map.
            int outside = 0; string first = null;
            foreach (var c in rooms.Floor)
            {
                var p = new Vector3(c.x + 0.5f, c.y + 0.5f, b.center.z);
                if (b.Contains(p)) continue;
                outside++;
                if (first == null) first = c.ToString();
            }
            if (outside > 0)
            {
                log.AppendLine($"FAIL: {outside} walkable cells lie outside the camera bounds (first {first}).");
                fails++;
            }

            log.AppendLine($"camera: bounds {b.size.x:F1}x{b.size.y:F1} vs viewport {viewW:F1}x{viewH:F1}, "
                         + $"{rooms.Floor.Count - outside}/{rooms.Floor.Count} walkable cells covered");
            return fails;
        }

        // ---------- 2. prop placement ----------

        static int AuditProps(CatacombsRooms rooms, System.Text.StringBuilder log)
        {
            int fails = 0;
            var occupied = new Dictionary<Vector2Int, string>();

            // (root, must stand on a walkable cell)
            var groups = new[]
            {
                ("Clocks", true), ("HidingSpots", true), ("CatacombsTorches", false),
            };
            foreach (var (rootName, needsFloor) in groups)
            {
                var root = GameObject.Find(rootName);
                if (root == null) { log.AppendLine($"FAIL: '{rootName}' missing."); fails++; continue; }
                int n = 0;
                foreach (Transform child in root.transform)
                {
                    if (child.name == "ObjectiveManager") continue;   // logic node, no position
                    n++;
                    var p = child.position;
                    var cell = new Vector2Int(Mathf.FloorToInt(p.x), Mathf.FloorToInt(p.y));
                    if (needsFloor && !rooms.Floor.Contains(cell))
                    {
                        log.AppendLine($"FAIL: {rootName}/{child.name} at {p} is not on a walkable cell.");
                        fails++;
                    }
                    if (occupied.TryGetValue(cell, out var other))
                    {
                        log.AppendLine($"FAIL: {rootName}/{child.name} shares cell {cell} with {other}.");
                        fails++;
                    }
                    else occupied[cell] = rootName + "/" + child.name;
                }
                log.AppendLine($"props: {rootName} = {n}");
            }

            var maniac = GameObject.Find("Maniac");
            if (maniac == null) { log.AppendLine("FAIL: Maniac missing."); fails++; }
            else if (!rooms.Floor.Contains(new Vector2Int(
                         Mathf.FloorToInt(maniac.transform.position.x),
                         Mathf.FloorToInt(maniac.transform.position.y))))
            { log.AppendLine($"FAIL: Maniac spawn {maniac.transform.position} is not walkable."); fails++; }

            var route = GameObject.Find("ManiacPatrolRoute");
            if (route == null) { log.AppendLine("FAIL: ManiacPatrolRoute missing."); fails++; }
            else
            {
                int bad = 0;
                foreach (Transform wp in route.transform)
                    if (!rooms.Floor.Contains(new Vector2Int(
                            Mathf.FloorToInt(wp.position.x), Mathf.FloorToInt(wp.position.y)))) bad++;
                log.AppendLine($"props: patrol waypoints = {route.transform.childCount}, off-floor = {bad}");
                if (bad > 0) fails++;
            }

            var player = Object.FindAnyObjectByType<PlayerController>();
            if (player == null) { log.AppendLine("FAIL: no PlayerController."); fails++; }
            else if (!rooms.Floor.Contains(new Vector2Int(
                         Mathf.FloorToInt(player.transform.position.x),
                         Mathf.FloorToInt(player.transform.position.y))))
            { log.AppendLine($"FAIL: player spawn {player.transform.position} is not walkable."); fails++; }

            return fails;
        }

        // ---------- 3. escape-loop wiring ----------

        static int AuditWiring(UnityEngine.SceneManagement.Scene scene, System.Text.StringBuilder log)
        {
            int fails = 0;

            int clocks = Object.FindObjectsByType<ClockObjective>(FindObjectsInactive.Exclude, FindObjectsSortMode.None).Length;
            if (clocks == 0) { log.AppendLine("FAIL: no ClockObjective — ObjectiveManager.AllFixed needs Total > 0."); fails++; }

            if (Object.FindAnyObjectByType<ObjectiveManager>() == null) { log.AppendLine("FAIL: no ObjectiveManager."); fails++; }
            if (Object.FindAnyObjectByType<ObjectiveHUD>() == null) { log.AppendLine("FAIL: no ObjectiveHUD."); fails++; }
            if (Object.FindAnyObjectByType<ExitDoor>() == null) { log.AppendLine("FAIL: no ExitDoor."); fails++; }

            var player = Object.FindAnyObjectByType<PlayerController>();
            if (player != null && player.GetComponent<ClockRepair>() == null)
            { log.AppendLine("FAIL: player has no ClockRepair — clocks cannot be repaired."); fails++; }

            bool inBuild = EditorBuildSettings.scenes.Any(s => s.enabled && s.path == scene.path);
            if (!inBuild) { log.AppendLine("FAIL: scene not registered+enabled in Build Settings (restart reloads this scene)."); fails++; }

            log.AppendLine($"wiring: clocks={clocks}, buildSettings={(inBuild ? "registered" : "MISSING")}");
            return fails;
        }
    }
}
