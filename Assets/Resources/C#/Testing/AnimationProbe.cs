// Play-mode probe for the player's 8-direction animation set.
//
// Answers two questions that a screenshot cannot:
//   1. Does every input octant select the matching facing AND the matching clip
//      asset? (a swapped or missing clip looks fine in a still frame)
//   2. Do foot-contact events land on the frames the art actually plants a foot
//      on, and at what real-world rate? That rate is what PlayerFootsteps turns
//      into PlayerFootstepEvent, which is what the maniac hears — so it is
//      gameplay, not polish.
//
// Possesses the player through the same IInputSource seam the bot playtester
// uses, restores the previous source when finished, and deletes itself. Nothing
// it touches is serialised, so it cannot leave a mark on the hand-fixed scene.
using System.Collections;
using System.Collections.Generic;
using System.Text;
using TimeKiller.Core;
using TimeKiller.Player;
using UnityEngine;

namespace TimeKiller.Testing
{
    // Runs before ClockRepair and PlayerController (both default 0) so a queued
    // key press is raised in Pump BEFORE its consumers read it, and cleared on
    // the next frame — exactly one frame of "pressed", like a real tap. Without
    // this ordering a two-frame pulse would be seen twice by ClockRepair, whose
    // second read is "E again = walk away", cancelling the repair it just began.
    [DefaultExecutionOrder(-200)]
    public class AnimationProbe : MonoBehaviour
    {
        /// Drives MoveInput directly — unlike ScriptedInputSource there is no
        /// waypoint to arrive at, because we want a sustained hold in one octant.
        class DirectInput : IInputSource
        {
            public Vector2 MoveInput { get; set; }
            public bool RunHeld { get; set; }
            public bool InteractPressed { get; private set; }
            public bool SkillCheckPressed { get; private set; }

            bool interactQueued, skillQueued;

            public void QueueInteract() => interactQueued = true;
            public void QueueSkillCheck() => skillQueued = true;

            public void Pump()
            {
                InteractPressed = interactQueued;
                interactQueued = false;
                SkillCheckPressed = skillQueued;
                skillQueued = false;
            }
        }

        void Update()
        {
            input?.Pump();
        }

        // Declaration order of FacingDirection: the probe asserts that feeding
        // octant i produces facing i, so this table is the expected answer.
        static readonly (string name, Vector2 input)[] Octants =
        {
            ("Down",      new Vector2( 0f, -1f)),
            ("DownLeft",  new Vector2(-1f, -1f)),
            ("Left",      new Vector2(-1f,  0f)),
            ("UpLeft",    new Vector2(-1f,  1f)),
            ("Up",        new Vector2( 0f,  1f)),
            ("UpRight",   new Vector2( 1f,  1f)),
            ("Right",     new Vector2( 1f,  0f)),
            ("DownRight", new Vector2( 1f, -1f)),
        };

        struct Contact
        {
            public float Time;
            public int Frame;
            public string Clip;
            public float Speed;
            public bool FromBus;
        }

        PlayerController player;
        SpriteAnimator animator;
        PlayerAnimationDriver driver;
        DirectInput input;
        IInputSource previous;

        readonly List<Contact> contacts = new List<Contact>();
        bool recording;

        /// Where the report is written. A file rather than only the console,
        /// because leaving play mode reloads the domain and wipes statics — and
        /// the console is not always readable from outside the editor.
        public static string ReportPath =>
            System.IO.Path.Combine(Application.dataPath, "..", "Temp", "animation_probe.txt");

        public static void Spawn()
        {
            var probe = new GameObject("~AnimationProbe") { hideFlags = HideFlags.DontSave };
            probe.AddComponent<AnimationProbe>();
        }

#if UNITY_EDITOR
        /// Set by the menu item before it asks for play mode. Entering play mode
        /// reloads the domain, which silently drops any playModeStateChanged
        /// subscription made beforehand — so the request has to survive in
        /// SessionState and be picked up from inside the fresh domain instead.
        public const string PendingKey = "TimeKiller.AnimationProbe.Pending";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void SpawnIfRequested()
        {
            if (!UnityEditor.SessionState.GetBool(PendingKey, false)) return;
            UnityEditor.SessionState.SetBool(PendingKey, false);
            Spawn();
        }
#endif

