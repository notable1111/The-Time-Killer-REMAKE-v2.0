// The one short sound the moment he commits to a hunt.
//
// Separate from FearConductor on purpose: the conductor decides WHEN a detection
// is worth marking (it owns the cooldown and the fear gate), this decides what it
// sounds like. Delete this component and the system is silent at that instant but
// otherwise unchanged; delete the conductor and this simply never fires.
//
// Restraint is the whole design. A sting that plays every time his Detected flag
// flickers behind a pillar stops being a shock and becomes a rhythm section —
// which is why the conductor gates it on a real cooldown AND on there being
// enough fear to justify it. Being clocked from the far side of the castle gets
// no sting at all.
using TimeKiller.Core;
using UnityEngine;

namespace TimeKiller.Fear
{
    [RequireComponent(typeof(AudioSource))]
    public class FearSting : MonoBehaviour
    {
        [SerializeField] FearConfig config;
        [Tooltip("Detection stings. One is picked at random so a repeat within a run does not read as a copy-paste.")]
        [SerializeField] AudioClip[] stings;

        AudioSource source;

        public int StingCount { get; private set; }
        public string Problem => stings == null || stings.Length == 0 ? "no sting clips" : "";

        public void Init(FearConfig fearConfig, AudioClip[] clips)
        {
            config = fearConfig;
            stings = clips;
        }

        void Awake()
        {
            source = GetComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;   // it is a score cue, not a thing in the room
            if (config == null) config = Resources.Load<FearConfig>("C#/Fear/Configs/FearConfig");
        }

        void Start()
        {
            EventBus.Subscribe<FearDetectionEvent>(OnDetected);
            DebugOverlay.Watch("Sting", () =>
                string.IsNullOrEmpty(Problem) ? $"{StingCount} played" : Problem);
        }

        void OnDestroy()
        {
            EventBus.Unsubscribe<FearDetectionEvent>(OnDetected);
            DebugOverlay.Unwatch("Sting");
        }

        void OnDetected(FearDetectionEvent e)
        {
            if (config == null || !config.stingEnabled) return;
            if (source == null || stings == null || stings.Length == 0) return;

            var clip = stings[Random.Range(0, stings.Length)];
            if (clip == null) return;

            // Scaled by the fear that earned it, so a distant sighting is a hint
            // and a close one is a shock. Announced through the mix so the score
            // steps back under it instead of both fighting for the same band.
            float volume = Mathf.Clamp01(config.stingVolume * Mathf.Clamp01(0.55f + 0.45f * e.Fear)
                                         * config.fearEffectMultiplier);
            TimeKiller.Audio.AudioMix.Announce(TimeKiller.Audio.MixChannel.Sting, clip.length);
            source.PlayOneShot(clip, volume
                * TimeKiller.Audio.AudioMix.GainFor(TimeKiller.Audio.MixChannel.Sting));
            StingCount++;
        }
    }
}
