// Tunables for the hitch probe. Lives in a config because a threshold you have to
// recompile to change is a threshold nobody adjusts, and the useful value differs
// per machine — 33ms is a hitch on a 4060, 50ms is normal on a laptop iGPU.
using UnityEngine;

namespace TimeKiller.Perf
{
    [CreateAssetMenu(menuName = "TimeKiller/Perf Config", fileName = "PerfConfig")]
    public class PerfConfig : ScriptableObject
    {
        public const string ResourcesPath = "C#/Perf/Configs/PerfConfig";

        [Header("What counts as a hitch")]
        [Tooltip("A frame longer than this is recorded. 33.3ms = dropped below 30fps.")]
        public float hitchThresholdMs = 33.3f;

        [Tooltip("Frames longer than this are logged as SEVERE — a visible freeze.")]
        public float severeThresholdMs = 100f;

        [Header("Sampling")]
        [Tooltip("Frames to ignore at startup. Scene load and shader warm-up are not gameplay hitches.")]
        public int warmupFrames = 120;

        [Tooltip("Report is rewritten this often, so a crash still leaves usable data.")]
        public float reportIntervalSeconds = 5f;

        [Tooltip("Ring buffer size. Preallocated — the probe must never allocate in the frame it measures.")]
        public int maxHitchesRecorded = 512;
    }
}
