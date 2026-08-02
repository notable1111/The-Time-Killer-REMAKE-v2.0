// The player's heartbeat: ONE clock, ONE voice, and the game's main tension
// channel.
//
// It used to be two. HidingVfx ran a distance-driven heart inside wardrobes and
// HealthVfxDirector ran a separate one at low health, each with its own
// AudioSource and its own phase — so being hurt inside a wardrobe played two
// unsynchronised hearts at once. The audio has a single owner now.
//
// THE RATE (design 2026-07-28):
//   distance ALWAYS drives it, on a curve that accelerates as he closes — that
//     is the channel the player learns to read, and it works even when he has
//     no idea they exist.
//   suspicion and detection raise a FLOOR under that, so being noticed spikes
//     the rate no matter how far off he is. Floors, not additions: stacking
//     would run off the top and make the loud end meaningless.
//   the rate CLIMBS fast and FALLS slow. Escaping does not feel safe for
//     several seconds afterwards, which is where most of the dread lives.
//
// On detection the world ducks away (AudioDucking) so the last thing you hear
// before he reaches you is your own body.
//
// Removable: delete this component and the game is silent-hearted and unducked
// but otherwise unchanged. Nothing depends on it; it reads other systems and
// writes one voluntary dial.
using TimeKiller.Core;
using TimeKiller.Maniac;
using TimeKiller.Player;
using UnityEngine;

namespace TimeKiller.Heartbeat
{
    [RequireComponent(typeof(AudioSource))]
    public class PlayerHeartbeat : MonoBehaviour
    {
        [SerializeField] HeartbeatConfig config;
        [Tooltip("The heartbeat. One clip for the whole range — the rate and the pitch carry the change, not a different sample.")]
        [SerializeField] AudioClip beatClip;

        [Tooltip("Optional. When a FearConductor is publishing, the rate and volume come from THIS asset's curves instead of the distance model below — one continuous fear value, no state floors. Leave empty and the component falls back to its own model unchanged.")]
        [SerializeField] TimeKiller.Fear.FearConfig fearConfig;

        AudioSource audioSource;
        ManiacController maniac;

        // Fear, if a conductor is running. `haveFear` stays false until the first
        // event arrives, so a scene with no conductor keeps the old behaviour
        // rather than sitting silently at fear 0.
        float fear;
        bool haveFear;
        float lastFearAt = float.NegativeInfinity;

        float bpm;
        float lastTargetBpm;
        float lastBeatVolume;
        float secondsToNextBeat = -1f;
        float phase;
        float nextBeatAt = 1f;      // in phase units; wobbles so it is not a metronome
        float duck = 1f;
        bool hidden;
        bool wasDetected;
        bool pendingPalpitation;   // the next beat is the thud after a skip

        /// Current rate. Visual systems can throb in time with this.
        public float Bpm => bpm;
        /// 0..1 across the configured rate range — drives volume, and anything
        /// else that wants to scale with dread.
        public float Intensity => config == null ? 0f
            : Mathf.Clamp01(Mathf.InverseLerp(config.calmBpm, config.nearBpm, bpm));
        /// True on the frame a beat fires.
        public bool BeatThisFrame { get; private set; }

        /// Below the audible gate — the heart is deliberately not beating at all.
        /// A heartbeat that never stops stops being frightening.
        public bool Silent => DrivenByFear
            ? fear < fearConfig.heartSilenceBelow
            : config != null && Intensity < config.silenceBelow;

        /// Seconds until the next scheduled beat, or -1 while silent. Exposed
        /// because AudioSource.isPlaying cannot answer "is the heart running?" —
        /// the clip is ~265ms inside an interval of up to a second, so most
        /// samples land in the gap and read as stopped when it is working.
        public float SecondsToNextBeat => secondsToNextBeat;
        public float CurrentVolume => lastBeatVolume;

        float softenUntil;
        float softenFactor = 1f;

