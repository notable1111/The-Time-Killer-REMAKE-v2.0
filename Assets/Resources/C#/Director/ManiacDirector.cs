// The macro brain. It knows where you are. He never does.
//
// Alien: Isolation runs two brains: a director that always knows the player's
// position and periodically points the creature at their AREA, and the creature
// itself, which has no idea and must use its own senses. The director never hands
// over the position — it only steers. That is the whole trick, and it is why the
// game reads as tense rather than as cheating.
//
// The problem here was measured, not assumed: a recorded bot session ran **80
// seconds with zero detections**. When the maniac loses you, his belief map
// decays, he returns to patrol, and the encounter is simply over. Tuning his
// senses cannot fix that — nothing existed to bring him back.
//
// THE RULE THIS FILE EXISTS TO KEEP: nothing in here ever writes his awareness,
// his LastSeenPosition, or his belief map. It publishes a smeared point on the
// bus and that is all. He still has to see or hear you. If a future change makes
// hintError small enough that arriving at a hint means finding the player, the
// Director has started cheating no matter what the code claims.
//
// Removable: delete the object and the maniac behaves exactly as before.
using TimeKiller.Core;
using TimeKiller.Maniac;
using TimeKiller.Player;
using UnityEngine;

namespace TimeKiller.Director
{
    public class ManiacDirector : MonoBehaviour
    {
        [SerializeField] DirectorConfig config;

        Transform player;
        ManiacController maniac;

        float lastSensedAt;        // last time he had ANY awareness of the player
        float lastHintAt = float.NegativeInfinity;
        int successfulHides;
        bool hiding;
        bool foundWhileHiding;

        /// Hints issued this run, for the overlay and the bot telemetry. A hint
        /// that fired and a hint that never fired look identical in a position
        /// track, so the count is the only way to tell the feature ran at all.
        public int HintsIssued { get; private set; }
        public int SuccessfulHides => successfulHides;
        public float WardrobeBonus => config == null ? 0f
            : Mathf.Min(config.maxWardrobeBonus,
                Mathf.Max(0, successfulHides - config.hidesBeforeLearning) * config.wardrobeBonusPerHide);
        public float QuietSeconds => Time.time - lastSensedAt;

        /// How much of the authored quiet threshold still applies. Reads the
        /// maniac's own escalation, which is allowed: the Director already
        /// depends on the Maniac feature (never the other way round), which is
        /// why ManiacHintEvent is declared over there and not here.
        public float HintQuietScale => maniac != null && maniac.Escalation != null
            ? ManiacEscalation.HintQuietMultiplier(maniac.Escalation.Config, maniac.Escalation.Intensity)
            : 1f;

        public void Init(DirectorConfig directorConfig) => config = directorConfig;

        void Start()
        {
            if (config == null) config = Resources.Load<DirectorConfig>("C#/Director/Configs/DirectorConfig");
            lastSensedAt = Time.time;
            EventBus.Subscribe<TimeKiller.Hiding.PlayerHidEvent>(OnHid);
            EventBus.Subscribe<TimeKiller.Hiding.PlayerUnhidEvent>(OnUnhid);
            EventBus.Subscribe<ManiacSpottedPlayerEvent>(OnSpotted);

            DebugOverlay.Watch("Director", () => config == null || !config.enabled
                ? "off"
                : $"quiet {QuietSeconds:0}s  hints {HintsIssued}  hides {successfulHides}  wardrobeBonus +{WardrobeBonus:0.00}");
        }

        void OnDestroy()
        {
            EventBus.Unsubscribe<TimeKiller.Hiding.PlayerHidEvent>(OnHid);
            EventBus.Unsubscribe<TimeKiller.Hiding.PlayerUnhidEvent>(OnUnhid);
            EventBus.Unsubscribe<ManiacSpottedPlayerEvent>(OnSpotted);
            DebugOverlay.Unwatch("Director");
        }

        void OnHid(TimeKiller.Hiding.PlayerHidEvent evt) { hiding = true; foundWhileHiding = false; }

        /// A hide only COUNTS as successful if he never found them during it —
        /// otherwise being dragged out of a wardrobe would teach him to search
        /// wardrobes, which is backwards: he already knows that trick works.
        void OnUnhid(TimeKiller.Hiding.PlayerUnhidEvent evt)
        {
            if (hiding && !foundWhileHiding)
            {
                successfulHides++;
                EventBus.Publish(new ManiacLearnedEvent
                {
                    WardrobeBonus = WardrobeBonus,
                    SuccessfulHides = successfulHides,
                });
            }
            hiding = false;
        }

        void OnSpotted(ManiacSpottedPlayerEvent evt)
        {
            if (hiding) foundWhileHiding = true;
        }

        void Update()
        {
            if (config == null || !config.enabled) return;
            if (maniac == null) maniac = Object.FindAnyObjectByType<ManiacController>();
            if (player == null)
            {
                var pc = Object.FindAnyObjectByType<PlayerController>();
                if (pc == null) return;
                player = pc.transform;
            }
            if (maniac == null || maniac.Perception == null) return;

            // Any awareness at all resets the clock. The Director's job is to
            // rescue DEAD time, not to pile on while something is already happening.
            if (maniac.Perception.Level != ManiacPerception.AwarenessLevel.Unaware)
            {
                lastSensedAt = Time.time;
                return;
            }

            // Escalation shortens the wait late in the run — both thresholds by
            // the SAME factor, so the ratio this was tuned with survives and he
            // returns sooner rather than more often. 1 without escalation, so the
            // Director is unchanged when the component or its config is gone.
            float quietScale = HintQuietScale;
            if (QuietSeconds < config.hintAfterQuietSeconds * quietScale) return;
            if (Time.time - lastHintAt < config.minSecondsBetweenHints * quietScale) return;

            Vector2 playerPos = player.position;
            float distance = Vector2.Distance(maniac.Motor.Position, playerPos);
            if (distance < config.minHintDistance) return;   // already near — nudging would read as a cheat

            IssueHint(playerPos);
        }

        /// Smear the position, then publish. The error is what makes this honest:
        /// he is being sent to a NEIGHBOURHOOD, and arriving there tells him
        /// nothing he did not already have to earn with his eyes and ears.
        void IssueHint(Vector2 playerPos)
        {
            float staleness = Mathf.Min(config.maxHintError,
                config.hintError + (QuietSeconds / 10f) * config.errorGrowthPer10s);

            // A random direction, not a bias — a consistent offset would be a
            // pattern players could learn to stand on the safe side of.
            float angle = Random.value * Mathf.PI * 2f;
            Vector2 offset = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * Random.Range(staleness * 0.4f, staleness);

            lastHintAt = Time.time;
            HintsIssued++;
            EventBus.Publish(new ManiacHintEvent
            {
                Area = playerPos + offset,
                Radius = config.hintRadius,
                Desperate = QuietSeconds > config.hintAfterQuietSeconds * 2f,
            });
        }
    }
}
