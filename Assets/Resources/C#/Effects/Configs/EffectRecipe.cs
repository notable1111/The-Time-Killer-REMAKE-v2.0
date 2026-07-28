// One juice moment as ONE asset: visuals + sound + camera shake, played by
// EffectPlayer.Play(recipe, worldPos). Adding an effect to the game = creating
// a recipe asset and calling Play — no new code per effect (the "Feel-lite"
// decision, 2026-07-22). Recipes live in C#/Effects/Configs/Recipes/.
//
// TWO visual paths (2026-07-28), either or both:
//   sheetClip      — a hand-drawn sprite-sheet burst in our locked art style.
//                    Use for anything the player LOOKS at: impacts, sparks, the
//                    clock waking up. Drawn art wins whenever the effect has a
//                    readable shape.
//   particlePrefab — a ParticleSystem. Still the right tool for continuous,
//                    never-repeating, many-tiny-elements work (embers, dust,
//                    fog), where a drawn loop would read as a repeating tile.
using TimeKiller.Core;
using UnityEngine;

namespace TimeKiller.Effects
{
    [CreateAssetMenu(menuName = "TimeKiller/Configs/Effect Recipe", fileName = "EffectRecipe")]
    public class EffectRecipe : ScriptableObject
    {
        [Header("World sprite sheet (hand-drawn)")]
        [Tooltip("Sprite-sheet burst spawned at the play position, auto-destroyed when it finishes. Built from PixelLab strips by Setup/38.")]
        public SpriteAnimationClip sheetClip;
        public float sheetScale = 1f;
        [Tooltip("Leave EMPTY for the default lit sprite material. Assign an UNLIT sprite material for anything that should GLOW in the dark (sparks, fire, the clock waking) — a lit VFX sprite is invisible in exactly the dark corners where it matters most.")]
        public Material sheetMaterial;
        [Tooltip("Sorting layer for the sheet.")]
        public string sheetSortingLayer = "Default";
        [Tooltip("5 = above the floor, below overhead geometry (matches BloodBurst).")]
        public int sheetSortingOrder = 5;
        [Tooltip("Mirror horizontally at random so repeat hits never look identical — the visual counterpart of pitchJitter.")]
        public bool randomFlip = true;
        [Tooltip("Random Z rotation. Right for blood and debris, WRONG for anything with a readable up (a rising soul, a spark shower).")]
        public bool randomRotation;

        [Header("World particles")]
        [Tooltip("Prefab spawned at the play position (auto-destroyed). CFXR prefabs drop straight in here.")]
        public GameObject particlePrefab;
        public float particleScale = 1f;

        [Header("Sound")]
        [Tooltip("One random clip plays per trigger.")]
        public AudioClip[] clips;
        [Range(0f, 1f)] public float volume = 0.85f;
        [Tooltip("Random pitch spread (0.08 = ±8%) so repeats never sound identical.")]
        public float pitchJitter = 0.08f;

        [Header("Camera shake")]
        [Tooltip("Cinemachine impulse strength (0 = no shake).")]
        public float shakeStrength = 0f;
        public float shakeDuration = 0.25f;

        [Header("Housekeeping")]
        [Tooltip("Seconds before the spawned particle object is destroyed. Also used for a LOOPING sheetClip — a one-shot sheet instead dies exactly when its last frame has played, so it can never be cut off mid-animation.")]
        public float particleLifetime = 3f;
    }
}
