// Player-side driver for repairing clocks. Press E near a broken clock to start;
// a marker sweeps a bar, press SPACE when it's in the green zone to add progress.
// A miss costs a little progress and makes a QUIET noise the maniac may hear
// (published as a low-loudness footstep — the hearing radius gates it to nearby).
// The maniac reaching you cancels the repair. The HUD reads the public state to
// draw the bar. Removable: no component -> clocks just sit broken.
using TimeKiller.Core;
using TimeKiller.Player;
using UnityEngine;

namespace TimeKiller.Objectives
{
    [RequireComponent(typeof(PlayerController))]
    public class ClockRepair : MonoBehaviour
    {
        [SerializeField] ClockConfig config;

        PlayerController player;
        TimeKiller.Maniac.ManiacController maniac;
        ClockObjective active;
        RepairState repairState;
        float sweepT;

        public bool Repairing => active != null;
        public ClockObjective Active => active;
        public float Marker { get; private set; }       // 0..1 sweep position
        public float ZoneCenter { get; private set; }   // 0..1 target-zone center
        public float ZoneWidth => config != null ? config.zoneWidth : 0.18f;

        public void Init(ClockConfig cfg) => config = cfg;

        /// The clock E would start right now, or null. Exposed for the interact
        /// prompt: it must offer exactly what pressing E would actually do, so it
        /// asks the same question TryStart asks rather than re-deriving it and
        /// drifting out of sync. Cheap — a distance check over the 3-clock list.
        public ClockObjective NearbyClock =>
            config != null && player != null && player.IsFreeToInterrupt ? NearestBrokenClock() : null;

        void Awake()
        {
            player = GetComponent<PlayerController>();
            repairState = new RepairState(player);
        }

        void Update()
        {
            if (config == null || player == null) return;

            if (active == null)
            {
                if (player.Input.InteractPressed) TryStart();
                return;
            }
            Repair();
        }

        void TryStart()
        {
            // Never yank the player out of someone else's state — pressing E
            // inside a wardrobe belongs to hiding, not to a clock behind it.
            if (!player.IsFreeToInterrupt) return;

            var clock = NearestBrokenClock();
            if (clock == null) return;
            active = clock;
            sweepT = 0f;
            NewZone();

            // Face the clock so the work pose points at it, then lock: repairing
            // is a commitment you have to deliberately break off.
            player.Facing.UpdateFromInput((Vector2)(clock.transform.position - transform.position));
            player.ChangeState(repairState);
        }

        void Repair()
        {
            if (active.IsFixed) { Stop(); return; }
            if (Vector2.Distance(transform.position, active.transform.position) > config.interactRange * 1.35f) { Stop(); return; }
            if (player.Input.InteractPressed) { Stop(); return; }   // E again = walk away
            if (ManiacTooClose()) { Stop(); return; }               // he caught you — run

            sweepT += Time.deltaTime * config.markerSpeed;
            Marker = Mathf.PingPong(sweepT, 1f);

            if (player.Input.SkillCheckPressed)
            {
                bool hit = Mathf.Abs(Marker - ZoneCenter) <= config.zoneWidth * 0.5f;
                if (hit)
                {
                    active.AddProgress(config.progressPerHit);
                    NewZone();
                }
                else
                {
                    active.AddProgress(-config.missPenalty);
                    EventBus.Publish(new PlayerFootstepEvent
                    {
                        Position = transform.position,
                        IsRunning = false,
                        Loudness = config.missNoiseLoudness,
                    });
                }
            }
        }

        void Stop()
        {
            active = null;
            // Only hand control back if we still hold it. If something with a
            // stronger claim (a grab, hiding) already moved the player on, this
            // must not drag them back to Idle behind its back.
            if (player.CurrentState == repairState) player.ChangeState(player.Idle);
        }

        void NewZone() => ZoneCenter = Random.Range(0.15f, 0.85f);

        ClockObjective NearestBrokenClock()
        {
            ClockObjective best = null;
            float bestD = config.interactRange;
            foreach (var c in ClockObjective.All)
            {
                if (c == null || c.IsFixed) continue;
                float d = Vector2.Distance(transform.position, c.transform.position);
                if (d <= bestD) { bestD = d; best = c; }
            }
            return best;
        }

        bool ManiacTooClose()
        {
            if (maniac == null) maniac = Object.FindAnyObjectByType<TimeKiller.Maniac.ManiacController>();
            return maniac != null &&
                Vector2.Distance(transform.position, maniac.transform.position) <= config.interruptRange;
        }

        // ---- the state ----

        /// Holds the player still at the clock. Deliberately has no transitions
        /// of its own: every way out of a repair (E, the maniac, the clock being
        /// finished, walking out of range) is decided in ClockRepair.Repair,
        /// which then calls Stop. Movement input is simply ignored while here,
        /// and that IS the lock.
        ///
        /// The class name is the animation key — PlayerAnimationDriver matches
        /// "RepairState" and plays PlayerAnimationSet.GetRepair. Renaming this
        /// class silently falls back to the idle pose.
        public class RepairState : IState
        {
            readonly PlayerController player;

            public RepairState(PlayerController player) => this.player = player;

            public void Enter() => player.Motor.SetTargetVelocity(Vector2.zero);

            public void Tick(float deltaTime) { }

            // Re-issued every physics step rather than once on Enter, so a shove
            // from the maniac's body or a lingering velocity cannot slide the
            // player off the clock while the pose says they are standing still.
            public void FixedTick(float fixedDelta) => player.Motor.SetTargetVelocity(Vector2.zero);

            public void Exit() { }
        }
    }
}
