// Menu: TimeKiller/Setup/31 - Place Catacombs Escape Loop.
// Run AFTER Setup/30, with Catacombs.unity open.
//
// STRATEGY: the existing setup scripts (28 clocks, 25 hiding, 20 maniac, 29 run
// flow) build correctly-wired objects at coordinates hand-tuned for the CASTLE.
// Rather than fork or parameterise five proven scripts, this calls each one and
// then RELOCATES what it produced onto catacombs positions derived from
// CatacombsRooms.json. All the component wiring stays exactly as authored, and
// nothing about the castle scene changes.
//
// Every destination is validated against the real floor set before it is used,
// and the whole placement is re-checked at the end (Setup/32 does it standalone).
using System.Collections.Generic;
using System.Linq;
using TimeKiller.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace TimeKiller.EditorTools
{
    public static class CatacombsGameplaySetup
    {
        // Clocks go in the three chambers furthest apart, so repairing all three
        // forces a full circuit of the map with the maniac hunting.
        static readonly string[] ClockRooms = { "west_crypt", "east_ossuary", "loop_east" };
        static readonly float[] ClockBias = { 0.35f, 0.65f, 0.5f };

        // Wardrobes: one per major chamber, so there is always cover within a
        // few seconds' run wherever a chase starts.
        static readonly (string room, float t)[] HideRooms =
        {
            ("west_crypt", 0.8f), ("east_ossuary", 0.2f), ("cistern", 0.5f),
            ("loop_east", 0.5f), ("north_hall", 0.25f), ("north_hall", 0.75f),
        };

        // Patrol ring, ordered as an actual walkable loop around the level.
        // Index 5 is the maniac's spawn (far from the player's stair_hall).
        static readonly string[] PatrolRooms =
        {
            "stair_hall", "nave_s", "east_link", "east_ossuary",
            "loop_east", "loop_north", "north_hall", "nave_n",
            "cistern_link", "cistern", "west_crypt", "west_link",
        };
        const int ManiacSpawnIndex = 5;

        // The maniac's frames are 32x32 at PPU 32 = 1.0 world units, against the
        // player's 32x48 = 1.5. Un-scaled he reads as a child. 1.5x puts him a
        // little taller than the player, which is what makes him threatening.
        const float ManiacScale = 1.5f;

        [MenuItem("TimeKiller/Setup/31 - Place Catacombs Escape Loop")]
        public static void Build()
        {
            if (TimeKiller.EditorTools.SetupGuard.Blocked("31 - Place Catacombs Escape Loop")) return;

            var scene = EditorSceneManager.GetActiveScene();
            if (scene.name != "Catacombs")
            {
                Debug.LogError("[TimeKiller Setup] Open Assets/Scenes/Catacombs.unity first (Setup/30 builds it).");
                return;
            }
            var rooms = CatacombsRooms.Load();
            if (rooms == null) return;

            // Re-runnable: the sub-setups refuse to rebuild when their root
            // already exists (placement is hand-tuned there), so clear ours out
            // first. Nothing else in the scene is touched.
            foreach (var name in new[] { "Clocks", "HidingSpots", "ManiacPatrolRoute",
                                         "Maniac", "CatacombsTorches", "GameFlow" })
            {
                var old = GameObject.Find(name);
                if (old != null) Object.DestroyImmediate(old);
            }

            // ONE reservation set across every prop. Clocks, wardrobes and
            // torches all compete for the same wall segments; without a shared
            // set a wardrobe gets placed standing inside a clock.
            var claimed = new HashSet<Vector2Int>();
            // Sarcophagus niches are decor-only (no collider) but a clock standing
            // inside one looks broken, so reserve them before anything is placed.
            foreach (var d in rooms.Decor) claimed.Add(d);

            // --- clocks + exit gate -------------------------------------------------
            var clockSpots = new List<Vector2>();
            for (int i = 0; i < ClockRooms.Length; i++)
            {
                var spot = rooms.Anchor(ClockRooms[i], ClockBias[i], claimed);
                if (spot == null) return;
                Claim(claimed, spot.Value);
                clockSpots.Add(spot.Value);
            }
            var exitSpot = rooms.NorthWallAnchor("north_hall", 0.5f, claimed);
            if (exitSpot == null) return;
            Claim(claimed, exitSpot.Value);

            TimeKiller.Objectives.EditorTools.ClocksSetup.Build();
            var clocksRoot = GameObject.Find("Clocks");
            if (clocksRoot == null) { Debug.LogError("[TimeKiller Setup] ClocksSetup produced no 'Clocks' object."); return; }
            for (int i = 0; i < clockSpots.Count; i++)
            {
                var clock = clocksRoot.transform.Find("Clock_" + i);
                if (clock != null) clock.position = clockSpots[i];
            }
            var exit = clocksRoot.transform.Find("ExitDoor");
            if (exit != null) exit.position = exitSpot.Value;

            // --- hiding spots -------------------------------------------------------
            TimeKiller.Hiding.EditorTools.HidingSetup.Build();
            var hideRoot = GameObject.Find("HidingSpots");
            if (hideRoot != null)
            {
                int idx = 0;
                foreach (Transform wardrobe in hideRoot.transform)
                {
                    if (idx >= HideRooms.Length) { Object.DestroyImmediate(wardrobe.gameObject); continue; }
                    var spot = rooms.Anchor(HideRooms[idx].room, HideRooms[idx].t, claimed);
                    if (spot != null) { Claim(claimed, spot.Value); wardrobe.position = spot.Value; }
                    idx++;
                }
                // The loop above mutates the collection it walks; sweep any leftovers.
                foreach (var extra in hideRoot.transform.Cast<Transform>()
                             .Skip(HideRooms.Length).ToArray())
                    Object.DestroyImmediate(extra.gameObject);
            }

            // --- maniac + patrol route ---------------------------------------------
            TimeKiller.Maniac.EditorTools.ManiacSetup.Build();
            // Setup/20 leaves the maniac showing the raw, unsliced stage_one.png at
            // Unity's default PPU 100 — 17x31 px becomes 0.17x0.31 world units, a
            // knee-high maniac. Setup/21 slices stage_two.png into 32x32 frames at
            // PPU 32 and installs the animation set, which is what makes him
            // person-sized. Never call 20 without 21.
            TimeKiller.Maniac.EditorTools.ManiacAnimationSetup.Generate();
            var route = GameObject.Find("ManiacPatrolRoute");
            var waypoints = new List<Vector2>();
            for (int i = 0; i < PatrolRooms.Length; i++)
            {
                var c = rooms.Room(PatrolRooms[i]);
                if (c == null) return;
                var p = NearestFloorCentre(rooms, c.Center);
                waypoints.Add(p);
            }
            if (route != null)
            {
                for (int i = 0; i < route.transform.childCount && i < waypoints.Count; i++)
                    route.transform.GetChild(i).position = waypoints[i];
                // Drop any waypoints beyond our ring so the route has no stragglers.
                foreach (var extra in route.transform.Cast<Transform>()
                             .Skip(waypoints.Count).ToArray())
                    Object.DestroyImmediate(extra.gameObject);
            }
            var maniac = GameObject.Find("Maniac");
            if (maniac != null)
            {
                maniac.transform.position = waypoints[Mathf.Min(ManiacSpawnIndex, waypoints.Count - 1)];
                ScaleManiac(maniac, ManiacScale);
            }

            // --- torches: the catacombs are pitch black without them ---------------
            BuildTorches(rooms, claimed);

            // --- run flow (also registers the scene in Build Settings) --------------
            TimeKiller.Core.EditorTools.GameFlowSetup.Build();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[TimeKiller Setup] Catacombs escape loop placed: {clockSpots.Count} clocks, exit gate, hiding spots, maniac + {waypoints.Count} waypoints, torches. Run Setup/32 to audit.");
        }

        // Make the maniac visually bigger WITHOUT changing his physical footprint.
        //
        // His SpriteRenderer and CapsuleCollider2D share the root object, so scaling
        // the transform would scale the hitbox too: 0.60 x 1.5 = 0.90, exactly the
        // WalkabilityGrid clearance the navigation grid is sampled with. He would
        // then be as wide as the corridors the pathfinder thinks he fits through and
        // would snag on every corner. Dividing the capsule by the same factor keeps
        // the world-space collider identical to before.
        static void ScaleManiac(GameObject maniac, float scale)
        {
            maniac.transform.localScale = Vector3.one * scale;
            var capsule = maniac.GetComponent<CapsuleCollider2D>();
            if (capsule != null)
            {
                capsule.size /= scale;
                capsule.offset /= scale;
            }
            var sr = maniac.GetComponent<SpriteRenderer>();
            Debug.Log($"[TimeKiller Setup] Maniac scaled to {scale}x "
                    + $"(visual {(sr != null ? sr.bounds.size.ToString("F2") : "?")}, "
                    + $"collider {(capsule != null ? capsule.bounds.size.ToString("F2") : "?")} — unchanged).");
        }

        // Reserve a prop's cell (and its immediate flanks) so nothing else is
        // anchored on top of it.
        static void Claim(HashSet<Vector2Int> claimed, Vector2 world)
        {
            // Full 3x3, not just the horizontal flanks: props on an EAST or WEST
            // wall stack vertically, so a horizontal-only reservation lets a
            // torch land one cell above a wardrobe on the same wall.
            int cx = Mathf.FloorToInt(world.x), cy = Mathf.FloorToInt(world.y);
            for (int dx = -1; dx <= 1; dx++)
                for (int dy = -1; dy <= 1; dy++)
                    claimed.Add(new Vector2Int(cx + dx, cy + dy));
        }

        // Snap an arbitrary point to the centre of the nearest walkable cell, so
        // a waypoint can never sit inside rock even if a room rect changes.
        static Vector2 NearestFloorCentre(CatacombsRooms rooms, Vector2 want)
        {
            var start = new Vector2Int(Mathf.FloorToInt(want.x), Mathf.FloorToInt(want.y));
            if (rooms.Floor.Contains(start)) return new Vector2(start.x + 0.5f, start.y + 0.5f);
            Vector2Int best = start; float bestD = float.MaxValue;
            foreach (var c in rooms.Floor)
            {
                float d = (new Vector2(c.x + 0.5f, c.y + 0.5f) - want).sqrMagnitude;
                if (d < bestD) { bestD = d; best = c; }
            }
            Debug.LogWarning($"[TimeKiller Setup] Point {want} was not on floor; snapped to {best}.");
            return new Vector2(best.x + 0.5f, best.y + 0.5f);
        }

        // One warm point light per chamber, hung on a north wall. Light2D shadows
        // are off: there are no ShadowCaster2D objects, so they would be a render
        // pass drawing nothing (same reasoning as ClocksSetup.NoShadows).
        static void BuildTorches(CatacombsRooms rooms, HashSet<Vector2Int> claimed)
        {
            var old = GameObject.Find("CatacombsTorches");
            if (old != null) Object.DestroyImmediate(old);
            var root = new GameObject("CatacombsTorches");

            var placements = new (string room, float t)[]
            {
                ("stair_hall", 0.5f), ("west_crypt", 0.15f), ("west_crypt", 0.85f),
                ("east_ossuary", 0.15f), ("east_ossuary", 0.85f), ("cistern", 0.5f),
                ("loop_east", 0.3f), ("north_hall", 0.15f), ("north_hall", 0.5f),
                ("north_hall", 0.85f), ("nave_m", 0.5f), ("nave_s", 0.5f),
            };
            int made = 0;
            foreach (var (room, t) in placements)
            {
                var spot = rooms.Anchor(room, t, claimed);
                if (spot == null) continue;
                Claim(claimed, spot.Value);
                var go = new GameObject("Torch_" + made);
                go.transform.SetParent(root.transform, false);
                go.transform.position = new Vector3(spot.Value.x, spot.Value.y + 0.9f, 0f);
                var light = go.AddComponent<Light2D>();
                light.lightType = Light2D.LightType.Point;
                light.color = new Color(1f, 0.72f, 0.42f);
                light.intensity = 1.15f;
                light.pointLightInnerRadius = 0.5f;
                light.pointLightOuterRadius = 5.5f;
                light.falloffIntensity = 0.75f;
                var so = new SerializedObject(light);
                var prop = so.FindProperty("m_ShadowsEnabled");
                if (prop != null) { prop.boolValue = false; so.ApplyModifiedPropertiesWithoutUndo(); }
                made++;
            }
            Debug.Log($"[TimeKiller Setup] {made} torches placed.");
        }
    }
}
