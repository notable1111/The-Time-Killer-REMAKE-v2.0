// Plays an EffectRecipe anywhere with one call:
//   EffectPlayer.Play(recipe, worldPosition);
// Lazily builds its own runtime rig (audio source + Cinemachine impulse
// source) the first time it's needed — no scene setup required, survives
// scene rebuilds, cleared by GameBootstrap's domain-reset contract.
using Unity.Cinemachine;
using UnityEngine;

namespace TimeKiller.Effects
{
    public static class EffectPlayer
    {
        static AudioSource audioSource;
        static CinemachineImpulseSource impulse;

        public static void Play(EffectRecipe recipe, Vector2 worldPosition)
        {
            if (recipe == null) return;
            EnsureRig();

            if (recipe.particlePrefab != null)
            {
                var go = Object.Instantiate(recipe.particlePrefab, worldPosition, Quaternion.identity);
                go.transform.localScale *= recipe.particleScale;
                Object.Destroy(go, recipe.particleLifetime);
            }

            if (recipe.clips != null && recipe.clips.Length > 0 && audioSource != null)
            {
                audioSource.pitch = 1f + Random.Range(-recipe.pitchJitter, recipe.pitchJitter);
                audioSource.PlayOneShot(recipe.clips[Random.Range(0, recipe.clips.Length)], recipe.volume);
            }

            if (recipe.shakeStrength > 0f && impulse != null)
            {
                impulse.ImpulseDefinition.ImpulseDuration = recipe.shakeDuration;
                impulse.GenerateImpulse(new Vector3(recipe.shakeStrength, recipe.shakeStrength * 0.6f, 0f));
            }
        }

        static void EnsureRig()
        {
            if (audioSource != null) return;
            var rig = new GameObject("[EffectPlayer]");
            Object.DontDestroyOnLoad(rig);
            audioSource = rig.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0f;
            impulse = rig.AddComponent<CinemachineImpulseSource>();
        }
    }
}
