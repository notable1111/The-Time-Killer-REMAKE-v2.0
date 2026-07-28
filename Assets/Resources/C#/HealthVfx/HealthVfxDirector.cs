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
        float bpm, pulseDepth, pulsePhase;
        float breathingTarget;

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
        }

        void OnDestroy()
        {
            EventBus.Unsubscribe<PlayerHealthChangedEvent>(OnHealthChanged);
            EventBus.Unsubscribe<PlayerHitEvent>(OnHit);
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

            // Heartbeat: sharp systole, slow diastole (|sin|^3), layer B counter-beats.
            pulsePhase += (bpm / 60f) * dt;
            float beatA = Beat(pulsePhase);
            float beatB = Beat(pulsePhase + config.layerBPhase);
            float pulseA = 1f - pulseDepth * (1f - beatA);
            float pulseB = 1f - pulseDepth * (1f - beatB);

            if (vignette != null) vignette.intensity.value = targetVignette * s * pulseA;
            if (chromatic != null) { chromatic.intensity.overrideState = true; chromatic.intensity.value = targetChromatic * s * pulseA; }
            if (grain != null) { grain.intensity.overrideState = true; grain.intensity.value = targetGrain * s; }
            if (colorAdjust != null) { colorAdjust.saturation.overrideState = true; colorAdjust.saturation.value = targetDesat * s; }

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
    }
}
