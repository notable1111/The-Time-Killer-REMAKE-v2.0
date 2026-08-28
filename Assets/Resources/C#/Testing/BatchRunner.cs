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
// Speed: runs can be accelerated with Time.timeScale. This comment used to warn
// that "the maniac quietly gets dumber" at speed; measured on 2026-07-25, the
// truth was the exact opposite. His loops — awareness integration, motor
// steering, every Time.time timer — were the only speed-invariant ones in the
// game, while the BOT's think, steering and skill-check press were all frame
// quantized. Acceleration never buffed him; it stripped the bot, and the win
// rate read that as difficulty. Both sides now tick on the physics clock (see
// BotPilot.FixedUpdate and ScriptedInputSource), and every row records the
// frames and fixed steps per game-second the engine actually achieved, so the
// claim is checkable instead of assumed. Tick "also at 1x" to emit a
// matched-seed control block; analyze.py still compares them.
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

            /// A/B arms: one full block of runs per body radius, over the SAME
            /// seeds. 0 is the old hairline string-pull, 0.275 the real player
            /// capsule (BotPilot.DefaultBodyRadius). null = one arm at the real
            /// body. This exists because the stuck counters are new: there is no
            /// instrumented history to compare a single number against, so the
            /// baseline has to be measured in the same batch or not at all.
            public float[] BodyRadii;
        }

        static float[] Arms(Request r) =>
            r.BodyRadii != null && r.BodyRadii.Length > 0
                ? r.BodyRadii : new[] { BotPilot.DefaultBodyRadius };

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

        // ---- the hidden variable ----
        // Every finding in the 2026-07-25 speed investigation turned on how many
        // frames and physics steps the engine actually achieved per game-second,
        // and that number appeared nowhere in the output. It does now, per run.
        // Nominal fixed steps per game-second is 1 / fixedDeltaTime = 50; anything
        // below it is game time the engine did not simulate, and a row that admits
        // that is worth more than one that quietly averages it into a win rate.
        int framesAtRunStart, fixedStepsThisRun;
        float realSecondsThisRun;
        bool measuring;

        // Unity clamps how much GAME time one frame may advance to
        // maximumDeltaTime. At 4x a 100 ms real hitch asks for 0.4 s against the
        // 0.333 default, and the surplus is simply discarded — game time stalls,
        // the maniac loses ticks, and nothing anywhere says so. Scale the cap with
        // the accelerator; the ceiling stops one bad frame demanding a 50-step
        // catch-up and spiralling.
        const float MaxDeltaCeiling = 1f;
        float defaultMaxDelta = 0.3333f;

        void FixedUpdate() { if (measuring) fixedStepsThisRun++; }

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

            defaultMaxDelta = Time.maximumDeltaTime;   // restore it exactly, whatever the project set

            var arms = Arms(request);
            total = 0;
            foreach (float s in speeds) total += ProfilesAt(s).Length * RunsAt(s) * arms.Length;
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
                    // Matched seeds, twice over: the 1x control block replays the
                    // SAME seeds as the fast block (so any win-rate gap is the
                    // accelerator), and each body-radius arm replays them again
                    // (so any stuck-rate gap is the pathfinding, not the level
                    // roll or the maniac's opening patrol).
                    int count = RunsAt(speed);
                    foreach (float bodyRadius in arms)
                    {
                        for (int i = 0; i < count && !abortBatch; i++)
                            yield return RunOnce(profile, request.BaseSeed + i, speed, bodyRadius,
                                                 reload: completed + 1 < total);
                        if (abortBatch) break;
                    }
                    if (abortBatch) break;
                }
                if (abortBatch) break;
            }

            EventBus.Unsubscribe<RunEndedEvent>(OnRunEnded);
            EventBus.Unsubscribe<PlayerDiedEvent>(OnPlayerDied);
            Application.logMessageReceived -= OnLogMessage;
            Time.timeScale = 1f;
            Time.maximumDeltaTime = defaultMaxDelta;

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

        IEnumerator RunOnce(BotProfileConfig profile, int seed, float speed, float bodyRadius, bool reload)
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
                pilot.BodyRadius = bodyRadius;   // must precede Begin — it builds the grid
                pilot.Begin(profile, seed);
            }
            var telemetry = pc != null ? pc.GetComponent<TestTelemetry>() : null;

            // A run whose preconditions are broken must be RECORDED as broken.
            // The old code logged an error and `yield break`d, which wrote no row
            // at all — the batch silently shrank and nobody could tell.
            string fault = PreflightFault(pc, pilot);
            if (fault != null)
            {
                WriteFaultRow(profile, seed, speed, bodyRadius, "preflight: " + fault);
                Fail($"preflight failed — {fault}");
                yield break;
            }

            runEnded = false; runWon = false; died = false;
            Time.timeScale = speed;
            Time.maximumDeltaTime = Mathf.Clamp(defaultMaxDelta * speed, defaultMaxDelta, MaxDeltaCeiling);

            framesAtRunStart = Time.frameCount;
            fixedStepsThisRun = 0;
            measuring = true;

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

            measuring = false;
            realSecondsThisRun = Time.realtimeSinceStartup - startedReal;

            float runSeconds = GameFlow.Instance != null ? GameFlow.Instance.RunSeconds
                                                         : (Time.realtimeSinceStartup - startedReal) * speed;

            if (exceptionsThisRun >= ExceptionBudget)
            {
                WriteFaultRow(profile, seed, speed, bodyRadius, $"{exceptionsThisRun} exceptions during run: {firstException}");
                Fail($"the scene threw {exceptionsThisRun} exceptions — {firstException}");
                yield break;
            }

            string endReason = runEnded ? (runWon ? "escape" : "death") : (stalled ? "stalled" : "timeout");
            WriteRow(profile, seed, speed, bodyRadius, runSeconds, endReason, pc, pilot, telemetry);

            completed++;
            if (completed % 5 == 0 || completed == total || stalled)
                Debug.Log($"[BatchRunner] {completed}/{total} ({profile.profileName} @ {speed}x, " +
                          $"body {bodyRadius:0.###}, seed {seed}: {endReason})");

            Time.timeScale = 1f;
            Time.maximumDeltaTime = defaultMaxDelta;
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
            measuring = false;
            Time.timeScale = 1f;
            Time.maximumDeltaTime = defaultMaxDelta;
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
            // The A/B arms, so a reader can tell "this batch measured one
            // pathfinding" from "it measured two" without scanning every row.
            var arms = Arms(request);
            sb.Append(",\"bodyRadii\":[");
            for (int i = 0; i < arms.Length; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append(arms[i].ToString("0.###", ci));
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
        void WriteFaultRow(BotProfileConfig profile, int seed, float speed, float bodyRadius, string fault)
        {
            var ci = CultureInfo.InvariantCulture;
            var sb = new StringBuilder("{\"harnessError\":true");
            sb.Append(",\"seed\":").Append(seed);
            sb.Append(",\"profile\":\"").Append(profile != null ? profile.profileName : "?").Append('"');
            sb.Append(",\"scene\":\"").Append(SceneManager.GetActiveScene().name).Append('"');
            sb.Append(",\"speed\":").Append(speed.ToString("0.##", ci));
            // Carried even on a fault row: if one arm dies more than the other,
            // that IS a finding about the arm, not noise to be dropped.
            sb.Append(",\"bodyRadius\":").Append(bodyRadius.ToString("0.###", ci));
            sb.Append(",\"endReason\":\"harness_error\"");
            sb.Append(",\"atRun\":").Append(completed + 1);
            sb.Append(",\"fault\":\"").Append(fault.Replace("\\", "/").Replace("\"", "'")).Append('"');
            sb.Append('}');
            File.AppendAllText(outputPath, sb.ToString() + "\n");
            Debug.LogError($"[BatchRunner] HARNESS ERROR at run {completed + 1}/{total} (seed {seed}): {fault}");
        }

        void WriteRow(BotProfileConfig profile, int seed, float speed, float bodyRadius,
                      float runSeconds, string endReason,
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
            // The A/B arm. 0 = old hairline string-pull, 0.275 = real player body.
            sb.Append(",\"bodyRadius\":").Append(bodyRadius.ToString("0.###", ci));
            sb.Append(",\"won\":").Append(runWon ? "true" : "false");
            sb.Append(",\"endReason\":\"").Append(endReason).Append('"');
            sb.Append(",\"runSeconds\":").Append(runSeconds.ToString("0.0", ci));
            sb.Append(",\"clocksFixed\":").Append(om != null ? om.FixedCount : 0);
            sb.Append(",\"clocksTotal\":").Append(om != null ? om.Total : 0);
            sb.Append(",\"endPos\":[").Append(endPos.x.ToString("0.0", ci)).Append(',').Append(endPos.y.ToString("0.0", ci)).Append(']');
            sb.Append(",\"nearestLandmark\":\"").Append(NearestLandmark(endPos)).Append('"');

            // What the ENGINE actually did, not what timeScale asked for. Every
            // conclusion in the speed investigation rests on these two rates and
            // neither was recorded, so no batch on disk can be re-audited for the
            // frame rate it ran at. fixedPerGameSecond should sit at 1/fixedDelta
            // (50); a shortfall is game time the physics loop never simulated.
            float fps = runSeconds > 0.01f ? (Time.frameCount - framesAtRunStart) / runSeconds : 0f;
            float fixedPer = runSeconds > 0.01f ? fixedStepsThisRun / runSeconds : 0f;
            sb.Append(",\"engine\":{\"framesPerGameSecond\":").Append(fps.ToString("0.0", ci))
              .Append(",\"fixedStepsPerGameSecond\":").Append(fixedPer.ToString("0.0", ci))
              .Append(",\"nominalFixedSteps\":").Append((1f / Time.fixedDeltaTime).ToString("0.0", ci))
              .Append(",\"realSeconds\":").Append(realSecondsThisRun.ToString("0.0", ci))
              .Append('}');

            if (pilot != null)
            {
                sb.Append(",\"skillChecks\":{\"attempted\":").Append(pilot.SkillChecksAttempted)
                  .Append(",\"hit\":").Append(pilot.SkillChecksHit).Append('}');
                sb.Append(",\"clocksFound\":").Append(pilot.Memory != null ? pilot.Memory.ClocksKnown : 0);
                sb.Append(",\"mapExplored\":").Append(pilot.Memory != null && pilot.Memory.ExplorableCount > 0
                    ? (100f * pilot.Memory.ExploredCount / pilot.Memory.ExplorableCount).ToString("0", ci) : "0");
                sb.Append(",\"accidentalHides\":").Append(pilot.AccidentalHides);
                // Wall-stuck. travelSeconds is the denominator — compare rates,
                // never raw counts, because the arms do not survive equally long.
                sb.Append(",\"stuck\":{\"events\":").Append(pilot.StuckEvents)
                  .Append(",\"giveUps\":").Append(pilot.StuckGiveUps)
                  .Append(",\"wedgedSeconds\":").Append(pilot.WedgedSeconds.ToString("0.0", ci))
                  .Append(",\"travelSeconds\":").Append(pilot.TravelSeconds.ToString("0.0", ci))
                  .Append('}');
            }
            if (t != null)
            {
                sb.Append(",\"spotted\":").Append(t.Spotted);
                // Total kept for continuity with pre-split files; the two causes
                // are what you actually read. One is the player being audible,
                // the other is his sight meter twitching — different fixes.
                sb.Append(",\"heardNoise\":").Append(t.Heard);
                sb.Append(",\"heardSound\":").Append(t.HeardSound);
                sb.Append(",\"heardSuspicion\":").Append(t.HeardSuspicion);
                sb.Append(",\"heardBlood\":").Append(t.HeardBlood);
                sb.Append(",\"hides\":").Append(t.Hides);
                // WHERE the bot hid. "9 hides" cannot distinguish one wardrobe
                // used nine times from nine wardrobes used once, and those are
                // opposite verdicts on the level's cover.
                sb.Append(",\"hideSpots\":").Append(t.HideSpotsJson());
                sb.Append(",\"hits\":").Append(t.Hits);
                sb.Append(",\"minManiacDistance\":").Append(
                    t.MinManiacDistance == float.MaxValue ? "null" : t.MinManiacDistance.ToString("0.00", ci));
                // When each clock landed on the run clock. A timeout row without
                // this is a guess; with it, "stopped finding clocks" and "never
                // got time to work" are two different, visible shapes.
                sb.Append(",\"clockFixTimes\":").Append(t.ClockFixTimesJson());
                // The SHAPE of the run: near misses, chase episodes, dread and
                // dead air, plus a 12-bucket threat curve. Win rate says whether
                // the bot escaped; this says whether escaping was frightening.
                sb.Append(",\"tension\":").Append(t.TensionJson());
                // Blood-tracking feasibility (2026-08-02): would a "1 HP + wet
                // blood" rule ever fire? Read `foundWetAtLastHp` against
                // `secondsAtLastHp` — the count alone cannot tell a viable
                // mechanic from a bot that was never at 1 HP to begin with.
                sb.Append(",\"blood\":").Append(t.BloodJson());
                // Composure's own value. See TestTelemetry: without it a null
                // A/B result cannot be told from a feature that never ticked.
                sb.Append(",\"composure\":").Append(t.ComposureJson());
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
        //
        // Those names are NOT unique: HidingSetup builds every wardrobe as
        // "WardrobeA" or "WardrobeB", so the castle's wardrobes collapsed into two
        // buckets — and two-thirds of all deaths happen at a wardrobe, which is
        // precisely where the map needed to be readable. Disambiguate here rather
        // than renaming objects in the scene: the hall is hand-fixed and protected,
        // and an ordinal taken from position-sorted duplicates is stable across
        // runs for as long as the layout is.
        static string NearestLandmark(Vector2 pos)
        {
            var named = new List<KeyValuePair<string, Vector2>>();
            void Add(Component c)
            {
                if (c == null) return;
                named.Add(new KeyValuePair<string, Vector2>(c.gameObject.name, c.transform.position));
            }
            foreach (var c in ClockObjective.All) Add(c);
            foreach (var s in Object.FindObjectsByType<HidingSpot>(FindObjectsSortMode.None)) Add(s);
            Add(Object.FindAnyObjectByType<ExitDoor>());
            if (named.Count == 0) return "?";

            // Sorting is what makes the ordinal deterministic: FindObjectsByType
            // gives no order guarantee, so an unsorted index would rename the same
            // wardrobe between two runs of the same batch.
            named.Sort((a, b) =>
            {
                int byName = string.CompareOrdinal(a.Key, b.Key);
                if (byName != 0) return byName;
                int byX = a.Value.x.CompareTo(b.Value.x);
                return byX != 0 ? byX : a.Value.y.CompareTo(b.Value.y);
            });

            int bestIndex = 0;
            float bestD = float.MaxValue;
            for (int i = 0; i < named.Count; i++)
            {
                float d = Vector2.Distance(pos, named[i].Value);
                if (d < bestD) { bestD = d; bestIndex = i; }
            }

            string name = named[bestIndex].Key;
            int copies = 0, ordinal = 0;
            for (int i = 0; i < named.Count; i++)
                if (named[i].Key == name) { copies++; if (i <= bestIndex) ordinal = copies; }
            return copies > 1 ? name + "#" + ordinal : name;
        }
    }
}
