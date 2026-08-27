// Makes a Light2D flicker like fire using layered Perlin noise — no two
// torches flicker in sync (seed from position). Attach next to any Light2D.
//
// It also breathes the light's SHADOW softness, from the same noise value that
// drives the intensity. That coupling is the whole point and the reason this
// lives here rather than in its own component: a separate flicker would compute
// its own noise and drift out of step with the flame it is supposed to belong
// to, so the shadows would shiver on a schedule of their own. Written by
// ShadowSetup (Setup/49); at shadowBreathAmount 0 the behaviour is byte-identical
// to before shadows existed.
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace TimeKiller.Lighting
{
    [RequireComponent(typeof(Light2D))]
    public class FlickerLight2D : MonoBehaviour
    {
        [Tooltip("Base intensity the flicker moves around")]
        [SerializeField] float baseIntensity = 1.1f;
        [Tooltip("How far intensity wanders from base (± this value)")]
        [SerializeField, Range(0f, 1f)] float amplitude = 0.25f;
        [Tooltip("Flicker speed — higher = more nervous flame")]
        [SerializeField, Range(0.1f, 20f)] float speed = 6f;

        [Header("Shadow breathing (written by Setup/49)")]
        [Tooltip("How far shadow softness wanders with the flame. 0 = this component never touches shadows, exactly as it behaved before the shadow pass.")]
        [SerializeField, Range(0f, 0.5f)] float shadowBreathAmount = 0f;
        [Tooltip("Softness the breathing moves around — kept in step with ShadowConfig.shadowSoftness.")]
        [SerializeField, Range(0f, 1f)] float baseShadowSoftness = 0.5f;

        Light2D light2d;
        float seed;

        void Awake()
        {
            light2d = GetComponent<Light2D>();
            seed = transform.position.x * 7.31f + transform.position.y * 3.17f;
        }

        void Update()
        {
            // Two noise octaves: slow breathing + fast crackle.
            float slow = Mathf.PerlinNoise(seed, Time.time * speed * 0.35f);
            float fast = Mathf.PerlinNoise(seed + 41.7f, Time.time * speed);
            float noise = (slow * 0.65f + fast * 0.35f) * 2f - 1f;
            light2d.intensity = Mathf.Max(0f, baseIntensity + noise * amplitude);

            // Same noise, so the shadow edge softens exactly when the flame dips.
            // Guarded because this component predates shadows and must stay inert
            // for any light that is not casting one.
            if (shadowBreathAmount > 0f && light2d.shadowsEnabled)
                light2d.shadowSoftness = Mathf.Clamp01(baseShadowSoftness + noise * shadowBreathAmount);
        }
    }
}
