// What the player's body says out loud. Pain, panic, effort, and the last cry.
//
// Everything here is driven off the event bus rather than off the systems that
// cause it, so damage, death and detection stay unaware that anyone is listening.
//
// One choice worth stating plainly: pain is triggered by PlayerHealthChangedEvent
// going DOWN, not by PlayerHitEvent. PlayerHitEvent is published by the attacker
// and fires even when the hit lands inside the invulnerability window, where it
// does nothing at all (PlayerHealth.cs:72 returns early). Grunting there would
// have the player cry out for damage they never took — audible feedback for an
// event the rules already discarded.
//
// Removable: delete the component and the survivor is simply mute.
using TimeKiller.Core;
using TimeKiller.Maniac;
using TimeKiller.Objectives;
using UnityEngine;

namespace TimeKiller.Player
{
    public class PlayerVoice : MonoBehaviour
    {
        [SerializeField] PlayerVoiceConfig config;

        AudioSource source;
        ClockRepair repair;
        int lastKnownHealth = int.MinValue;
        float nextGasp;
        float nextRepairEffort;
        bool dead;

        /// Counts every line played, by kind. Public because "did the grunt fire?"
        /// is otherwise unanswerable after the fact — the sound is gone and no
        /// state remains to prove it happened.
        public int PainCount { get; private set; }
        public int GaspCount { get; private set; }
        public int DeathCount { get; private set; }

        public void Init(PlayerVoiceConfig voiceConfig) => config = voiceConfig;

        void Awake()
        {
            var go = new GameObject("Voice");
            go.transform.SetParent(transform, false);
            source = go.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;   // your own throat, not a world sound
        }

        void Start()
        {
            EventBus.Subscribe<PlayerHealthChangedEvent>(OnHealthChanged);
            EventBus.Subscribe<PlayerDiedEvent>(OnDied);
            EventBus.Subscribe<ManiacSpottedPlayerEvent>(OnSpotted);
            repair = GetComponent<ClockRepair>();
            if (repair == null) repair = Object.FindAnyObjectByType<ClockRepair>();
            DebugOverlay.Watch("Voice", () => config == null || !config.enabled
                ? "OFF"
                : $"pain {PainCount} gasp {GaspCount} death {DeathCount}");
        }

        void OnDestroy()
        {
            EventBus.Unsubscribe<PlayerHealthChangedEvent>(OnHealthChanged);
            EventBus.Unsubscribe<PlayerDiedEvent>(OnDied);
            EventBus.Unsubscribe<ManiacSpottedPlayerEvent>(OnSpotted);
            DebugOverlay.Unwatch("Voice");
        }

        void Update()
        {
            if (config == null || !config.enabled || dead) return;
            if (repair == null || !repair.Repairing) return;
            if (Time.time < nextRepairEffort) return;
            nextRepairEffort = Time.time + Random.Range(config.repairMinInterval, config.repairMaxInterval);
            Play(config.repairEfforts, config.repairVolume);
        }

        void OnHealthChanged(PlayerHealthChangedEvent evt)
        {
            int previous = lastKnownHealth;
            lastKnownHealth = evt.Current;
            if (config == null || !config.enabled || dead) return;
            // First event of the run is the initial reading, not a wound. Healing
            // and respawns raise it, and neither should hurt.
            if (previous == int.MinValue || evt.Current >= previous) return;
            // The death cry covers the killing blow — a grunt on top of it would
            // step on the one line that should land cleanly.
            if (evt.Current <= 0) return;

            float remaining = evt.Max > 0 ? (float)evt.Current / evt.Max : 1f;
            bool critical = remaining <= config.criticalAtOrBelow;
            var pool = critical && config.painGruntsCritical != null && config.painGruntsCritical.Length > 0
                ? config.painGruntsCritical
                : config.painGrunts;
            if (Play(pool, config.painVolume)) PainCount++;
        }

        void OnDied(PlayerDiedEvent evt)
        {
            if (config == null || !config.enabled || dead) return;
            dead = true;
            if (Play(config.deathCries, config.deathVolume)) DeathCount++;
        }

        void OnSpotted(ManiacSpottedPlayerEvent evt)
        {
            if (config == null || !config.enabled || dead || Time.time < nextGasp) return;
            nextGasp = Time.time + config.gaspCooldown;
            if (Play(config.spottedGasps, config.gaspVolume)) GaspCount++;
        }

        /// Returns whether anything actually played, so the counters above stay
        /// honest when a pool has not been wired yet.
        bool Play(AudioClip[] pool, float volume)
        {
            if (pool == null || pool.Length == 0 || source == null) return false;
            var clip = pool[Random.Range(0, pool.Length)];
            source.pitch = 1f + Random.Range(-config.pitchJitter, config.pitchJitter);
            // Announce before playing so the score is already stepping back when
            // the cry arrives rather than a beat afterwards.
            TimeKiller.Audio.AudioMix.Announce(TimeKiller.Audio.MixChannel.PlayerVoice, clip.length);
            source.PlayOneShot(clip, volume
                * TimeKiller.Audio.AudioMix.GainFor(TimeKiller.Audio.MixChannel.PlayerVoice));
            return true;
        }

        /// Respawn puts the body back in play; without this the survivor stays
        /// mute for the rest of the session after their first death.
        public void ResetForRespawn()
        {
            dead = false;
            lastKnownHealth = int.MinValue;
        }
    }
}
