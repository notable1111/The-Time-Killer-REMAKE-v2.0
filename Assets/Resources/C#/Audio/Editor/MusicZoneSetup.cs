// Menu: TimeKiller/Setup/42 - Place Mystery Zones (approach dread).
//
// The Mystery layer had never fired in real play: its only entry was the demo
// Rect at (0,0,3,3) that AudioConfig itself asks you to replace.
//
// Placement is DERIVED from the scene, not typed in, so it is correct on any map
// — Catacombs included — and stays correct if the clocks move.
//
// Where the dread belongs, and why:
//   CLOCKS      committing to a repair is the most dangerous thing the player
//               chooses to do. It pins them in place, plays a skill-check that
//               owns their attention, and takes many seconds. The zone covers
//               the approach AND the repair itself, so the layer is already
//               running before they commit rather than arriving late.
//   EXIT DOOR   only fires BEFORE the gate opens, because Endgame outranks
//               Mystery and latches on the last clock. So walking up to a door
//               you cannot yet open feels wrong — which it should.
//
// NOT wardrobes: hiding is a reaction, not a plan, and with seven of them spread
// across the map a wardrobe zone would leave Mystery running almost permanently.
// A layer that is always on is a layer that means nothing.
//
// Re-running deletes only the zones this script created (they carry a known name
// prefix) and rebuilds them, so hand-dragged zones with any other name survive.
using System.Collections.Generic;
using TimeKiller.Objectives;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace TimeKiller.Audio.EditorTools
{
    public static class MusicZoneSetup
    {
        const string Prefix = "MysteryZone_";
        const string RootName = "MusicZones";
        static readonly Vector2 ClockZone = new Vector2(11f, 9f);
        static readonly Vector2 DoorZone = new Vector2(10f, 8f);

        [MenuItem("TimeKiller/Setup/42 - Place Mystery Zones (approach dread)")]
        public static void Build()
        {
            var scene = EditorSceneManager.GetActiveScene();
            var log = new List<string>();

            var root = GameObject.Find(RootName);
            if (root == null)
            {
                root = new GameObject(RootName);
                Undo.RegisterCreatedObjectUndo(root, "Create MusicZones root");
                log.Add($"created '{RootName}' root");
            }

            // Remove only OUR zones. A designer's own zone, named anything else,
            // is left completely alone — this script must never be the reason
            // hand-placed work disappears.
            int removed = 0;
            foreach (var existing in Object.FindObjectsByType<MusicZone>(FindObjectsSortMode.None))
            {
                if (existing == null || !existing.gameObject.name.StartsWith(Prefix)) continue;
                Undo.DestroyObjectImmediate(existing.gameObject);
                removed++;
            }
            if (removed > 0) log.Add($"replaced {removed} previously generated zone(s)");

            int made = 0;
            var clocks = Object.FindObjectsByType<ClockObjective>(FindObjectsSortMode.None);
            foreach (var clock in clocks)
            {
                Place($"{Prefix}Clock_{made}", clock.transform.position, ClockZone, root, log);
                made++;
            }
            var door = Object.FindAnyObjectByType<ExitDoor>();
            if (door != null) Place($"{Prefix}ExitDoor", door.transform.position, DoorZone, root, log);
            else log.Add("no ExitDoor in this scene — skipped");

            if (clocks.Length == 0) log.Add("** no clocks found — is this the right scene? **");

            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log($"[TimeKiller Setup] 42 - Mystery Zones in '{scene.name}':\n  " + string.Join("\n  ", log));
        }

        static void Place(string name, Vector3 at, Vector2 size, GameObject root, List<string> log)
        {
            var go = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(go, "Create Mystery Zone");
            go.transform.SetParent(root.transform, false);
            go.transform.position = new Vector3(at.x, at.y, 0f);
            var zone = Undo.AddComponent<MusicZone>(go);
            zone.kind = MusicZone.Kind.Mystery;
            zone.size = size;
            log.Add($"{name} at ({at.x:0.0}, {at.y:0.0})  {size.x:0}x{size.y:0}");
        }
    }
}
