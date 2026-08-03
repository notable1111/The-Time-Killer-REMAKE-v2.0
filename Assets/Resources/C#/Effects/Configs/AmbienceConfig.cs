// Tunables for the castle's ambient life — the dust in the air and the embers
// off the torches (Phase 5 of the VFX pass, 2026-08-03).
//
// This is the ONE place particles are the right tool rather than drawn sheets.
// Dust and embers are continuous, never-repeating, many-tiny-elements work with
// no readable shape, which is exactly the criterion written into EffectRecipe:
// drawn art for things the player LOOKS at, particles for dispersal.
//
// Art: Kenney Particle Pack (CC0, commercial use explicit). Those sprites
// measured value 223 / softness 0.75 against this game's 33 / 0.00, so they are
// tinted HARD downward here. They are white masks by design — the tint below is
// what actually sets the colour on screen, and it is the whole reason a pack
// that "fails" the style check is still correct for this job.
// Asset: C#/Effects/Configs/AmbienceConfig.asset.
using UnityEngine;

namespace TimeKiller.Effects
{
    [CreateAssetMenu(menuName = "TimeKiller/Configs/Ambience", fileName = "AmbienceConfig")]
    public class AmbienceConfig : ScriptableObject
    {
        [Header("Dust — the air itself")]
        [Tooltip("Particles alive at once across the visible area. Low: dust reads as an occasional catch of light, not as snow.")]
        [Range(0, 300)] public int dustCount = 90;
        [Tooltip("World size of the box the dust spawns in. Should comfortably exceed the camera view so motes drift in from off-screen rather than popping into existence.")]
        public Vector2 dustArea = new Vector2(16f, 11f);
        [Tooltip("Tint. Cool and DIM on purpose — the reference art sits at value 33, and a bright mote reads as a firefly.")]
        public Color dustTint = new Color(0.62f, 0.66f, 0.74f, 0.20f);
        [Range(0.005f, 0.12f)] public float dustSizeMin = 0.02f;
        [Range(0.005f, 0.25f)] public float dustSizeMax = 0.07f;
        [Tooltip("Drift speed. Barely moving: dust hangs, it does not fall.")]
        public float dustDrift = 0.18f;
        [Tooltip("Seconds a mote lives. Long, so the field looks settled rather than churning.")]
        public Vector2 dustLifetime = new Vector2(6f, 14f);

        [Header("Embers — off the torches")]
        [Tooltip("Embers per second from each torch.")]
        [Range(0f, 20f)] public float emberRate = 3.5f;
        [Tooltip("Tint. Warm, but kept dim — an ember is a dying scrap of fire, not a spark plug.")]
        public Color emberTint = new Color(0.95f, 0.48f, 0.18f, 0.75f);
        [Range(0.01f, 0.2f)] public float emberSizeMin = 0.025f;
        [Range(0.01f, 0.3f)] public float emberSizeMax = 0.06f;
        [Tooltip("Upward speed. Embers rise on the heat, then die.")]
        public float emberRise = 0.55f;
        [Tooltip("Sideways wander, so they do not rise in a straight column.")]
        public float emberWander = 0.25f;
        public Vector2 emberLifetime = new Vector2(0.9f, 2.2f);
        [Tooltip("How far from a torch embers spawn.")]
        public float emberRadius = 0.12f;

        [Header("Haze — the medium, not the specks")]
        // Dust alone reads as snow because specks are DISCRETE. Horror murk is a
        // medium: something thin and continuous between you and the wall, which
        // gives a room depth. This is that layer, and it does more for
        // atmosphere than any number of motes.
        [Tooltip("Huge, near-transparent smoke puffs drifting across the view. Few and enormous: this is a medium, not an effect.")]
        [Range(0, 40)] public int hazeCount = 14;
        [Tooltip("World size of a haze puff. Should be a decent fraction of the screen — small puffs read as smoke, big ones read as air.")]
        public Vector2 hazeSize = new Vector2(4.5f, 9f);
        [Tooltip("Tint. Alpha stays TINY — haze works by accumulating over several overlapping puffs, and any single one being visible means it is too strong.")]
        public Color hazeTint = new Color(0.42f, 0.47f, 0.55f, 0.055f);
        public float hazeDrift = 0.22f;
        public Vector2 hazeLifetime = new Vector2(9f, 18f);

        [Header("Light shafts — where the air becomes visible")]
        [Tooltip("A cone of lit air under each torch. This is what makes the torches feel like they are DOING something to the room rather than just being bright sprites.")]
        public bool shafts = true;
        [Tooltip("Tint. Warm and very faint — a shaft you notice as a shape is too strong; it should only be felt.")]
        public Color shaftTint = new Color(1f, 0.82f, 0.55f, 0.16f);
        [Tooltip("World width of a shaft at its widest (the floor end).")]
        public float shaftWidth = 2.6f;
        [Tooltip("World length, source to dissolve.")]
        public float shaftLength = 4.2f;
        [Tooltip("How much the shaft breathes, as a share of its alpha. Light through moving air is never perfectly steady.")]
        [Range(0f, 1f)] public float shaftFlicker = 0.35f;

        [Header("Sorting")]
        [Tooltip("Dust sits BELOW characters (they are at 0) but above the floor, so motes never obscure the player or the maniac.")]
        public int dustSortingOrder = -2;
        [Tooltip("Embers sit above characters: they come off wall torches, which are behind everything, and would be invisible underneath.")]
        public int emberSortingOrder = 6;
        [Tooltip("Haze sits ABOVE characters, because a medium you are standing inside must come between the camera and everything, including the player. Below the dust so motes read as being in front of the murk.")]
        public int hazeSortingOrder = 3;
        [Tooltip("Shafts sit just above the floor and below characters — light lying ON the ground, which the player then walks through.")]
        public int shaftSortingOrder = -1;
    }
}
