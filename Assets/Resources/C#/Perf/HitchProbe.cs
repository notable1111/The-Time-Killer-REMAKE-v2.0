// Finds the STUTTER, not the framerate. A smoothed FPS counter (DebugOverlay uses
// 0.95 smoothing) mathematically hides exactly the spikes we are hunting, so this
// records RAW unscaled frame times and writes a report file.
//
// Why a file and not the console: read_console returns nothing even when Unity has
// logged, and a player has no console at all. A report file read with Bash is the
// only durable channel out of a running build.
//
// Why it self-installs: [RuntimeInitializeOnLoadMethod] means no scene is edited to
// use this. The scenes in this project are hand-tuned and are not to be touched by
// tooling.
//
// It records per-subsystem counters alongside the frame time, because a wall-clock
// delta alone cannot tell a GC pause from a draw-call spike from an OS stall - which
// is the precise mistake that made the previous hitch hunt inconclusive.
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace TimeKiller.Perf
{
    public class HitchProbe : MonoBehaviour
    {
#if DEVELOPMENT_BUILD || UNITY_EDITOR
        struct Hitch
        {
            public float atSeconds, ms;
            public long gcAllocBytes, drawCalls, setPass, batches;
            public int gcCollections, sceneIndex;
        }

        PerfConfig config;
        Hitch[] hitches;
        float[] frameMs;
        int hitchCount, frameCount, totalFrames, skipUntilFrame;
        float startedAt, nextReportAt;
        int lastGcCount;

        Unity.Profiling.ProfilerRecorder recDraw, recSetPass, recBatches, recGcAlloc;

        public static string ReportPath { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            if (FindObjectOfType<HitchProbe>() != null) return;
            var go = new GameObject("~HitchProbe");
            go.hideFlags = HideFlags.HideAndDontSave;
            DontDestroyOnLoad(go);
            go.AddComponent<HitchProbe>();
        }

        void Awake()
        {
            config = Resources.Load<PerfConfig>(PerfConfig.ResourcesPath);
            if (config == null) config = ScriptableObject.CreateInstance<PerfConfig>();

            hitches = new Hitch[Mathf.Max(16, config.maxHitchesRecorded)];
            frameMs = new float[65536];
            startedAt = Time.unscaledTime;
            nextReportAt = startedAt + config.reportIntervalSeconds;
            skipUntilFrame = config.warmupFrames;
            lastGcCount = System.GC.CollectionCount(0);

            ReportPath = Path.Combine(Application.persistentDataPath, "hitch_report.txt");

            recDraw = Unity.Profiling.ProfilerRecorder.StartNew(Unity.Profiling.ProfilerCategory.Render, "Draw Calls Count");
            recSetPass = Unity.Profiling.ProfilerRecorder.StartNew(Unity.Profiling.ProfilerCategory.Render, "SetPass Calls Count");
            recBatches = Unity.Profiling.ProfilerRecorder.StartNew(Unity.Profiling.ProfilerCategory.Render, "Batches Count");
            recGcAlloc = Unity.Profiling.ProfilerRecorder.StartNew(Unity.Profiling.ProfilerCategory.Memory, "GC Allocated In Frame");
        }

        void OnDestroy()
        {
            WriteReport();
            if (recDraw.Valid) recDraw.Dispose();
            if (recSetPass.Valid) recSetPass.Dispose();
            if (recBatches.Valid) recBatches.Dispose();
            if (recGcAlloc.Valid) recGcAlloc.Dispose();
        }

        void Update()
        {
            totalFrames++;
            // Startup is not gameplay: scene load, shader warm-up and the first GC
            // all land here and would dominate the report with noise.
            if (totalFrames < skipUntilFrame) return;

            float ms = Time.unscaledDeltaTime * 1000f;
            if (frameCount < frameMs.Length) frameMs[frameCount++] = ms;

            int gc = System.GC.CollectionCount(0);
            int gcDelta = gc - lastGcCount;
            lastGcCount = gc;

            if (ms >= config.hitchThresholdMs && hitchCount < hitches.Length)
            {
                hitches[hitchCount++] = new Hitch
                {
                    atSeconds = Time.unscaledTime - startedAt,
                    ms = ms,
                    gcAllocBytes = recGcAlloc.Valid ? recGcAlloc.LastValue : -1,
                    gcCollections = gcDelta,
                    drawCalls = recDraw.Valid ? recDraw.LastValue : -1,
                    setPass = recSetPass.Valid ? recSetPass.LastValue : -1,
                    batches = recBatches.Valid ? recBatches.LastValue : -1,
                    sceneIndex = UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex,
                };
            }

            if (Time.unscaledTime >= nextReportAt)
            {
                nextReportAt = Time.unscaledTime + config.reportIntervalSeconds;
                WriteReport();
                // The report write allocates and touches the disk. Recording the next
                // frame would be the probe measuring its own footprint - the same
                // mistake as timing a method through reflection and blaming the method.
                skipUntilFrame = totalFrames + 2;
            }
        }

        void WriteReport()
        {
            if (frameCount == 0) return;
            var inv = CultureInfo.InvariantCulture;

            var sorted = new float[frameCount];
            System.Array.Copy(frameMs, sorted, frameCount);
            System.Array.Sort(sorted);
            float median = Pct(sorted, 0.50f);

            var sb = new StringBuilder(8192);
            sb.AppendLine("=== TimeKiller hitch report ===");
            sb.AppendLine("elapsed        : " + (Time.unscaledTime - startedAt).ToString("0.0", inv) + " s");
            sb.AppendLine("frames sampled : " + frameCount);
            sb.AppendLine("scene          : " + UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
            sb.AppendLine("vSyncCount     : " + QualitySettings.vSyncCount + "   targetFrameRate: " + Application.targetFrameRate);
            sb.AppendLine("resolution     : " + Screen.width + "x" + Screen.height + (Screen.fullScreen ? " fullscreen" : " windowed"));
            sb.AppendLine();
            sb.AppendLine("-- frame time distribution (ms) --");
            sb.AppendLine("  median : " + median.ToString("0.00", inv) + "   (" + (1000f / Mathf.Max(0.001f, median)).ToString("0", inv) + " fps)");
            sb.AppendLine("  p95    : " + Pct(sorted, 0.95f).ToString("0.00", inv));
            sb.AppendLine("  p99    : " + Pct(sorted, 0.99f).ToString("0.00", inv));
            sb.AppendLine("  worst  : " + sorted[frameCount - 1].ToString("0.00", inv));
            sb.AppendLine();

            int severe = 0;
            for (int i = 0; i < hitchCount; i++) if (hitches[i].ms >= config.severeThresholdMs) severe++;
            float minutes = Mathf.Max(0.01f, (Time.unscaledTime - startedAt) / 60f);
            sb.AppendLine("-- hitches over " + config.hitchThresholdMs.ToString("0.0", inv) + "ms: " + hitchCount
                          + "  (severe over " + config.severeThresholdMs.ToString("0", inv) + "ms: " + severe + ") --");
            sb.AppendLine("  hitches per minute: " + (hitchCount / minutes).ToString("0.0", inv));
            sb.AppendLine();
            sb.AppendLine("      t(s)      ms   gcAlloc(B)  gcColl  draws  setPass  batches  scene");
            for (int i = 0; i < hitchCount; i++)
            {
                var h = hitches[i];
                sb.AppendLine(string.Format(inv, "  {0,8:0.00} {1,7:0.0} {2,12} {3,7} {4,6} {5,8} {6,8} {7,6}",
                    h.atSeconds, h.ms, h.gcAllocBytes, h.gcCollections, h.drawCalls, h.setPass, h.batches, h.sceneIndex));
            }

            try { File.WriteAllText(ReportPath, sb.ToString()); } catch { }
        }

        static float Pct(float[] sorted, float p)
        {
            int i = Mathf.Clamp(Mathf.RoundToInt(p * (sorted.Length - 1)), 0, sorted.Length - 1);
            return sorted[i];
        }
#else
        // Release build: the probe does not exist. Stub so any call site still
        // compiles - the exact omission that broke the Player build in DebugOverlay.
        public static string ReportPath { get { return null; } }
#endif
    }
}
