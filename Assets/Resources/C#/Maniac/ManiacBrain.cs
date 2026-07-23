// Utility AI: the maniac's decision-maker. Instead of hard-coded state
// transitions ("if see -> chase"), every behavior gets a numeric score from
// weighted considerations (line of sight, time since seen, noise recency +
// proximity) and the highest wins. Competing stimuli — a fresh noise while
// searching, losing sight near a sound — resolve by comparison, which reads as
// emergent, less predictable "thinking". The pure Score() is unit-testable
// without a scene. The committed Attack swing and the Outlast wardrobe-march
// stay reactive (handled in ManiacController), the brain owns everything else.
using UnityEngine;

namespace TimeKiller.Maniac
{
    public enum ManiacBehavior { Patrol = 0, Investigate = 1, Search = 2, Chase = 3 }

    public class ManiacBrain
    {
        readonly ManiacController maniac;
        public ManiacBrain(ManiacController maniac) => this.maniac = maniac;

        public float[] LastScores { get; private set; } = new float[4];

        /// Pick the best behavior for the current situation, nudged to stay put.
        public ManiacBehavior Decide(ManiacBehavior current)
        {
            var per = maniac.Perception;
            var cfg = maniac.Config;
            float tSeen = float.IsNegativeInfinity(per.LastSeenTime) ? float.MaxValue : Time.time - per.LastSeenTime;
            float tNoise = float.IsNegativeInfinity(per.LastNoiseTime) ? float.MaxValue : Time.time - per.LastNoiseTime;
            float distNoise = Vector2.Distance(maniac.Motor.Position, per.LastNoisePosition);

            float[] scores = Score(cfg, per.CanSeePlayer, tSeen, tNoise, distNoise);
            scores[(int)current] += cfg.brainStickiness; // anti-flicker
            LastScores = scores;

            int best = 0;
            for (int i = 1; i < scores.Length; i++)
                if (scores[i] > scores[best]) best = i;
            return (ManiacBehavior)best;
        }

        /// Pure utility scoring. Order: [Patrol, Investigate, Search, Chase].
        /// tSeen / tNoise are seconds since (float.MaxValue if never).
        public static float[] Score(ManiacConfig cfg, bool seen, float tSeen, float tNoise, float distNoise)
        {
            // Chase: dominant while seen; stays high through brief sight breaks
            // (the generous lose-sight valve), then drops to 0.
            float chase = seen ? 1f
                : (tSeen < cfg.loseSightSeconds ? Mathf.Lerp(1f, 0.6f, tSeen / cfg.loseSightSeconds) : 0f);

            // Search: the hunt after the trail goes cold — peaks as chase fades,
            // decays to 0 over the memory window, then he gives up.
            float search = (!seen && tSeen < cfg.brainSearchMemory)
                ? cfg.brainSearchWeight * (1f - tSeen / cfg.brainSearchMemory)
                : 0f;

            // Investigate: pulled by a recent, nearby noise (recency × proximity).
            float recency = tNoise < cfg.brainNoiseMemory ? 1f - tNoise / cfg.brainNoiseMemory : 0f;
            float prox = cfg.hearingRadius > 0f ? Mathf.Clamp01(1f - distNoise / cfg.hearingRadius) : 0f;
            float investigate = cfg.brainNoiseWeight * recency * prox;

            float patrol = cfg.brainPatrolBaseline;

            return new[] { patrol, investigate, search, chase };
        }
    }
}
