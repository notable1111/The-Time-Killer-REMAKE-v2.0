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
        // Rebuilt whenever it goes stale, not only on a timer - see Stale().

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
        /// <param name="ignoreUnder">Lights parented under this transform are
        /// skipped. Pass the subject being sampled — see the warning below.</param>
        ///
        /// ⚠️ THE SECOND BUG THIS SIGNATURE EXISTS FOR (found 2026-08-28 by a bot
        /// A/B, and it was live). The scene gives the player a child Light2D
        /// called PlayerGlow: intensity 0.65, inner radius 0.3. PlayerComposure
        /// samples at the player's OWN position, so the distance to that light is
        /// always 0 — inside the inner radius, contributing its full 0.65 forever.
        /// With CastleWing's 0.32 global light on top, the player read 0.97
        /// MINIMUM, everywhere, always, against a darkness threshold of 0.4.
        ///
        /// So "only genuine darkness drains" could not fire once. Not rarely —
        /// never, as arithmetic rather than as bad luck. 24 bot runs recorded a
        /// darkFraction of exactly 0.000 and every run that never hid ended on
        /// composure exactly 1.000.
        ///
        /// It hid behind a number that looked like a reading: an earlier session
        /// sampled 0.97 and recorded it as a lit spot, when 0.97 is precisely the
        /// floor the player carries around with them. And the survey that put 55%
        /// of walkable ground in darkness replayed positions with no player in the
        /// scene, so it measured a world the game never actually asks about.
        public static float LevelAt(Vector2 worldPosition, Transform ignoreUnder = null)
        {
            Refresh();
            float total = 0f;
            for (int i = 0; i < cache.Count; i++)
            {
                var l = cache[i];
                if (l == null || !l.isActiveAndEnabled) continue;
                // IsChildOf is true for the transform itself, so a light placed
                // on the player root is excluded too, not just one in a child.
                if (ignoreUnder != null && l.transform.IsChildOf(ignoreUnder)) continue;
                total += Contribution(l, worldPosition);
            }
            return total;
        }

        /// Is the cache still pointing at live lights?
        ///
        /// ⚠️ THE BUG THIS EXISTS FOR (found 2026-08-28, and it was live). A scene
        /// reload destroys every Light2D and builds new ones, but the cached
        /// references survive as Unity's fake-null. Every contribution was then
        /// skipped, LevelAt returned 0, and the player read as being in TOTAL
        /// DARKNESS wherever they were standing - for up to RefreshSeconds. And
        /// GameFlow reloads the scene on every restart, so EVERY run began with a
        /// false dark reading that drained composure for nothing.
        ///
        /// It hid because a stale cache and a genuinely unlit room produce exactly
        /// the same number. Caught only by sampling a spot measured at 0.97 a few
        /// minutes earlier and getting 0.00.
        static bool Stale()
        {
            for (int i = 0; i < cache.Count; i++)
                if (cache[i] == null) return true;
            return false;
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
            // realtimeSinceStartup, not Time.time: Time.time does not advance in
            // EDIT mode, so an editor-side probe would hold the first cache it
            // ever built forever - which is how the stale-reference bug above was
            // first observed. It is also immune to timeScale, and GameFlow sets
            // timeScale to 0 on the ending beat.
            float now = Time.realtimeSinceStartup;
            if (now - cachedAt < RefreshSeconds && cache.Count > 0 && !Stale()) return;
            cachedAt = now;
            cache.Clear();
            cache.AddRange(Object.FindObjectsByType<Light2D>(
                FindObjectsInactive.Exclude, FindObjectsSortMode.None));
        }

        /// For probes and tests: how many lights the sampler currently knows about.
        public static int KnownLights => cache.Count;
    }
}
