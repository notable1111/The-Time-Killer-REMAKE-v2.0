// The screen-is-the-health-bar presenter, v2 (quality pass 2026-07-22).
// Listens to PlayerHealthChangedEvent / PlayerHitEvent on the bus and drives:
//   1. URP post stack — red Vignette + Chromatic Aberration + Film Grain +
//      desaturation, all scaled by band strength and heartbeat-pulsed
//   2. TWO fullscreen blood layers per band, pulsing out of phase with a
//      subtle scale-breath — the blood feels alive, not pasted
//   3. Hit: splatter flash that slams in at scale + Cinemachine camera shake
//   4. Heavy breathing loop at critical HP
// The pulse clock below is VISUAL ONLY. The audible heartbeat belongs to
// PlayerHeartbeat, which folds injury into one intensity alongside the maniac's
// awareness and distance — see C#/Heartbeat/PlayerHeartbeat.cs.
// Fully removable: delete the HealthVfx object and the game runs unchanged.
using TimeKiller.Core;
using TimeKiller.Player;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace TimeKiller.HealthVfx
{
    public class HealthVfxDirector : MonoBehaviour
    {
        [SerializeField] HealthVfxConfig config;
        [SerializeField] Volume volume;
        [SerializeField] Image bandImage;
        [SerializeField] CanvasGroup bandGroup;
        [SerializeField] Image bandImageB;
        [SerializeField] CanvasGroup bandGroupB;
        [SerializeField] Image flashImage;
        [SerializeField] CanvasGroup flashGroup;
        [SerializeField] AudioSource breathing;
        // Impact juice (world blood burst, splash SFX, camera shake, death
        // sting) moved to C#/Effects recipes — this director is screen-state only.
        [SerializeField] Sprite subtleSprite;
        [SerializeField] Sprite subtleSpriteB;
        [SerializeField] Sprite criticalSprite;
        [SerializeField] Sprite criticalSpriteB;

        Vignette vignette;
        ChromaticAberration chromatic;
        FilmGrain grain;
        ColorAdjustments colorAdjust;

        float bandStrength, targetStrength;
        float targetVignette, targetOverlay, targetChromatic, targetGrain, targetDesat;
        // Fear's visual share, pre-scaled by FearConductor. Zero when no
        // conductor is running or fear visuals are switched off, which is why
        // nothing below needs a null check or a feature flag.
        float fearVignette, fearPulse, fearDesaturation;
        float bpm, pulseDepth, pulsePhase;
        float breathingTarget;

        // Driven by HeartbeatPulseEvent. The screen used to throb on its OWN
        // 74/118bpm clock off health while the heart ran 56-160 off the maniac,
        // so the two were never once in sync. Now the beat resets this phase and
        // the dread term below rides the real rate — two channels landing on the
        // same instant is what makes a heartbeat FELT rather than merely heard
        // (James-Lange: the player attributes the racing heart to their own fear).
        float heartIntensity;
        // Beat envelope. `beatAge` is seconds since the last thump; the envelope is
        // an eased swell then an exponential fall, computed in Update. This replaced
        // a `heartFlash` that snapped to 1 and fell in a STRAIGHT LINE — an instant
        // attack plus a linear decay is a sawtooth, and a sawtooth is what made the
        // whole effect read as a blinking light rather than a pulse (user, 2026-08-27,
        // the first day this was ever visible: "it looks cheap").
        float beatAge = 999f;
        float beatStrength;    // 1, or more for a palpitation

        public void Init(HealthVfxConfig vfxConfig) => config = vfxConfig;

        void Start()
        {
            if (volume != null && volume.profile != null)
            {
                volume.profile.TryGet(out vignette);
                volume.profile.TryGet(out chromatic);
                volume.profile.TryGet(out grain);
                volume.profile.TryGet(out colorAdjust);
            }
            if (vignette != null)
            {
                vignette.color.overrideState = true;
                vignette.color.value = config.vignetteColor;
                vignette.intensity.overrideState = true;
                vignette.intensity.value = 0f;
            }
            bandGroup.alpha = 0f;
            if (bandGroupB != null) bandGroupB.alpha = 0f;
            flashGroup.alpha = 0f;
            EventBus.Subscribe<PlayerHealthChangedEvent>(OnHealthChanged);
            EventBus.Subscribe<PlayerHitEvent>(OnHit);
            EventBus.Subscribe<TimeKiller.Heartbeat.HeartbeatPulseEvent>(OnHeartbeat);
            EventBus.Subscribe<TimeKiller.Fear.FearChangedEvent>(OnFearChanged);
        }

        void OnDestroy()
        {
            EventBus.Unsubscribe<PlayerHealthChangedEvent>(OnHealthChanged);
            EventBus.Unsubscribe<PlayerHitEvent>(OnHit);
            EventBus.Unsubscribe<TimeKiller.Heartbeat.HeartbeatPulseEvent>(OnHeartbeat);
            EventBus.Unsubscribe<TimeKiller.Fear.FearChangedEvent>(OnFearChanged);
        }

        void OnHealthChanged(PlayerHealthChangedEvent evt)
        {
            if (evt.Current <= config.criticalAtHp)
            {
                SetBand(criticalSprite, criticalSpriteB, config.criticalVignette, config.criticalOverlayAlpha,
                    config.criticalBpm, config.criticalPulseDepth, config.criticalChromatic,
                    config.criticalGrain, config.criticalDesaturation);
                breathingTarget = config.breathingVolume;
            }
            else if (evt.Current <= config.subtleAtHp)
            {
                SetBand(subtleSprite, subtleSpriteB, config.subtleVignette, config.subtleOverlayAlpha,
                    config.subtleBpm, config.subtlePulseDepth, config.subtleChromatic,
                    config.subtleGrain, 0f);
                breathingTarget = 0f;
            }
            else
            {
                targetStrength = 0f;
                breathingTarget = 0f;
            }
        }

        /// The heart beat. Snap the screen's pulse phase to it so the throb and
        /// the thump land together, and kick a decaying flash the dread vignette
        /// rides. A palpitation's thud hits harder — that beat is the one the
        /// player feels in their throat.
        void OnFearChanged(TimeKiller.Fear.FearChangedEvent evt)
        {
            fearVignette = evt.VisualVignette;
            fearPulse = evt.VisualPulse;
            fearDesaturation = evt.VisualDesaturation;
        }

        void OnHeartbeat(TimeKiller.Heartbeat.HeartbeatPulseEvent evt)
        {
            heartIntensity = evt.Intensity;
            beatAge = 0f;
            beatStrength = evt.Palpitation ? 1.35f : 1f;
            // The visual systole sits at phase x.5 (see Beat below), so putting
            // the phase half a cycle back puts the peak ON the sound.
            pulsePhase = Mathf.Floor(pulsePhase) + 0.5f;
            if (evt.Bpm > 1f) bpm = evt.Bpm;
        }

        void SetBand(Sprite spriteA, Sprite spriteB, float vig, float overlay, float beatsPerMinute, float depth,
            float chroma, float grainAmount, float desat)
        {
            bandImage.sprite = spriteA;
            if (bandImageB != null && spriteB != null) bandImageB.sprite = spriteB;
            targetVignette = vig;
            targetOverlay = overlay;
            bpm = beatsPerMinute;
            pulseDepth = depth;
            targetChromatic = chroma;
            targetGrain = grainAmount;
            targetDesat = desat;
            targetStrength = 1f;
        }

        void OnHit(PlayerHitEvent evt)
        {
            flashGroup.alpha = config.hitFlashAlpha;
            if (flashImage != null)
                flashImage.rectTransform.localScale = Vector3.one * config.hitFlashPunchScale;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            bandStrength = Mathf.Lerp(bandStrength, targetStrength, 1f - Mathf.Exp(-config.transitionSharpness * dt));
            float s = bandStrength;

            // The dread vignette. The blood bands are health's business and show
            // nothing at 3 HP — but a hunted player at full health still needs to
            // SEE the heart they can hear, or the sound is the only channel and
            // reads as an audio cue rather than as their own body. Scaled by the
            // heart's own intensity and punched on each beat, so it is invisible
            // when calm and unmistakable when he is closing.
            // ONE rhythm owns the frame edge. Everything that pulses on it rides this
            // single envelope. Before, THREE oscillators drove the same vignette at
            // once — a |sin|^3 throb on the injury band, a sawtooth punch from the
            // heart, and a third shape on fear — so the edge was asked to do three
            // things on one beat and the result read as mush.
            beatAge += dt;
            float env = BeatEnvelope(beatAge) * beatStrength;
            float heartVig = config.heartVignette * heartIntensity * env;

            // Heartbeat: sharp systole, slow diastole (|sin|^3), layer B counter-beats.
            pulsePhase += (bpm / 60f) * dt;
            float beatA = Beat(pulsePhase);
            float beatB = Beat(pulsePhase + config.layerBPhase);
            float pulseA = 1f - pulseDepth * (1f - beatA);
            float pulseB = 1f - pulseDepth * (1f - beatB);

            // FEAR, on top of health. These arrive pre-scaled from FearConductor
            // (already through visualFearEnabled and the master multiplier), so
            // there is nothing to honour here and no config of theirs to read —
            // with fear visuals off they are simply zero. Health owns the blood
            // bands; fear owns this quiet tightening at the edge of the frame.
            // Fear rides the SAME envelope as the heart rather than carrying a shape
            // of its own. fearPulse still says how much of fear's vignette is allowed
            // to breathe; it just breathes on the one beat now.
            float fearVig = fearVignette * (1f + config.fearBeatLift * fearPulse * env);
            float fearDesat = -100f * fearDesaturation;   // saturation is -100..100

            if (vignette != null)
            {
                float damageVig = targetVignette * s * (1f + config.damageBeatLift * env);
                float totalVig = damageVig + heartVig + fearVig;
                vignette.intensity.value = totalVig;

                // Red belongs to being HURT. Dread and fear darken instead, so a hunted
                // player at full health gets the corners closing in rather than a red
                // screen for no reason they could name — which is what a shared colour
                // was doing, and why an unhurt player saw a damage filter.
                float redShare = totalVig > 0.0001f ? Mathf.Clamp01(damageVig / totalVig) : 0f;
                vignette.color.overrideState = true;
                vignette.color.value = Color.Lerp(config.dreadVignetteColor, config.vignetteColor, redShare);

                // Soften as it deepens. A vignette gives itself away when the oval
                // hardens at peak, so the edge gets softer exactly when it gets darker.
                vignette.smoothness.overrideState = true;
                vignette.smoothness.value = Mathf.Lerp(config.vignetteSmoothnessBase,
                                                       config.vignetteSmoothnessPeak,
                                                       Mathf.Clamp01(totalVig / 0.6f));
            }
            if (chromatic != null) { chromatic.intensity.overrideState = true; chromatic.intensity.value = targetChromatic * s * pulseA; }
            if (grain != null) { grain.intensity.overrideState = true; grain.intensity.value = targetGrain * s; }
            if (colorAdjust != null)
            {
                colorAdjust.saturation.overrideState = true;
                // Both pull the same direction (toward grey), so take whichever is
                // stronger rather than summing — stacking them would drain the
                // colour out of the screen entirely at low health during a chase.
                colorAdjust.saturation.value = Mathf.Min(targetDesat * s, fearDesat);
            }

            bandGroup.alpha = targetOverlay * s * pulseA;
            bandImage.rectTransform.localScale = Vector3.one * (1f + config.scalePulse * beatA * s);
            if (bandGroupB != null)
            {
                bandGroupB.alpha = targetOverlay * config.layerBWeight * s * pulseB;
                bandImageB.rectTransform.localScale = Vector3.one * (1f + config.scalePulse * beatB * s);
            }

            if (flashGroup.alpha > 0f)
            {
                flashGroup.alpha = Mathf.MoveTowards(flashGroup.alpha, 0f, dt * config.hitFlashAlpha / config.hitFlashFade);
                if (flashImage != null)
                    flashImage.rectTransform.localScale = Vector3.Lerp(
                        flashImage.rectTransform.localScale, Vector3.one, 1f - Mathf.Exp(-10f * dt));
            }

            // Breathing now belongs to PlayerBreathing, which drives it from fear
            // rather than only from injury and can hold it while you hide. Two
            // breath loops running at different rates was the same mistake the
            // heartbeat made before it was consolidated, so this one stands down
            // and simply makes sure its old source is not left audible.
            if (breathing != null && breathing.isPlaying)
            {
                breathing.volume = Mathf.MoveTowards(breathing.volume, 0f, dt);
                if (breathing.volume <= 0.001f) breathing.Stop();
            }
        }

        static float Beat(float phase)
        {
            float w = Mathf.Abs(Mathf.Sin(phase * Mathf.PI));
            return w * w * w;
        }

        /// One beat's shape for the frame edge: an eased swell to full, then an
        /// exponential fall.
        ///
        /// The swell matters as much as the fall. A body has no instant edges, and
        /// the version this replaced jumped to full in a single frame and then fell
        /// in a straight line — which is precisely what a blinking light does, and
        /// why the effect read as cheap the first day it was ever visible.
        ///
        /// `heartFlashFade` stays the time to fall to roughly 5%, so the knob keeps
        /// meaning what its name and tooltip say it means.
        float BeatEnvelope(float age)
        {
            float attack = Mathf.Max(0.001f, config.heartVignetteAttack);
            if (age < attack)
            {
                float t = age / attack;
                return t * t * (3f - 2f * t);          // smoothstep: no corner at either end
            }
            float tau = Mathf.Max(0.02f, config.heartFlashFade) / 3f;
            return Mathf.Exp(-(age - attack) / tau);
        }
    }
}