        /// Step the heart back briefly so something else can be heard over it.
        /// Used for the recovery breath: the exhale IS the moment, and a heart
        /// still hammering at 150bpm buries it — measured 12dB louder than the
        /// breath even after the breath was normalised.
        public void Soften(float seconds, float factor)
        {
            softenUntil = Time.time + seconds;
            softenFactor = Mathf.Clamp01(factor);
        }
        /// Beats since the run began. AudioSource.isPlaying is useless for
        /// checking this from outside — the clip is 265ms inside an interval of
        /// up to a second, so most samples land in the silence between beats and
        /// read as "not beating" when it is working perfectly.
        public int BeatCount { get; private set; }

        public void Init(HeartbeatConfig heartbeatConfig, AudioClip clip)
        {
            config = heartbeatConfig;
            beatClip = clip;
        }

        public void InitFear(TimeKiller.Fear.FearConfig fear) => fearConfig = fear;

        /// True while the rate is being driven by the FearConductor rather than
        /// by this component's own fallback distance model.
        public bool DrivenByFear => fearConfig != null && haveFear
                                    && Time.time - lastFearAt < 0.5f;

        void OnFearChanged(TimeKiller.Fear.FearChangedEvent e)
        {
            fear = e.Fear;
            haveFear = true;
            lastFearAt = Time.time;
        }

        void Awake()
        {
            audioSource = GetComponent<AudioSource>();
            if (config != null) bpm = config.calmBpm;
        }

        void Start()
        {
            EventBus.Subscribe<TimeKiller.Hiding.PlayerHidEvent>(OnHid);
            EventBus.Subscribe<TimeKiller.Hiding.PlayerUnhidEvent>(OnUnhid);
            EventBus.Subscribe<TimeKiller.Fear.FearChangedEvent>(OnFearChanged);
            if (fearConfig == null)
                fearConfig = Resources.Load<TimeKiller.Fear.FearConfig>("C#/Fear/Configs/FearConfig");
            DebugOverlay.Watch("Heart", () => config == null
                ? "NO CONFIG"
                : $"{bpm:0} bpm (target {lastTargetBpm:0})  vol {lastBeatVolume:0.00}" +
                  $"  beats {BeatCount}  next {Mathf.Max(0f, secondsToNextBeat):0.00}s" +
                  $"  {(DrivenByFear ? "fear" : "fallback")}{(hidden ? "  (hidden)" : "")}" +
                  $"{(beatClip == null ? "  !! NO CLIP" : "")}");
        }

        void OnDestroy()
        {
            EventBus.Unsubscribe<TimeKiller.Hiding.PlayerHidEvent>(OnHid);
            EventBus.Unsubscribe<TimeKiller.Hiding.PlayerUnhidEvent>(OnUnhid);
            EventBus.Unsubscribe<TimeKiller.Fear.FearChangedEvent>(OnFearChanged);
            DebugOverlay.Unwatch("Heart");
            // Only release the world if we are the one holding it. With a
            // conductor running, the duck belongs to IT — stamping 1 here on a
            // scene reload would fight the conductor for the dial.
            if (!DrivenByFear) AudioDucking.SetWorld(1f);
        }

        void OnHid(TimeKiller.Hiding.PlayerHidEvent evt) => hidden = true;
        void OnUnhid(TimeKiller.Hiding.PlayerUnhidEvent evt) => hidden = false;

