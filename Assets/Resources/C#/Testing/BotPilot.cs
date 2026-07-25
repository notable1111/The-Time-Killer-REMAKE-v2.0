// The bot's head. Drives ScriptedInputSource through a full run: explore the
// castle until it finds a clock, repair it with a fallible skill-check model,
// run or hide when the maniac closes in, and leave through the gate once the
// last clock is done.
//
// Structured like ManiacController on purpose — a StateMachine of small states
// plus a Choose() that re-picks the state every frame — so anyone who has read
// the maniac's AI can read this one.
//
// It only ever presses the same four inputs a person has (move, sprint, E,
// Space) and only ever reacts to things BotMemory says it has seen. Everything
// it does badly, it does badly on purpose: see BotProfileConfig.
using System.Collections.Generic;
using TimeKiller.Core;
using TimeKiller.Hiding;
using TimeKiller.Maniac;
using TimeKiller.Navigation;
using TimeKiller.Objectives;
using TimeKiller.Player;
using UnityEngine;

namespace TimeKiller.Testing
{
    // First on the physics step, ahead of ScriptedInputSource (-150), the player's
    // states (0) and PlayerMotor (50). The chain think -> steer -> state -> body
    // therefore completes inside ONE fixed step, at every timeScale. It used to
    // straddle whatever order Unity happened to pick, which cost one frame or two
    // depending on the run — the kind of hidden variable that makes a batch
    // unreproducible even at a fixed seed.
    [DefaultExecutionOrder(-200)]
    public class BotPilot : MonoBehaviour, INavDebugSource
    {
        public BotProfileConfig Profile;
        public int Seed = 1;

        /// Half the player capsule (0.55 x 0.4 — see PlayerSetup). The A/B knob
        /// for the batch harness: 0 reproduces the old hairline string-pull that
        /// GridPathfinder documents, so one batch can run both pathfindings over
        /// matched seeds instead of comparing against uninstrumented history.
        /// Nothing in normal play changes it.
        public const float DefaultBodyRadius = 0.275f;
        public float BodyRadius = DefaultBodyRadius;

        // ---- results the batch runner reads ----
        public int SkillChecksAttempted { get; private set; }
        public int SkillChecksHit { get; private set; }
        public int AccidentalHides { get; private set; }   // E next to a clock grabbed a wardrobe instead
        public int ExploreTargets { get; private set; }
        public string CurrentGoal => machine.Current?.GetType().Name ?? "none";
        public BotMemory Memory => memory;

        // nav is REBUILT mid-run when the gate opens, so reading the live
        // instance would silently drop every stuck event before that moment —
        // and the escape leg is the shortest, so almost all of them.
        int stuckEventsCarry, stuckGiveUpsCarry;
        float wedgedCarry, travelCarry;

        public int StuckEvents => stuckEventsCarry + (nav != null ? nav.StuckEvents : 0);
        public int StuckGiveUps => stuckGiveUpsCarry + (nav != null ? nav.StuckGiveUps : 0);
        public float WedgedSeconds => wedgedCarry + (nav != null ? nav.WedgedSeconds : 0f);
        public float TravelSeconds => travelCarry + (nav != null ? nav.TravelSeconds : 0f);

        void RetireNav()
        {
            if (nav == null) return;
            stuckEventsCarry += nav.StuckEvents;
            stuckGiveUpsCarry += nav.StuckGiveUps;
            wedgedCarry += nav.WedgedSeconds;
            travelCarry += nav.TravelSeconds;
        }

        PlayerController player;
        ScriptedInputSource input;
        ClockRepair repair;
        PlayerHiding hiding;
        ClockConfig clockConfig;
        ManiacController maniac;

        BotPath nav;
        BotMemory memory;
        System.Random rng;
        readonly StateMachine machine = new StateMachine();

        // Threat model — what the bot believes about the maniac right now.
        bool threatKnown;
        Vector2 threatPos;
        float threatSeenAt = float.NegativeInfinity;
        string maniacState = "PatrolState";
        float maniacStateAt;

