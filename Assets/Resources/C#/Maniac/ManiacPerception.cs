// The maniac's senses. HEARING: subscribes to PlayerFootstepEvent — a step is
// heard when distance < loudness * hearingRadius (running is loud, walking is
// quiet: the player's core stealth choice). SIGHT: range + facing cone + a
// linecast that walls block. States read the flags; this component never
// drives movement itself.
using TimeKiller.Core;
using TimeKiller.Hiding;
using TimeKiller.Player;
using UnityEngine;

namespace TimeKiller.Maniac
{
    public class ManiacPerception : MonoBehaviour
    {
        ManiacConfig config;
        Transform player;

        /// True while the player is inside a hiding spot — sight can't find them.
        public bool PlayerHidden { get; private set; }

        public bool CanSeePlayer { get; private set; }
        public Vector2 LastSeenPosition { get; private set; }
        public Vector2 LastSeenDirection { get; private set; } = Vector2.zero; // which way the player was moving when last visible (flee bias for the search belief map)
        public float LastSeenTime { get; private set; } = float.NegativeInfinity;
        public bool HasUnhandledNoise { get; private set; }
        public Vector2 LastNoisePosition { get; private set; }
        public float LastNoiseTime { get; private set; } = float.NegativeInfinity; // for the brain's noise-recency score
        public Vector2 FacingDirection { get; set; } = Vector2.down; // set by controller from velocity

        public void Init(ManiacConfig maniacConfig)
        {
            config = maniacConfig;
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

            Notice(step.Position);
        }

        // World noises (the escape gate crashing open, future props). AlwaysHeard
        // ones cut through the radius — he KNOWS the way out just opened.
        void OnWorldNoise(WorldNoiseEvent noise)
        {
            if (config == null) return;
            if (!noise.AlwaysHeard)
            {
                float heardRadius = config.hearingRadius * Mathf.Clamp01(noise.Loudness);
                if (Vector2.Distance(transform.position, noise.Position) > heardRadius) return;
            }
            Notice(noise.Position);
        }

        void Notice(Vector2 position)
        {
            LastNoisePosition = position;
            LastNoiseTime = Time.time;
            HasUnhandledNoise = true;
            EventBus.Publish(new ManiacHeardNoiseEvent { NoisePosition = position });
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
                Vector2 now = player.position;
                Vector2 delta = now - LastSeenPosition;
                if (delta.sqrMagnitude > 0.0004f) // moving — remember the flee direction
                    LastSeenDirection = delta.normalized;
                LastSeenPosition = now;
                LastSeenTime = Time.time;
            }
            CanSeePlayer = seenNow;
        }

        bool CheckSight()
        {
            if (PlayerHidden) return false; // inside a wardrobe — eyes can't find you
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
