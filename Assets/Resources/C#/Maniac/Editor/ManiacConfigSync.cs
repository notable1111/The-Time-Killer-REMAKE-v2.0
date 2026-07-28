// Menu: TimeKiller/Setup/35 - Sync Maniac Config (serialize every field).
//
// ManiacConfig.asset was written before the awareness stealth model existed, so
// none of its 15 detection/tuning fields are in the YAML at all. Unity keeps the
// C# field initializers for keys it doesn't find, which is why the model WORKS —
// but it means the Inspector is showing values that were never saved, and the
// numbers that actually decide how hard the game is live in source, not in the
// asset the designer is supposed to tune.
//
// This rewrites the asset with every serialized field present, changing no
// values. Run it once; after that the detection block is editable like the rest.
using UnityEditor;
using UnityEngine;

namespace TimeKiller.Maniac.EditorTools
{
    public static class ManiacConfigSync
    {
        const string ConfigPath = "Assets/Resources/C#/Maniac/Configs/ManiacConfig.asset";

        [MenuItem("TimeKiller/Setup/35 - Sync Maniac Config (serialize every field)")]
        public static void Sync()
        {
            var config = AssetDatabase.LoadAssetAtPath<ManiacConfig>(ConfigPath);
            if (config == null)
            {
                Debug.LogError($"[TimeKiller Setup] No ManiacConfig at {ConfigPath} — run Setup/20 first. Nothing changed.");
                return;
            }

            // Touching the SerializedObject is what forces Unity to write out the
            // keys it currently omits; nothing is assigned, so no value moves.
            var so = new SerializedObject(config);
            int fields = 0;
            var it = so.GetIterator();
            while (it.NextVisible(true)) fields++;
            so.ApplyModifiedProperties();

            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();
            Debug.Log($"[TimeKiller Setup] ManiacConfig synced — {fields} fields now serialized in the asset " +
                      "(detection/awareness block included). Values unchanged; tune them in the Inspector.");
        }
    }
}
