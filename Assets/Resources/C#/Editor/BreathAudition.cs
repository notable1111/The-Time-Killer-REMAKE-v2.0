// Menu: TimeKiller/Verify/Breaths — Audition …
//
// Lets the breath one-shots be judged the way they will actually be heard: as a
// CYCLE. Clicking 25 sliced files one at a time in the Project window tells you
// almost nothing, because the thing being chosen is not a sample — it is the
// alternance of inhale and exhale, and the gap between them.
//
// Each audition plays the same pool twice: first at a calm rate, then at a
// panicked one. Note that NOTHING is pitch-shifted between the two. That is the
// point of the rebuild: the current system pitches one looping clip 0.9 -> 1.4
// to suggest effort, which does not sound faster, it sounds like a smaller
// person. Real systems change the SEQUENCING RATE and leave pitch alone, and
// this tool is the proof you can hear before any runtime code is written.
//
// Editor-only, plays through the editor's own preview. Touches no config, no
// scene, and nothing in the (frozen) heartbeat.
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace TimeKiller.EditorTools
{
    public static class BreathAudition
    {
        const string Folder = "Assets/Resources/Assets/Heartbeat/Breaths";

        // (inhale->exhale gap, exhale->inhale gap) in seconds. A real breath is
        // not evenly spaced: the turnaround after an inhale is quick, the rest
        // after an exhale is where the pause lives — and it is that pause which
        // shortens when you are frightened.
        static readonly (float hold, float rest, string label)[] Rates =
        {
            (0.16f, 0.95f, "CALM      (~2.5s per breath)"),
            (0.06f, 0.22f, "PANICKED  (~0.8s per breath)"),
        };

        [MenuItem("TimeKiller/Verify/Breaths — Audition 'strong' (the current voice)")]
        public static void AuditionStrong() => Audition("strong");

        [MenuItem("TimeKiller/Verify/Breaths — Audition 'sleeping' (bigger pool, hissier)")]
        public static void AuditionSleeping() => Audition("sleeping");

        [MenuItem("TimeKiller/Verify/Breaths — Stop")]
        public static void Stop()
        {
            EditorApplication.update -= Tick;
            StopPreview();
            Debug.Log("[Breaths] stopped.");
        }

        static List<AudioClip> inhales = new List<AudioClip>();
        static List<AudioClip> exhales = new List<AudioClip>();
        static double nextAt;
        static int step;          // even = inhale, odd = exhale
        static int cyclesLeft;
        static int rateIndex;
        static System.Random rng = new System.Random();

        static void Audition(string prefix)
        {
            inhales = Load(prefix, "inhale");
            exhales = Load(prefix, "exhale");
            if (inhales.Count == 0 || exhales.Count == 0)
            {
                Debug.LogError($"[Breaths] No '{prefix}' one-shots in {Folder}. " +
                               "Run Tools/AudioPipeline/slice_breaths.py first.");
                return;
            }

            Debug.Log($"[Breaths] Auditioning '{prefix}': {inhales.Count} inhales, {exhales.Count} exhales.\n" +
                      "    Plays 4 CALM breaths, a pause, then 4 PANICKED breaths.\n" +
                      "    Nothing is pitch-shifted between them — only the rate and the gaps change.\n" +
                      "    Use 'TimeKiller/Verify/Breaths — Stop' to cut it short.");

            rateIndex = 0;
            cyclesLeft = 4;
            step = 0;
            nextAt = EditorApplication.timeSinceStartup;
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
        }

        static List<AudioClip> Load(string prefix, string phase) =>
            AssetDatabase.FindAssets("t:AudioClip", new[] { Folder })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(p => System.IO.Path.GetFileName(p).StartsWith($"{prefix}_{phase}_"))
                .Select(AssetDatabase.LoadAssetAtPath<AudioClip>)
                .Where(c => c != null)
                .ToList();

        static void Tick()
        {
            if (EditorApplication.timeSinceStartup < nextAt) return;

            bool inhale = step % 2 == 0;
            var pool = inhale ? inhales : exhales;
            // Shuffled, not sequential: the pool exists so that no two breaths
            // in a row are identical. Playing them in order would rebuild the
            // exact repetition the loop had, just with more steps.
            var clip = pool[rng.Next(pool.Count)];
            PlayPreview(clip);

            var (hold, rest, _) = Rates[rateIndex];
            nextAt = EditorApplication.timeSinceStartup + clip.length + (inhale ? hold : rest);
            step++;

            if (step % 2 == 0)
            {
                cyclesLeft--;
                if (cyclesLeft <= 0)
                {
                    rateIndex++;
                    if (rateIndex >= Rates.Length)
                    {
                        EditorApplication.update -= Tick;
                        Debug.Log("[Breaths] Audition finished.");
                        return;
                    }
                    cyclesLeft = 4;
                    nextAt += 1.1f;   // a beat of silence between the two rates
                    Debug.Log($"[Breaths] now: {Rates[rateIndex].label}");
                }
            }
        }

        static MethodInfo playMethod, stopMethod;

        static void PlayPreview(AudioClip clip)
        {
            if (playMethod == null)
            {
                var util = typeof(AudioImporter).Assembly.GetType("UnityEditor.AudioUtil");
                playMethod = util?.GetMethod("PlayPreviewClip",
                    BindingFlags.Static | BindingFlags.Public,
                    null, new[] { typeof(AudioClip), typeof(int), typeof(bool) }, null);
            }
            playMethod?.Invoke(null, new object[] { clip, 0, false });
        }

        static void StopPreview()
        {
            if (stopMethod == null)
            {
                var util = typeof(AudioImporter).Assembly.GetType("UnityEditor.AudioUtil");
                stopMethod = util?.GetMethod("StopAllPreviewClips",
                    BindingFlags.Static | BindingFlags.Public, null, System.Type.EmptyTypes, null);
            }
            stopMethod?.Invoke(null, null);
        }
    }
}
