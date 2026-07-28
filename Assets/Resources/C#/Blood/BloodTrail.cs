// You bleed when you are hurt, and the floor remembers.
//
// Lives on the Player. Two ways blood is spilled:
//   THE WOUND   — a splash on the floor the moment you are hit, thrown in the
//                 direction the blow came from.
//   THE TRAIL   — while wounded and moving, a drip every so often, and faster
//                 the closer you are to dying.
//
// Publishes BloodSpilledEvent and nothing else — it never touches the stain
// renderer, so this component and BloodStainField are independently removable.
// It also reads health only through the event bus, so it compiles and runs even
// if the health feature is stripped out (you simply never bleed).
using TimeKiller.Core;
using TimeKiller.Player;
using UnityEngine;

namespace TimeKiller.Blood
{
    public class BloodTrail : MonoBehaviour
    {
        [SerializeField] BloodConfig config;

        int currentHp = int.MaxValue;
        int maxHp = 3;
        float nextDripAt;
        Vector2 lastDripPosition;

        public void Init(BloodConfig bloodConfig) => config = bloodConfig;

        void Start()
        {
            lastDripPosition = transform.position;
            var healthConfig = Resources.Load<PlayerHealthConfig>("C#/Player/Configs/PlayerHealthConfig");
            if (healthConfig != null) maxHp = Mathf.Max(1, healthConfig.maxHealth);

            EventBus.Subscribe<PlayerHealthChangedEvent>(OnHealthChanged);
            EventBus.Subscribe<PlayerHitEvent>(OnHit);
            DebugOverlay.Watch("Bleed", () => config == null
                ? "NO CONFIG"
                : Bleeding ? $"yes ({currentHp}/{maxHp}, every {DripPeriod():0.00}s)" : "no");
        }

        void OnDestroy()
        {
            EventBus.Unsubscribe<PlayerHealthChangedEvent>(OnHealthChanged);
            EventBus.Unsubscribe<PlayerHitEvent>(OnHit);
            DebugOverlay.Unwatch("Bleed");
        }

        bool Bleeding => config != null && currentHp <= config.bleedAtHp;

        void OnHealthChanged(PlayerHealthChangedEvent evt) => currentHp = evt.Current;

        void OnHit(PlayerHitEvent evt)
        {
            if (config == null || config.stainsPerHit <= 0) return;

            Vector2 at = transform.position;
            Vector2 away = at - evt.SourcePosition;
            EventBus.Publish(new BloodSpilledEvent
            {
                Position = at,
                Amount = config.hitSizeBoost,
                Direction = away.sqrMagnitude > 0.0001f ? away.normalized : Vector2.zero,
                Spread = config.hitSpread,
                Count = config.stainsPerHit,
            });
            // The wound resets the drip clock so the trail starts from the hit
            // rather than mid-interval.
            nextDripAt = Time.time + DripPeriod();
            lastDripPosition = at;
        }

        /// Seconds between drips. Scales from the base interval at the bleeding
        /// threshold down to a much faster drip on the last hit point — bleeding
        /// out should feel like it is getting worse, not steady.
        float DripPeriod()
        {
            float baseRate = 1f / Mathf.Max(0.01f, config.dripInterval);
            float severity = config.bleedAtHp <= 1
                ? 1f
                : Mathf.InverseLerp(config.bleedAtHp, 1f, currentHp);   // 0 at threshold, 1 at 1 HP
            return 1f / Mathf.Max(0.01f, baseRate + severity * config.criticalBleedBonus);
        }

        void Update()
        {
            if (!Bleeding) return;
            if (Time.time < nextDripAt) return;

            Vector2 at = transform.position;
            // Distance gate, not just a timer: standing still must not stack a
            // puddle on one spot, and it would also hand the maniac a beacon for
            // free if tracking is ever switched on.
            if (Vector2.Distance(at, lastDripPosition) < config.dripMinDistance) return;

            EventBus.Publish(new BloodSpilledEvent
            {
                Position = at,
                Amount = 1f,
                Direction = Vector2.zero,
                Spread = config.dripMinDistance * 0.35f,
                Count = Mathf.Max(1, config.dripStains),
            });

            lastDripPosition = at;
            nextDripAt = Time.time + DripPeriod();
        }
    }
}
