// Every tunable of the maniac in one asset (C#/Maniac/Configs/ManiacConfig.asset).
// Design (interview 2026-07-22): hearing+sight hybrid, patrols the wing loop,
// CHASE IS FASTER THAN PLAYER RUN (4.5) — the generous lose-sight timer is the
// balancing valve that keeps that survivable.
using UnityEngine;

namespace TimeKiller.Maniac
{
    [CreateAssetMenu(menuName = "TimeKiller/Configs/Maniac", fileName = "ManiacConfig")]
    public class ManiacConfig : ScriptableObject
    {
        [Header("Movement (player run = 4.5)")]
        public float patrolSpeed = 1.8f;
        public float investigateSpeed = 3.0f;
        [Tooltip("FASTER than player run by design — hiding/breaking sight is the answer.")]
        public float chaseSpeed = 5.2f;
        public float acceleration = 25f;
        public float deceleration = 35f;

        [Header("Hearing")]
        [Tooltip("A footstep is heard when distance < loudness * this radius. Run loudness 1 -> full radius; quiet walk shrinks it.")]
        public float hearingRadius = 9f;
        [Tooltip("Seconds he searches around the last heard position before returning to patrol.")]
        public float investigateSeconds = 5f;

        [Header("Sight")]
        public float sightRange = 7f;
        [Tooltip("Full cone angle in degrees, centered on his facing.")]
        public float sightConeAngle = 140f;
        [Tooltip("Point-blank awareness: within this distance he SENSES the player regardless of his facing cone (walls still block). Fixes overrunning the player and standing next to them oblivious. Should exceed attackRange so a touch also lands a hit.")]
        public float proximityRange = 1.9f;
        [Tooltip("Layers that block line of sight (walls).")]
        public LayerMask sightBlockers = ~0;

        [Header("Detection — awareness stealth model (hard but fair)")]
        [Tooltip("Seeing is NOT instant: an awareness meter (0..1) fills while you're exposed and drains when he loses you. Crossing 'suspicion' makes him investigate; reaching 1 = fully spotted -> Chase.")]
        [Range(0f, 1f)] public float suspicionThreshold = 0.4f;
        [Tooltip("Base awareness gained per second under IDEAL exposure (central vision, close, lit, moving). Higher = spotted faster.")]
        public float awarenessFillRate = 2.6f;
        [Tooltip("Awareness lost per second when he can't sense you (out of range / behind cover / hidden). Higher = forgets faster.")]
        public float awarenessDrainRate = 0.8f;
        [Tooltip("Central vision cone (deg): full-strength detection inside this.")]
        public float centralConeAngle = 100f;
        [Tooltip("Peripheral vision cone (deg): 'corner of his eye' — only up close (peripheralRange) and at reduced strength (peripheralWeight). Motion here makes him turn and check.")]
        public float peripheralConeAngle = 250f;
        [Tooltip("Max distance the peripheral cone works — you can only be caught in the corner of his eye when close.")]
        public float peripheralRange = 4.5f;
        [Range(0f, 1f)] [Tooltip("How strongly peripheral vision detects vs central (0.4 = 40% speed).")]
        public float peripheralWeight = 0.4f;

        [Header("Detection — stealth factors")]
        [Range(0f, 1f)] [Tooltip("Detection multiplier while you STAND STILL. Low = a motionless player is much harder to notice (stillness is a real tool).")]
        public float stillDetectionMultiplier = 0.4f;
        [Tooltip("Detection multiplier while RUNNING (player run=4.5). >1 = running gets you spotted fast.")]
        public float runningDetectionMultiplier = 1.6f;
        [Tooltip("Player speed above which counts as 'running' for detection.")]
        public float runSpeedThreshold = 3.5f;
        [Range(0f, 1f)] [Tooltip("Detection floor in PITCH DARK (0 exposure). Low = deep shadow nearly hides you; he's not fully blind though. Full torchlight always detects at full speed.")]
        public float exposureFloor = 0.2f;
        [Tooltip("Ambient light everywhere (before torches). Keeps open areas a bit exposed even away from a torch.")]
        [Range(0f, 1f)] public float ambientExposure = 0.12f;

