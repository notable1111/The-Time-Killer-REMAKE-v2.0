// Bridges the maniac's motion to his sprite clips — visuals only, removable.
// Direction comes from actual velocity (8-way snap with hysteresis so diagonal
// movement doesn't flicker); playback speed follows real ground speed the same
// way the player's does.
using TimeKiller.Core;
using TimeKiller.Player; // FacingDirection
using UnityEngine;

namespace TimeKiller.Maniac
{
    [RequireComponent(typeof(SpriteAnimator))]
    [RequireComponent(typeof(ManiacMotor))]
    public class ManiacAnimationDriver : MonoBehaviour
    {
        [SerializeField] ManiacAnimationSet animations;

        const float MoveThreshold = 0.15f;   // below this he's "standing"
        const float TurnHysteresis = 12f;    // degrees a new direction must win by

        SpriteAnimator animator;
        ManiacMotor motor;
        FacingDirection facing = FacingDirection.Down;
        bool moving;

        void Awake()
        {
            animator = GetComponent<SpriteAnimator>();
            motor = GetComponent<ManiacMotor>();
        }

        void Start() => Apply(preservePhase: false);

        void Update()
        {
            if (animations == null) return;

            var velocity = motor.CurrentVelocity;
            bool nowMoving = velocity.magnitude > MoveThreshold;

            var newFacing = facing;
            if (nowMoving)
            {
                float angle = Vector2.SignedAngle(Vector2.down, velocity); // CCW from Down = enum order
                var candidate = (FacingDirection)(((int)Mathf.Round(angle / 45f) % 8 + 8) % 8);
                // Hysteresis: only turn when clearly past the sector boundary.
                if (candidate != facing)
                {
                    float currentAngle = (int)facing * 45f;
                    if (Mathf.Abs(Mathf.DeltaAngle(angle, currentAngle)) > 22.5f + TurnHysteresis)
                        newFacing = candidate;
                }
            }

            if (nowMoving != moving || newFacing != facing)
            {
                bool samePose = nowMoving == moving;
                moving = nowMoving;
                facing = newFacing;
                Apply(preservePhase: samePose);
            }

            if (moving)
            {
                float normalized = velocity.magnitude / animations.speedForNormalPlayback;
                animator.SetSpeed(Mathf.Max(normalized, animations.minAnimationSpeed));
            }
        }

        void Apply(bool preservePhase)
        {
            if (animations == null) return;
            animator.Play(moving ? animations.GetWalk(facing) : animations.GetIdle(facing), 1f, preservePhase);
        }
    }
}