        bool fleeing;               // hysteresis latch for Choose()
        float sprintRoll;           // re-rolled per travel leg
        float repairBlockedUntil;   // every known clock is unreachable right now — go explore instead

        public bool Ready { get; private set; }

        public void Begin(BotProfileConfig profile, int seed)
        {
            Profile = profile;
            Seed = seed;
            rng = new System.Random(seed);

            player = GetComponent<PlayerController>();
            input = GetComponent<ScriptedInputSource>();
            repair = GetComponent<ClockRepair>();
            hiding = GetComponent<PlayerHiding>();
            maniac = Object.FindAnyObjectByType<ManiacController>();
            clockConfig = Resources.Load<ClockConfig>("C#/Objectives/Configs/ClockConfig");

            stuckEventsCarry = stuckGiveUpsCarry = 0;
            wedgedCarry = travelCarry = 0f;   // a reused component must not carry the last run's wall

            nav = new BotPath(transform.position, bodyRadius: BodyRadius);
            memory = new BotMemory(transform, nav, Profile.sightRange, Profile.knowsEverything);
            NavDebugView.Register(this);   // F3 in play mode paints what the bot sees

            EventBus.Subscribe<ManiacStateChangedEvent>(OnManiacState);
            EventBus.Subscribe<AllClocksFixedEvent>(OnAllFixed);

            machine.ChangeState(new ExploreState(this));
            Ready = true;

            DebugOverlay.Watch("Bot", () =>
                $"{Profile.profileName} {CurrentGoal} clocks {memory.ClocksKnown}/{ClockObjective.All.Count} map {memory.ExploredCount}/{memory.ExplorableCount}");
        }

        void OnDestroy()
        {
            EventBus.Unsubscribe<ManiacStateChangedEvent>(OnManiacState);
            EventBus.Unsubscribe<AllClocksFixedEvent>(OnAllFixed);
            DebugOverlay.Unwatch("Bot");
            NavDebugView.Unregister(this);
        }

        // --- INavDebugSource: same overlay the maniac uses, the bot's own body ---
        public string NavLabel => "Bot";
        public WalkabilityGrid NavGrid => nav?.Grid;
        public GridPathfinder NavFinder => nav?.Finder;
        public IReadOnlyList<Vector2> NavPath => nav?.CurrentPath;
        public Vector2 NavPosition => transform.position;

        void OnManiacState(ManiacStateChangedEvent e) { maniacState = e.StateName; maniacStateAt = Time.time; }

        // The gate crashing open is an AlwaysHeard world noise — and it reopens
        // a doorway the walkability grid sampled as solid, so repath from scratch.
        void OnAllFixed(AllClocksFixedEvent e)
        {
            memory.HearExitOpen();
            RetireNav();
            nav = new BotPath(transform.position, bodyRadius: BodyRadius);
            machine.ChangeState(new EscapeState(this));
        }

        // The think runs on the PHYSICS clock, not the render frame.
        //
        // FixedUpdate is a fixed cadence in GAME time (0.02 s) whatever timeScale
        // is, so the bot senses, re-chooses and re-steers exactly as often per
        // game-second at 6x as at 1x. On Update it did that 60 times a game-second
        // at 1x and 15 at 4x, while every loop on the maniac's side — awareness
        // integration, motor steering, all his Time.time timers — is already
        // speed-invariant. Acceleration was not making him smarter; it was
        // stripping the bot, and the win rate was reading the difference as
        // difficulty.
        //
        // NOT the same as ticking this N times inside one Update: nothing moves
        // between those slices, so N identical linecasts buy nothing.
        void FixedUpdate()
        {
            if (!Ready || player == null) return;
            memory.Observe();
            SenseManiac();
            Choose();
            machine.Tick(Time.fixedDeltaTime);
        }

        // ---- threat perception ----------------------------------------------

