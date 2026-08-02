// Composition root of the maniac: wires config, motor, perception, breadcrumbs
// and patrol route into the state machine (Patrol/Investigate/Chase/Attack),
// publishes state changes on the EventBus, feeds the perception cone with his
// movement direction. Built in the scene by TimeKiller/Setup/20.
using TimeKiller.Core;
using TimeKiller.Navigation;
using TimeKiller.Player;
using UnityEngine;

namespace TimeKiller.Maniac
{
    [RequireComponent(typeof(ManiacMotor))]
    [RequireComponent(typeof(ManiacPerception))]
    [RequireComponent(typeof(ManiacBreadcrumbs))]
    [RequireComponent(typeof(ManiacNavigator))]
    public class ManiacController : MonoBehaviour
    {
        [SerializeField] ManiacConfig config;
        [SerializeField] ManiacPatrolRoute route;

        public ManiacConfig Config => config;
        public ManiacPatrolRoute Route => route;
        public ManiacMotor Motor { get; private set; }
        public ManiacPerception Perception { get; private set; }
        public ManiacBreadcrumbs Breadcrumbs { get; private set; }
        public ManiacNavigator Nav { get; private set; }

        public PatrolState Patrol { get; private set; }
        public InvestigateState Investigate { get; private set; }
        public SearchState Search { get; private set; }
        public ChaseState Chase { get; private set; }
        public AttackState Attack { get; private set; }

        public Vector2 PlayerPosition => player != null ? (Vector2)player.position : Motor.Position;

        /// Next Time.time a swing is allowed — chase keeps running in between.
        public float NextAttackAllowed { get; set; }

        /// The Outlast rule: set when the player hides while he had recent eyes
        /// on them — he marches to this spot and drags a hit out of it.
        public Vector2? CompromisedSpot { get; private set; }

        readonly StateMachine stateMachine = new StateMachine();
        ManiacBrain brain;
        Transform player;
        Collider2D ownCollider;
        Collider2D playerCollider;
        float phaseUntil;
        bool phasing;

        /// Called by AttackState when a hit lands: the player can slip through
        /// his body during the escape window — an unpushable maniac must never
        /// pin a cornered player. Collision restores once they separate.
        public void BeginPhaseThrough()
        {
            if (ownCollider == null) ownCollider = GetComponent<Collider2D>();
            if (playerCollider == null && player != null)
                playerCollider = player.GetComponentInChildren<Collider2D>();
            if (ownCollider == null || playerCollider == null) return;

            Physics2D.IgnoreCollision(ownCollider, playerCollider, true);
            phaseUntil = Time.time + config.phaseThroughSeconds;
            phasing = true;
        }

        public void Init(ManiacConfig maniacConfig, ManiacPatrolRoute patrolRoute)
        {
            config = maniacConfig;
            route = patrolRoute;
        }

        void Awake()
        {
            Motor = GetComponent<ManiacMotor>();
            Perception = GetComponent<ManiacPerception>();
            Breadcrumbs = GetComponent<ManiacBreadcrumbs>();
            Nav = GetComponent<ManiacNavigator>();
            if (Nav == null) Nav = gameObject.AddComponent<ManiacNavigator>(); // existing scene maniac
            if (config == null) Debug.LogError("[ManiacController] ManiacConfig not assigned.");
            if (route == null) Debug.LogError("[ManiacController] Patrol route not assigned.");

            Motor.Init(config);
            Perception.Init(config);
            Breadcrumbs.Init(config);

            Patrol = new PatrolState(this);
            Investigate = new InvestigateState(this);
            Search = new SearchState(this);
            Chase = new ChaseState(this);
            Attack = new AttackState(this);
            brain = new ManiacBrain(this);

            stateMachine.StateChanged += OnStateChanged;
        }

