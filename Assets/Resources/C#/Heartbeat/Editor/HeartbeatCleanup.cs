// Menu: TimeKiller/Setup/37 - Remove Dead Heartbeat Objects.
//
// One-off janitor for scenes built BEFORE the heartbeat got a single owner.
// Back then HealthVfx made a "Heartbeat" child and HidingVfx made a
// "HiddenHeartbeat" child, each with its own AudioSource. PlayerHeartbeat owns
// the lub-dub now, the two directors no longer have fields pointing at those
// sources, and Setup/22 and Setup/25 no longer create them — so in an existing
// scene they are orphans that load an AudioSource and never make a sound.
//
// Deliberately a SEPARATE menu item rather than something Setup/22 and Setup/25
// do behind your back: deleting scene objects should be a thing you asked for,
// and it needs to happen exactly once per scene.
//
// Safety: an object is only removed if it is a childless leaf carrying nothing
// but a Transform and an AudioSource. If you ever repurpose one of those names
// the check fails, the object is left alone, and the log says why. Re-running
// after a clean run reports "nothing to remove" and changes nothing.
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TimeKiller.EditorTools
{
    public static class HeartbeatCleanup
    {
        // (object name, the rig it used to hang under) — both are children, so
        // the parent name keeps this from hitting a same-named object elsewhere.
        static readonly (string parent, string child)[] Dead =
        {
            ("HealthVfx", "Heartbeat"),
            ("HidingVfx", "HiddenHeartbeat"),
        };

        [MenuItem("TimeKiller/Setup/37 - Remove Dead Heartbeat Objects")]
        public static void Run()
        {
            var removed = new List<string>();
            var skipped = new List<string>();
            Scene? touched = null;

            foreach (var (parentName, childName) in Dead)
            {
                var parent = GameObject.Find(parentName);
                if (parent == null) continue;

                var child = parent.transform.Find(childName);
                if (child == null) continue;

                if (!IsIdleAudioLeaf(child, out string reason))
                {
                    skipped.Add($"{parentName}/{childName} ({reason})");
                    continue;
                }

                touched = child.gameObject.scene;
                Undo.DestroyObjectImmediate(child.gameObject);
                removed.Add($"{parentName}/{childName}");
            }

            foreach (var s in skipped)
                Debug.LogWarning($"[TimeKiller Setup] Left '{s}' alone — it is no longer a bare AudioSource, so it is not mine to delete. Check it by hand.");

            if (removed.Count == 0)
            {
                Debug.Log("[TimeKiller Setup] Nothing to remove — this scene has no dead heartbeat objects. PlayerHeartbeat is the only heart.");
                return;
            }

            if (touched.HasValue)
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(touched.Value);
            Debug.Log($"[TimeKiller Setup] Removed {removed.Count} dead heartbeat object(s): {string.Join(", ", removed)}. " +
                      "Save the scene (Ctrl+S) to keep it. The audible heart is PlayerHeartbeat's alone now — watch the 'Heart' line on F1.");
        }

        /// True only for a childless object whose components are exactly
        /// Transform + AudioSource, with nothing playing on awake.
        static bool IsIdleAudioLeaf(Transform t, out string reason)
        {
            if (t.childCount > 0) { reason = "it has children"; return false; }

            var components = t.GetComponents<Component>();
            if (components.Length != 2) { reason = $"it has {components.Length} components, expected Transform + AudioSource"; return false; }

            var source = t.GetComponent<AudioSource>();
            if (source == null) { reason = "no AudioSource on it"; return false; }
            if (source.playOnAwake) { reason = "its AudioSource is set to play on awake — something may still want it"; return false; }

            reason = null;
            return true;
        }
    }
}
