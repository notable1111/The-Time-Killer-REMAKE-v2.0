// How being SEEN and being SWUNG AT hit the player's vision.
//
// WHY THIS REPLACED TWO SPRITES. Both beats used to draw a picture on the floor:
// a red bloom for spotted, a steel crescent for the swing. The user's verdict,
// 2026-08-28: "looks like cheap, not feeling dangerous, not feels like the horror
// game effect... before adding something you should always check other best
// horror games and then decide."
//
// He was right, and the references agree. Outlast and Amnesia communicate threat
// by OBSCURING or DISTORTING what the player can see — the frame changes, the
// world does not gain a decal. Dead by Daylight, whose loop this game copies,
// keeps threat feedback on the frame and on the survivor's own body. A crescent
// arc is an action-game hit-spark; a soft glow is a mobile-game idiom. Neither is
// horror, and the coverage numbers that passed them (1.21% and 2.61% of screen)
// only ever proved they were VISIBLE — never that they were frightening.
//
// Nothing here draws anything. It pushes a transient shock into
// HealthVfxDirector, which is the single owner of the URP Volume. That
// single-owner rule is not tidiness: three oscillators driving one vignette is
// exactly what made the heartbeat read as mush a day earlier.
using UnityEngine;

namespace TimeKiller.Effects
{
    [CreateAssetMenu(menuName = "TimeKiller/Effects/Threat Vision Config", fileName = "ThreatVisionConfig")]
    public class ThreatVisionConfig : ScriptableObject
    {
        public const string ResourcesPath = "C#/Effects/Configs/ThreatVisionConfig";

        [Header("HE HAS SEEN YOU — the colour drains and the edges close in")]
        [Tooltip("Extra darkness clamping in from the frame edge. This is the 'obscuration' half of the horror convention: your view of the room gets smaller at the exact moment you need it most.")]
        [Range(0f, 1f)] public float spottedVignette = 0.45f;

        [Tooltip("How far the world drains toward grey, 0..1. Colour leaving is the clearest 'something is wrong with YOU, not with the room' signal a frame can give.")]
        [Range(0f, 1f)] public float spottedDesaturation = 0.72f;

        [Tooltip("Lens tearing at the edges. The 'distortion' half. Small — past about 0.4 it stops reading as dread and starts reading as a broken TV.")]
        [Range(0f, 1f)] public float spottedChromatic = 0.32f;

        [Tooltip("Seconds to reach full. Fast, because he has ALREADY seen you — but never 0, since an instant step is a blink and reads as a glitch rather than as fear.")]
        [Range(0f, 0.5f)] public float spottedAttack = 0.07f;

        [Tooltip("Seconds to fall away. Long: dread should outlast the moment. This is what makes being seen feel like a state you are now IN rather than an event that happened.")]
        [Range(0.1f, 4f)] public float spottedRelease = 1.35f;

        [Header("HE SWUNG AT YOU — the camera takes the hit")]
        [Tooltip("Harder and darker than being spotted: this is contact, not attention.")]
        [Range(0f, 1f)] public float swingVignette = 0.58f;

        [Tooltip("A near-total colour drop for an instant. The world going grey on impact is the oldest trick in screen-language for a blow landing.")]
        [Range(0f, 1f)] public float swingDesaturation = 0.9f;

        [Tooltip("Heavier tearing than spotted — the frame itself is rattled.")]
        [Range(0f, 1f)] public float swingChromatic = 0.5f;

        [Tooltip("Nearly instant. A blow has no swell; the only reason this is not 0 is that a single-frame step aliases badly at high frame rates.")]
        [Range(0f, 0.5f)] public float swingAttack = 0.025f;

        [Tooltip("Short. A hit should snap back fast, or it reads as damage-over-time rather than as a strike.")]
        [Range(0.1f, 4f)] public float swingRelease = 0.5f;

        [Header("Install")]
        [Tooltip("Off leaves the game exactly as it was — nothing subscribes, nothing renders. Deleting this asset uninstalls the feature entirely.")]
        public bool enabled = true;
    }
}
