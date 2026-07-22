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

        [Header("Chase (the balancing valve)")]
        [Tooltip("GENEROUS on purpose: seconds after losing line of sight before he gives up and investigates your last seen spot.")]
        public float loseSightSeconds = 2.5f;
        [Tooltip("Seconds between breadcrumbs recorded while chasing (he follows the player's trail through corridors).")]
        public float breadcrumbInterval = 0.15f;

        [Header("Attack")]
        public int damage = 1;
        public float attackRange = 0.9f;
        [Tooltip("Seconds between swings — also the player's escape window after the shove.")]
        public float attackCooldown = 1.6f;

        [Header("Patrol")]
        [Tooltip("How close to a waypoint counts as reached.")]
        public float waypointTolerance = 0.4f;
        [Tooltip("Idle pause at each waypoint (looking around).")]
        public float waypointPauseSeconds = 1.2f;
    }
}