        void Start()
        {
            var controller = Object.FindAnyObjectByType<PlayerController>();
            if (controller != null) player = controller.transform;

            EventBus.Subscribe<TimeKiller.Hiding.PlayerHidEvent>(OnPlayerHid);
            EventBus.Subscribe<TimeKiller.Hiding.PlayerUnhidEvent>(OnPlayerUnhid);
            EventBus.Subscribe<ManiacHintEvent>(OnHint);

            stateMachine.ChangeState(Patrol);
            DebugOverlay.Watch("Maniac", () => stateMachine.Current?.GetType().Name ?? "none");
            DebugOverlay.Watch("Maniac Sees", () => Perception.CanSeePlayer ? "PLAYER!" : "-");
            DebugOverlay.Watch("Last Hide", () => LastHideVerdict);
            DebugOverlay.Watch("Awareness", () =>
            {
                int pct = Mathf.RoundToInt(Perception.Awareness * 100f);
                int bars = Mathf.RoundToInt(Perception.Awareness * 10f);
                return $"[{new string('|', bars)}{new string('.', 10 - bars)}] {pct}% {Perception.Level}";
            });
            DebugOverlay.Watch("Brain", () =>
            {
                var s = brain.LastScores;
                return $"P{s[0]:F2} I{s[1]:F2} S{s[2]:F2} C{s[3]:F2}";
            });
        }

        public void ChangeState(IState next) => stateMachine.ChangeState(next);

        static ManiacBehavior BehaviorOf(IState s) =>
            s is ChaseState || s is AttackState ? ManiacBehavior.Chase :
            s is SearchState ? ManiacBehavior.Search :
            s is InvestigateState ? ManiacBehavior.Investigate :
            ManiacBehavior.Patrol;

        IState StateOf(ManiacBehavior b) => b switch
        {
            ManiacBehavior.Chase => Chase,
            ManiacBehavior.Search => Search,
            ManiacBehavior.Investigate => Investigate,
            _ => Patrol,
        };

        void Update()
        {
            // The utility brain chooses the high-level behavior. Attack is a
            // committed swing and the Outlast wardrobe-march (CompromisedSpot) is
            // reactive — the brain owns everything else.
            var current = stateMachine.Current;
            if (brain != null && !(current is AttackState) && !CompromisedSpot.HasValue)
            {
                var want = StateOf(brain.Decide(BehaviorOf(current)));
                if (want != current) ChangeState(want);
            }

            stateMachine.Tick(Time.deltaTime);
            // The sight cone points where he's moving (or keeps its last aim while still).
            if (Motor.CurrentVelocity.sqrMagnitude > 0.04f)
                Perception.FacingDirection = Motor.CurrentVelocity.normalized;

            // Restore body collision only after the window AND once separated —
            // re-enabling while overlapped would pop them apart violently.
            if (phasing && Time.time >= phaseUntil &&
                (playerCollider == null || !ownCollider.bounds.Intersects(playerCollider.bounds)))
            {
                if (playerCollider != null)
                    Physics2D.IgnoreCollision(ownCollider, playerCollider, false);
                phasing = false;
            }
        }

        void FixedUpdate() => stateMachine.FixedTick(Time.fixedDeltaTime);

        void OnStateChanged(IState from, IState to) =>
            EventBus.Publish(new ManiacStateChangedEvent { StateName = to?.GetType().Name ?? "none" });

        void OnDestroy()
        {
            stateMachine.StateChanged -= OnStateChanged;
            EventBus.Unsubscribe<TimeKiller.Hiding.PlayerHidEvent>(OnPlayerHid);
            EventBus.Unsubscribe<TimeKiller.Hiding.PlayerUnhidEvent>(OnPlayerUnhid);
            EventBus.Unsubscribe<ManiacHintEvent>(OnHint);
            DebugOverlay.Unwatch("Maniac");
            DebugOverlay.Unwatch("Maniac Sees");
            DebugOverlay.Unwatch("Last Hide");
            DebugOverlay.Unwatch("Awareness");
            DebugOverlay.Unwatch("Brain");
        }

        /// Why the last hide did or did not compromise the spot. Recorded because
        /// this rule has TWO independent guards and, from outside, every rejection
        /// looks identical: he stands next to the wardrobe doing nothing.
        ///
        /// Measured 2026-08-02 in Recordings/2026-08-02_154554: the player hid in
        /// WardrobeA (1.7, 9.6) while he was 1.4u away and DETECTED, and he then
        /// stood 0.44u from the door for 11.6s cycling suspicious/searching. One
        /// of these two guards rejected it and there was no way to tell which.
        public string LastHideVerdict { get; private set; } = "none yet";

