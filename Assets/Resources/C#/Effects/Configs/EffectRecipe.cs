// One juice moment as ONE asset: particles + sound + camera shake, played by
// EffectPlayer.Play(recipe, worldPos). Adding an effect to the game = creating
// a recipe asset and calling Play — no new code per effect (the "Feel-lite"
// decision, 2026-07-22). Recipes live in C#/Effects/Configs/Recipes/.
using UnityEngine;

namespace TimeKiller.Effects
{
    [CreateAssetMenu(menuName = "TimeKiller/Configs/Effect Recipe", fileName = "EffectRecipe")]
    public class EffectRecipe : ScriptableObject
    {
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
        [Tooltip("Seconds before the spawned particle object is destroyed.")]
        public float particleLifetime = 3f;
    }
}
