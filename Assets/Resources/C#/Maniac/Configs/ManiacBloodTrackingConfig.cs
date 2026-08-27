// Every tunable of "he can read the floor".
//
// SHIPS DISABLED, and that is not timidity. ARCHITECTURE's blood pass-1 note set
// the condition when the seam was designed: tracking "must ship behind a config
// flag and be A/B'd with the bot playtester: the maniac's difficulty is tuned
// across five passes, and a homing trail could make the 1-HP state unsurvivable
// rather than tense". Bleeding starts at low HP by definition, so this feature
// applies its pressure exactly when the player has the least left to give.
//
// There is a second reason today: per-clock escalation is already in flight and
// has not had the user's ear. Two unjudged difficulty changes at once cannot be
// attributed to either one. Turn this on deliberately, on its own, and measure.
using UnityEngine;

namespace TimeKiller.Maniac
{
    [CreateAssetMenu(fileName = "ManiacBloodTrackingConfig",
                     menuName = "TimeKiller/Configs/Maniac Blood Tracking")]
    public class ManiacBloodTrackingConfig : ScriptableObject
    {
        public const string ResourcesPath = "C#/Maniac/Configs/ManiacBloodTrackingConfig";

        [Tooltip("Master switch, OFF by default. See the header: this changes difficulty precisely when the player is weakest, and the architecture asked for a bot A/B before it goes live.\n\nOff = the tracker subscribes to nothing and costs nothing.")]
        public bool enabled = false;

        [Header("Finding it")]
        [Tooltip("How close he must physically come to a stain before he notices it.\n\nKeep this SMALL. This is the whole difference between a trail he stumbles across and a beacon that tells him where you are: at 2u he has to nearly walk over your blood, which means the trail matters only where he was already going to be. Raising it toward his sight range turns bleeding into a homing signal.")]
        [Range(0.5f, 6f)] public float noticeRadius = 2f;

        [Tooltip("Require clear line of sight to the stain before he can notice it.\n\nOn, because floor blood is something he SEES. Without this he reads a smear through a wall, which is the same unfairness the 2026-08-02 hearing pass removed from his ears.")]
        public bool requireLineOfSight = true;

        [Tooltip("Seconds a trace stays trackable.\n\nBlood dries — BloodConfig already darkens a stain over time — and a trail he can still read ten minutes later would make the whole map a map of everywhere you have ever been. Shorter than the stain's visual life on purpose: the picture outlives the scent.")]
        [Range(5f, 180f)] public float traceLifetime = 45f;

        [Header("Following it")]
        [Tooltip("Once he notices blood, how far along the trail he is allowed to look for the FRESHEST nearby trace, which is the one pointing where you went.\n\nThis is what makes it a trail rather than a puddle: he reads the direction of travel. Capped so he cannot leap the whole trail at once and arrive at your feet — he gets the next few steps, not the destination.")]
        [Range(1f, 10f)] public float followRadius = 4f;

        [Tooltip("Traces within this radius of a lead he has just taken are consumed, so the same puddle cannot re-trigger him forever.\n\nWithout it he re-notices the stain he is standing on every scan and orbits it.")]
        [Range(0.5f, 6f)] public float consumeRadius = 2.5f;

        [Tooltip("Minimum seconds between two leads. Stops a dense trail firing an investigate every frame while he walks along it.")]
        [Range(0f, 20f)] public float leadCooldown = 4f;

        [Header("Cost")]
        [Tooltip("Seconds between floor scans. This is not a per-frame system: 0.4s is far finer than walking speed can outrun and costs a handful of distance checks.")]
        [Range(0.05f, 2f)] public float scanInterval = 0.4f;

        [Tooltip("How many traces are remembered at once. The oldest is overwritten when full, exactly like BloodStainField's ring buffer, so a long run cannot grow this without bound.")]
        [Range(8, 256)] public int maxTraces = 96;
    }
}
