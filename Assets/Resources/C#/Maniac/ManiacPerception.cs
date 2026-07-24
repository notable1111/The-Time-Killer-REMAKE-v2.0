// The maniac's senses.
//
// SIGHT is a GRADUAL AWARENESS model (2026-07-24), not a binary see/unsee:
// each frame a detection RATE is computed from how exposed the player is —
// distance, how centered they are in his vision (central cone strong, peripheral
// weak and close-only), how LIT they are (torchlight exposes, shadow hides), and
// whether they're MOVING (running spots fast, standing still is a real hiding
// tool). That rate fills an Awareness meter (0..1); it drains when he loses you.
//   Awareness >= suspicionThreshold -> he INVESTIGATES your rough position (turns,
//     comes to check — the "did he see me?" beat).
//   Awareness >= 1                    -> fully SPOTTED: CanSeePlayer -> Chase.
// Walls block sight (linecast); a wardrobe hides you outright.
//
// HEARING is unchanged: footstep/world noises within loudness*hearingRadius set
// the noise fields that drive Investigate. States read the flags; this component
// never drives movement itself.
using TimeKiller.Core;
using TimeKiller.Hiding;
using TimeKiller.Player;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace TimeKiller.Maniac
{
    public class ManiacPerception : MonoBehaviour
    {
        public enum AwarenessLevel { Unaware, Suspicious, Detected }

        ManiacConfig config;
        Transform player;
        Light2D[] lights;          // cached torches/glows for exposure sampling
        Vector2 prevPlayerPos;
        float playerSpeed;
        bool havePrev;

        /// True while the player is inside a hiding spot — sight can't find them.
        public bool PlayerHidden { get; private set; }

        /// 0..1 stealth meter. Fills while exposed, drains when he loses you.
        public float Awareness { get; private set; }
        public AwarenessLevel Level { get; private set; } = AwarenessLevel.Unaware;

        public bool CanSeePlayer { get; private set; }   // == fully Detected (Awareness hit 1)
        public Vector2 LastSeenPosition { get; private set; }
        public Vector2 LastSeenDirection { get; private set; } = Vector2.zero; // flee bias for the search belief map
        public float LastSeenTime { get; private set; } = float.NegativeInfinity;
        public bool HasUnhandledNoise { get; private set; }
        public Vector2 LastNoisePosition { get; private set; }
        public float LastNoiseTime { get; private set; } = float.NegativeInfinity;
        public Vector2 FacingDirection { get; set; } = Vector2.down; // set by controller from velocity

        public void Init(ManiacConfig maniacConfig)
        {
            config = maniacConfig;
            lights = Object.FindObjectsByType<Light2D>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            EventBus.Subscribe<PlayerFootstepEvent>(OnFootstep);
            EventBus.Subscribe<PlayerHidEvent>(OnPlayerHid);
            EventBus.Subscribe<PlayerUnhidEvent>(OnPlayerUnhid);
            EventBus.Subscribe<WorldNoiseEvent>(OnWorldNoise);
        }

        void OnDestroy()
        {
            EventBus.Unsubscribe<PlayerFootstepEvent>(OnFootstep);
            EventBus.Unsubscribe<PlayerHidEvent>(OnPlayerHid);
            EventBus.Unsubscribe<PlayerUnhidEvent>(OnPlayerUnhid);
            EventBus.Unsubscribe<WorldNoiseEvent>(OnWorldNoise);
        }

        void OnPlayerHid(PlayerHidEvent evt) => PlayerHidden = true;
        void OnPlayerUnhid(PlayerUnhidEvent evt) => PlayerHidden = false;

        /// Called by states when they start responding to the current noise.
        public void ConsumeNoise() => HasUnhandledNoise = false;

        void OnFootstep(PlayerFootstepEvent step)
        {
            if (config == null) return;
            float heardRadius = config.hearingRadius * Mathf.Clamp01(step.Loudness);
            if (Vector2.Distance(transform.position, step.Position) > heardRadius) return;
            HearNoise(step.Position);
        }

        void OnWorldNoise(WorldNoiseEvent noise)
        {
            if (config == null) return;
            if (!noise.AlwaysHeard)
            {
                float heardRadius = config.hearingRadius * Mathf.Clamp01(noise.Loudness);
                if (Vector2.Distance(transform.position, noise.Position) > heardRadius) return;
            }
            HearNoise(noise.Position);
        }

        // A discrete noise (footstep, gate) — sets the noise fields AND announces it.
        void HearNoise(Vector2 position)
        {
            SetNoise(position);
            EventBus.Publish(new ManiacHeardNoiseEvent { NoisePosition = position });
        }

        void SetNoise(Vector2 position)
        {
            LastNoisePosition = position;
            LastNoiseTime = Time.time;
            HasUnhandledNoise = true;
        }

        void Update()
        {
            if (config == null) return;
            if (player == null)
            {
                var controller = Object.FindAnyObjectByType<PlayerController>();
                if (controller == null) return;
                player = controller.transform;
                prevPlayerPos = player.position;
            }

            TrackPlayerSpeed();

            float dt = Time.deltaTime;
            float rate = DetectionRate();
            Awareness = Mathf.Clamp01(Awareness + (rate > 0f
                ? rate * config.awarenessFillRate * dt
                : -config.awarenessDrainRate * dt));

            var newLevel = Awareness >= 1f ? AwarenessLevel.Detected
                         : Awareness >= config.suspicionThreshold ? AwarenessLevel.Suspicious
                         : AwarenessLevel.Unaware;

            if (newLevel == AwarenessLevel.Detected)
            {
                if (Level != AwarenessLevel.Detected)
                    EventBus.Publish(new ManiacSpottedPlayerEvent { PlayerPosition = player.position });
                Vector2 now = player.position;
                Vector2 delta = now - LastSeenPosition;
                if (delta.sqrMagnitude > 0.0004f) LastSeenDirection = delta.normalized;
                LastSeenPosition = now;
                LastSeenTime = Time.time;
                CanSeePlayer = true;
            }
            else
            {
                CanSeePlayer = false;
                // Suspicious + still actively sensing something -> he comes to CHECK
                // your rough spot (drives Investigate; his movement turns him to look).
                if (newLevel == AwarenessLevel.Suspicious && rate > 0f)
                {
                    SetNoise(player.position);
                    if (Level == AwarenessLevel.Unaware) // first flicker of suspicion — a dark riser
                        EventBus.Publish(new ManiacHeardNoiseEvent { NoisePosition = player.position });
                }
            }
            Level = newLevel;
        }

        void TrackPlayerSpeed()
        {
            Vector2 now = player.position;
            float dt = Time.deltaTime;
            if (havePrev && dt > 0f)
            {
                float raw = (now - prevPlayerPos).magnitude / dt;
                if (raw < 20f) playerSpeed = raw; // ignore teleport spikes (tests, respawns)
            }
            prevPlayerPos = now;
            havePrev = true;
        }

        // 0 (undetectable this frame) .. 1 (ideal exposure). Drives the meter.
        float DetectionRate()
        {
            if (PlayerHidden) return 0f;
            Vector2 toPlayer = (Vector2)player.position - (Vector2)transform.position;
            float dist = toPlayer.magnitude;
            if (dist > config.sightRange) return 0f;
            if (!HasLineOfSight()) return 0f;               // a wall/prop is in the way
            if (dist <= config.proximityRange) return 1f;   // point-blank — he feels you

            float angle = Vector2.Angle(FacingDirection, toPlayer);
            float coneWeight;
            if (angle <= config.centralConeAngle * 0.5f)
                coneWeight = 1f;                             // central vision
            else if (angle <= config.peripheralConeAngle * 0.5f && dist <= config.peripheralRange)
                coneWeight = config.peripheralWeight;        // corner of the eye
            else
                return 0f;                                   // behind him

            float distFactor = 1f - dist / config.sightRange;                 // closer = stronger
            float lightFactor = Mathf.Lerp(config.exposureFloor, 1f, Exposure(player.position)); // shadow hides
            float moveFactor = playerSpeed < 0.1f ? config.stillDetectionMultiplier
                             : playerSpeed >= config.runSpeedThreshold ? config.runningDetectionMultiplier
                             : 1f;
            return coneWeight * distFactor * lightFactor * moveFactor;
        }

        bool HasLineOfSight()
        {
            foreach (var hit in Physics2D.LinecastAll(transform.position, player.position, config.sightBlockers))
            {
                if (hit.collider == null || hit.collider.isTrigger) continue;
                var root = hit.collider.transform.root;
                if (root == transform.root || root == player.root) continue;
                return false;
            }
            return true;
        }

        // How lit the point is: ambient + nearby enabled point lights (torches,
        // clock glows, the open gate). 0 = deep shadow, 1 = full torchlight.
        float Exposure(Vector2 pos)
        {
            float e = config.ambientExposure;
            if (lights != null)
            {
                foreach (var l in lights)
                {
                    if (l == null || !l.enabled || !l.isActiveAndEnabled) continue;
                    if (l.lightType != Light2D.LightType.Point) continue;
                    float outer = l.pointLightOuterRadius;
                    if (outer <= 0f) continue;
                    float d = Vector2.Distance(pos, l.transform.position);
                    if (d >= outer) continue;
                    e += l.intensity * (1f - d / outer);
                }
            }
            return Mathf.Clamp01(e);
        }

        /// Seconds since he last had eyes on the player.
        public float TimeSinceSeen => Time.time - LastSeenTime;
    }
}