        void SenseManiac()
        {
            if (maniac == null) { threatKnown = false; return; }

            Vector2 me = transform.position, him = maniac.transform.position;
            float d = Vector2.Distance(me, him);
            bool chasing = (maniacState == "ChaseState" || maniacState == "AttackState")
                           && Time.time - maniacStateAt < 6f;

            // Seen (line of sight), heard up close (footsteps/breathing), or
            // unmistakably being chased — a screaming man behind you is not subtle.
            bool sensed = (d <= Profile.sightRange && ClearLine(me, him))
                       || d <= 5f
                       || (chasing && d <= 14f);

            if (sensed) { threatKnown = true; threatPos = him; threatSeenAt = Time.time; }
            else if (Time.time - threatSeenAt > 2.5f) threatKnown = false;
        }

        bool ClearLine(Vector2 a, Vector2 b)
        {
            foreach (var hit in Physics2D.LinecastAll(a, b))
            {
                if (hit.collider == null || hit.collider.isTrigger) continue;
                var root = hit.collider.transform.root;
                if (root == transform.root || (maniac != null && root == maniac.transform.root)) continue;
                return false;
            }
            return true;
        }

        float ThreatDistance => threatKnown ? Vector2.Distance(transform.position, threatPos) : float.MaxValue;

        // ---- goal selection --------------------------------------------------

        void Choose()
        {
            if (hiding != null && hiding.IsHidden)
            {
                if (!(machine.Current is HideState)) machine.ChangeState(new HideState(this));
                return;
            }

            // Hysteresis: panic at panicDistance, calm down only at calmDistance.
            if (threatKnown && ThreatDistance <= Profile.panicDistance) fleeing = true;
            else if (!threatKnown || ThreatDistance >= Profile.calmDistance) fleeing = false;

            if (fleeing)
            {
                if (!(machine.Current is FleeState)) machine.ChangeState(new FleeState(this));
                return;
            }

            bool allFixed = ObjectiveManager.Instance != null && ObjectiveManager.Instance.AllFixed;
            if (allFixed)
            {
                if (!(machine.Current is EscapeState)) machine.ChangeState(new EscapeState(this));
                return;
            }

            bool haveClock = false;
            foreach (var _ in memory.KnownUnfixedClocks()) { haveClock = true; break; }
            if (haveClock && Time.time >= repairBlockedUntil)
            {
                if (!(machine.Current is RepairState)) machine.ChangeState(new RepairState(this));
                return;
            }

            if (!(machine.Current is ExploreState)) machine.ChangeState(new ExploreState(this));
        }

        // ---- shared movement -------------------------------------------------

        void RollSprint() => sprintRoll = (float)rng.NextDouble();
        bool SprintNow => sprintRoll < Profile.sprintTendency;

        /// Walk the current path. Returns the follower result so states can react
        /// to Arrived / Failed without knowing anything about A*.
        BotPath.Result Drive(bool sprint)
        {
            // dt is passed, not read inside BotPath: the follower now ticks on the
            // physics clock and Time.deltaTime there is the RENDER frame's, which
            // would over-count travelSeconds by ~3.3x at 4x — inflating the
            // denominator of every wall-stuck rate and hiding real wedging.
            var result = nav.Tick(transform.position, Time.fixedDeltaTime, out var waypoint);
            if (result == BotPath.Result.Following)
            {
                input.Target = waypoint;
                input.Run = sprint;
            }
            else
            {
                input.Target = null;
                input.Run = false;
            }
            return result;
        }

        void Halt() { input.Target = null; input.Run = false; nav.Clear(); }

        // ---- states ----------------------------------------------------------

        /// Walk the map looking for clocks. This is the state that exists because
        /// the bot is not allowed to know where they are.
        class ExploreState : IState
        {
            readonly BotPilot b;
            float retargetAt;
            public ExploreState(BotPilot bot) => b = bot;

            public void Enter() { b.RollSprint(); Retarget(); }
            public void Exit() => b.Halt();
            public void FixedTick(float f) { }

            public void Tick(float dt)
            {
                var r = b.Drive(b.SprintNow);
                if (r == BotPath.Result.Arrived || r == BotPath.Result.Failed || Time.time > retargetAt)
                {
                    if (r == BotPath.Result.Failed) b.memory.GiveUpOn(b.nav.Destination);
                    Retarget();
                }
            }

