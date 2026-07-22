// Menu: TimeKiller/Setup/17 - Hall collider snapshot tools.
// The hall's side/band colliders on CastleHall/Colliders are HAND-TUNED and
// protected: no setup script may regenerate them. These two menus make that
// tuning durable — export the current values to a JSON snapshot, and re-apply
// the snapshot exactly if anything (an LDtk import, a bad rebuild) clobbers it.
//
// Snapshot lives at C#/Environment/Configs/HallColliderSnapshot.json and is
// committed to git, so the tuning survives even a full scene loss.
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace TimeKiller.EditorTools
{
    public static class HallColliderGuard
    {
        const string SnapshotPath = "Assets/Resources/C#/Environment/Configs/HallColliderSnapshot.json";

        [Serializable]
        class BoxData { public float offsetX, offsetY, sizeX, sizeY; }

        [Serializable]
        class Snapshot { public BoxData[] boxes; }

        [MenuItem("TimeKiller/Setup/17 - Hall Colliders: Export Snapshot")]
        public static void Export()
        {
            var holder = FindHolder();
            if (holder == null) return;

            var snapshot = new Snapshot
            {
                boxes = holder.GetComponents<BoxCollider2D>()
                    .Select(b => new BoxData
                    {
                        offsetX = b.offset.x, offsetY = b.offset.y,
                        sizeX = b.size.x, sizeY = b.size.y,
                    })
                    .ToArray()
            };
            File.WriteAllText(SnapshotPath, JsonUtility.ToJson(snapshot, true));
            AssetDatabase.ImportAsset(SnapshotPath);
            Debug.Log($"[TimeKiller Setup] Hall collider snapshot exported: {snapshot.boxes.Length} boxes -> {SnapshotPath}");
        }

        [MenuItem("TimeKiller/Setup/17 - Hall Colliders: Re-apply Snapshot")]
        public static void Reapply()
        {
            var holder = FindHolder();
            if (holder == null) return;
            if (!File.Exists(SnapshotPath))
            {
                Debug.LogError($"[TimeKiller Setup] No snapshot at {SnapshotPath} — export one first.");
                return;
            }

            Undo.RegisterFullObjectHierarchyUndo(holder.gameObject, "Re-apply Hall Collider Snapshot");
            if (ApplyTo(holder.gameObject))
                Debug.Log("[TimeKiller Setup] Hall collider snapshot re-applied exactly.");
        }

        // Replaces every BoxCollider2D on the holder with the snapshot's boxes.
        // Also used by Setup/18 to give the LDtk scene the identical hand tuning.
        public static bool ApplyTo(GameObject holder)
        {
            if (!File.Exists(SnapshotPath))
            {
                Debug.LogError($"[TimeKiller Setup] No snapshot at {SnapshotPath} — export one first.");
                return false;
            }
            var snapshot = JsonUtility.FromJson<Snapshot>(File.ReadAllText(SnapshotPath));
            if (snapshot?.boxes == null || snapshot.boxes.Length == 0)
            {
                Debug.LogError("[TimeKiller Setup] Snapshot is empty — refusing to touch the hall colliders.");
                return false;
            }

            foreach (var box in holder.GetComponents<BoxCollider2D>())
                Undo.DestroyObjectImmediate(box);
            foreach (var data in snapshot.boxes)
            {
                var box = holder.AddComponent<BoxCollider2D>();
                box.offset = new Vector2(data.offsetX, data.offsetY);
                box.size = new Vector2(data.sizeX, data.sizeY);
            }

            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(holder.scene);
            return true;
        }

        static Transform FindHolder()
        {
            var hall = GameObject.Find("CastleHall");
            var holder = hall != null ? hall.transform.Find("Colliders") : null;
            if (holder == null) Debug.LogError("[TimeKiller Setup] CastleHall/Colliders not found in the open scene.");
            return holder;
        }
    }
}
