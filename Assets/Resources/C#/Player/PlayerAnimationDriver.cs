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

        // Matched by name, not nameof: the repair state belongs to the Objectives
        // feature, and the player must not take a hard reference to something
        // designed to be deleted with its clocks.
        const string RepairStateName = "RepairState";

        // Explicit list — unknown states (Hiding, Repair, future Stunned…) must
        // count as NOT moving, or their run-clip frame events would publish
        // phantom footstep noise for the maniac to hear.
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
            // Each clip's fps is authored for the speed it depicts, so normalise
            // against THAT speed — dividing a walk cycle by runSpeed would play it
            // at half rate and slide the feet.
            if (!IsMovingState || animations == null || player.Config == null) return;
            float reference = UsingWalkClip ? player.Config.walkSpeed : player.Config.runSpeed;
            float normalized = player.Motor.CurrentVelocity.magnitude / reference;
            animator.SetSpeed(Mathf.Max(normalized, animations.minAnimationSpeed));
        }

        // Walking only uses a distinct clip when the pack actually ships one;
        // otherwise the run clip is reused and must stay normalised to runSpeed.
        bool UsingWalkClip => currentState == nameof(WalkState) && animations.HasWalkClips;

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
            animator.Play(ClipForCurrentState(), 1f, preservePhase);
        }

        SpriteAnimationClip ClipForCurrentState()
        {
            if (currentState == nameof(RunState)) return animations.GetRun(facing);
            if (currentState == nameof(WalkState)) return animations.GetWalk(facing);
            if (currentState == RepairStateName) return animations.GetRepair(facing);
            return animations.GetIdle(facing);
        }
    }
}