        IEnumerator Start()
        {
            player = FindAnyObjectByType<PlayerController>();
            if (player == null)
            {
                Debug.LogError("[AnimationProbe] No PlayerController in the scene.");
                Destroy(gameObject);
                yield break;
            }

            animator = player.GetComponent<SpriteAnimator>();
            driver = player.GetComponent<PlayerAnimationDriver>();
            if (animator == null || driver == null)
            {
                Debug.LogError("[AnimationProbe] Player has no SpriteAnimator/PlayerAnimationDriver — run Setup/33.");
                Destroy(gameObject);
                yield break;
            }

            previous = player.Input;
            input = new DirectInput();
            player.SetInputSource(input);

            animator.FrameReached += OnFrameReached;
            EventBus.Subscribe<PlayerFootstepEvent>(OnFootstepEvent);

            var report = new StringBuilder();
            report.AppendLine("=== ANIMATION PROBE ===");

            yield return DirectionMatrix(report);
            yield return FootstepTiming(report);
            yield return RepairLock(report);

            animator.FrameReached -= OnFrameReached;
            EventBus.Unsubscribe<PlayerFootstepEvent>(OnFootstepEvent);
            input.MoveInput = Vector2.zero;
            input.RunHeld = false;
            player.SetInputSource(previous);

            Debug.Log(report.ToString());
            try { System.IO.File.WriteAllText(ReportPath, report.ToString()); }
            catch (System.Exception e) { Debug.LogWarning("[AnimationProbe] Could not write report: " + e.Message); }
            Destroy(gameObject);
        }

        // ---- Phase 1: does octant i select facing i and clip i? ----------------

        IEnumerator DirectionMatrix(StringBuilder report)
        {
            report.AppendLine();
            report.AppendLine("-- 8-direction matrix (input octant -> facing / state / clip) --");
            int bad = 0;

            foreach (bool run in new[] { false, true })
            {
                report.AppendLine(run ? "  [RUN]" : "  [WALK]");
                for (int i = 0; i < Octants.Length; i++)
                {
                    var (name, vector) = Octants[i];
                    input.MoveInput = vector.normalized;
                    input.RunHeld = run;

                    // A few frames: state machine ticks in Update, motor in FixedUpdate.
                    for (int f = 0; f < 8; f++) yield return null;

                    string facing = player.Facing.Current.ToString();
                    string state = driver.IsRunningState ? "Run" : driver.IsMovingState ? "Walk" : "Idle";
                    string clip = animator.CurrentClip != null ? animator.CurrentClip.name : "<null>";
                    string expectedClip = (run ? "Run_" : "Walk_") + name;

                    bool facingOk = facing == name;
                    bool clipOk = clip == expectedClip;
                    if (!facingOk || !clipOk) bad++;

                    report.AppendLine(string.Format(
                        "    {0,-10} -> facing {1,-10} state {2,-5} clip {3,-16} {4}",
                        name, facing, state, clip,
                        facingOk && clipOk ? "OK" : (facingOk ? "CLIP MISMATCH exp " + expectedClip : "FACING MISMATCH")));
                }
            }

            input.MoveInput = Vector2.zero;
            report.AppendLine("  => " + (bad == 0 ? "all 16 combinations correct" : bad + " MISMATCHES"));
        }

        // ---- Phase 2: foot contacts, on which frames and how often -------------

        IEnumerator FootstepTiming(StringBuilder report)
        {
            report.AppendLine();
            report.AppendLine("-- foot contacts (frame the event fires on, and the real rate) --");

            // Find the octant with the most clearance, so the measurement is of
            // the gait and not of the player grinding into a wall.
            Vector2 best = Vector2.down;
            float bestSpeed = -1f;
            foreach (var (_, vector) in Octants)
            {
                input.MoveInput = vector.normalized;
                input.RunHeld = true;
                for (int f = 0; f < 12; f++) yield return null;
                float speed = player.Motor.CurrentVelocity.magnitude;
                if (speed > bestSpeed) { bestSpeed = speed; best = vector.normalized; }
            }
            report.AppendLine(string.Format("  clearest heading {0} (reached {1:0.00} u/s)", best, bestSpeed));

            foreach (bool run in new[] { false, true })
            {
                // Bounce along the clear heading so we never leave open floor.
                input.RunHeld = run;
                contacts.Clear();

                float target = run ? player.Config.runSpeed : player.Config.walkSpeed;
                float speedSum = 0f;
                int speedSamples = 0;
                float t0 = Time.time;
                recording = true;

                float duration = 4f;
                float flip = 1f;
                float nextFlip = Time.time + 0.8f;
                while (Time.time - t0 < duration)
                {
                    if (Time.time >= nextFlip) { flip = -flip; nextFlip = Time.time + 0.8f; }
                    input.MoveInput = best * flip;
                    speedSum += player.Motor.CurrentVelocity.magnitude;
                    speedSamples++;
                    yield return null;
                }
                recording = false;
                input.MoveInput = Vector2.zero;

                var frames = new List<int>();
                var busCount = 0;
                float firstT = 0f, lastT = 0f;
                int n = 0;
                foreach (var c in contacts)
                {
                    if (c.FromBus) { busCount++; continue; }
                    if (n == 0) firstT = c.Time;
                    lastT = c.Time;
                    frames.Add(c.Frame);
                    n++;
                }

                float meanSpeed = speedSamples > 0 ? speedSum / speedSamples : 0f;
                float span = lastT - firstT;
                float rate = (n > 1 && span > 0f) ? (n - 1) / span : 0f;

                var uniq = new List<int>();
                foreach (var f in frames) if (!uniq.Contains(f)) uniq.Add(f);
                uniq.Sort();

                report.AppendLine(string.Format(
                    "  {0,-5} contacts={1,-3} busEvents={2,-3} onFrames=[{3}]  rate={4:0.00}/s  "
                    + "speed {5:0.00}/{6:0.00} u/s  => stride {7:0.000} u",
                    run ? "RUN" : "WALK", n, busCount, string.Join(",", uniq.ConvertAll(x => x.ToString())),
                    rate, meanSpeed, target, rate > 0f ? meanSpeed / rate : 0f));

                // Let the character settle before the next gait.
                for (int f = 0; f < 20; f++) yield return null;
            }
        }

