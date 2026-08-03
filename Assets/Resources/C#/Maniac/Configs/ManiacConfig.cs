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
        [Range(0f, 1f)]
        [Tooltip("Fraction of the heard radius that survives ONE wall standing between him and the noise; each further wall multiplies again. Hearing used to be the only sense that ignored geometry, so a footstep through solid stone reached him exactly as loud as one beside him and the 9u radius was really a 9u sphere of omniscience. Walls here are the same sightBlockers layers that stop his eyes, so 'solid' means one thing. Set to 1 to restore the old wall-ignoring behaviour.")]
        public float hearingWallMuffle = 0.45f;
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
        [Tooltip("How sharply detection weakens with distance. 1 = linear to zero at sightRange, which left everything past ~4u effectively invisible. 2 = strong through the mid range, falling off only near the edge of his vision.")]
        public float sightFalloffPower = 2f;

        [Header("Detection — awareness stealth model (hard but fair)")]
        [Tooltip("Seeing is NOT instant: an awareness meter (0..1) fills while you're exposed and drains when he loses you. Crossing 'suspicion' makes him investigate; reaching 1 = fully spotted -> Chase.")]
        [Range(0f, 1f)] public float suspicionThreshold = 0.4f;
        [Tooltip("Base awareness gained per second under IDEAL exposure (central vision, close, lit, moving). Higher = spotted faster.")]
        public float awarenessFillRate = 2.6f;
        [Range(0.05f, 1f)]
        [Tooltip("HESITATION. Fraction of the fill rate that applies ONCE he is already suspicious — the climb from suspicionThreshold up to certain.\n\nThe suspicion beat was already built (he stops, stares, then closes at 1.4, slower than your walk so backing away works) but nobody ever experienced it: at fill rate 2.6 the climb from suspicious to spotted takes UNDER 0.25s, so he was through the window before the beat could play. Measured across a real session, that left the tension curve bimodal — Safe 48s, Panic 44s, but Threat only 6s. The build, which is where dread actually lives, was 13 of 143 seconds.\n\n0.35 stretches that final climb to roughly three times as long. Deliberately does NOT change how easily he NOTICES you: everything up to the threshold fills at the full rate, so stealth is exactly as hard as before. Only the moment of being caught stretches out. Set to 1 for the old instant certainty.")]
        public float awarenessCertaintyScale = 0.35f;
        [Tooltip("Awareness lost per second when he can't sense you (out of range / behind cover / hidden). Must stay BELOW the effective fill rate, or ducking behind one pillar erases everything he'd built up.")]
        public float awarenessDrainRate = 0.35f;
        [Tooltip("Grace period: after losing you he HOLDS his current awareness this long before it starts draining. This is the 'he's onto you' beat — stepping behind cover for a moment no longer resets him to oblivious.")]
        public float awarenessHoldSeconds = 1.2f;
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

        [Header("Suspicion — the \"did he see me?\" beat")]
        [Tooltip("How far off his GUESS is when suspicion first crosses the threshold. Suspicion used to hand him the player's exact live position every frame, which made being half-noticed identical to being seen. The error shrinks to zero as awareness climbs toward 1, so his estimate converges as he grows certain.")]
        public float suspicionGuessError = 3f;
        [Tooltip("Seconds he STOPS and stares at his guess before moving on it. This is the tell — footsteps stopping dead — and the window the player uses to back away.")]
        public float suspicionHoldSeconds = 1f;
        [Tooltip("Speed of the suspicious approach. Deliberately BELOW the player's walk (2.2) so retreating is a real option; a plain heard noise still uses the faster investigateSpeed.")]
        public float suspiciousApproachSpeed = 1.4f;

        [Header("Search (the lost-sight hunt — replaces the abrupt give-up)")]
        [Tooltip("Speed while sweeping for you after losing sight — alert, between patrol and investigate.")]
        public float searchSpeed = 2.6f;
        [Tooltip("Speed for the RUSH that opens every hunt. He does not know you stopped — his working theory is that you are still running — so he keeps driving at chase pace toward where you would be if you had. Set at chaseSpeed by default; the moment he slows to searchSpeed is the moment he stops believing that.")]
        public float searchRushSpeed = 5.2f;
        [Tooltip("Longest the opening rush lasts. It also ends early the instant he arrives at his first guess and starts looking — the rush is over when the theory has been tested, whichever comes first.")]
        public float searchRushSeconds = 3.5f;
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
        [Tooltip("While he can SEE you he beelines with no pathfinding, on the reasoning that line of sight means the way is clear. It does not: sight is a CENTRE-TO-CENTRE line and his body is ~0.6u wide, so a doorway seen at an angle passes the line and stops the body. Measured on CastleWingLDtk (TimeKiller/Verify/Maniac Chase Grind, 2026-08-02): 9.5% of all sightings, rising to 18.3% at 6-7u — which is sightRange, so it is worst at the moment he first acquires you. He now watches his own progress over this window instead of guessing which sightings those are.")]
        public float beelineStallWindow = 0.35f;
        [Range(0f, 1f)]
        [Tooltip("Fraction of the ground his speed SHOULD have covered in that window. Below it he is grinding on geometry rather than closing. Deliberately low: brushing a wall and sliding along it is normal chasing, being stopped dead by one is not.")]
        public float beelineStallFraction = 0.4f;
        [Tooltip("Seconds the navigator drives the chase after a stall before he tries the straight line again. The beeline is the better look wherever it works, so this hands control back rather than keeping it for the rest of the chase.")]
        public float beelineNavSeconds = 0.9f;

        [Tooltip("Chance he walks to the SECOND or third likeliest place instead of the best one while searching. A searcher who always takes the optimal cell reads as a pathfinder rather than a person — Alien: Isolation searches sub-optimally on purpose for exactly this reason. 0 restores the old always-optimal behaviour exactly.")]
        [Range(0f, 1f)] public float searchDoubtChance = 0.3f;

        [Header("Director hints (optional — no ManiacDirector in scene = never used)")]
        [Tooltip("Seconds a Director hint stays worth acting on. It expires by itself because a hint that never went stale would pin him to a spot the player left minutes ago — and standing on old information is exactly what makes an AI look stupid.")]
        public float hintLifetimeSeconds = 45f;
        [Tooltip("How close to the hint's centre counts as having swept it. Generous: the hint is an AREA, and treating it as a point to stand on defeats the purpose.")]
        public float hintReachedTolerance = 3.5f;

        [Header("Attack")]
        public int damage = 1;
        public float attackRange = 0.9f;
        [Tooltip("Minimum seconds between swings. He KEEPS CHASING during this — the player's escape comes from the post-hit adrenaline burst, not from him stopping (design 2026-07-23).")]
        public float attackCooldown = 1.6f;
        [Tooltip("Brief swing recovery before he resumes the chase — just enough to read the attack.")]
        public float attackRecoverySeconds = 0.35f;
        // attackMoveShare was removed on 2026-08-03. It let him keep closing
        // during a swing instead of stopping, and measured across two sessions it
        // changed nothing: their capsules touch at 0.58u inside a 0.9u attackRange,
        // so physics cancels whatever velocity the motor is given. See the note on
        // AttackState in ManiacStates.cs before reintroducing it.

        [Header("Hiding (the Outlast rule)")]
        [Tooltip("If he had eyes on the player within this many seconds before they hid, the spot is compromised — he walks up and drags a hit out of it.")]
        public float seenEnterWindow = 1.25f;
        [Tooltip("Only compromise the spot if he was actually ON SCREEN when you hid. Measured 2026-07-28: the camera shows 3.4u vertically but he sees 7u, so there is a 3.6u band above and below where he watches you and you cannot see him — hiding there was punished on information the game never showed the player. This does NOT weaken his sight; he still detects and chases from the full range. It only stops the hiding penalty firing from a blind spot.")]
        public bool compromiseOnlyWhenOnScreen = true;

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
        [Tooltip("Pull of a noise heard at the very EDGE of hearingRadius, relative to one at his feet. A sound only reaches him at all if it was in range, so distance must bias his interest, not veto it — at 0 the far two-thirds of his hearing did nothing at all.")]
        [Range(0f, 1f)] public float noiseFarWeight = 0.55f;
        [Tooltip("Multiplier on Investigate when a noise arrives that is NEWER than his last sighting, while he's mid-hunt. New information should outrank a belief map that's pointing at where you WERE — without this he ignores footsteps a metre away for the first 5s of a search.")]
        public float brainFreshNoiseBoost = 1.6f;
        [Tooltip("Bonus added to whatever he's currently doing — stops rapid flip-flopping between behaviors.")]
        public float brainStickiness = 0.1f;

        [Header("Patrol")]
        [Tooltip("How close to a waypoint counts as reached.")]
        public float waypointTolerance = 0.4f;
        [Tooltip("Idle pause at each waypoint (looking around).")]
        public float waypointPauseSeconds = 1.2f;
        [Tooltip("Degrees he sweeps his sight cone during that pause. A stopped maniac keeps whatever facing his last step gave him, so without this the 'look around' pause pointed his eyes at a wall the whole time.")]
        public float patrolScanAngle = 100f;
        [Tooltip("How fast the patrol pause sweeps — slower than the search sweep; he's calm, not hunting.")]
        public float patrolScanSpeed = 1.1f;
        [Tooltip("Anti-stuck: if he can't reach a waypoint within this many seconds (wedged on a pillar/corner), he gives up and moves to the next. Patrol must never hang.")]
        public float patrolWaypointTimeout = 5f;
        [Range(0f, 0.5f)]
        [Tooltip("Chance, at each waypoint, that he turns round and walks the loop the other way.\n\nThe route used to be (index + 1) % Count forever: same ring, same direction, every run. Horror-design research names enemy predictability as the primary fear-killer — once a player can predict the threat, atmosphere cannot bring the fear back, and a fixed loop is learnable in about two circuits.\n\n0.25 means he reverses roughly every fourth stop, which is often enough that you cannot bank on where he will be next and rare enough that he does not read as dithering. 0 restores the old fixed loop exactly.")]
        public float patrolReverseChance = 0.25f;
    }
}
