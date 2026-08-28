// How lit is this spot? The question the "only total darkness drains" ruling
// turns into code.
//
// There is no API in URP that answers it — 2D lighting is a render-time result,
// not queryable state — so this reconstructs an approximation from the Light2D
// components actually in the scene. It is deliberately an APPROXIMATION and the
// places it is wrong are worth knowing:
//
//   - It ignores shadows. A player standing behind a pillar reads as lit. Adding
//     a raycast per light would fix that and cost a physics query per light per
//     sample; not paid until something needs it.
//   - It ignores light COLOUR, using intensity only. A dim red torch and a dim
//     white one read the same, which is right for "can I see" and wrong for
//     mood.
//   - Falloff is linear from the inner to the outer radius. URP's real 2D
//     falloff curve is not linear, so mid-range readings drift from what the
//     screen shows. The threshold this feeds is about the DARK end, where both
//     agree, which is why linear is good enough here and would not be if this
//     drove a visual.
//
// Kept separate from PlayerComposure so the approximation can be replaced
// without touching the mechanic, and so the mechanic can be reasoned about
// without reading light maths.
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace TimeKiller.Sanity
{
    public static class LightSampler2D
    {
        static readonly List<Light2D> cache = new List<Light2D>();
        static float cachedAt = float.NegativeInfinity;

        /// Lights change rarely (a torch is destroyed when a room is), so the
        /// list is rebuilt on an interval rather than per sample. FindObjects is
        /// the expensive part of this whole system and it is the part that does
        /// not need to be fresh.
        const float RefreshSeconds = 5f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset() { cache.Clear(); cachedAt = float.NegativeInfinity; }

        /// Approximate 0..1-ish light level at a world position. Can exceed 1
        /// where lights overlap, which is fine — every caller compares against a
        /// low threshold.
        public static float LevelAt(Vector2 worldPosition)
        {
            Refresh();
            float total = 0f;
            for (int i = 0; i < cache.Count; i++)
            {
                var l = cache[i];
                if (l == null || !l.isActiveAndEnabled) continue;
                total += Contribution(l, worldPosition);
            }
            return total;
        }

        /// One light's contribution. Global lights apply everywhere; point
        /// lights fall off linearly between their inner and outer radius.
        static float Contribution(Light2D light, Vector2 at)
        {
            if (light.lightType == Light2D.LightType.Global)
                return Mathf.Max(0f, light.intensity);

            float outer = light.pointLightOuterRadius;
            if (outer <= 0.0001f) return 0f;

            float d = Vector2.Distance(at, light.transform.position);
            if (d >= outer) return 0f;

            float inner = Mathf.Min(light.pointLightInnerRadius, outer);
            // Inside the inner radius a 2D light is at full strength.
            float falloff = d <= inner ? 1f : 1f - Mathf.InverseLerp(inner, outer, d);
            return Mathf.Max(0f, light.intensity) * falloff;
        }

        static void Refresh()
        {
            if (Time.time - cachedAt < RefreshSeconds && cache.Count > 0) return;
            cachedAt = Time.time;
            cache.Clear();
            cache.AddRange(Object.FindObjectsByType<Light2D>(
                FindObjectsInactive.Exclude, FindObjectsSortMode.None));
        }

        /// For probes and tests: how many lights the sampler currently knows about.
        public static int KnownLights => cache.Count;
    }
}
