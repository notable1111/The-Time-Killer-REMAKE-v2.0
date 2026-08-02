// The tension bed: a low drone that swells with fear and gets out of the way.
//
// It is NOT music and it is NOT the ambience. The ambience is the castle, and it
// keeps playing (only ducked a couple of dB) because the maniac's footsteps live
// in it and taking those away at the worst moment is the cruellest thing this
// game could do. This is a separate sub-200Hz layer that exists to be FELT
// rather than heard — you should not be able to say when it started.
//
// WHY MONOLITH_1. Measured across the whole EchoChambers ambience set
// (2026-08-02): lowpassing at 200Hz costs it only 0.5 dB, so essentially all of
// its energy is already below 200Hz — a true drone rather than a texture. Void_1
// and An Empty Home_1 lose 4-5 dB to the same filter, meaning a lot of their
// content is air and detail that would fight the heartbeat. Dark Eerie_1 is
// equally low but sits 12 dB hotter, so it would have to be trimmed so far that
// its noise floor came with it.
//
// THE SILENCE RULE. After a real fright the drone pulls BACK further than fear
// alone would explain, and comes back slowly. A tension layer that simply
// tracks fear is a loudness meter; taking it away is what makes the next swell
// land. Silence is the instrument.
//
// Removable: delete the component and the game loses one bed layer. Nothing
// references it; it only listens.
using TimeKiller.Core;
using UnityEngine;

namespace TimeKiller.Fear
{
    [RequireComponent(typeof(AudioSource))]
    public class FearDrone : MonoBehaviour
    {
        [SerializeField] FearConfig config;
        [SerializeField] AudioClip droneLoop;

        [Tooltip("Loudest the drone gets, at maximum fear. Low on purpose — it is a bed, not a cue.")]
        [Range(0f, 1f)] [SerializeField] float maxVolume = 0.34f;
        [Tooltip("Fear below which the drone is silent. Above the heartbeat's own gate so the pulse always arrives first — the body reacts before the room does.")]
        [Range(0f, 1f)] [SerializeField] float startsAt = 0.1f;
        [Tooltip("Seconds to swell in. Slow: you should never catch it starting.")]
        [SerializeField] float riseSeconds = 4f;
        [Tooltip("Seconds to fade out.")]
        [SerializeField] float fallSeconds = 6f;
        [Tooltip("How far the drone drops BELOW its fear level in the seconds after a fright passes. This is the deliberate silence — a bed that only ever tracks fear stops being felt.")]
        [Range(0f, 1f)] [SerializeField] float aftershockDip = 0.55f;

        AudioSource source;
        float fear;
        bool recovering;
        float haveFearUntil = float.NegativeInfinity;
        float voiced;

        public float Voiced => voiced;
        public string Problem => droneLoop == null ? "no drone clip" : "";

        public void Init(FearConfig fearConfig, AudioClip clip)
        {
            config = fearConfig;
            droneLoop = clip;
        }

        void Awake()
        {
            source = GetComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = true;
            source.spatialBlend = 0f;   // it is inside your head, not in the room
            source.volume = 0f;
            if (config == null) config = Resources.Load<FearConfig>("C#/Fear/Configs/FearConfig");
        }

        void Start()
        {
            EventBus.Subscribe<FearChangedEvent>(OnFearChanged);
            DebugOverlay.Watch("Drone", () => string.IsNullOrEmpty(Problem)
                ? $"vol {voiced:0.00}{(recovering ? " (dipped)" : "")}"
                : Problem);

            if (droneLoop != null)
            {
                source.clip = droneLoop;
                source.Play();   // always running; volume is the only gate
            }
        }

        void OnDestroy()
        {
            EventBus.Unsubscribe<FearChangedEvent>(OnFearChanged);
            DebugOverlay.Unwatch("Drone");
        }

        void OnFearChanged(FearChangedEvent e)
        {
            fear = e.Fear;
            recovering = e.Recovering;
            haveFearUntil = Time.time + 0.5f;
        }

        void Update()
        {
            if (source == null || droneLoop == null || config == null) return;
            // No conductor running (or it was deleted) — fade out and stay out
            // rather than holding whatever level we last heard about.
            float target = Time.time <= haveFearUntil ? LevelFor(fear) : 0f;

            float seconds = target > voiced ? riseSeconds : fallSeconds;
            voiced = Mathf.MoveTowards(voiced, target, Time.deltaTime / Mathf.Max(0.01f, seconds));
            source.volume = voiced * TimeKiller.Audio.AudioMix.GainFor(TimeKiller.Audio.MixChannel.Ambience)
                            * AudioDucking.World;
        }

        float LevelFor(float f)
        {
            if (f < startsAt) return 0f;
            float shaped = Mathf.InverseLerp(startsAt, 1f, f);
            float level = maxVolume * shaped * config.fearEffectMultiplier;
            // The dip. While fear is decaying the room goes quieter than the
            // number alone would justify, so the next swell has somewhere to
            // come from.
            if (recovering) level *= 1f - aftershockDip;
            return level;
        }
    }
}
