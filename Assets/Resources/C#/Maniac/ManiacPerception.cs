// The maniac's senses. HEARING: subscribes to PlayerFootstepEvent — a step is
// heard when distance < loudness * hearingRadius (running is loud, walking is
// quiet: the player's core stealth choice). SIGHT: range + facing cone + a
// linecast that walls block. States read the flags; this component never
// drives movement itself.
using TimeKiller.Core;
using TimeKiller.Player;
using UnityEngine;

namespace TimeKiller.Maniac
{
    public class ManiacPerception : MonoBehaviour
    {
        ManiacConfig config;
        Transform player;

        public bool CanSeePlayer { get; private set; }
        public Vector2 LastSeenPosition { get; private set; }
        public float LastSeenTime { get; private set; } = float.NegativeInfinity;
        public bool HasUnhandledNoise { get; private set; }
        public Vector2 LastNoisePosition { get; private set; }
        public Vector2 FacingDirection { get; set; } = Vector2.down; // set by controller from velocity

        public void Init(ManiacConfig maniacConfig)
        {
            config = maniacConfig;
            EventBus.Subscribe<PlayerFootstepEvent>(OnFootstep);
        }

        void OnDestroy() => EventBus.Unsubscribe<PlayerFootstepEvent>(OnFootstep);

        /// Called by states when they start responding to the current noise.
        public void ConsumeNoise() => HasUnhandledNoise = false;

        void OnFootstep(PlayerFootstepEvent step)
        {
            if (config == null) return;
            float heardRadius = config.hearingRadius * Mathf.Clamp01(step.Loudness);
            if (Vector2.Distance(transform.position, step.Position) > heardRadius) return;

            LastNoisePosition = step.Position;
            HasUnhandledNoise = true;
            EventBus.Publish(new ManiacHeardNoiseEvent { NoisePosition = step.Position });
        }

        void Update()
        {
            if (config == null) return;
            if (player == null)
            {
                var controller = Object.FindAnyObjectByType<PlayerController>();
                if (controller == null) return;
                player = controller.transform;
            }

            bool seenNow = CheckSight();
            if (seenNow)
            {
                if (!CanSeePlayer)
                    EventBus.Publish(new ManiacSpottedPlayerEvent { PlayerPosition = player.position });
                LastSeenPosition = player.position;
                LastSeenTime = Time.time;
            }
            CanSeePlayer = seenNow;
        }

        bool CheckSight()
        {
            Vector2 toPlayer = player.position - transform.position;
            if (toPlayer.magnitude > config.sightRange) return false;
            if (Vector2.Angle(FacingDirection, toPlayer) > config.sightConeAngle * 0.5f) return false;

            // Walls block sight. LinecastAll because the ray starts inside the
            // maniac's own collider — self and the player must not count as
            // blockers; triggers (camera zones) never do.
            foreach (var hit in Physics2D.LinecastAll(transform.position, player.position, config.sightBlockers))
            {
                if (hit.collider == null || hit.collider.isTrigger) continue;
                var root = hit.collider.transform.root;
                if (root == transform.root || root == player.root) continue;
                return false; // a wall or prop is in the way
            }
            return true;
        }

        /// Seconds since he last had eyes on the player.
        public float TimeSinceSeen => Time.time - LastSeenTime;
    }
}
