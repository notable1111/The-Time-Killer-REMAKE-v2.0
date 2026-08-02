// The tension radar. Listens to the maniac's events on the bus (never
// references him directly) and keeps ONE music layer playing at a time with
// crossfades. Priority (high→low): Chase > Endgame > Safe > Investigate >
// Mystery > Dread. Endgame latches on when the last clock is fixed.
// Mystery is the "about to enter / act" approach dread, driven by hand-placed
// mysteryZones. Stings: jumpscare on spotted, dark riser when he first hears
// you (from an unaware layer), death sting. Fully removable — delete the
// object, game runs.
using TimeKiller.Core;
using TimeKiller.Maniac;
using TimeKiller.Objectives;
using TimeKiller.Player;
using UnityEngine;

namespace TimeKiller.Audio
{
    public class AudioDirector : MonoBehaviour
    {
        public enum Layer { Dread, Mystery, Investigate, Chase, Safe, Endgame }

        [SerializeField] AudioConfig config;
        [SerializeField] AudioSource musicA;
        [SerializeField] AudioSource musicB;
        [SerializeField] AudioSource stings;

        Layer currentLayer = (Layer)(-1);
        AudioSource active, standby;
        float fade;                 // 0..1 progress of the crossfade
        string maniacState = nameof(PatrolState);
        Transform player;
        MusicZone[] sceneZones;     // collected once; zones are authored, not spawned
        float nextSpottedSting;
        float nextRiser;
        bool endgame;               // gate is open — the run for the door

        public void Init(AudioConfig audioConfig) => config = audioConfig;

        void Start()
        {
            active = musicA;
            standby = musicB;
            sceneZones = Object.FindObjectsByType<MusicZone>(FindObjectsSortMode.None);
            EventBus.Subscribe<ManiacStateChangedEvent>(OnManiacState);
            EventBus.Subscribe<ManiacSpottedPlayerEvent>(OnSpotted);
            EventBus.Subscribe<ManiacHeardNoiseEvent>(OnHeard);
            EventBus.Subscribe<PlayerDiedEvent>(OnDied);
            EventBus.Subscribe<AllClocksFixedEvent>(OnGateOpened);
            EventBus.Subscribe<GameWonEvent>(OnEscaped);
            DebugOverlay.Watch("Music", () => $"{currentLayer} ({(active != null && active.clip != null ? active.clip.name : "-")})"
                + $" zones:{(sceneZones == null ? 0 : sceneZones.Length)}");
            // The mix has to be visible to be tuned by ear — otherwise a duck
            // that is firing and a duck that is broken sound the same as "quiet".
            DebugOverlay.Watch("Mix", () => !AudioMix.Active ? "off" :
                $"mus {AudioMix.Describe(MixChannel.Music)} " +
                $"hrt {AudioMix.Describe(MixChannel.Heartbeat)} " +
                $"mBr {AudioMix.Describe(MixChannel.ManiacBreath)} " +
                $"sting {AudioMix.Describe(MixChannel.Sting)}");
        }

        void OnDestroy()
        {
            EventBus.Unsubscribe<ManiacStateChangedEvent>(OnManiacState);
            EventBus.Unsubscribe<ManiacSpottedPlayerEvent>(OnSpotted);
            EventBus.Unsubscribe<ManiacHeardNoiseEvent>(OnHeard);
            EventBus.Unsubscribe<PlayerDiedEvent>(OnDied);
            EventBus.Unsubscribe<AllClocksFixedEvent>(OnGateOpened);
            EventBus.Unsubscribe<GameWonEvent>(OnEscaped);
            DebugOverlay.Unwatch("Music");
            DebugOverlay.Unwatch("Mix");
        }

        void OnManiacState(ManiacStateChangedEvent evt) => maniacState = evt.StateName;

        void OnSpotted(ManiacSpottedPlayerEvent evt)
        {
            if (Time.time < nextSpottedSting) return;
            if (config.spottedStings != null && config.spottedStings.Length > 0)
                PlaySting(config.spottedStings[Random.Range(0, config.spottedStings.Length)]);
            nextSpottedSting = Time.time + config.spottedStingCooldown;
        }

        void OnHeard(ManiacHeardNoiseEvent evt)
        {
            // Only the FIRST suspicion from an unaware layer gets the riser —
            // constant re-triggers while already alert would wear it out.
            bool unaware = currentLayer == Layer.Dread || currentLayer == Layer.Mystery;
            if (!unaware || Time.time < nextRiser) return;
            if (config.heardRiser != null) PlaySting(config.heardRiser);
            nextRiser = Time.time + config.riserCooldown;
        }

