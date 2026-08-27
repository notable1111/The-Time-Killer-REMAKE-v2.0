// The look of him. Bridges the maniac's motion, state and awareness to his
// sprite — visuals only, removable.
//
// Direction comes from actual velocity (8-way snap with hysteresis so diagonal
// movement doesn't flicker); playback speed follows real ground speed the same
// way the player's does.
//
// WHAT THIS PASS FIXED (2026-08-04). He had 2,492 lines of AI — Patrol,
// Investigate, Search, Chase, Attack, a belief map, breadcrumbs, wardrobe
// search — and this component rendered all of it through one boolean:
//
//     animator.Play(moving ? GetWalk(facing) : GetIdle(facing))
//
// So being hunted looked exactly like being ignored, and AttackState had no
// visual whatsoever. Everything the brain knew was thrown away at the last
// step. Three things changed, none of them new behaviour:
//
//   1. AttackState now plays the lunge that shipped in the sprite pack and had
//      never been referenced (stage_three row 8).
//   2. Tone and cadence follow ManiacPerception.Level. This deliberately
//      mirrors ManiacVoice, which was ALREADY moving his breath across the same
//      three levels — the audio was telling the player something the picture
//      was flatly contradicting.
//   3. He is tinted down into the castle's tonal range. His sheet averages
//      value 153/255 against the survivor's 48; that gap is most of why he read
//      as pasted on rather than standing there.
//
// FAIRNESS, same rule BrightnessSettings documents: this component only ever
// writes SpriteRenderer.color and animation speed. ManiacPerception decides
// visibility from Light2D intensities in the world, so nothing here can change
// what he knows or what he can see. It is a look, not an advantage.
//
// Removable: delete the component and he falls back to the raw sheet, untinted,
// exactly as authored.
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
        SpriteRenderer spriteRenderer;
        ManiacMotor motor;
        ManiacPerception perception;
        FacingDirection facing = FacingDirection.Down;
        bool moving;

        float attackUntil;                   // while > Time.time the lunge owns the sprite
        bool attacking;
        Color tone = Color.white;            // smoothed, before it reaches the renderer
        float cadence = 1f;                  // smoothed awareness multiplier

        /// The multiplier currently applied to his walk cycle, and the colour he
        /// is actually being drawn with. Public because a look nobody can measure
        /// is a look nobody can tune — the overlay reads both.
        public float Cadence => cadence;
        public Color Tone => tone;
        public bool Attacking => attacking;

        void Awake()
        {
            animator = GetComponent<SpriteAnimator>();
            motor = GetComponent<ManiacMotor>();
            perception = GetComponent<ManiacPerception>();
            spriteRenderer = GetComponent<SpriteRenderer>();
            tone = TargetTone();
            cadence = TargetCadence();
        }

        void Start()
        {
            EventBus.Subscribe<ManiacStateChangedEvent>(OnStateChanged);
            Apply(preservePhase: false);

            DebugOverlay.Watch("ManiacLook", () => animations == null
                ? "OFF"
                : $"{(attacking ? "ATTACK" : moving ? "walk" : "stand")} {facing} " +
                  $"cadence {cadence:0.00} tone {tone.r:0.00}/{tone.g:0.00}/{tone.b:0.00}");
        }

        void OnDestroy()
        {
            EventBus.Unsubscribe<ManiacStateChangedEvent>(OnStateChanged);
            DebugOverlay.Unwatch("ManiacLook");
            // Leave him exactly as authored if this component goes away.
            if (spriteRenderer != null) spriteRenderer.color = Color.white;
        }

        void Update()
        {
            if (animations == null) return;
            float dt = Time.deltaTime;

            DriveTone(dt);

            // The lunge owns the sprite for its hold, so a chase frame cannot
            // stomp the one pose in the whole set that says "he has you".
            if (attacking)
            {
                if (Time.time < attackUntil) return;
                attacking = false;
                Apply(preservePhase: false);   // force back onto walk/idle
            }

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

            // Every frame, never through Play(): SpriteAnimator.Play restarts the
            // phase when the speed argument differs, so driving cadence through it
            // would reset the walk cycle on every frame it changed.
            animator.SetSpeed(moving
                ? Mathf.Max(velocity.magnitude / animations.speedForNormalPlayback, animations.minAnimationSpeed) * cadence
                : cadence);
        }

        /// Tint and cadence follow his awareness, blended rather than snapped for
        /// the same reason ManiacVoice blends his breath: a hard switch reads as a
        /// bug, a ramp reads as him getting interested.
        void DriveTone(float dt)
        {
            var targetTone = TargetTone();
            float targetCadence = TargetCadence();

            float t = 1f - Mathf.Exp(-dt / Mathf.Max(0.05f, animations.toneBlendSeconds));
            tone = Color.Lerp(tone, targetTone, t);
            cadence = Mathf.Lerp(cadence, targetCadence, t);

            if (spriteRenderer != null) spriteRenderer.color = tone;
        }

        Color TargetTone()
        {
            if (animations == null) return Color.white;
            var result = animations.bodyTint;
            if (perception != null)
            {
                switch (perception.Level)
                {
                    case ManiacPerception.AwarenessLevel.Detected:
                        result *= animations.detectedTint;
                        break;
                    case ManiacPerception.AwarenessLevel.Suspicious:
                        result *= animations.suspiciousTint;
                        break;
                }
            }
            result.a = animations.bodyTint.a;   // multiplying alpha would fade him out
            return result;
        }

        float TargetCadence()
        {
            if (animations == null) return 1f;
            if (perception == null) return animations.cadenceUnaware;
            switch (perception.Level)
            {
                case ManiacPerception.AwarenessLevel.Detected: return animations.cadenceDetected;
                case ManiacPerception.AwarenessLevel.Suspicious: return animations.cadenceSuspicious;
                default: return animations.cadenceUnaware;
            }
        }

        void OnStateChanged(ManiacStateChangedEvent evt)
        {
            if (animations == null || animations.attack == null) return;
            if (evt.StateName != nameof(AttackState)) return;

            attacking = true;
            attackUntil = Time.time + animations.attackHoldSeconds;
            animator.Play(animations.attack, 1f, preservePhase: false);
        }

        void Apply(bool preservePhase)
        {
            if (animations == null) return;
            animator.Play(moving ? animations.GetWalk(facing) : animations.GetIdle(facing), 1f, preservePhase);
        }
    }
}
