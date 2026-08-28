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
        [Tooltip("Blood at 2 of 3 HP. Lowered 0.35 -> 0.18 on 2026-08-28 by the user: bleeding heavily at the LAST point of health is right, but this band fires while he still has two, and it was bleeding almost as hard. The band should say 'you are hurt', not 'you are dying' — the critical band below is what says dying, and it needs somewhere louder to go.")]
        [Range(0f, 1f)] public float subtleOverlayAlpha = 0.18f;
        [Tooltip("Blood at 1 HP — the next hit kills. Deliberately left loud: this is the one that should frighten.")]
        [Range(0f, 1f)] public float criticalOverlayAlpha = 0.85f;

        [Header("Dread vignette — the heartbeat you can SEE")]
        [Tooltip("Extra vignette added on each heartbeat, scaled by the heart's own intensity. Works at ANY health, unlike the blood bands below, because a hunted player at full HP still needs to see the heart they can hear — otherwise the sound is a lone channel and reads as an audio cue rather than as their own body. 0 switches it off. Lowered 0.22 -> 0.10 on 2026-08-27: the first time this was ever visible it read as an effect rather than as a body.")]
        [Range(0f, 0.6f)] public float heartVignette = 0.10f;

        [Tooltip("Seconds for the beat to SWELL IN. 0 is an instant snap, which is what made this read as a blinking light rather than a pulse — nothing in a body moves instantly. Keep it short enough to still land on the thump.")]
        [Range(0f, 0.4f)] public float heartVignetteAttack = 0.10f;

        [Tooltip("Seconds for one beat to fall away. The fall is EXPONENTIAL, not a straight line — a linear ramp is the single loudest 'cheap' tell in a pulsing effect.")]
        [Range(0.05f, 1.5f)] public float heartFlashFade = 0.55f;

        [Tooltip("Colour the edge darkens toward when the pulse is DREAD rather than injury. Near-black and slightly cool, so an unhurt but hunted player gets darkness closing in — not a red screen. Red is reserved for actually being hurt.")]
        public Color dreadVignetteColor = new Color(0.04f, 0.04f, 0.07f);

        [Header("Vignette edge — soft enough not to read as a ring")]
        [Tooltip("Edge softness at rest. Low values draw a visible oval sitting on top of the picture; high values read as darkness gathering in the corners.")]
        [Range(0.3f, 1f)] public float vignetteSmoothnessBase = 0.85f;

        [Tooltip("Edge softness at full strength. Held HIGHER than the base so the ring never hardens as it deepens, which is when a vignette normally gives itself away.")]
        [Range(0.3f, 1f)] public float vignetteSmoothnessPeak = 0.95f;

        [Tooltip("How much the injury vignette swells on each beat. Small: the wound should breathe, not flash.")]
        [Range(0f, 0.5f)] public float damageBeatLift = 0.12f;

        [Tooltip("How much the fear vignette swells on each beat. Fear owns the quiet tightening at the frame edge; this is the only rhythm it is allowed to ride, so the edge never carries two competing beats at once.")]
        [Range(0f, 0.5f)] public float fearBeatLift = 0.15f;

        [Header("Screen pulse (visual throb only — the SOUND lives in HeartbeatConfig)")]
        [Tooltip("Beats per minute at 2 HP. Note the heartbeat OVERRIDES this at runtime once a pulse arrives, so the screen and the chest stay in step; these remain the fallback when no heartbeat exists in the scene.")]
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

        [Tooltip("How much the blood RECEDES while a dread shock is on, 0..1. Measured in the user's own recording 2026-08-28: being spotted correctly drained screen colour to 7.3, but once he was hurt the blood pushed colour to 35-60 against a calm baseline of 12 — so the injury overlay completely buried the dread effect at the exact moment it mattered. The two were fighting: dread drains colour, blood floods it. Blood now steps back while he is being hunted and returns when the moment passes. 0 restores the old fight.")]
        [Range(0f, 1f)] public float bloodRecedesUnderDread = 0.7f;

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