        void Update()
        {
            BeatThisFrame = false;
            if (config == null) return;
            float dt = Time.deltaTime;

            bool detected = false, suspicious = false;
            float target;

            if (DrivenByFear)
            {
                // The conductor has already smoothed fear (and asymmetrically —
                // fast up, slow down), so the rate follows it directly. Smoothing
                // a smoothed value again would just add lag.
                target = Mathf.Lerp(fearConfig.minBpm, fearConfig.maxBpm,
                                    Mathf.Clamp01(fearConfig.bpmCurve.Evaluate(fear)));
                bpm = target;
            }
            else
            {
                target = TargetBpm(out detected, out suspicious);
                // Asymmetric: adrenaline arrives at once, it leaves slowly.
                float speed = target > bpm ? config.riseSpeed : config.fallSpeed;
                bpm = Mathf.Lerp(bpm, target, 1f - Mathf.Exp(-speed * dt));
            }
            lastTargetBpm = target;

            // The conductor owns the world dial whenever it is running; two
            // writers on one shared static would fight every frame.
            if (!DrivenByFear) UpdateDuck(detected, dt);

            // The lurch: the chest kicks the instant he has you, before the rate
            // has had time to climb. One free beat, only on the transition.
            if (detected && !wasDetected && config.lurchOnDetected)
            {
                phase = 0f;
                nextBeatAt = 1f;
                Beat(force: true);
            }
            wasDetected = detected;

            if (Silent) { phase = 0f; nextBeatAt = 1f; secondsToNextBeat = -1f; return; }

            // secondsPerBeat = 60 / bpm, expressed as phase advancing at bpm/60.
            // The clip is never stretched or pitched to change tempo — the
            // SCHEDULE carries the rate, which is what keeps it sounding like a
            // body instead of a tape playing faster.
            phase += (bpm / 60f) * dt;
            secondsToNextBeat = bpm > 0.01f ? (nextBeatAt - phase) * 60f / bpm : -1f;
            if (phase < nextBeatAt) return;
            phase -= nextBeatAt;

            // A beat that follows a skipped one lands at full volume — the pause
            // is what frightens, and the thud after it is what you feel in your
            // throat. Consume the flag here so it applies to exactly one beat.
            bool thud = pendingPalpitation;
            pendingPalpitation = false;

            float wobble = DrivenByFear ? fearConfig.timingVariation : config.irregularity;
            nextBeatAt = 1f + Random.Range(-wobble, wobble);

            // ...and decide whether THIS interval is the one that stumbles.
            // Only high up, and rarely: it works because it is wrong, and a
            // stumble every few beats just reads as a broken metronome.
            float palpIntensity = DrivenByFear ? fear : Intensity;
            if (palpIntensity >= config.palpitationAbove && Random.value < config.palpitationChance)
            {
                nextBeatAt *= config.palpitationPause;
                pendingPalpitation = true;
            }

            Beat(force: thud, palpitation: thud);
        }

        void Beat(bool force, bool palpitation = false)
        {
            BeatThisFrame = true;
            BeatCount++;

            // Announce it before the audio, so a listener that throbs is in step
            // with the sound rather than a frame behind it. Published even with
            // no clip assigned: the visuals are not the audio's dependant.
            EventBus.Publish(new HeartbeatPulseEvent
            {
                Bpm = bpm,
                Intensity = Intensity,
                Palpitation = palpitation,
            });

            if (audioSource == null || beatClip == null) return;

            float soften = Time.time < softenUntil ? softenFactor : 1f;
            float volume;
            if (DrivenByFear)
            {
                // Starts at NEAR ZERO and builds. The old model ramped from
                // quietVolume (0.55), so the very first beat you could hear was
                // already better than half volume and there was nowhere left to
                // grow — the loud end meant nothing because the quiet end was not
                // quiet. The curve now owns the whole shape.
                float shaped = Mathf.Clamp01(fearConfig.heartVolumeCurve.Evaluate(fear));
                float varied = 1f + Random.Range(-fearConfig.volumeVariation, fearConfig.volumeVariation);
                volume = fearConfig.maxHeartVolume * shaped * varied * soften
                         * fearConfig.fearEffectMultiplier
                         * (hidden ? fearConfig.hiddenBoost : 1f);
                // No pitch ramp on the fear path. Speeding a heart up by pitching
                // the sample reads as a tape running fast; the SCHEDULE carries
                // the tempo. A whisker of per-beat variation keeps it organic.
                audioSource.pitch = 1f + Random.Range(-0.02f, 0.02f);
            }
            else
            {
                float loudness = force ? 1f : Mathf.Lerp(config.quietVolume, 1f, Intensity);
                volume = config.maxVolume * loudness * soften * (hidden ? config.hiddenBoost : 1f);
                audioSource.pitch = Mathf.Lerp(1f, config.pitchAtMax, Intensity);
            }
            volume = Mathf.Clamp01(volume);
            lastBeatVolume = volume;

            // ONE clip, always. A crossfade between three variants was tried on
            // 2026-07-28 and rejected by ear: it read as three different sounds
            // rather than one heart changing, and the single dry beat with the
            // pitch rise above is what actually sounds like a body.
            // The mix trims this, and specifically pulls it down when the maniac's
            // breath is loud — they are both sub-300Hz and measured 2.75 stacked.
            // Nothing is lost: the heart's RATE is what carries proximity, and the
            // rate keeps saying it at any volume.
            audioSource.PlayOneShot(beatClip, volume
                * TimeKiller.Audio.AudioMix.GainFor(TimeKiller.Audio.MixChannel.Heartbeat));
        }

