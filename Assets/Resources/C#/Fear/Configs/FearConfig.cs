// Every tunable of the fear system in one asset.
//
// THE MODEL: one continuous 0..1 fear value, built from proximity and MODIFIED
// by context, never stepped by it.
//
//   fear = curve(proximity) * awarenessMultiplier * closingMultiplier, then
//          held up by memory of how bad things recently were.
//
// The thing this replaces: the old heartbeat took a distance curve and clamped
// FLOORS under it (Suspicious = 108 bpm, Detected = 150). A maniac 30u away who
// happened to flip to Detected snapped the heart to 150 instantly — maximum
// panic for a threat that could not reach you. Multipliers cannot do that: a
// far-away enemy has near-zero proximity, and any multiple of near-zero is
// still near-zero. Being detected across the castle now feels like being
// detected across the castle.
using UnityEngine;

namespace TimeKiller.Fear
{
    [CreateAssetMenu(menuName = "TimeKiller/Configs/Fear", fileName = "FearConfig")]
    public class FearConfig : ScriptableObject
    {
        [Header("Threat distance — where fear begins and where it peaks")]
        [Tooltip("Outer radius. Beyond this the threat contributes nothing.\n\nThe old heartbeat used ~10.4u against a camera showing ~10.7u wide, so the maniac was literally on screen before your body reacted — the warning arrived after the information. This wants to be roughly 2.5-3x the camera width so unease starts while he is still off screen.")]
        public float fearStartDistance = 26f;
        [Tooltip("At or inside this distance proximity is 1.0 — he is on top of you. Should sit near his attackRange (0.9) plus a breath, not at it, or maximum fear only arrives after the swing.")]
        public float closeDangerDistance = 3f;
        [Tooltip("Shape of the climb from the outer radius (0) to close danger (1). Should build GENTLY far out, become clearly noticeable mid-range, and accelerate hard when he is close. A straight line reads as a status bar; the fear is in the change of rate, not the value.")]
        public AnimationCurve distanceCurve = new AnimationCurve(
            new Keyframe(0f, 0f, 0.35f, 0.35f),
            new Keyframe(0.5f, 0.32f, 1.1f, 1.1f),
            new Keyframe(1f, 1f, 2.2f, 2.2f));

        [Tooltip("Extra distance the threat must LEAVE before it stops counting, so a maniac loitering exactly on the boundary cannot start and stop the heartbeat repeatedly. Pure hysteresis: it widens the radius only once you are already afraid.")]
        public float exitHysteresis = 4f;

        [Header("Path awareness")]
        [Tooltip("Prefer navigation-path distance over straight-line, so a maniac on the far side of a wall is not treated as if he were beside you. Falls back to direct distance whenever the navigator has no route or is not ready — the fallback is never worse than the old behaviour.")]
        public bool usePathDistance = true;
        [Tooltip("Cap on how much longer the path may be than the straight line before we stop trusting it. A path 6x longer usually means the router went round the whole castle, and treating that as 'far' would make a maniac through a doorway feel safe.")]
        [Range(1f, 8f)] public float maxPathDetour = 3.5f;
        [Tooltip("Seconds between path queries. Fear does not need a route every frame and A* is ~0.2ms a call.")]
        public float pathInterval = 0.35f;

        [Header("Awareness — a MULTIPLIER, never a floor")]
        [Tooltip("Multiplier while he has no idea you exist. This is the baseline the curve was shaped for, so 1 = distance alone.")]
        [Range(0.1f, 3f)] public float unawareMultiplier = 0.85f;
        [Tooltip("Multiplier while his awareness meter is filling but has not crossed suspicion. The heart reacting BEFORE you know you were noticed is the most frightening beat in the system — you feel your body react, cannot find a cause, and invent a worse one.")]
        [Range(0.1f, 3f)] public float noticingMultiplier = 1f;
        [Tooltip("Multiplier while he is Suspicious — stopped, staring, not yet sure.")]
        [Range(0.1f, 3f)] public float suspiciousMultiplier = 1.1f;
        [Tooltip("Multiplier while he is actively Searching for you after losing sight. Just under Suspicious: a hunt is dread, a stare is worse, because a stare might already be about you.")]
        [Range(0.1f, 3f)] public float searchingMultiplier = 1.08f;
        [Tooltip("Multiplier while he has you — Detected or chasing. Note this MULTIPLIES proximity, so being seen from across the map is still not panic.\n\nMEASURED DOWN FROM 1.75 (2026-08-02). A recorded bot session showed the player spending 21.2s in the Panic band on the way up and only 3.5s in Threat — the build-up was being skipped. Cause: at 10u the distance curve gives 0.53, and 0.53 x 1.75 = 0.93, so being spotted at mid range slammed straight past Threat into deep Panic. That is the same 'unnatural jump' the fixed BPM floors used to cause, just wearing a multiplier. At 1.25 the same sighting lands at 0.66 (Threat) while 5u and closer still clamps to full Panic.")]
        [Range(0.1f, 3f)] public float detectedMultiplier = 1.25f;