            void Retarget()
            {
                retargetAt = Time.time + 10f;   // never commit forever to one room
                b.RollSprint();
                for (int attempt = 0; attempt < 4; attempt++)
                {
                    if (!b.memory.TryPickExplorationTarget(b.transform.position, b.Profile.routeKnowledge, b.rng, out var t))
                    {
                        // Nothing left unexplored: sweep back over the map rather
                        // than freeze — clocks can be missed on a first pass.
                        if (b.nav.TryFindOpenPoint(b.transform.position, RandomDir(b.rng), 14f, b.rng, out var wander)
                            && b.nav.SetDestination(b.transform.position, wander)) return;
                        continue;
                    }
                    if (b.nav.SetDestination(b.transform.position, b.nav.SnapToWalkable(t)))
                    {
                        b.ExploreTargets++;
                        return;
                    }
                    b.memory.GiveUpOn(t);   // unreachable patch — stop offering it
                }
            }

            static Vector2 RandomDir(System.Random rng)
            {
                float a = (float)rng.NextDouble() * Mathf.PI * 2f;
                return new Vector2(Mathf.Cos(a), Mathf.Sin(a));
            }
        }

        /// Walk to a known broken clock and play the skill-check badly.
        class RepairState : IState
        {
            readonly BotPilot b;
            readonly Dictionary<ClockObjective, float> avoidUntil = new Dictionary<ClockObjective, float>();
            ClockObjective target;
            bool atClock;
            float nextPressAt;
            float pressDueAt = -1f;
            float progressAtPress;
            bool awaitingResult;
            float resolveBy;
            float reapproachAt;
            float giveUpAt;
            int dir = 1;
            float lastMarker;
            bool aimed;              // a press is already planned — do not re-decide per frame
            float aimedZone = -1f;   // the zone centre that plan was computed against

            public RepairState(BotPilot bot) => b = bot;

            public void Enter() { b.RollSprint(); PickTarget(); }
            public void Exit() => b.Halt();
            public void FixedTick(float f) { }

            public void Tick(float dt)
            {
                // Resolve BEFORE the IsFixed check — the press that completes a
                // clock is a hit, and this used to throw it away.
                ResolvePendingPress();

                if (target == null || target.IsFixed)
                {
                    PickTarget();
                    // Known clocks, none reachable — hand the run back to Explore
                    // for a while rather than standing in a doorway thinking.
                    if (target == null) { b.repairBlockedUntil = Time.time + 8f; return; }
                }

                if (!atClock)
                {
                    float d = Vector2.Distance(b.transform.position, target.transform.position);
                    if (d <= b.InteractRange * 0.8f)
                    {
                        b.Halt();
                        atClock = true;
                        b.input.QueueInteract();
                        reapproachAt = Time.time + 0.6f;
                        return;
                    }

                    var r = b.Drive(b.SprintNow);
                    if (r == BotPath.Result.Following) return;

                    // A* stops on the last walkable cell, which the clock's own
                    // blocking collider can push out of interact range. Close the
                    // final metres by steering straight — it is in plain sight.
                    if (d <= 4f) { b.input.Target = target.transform.position; b.input.Run = false; }
                    if (Time.time > giveUpAt)
                    {
                        avoidUntil[target] = Time.time + 20f;   // something is in the way; try another
                        PickTarget();
                    }
                    return;
                }

                // E next to a clock that stands beside a wardrobe can open the
                // wardrobe instead (both read InteractPressed at 2.2 range).
                // Back out, note it, and step off before trying again.
                if (b.hiding != null && b.hiding.IsHidden)
                {
                    b.AccidentalHides++;
                    b.input.QueueInteract();
                    atClock = false;
                    return;
                }

                if (!b.repair.Repairing)
                {
                    if (Time.time < reapproachAt) return;   // give the press a frame to land
                    atClock = false;                        // knocked off it — walk back
                    b.nav.SetDestination(b.transform.position, target.transform.position);
                    return;
                }

                TrackDirection();
                TryPress();
            }

