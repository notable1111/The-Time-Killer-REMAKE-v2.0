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

        AudioSource audioSource;
        ManiacController maniac;

        float bpm;
        float phase;
        float nextBeatAt = 1f;      // in phase units; wobbles so it is not a metronome
        float duck = 1f;
        bool hidden;
        bool wasDetected;

        /// Current rate. Visual systems can throb in time with this.
        public float Bpm => bpm;
        /// 0..1 across the configured rate range — drives volume, and anything
        /// else that wants to scale with dread.
        public float Intensity => config == null ? 0f
            : Mathf.Clamp01(Mathf.InverseLerp(config.calmBpm, config.nearBpm, bpm));
        /// True on the frame a beat fires.
        public bool BeatThisFrame { get; private set; }

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

        void Awake()
        {
            audioSource = GetComponent<AudioSource>();
            if (config != null) bpm = config.calmBpm;
        }

        void Start()
        {
            EventBus.Subscribe<TimeKiller.Hiding.PlayerHidEvent>(OnHid);
            EventBus.Subscribe<TimeKiller.Hiding.PlayerUnhidEvent>(OnUnhid);
            DebugOverlay.Watch("Heart", () => config == null
                ? "NO CONFIG"
                : $"{bpm:0} bpm  {(Intensity < config.silenceBelow ? "SILENT" : $"vol {Mathf.Lerp(config.quietVolume, 1f, Intensity) * config.maxVolume:0.00}")}" +
                  $"  beats {BeatCount}  world {duck:0.00}{(hidden ? "  (hidden)" : "")}");
        }

        void OnDestroy()
        {
            EventBus.Unsubscribe<TimeKiller.Hiding.PlayerHidEvent>(OnHid);
            EventBus.Unsubscribe<TimeKiller.Hiding.PlayerUnhidEvent>(OnUnhid);
            DebugOverlay.Unwatch("Heart");
            AudioDucking.SetWorld(1f);   // never leave the world muted behind us
        }

        void OnHid(TimeKiller.Hiding.PlayerHidEvent evt) => hidden = true;
        void OnUnhid(TimeKiller.Hiding.PlayerUnhidEvent evt) => hidden = false;

        void Update()
        {
            BeatThisFrame = false;
            if (config == null) return;
            float dt = Time.deltaTime;

            bool detected = false, suspicious = false;
            float target = TargetBpm(out detected, out suspicious);

            // Asymmetric: adrenaline arrives at once, it leaves slowly.
            float speed = target > bpm ? config.riseSpeed : config.fallSpeed;
            bpm = Mathf.Lerp(bpm, target, 1f - Mathf.Exp(-speed * dt));

            UpdateDuck(detected, dt);

            // The lurch: the chest kicks the instant he has you, before the rate
            // has had time to climb. One free beat, only on the transition.
            if (detected && !wasDetected && config.lurchOnDetected)
            {
                phase = 0f;
                nextBeatAt = 1f;
                Beat(force: true);
            }
            wasDetected = detected;

            if (Intensity < config.silenceBelow) { phase = 0f; nextBeatAt = 1f; return; }

            phase += (bpm / 60f) * dt;
            if (phase < nextBeatAt) return;
            phase -= nextBeatAt;
            nextBeatAt = 1f + Random.Range(-config.irregularity, config.irregularity);
            Beat(force: false);
        }

        void Beat(bool force)
        {
            BeatThisFrame = true;
            BeatCount++;
            if (audioSource == null || beatClip == null) return;
            // The lurch plays at full whack even if the rate has not caught up.
            // Otherwise ramp from quietVolume, NOT from zero: the faintest beat
            // the player is meant to hear must already be unmistakable.
            float loudness = force ? 1f : Mathf.Lerp(config.quietVolume, 1f, Intensity);
            float soften = Time.time < softenUntil ? softenFactor : 1f;
            float volume = Mathf.Clamp01(config.maxVolume * loudness * soften * (hidden ? config.hiddenBoost : 1f));
            // A racing heart is not just a faster calm one — it tightens and
            // rises. Set on the source before the one-shot, which inherits it.
            audioSource.pitch = Mathf.Lerp(1f, config.pitchAtMax, Intensity);

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