        [Header("Closing speed — a nudge, not a driver")]
        [Tooltip("Extra multiplier when he is closing on you at speed. An enemy sprinting at you should feel worse than one standing still the same distance away — but only slightly, or fear becomes a speedometer.")]
        [Range(0f, 1f)] public float closingBoost = 0.22f;
        [Tooltip("Closing speed (units/sec) that earns the full boost. His chase speed is 5.2, so a little under that means a real charge counts.")]
        public float closingSpeedForFull = 4f;

        [Header("Smoothing — fear rises fast and falls slow")]
        [Tooltip("Seconds to approach a HIGHER fear during an ordinary approach. Long enough that walking toward him feels like a build, not a switch.")]
        [Range(0.05f, 5f)] public float riseSeconds = 1.4f;
        [Tooltip("Seconds to approach a higher fear during a SUDDEN spike — detection, or a fast closing rush. This is the adrenaline response and it should feel like a drop in the stomach.")]
        [Range(0.05f, 2f)] public float detectionRiseSeconds = 0.45f;
        [Tooltip("Rise faster than this much fear per second and the fast response takes over. Keeps a gentle approach gentle while still catching a genuine lurch.")]
        public float spikeThreshold = 0.55f;
        [Tooltip("Seconds fear HOLDS at its recent peak after the threat eases, before it starts falling at all. This is the 'am I actually safe?' beat and removing it removes most of the dread.")]
        public float recoveryDelaySeconds = 3.5f;
        [Tooltip("Seconds to fall from full panic to calm once the hold expires. Long on purpose: escaping should not feel safe for a while afterwards.")]
        public float recoverySeconds = 9f;

        [Header("Memory — recently-bad keeps fear up")]
        [Tooltip("Seconds a recent peak keeps propping fear up. Without this, breaking line of sight for one frame collapses the whole system and the player learns to treat corners as safety.")]
        public float memorySeconds = 8f;
        [Tooltip("Share of the remembered peak that survives as a floor while memory lasts. 0.5 = a full panic keeps you at 0.5 for a while even if he is gone.")]
        [Range(0f, 1f)] public float memoryFloorShare = 0.55f;

        [Header("Stage thresholds — display and cues only")]
        [Tooltip("Fear at or above which the stage reads Unease.")]
        [Range(0f, 1f)] public float uneaseAt = 0.12f;
        [Range(0f, 1f)] public float threatAt = 0.4f;
        [Range(0f, 1f)] public float panicAt = 0.72f;

        [Header("Heartbeat rate")]
        [Tooltip("BPM at fear 0. Below the audible gate, so it is silence you never hear reach zero.")]
        public float minBpm = 52f;
        [Tooltip("BPM at fear 1. Human maximum is ~200; this is terror, not exercise.")]
        public float maxBpm = 165f;
        [Tooltip("Rate across the fear range, as a share of min..max BPM. Keyed to the design target and VERIFIED against it: 0.15 -> 58, 0.35 -> 78, 0.60 -> 110, 0.80 -> 137, 1.00 -> 165. Tangents are the local slopes so the curve passes through those points without the wobble that flat-tangent keys produce.")]
        public AnimationCurve bpmCurve = new AnimationCurve(
            new Keyframe(0f, 0f, 0.35f, 0.35f),
            new Keyframe(0.15f, 0.053f, 0.62f, 0.62f),
            new Keyframe(0.35f, 0.230f, 1.00f, 1.00f),
            new Keyframe(0.60f, 0.510f, 1.16f, 1.16f),
            new Keyframe(0.80f, 0.750f, 1.22f, 1.22f),
            new Keyframe(1f, 1f, 1.25f, 1.25f));