            void PickTarget()
            {
                atClock = false;
                target = null;
                giveUpAt = Time.time + 30f;
                float best = float.MaxValue;
                foreach (var c in b.memory.KnownUnfixedClocks())
                {
                    if (avoidUntil.TryGetValue(c, out var until) && Time.time < until) continue;
                    float d = Vector2.Distance(b.transform.position, c.transform.position);
                    if (d < best) { best = d; target = c; }
                }
                if (target != null && !b.nav.SetDestination(b.transform.position, target.transform.position))
                {
                    avoidUntil[target] = Time.time + 20f;   // no route today — look elsewhere
                    target = null;
                }
            }

            // A press is scored by whether the clock's progress moves — the game
            // never announces hit/miss, and neither should the bot.
            //
            // It has to WAIT for the move: QueueSkillCheck only sets a flag that
            // ScriptedInputSource turns into a one-frame press, which ClockRepair
            // then reads, and script execution order decides whether that costs
            // one frame or two. Scoring on the next Tick reported 0 hits out of
            // 16 while the clock was visibly being repaired.
            void ResolvePendingPress()
            {
                if (!awaitingResult || target == null) return;
                float delta = target.Progress - progressAtPress;
                if (delta > 0.001f) { b.SkillChecksHit++; awaitingResult = false; }
                else if (delta < -0.001f) awaitingResult = false;      // miss penalty landed
                else if (Time.time > resolveBy) awaitingResult = false; // interrupted mid-press
            }

            void TrackDirection()
            {
                float m = b.repair.Marker;
                if (m > lastMarker + 0.0001f) dir = 1;
                else if (m < lastMarker - 0.0001f) dir = -1;
                lastMarker = m;
            }

            // Decide ONCE per press, not once per frame.
            //
            // The original re-rolled the noise every Update and fired on the FIRST
            // roll that passed. The marker sweeps in game time, so the number of
            // rolls taken while it sat inside the window was set by the frame rate:
            // ~13 at 1x/60fps but only ~3 at 4x. More rolls means more chances to
            // fire on an early false positive, so the bot got WORSE the more frames
            // it had. Measured across the batches on disk: 21% hit rate at 1x, 43%
            // at 4x, 50% at 6x — monotone in timeScale. That made every accelerated
            // batch measure the accelerator instead of the game, and it also made
            // the 1x bot spend ~60% of its run standing motionless at a clock,
            // which is the strongest stealth state in the game (0.4x detection).
            //
            // Now: work out analytically WHEN the marker next reaches the zone
            // centre, apply the profile's aim error once as the timing error it
            // really is, and commit. Hit rate becomes a property of the profile —
            // which is what aimError was always supposed to mean.
            void TryPress()
            {
                // A zone re-roll invalidates a plan that has not fired yet. Observed
                // off ZoneCenter rather than inferred from our own hit, so a zone
                // moved by anything else is still handled.
                if (!Mathf.Approximately(b.repair.ZoneCenter, aimedZone))
                {
                    if (pressDueAt > 0f) pressDueAt = -1f;
                    aimed = false;
                    aimedZone = b.repair.ZoneCenter;
                }

                if (pressDueAt > 0f)
                {
                    if (Time.time < pressDueAt) return;
                    pressDueAt = -1f;
                    aimed = false;              // plan spent — re-aim for the next press
                    progressAtPress = target.Progress;
                    awaitingResult = true;
                    resolveBy = Time.time + 0.5f;
                    b.SkillChecksAttempted++;
                    b.input.QueueSkillCheck();
                    nextPressAt = Time.time + b.Profile.pressCooldown;
                    return;
                }
                if (awaitingResult || aimed || Time.time < nextPressAt) return;

                float speed = Mathf.Max(0.0001f, b.MarkerSpeed);
                float wait = TimeToCentre(b.repair.Marker, dir, b.repair.ZoneCenter, speed,
                                          b.Profile.RollReaction(b.rng));
                // Aim error is a POSITION error in marker units; divide by marker
                // speed to land it as the timing error it actually is.
                pressDueAt = Time.time + wait + b.Profile.RollAimError(b.rng) / speed;
                aimed = true;
            }

