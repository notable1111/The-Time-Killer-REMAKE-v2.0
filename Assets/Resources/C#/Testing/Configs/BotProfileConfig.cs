// The skill model of one simulated player. This is the single most important
// file in the playtest harness, because a bot that reads Marker/ZoneCenter
// straight from memory hits 100% of skill checks forever and reports that the
// game is easy — a broken instrument that looks like it works.
//
// So every field here exists to make the bot WORSE in a human-shaped way:
// it reacts late, it misjudges the marker, it panics at the wrong distance,
// and — unless you check knowsEverything — it does not know where the clocks
// are until it has walked into a room and SEEN one.
//
// knowsEverything is the cheat, kept deliberately as a labelled control:
// an omniscient bot measures "how hard is this level once you know it by
// heart", which is a real question, just a different one from "how hard is
// this level the first time". Ship both, never mix them in one report.
using UnityEngine;

namespace TimeKiller.Testing
{
    [CreateAssetMenu(menuName = "TimeKiller/Configs/Bot Profile", fileName = "BotProfile")]
    public class BotProfileConfig : ScriptableObject
    {
        [Tooltip("Short id written into every result row. Keep it filename-safe.")]
        public string profileName = "average";

        [Header("Skill check (clock repair)")]
        [Tooltip("Delay between deciding to press and the press landing. The bot predicts where the marker WILL be, so this alone is survivable — the jitter below is what actually costs hits.")]
        [Range(0.02f, 0.8f)] public float reactionTime = 0.22f;
        [Tooltip("Random spread on the reaction, as a fraction of it. This is un-predictable by design: the bot cannot compensate for its own inconsistency.")]
        [Range(0f, 1f)] public float reactionJitter = 0.3f;
        [Tooltip("Error added to the PERCEIVED marker position (0..1 bar units). Compare against ClockConfig.zoneWidth * 0.5 — at 0.12 vs a 0.09 half-zone, most presses miss.")]
        [Range(0f, 0.4f)] public float aimError = 0.06f;
        [Tooltip("Minimum gap between presses — a human cannot mash faster than this.")]
        [Range(0.05f, 1f)] public float pressCooldown = 0.18f;

        [Header("Nerve")]
        [Tooltip("Maniac this close (and perceived) = abandon whatever you were doing and run.")]
        public float panicDistance = 5f;
        [Tooltip("Feel safe again once he is this far away (hysteresis — stops flee/work flapping).")]
        public float calmDistance = 11f;
        [Tooltip("0 = always outrun him, 1 = always dive into the nearest wardrobe.")]
        [Range(0f, 1f)] public float hideBias = 0.6f;
        [Tooltip("Once hidden, sit still at least this long before even considering coming out.")]
        public float minHideSeconds = 4f;

        [Header("Knowledge — the honesty switch")]
        [Tooltip("THE CHEAT. On: every clock, wardrobe and the exit are known from frame one (an upper bound — 'a player who has memorised the level'). Off: the bot must explore and SEE them, like a first-time player. Keep the two apart in every report.")]
        public bool knowsEverything = false;
        [Tooltip("How far the bot notices things it has line of sight to. Roughly 'what is legible on screen', not the maniac's 7-unit cone.")]
        public float sightRange = 9f;
        [Tooltip("0 = wanders semi-randomly toward unexplored space, 1 = always heads for the nearest unexplored room. Models map sense, not eyesight.")]
        [Range(0f, 1f)] public float routeKnowledge = 0.75f;

        [Header("Movement")]
        [Tooltip("Chance of sprinting while travelling in safety. Sprinting is LOUD — a jumpy novice sprints everywhere and summons the maniac onto themselves.")]
        [Range(0f, 1f)] public float sprintTendency = 0.5f;

        /// Reaction for one press: the mean plus un-compensated jitter.
        public float RollReaction(System.Random rng) =>
            Mathf.Max(0.02f, reactionTime * (1f + reactionJitter * (float)Gauss(rng)));

        /// Perception noise on the marker read.
        public float RollAimError(System.Random rng) => aimError * (float)Gauss(rng);

        // Box-Muller, clamped: keeps the tail from producing absurd one-off values.
        static double Gauss(System.Random rng)
        {
            double u1 = 1.0 - rng.NextDouble(), u2 = rng.NextDouble();
            double g = System.Math.Sqrt(-2.0 * System.Math.Log(u1)) * System.Math.Cos(2.0 * System.Math.PI * u2);
            return System.Math.Max(-2.5, System.Math.Min(2.5, g));
        }
    }
}
