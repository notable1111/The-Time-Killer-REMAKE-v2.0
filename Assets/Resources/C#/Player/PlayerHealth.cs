// The 3-point health system. Listens for PlayerHitEvent on the EventBus (the
// maniac — or any future hazard — publishes it, never calls the player
// directly), applies damage + invulnerability window + shove, and publishes
// PlayerHealthChangedEvent / PlayerDiedEvent for UI, audio and enemies.
// v1 death rule: respawn at the spawn position with full health (documented
// placeholder until the save/death design is decided).
using TimeKiller.Core;
using UnityEngine;

namespace TimeKiller.Player
{
    [RequireComponent(typeof(Rigidbody2D))]
    public class PlayerHealth : MonoBehaviour
    {
        [SerializeField] PlayerHealthConfig config;

        public int Current { get; private set; }
        public bool IsInvulnerable => Time.time < invulnerableUntil || GodMode;
        public bool GodMode { get; set; }   // cheat hotkey (Core/CheatHotkeys)

        Rigidbody2D body;
        SpriteRenderer sprite;
        Vector3 spawnPosition;
        float invulnerableUntil;
        float flashUntil;

        public void Init(PlayerHealthConfig healthConfig) => config = healthConfig;

        void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            sprite = GetComponent<SpriteRenderer>();
            if (config == null)
                Debug.LogError("[PlayerHealth] PlayerHealthConfig not assigned.");
        }

        void Start()
        {
            Current = config != null ? config.maxHealth : 3;
            spawnPosition = transform.position;
            EventBus.Subscribe<PlayerHitEvent>(OnHit);
            DebugOverlay.Watch("Health", () => $"{Current}/{config?.maxHealth ?? 3}{(GodMode ? " (GOD)" : "")}");
            CheatHotkeys.RegisterCheat(UnityEngine.InputSystem.Key.F5, "God mode", () => GodMode = !GodMode);
            CheatHotkeys.RegisterCheat(UnityEngine.InputSystem.Key.F6, "Refill health", RefillFull);
            PublishChanged();
        }

        void OnDestroy()
        {
            EventBus.Unsubscribe<PlayerHitEvent>(OnHit);
            DebugOverlay.Unwatch("Health");
        }

        void Update()
        {
            if (sprite != null && Time.time >= flashUntil && sprite.color != Color.white)
                sprite.color = Color.white;
        }

        public void RefillFull()   // cheat hotkey
        {
            Current = config.maxHealth;
            PublishChanged();
        }

        void OnHit(PlayerHitEvent hit)
        {
            if (IsInvulnerable || Current <= 0) return;

            Current -= Mathf.Max(1, hit.Damage);
            invulnerableUntil = Time.time + config.invulnerabilitySeconds;

            // Shove away from the attacker — the escape window.
            var away = ((Vector2)transform.position - hit.SourcePosition).normalized;
            if (away.sqrMagnitude < 0.01f) away = Vector2.down;
            body.AddForce(away * config.shoveImpulse, ForceMode2D.Impulse);

            if (sprite != null)
            {
                sprite.color = new Color(1f, 0.35f, 0.35f);
                flashUntil = Time.time + config.hitFlashSeconds;
            }

            PublishChanged();

            if (Current <= 0)
            {
                EventBus.Publish(new PlayerDiedEvent { Position = transform.position });
                if (config.respawnOnDeath)
                {
                    // Teleport through the RIGIDBODY — writing transform.position
                    // alone gets overridden by interpolation on the next physics step.
                    body.position = spawnPosition;
                    transform.position = spawnPosition;
                    body.linearVelocity = Vector2.zero;
                    Physics2D.SyncTransforms();
                    Current = config.maxHealth;
                    PublishChanged();
                }
            }
        }

        void PublishChanged() =>
            EventBus.Publish(new PlayerHealthChangedEvent { Current = Current, Max = config?.maxHealth ?? 3 });
    }
}