        [Header("Search (the lost-sight hunt — replaces the abrupt give-up)")]
        [Tooltip("Speed while sweeping for you after losing sight — alert, between patrol and investigate.")]
        public float searchSpeed = 2.6f;
        [Tooltip("How many spots he checks before giving up. First is your last-seen position; the rest are the nearest patrol waypoints (known-reachable).")]
        public int searchPoints = 3;
        [Tooltip("Seconds he stops to scan his sight cone at each search spot.")]
        public float searchLookSeconds = 1.7f;
        [Tooltip("Anti-stuck: max seconds to reach a search spot before moving to the next.")]
        public float searchTravelTimeout = 4f;
        [Tooltip("Total degrees he sweeps his sight cone side-to-side while looking (catches a peeking player).")]
        public float searchScanAngle = 120f;
        [Tooltip("How fast the scan sweeps.")]
        public float searchScanSpeed = 2f;

        [Header("Chase (the balancing valve)")]
        [Tooltip("GENEROUS on purpose: seconds after losing line of sight before he gives up and searches your last seen area.")]
        public float loseSightSeconds = 2.5f;
        [Tooltip("Seconds between breadcrumbs recorded while chasing (he follows the player's trail through corridors).")]
        public float breadcrumbInterval = 0.15f;

        [Header("Attack")]
        public int damage = 1;
        public float attackRange = 0.9f;
        [Tooltip("Minimum seconds between swings. He KEEPS CHASING during this — the player's escape comes from the post-hit adrenaline burst, not from him stopping (design 2026-07-23).")]
        public float attackCooldown = 1.6f;
        [Tooltip("Brief swing recovery before he resumes the chase — just enough to read the attack.")]
        public float attackRecoverySeconds = 0.35f;

        [Header("Hiding (the Outlast rule)")]
        [Tooltip("If he had eyes on the player within this many seconds before they hid, the spot is compromised — he walks up and drags a hit out of it.")]
        public float seenEnterWindow = 1.25f;

        [Header("Body")]
        [Tooltip("Rigidbody mass. Heavy on purpose: the player must NOT be able to push him around.")]
        public float bodyMass = 400f;
        [Tooltip("After his hit lands the player can slip THROUGH him for this long — otherwise his unpushable body can pin a cornered player (design 2026-07-23). Collision restores once they separate.")]
        public float phaseThroughSeconds = 2.5f;

        [Header("Brain (utility AI — scores each behavior, highest wins)")]
        [Tooltip("Baseline pull toward Patrol when nothing else scores. The floor every other behavior must beat.")]
        public float brainPatrolBaseline = 0.15f;
        [Tooltip("Peak weight of the Search urge right after losing sight.")]
        public float brainSearchWeight = 0.95f;
        [Tooltip("Seconds after last seeing you that he keeps hunting (Search fades to 0 over this). Longer = he ranges farther before giving up.")]
        public float brainSearchMemory = 14f;
        [Tooltip("Peak weight of Investigate for a fresh, close noise.")]
        public float brainNoiseWeight = 0.8f;
        [Tooltip("Seconds over which a heard noise loses its pull.")]
        public float brainNoiseMemory = 5f;
        [Tooltip("Bonus added to whatever he's currently doing — stops rapid flip-flopping between behaviors.")]
        public float brainStickiness = 0.1f;

        [Header("Patrol")]
        [Tooltip("How close to a waypoint counts as reached.")]
        public float waypointTolerance = 0.4f;
        [Tooltip("Idle pause at each waypoint (looking around).")]
        public float waypointPauseSeconds = 1.2f;
        [Tooltip("Anti-stuck: if he can't reach a waypoint within this many seconds (wedged on a pillar/corner), he gives up and moves to the next. Patrol must never hang.")]
        public float patrolWaypointTimeout = 5f;
    }
}