            // Seconds until the marker next sits on `centre`, given the bot cannot
            // act sooner than `minLead` (its reaction time).
            //
            // Marker = PingPong(t), so fold the 0..1 bar into a 0..2 loop where the
            // sweep only ever moves forward. In that space the centre is crossed at
            // TWO places — `centre` on the way up and `2 - centre` on the way back —
            // and the bounce at either end needs no special case.
            static float TimeToCentre(float marker, int dir, float centre, float speed, float minLead)
            {
                float p = dir >= 0 ? marker : 2f - marker;
                float toUp = Mathf.Repeat(centre - p, 2f);
                float toDown = Mathf.Repeat((2f - centre) - p, 2f);
                float first = Mathf.Min(toUp, toDown), second = Mathf.Max(toUp, toDown);

                if (first / speed >= minLead) return first / speed;
                if (second / speed >= minLead) return second / speed;
                return (first + 2f) / speed;   // both too close — take the next lap
            }
        }

        /// Break off and get away — outrun him, or dive into a wardrobe.
        class FleeState : IState
        {
            readonly BotPilot b;
            HidingSpot spot;
            float retargetAt;

            public FleeState(BotPilot bot) => b = bot;

            public void Enter()
            {
                // Runner or hider? Rolled once per panic, not per frame, so the
                // bot commits to a decision the way a frightened person does.
                spot = null;
                if (b.rng.NextDouble() < b.Profile.hideBias)
                    spot = b.memory.NearestKnownFreeSpot(b.transform.position, b.threatPos, 13f);
                if (spot != null && b.nav.SetDestination(b.transform.position, spot.transform.position)) return;
                spot = null;
                RunAway();
            }

            public void Exit() => b.Halt();
            public void FixedTick(float f) { }

            public void Tick(float dt)
            {
                var r = b.Drive(true);   // fleeing is always a sprint

                if (spot != null)
                {
                    if (Vector2.Distance(b.transform.position, spot.transform.position) <= 2.0f)
                    {
                        b.Halt();
                        b.input.QueueInteract();   // climb in; Choose() hands over to HideState
                    }
                    else if (r == BotPath.Result.Failed) { spot = null; RunAway(); }
                    return;
                }

                if (r != BotPath.Result.Following || Time.time > retargetAt) RunAway();
            }

            void RunAway()
            {
                retargetAt = Time.time + 1.5f;   // he moves; so must the plan
                Vector2 away = ((Vector2)b.transform.position - b.threatPos).normalized;
                if (away.sqrMagnitude < 0.01f) away = Vector2.up;
                if (b.nav.TryFindOpenPoint(b.transform.position, away, 9f, b.rng, out var p))
                    b.nav.SetDestination(b.transform.position, p);
            }
        }

        /// Sit in the wardrobe and listen. Coming out too early is how people die.
        class HideState : IState
        {
            readonly BotPilot b;
            float enteredAt;
            public HideState(BotPilot bot) => b = bot;

            public void Enter() { enteredAt = Time.time; b.Halt(); }
            public void Exit() { }
            public void FixedTick(float f) { }

            public void Tick(float dt)
            {
                if (Time.time - enteredAt < b.Profile.minHideSeconds) return;
                bool safe = !b.threatKnown || b.ThreatDistance >= b.Profile.calmDistance;
                if (safe) { b.input.QueueInteract(); b.fleeing = false; }
            }
        }

        /// Every clock is fixed — walk out of the gate.
        class EscapeState : IState
        {
            readonly BotPilot b;
            float retargetAt;

            // Last-metre push. We clear the path on purpose to do it, which also
            // switches off BotPath's stuck detector — so this state carries its own.
            bool pushing;
            Vector2 pushedFrom;
            float progressAt;
            Collider2D winTrigger;

            // The gate is wall-mounted and its trigger covers the whole opening,
            // but only the LOWER slice of that box is standable — the upper half is
            // the wall the sprite hangs on. Measured live on CastleWing: the trigger
            // spans y[6.45..7.65], yet a body only fits at y[6.55..7.05]. So aim just
            // inside the trigger's NEAR edge, never at its middle.
            const float PushBite = 0.35f;        // how far past the near edge to aim
            const float StageBack = 0.45f;       // doorstep, below the gate, on the grid
            const float PushRange = 1.2f;        // only push from the doorstep itself
            const float StageHalfWidth = 0.6f;   // squared up with the opening
            const float PushStallSeconds = 1.5f;
            const float PushProgress = 0.12f;