        [Header("Heartbeat volume")]
        [Tooltip("Loudest the heart ever gets, before the mix trim.")]
        [Range(0f, 1f)] public float maxHeartVolume = 0.95f;
        [Tooltip("Volume across the fear range, as a share of maxHeartVolume. MUST start near zero — the old system began at 0.55-0.72, so the first audible beat arrived already loud and there was nowhere left to build to.")]
        public AnimationCurve heartVolumeCurve = new AnimationCurve(
            new Keyframe(0f, 0f, 0.47f, 0.47f),
            new Keyframe(0.15f, 0.07f, 0.68f, 0.68f),
            new Keyframe(0.35f, 0.25f, 0.85f, 0.85f),
            new Keyframe(0.60f, 0.45f, 1.02f, 1.02f),
            new Keyframe(0.80f, 0.70f, 1.38f, 1.38f),
            new Keyframe(1f, 1f, 1.5f, 1.5f));
        [Tooltip("Below this fear the heart is SILENT. A heartbeat that never stops stops being frightening.")]
        [Range(0f, 0.5f)] public float heartSilenceBelow = 0.06f;
        [Tooltip("Extra volume while hidden — in a wardrobe your own body is all you can hear, loud enough to feel like it will give you away.")]
        [Range(1f, 2f)] public float hiddenBoost = 1.25f;

        [Header("Heartbeat feel — biological, not a metronome")]
        [Tooltip("Beat-to-beat timing wobble (0.025 = 2.5%). Small. Large values read as a broken machine rather than a body.")]
        [Range(0f, 0.15f)] public float timingVariation = 0.028f;
        [Tooltip("Beat-to-beat volume wobble (0.03 = 3%).")]
        [Range(0f, 0.2f)] public float volumeVariation = 0.035f;

        [Header("Breathing")]
        [Tooltip("Fear below which fear-driven breathing is silent. Exertion breathing is separate and unaffected — sprinting in an empty castle must never start the fear layer.")]
        [Range(0f, 1f)] public float breathStartsAt = 0.3f;
        [Tooltip("Loudest the fear-driven breath layer gets.")]
        [Range(0f, 1f)] public float breathMaxVolume = 0.8f;

        [Header("Ambience ducking")]
        [Tooltip("How far the world recedes at maximum fear. 0.75 is roughly -2.5 dB — the spec's 1-3 dB. The old system pulled it to 0.08, which is effectively muting the level and takes the maniac's own footsteps with it.")]
        [Range(0f, 1f)] public float ambienceAtMaxFear = 0.75f;
        [Tooltip("Seconds for the world to recede.")]
        public float duckInSeconds = 1.2f;
        [Tooltip("Seconds for the world to return. Slower, so quiet lingers and you are not sure it is over.")]
        public float duckOutSeconds = 3.5f;

        [Header("Detection sting")]
        [Tooltip("Play a short sting when he genuinely commits to a hunt.")]
        public bool stingEnabled = true;
        [Tooltip("Minimum seconds between stings. His awareness flickers in and out of Detected constantly behind cover, so without this the sting machine-guns.")]
        public float stingCooldown = 12f;
        [Tooltip("Fear below which detection is not worth a sting at all — being clocked from the far side of the castle is not a jump scare.")]
        [Range(0f, 1f)] public float stingNeedsFear = 0.28f;
        [Range(0f, 1f)] public float stingVolume = 0.7f;

        [Header("Visual feedback")]
        [Tooltip("Master switch. The whole mechanic must work through audio alone with this off.")]
        public bool visualFearEnabled = true;
        [Tooltip("Strength of the fear vignette at maximum fear. Deliberately small — this is a suggestion at the edge of vision, not a red flash.")]
        [Range(0f, 0.5f)] public float visualFearVignette = 0.16f;
        [Tooltip("Strength of the per-beat vignette pulse at maximum fear.")]
        [Range(0f, 0.3f)] public float visualPulseIntensity = 0.06f;
        [Tooltip("Desaturation at maximum fear. Keep near zero unless you want the tunnel-vision look.")]
        [Range(0f, 1f)] public float visualDesaturation = 0.12f;

        [Header("Master")]
        [Tooltip("Scales every fear-driven effect at once, for quick A/B without retuning each curve. 0 disables the system's output while leaving it computing (and still visible in F1).")]
        [Range(0f, 1f)] public float fearEffectMultiplier = 1f;

        [Header("Debug")]
        [Tooltip("Ignore the world and drive fear straight from the slider below. The way to audition the entire safe->panic transition without moving the maniac.")]
        public bool debugOverrideFear;
        [Range(0f, 1f)] public float debugFear;
        [Tooltip("Draw the outer fear radius and close-danger radius around the player in the Scene view.")]
        public bool drawGizmos = true;
    }
}