        // ---- The Director's hint. Optional: with no ManiacDirector in the scene
        // this stays null forever and PatrolState behaves exactly as before.
        Vector2? hintArea;
        float hintRadius;
        float hintUntil;

        /// Where the Director suggested he look, or null. A SUGGESTION — it is
        /// deliberately smeared before it reaches him and it never touches his
        /// awareness, so arriving here is not the same as finding anyone.
        public Vector2? Hint => hintArea.HasValue && Time.time < hintUntil ? hintArea : null;
        public float HintRadius => hintRadius;

        void OnHint(ManiacHintEvent evt)
        {
            hintArea = evt.Area;
            hintRadius = evt.Radius;
            // Expires on its own. A hint that never went stale would pin him to a
            // spot the player left minutes ago.
            hintUntil = Time.time + (config != null ? config.hintLifetimeSeconds : 45f);
        }

        /// Consumed once he has swept it, so he does not orbit a stale suggestion.
        public void ClearHint() { hintArea = null; }

        /// The state he is in right now, by name. Public so the recorder can log
        /// it — a position track alone cannot tell "searching" from "patrolling",
        /// and those look identical in the data while meaning opposite things.
        public string CurrentStateName => stateMachine?.Current?.GetType().Name ?? "none";

        void OnPlayerHid(TimeKiller.Hiding.PlayerHidEvent evt)
        {
            // Saw them within the window? Then hiding fools nobody — but only if
            // the player could have KNOWN he was watching. See IsOnScreen.
            float sinceSeen = Perception.TimeSinceSeen;
            float distance = Vector2.Distance(Motor.Position, evt.SpotPosition);

            if (sinceSeen > config.seenEnterWindow)
            {
                LastHideVerdict = $"SAFE: not seen recently ({sinceSeen:0.00}s > {config.seenEnterWindow:0.00}s window), {distance:0.0}u away";
                return;
            }
            if (config.compromiseOnlyWhenOnScreen && !IsOnScreen())
            {
                LastHideVerdict = $"SAFE: he was OFF-SCREEN (seen {sinceSeen:0.00}s ago, {distance:0.0}u away) — the blind band";
                return;
            }

            LastHideVerdict = $"CAUGHT: seen {sinceSeen:0.00}s ago, on screen, {distance:0.0}u away";
            CompromisedSpot = evt.SpotPosition;
            ChangeState(Chase);
        }

        /// Was he actually visible to the player at this moment?
        ///
        /// The camera is wide and short — measured 7.5u horizontally but only
        /// 3.4u vertically — while his sightRange is 7u. That leaves a 3.6u band
        /// directly above and below the player where he has clear line of sight
        /// and is completely off screen. Punishing a hide decided in that band
        /// means punishing the player for information the game refused to show
        /// them, which reads as the hiding system being broken.
        ///
        /// Deliberately narrow in scope: this gates the HIDING penalty only. His
        /// detection, chasing and searching all still use the full sight range.
        bool IsOnScreen()
        {
            var cam = Camera.main;
            if (cam == null) return true;   // can't tell — keep the original rule
            var vp = cam.WorldToViewportPoint(Motor.Position);
            return vp.z > 0f && vp.x >= 0f && vp.x <= 1f && vp.y >= 0f && vp.y <= 1f;
        }

        void OnPlayerUnhid(TimeKiller.Hiding.PlayerUnhidEvent evt) => CompromisedSpot = null;

        /// He has FOUND the player by opening a wardrobe mid-hunt (SearchState +
        /// ManiacWardrobeSearch), rather than by watching them climb in.
        ///
        /// Deliberately routed through the same compromised-spot path as
        /// OnPlayerHid: there is one way to be caught in a wardrobe, not two that
        /// can drift apart in tuning. ChaseState already knows how to march on a
        /// known spot and swing regardless of sight.
        public void CompromiseSpot(Vector2 spotPosition)
        {
            CompromisedSpot = spotPosition;
            ChangeState(Chase);
        }
    }
}
