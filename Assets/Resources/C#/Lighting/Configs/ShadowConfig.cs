// Tunables for URP 2D shadow casting (Setup/49).
//
// WHY THIS FEATURE EXISTS. Measured 2026-08-03 in CastleWingLDtk: the scene had
// ShadowCaster2D count = 0 and every Light2D had shadowsEnabled = false. Light
// passed through every wall, pillar and wardrobe as if they were not there, so a
// torch lit both sides of a wall equally and a pillar standing directly under one
// threw nothing. That absence is most of what made the room read as flat — a lit
// backdrop with sprites laid on top rather than a room with things in it.
//
// WHAT IS AND IS NOT VERIFIED HERE. Verified by reading scene data: the zero
// casters above, and that all 25 Light2D sit on the Multiply blend style. NOT
// verified: how strongly the shadows read on screen. Edit-mode renders in this
// project do not reflect Light2D property changes at all — see the header of
// ShadowAudit.cs for the four measurements that establish that — so any
// percentage claimed from an editor screenshot would be fiction. Judge the
// contrast in Play, and treat `torchBoost` as an untested lever until you have.
//
// WHY THE LEVER IS THE TORCHES AND NOT THE AMBIENT. Dimming GlobalLight would
// also deepen shadows, but it is NOT a safe change, and this part IS verified —
// by reading the code rather than a render:
// ManiacPerception.Exposure() starts from `config.ambientExposure`, a CONSTANT —
// it never reads GlobalLight. Dimming the ambient therefore darkens the player's
// screen while leaving the maniac's stealth model untouched, so the player reads
// "I am in deep shadow" and gets spotted anyway. That is the same desync
// BrightnessSettings.cs was written to forbid, and it is why brightness there is
// post-process only. Point-light intensity IS read (Exposure() line 447), so
// raising the torches keeps the player's view and the maniac's model in step.
//
// BALANCE CONSEQUENCE of raising torchBoost, stated because it is a gameplay
// change and not just a look: the radius around a torch at which the player is
// FULLY exposed grows with intensity. At the shipped 1.1 that radius is ~0.6u;
// at boost 2.0 it is ~2.5u. Torch-lit ground becomes genuinely dangerous. That
// is the direction the design already intends ("torchlight exposes, shadow
// hides") but it is the user's call, which is why the default here is 1.0.
using UnityEngine;

namespace TimeKiller.Lighting
{
    [CreateAssetMenu(menuName = "TimeKiller/Configs/Shadows", fileName = "ShadowConfig")]
    public class ShadowConfig : ScriptableObject
    {
        [Header("Who casts a shadow")]
        [Tooltip("The two Collision/IntGrid composites — the actual walls. This is the one that matters: without it a torch lights straight through a wall.")]
        public bool walls = true;
        [Tooltip("Hand-placed props: pillars, wardrobes, furniture, barrels, pots, clocks. Their colliders sit at the base of the sprite, so the shadow leaves the foot of the object, which is what a floor shadow should do.")]
        public bool props = true;
        [Tooltip("Player and maniac. Turning this on means your own shadow sweeps the wall as you pass a torch — and so does his, which is a genuine tell.")]
        public bool characters = true;

        [Tooltip("Never cast from an object whose name is in this list, whatever else matches. CameraBounds is here because it is a 66x52 trigger volume that would shadow the entire map.")]
        public string[] neverCast = { "CameraBounds" };

        [Header("How the shadow looks")]
        [Tooltip("How much light the shadow removes. 1 = the caster blocks its light completely.")]
        [Range(0f, 1f)] public float shadowIntensity = 1f;
        [Tooltip("Edge softness. A little goes a long way — a razor-sharp 2D shadow reads as a cut-out.")]
        [Range(0f, 1f)] public float shadowSoftness = 0.25f;

        [Header("Torch strength — the lever that makes shadows readable")]
        [Tooltip("Multiplies BOTH values below. 1.0 = exactly what is authored today, so a run at the default changes nothing at all. Raising it is UNTESTED — edit-mode renders cannot show its effect (see ShadowAudit.cs), so change it, enter Play, and judge with your own eye. Read the balance note at the top of this file first: it costs stealth.")]
        [Range(0.5f, 4f)] public float torchBoost = 1f;
        [Tooltip("The serialized Light2D.intensity on each torch — what the EDITOR viewport renders. Authored value: 1.0.")]
        public float torchLightIntensity = 1.0f;
        [Tooltip("FlickerLight2D.baseIntensity — what PLAY mode actually renders, because the flicker component overwrites Light2D.intensity every frame. Authored value: 1.1. These two differ in the project as shipped; that is not a mistake to 'fix' here without asking.")]
        public float torchFlickerBase = 1.1f;

        [Header("Which lights cast")]
        [Tooltip("Point lights whose GameObject name contains any of these will have shadows switched on. Global lights are excluded by URP itself — a global light has no position, so it cannot cast.")]
        public string[] castingLightNames = { "TorchLight", "CandleLight", "Glow", "Moonlight", "PlayerGlow" };
    }
}