        /// Distance sets the rate; state sets a floor it cannot fall below.
        float TargetBpm(out bool detected, out bool suspicious)
        {
            detected = false;
            suspicious = false;
            float target = config.calmBpm;

            if (maniac == null) maniac = Object.FindAnyObjectByType<ManiacController>();
            if (maniac != null && maniac.Perception != null)
            {
                float distance = Vector2.Distance(transform.position, maniac.Motor.Position);
                float closeness = 1f - Mathf.Clamp01(distance / Mathf.Max(0.1f, config.heartRange));
                // Curved so the climb accelerates as he closes — the panic is in
                // the CHANGE of rate, which a straight line never delivers.
                float shaped = Mathf.Pow(closeness, config.distanceCurve);
                target = Mathf.Lerp(config.calmBpm, config.nearBpm, shaped);

                var level = maniac.Perception.Level;
                detected = level == ManiacPerception.AwarenessLevel.Detected;
                suspicious = level == ManiacPerception.AwarenessLevel.Suspicious;

                // ANTICIPATION. His awareness climbs long before it crosses into
                // Suspicious, and you have no way to see it happening. Letting it
                // lift the rate means your chest reacts to being noticed before
                // you know you have been — which is the actual engine of panic:
                // you feel your body react, cannot find a cause, and supply a
                // worse one than the truth. Still answering the one question the
                // heart is allowed to answer ("how close is he to having you"),
                // so the no-exertion rule below stands.
                target = Mathf.Max(target, Mathf.Lerp(config.calmBpm, config.anticipationBpm,
                                                      maniac.Perception.Awareness));

                if (suspicious) target = Mathf.Max(target, config.suspiciousFloorBpm);
                if (detected) target = Mathf.Max(target, config.detectedFloorBpm);
            }

            // NOTHING ELSE RAISES IT. Sprinting and injury both used to, which
            // meant the heart pounded while running across an empty castle with
            // the maniac nowhere near — and that is precisely what makes the
            // sound stop meaning anything. The heartbeat answers exactly one
            // question now: how close is he? Exertion belongs to the lungs.
            return Mathf.Clamp(target, config.calmBpm, config.nearBpm);
        }

        void UpdateDuck(bool detected, float dt)
        {
            // The score recedes progressively as the heart climbs, then goes
            // fully once he has you — so the heartbeat takes over the mix on the
            // way in rather than only arriving at the last moment.
            float pull = detected ? 1f : Mathf.Clamp01(Intensity * config.duckByIntensity);
            float want = Mathf.Lerp(1f, config.duckedWorldVolume, pull);
            float seconds = want < duck ? config.duckInSeconds : config.duckOutSeconds;
            duck = Mathf.MoveTowards(duck, want, dt / Mathf.Max(0.01f, seconds));
            AudioDucking.SetWorld(duck);
        }
    }
}
