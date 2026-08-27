// Levels the tracks INSIDE each music layer against each other.
//
// THE PROBLEM, measured 2026-08-27 with ffmpeg mean_volume over every track the
// director actually plays. A layer holds several tracks and picks one at random,
// and the pools are not internally matched:
//
//   investigate  5.3 dB spread      dread  3.7 dB      menu  3.7 dB
//   safe         2.7 dB             mystery 2.6 dB     chase 1.9 dB
//
// So entering the same threat state could sound noticeably louder or quieter
// depending on the roll. In a system whose premise is that the music IS the
// threat detector, that means level — the loudest cue the mix has — was carrying
// information the dice were setting. Chase, the layer that matters most, was
// already tight at 1.9 dB and barely moves here.
//
// THE TARGET IS EACH LAYER'S MEDIAN, not the loudest or the quietest. The median
// keeps the layer's centre of loudness where the user tuned it: roughly half the
// tracks come down, half come up, and the layer as a whole still sits where it
// did. Matching to the loudest would raise the whole game; matching to the
// quietest would sink it.
//
// BETWEEN-LAYER DIFFERENCES ARE LEFT ALONE. Menu is loudest, safe is quietest,
// and that ordering is the user's tuning and carries real meaning. This script
// never touches a `<layer>Volume`.
//
// CLIPPING WAS CHECKED AGAINST THE REAL CHAIN, not against the source files. A
// boost only matters if it clips at the output, and the output is
// clip peak + trim + layerVolume + AudioMix Music level + masterLevel(0.85).
// Worst case per layer after these trims:
//
//   chase -3.0    menu -2.4    mystery -4.9    investigate -5.1
//   dread -12.0   safe -15.5                        (all dBFS, all under 0)
//
// A first pass clamped boosts against the SOURCE file's -3 dBFS peak instead and
// produced nonsense — it turned "this track needs +3.5 dB" into "-0.1 dB", i.e.
// the opposite of the intent. The source peak is not the constraint; the mix is.
using System.Collections.Generic;
using TimeKiller.EditorTools;
using UnityEditor;
using UnityEngine;

namespace TimeKiller.Audio
{
    public static class MusicLevelMatchSetup
    {
        const string ConfigPath = "Assets/Resources/C#/Audio/Configs/AudioConfig.asset";

        /// Measured trims, keyed by clip name. Tracks sitting exactly on their
        /// layer's median are deliberately absent: they are the reference, and an
        /// entry of 0 dB would be an inert row in the inspector.
        static readonly Dictionary<string, float> Trims = new()
        {
            // dread — median -15.9 dB (reference: Insidious Fog)
            { "Shadow", -0.20f },
            { "Haunted Manor", 3.50f },

            // mystery — median -15.7 dB
            { "Blood Moon Ritual", -1.45f },
            { "Necrotic Decay", -0.95f },
            { "Entity Behind Glass", 0.95f },
            { "Forgotten Asylum", 1.15f },

            // investigate — median -17.0 dB (reference: It's in the Blood)
            { "WAV_LIGHT_TENSION_LOOP_The_Urge_to_Kill", -2.30f },
            { "Abyssal Depths", -2.20f },
            { "Breathing Walls", 0.30f },
            { "Deep Creeping Horror", 3.00f },

            // chase — median -14.4 dB (reference: Wood Monster AND Containment Breach)
            { "WAV_MENU_FULL_Systolic_Menace", 1.90f },

            // safe — median -20.2 dB (reference: AWakingDream_I)
            { "WAV_AMBIENCE_LOOP_Mirrored_Reflections", -1.30f },
            { "Whispers in the Void", 1.40f },

            // menu — median -13.3 dB
            { "Desolate Wasteland", -1.40f },
            { "Phantom Echoes", -0.60f },
            { "Mutation Chamber", 0.60f },
            { "Toxic Corridor", 2.30f },
        };

        [MenuItem("TimeKiller/Setup/54 - Level-match music tracks within each layer")]
        public static void Run()
        {
            if (SetupGuard.Blocked("54 - Level-match music tracks")) return;

            var config = AssetDatabase.LoadAssetAtPath<AudioConfig>(ConfigPath);
            if (config == null)
            {
                Debug.LogWarning($"[TimeKiller Setup] 54 - no AudioConfig at {ConfigPath}");
                return;
            }

            var report = new System.Text.StringBuilder("[TimeKiller Setup] 54 - music level match\n");
            var entries = new List<TrackTrim>();
            var seen = new HashSet<string>();

            // Walk the pools rather than the table, so a trim for a track that has
            // since been removed from the config simply never lands — and gets
            // named in the report instead of silently rotting in the asset.
            foreach (var pool in new[]
                     {
                         config.dreadTracks, config.mysteryTracks, config.investigateTracks,
                         config.chaseTracks, config.safeTracks, config.endgameTracks, config.menuTracks,
                     })
            {
                if (pool == null) continue;
                foreach (var clip in pool)
                {
                    if (clip == null || !seen.Add(clip.name)) continue;
                    if (Trims.TryGetValue(clip.name, out float db))
                        entries.Add(new TrackTrim { clip = clip, trimDb = db });
                }
            }

            foreach (var name in Trims.Keys)
                if (!seen.Contains(name))
                    report.AppendLine($"   NOT IN ANY POOL, trim skipped: {name}");

            var so = new SerializedObject(config);
            var array = so.FindProperty("trackTrims");
            array.arraySize = entries.Count;
            for (int i = 0; i < entries.Count; i++)
            {
                var element = array.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("clip").objectReferenceValue = entries[i].clip;
                element.FindPropertyRelative("trimDb").floatValue = entries[i].trimDb;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();

            report.AppendLine($"   wrote {entries.Count} trims; every other track plays at 0 dB.");
            report.AppendLine("   Layer volumes untouched — this only levels tracks against their own pool.");
            Debug.LogWarning(report.ToString());
        }

        [MenuItem("TimeKiller/Setup/54b - Clear music level match")]
        public static void Clear()
        {
            if (SetupGuard.Blocked("54b - Clear music level match")) return;
            var config = AssetDatabase.LoadAssetAtPath<AudioConfig>(ConfigPath);
            if (config == null) return;
            var so = new SerializedObject(config);
            so.FindProperty("trackTrims").arraySize = 0;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();
            Debug.LogWarning("[TimeKiller Setup] 54b - trims cleared; every track back to its raw level.");
        }
    }
}
