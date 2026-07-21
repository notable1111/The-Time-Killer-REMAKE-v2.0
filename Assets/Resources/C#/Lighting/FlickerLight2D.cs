// Makes a Light2D flicker like fire using layered Perlin noise — no two
// torches flicker in sync (seed from position). Attach next to any Light2D.
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
        }
    }
}
