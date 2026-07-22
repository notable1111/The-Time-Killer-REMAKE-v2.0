// Composition root of the maniac: wires config, motor, perception, breadcrumbs
// and patrol route into the state machine (Patrol/Investigate/Chase/Attack),
// publishes state changes on the EventBus, feeds the perception cone with his
// movement direction. Built in the scene by TimeKiller/Setup/20.
using TimeKiller.Core;
using TimeKiller.Player;
using UnityEngine;

namespace TimeKiller.Maniac
{
    [RequireComponent(typeof(ManiacMotor))]
    [RequireComponent(typeof(ManiacPerception))]
    [RequireComponent(typeof(ManiacBreadcrumbs))]
    public class ManiacController : MonoBehaviour
    {
        [SerializeField] ManiacConfig config;
        [SerializeField] ManiacPatrolRoute route;

        public ManiacConfig Config => config;
        public ManiacPatrolRoute Route => route;
        public ManiacMotor Motor { get; private set; }
        public ManiacPerception Perception { get; private set; }
        public ManiacBreadcrumbs Breadcrumbs { get; private set; }

        public PatrolState Patrol { get; private set; }
        public InvestigateState Investigate { get; private set; }
        public ChaseState Chase { get; private set; }
        public AttackState Attack { get; private set; }

        public Vector2 PlayerPosition => player != null ? (Vector2)player.position : Motor.Position;

        readonly StateMachine stateMachine = new StateMachine();
        Transform player;

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
            if (config == null) Debug.LogError("[ManiacController] ManiacConfig not assigned.");
            if (route == null) Debug.LogError("[ManiacController] Patrol route not assigned.");

            Motor.Init(config);
            Perception.Init(config);
            Breadcrumbs.Init(config);

            Patrol = new PatrolState(this);
            Investigate = new InvestigateState(this);
            Chase = new ChaseState(this);
            Attack = new AttackState(this);

            stateMachine.StateChanged += OnStateChanged;
        }

        void Start()
        {
            var controller = Object.FindAnyObjectByType<PlayerController>();
            if (controller != null) player = controller.transform;

            stateMachine.ChangeState(Patrol);
            DebugOverlay.Watch("Maniac", () => stateMachine.Current?.GetType().Name ?? "none");
            DebugOverlay.Watch("Maniac Sees", () => Perception.CanSeePlayer ? "PLAYER!" : "-");
        }

        public void ChangeState(IState next) => stateMachine.ChangeState(next);

        void Update()
        {
            stateMachine.Tick(Time.deltaTime);
            // The sight cone points where he's moving (or keeps its last aim while still).
            if (Motor.CurrentVelocity.sqrMagnitude > 0.04f)
                Perception.FacingDirection = Motor.CurrentVelocity.normalized;
        }

        void FixedUpdate() => stateMachine.FixedTick(Time.fixedDeltaTime);

        void OnStateChanged(IState from, IState to) =>
            EventBus.Publish(new ManiacStateChangedEvent { StateName = to?.GetType().Name ?? "none" });

        void OnDestroy()
        {
            stateMachine.StateChanged -= OnStateChanged;
            DebugOverlay.Unwatch("Maniac");
            DebugOverlay.Unwatch("Maniac Sees");
        }
    }
}
