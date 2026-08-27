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
using UnityEngine.Rendering.Universal;

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
        [Tooltip("Edge softness. A little goes a long way — a razor-sharp 2D shadow reads as a cut-out. Was 0.25 in the first shadow pass; raised because softness is half of the penumbra effect below.")]
        [Range(0f, 1f)] public float shadowSoftness = 0.5f;

        [Header("Penumbra — shadows soften as they stretch")]
        // A real shadow is crisp where the object meets the floor and dissolves
        // as it runs away from the light. Ours was equally sharp at the foot of a
        // pillar and four units off, which is one of the loudest cheap-vs-expensive
        // tells in 2D lighting. This costs no geometry and nothing at runtime: it
        // is a per-light value URP already supports and we had left at its default.
        [Tooltip("How fast softness grows with distance from the caster. URP's default is 0.5, which is where this project sat untouched. Higher = the shadow dissolves sooner as it stretches away.")]
        [Range(0f, 1f)] public float shadowSoftnessFalloff = 0.8f;

        [Header("Torch strength — the lever that makes shadows readable")]
        [Tooltip("Multiplies BOTH values below. 1.0 = exactly what is authored today, so a run at the default changes nothing at all. Raising it is UNTESTED — edit-mode renders cannot show its effect (see ShadowAudit.cs), so change it, enter Play, and judge with your own eye. Read the balance note at the top of this file first: it costs stealth.")]
        [Range(0.5f, 4f)] public float torchBoost = 1f;
        [Tooltip("The serialized Light2D.intensity on each torch — what the EDITOR viewport renders. Authored value: 1.0.")]
        public float torchLightIntensity = 1.0f;
        [Tooltip("FlickerLight2D.baseIntensity — what PLAY mode actually renders, because the flicker component overwrites Light2D.intensity every frame. Authored value: 1.1. These two differ in the project as shipped; that is not a mistake to 'fix' here without asking.")]
        public float torchFlickerBase = 1.1f;

        [Header("Volume — do objects shade themselves?")]
        // CastShadow means a prop shades the WORLD but never itself, so a barrel
        // is lit identically on the torch side and the far side and reads flat.
        // CastAndSelfShadow darkens the face turned away from the light.
        // Walls stay CastShadow deliberately: a wall self-shadowing its own face
        // darkens the very surface the torch is meant to be lighting.
        [Tooltip("Props: pillars, wardrobes, furniture, barrels, pots. CastAndSelfShadow is what gives them a lit side and a dark side.")]
        public ShadowCaster2D.ShadowCastingOptions propCasting = ShadowCaster2D.ShadowCastingOptions.CastAndSelfShadow;
        [Tooltip("Walls. Leave on CastShadow — a wall shading its own face fights the torch that is lighting it.")]
        public ShadowCaster2D.ShadowCastingOptions wallCasting = ShadowCaster2D.ShadowCastingOptions.CastShadow;
        [Tooltip("Player and maniac. CastAndSelfShadow gives a character a dark side too, but their colliders are small capsules so the effect is subtle.")]
        public ShadowCaster2D.ShadowCastingOptions characterCasting = ShadowCaster2D.ShadowCastingOptions.CastShadow;

        [Header("Breathing — shadows move with the flame")]
        // FlickerLight2D already wobbles each torch on layered Perlin noise, but
        // shadowSoftness was static, so the shadow's edge never changed even as
        // the flame guttered. This drives softness from the SAME noise value the
        // intensity uses — a separate component would drift out of step with the
        // flame it is supposed to belong to, which is worse than not doing it.
        [Tooltip("How far shadow softness wanders with the flame, as an absolute offset. 0 disables it entirely and FlickerLight2D behaves exactly as before.")]
        [Range(0f, 0.5f)] public float shadowBreathAmount = 0.18f;

        [Header("The maniac's shadow — a tell, not a physical truth")]
        // Deliberately stylised. His shadow is larger than his body so it reaches
        // your view before he does, which turns the shadow system from scenery
        // into information and gives hide-and-run an early warning that rewards
        // paying attention. It does hand the player real intel: he becomes a
        // little easier to avoid and a lot more frightening to be near.
        [Tooltip("How much wider his shadow reads than his 0.6u body. 1 = physically honest and easy to miss. 0 disables the dedicated caster entirely.")]
        [Range(0f, 4f)] public float maniacShadowScale = 1.8f;
        [Tooltip("Same for the player, so you can see your own shadow sweep a wall. 1 = honest size; the player's is deliberately NOT exaggerated, since the drama belongs to him.")]
        [Range(0f, 4f)] public float playerShadowScale = 1f;

        [Header("Which lights cast")]
        [Tooltip("Point lights whose GameObject name contains any of these will have shadows switched on. Global lights are excluded by URP itself — a global light has no position, so it cannot cast.")]
        public string[] castingLightNames = { "TorchLight", "CandleLight", "Glow", "Moonlight", "PlayerGlow" };
    }
}
