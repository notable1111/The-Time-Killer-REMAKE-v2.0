// Listens to the player's state/facing events on the EventBus and tells the
// SpriteAnimator what to play. Movement code knows nothing about visuals —
// this driver is the only bridge, and it can be removed or replaced freely.
// Playback speed is matched to actual velocity every frame so feet never
// slide during acceleration/deceleration (run clip fps is tuned for runSpeed).
using TimeKiller.Core;
using UnityEngine;

namespace TimeKiller.Player
{
    [RequireComponent(typeof(SpriteAnimator))]
    [RequireComponent(typeof(PlayerController))]
    public class PlayerAnimationDriver : MonoBehaviour
    {
        [SerializeField] PlayerAnimationSet animations;

        SpriteAnimator animator;
        PlayerController player;
        string currentState = nameof(IdleState);
        FacingDirection facing = FacingDirection.Down;

        // Explicit list — unknown states (Hiding, future Stunned…) must count as
        // NOT moving, or their run-clip frame events would publish phantom
        // footstep noise for the maniac to hear.
        public bool IsMovingState => currentState == nameof(WalkState) || currentState == nameof(RunState);
        public bool IsRunningState => currentState == nameof(RunState);

        void Awake()
        {
            animator = GetComponent<SpriteAnimator>();
            player = GetComponent<PlayerController>();
        }

        void OnEnable()
        {
            EventBus.Subscribe<PlayerStateChangedEvent>(OnStateChanged);
            EventBus.Subscribe<PlayerFacingChangedEvent>(OnFacingChanged);
        }

        void OnDisable()
        {
            EventBus.Unsubscribe<PlayerStateChangedEvent>(OnStateChanged);
            EventBus.Unsubscribe<PlayerFacingChangedEvent>(OnFacingChanged);
        }

        void Start() => Apply(preservePhase: false);

        void Update()
        {
            // Velocity-matched playback: leg cycle speed follows real ground speed.
            if (!IsMovingState || animations == null || player.Config == null) return;
            float normalized = player.Motor.CurrentVelocity.magnitude / player.Config.runSpeed;
            animator.SetSpeed(Mathf.Max(normalized, animations.minAnimationSpeed));
        }

        void OnStateChanged(PlayerStateChangedEvent evt)
        {
            // Walk and Run share clips; switching between them is a speed change
            // only, so keep the phase to avoid a visible leg-cycle restart.
            bool bothMoving = IsMovingState && evt.StateName != nameof(IdleState);
            currentState = evt.StateName;
            Apply(preservePhase: bothMoving);
        }

        void OnFacingChanged(PlayerFacingChangedEvent evt)
        {
            facing = evt.Direction;
            Apply(preservePhase: true); // same action, new direction: don't restart the cycle
        }

        void Apply(bool preservePhase)
        {
            if (animations == null) return;
            animator.Play(IsMovingState ? animations.GetRun(facing) : animations.GetIdle(facing), 1f, preservePhase);
        }
    }
}
