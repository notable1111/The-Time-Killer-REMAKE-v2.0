// Player-side driver for repairing clocks. Press E near a broken clock to start;
// a marker sweeps a bar, press SPACE when it's in the green zone to add progress.
// The maniac reaching you cancels the repair. The HUD reads the public state to
// draw the bar. Removable: no component -> clocks just sit broken.
//
// NOISE IS THE POINT (2026-08-02). Three separate sounds leave a repair, and they
// are what make standing still at a clock a decision rather than a formality:
//
//   WORKING  — a steady quiet wind from the CLOCK, whether or not you are hitting
//              the presses. Before this, a skilled player was completely silent
//              and repairing was free; all the danger lived in fumbling and
//              evaporated the moment someone got good at the mini-game. Skill now
//              buys a SHORTER exposure, never total safety.
//   MISS     — louder, from the PLAYER, and scaled by fear (see fearMissNoiseBoost).
//   THE RING — the miss drawn at its true radius, so "fumbling summons him" is a
//              rule the player can actually learn instead of bad luck.
//
// The difficulty of the press itself deliberately barely moves with fear. The
// STAKES scale, not the skill: a press you earned is never taken from you.
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
        float nextWorkNoiseAt;

        public bool Repairing => active != null;
        public ClockObjective Active => active;
        public float Marker { get; private set; }       // 0..1 sweep position
        public float ZoneCenter { get; private set; }   // 0..1 target-zone center
        /// Live target-zone width. NOT the raw config value any more: fear narrows
        /// it, so the UI must read THIS or the green band it draws would stop
        /// matching the band the hit test actually uses — the player would be
        /// judged against a zone they cannot see.
        public float ZoneWidth => config == null ? 0.18f
            : config.zoneWidth * Mathf.Lerp(1f, config.fearZoneShrink, FearFactor);

        /// Live sweep speed, likewise fear-scaled.
        public float MarkerSpeed => config == null ? 0.75f
            : config.markerSpeed * Mathf.Lerp(1f, config.fearSpeedBoost, FearFactor);

        /// 0..1 dread driving both. Reads the conductor through the event bus, so
        /// deleting the Fear feature leaves the mini-game at its calm values
        /// rather than breaking it.
        public float FearFactor => config != null && config.fearAffectsRepair && haveFear
            ? Mathf.Clamp01(fear) : 0f;

        float fear;
        bool haveFear;

        public void Init(ClockConfig cfg) => config = cfg;

        void OnEnable() => TimeKiller.Core.EventBus.Subscribe<TimeKiller.Fear.FearChangedEvent>(OnFear);
        void OnDisable() => TimeKiller.Core.EventBus.Unsubscribe<TimeKiller.Fear.FearChangedEvent>(OnFear);

        void OnFear(TimeKiller.Fear.FearChangedEvent e) { fear = e.Fear; haveFear = true; }

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
            nextWorkNoiseAt = 0f;
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

            sweepT += Time.deltaTime * MarkerSpeed;
            Marker = Mathf.PingPong(sweepT, 1f);
            EmitWorkingNoise();

            if (player.Input.SkillCheckPressed)
            {
                bool hit = Mathf.Abs(Marker - ZoneCenter) <= ZoneWidth * 0.5f;
                if (hit)
                {
                    active.AddProgress(config.progressPerHit);

                    // Announced so the press can be SEEN and HEARD. A miss has
                    // had a ring and a noise since 2026-08-02; success had
                    // nothing but a bar that moved, which is backwards — the
                    // beat the mini-game rewards you for was the quietest one
                    // in it.
                    //
                    // Skipped when THIS press finished the clock: AddProgress
                    // above has already run Fix() and published ClockFixedEvent,
                    // and firing both would stack the small burst underneath the
                    // big one on the same frame.
                    if (!active.IsFixed)
                    {
                        EventBus.Publish(new ClockHitEvent
                        {
                            Position = active.EffectPoint,
                            Progress = active.Progress,
                            Fear = FearFactor,
                        });
                    }

                    NewZone();
                }
                else
                {
                    active.AddProgress(-config.missPenalty);
                    // THE STAKES SCALE, NOT THE DIFFICULTY. The press window is
                    // deliberately left alone (see fearSpeedBoost), so skill is
                    // never taken away — what rises is the price of fumbling.
                    // Calm, a miss is a small setback; panicking with him near, the
                    // same miss is a beacon that carries roughly twice as far.
                    float loudness = Mathf.Clamp01(config.missNoiseLoudness *
                        Mathf.Lerp(1f, config.fearMissNoiseBoost, FearFactor));
                    EventBus.Publish(new PlayerFootstepEvent
                    {
                        Position = transform.position,
                        IsRunning = false,
                        Loudness = loudness,
                    });
                    // Announced so the mistake can be SEEN as well as heard. The
                    // player has to be able to learn "fumbling summons him", and a
                    // consequence you only ever hear is one most players never
                    // connect to its cause. Carries the loudness so a visual can
                    // size itself to the real noise rather than guessing.
                    EventBus.Publish(new ClockMissEvent
                    {
                        Position = transform.position,
                        Loudness = loudness,
                        Fear = FearFactor,
                    });
                }
            }
        }

        /// The clock is loud while you work it, hit or miss.
        ///
        /// Published from the CLOCK's position, not the player's: it is the
        /// mechanism winding, and a maniac tracking it should be drawn to the
        /// thing making the sound. Goes out as a WorldNoiseEvent rather than a
        /// footstep because that is what it is — and that path already carries
        /// the wall muffling, so a clock behind stone is genuinely quieter.
        void EmitWorkingNoise()
        {
            if (config.repairNoiseLoudness <= 0f) return;
            if (Time.time < nextWorkNoiseAt) return;
            nextWorkNoiseAt = Time.time + Mathf.Max(0.1f, config.repairNoiseInterval);
            EventBus.Publish(new WorldNoiseEvent
            {
                Position = active.transform.position,
                Loudness = config.repairNoiseLoudness,
                AlwaysHeard = false,   // distance and walls both still gate it
            });
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
