// Plays an EffectRecipe anywhere with one call:
//   EffectPlayer.Play(recipe, worldPosition);
// Lazily builds its own runtime rig (audio source + Cinemachine impulse
// source) the first time it's needed — no scene setup required, survives
// scene rebuilds, cleared by GameBootstrap's domain-reset contract.
using TimeKiller.Core;
using Unity.Cinemachine;
using UnityEngine;

namespace TimeKiller.Effects
{
    public static class EffectPlayer
    {
        static AudioSource audioSource;
        static CinemachineImpulseSource impulse;

        public static void Play(EffectRecipe recipe, Vector2 worldPosition)
            => Play(recipe, worldPosition, Vector2.zero);

        /// direction: which way the effect should throw itself — for a hit, away
        /// from the blow. Vector2.zero means "no opinion" and the effect plays
        /// unrotated. Aiming matters because a symmetric burst reads as an
        /// explosion centred on the victim rather than as a wound.
        public static void Play(EffectRecipe recipe, Vector2 worldPosition, Vector2 direction)
        {
            if (recipe == null) return;
            EnsureRig();

            PlaySheet(recipe, worldPosition);

            if (recipe.particlePrefab != null)
            {
                var go = Object.Instantiate(recipe.particlePrefab, worldPosition, AimRotation(recipe, direction));
                go.transform.localScale *= recipe.particleScale;
                Object.Destroy(go, recipe.particleLifetime);
            }

            if (recipe.clips != null && recipe.clips.Length > 0 && audioSource != null)
            {
                audioSource.pitch = 1f + Random.Range(-recipe.pitchJitter, recipe.pitchJitter);
                audioSource.PlayOneShot(recipe.clips[Random.Range(0, recipe.clips.Length)],
                    recipe.volume * TimeKiller.Audio.AudioMix.GainFor(TimeKiller.Audio.MixChannel.Effects));
            }

            if (recipe.shakeStrength > 0f && impulse != null)
            {
                impulse.ImpulseDefinition.ImpulseDuration = recipe.shakeDuration;
                impulse.GenerateImpulse(new Vector3(recipe.shakeStrength, recipe.shakeStrength * 0.6f, 0f));
            }
        }

        /// Rotates the burst so its emission WEDGE is centred on `direction`.
        /// The wedge width is read straight off the prefab's own shape module
        /// rather than duplicated onto the recipe, so the two can never drift
        /// out of sync. A full 360° emitter is symmetric, so aiming it would do
        /// nothing and we skip the work.
        static Quaternion AimRotation(EffectRecipe recipe, Vector2 direction)
        {
            if (direction.sqrMagnitude < 0.0001f || recipe.particlePrefab == null)
                return Quaternion.identity;

            var ps = recipe.particlePrefab.GetComponent<ParticleSystem>();
            if (ps == null) return Quaternion.identity;
            float arc = ps.shape.arc;
            if (arc >= 359f) return Quaternion.identity;

            // Unity's Circle/Cone arc starts at local +X and sweeps CCW, so the
            // wedge's centre sits half an arc in. Subtract that to put the
            // CENTRE on the requested direction instead of the leading edge.
            float degrees = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            return Quaternion.Euler(0f, 0f, degrees - arc * 0.5f);
        }

        /// Spawns a throwaway SpriteRenderer + SpriteAnimator for a one-shot
        /// hand-drawn burst. Nothing is pooled: these live for well under a
        /// second and a pool would outweigh the allocation it saves.
        static void PlaySheet(EffectRecipe recipe, Vector2 worldPosition)
        {
            var clip = recipe.sheetClip;
            if (clip == null || clip.frames == null || clip.frames.Length == 0) return;

            var go = new GameObject($"[VFX] {recipe.name}");
            go.transform.position = worldPosition;
            go.transform.localScale = Vector3.one * recipe.sheetScale;
            if (recipe.randomRotation)
                go.transform.rotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));

            var renderer = go.AddComponent<SpriteRenderer>();
            // Set frame 0 up front: SpriteAnimator only assigns a sprite in its
            // Update, which would leave this invisible for the first frame —
            // long enough to see on a 3-frame burst.
            renderer.sprite = clip.frames[0];
            renderer.sortingLayerName = recipe.sheetSortingLayer;
            renderer.sortingOrder = recipe.sheetSortingOrder;
            if (recipe.sheetMaterial != null) renderer.sharedMaterial = recipe.sheetMaterial;
            if (recipe.randomFlip) renderer.flipX = Random.value < 0.5f;

            go.AddComponent<SpriteAnimator>().Play(clip);

            // A one-shot dies exactly when its last frame has been shown, so a
            // mistuned lifetime can never truncate the animation. Only a LOOPING
            // sheet needs an arbitrary cut-off.
            float life = clip.loop
                ? recipe.particleLifetime
                : clip.frames.Length / Mathf.Max(0.01f, clip.framesPerSecond);
            Object.Destroy(go, life);
        }

        static void EnsureRig()
        {
            if (audioSource != null) return;
            var rig = new GameObject("[EffectPlayer]");
            // DontDestroyOnLoad throws outside play mode, which would leave this
            // object stranded in whatever scene is open — and this project's
            // scene rule says nothing may quietly appear in a hand-tuned scene.
            // HideAndDontSave makes that impossible regardless of who calls us.
            rig.hideFlags = HideFlags.HideAndDontSave;
            Object.DontDestroyOnLoad(rig);
            audioSource = rig.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0f;
            impulse = rig.AddComponent<CinemachineImpulseSource>();
        }
    }
}
