// How the maniac LOOKS — clips, cadence and tone, all in one place.
//
// Originally this only mapped movement to sprite clips. It now also carries the
// two presentation levers that turn his AI into something a player can read,
// because the alternative was hardcoding them in the driver and the project rule
// is that every tunable lives in a config.
//
// WHY THE TONE FIELDS EXIST. Measured 2026-08-04 off the sheets themselves:
// his albedo averages value 153/255 against the survivor's 48/255 — the same
// saturation, so it is not hue, he is simply 3.2x brighter than the character
// standing next to him. That is what made him read as pasted onto the castle
// rather than standing in it. `bodyTint` multiplies him back down; the lights
// use the Multiply blend style, so lowering his albedo lowers his final pixel
// value proportionally.
//
// WHY THE CADENCE/TONE ARE KEYED TO AWARENESS. ManiacVoice already moves his
// breath volume and pitch across Unaware/Suspicious/Detected. The sprite did
// not move at all, so the audio was telling the player something the picture
// contradicted. These fields mirror that component deliberately — same three
// levels, same blended transition — so the sound and the look agree.
//
// Anything here at its neutral value (tint white, multiplier 1, no attack clip)
// makes the driver behave exactly as it did before this pass.
using TimeKiller.Core;
using TimeKiller.Player; // FacingDirection — the shared 8-direction enum
using UnityEngine;

namespace TimeKiller.Maniac
{
    [CreateAssetMenu(menuName = "TimeKiller/Configs/Maniac Animation Set", fileName = "ManiacAnimationSet")]
    public class ManiacAnimationSet : ScriptableObject
    {
        [Header("Clips")]
        [Tooltip("Walk cycles indexed by (int)FacingDirection.")]
        public SpriteAnimationClip[] walk = new SpriteAnimationClip[8];
        [Tooltip("Standing clips indexed by (int)FacingDirection. See the breathing note below — these are no longer single frames.")]
        public SpriteAnimationClip[] idle = new SpriteAnimationClip[8];
        public SpriteAnimationClip death;

        [Tooltip("The lunge. Taken from the maranza pack's stage_three row 8 — a blood-soaked, red-eyed rearing pose that shipped with the pack and had never been wired to anything. AttackState had no visual at all before this.")]
        public SpriteAnimationClip attack;

        [Header("Playback")]
        [Tooltip("Playback speed never drops below this while moving.")]
        public float minAnimationSpeed = 0.4f;
        [Tooltip("Chase speed that maps to 1x walk-cycle playback (cycle fps is tuned for it).")]
        public float speedForNormalPlayback = 5.2f;

        [Header("Attack")]
        [Tooltip("Seconds the lunge pose holds before he returns to the walk cycle. Keep at or below ManiacConfig.attackRecoverySeconds (0.35 as shipped) or the pose outlives the state that caused it — he would still be rearing up while the AI is already back in Chase.")]
        public float attackHoldSeconds = 0.35f;

        // ---- Awareness-driven look. Mirrors ManiacVoice's three levels. ----

        [Header("Tone — the fix for 'he looks pasted in'")]
        [Tooltip("Multiplied into SpriteRenderer.color at all times. White = the raw sheet (mean value 153, against game art at ~48). Lowering this is what settles him into the castle. Cosmetic ONLY: ManiacPerception reads Light2D intensities in the world and never looks at this.")]
        public Color bodyTint = new Color(0.52f, 0.55f, 0.62f, 1f);
        [Tooltip("Extra multiplier while he is Suspicious — he is working, but has not found you.")]
        public Color suspiciousTint = new Color(1.04f, 1.02f, 1f, 1f);
        [Tooltip("Extra multiplier once he is Detected. Slightly warmer and brighter: the moment he commits should be legible at a glance, and it is the visual half of the breath that ManiacVoice already raises here.")]
        public Color detectedTint = new Color(1.18f, 1.08f, 1.02f, 1f);
        [Tooltip("Seconds to blend between tones. Snapping reads as a switch being thrown; blending reads as him getting interested. Same reasoning as ManiacVoice.breathBlendSeconds.")]
        public float toneBlendSeconds = 0.35f;

        [Header("Cadence — how his walk reads per awareness")]
        [Tooltip("Walk-cycle speed multiplier while Unaware. 1 = the velocity-matched speed the driver already computes.")]
        public float cadenceUnaware = 1f;
        [Tooltip("Multiplier while Suspicious. Below 1 = he slows and picks his way, which is what 'I heard something' should look like.")]
        public float cadenceSuspicious = 0.82f;
        [Tooltip("Multiplier while Detected. Above 1 = urgency on top of the speed he is already making.")]
        public float cadenceDetected = 1.25f;

        [Header("Idle breathing")]
        [Tooltip("Frames per second of the two-frame standing clip, so the full breath cycle is 2/fps seconds — 0.9 gives ~2.2s, about 27 breaths a minute. The breath is a ONE-PIXEL lift built from pivot-shifted slices of the same art, because at 32 PPU with Point filtering anything smaller than a whole pixel is shimmer, not motion.")]
        public float idleBreathFps = 0.9f;

        public SpriteAnimationClip GetWalk(FacingDirection direction) => walk[(int)direction];
        public SpriteAnimationClip GetIdle(FacingDirection direction) => idle[(int)direction];
    }
}
