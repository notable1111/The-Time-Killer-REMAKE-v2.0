// The tension radar. Listens to the maniac's events on the bus (never
// references him directly) and keeps ONE music layer playing at a time with
// crossfades: Safe (inside the servant passage) > Chase > Tense > Calm.
// Stings: jumpscare on spotted, dark riser when he first hears you (from
// Calm only), death sting. Fully removable — delete the object, game runs.
using TimeKiller.Core;
using TimeKiller.Maniac;
using TimeKiller.Player;
using UnityEngine;

namespace TimeKiller.Audio
{
    public class AudioDirector : MonoBehaviour
    {
        public enum Layer { Calm, Tense, Chase, Safe }

        [SerializeField] AudioConfig config;
        [SerializeField] AudioSource musicA;
        [SerializeField] AudioSource musicB;
        [SerializeField] AudioSource stings;

        Layer currentLayer = (Layer)(-1);
        AudioSource active, standby;
        float fade;                 // 0..1 progress of the crossfade
        string maniacState = nameof(PatrolState);
        Transform player;
        float nextSpottedSting;
        float nextRiser;

        public void Init(AudioConfig audioConfig) => config = audioConfig;

        void Start()
        {
            active = musicA;
            standby = musicB;
            EventBus.Subscribe<ManiacStateChangedEvent>(OnManiacState);
            EventBus.Subscribe<ManiacSpottedPlayerEvent>(OnSpotted);
            EventBus.Subscribe<ManiacHeardNoiseEvent>(OnHeard);
            EventBus.Subscribe<PlayerDiedEvent>(OnDied);
            DebugOverlay.Watch("Music", () => $"{currentLayer} ({(active != null && active.clip != null ? active.clip.name : "-")})");
        }

        void OnDestroy()
        {
            EventBus.Unsubscribe<ManiacStateChangedEvent>(OnManiacState);
            EventBus.Unsubscribe<ManiacSpottedPlayerEvent>(OnSpotted);
            EventBus.Unsubscribe<ManiacHeardNoiseEvent>(OnHeard);
            EventBus.Unsubscribe<PlayerDiedEvent>(OnDied);
            DebugOverlay.Unwatch("Music");
        }

        void OnManiacState(ManiacStateChangedEvent evt) => maniacState = evt.StateName;

        void OnSpotted(ManiacSpottedPlayerEvent evt)
        {
            if (Time.time < nextSpottedSting) return;
            if (config.spottedStings != null && config.spottedStings.Length > 0)
                stings.PlayOneShot(config.spottedStings[Random.Range(0, config.spottedStings.Length)], config.stingVolume);
            nextSpottedSting = Time.time + config.spottedStingCooldown;
        }

        void OnHeard(ManiacHeardNoiseEvent evt)
        {
            // Only the FIRST suspicion out of calm gets the riser — constant
            // re-triggers while already tense would wear it out.
            if (currentLayer != Layer.Calm || Time.time < nextRiser) return;
            if (config.heardRiser != null)
                stings.PlayOneShot(config.heardRiser, config.stingVolume);
            nextRiser = Time.time + config.riserCooldown;
        }

        void OnDied(PlayerDiedEvent evt)
        {
            if (config.deathSting != null)
                stings.PlayOneShot(config.deathSting, config.stingVolume);
        }

        void Update()
        {
            if (config == null) return;
            var desired = DesiredLayer();
            if (desired != currentLayer)
            {
                currentLayer = desired;
                BeginCrossfade(PickTrack(desired), LayerVolume(desired));
            }

            // Drive the crossfade.
            if (fade < 1f)
            {
                fade = Mathf.MoveTowards(fade, 1f, Time.deltaTime / Mathf.Max(0.05f, config.crossfadeSeconds));
                active.volume = targetVolume * fade;
                standby.volume = standbyStartVolume * (1f - fade);
                if (fade >= 1f && standby.isPlaying) standby.Stop();
            }
        }

        float targetVolume;
        float standbyStartVolume;

        Layer DesiredLayer()
        {
            bool chased = maniacState == nameof(ChaseState) || maniacState == nameof(AttackState);
            if (!chased && InSafeZone()) return Layer.Safe;
            if (chased) return Layer.Chase;
            if (maniacState == nameof(InvestigateState)) return Layer.Tense;
            return Layer.Calm;
        }

        bool InSafeZone()
        {
            if (player == null)
            {
                var controller = Object.FindAnyObjectByType<PlayerController>();
                if (controller == null) return false;
                player = controller.transform;
            }
            Vector2 p = player.position;
            foreach (var zone in config.safeZones)
                if (zone.Contains(p)) return true;
            return false;
        }

        AudioClip PickTrack(Layer layer)
        {
            var pool = layer switch
            {
                Layer.Tense => config.tenseTracks,
                Layer.Chase => config.chaseTracks,
                Layer.Safe => config.safeTracks,
                _ => config.calmTracks,
            };
            return pool != null && pool.Length > 0 ? pool[Random.Range(0, pool.Length)] : null;
        }

        float LayerVolume(Layer layer) => layer switch
        {
            Layer.Tense => config.tenseVolume,
            Layer.Chase => config.chaseVolume,
            Layer.Safe => config.safeVolume,
            _ => config.calmVolume,
        };

        void BeginCrossfade(AudioClip next, float volume)
        {
            // Swap roles: the playing source fades out, the standby fades in.
            (active, standby) = (standby, active);
            standbyStartVolume = standby.volume;
            targetVolume = volume;
            fade = 0f;
            active.clip = next;
            active.loop = true;
            active.volume = 0f;
            if (next != null) active.Play();
        }
    }
}
