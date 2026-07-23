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
        [Tooltip("Layers that block line of sight (walls).")]
        public LayerMask sightBlockers = ~0;

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
        public float brainSearchWeight = 0.9f;
        [Tooltip("Seconds after last seeing you that he keeps hunting (Search fades to 0 over this).")]
        public float brainSearchMemory = 8f;
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
    }
}