        void OnDied(PlayerDiedEvent evt)
        {
            if (config.deathSting != null) PlaySting(config.deathSting);
        }

        // The last clock lands. This sting is deliberately NOT positional — the
        // whole castle hears the gate give way, and so does he.
        void OnGateOpened(AllClocksFixedEvent evt)
        {
            endgame = true;
            if (config.gateUnlockSting != null) PlaySting(config.gateUnlockSting);
        }

        void OnEscaped(GameWonEvent evt)
        {
            if (config.escapeSting != null) PlaySting(config.escapeSting);
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
                standby.volume = standbyStartVolume * (1f - fade) * AudioDucking.World
                               * AudioMix.GainFor(MixChannel.Music);
                if (fade >= 1f && standby.isPlaying) standby.Stop();
            }

            // The score is the "world" that PlayerHeartbeat pulls away once the
            // maniac has you, leaving only your own heart. Applied here every
            // frame rather than folded into targetVolume so it survives the
            // crossfade above and so nothing breaks if the dial never moves.
            if (active != null)
                active.volume = targetVolume * fade * AudioDucking.World * AudioMix.GainFor(MixChannel.Music);
        }

        float targetVolume;
        float standbyStartVolume;

        Layer DesiredLayer()
        {
            bool chased = maniacState == nameof(ChaseState) || maniacState == nameof(AttackState);
            if (chased) return Layer.Chase;
            // Once the gate is open nothing sounds calm again — only being seen
            // outranks the run for the door.
            if (endgame && config.endgameTracks != null && config.endgameTracks.Length > 0) return Layer.Endgame;
            if (InSafeZone()) return Layer.Safe;
            // Both "he heard you" and "he's hunting where he lost you" keep the tension up.
            if (maniacState == nameof(InvestigateState) || maniacState == nameof(SearchState)) return Layer.Investigate;
            if (InMysteryZone()) return Layer.Mystery;
            return Layer.Dread;
        }

        bool InSafeZone() => InAnyZone(config.safeZones, MusicZone.Kind.Safe);
        bool InMysteryZone() => InAnyZone(config.mysteryZones, MusicZone.Kind.Mystery);

        /// Config rects OR a scene-placed MusicZone. Both, on purpose: the rects
        /// are the original hand-typed servant-passage zones and still work, while
        /// scene zones are per-map and draggable. AudioConfig is shared by every
        /// scene, so a rect authored for one map is also live in the other — which
        /// is exactly why scene zones exist.
        bool InAnyZone(Rect[] zones, MusicZone.Kind kind)
        {
            if (player == null)
            {
                var controller = Object.FindAnyObjectByType<PlayerController>();
                if (controller == null) return false;
                player = controller.transform;
            }
            Vector2 p = player.position;

            if (zones != null)
                foreach (var zone in zones)
                    if (zone.Contains(p)) return true;

            if (sceneZones != null)
                foreach (var zone in sceneZones)
                    if (zone != null && zone.kind == kind && zone.Contains(p)) return true;

            return false;
        }

        AudioClip PickTrack(Layer layer)
        {
            var pool = layer switch
            {
                Layer.Mystery => config.mysteryTracks,
                Layer.Investigate => config.investigateTracks,
                Layer.Chase => config.chaseTracks,
                Layer.Safe => config.safeTracks,
                Layer.Endgame => config.endgameTracks,
                _ => config.dreadTracks,
            };
            return pool != null && pool.Length > 0 ? pool[Random.Range(0, pool.Length)] : null;
        }

        float LayerVolume(Layer layer) => layer switch
        {
            Layer.Mystery => config.mysteryVolume,
            Layer.Investigate => config.investigateVolume,
            Layer.Chase => config.chaseVolume,
            Layer.Safe => config.safeVolume,
            Layer.Endgame => config.endgameVolume,
            _ => config.dreadVolume,
        };

        /// Every sting goes through here. A sting is tier 0 — the punctuation the
        /// whole mix makes room for — so it announces itself for exactly its own
        /// length before it plays, and everything below it steps back.
        void PlaySting(AudioClip clip)
        {
            if (clip == null || stings == null) return;
            AudioMix.Announce(MixChannel.Sting, clip.length);
            stings.PlayOneShot(clip, config.stingVolume * AudioMix.GainFor(MixChannel.Sting));
        }

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
