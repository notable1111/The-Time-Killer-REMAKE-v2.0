// Runs the bot N times unattended and writes one JSON object per run.
//
// Three details make this work at all:
//   1. It lives on a DontDestroyOnLoad object, because the only reset that
//      cannot leak stale state between runs is GameFlow.Restart's full scene
//      reload — which destroys everything else, including the player it drives.
//   2. Every wait is on REALTIME. GameFlow sets Time.timeScale = 0 the instant
//      a run ends, so anything waiting on scaled time would deadlock there.
//   3. Each run seeds UnityEngine.Random, because ClockRepair.NewZone() rolls
//      the skill-check zone unseeded — without this, two "identical" runs
//      diverge and no interesting failure can ever be replayed.
//
// Speed: runs can be accelerated with Time.timeScale, but perception and
// pathfinding tick on Update while physics is fixed-step, so past some factor
// the maniac quietly gets dumber and the win rate measures the accelerator
// instead of the game. Tick "also at 1x" to emit a matched-seed control block;
// analyze.py compares them and tells you whether the speed was honest.
//
// INSTRUMENT HONESTY. The first 75-run batch died silently at run 11 (a mid-run
// domain reload — see BatchGuard) and left a file that simply stopped, with
// nothing to say it had not finished. Worse, the failure modes it DID survive
// were being written as ordinary losses. That is the one bug class this whole
// project cannot afford, because a broken instrument that looks like it works
// biases every balance number toward "flat".
//
// So a run now ends in exactly one of five ways, and three of them are the
// harness admitting fault rather than reporting a result:
//   escape / death      real outcomes
//   timeout             ran out of clock, genuinely ambiguous
//   stalled             the bot stopped moving and stopped making progress
//   harness_error       preconditions failed, or Unity threw during the run
// analyze.py must never average the last two into a win rate.
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using TimeKiller.Core;
using TimeKiller.Hiding;
using TimeKiller.Objectives;
using TimeKiller.Player;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TimeKiller.Testing
{
    public class BatchRunner : MonoBehaviour
    {
        public class Request
        {
            public string[] Profiles = { "Bot_average" };
            public int Runs = 20;
            public int BaseSeed = 1000;
            public float Speed = 4f;
            public bool AlsoAtRealtime = true;   // matched-seed 1x control block
            public int RealtimeControlRuns = 5;  // kept small on purpose: 1x costs real minutes per run
            public string[] ControlProfiles;     // null = every profile; see ProfilesAt
            public float MaxRunSeconds = 480f;   // in-game seconds before a run is called a timeout
        }

        public static BatchRunner Instance { get; private set; }

        Request request;
        string outputPath;
        int completed, total;
        bool runEnded, runWon;
        Vector2 deathPos;
        bool died;

        // ---- instrument health ----
        // A run that throws is not a loss, it is a void run. Unity keeps ticking
        // Update on a broken scene forever, so without a budget one bad frame
        // becomes half a million exceptions (measured: 498,704 on 2026-07-25).
        const int ExceptionBudget = 20;
        const float StallSeconds = 75f;    // in-game, with no movement AND no progress
        const float StallDistance = 1.5f;

        int exceptionsThisRun;
        string firstException;
        bool abortBatch;
        string abortReason;

        public string Progress => $"{completed}/{total}";

        public static void Launch(Request request)
        {
            if (Instance != null) { Debug.LogWarning("[BatchRunner] Already running."); return; }
            var go = new GameObject("[BatchRunner]");
            DontDestroyOnLoad(go);
            Instance = go.AddComponent<BatchRunner>();
            Instance.request = request;
            Instance.StartCoroutine(Instance.RunAll());
        }

        void OnDestroy() { if (Instance == this) Instance = null; }

        IEnumerator RunAll()
        {
            // The fast block goes FIRST and the 1x control last. Rows are appended
            // as they finish, so a batch killed halfway still leaves the whole
            // experiment on disk with only its validation missing — the recoverable
            // failure. The other order loses the experiment and keeps the check.
            var speeds = new List<float>();
            if (!Mathf.Approximately(request.Speed, 1f)) speeds.Add(request.Speed);
            if (request.AlsoAtRealtime || speeds.Count == 0) speeds.Add(1f);

            total = 0;
            foreach (float s in speeds) total += ProfilesAt(s).Length * RunsAt(s);
            outputPath = MakeOutputPath();
            WriteHeader(speeds);
            Debug.Log($"[BatchRunner] {total} runs -> {outputPath}");

            EventBus.Subscribe<RunEndedEvent>(OnRunEnded);
            EventBus.Subscribe<PlayerDiedEvent>(OnPlayerDied);
            Application.logMessageReceived += OnLogMessage;
#if UNITY_EDITOR
            // The fix for the run-11 death: defer any script compilation until
            // this batch is done, so the coroutine cannot be killed mid-flight.
            BatchGuard.Hold();
#endif

            foreach (float speed in speeds)
            {
                foreach (string profileName in ProfilesAt(speed))
                {
                    var profile = Resources.Load<BotProfileConfig>("C#/Testing/Configs/" + profileName);
                    if (profile == null)
                    {
                        Debug.LogError($"[BatchRunner] Profile '{profileName}' not found — run TimeKiller/Setup/33 first.");
                        continue;
                    }
                    // Matched seeds: the 1x control block replays the SAME seeds
                    // as the fast block, so any win-rate gap is the accelerator.
                    int count = RunsAt(speed);
                    for (int i = 0; i < count && !abortBatch; i++)
                        yield return RunOnce(profile, request.BaseSeed + i, speed, reload: completed + 1 < total);
                    if (abortBatch) break;
                }
                if (abortBatch) break;
            }

            EventBus.Unsubscribe<RunEndedEvent>(OnRunEnded);
            EventBus.Unsubscribe<PlayerDiedEvent>(OnPlayerDied);
            Application.logMessageReceived -= OnLogMessage;
            Time.timeScale = 1f;

            if (abortBatch)
                Debug.LogError($"[BatchRunner] ABORTED after {completed}/{total} runs: {abortReason}. " +
                               $"Partial results in {outputPath}");
            else
                Debug.Log($"[BatchRunner] Done: {completed} runs written to {outputPath}");

#if UNITY_EDITOR
            // Disarm the black box BEFORE releasing the lock. Release() refreshes
            // the AssetDatabase, which can immediately run the compile it was
            // holding back — and that reload would otherwise write an "aborted"
            // row for a batch that had in fact just finished cleanly. A false
            // alarm in the instrument is exactly as corrosive as a missed one.
            UnityEditor.SessionState.EraseString(BatchGuard.LiveKey);
            BatchGuard.Release();
#endif
            Destroy(gameObject);
#if UNITY_EDITOR
            // Leave the editor where it was found. Without this the last reloaded
            // scene keeps running unattended and the idle player gets murdered.
            UnityEditor.EditorApplication.isPlaying = false;
#endif
        }

        // Counts only genuine thrown exceptions — Debug.LogError is something the
        // harness itself does on purpose and must not trip the watchdog.
        void OnLogMessage(string condition, string stack, LogType type)
        {
            if (type != LogType.Exception) return;
            exceptionsThisRun++;
            if (firstException == null)
            {
                string frame = stack ?? "";
                int nl = frame.IndexOf('\n');
                if (nl > 0) frame = frame.Substring(0, nl);
                firstException = (condition + " @ " + frame).Trim();
            }
        }

        /// Everything that must be true before a run's numbers mean anything.
        /// Checked per run, because the failure we are guarding against wipes
        /// these fields on a scene that otherwise looks perfectly alive.
        static string PreflightFault(PlayerController pc, BotPilot pilot)
        {
            if (pc == null) return "no PlayerController in scene";
            if (pc.Input == null) return "PlayerController.Input is null — the scene lost its runtime wiring";
            if (ObjectiveManager.Instance == null) return "no ObjectiveManager";
            if (ClockObjective.All.Count == 0) return "no clocks in scene";
            if (pilot == null || !pilot.Ready) return "BotPilot failed to start";
            return null;
        }

#if UNITY_EDITOR
        // The black box. Re-published every run so that if the domain dies, the
        // editor-side guard knows exactly which run was in flight.
        void PublishLive(int seed, string profileName, float speed)
        {
            var ci = CultureInfo.InvariantCulture;
            UnityEditor.SessionState.SetString(BatchGuard.LiveKey,
                $"{outputPath}|{completed}|{total}|{seed}|{profileName}|{speed.ToString("0.##", ci)}");
        }
#endif

        IEnumerator RunOnce(BotProfileConfig profile, int seed, float speed, bool reload)
        {
            Random.InitState(seed);   // the skill-check zones become replayable
            exceptionsThisRun = 0;
            firstException = null;
#if UNITY_EDITOR
            PublishLive(seed, profile.profileName, speed);
#endif

            // Wait for the freshly loaded scene to finish waking up.
            float waitUntil = Time.realtimeSinceStartup + 15f;
            PlayerController pc = null;
            while (Time.realtimeSinceStartup < waitUntil)
            {
                pc = Object.FindAnyObjectByType<PlayerController>();
                if (pc != null && ObjectiveManager.Instance != null && ClockObjective.All.Count > 0) break;
                yield return null;
            }
            yield return null;   // one more frame so Start() has run everywhere

            BotPilot pilot = null;
            if (pc != null)
            {
                TestDriver.Possess();
                pilot = pc.GetComponent<BotPilot>();
                if (pilot == null) pilot = pc.gameObject.AddComponent<BotPilot>();
                pilot.Begin(profile, seed);
            }
            var telemetry = pc != null ? pc.GetComponent<TestTelemetry>() : null;

            // A run whose preconditions are broken must be RECORDED as broken.
            // The old code logged an error and `yield break`d, which wrote no row
            // at all — the batch silently shrank and nobody could tell.
            string fault = PreflightFault(pc, pilot);
            if (fault != null)
            {
                WriteFaultRow(profile, seed, speed, "preflight: " + fault);
                Fail($"preflight failed — {fault}");
                yield break;
            }

            runEnded = false; runWon = false; died = false;
            Time.timeScale = speed;

            var hiding = pc.GetComponent<PlayerHiding>();
            var om = ObjectiveManager.Instance;
            Vector2 stallAnchor = pc.transform.position;
            int stallFixed = om.FixedCount;
            int stallPresses = pilot.SkillChecksAttempted;
            float stallSince = Time.realtimeSinceStartup;

            float startedReal = Time.realtimeSinceStartup;
            float budgetReal = request.MaxRunSeconds / Mathf.Max(0.01f, speed);
            float stallBudgetReal = StallSeconds / Mathf.Max(0.01f, speed);
            bool stalled = false;

            while (!runEnded && Time.realtimeSinceStartup - startedReal < budgetReal)
            {
                if (exceptionsThisRun >= ExceptionBudget) break;

                // Stall: not moving, not fixing anything, not deliberately hidden.
                // Two runs in the first batch burned the full 480s standing at the
                // exit door, and were written as ordinary losses.
                //
                // Standing still is NOT enough on its own. A bot working a clock
                // is stationary by design, and a novice missing check after check
                // can stay on one clock a long while without FixedCount moving —
                // so skill-check presses count as liveness too. Getting this
                // wrong would void exactly the runs where the bot is trying
                // hardest, which is the worst possible sample to throw away.
                Vector2 here = pc.transform.position;
                bool hidden = hiding != null && hiding.IsHidden;
                if (hidden || om.FixedCount != stallFixed
                    || pilot.SkillChecksAttempted != stallPresses
                    || Vector2.Distance(here, stallAnchor) > StallDistance)
                {
                    stallAnchor = here;
                    stallFixed = om.FixedCount;
                    stallPresses = pilot.SkillChecksAttempted;
                    stallSince = Time.realtimeSinceStartup;
                }
                else if (Time.realtimeSinceStartup - stallSince > stallBudgetReal) { stalled = true; break; }

                yield return null;
            }

            float runSeconds = GameFlow.Instance != null ? GameFlow.Instance.RunSeconds
                                                         : (Time.realtimeSinceStartup - startedReal) * speed;

            if (exceptionsThisRun >= ExceptionBudget)
            {
                WriteFaultRow(profile, seed, speed, $"{exceptionsThisRun} exceptions during run: {firstException}");
                Fail($"the scene threw {exceptionsThisRun} exceptions — {firstException}");
                yield break;
            }

            string endReason = runEnded ? (runWon ? "escape" : "death") : (stalled ? "stalled" : "timeout");
            WriteRow(profile, seed, speed, runSeconds, endReason, pc, pilot, telemetry);

            completed++;
            if (completed % 5 == 0 || completed == total || stalled)
                Debug.Log($"[BatchRunner] {completed}/{total} ({profile.profileName} @ {speed}x, seed {seed}: {endReason})");

            Time.timeScale = 1f;
            if (!reload) yield break;
            var scene = SceneManager.GetActiveScene();
            SceneManager.LoadScene(scene.buildIndex >= 0 ? scene.buildIndex : 0);
            yield return null;
        }

        // A harness fault is not a data point — it means every LATER run is
        // suspect too (the scene is already broken), so the batch stops rather
        // than filling the file with plausible-looking rubbish.
        void Fail(string reason)
        {
            abortBatch = true;
            abortReason = reason;
            Time.timeScale = 1f;
        }

        bool IsControl(float speed) =>
            Mathf.Approximately(speed, 1f) && request.AlsoAtRealtime && request.Speed > 1f;

        // A 1x run costs real minutes, so the control block is deliberately a few
        // runs — enough to catch a speed that lies, not a second full experiment.
        int RunsAt(float speed) =>
            IsControl(speed) ? Mathf.Min(request.Runs, request.RealtimeControlRuns) : request.Runs;

        // The control may be CONCENTRATED on one profile instead of spread thin
        // across all of them. Win rate is binary, so its resolution is set by run
        // count: three 5-run controls resolve to roughly ±20 points each and
        // cannot see the 5-point gap the guard exists to catch, while the same
        // 15 runs on one profile resolve to ~±12. Same wall clock, one usable
        // answer instead of three unusable ones. Seeds start at BaseSeed in both
        // blocks, so the control's runs stay matched to the fast block's first N.
        string[] ProfilesAt(float speed) =>
            IsControl(speed) && request.ControlProfiles != null && request.ControlProfiles.Length > 0
                ? request.ControlProfiles
                : request.Profiles;

        void OnRunEnded(RunEndedEvent e) { runEnded = true; runWon = e.Won; }
        void OnPlayerDied(PlayerDiedEvent e) { died = true; deathPos = e.Position; }

        // ---- output ----------------------------------------------------------

        static string MakeOutputPath()
        {
            string dir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Tools", "Playtest", "results"));
            Directory.CreateDirectory(dir);
            return Path.Combine(dir, System.DateTime.Now.ToString("yyyy-MM-dd_HHmmss") + ".jsonl");
        }

        void WriteHeader(List<float> speeds)
        {
            var ci = CultureInfo.InvariantCulture;
            var sb = new StringBuilder("{\"meta\":true");
            sb.Append(",\"scene\":\"").Append(SceneManager.GetActiveScene().name).Append('"');
            sb.Append(",\"started\":\"").Append(System.DateTime.Now.ToString("s")).Append('"');
            sb.Append(",\"runsPerCell\":").Append(request.Runs);
            sb.Append(",\"speeds\":[");
            for (int i = 0; i < speeds.Count; i++) { if (i > 0) sb.Append(','); sb.Append(speeds[i].ToString("0.##", ci)); }
            sb.Append("],\"profiles\":[");
            for (int i = 0; i < request.Profiles.Length; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append('"').Append(request.Profiles[i]).Append('"');
            }
            sb.Append(']');
            // Which profiles the 1x control covered. Without this a reader cannot
            // tell "this profile's speed was never validated" from "it was, and
            // agreed" — the two look identical once the rows are aggregated.
            if (request.AlsoAtRealtime && request.Speed > 1f)
            {
                var control = ProfilesAt(1f);
                sb.Append(",\"controlProfiles\":[");
                for (int i = 0; i < control.Length; i++)
                {
                    if (i > 0) sb.Append(',');
                    sb.Append('"').Append(control[i]).Append('"');
                }
                sb.Append(']');
            }
            sb.Append('}');
            File.AppendAllText(outputPath, sb.ToString() + "\n");
        }

        /// A run the harness could not honestly measure. Deliberately carries no
        /// gameplay fields at all — there is nothing to average, and a row that
        /// looked half-plausible would be worse than one that looks broken.
        void WriteFaultRow(BotProfileConfig profile, int seed, float speed, string fault)
        {
            var ci = CultureInfo.InvariantCulture;
            var sb = new StringBuilder("{\"harnessError\":true");
            sb.Append(",\"seed\":").Append(seed);
            sb.Append(",\"profile\":\"").Append(profile != null ? profile.profileName : "?").Append('"');
            sb.Append(",\"scene\":\"").Append(SceneManager.GetActiveScene().name).Append('"');
            sb.Append(",\"speed\":").Append(speed.ToString("0.##", ci));
            sb.Append(",\"endReason\":\"harness_error\"");
            sb.Append(",\"atRun\":").Append(completed + 1);
            sb.Append(",\"fault\":\"").Append(fault.Replace("\\", "/").Replace("\"", "'")).Append('"');
            sb.Append('}');
            File.AppendAllText(outputPath, sb.ToString() + "\n");
            Debug.LogError($"[BatchRunner] HARNESS ERROR at run {completed + 1}/{total} (seed {seed}): {fault}");
        }

        void WriteRow(BotProfileConfig profile, int seed, float speed, float runSeconds, string endReason,
                      PlayerController pc, BotPilot pilot, TestTelemetry t)
        {
            var ci = CultureInfo.InvariantCulture;
            var om = ObjectiveManager.Instance;
            Vector2 endPos = died ? deathPos : (Vector2)pc.transform.position;

            var sb = new StringBuilder("{");
            sb.Append("\"seed\":").Append(seed);
            sb.Append(",\"profile\":\"").Append(profile.profileName).Append('"');
            sb.Append(",\"knowsEverything\":").Append(profile.knowsEverything ? "true" : "false");
            sb.Append(",\"scene\":\"").Append(SceneManager.GetActiveScene().name).Append('"');
            sb.Append(",\"speed\":").Append(speed.ToString("0.##", ci));
            sb.Append(",\"won\":").Append(runWon ? "true" : "false");
            sb.Append(",\"endReason\":\"").Append(endReason).Append('"');
            sb.Append(",\"runSeconds\":").Append(runSeconds.ToString("0.0", ci));
            sb.Append(",\"clocksFixed\":").Append(om != null ? om.FixedCount : 0);
            sb.Append(",\"clocksTotal\":").Append(om != null ? om.Total : 0);
            sb.Append(",\"endPos\":[").Append(endPos.x.ToString("0.0", ci)).Append(',').Append(endPos.y.ToString("0.0", ci)).Append(']');
            sb.Append(",\"nearestLandmark\":\"").Append(NearestLandmark(endPos)).Append('"');

            if (pilot != null)
            {
                sb.Append(",\"skillChecks\":{\"attempted\":").Append(pilot.SkillChecksAttempted)
                  .Append(",\"hit\":").Append(pilot.SkillChecksHit).Append('}');
                sb.Append(",\"clocksFound\":").Append(pilot.Memory != null ? pilot.Memory.ClocksKnown : 0);
                sb.Append(",\"mapExplored\":").Append(pilot.Memory != null && pilot.Memory.ExplorableCount > 0
                    ? (100f * pilot.Memory.ExploredCount / pilot.Memory.ExplorableCount).ToString("0", ci) : "0");
                sb.Append(",\"accidentalHides\":").Append(pilot.AccidentalHides);
            }
            if (t != null)
            {
                sb.Append(",\"spotted\":").Append(t.Spotted);
                sb.Append(",\"heardNoise\":").Append(t.Heard);
                sb.Append(",\"hides\":").Append(t.Hides);
                sb.Append(",\"hits\":").Append(t.Hits);
                sb.Append(",\"minManiacDistance\":").Append(
                    t.MinManiacDistance == float.MaxValue ? "null" : t.MinManiacDistance.ToString("0.00", ci));
                // When each clock landed on the run clock. A timeout row without
                // this is a guess; with it, "stopped finding clocks" and "never
                // got time to work" are two different, visible shapes.
                sb.Append(",\"clockFixTimes\":").Append(t.ClockFixTimesJson());
            }
            // Exceptions below the budget still get recorded. A run with three
            // stray NREs is probably fine, but it is not nothing, and the only
            // way to notice a slow-growing instrument fault is to keep the count.
            if (exceptionsThisRun > 0)
            {
                sb.Append(",\"exceptions\":").Append(exceptionsThisRun);
                sb.Append(",\"firstException\":\"")
                  .Append((firstException ?? "").Replace("\\", "/").Replace("\"", "'")).Append('"');
            }
            sb.Append('}');
            File.AppendAllText(outputPath, sb.ToString() + "\n");
        }

        // CastleWing has no room-rectangle data (Catacombs does), so a death is
        // attributed to the nearest thing with a name a designer recognises:
        // a clock, a wardrobe, or the gate. Raw endPos is kept for heatmaps.
        static string NearestLandmark(Vector2 pos)
        {
            string best = "?";
            float bestD = float.MaxValue;
            void Consider(Component c)
            {
                if (c == null) return;
                float d = Vector2.Distance(pos, c.transform.position);
                if (d < bestD) { bestD = d; best = c.gameObject.name; }
            }
            foreach (var c in ClockObjective.All) Consider(c);
            foreach (var s in Object.FindObjectsByType<HidingSpot>(FindObjectsSortMode.None)) Consider(s);
            Consider(Object.FindAnyObjectByType<ExitDoor>());
            return best;
        }
    }
}
