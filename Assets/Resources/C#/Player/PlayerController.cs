// Composition root of the player: connects input source, movement config,
// motor, facing and the state machine, and publishes state changes on the
// EventBus. Add this to a GameObject via TimeKiller/Setup/4 - Create Player.
using TimeKiller.Core;
using UnityEngine;

namespace TimeKiller.Player
{
    [RequireComponent(typeof(PlayerMotor))]
    [RequireComponent(typeof(PlayerFacing))]
    public class PlayerController : MonoBehaviour
    {
        [SerializeField] PlayerMovementConfig config;

        public PlayerMovementConfig Config => config;
        public IInputSource Input { get; private set; }
        public PlayerMotor Motor { get; private set; }
        public PlayerFacing Facing { get; private set; }

        public IdleState Idle { get; private set; }
        public WalkState Walk { get; private set; }
        public RunState Run { get; private set; }

        readonly StateMachine stateMachine = new StateMachine();

        void Awake()
        {
            Motor = GetComponent<PlayerMotor>();
            Facing = GetComponent<PlayerFacing>();
            Input = GetComponent<IInputSource>();
            if (Input == null)
                Debug.LogError("[PlayerController] No IInputSource component on the Player object.");
            if (config == null)
                Debug.LogError("[PlayerController] PlayerMovementConfig not assigned.");

            Motor.Init(config);

            Idle = new IdleState(this);
            Walk = new WalkState(this);
            Run = new RunState(this);

            stateMachine.StateChanged += OnStateChanged;
        }

        void Start()
        {
            stateMachine.ChangeState(Idle);
            DebugOverlay.Watch("Player State", () => stateMachine.Current?.GetType().Name ?? "none");
            DebugOverlay.Watch("Player Speed", () => Motor.CurrentVelocity.magnitude.ToString("0.00"));
            DebugOverlay.Watch("Facing", () => Facing.Current.ToString());
        }

        public void ChangeState(IState next) => stateMachine.ChangeState(next);

        void Update()
        {
            stateMachine.Tick(Time.deltaTime);
            Facing.UpdateFromInput(Input.MoveInput);
        }

        void FixedUpdate() => stateMachine.FixedTick(Time.fixedDeltaTime);

        void OnStateChanged(IState from, IState to) =>
            EventBus.Publish(new PlayerStateChangedEvent { StateName = to?.GetType().Name ?? "none" });

        void OnDestroy()
        {
            stateMachine.StateChanged -= OnStateChanged;
            DebugOverlay.Unwatch("Player State");
            DebugOverlay.Unwatch("Player Speed");
            DebugOverlay.Unwatch("Facing");
        }
    }
}