            public EscapeState(BotPilot bot) => b = bot;

            public void Enter() { b.RollSprint(); pushing = false; winTrigger = null; Retarget(); }
            public void Exit() => b.Halt();
            public void FixedTick(float f) { }

            public void Tick(float dt)
            {
                var exit = b.memory.KnownExit;
                if (exit != null)
                {
                    Vector2 pos = b.transform.position;
                    Vector2 door = exit.transform.position;

                    // Standing on the doorstep and squared up with the opening? Then stop
                    // pathfinding and just hold the direction, the way a person does. The
                    // alignment test is the point: the old code pushed from anywhere within
                    // 2.5 units, which from the dead-end pocket west of the gate meant
                    // pressing east into a wall for the rest of the run.
                    if (!pushing
                        && Vector2.Distance(pos, door) <= PushRange
                        && pos.y <= door.y + 0.15f
                        && Mathf.Abs(pos.x - door.x) <= StageHalfWidth)
                    {
                        pushing = true;
                        pushedFrom = pos;
                        progressAt = Time.time;
                    }

                    if (pushing) { Push(pos, door); return; }
                }

                // Gate not found yet, or found but not yet stood on the doorstep —
                // either way keep WALKING. An earlier revision returned early when the
                // exit was unknown, which left honest profiles standing still until the
                // timeout instead of exploring to find the gate.
                var r = b.Drive(true);   // the way out is worth the noise
                if (r == BotPath.Result.Following && Time.time < retargetAt) return;
                Retarget();
            }

            void Push(Vector2 pos, Vector2 door)
            {
                b.nav.Clear();
                b.input.Target = PushTarget(door);
                b.input.Run = true;

                if (Vector2.Distance(pos, pushedFrom) > PushProgress)
                {
                    pushedFrom = pos;
                    progressAt = Time.time;
                    return;
                }
                // Went nowhere. Re-approach rather than grind — a silent grind here
                // is exactly what turned winnable runs into `stalled` rows.
                if (Time.time - progressAt < PushStallSeconds) return;
                pushing = false;
                Retarget();
            }

            // Just inside the near edge of the real trigger box, centred in the opening.
            Vector2 PushTarget(Vector2 door)
            {
                if (winTrigger == null)
                    foreach (var c in b.memory.KnownExit.GetComponents<Collider2D>())
                        if (c.isTrigger) { winTrigger = c; break; }

                if (winTrigger == null) return door + Vector2.up * PushBite;
                var t = winTrigger.bounds;
                return new Vector2(t.center.x, t.min.y + PushBite);
            }

            void Retarget()
            {
                retargetAt = Time.time + 4f;
                var exit = b.memory.KnownExit;
                if (exit != null)
                {
                    // Path to the DOORSTEP, not to the gate. The walkability grid is
                    // baked while the gate is still shut, so the opening itself reads
                    // as solid and a path to the gate resolves to whatever pocket is
                    // nearest — on CastleWing that is a dead end 1.5m west, behind a
                    // wall, which is where the wedged runs all ended up.
                    Vector2 door = exit.transform.position;
                    b.nav.SetDestination(b.transform.position,
                                         b.nav.SnapToWalkable(door + Vector2.down * StageBack));
                    return;
                }
                // Gate not found yet — keep searching the map for it.
                if (b.memory.TryPickExplorationTarget(b.transform.position, b.Profile.routeKnowledge, b.rng, out var t))
                    b.nav.SetDestination(b.transform.position, b.nav.SnapToWalkable(t));
            }
        }

        // ---- config reads (HUD-visible values only — no hidden state) --------

        float InteractRange => clockConfig != null ? clockConfig.interactRange : 2.2f;
        float MarkerSpeed => clockConfig != null ? clockConfig.markerSpeed : 0.75f;
    }
}
