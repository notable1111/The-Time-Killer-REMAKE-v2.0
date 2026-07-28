// Tunables for the diegetic health presentation (design 2026-07-22: no health
// UI — the screen IS the health bar). Bands on the 3-HP system:
//   3 HP: clean   2 HP: subtle red pulse   1 HP: heavy red + blood + breathing
// Asset: C#/HealthVfx/Configs/HealthVfxConfig.asset.
using UnityEngine;

namespace TimeKiller.HealthVfx
{
    [CreateAssetMenu(menuName = "TimeKiller/Configs/Health VFX", fileName = "HealthVfxConfig")]
    public class HealthVfxConfig : ScriptableObject
    {
        [Header("Bands (HP thresholds, inclusive)")]
        [Tooltip("At or below this HP the subtle band shows (2 of 3).")]
        public int subtleAtHp = 2;
        [Tooltip("At or below this HP the critical band shows (1 of 3 = next hit kills).")]
        public int criticalAtHp = 1;

        [Header("Vignette (URP Volume)")]
        public Color vignetteColor = new Color(0.45f, 0f, 0f);
        [Range(0f, 1f)] public float subtleVignette = 0.28f;
        [Range(0f, 1f)] public float criticalVignette = 0.47f;

        [Header("Blood overlay")]
        [Range(0f, 1f)] public float subtleOverlayAlpha = 0.35f;
        [Range(0f, 1f)] public float criticalOverlayAlpha = 0.85f;

        [Header("Screen pulse (visual throb only — the SOUND lives in HeartbeatConfig)")]
        [Tooltip("Beats per minute at 2 HP — an elevated but steady heart.")]
        public float subtleBpm = 74f;
        [Tooltip("BPM at 1 HP — tachycardia, a panicking heart.")]
        public float criticalBpm = 118f;
        [Range(0f, 1f), Tooltip("How much of the effect strength the pulse modulates.")]
        public float subtlePulseDepth = 0.18f;
        [Range(0f, 1f)] public float criticalPulseDepth = 0.35f;

        [Header("Damage sounds")]
        [Range(0f, 1f)] public float hitSoundVolume = 0.85f;
        [Tooltip("Random pitch spread per hit (0.08 = ±8%).")]
        public float hitPitchJitter = 0.08f;
        [Range(0f, 1f)] public float deathSoundVolume = 0.9f;

        [Header("Living motion")]
        [Tooltip("How much the blood layers scale-breathe with the heartbeat (0.03 = 3%).")]
        public float scalePulse = 0.03f;
        [Tooltip("Phase offset of the second blood layer in beats (0.5 = counter-beat).")]
        public float layerBPhase = 0.45f;
        [Range(0f, 1f), Tooltip("Second layer's alpha relative to the first.")]
        public float layerBWeight = 0.7f;

        [Header("Post-processing juice")]
        [Range(0f, 1f)] public float subtleChromatic = 0.15f;
        [Range(0f, 1f)] public float criticalChromatic = 0.45f;
        [Range(0f, 1f)] public float subtleGrain = 0.15f;
        [Range(0f, 1f)] public float criticalGrain = 0.35f;
        [Tooltip("Color saturation drained at critical (negative = grayer world).")]
        public float criticalDesaturation = -35f;

        [Header("Hit flash")]
        [Range(0f, 1f)] public float hitFlashAlpha = 0.9f;
        [Tooltip("Seconds for the splatter flash to fade out.")]
        public float hitFlashFade = 0.6f;
        [Tooltip("Splatter slams in from this scale down to 1.")]
        public float hitFlashPunchScale = 1.18f;
        [Tooltip("Camera shake impulse strength on hit.")]
        public float hitShake = 0.35f;

        [Header("Breathing (critical only)")]
        [Range(0f, 1f)] public float breathingVolume = 0.75f;
        [Tooltip("Seconds to fade breathing in/out when crossing the band.")]
        public float breathingFade = 1.2f;

        [Header("Transitions")]
        [Tooltip("Higher = snappier band transitions (exponential damp).")]
        public float transitionSharpness = 5f;
    }
}
