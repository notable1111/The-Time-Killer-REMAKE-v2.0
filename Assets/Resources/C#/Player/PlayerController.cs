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

        /// Temporary speed factor (adrenaline after a hit). 1 = normal.
        public float SpeedMultiplier => Time.time < boostUntil ? boostFactor : 1f;

        readonly StateMachine stateMachine = new StateMachine();
        float boostFactor = 1f;
        float boostUntil;

        /// Called by PlayerHealth on hit: outrun-the-killer escape window.
        public void SetSpeedBoost(float multiplier, float seconds)
        {
            boostFactor = multiplier;
            boostUntil = Time.time + seconds;
        }

        /// Swap who drives this character (scripted playtests, future co-op/AI).
        /// Pass null to re-fetch whatever IInputSource sits on the GameObject.
        public void SetInputSource(IInputSource source) =>
            Input = source ?? GetComponent<IInputSource>();

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

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            // Puts "Player" in the F3 walkability cycle, so the overlay can be
            // asked about THIS body instead of only the maniac's much wider one.
            // Attached here rather than in PlayerSetup so existing scenes and
            // prefabs need no edit and cannot drift out of sync. It builds
            // nothing until the first time you actually select it.
            //
            // DontSave, and not optional: this is the only structural change any
            // runtime script makes to a scene object, and an un-flagged
            // AddComponent asks the editor to dirty the scene — which throws
            // "cannot be used during play mode" and, worse, leaves the hall
            // showing unsaved changes it never actually received. The hall is
            // hand-fixed and protected; a debug component must not be able to
            // creep into it.
            if (GetComponent<PlayerNavDebug>() == null)
                gameObject.AddComponent<PlayerNavDebug>().hideFlags = HideFlags.DontSave;
#endif

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

        /// Who is driving right now. Features that push the player into a state
        /// of their own (hiding, clock repair) read this so they only ever hand
        /// control back if they still hold it — two features must never fight
        /// over returning the player to Idle.
        public IState CurrentState => stateMachine.Current;

        /// True when the player is in one of the three normal movement states,
        /// i.e. free for a feature to take over.
        public bool IsFreeToInterrupt =>
            stateMachine.Current == Idle || stateMachine.Current == Walk || stateMachine.Current == Run;

        void Update()
        {
            stateMachine.Tick(Time.deltaTime);

            // Only the movement states steer the body. A feature state (repair,
            // hiding) has aimed the character deliberately, and letting WASD keep
            // writing facing underneath it would let you spin a locked player's
            // pose on the spot. No behaviour change for Idle/Walk/Run: idle input
            // is near-zero, which UpdateFromInput already ignores.
            if (IsFreeToInterrupt) Facing.UpdateFromInput(Input.MoveInput);
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