        // ---- Phase 3: does repairing hold the player still, in the work pose? --

        IEnumerator RepairLock(StringBuilder report)
        {
            report.AppendLine();
            report.AppendLine("-- clock repair (pose + lock) --");

            var repair = player.GetComponent<TimeKiller.Objectives.ClockRepair>();
            if (repair == null)
            {
                report.AppendLine("  SKIPPED — no ClockRepair on the player.");
                yield break;
            }

            TimeKiller.Objectives.ClockObjective clock = null;
            foreach (var c in TimeKiller.Objectives.ClockObjective.All)
                if (c != null && !c.IsFixed) { clock = c; break; }
            if (clock == null)
            {
                report.AppendLine("  SKIPPED — no broken clock in the scene.");
                yield break;
            }

            // Park the maniac far away: standing within interruptRange (3u) of the
            // player cancels a repair, which would look like the lock failing.
            var maniac = FindAnyObjectByType<TimeKiller.Maniac.ManiacController>();
            Vector3 maniacHome = Vector3.zero;
            if (maniac != null)
            {
                maniacHome = maniac.transform.position;
                Teleport(maniac.transform, maniacHome + new Vector3(500f, 500f, 0f));
            }

            // Stand just south of the clock, inside interactRange (2.2u) but clear
            // of its 1.6x0.7 base collider.
            Vector3 stand = clock.transform.position + new Vector3(0f, -1.4f, 0f);
            Teleport(player.transform, stand);
            input.MoveInput = Vector2.zero;
            for (int f = 0; f < 5; f++) yield return null;

            input.QueueInteract();
            for (int f = 0; f < 6; f++) yield return null;

            string state = player.CurrentState != null ? player.CurrentState.GetType().Name : "none";
            string clip = animator.CurrentClip != null ? animator.CurrentClip.name : "<null>";
            string facing = player.Facing.Current.ToString();
            report.AppendLine(string.Format("  after E: state={0}  clip={1}  facing={2}  repairing={3}",
                state, clip, facing, repair.Repairing));
            report.AppendLine("    expected: state=RepairState, clip=Repair_Up (clock is north of us), facing=Up");

            // The lock: hold a movement key and confirm the body does not travel
            // and the state does not fall through to Walk/Run.
            Vector3 before = player.transform.position;
            input.MoveInput = Vector2.down;
            input.RunHeld = true;
            for (int f = 0; f < 40; f++) yield return null;
            float drift = Vector3.Distance(before, player.transform.position);
            string stateHeld = player.CurrentState != null ? player.CurrentState.GetType().Name : "none";
            string facingHeld = player.Facing.Current.ToString();
            report.AppendLine(string.Format(
                "  holding run+down for 40 frames: drift={0:0.000}u  state={1}  facing={2}  {3}",
                drift, stateHeld, facingHeld,
                drift < 0.05f && stateHeld == "RepairState" && facingHeld == facing ? "LOCKED OK" : "LOCK FAILED"));

            input.MoveInput = Vector2.zero;
            input.RunHeld = false;
            for (int f = 0; f < 3; f++) yield return null;

            // E again should release us back to Idle.
            input.QueueInteract();
            for (int f = 0; f < 6; f++) yield return null;
            string released = player.CurrentState != null ? player.CurrentState.GetType().Name : "none";
            report.AppendLine(string.Format("  after E again: state={0}  repairing={1}  {2}",
                released, repair.Repairing,
                released == "IdleState" && !repair.Repairing ? "RELEASED OK" : "RELEASE FAILED"));

            if (maniac != null) Teleport(maniac.transform, maniacHome);
        }

        static void Teleport(Transform t, Vector3 position)
        {
            t.position = position;
            var body = t.GetComponent<Rigidbody2D>();
            if (body != null) { body.position = position; body.linearVelocity = Vector2.zero; }
            Physics2D.SyncTransforms();
        }

        void OnFrameReached(int frame)
        {
            if (!recording) return;
            contacts.Add(new Contact
            {
                Time = Time.time,
                Frame = frame,
                Clip = animator.CurrentClip != null ? animator.CurrentClip.name : "<null>",
                Speed = player.Motor.CurrentVelocity.magnitude,
                FromBus = false,
            });
        }

        void OnFootstepEvent(PlayerFootstepEvent evt)
        {
            if (!recording) return;
            contacts.Add(new Contact { Time = Time.time, FromBus = true });
        }
    }
}
