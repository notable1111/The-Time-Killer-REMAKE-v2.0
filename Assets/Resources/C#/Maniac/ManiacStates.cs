// The maniac's brain: Patrol -> (noise) Investigate -> (sight) Chase -> Attack.
// Chase follows a breadcrumb trail of the player's recent positions — the
// player always came from somewhere reachable, so following the trail steers
// him through doorways without pathfinding. Losing sight starts the GENEROUS
// lose-sight timer (the balancing valve for his faster-than-run speed).
using System.Collections.Generic;
using System.Linq;
using TimeKiller.Core;
using UnityEngine;

namespace TimeKiller.Maniac
{
    public abstract class ManiacStateBase : IState
    {
        protected readonly ManiacController maniac;
        protected ManiacStateBase(ManiacController maniac) => this.maniac = maniac;
        public virtual void Enter() { }
        public virtual void Tick(float deltaTime) { }
        public virtual void FixedTick(float fixedDelta) { }
        public virtual void Exit() { }

        /// Sweep his sight cone side-to-side around a base direction.
        ///
        /// ManiacController only writes FacingDirection while he is MOVING, so a
        /// stopped maniac keeps aiming wherever his last step pointed him. Any
        /// state that stops him must therefore aim him itself — otherwise his
        /// "pause and look around" is spent staring at whatever wall he walked at.
        protected void SweepCone(Vector2 baseDir, float totalAngle, float speed)
        {
            if (baseDir.sqrMagnitude < 0.01f) baseDir = Vector2.down;
            float sweep = Mathf.Sin(Time.time * speed) * (totalAngle * 0.5f);
            maniac.Perception.FacingDirection = Rotate(baseDir.normalized, sweep);
        }

        /// His current aim, safe to use as a sweep centre.
        protected Vector2 CurrentFacing()
        {
            var f = maniac.Perception.FacingDirection;
            return f.sqrMagnitude > 0.01f ? f.normalized : Vector2.down;
        }

        protected static Vector2 Rotate(Vector2 v, float degrees)
        {
            float r = degrees * Mathf.Deg2Rad;
            float c = Mathf.Cos(r), s = Mathf.Sin(r);
            return new Vector2(v.x * c - v.y * s, v.x * s + v.y * c);
        }
    }

    /// Walks the waypoint loop, pausing at each stop to "look around".
    public class PatrolState : ManiacStateBase
    {
        int waypointIndex;
        float pauseUntil;
        float waypointDeadline;   // give up on a waypoint he can't reach
        Vector2 scanBase;         // centre of the look-around sweep at a stop

        public PatrolState(ManiacController maniac) : base(maniac) { }

        public override void Enter()
        {
            waypointIndex = maniac.Route.ClosestIndex(maniac.Motor.Position);
            pauseUntil = 0f;
            scanBase = CurrentFacing();
            waypointDeadline = Time.time + maniac.Config.patrolWaypointTimeout;
        }

        public override void Tick(float deltaTime)
        {
            var config = maniac.Config;
            // The pause is meant to BE the looking around, so actually turn his
            // head — a frozen cone here made half his patrol time blind.
            if (Time.time < pauseUntil)
            {
                maniac.Nav.Stop();
                SweepCone(scanBase, config.patrolScanAngle, config.patrolScanSpeed);
                return;
            }

            maniac.Nav.MoveTo(maniac.Route.Waypoint(waypointIndex), config.patrolSpeed);
            // Advance when reached OR when he's spent too long trying — a waypoint
            // wedged against a pillar/corner must never freeze the whole patrol.
            if (maniac.Nav.ReachedDestination(config.waypointTolerance) || Time.time >= waypointDeadline)
            {
                waypointIndex = (waypointIndex + 1) % Mathf.Max(1, maniac.Route.Count);
                pauseUntil = Time.time + config.waypointPauseSeconds;
                scanBase = CurrentFacing();
                waypointDeadline = pauseUntil + config.patrolWaypointTimeout;
            }
        }
    }

    /// Heads to the last heard noise, then SCANS it. The brain decides when to
    /// leave (Investigate's utility fades as the noise gets old) — this state
    /// decides where to look. Always chases the freshest noise position, which
    /// perception keeps up to date, so a player who keeps moving keeps pulling him.
    public class InvestigateState : ManiacStateBase
    {
        bool arrived;
        Vector2 checkedSpot;   // the noise position he actually walked to
        Vector2 scanBase;
        public InvestigateState(ManiacController maniac) : base(maniac) { }

        public override void Enter()
        {
            maniac.Perception.ConsumeNoise();
            arrived = false;
        }

        public override void Tick(float deltaTime)
        {
            var config = maniac.Config;
            var perception = maniac.Perception;
            var noise = perception.LastNoisePosition;

            // A heard footstep he simply walks to. A SUSPICION he stops dead for
            // first — that pause is the tell (his footsteps cut out) and it is the
            // window in which backing away still works. Recomputed every frame from
            // the episode's own start time, never latched.
            bool suspicion = perception.LastNoiseCause == NoiseCause.Suspicion;
            if (suspicion && Time.time < perception.SuspicionStartedTime + config.suspicionHoldSeconds)
            {
                maniac.Nav.Stop();
                // A stare, not a sweep: he has one spot in mind and is looking
                // straight at it. The sweeping only starts once he gets there.
                Vector2 toGuess = noise - maniac.Motor.Position;
                if (toGuess.sqrMagnitude > 0.01f)
                    maniac.Perception.FacingDirection = toGuess.normalized;
                return;
            }

            // Suspicion closes SLOWER than the player walks; a real noise does not.
            float approachSpeed = suspicion ? config.suspiciousApproachSpeed : config.investigateSpeed;

            // A newer noise elsewhere restarts the approach — perception keeps
            // LastNoisePosition current, so he follows a player who keeps moving.
            if (arrived && Vector2.Distance(noise, checkedSpot) > config.waypointTolerance)
                arrived = false;

            if (!arrived)
            {
                maniac.Nav.MoveTo(noise, approachSpeed);
                if (maniac.Nav.ReachedDestination(config.waypointTolerance))
                {
                    arrived = true;
                    checkedSpot = noise;
                    scanBase = CurrentFacing();
                }
                return;
            }

            // Standing ON the noise means nothing if his eyes stay pointed the way
            // he arrived. Sweep the cone here exactly as Search does — this is the
            // beat where he decides whether anything is actually there.
            maniac.Nav.Stop();
            SweepCone(scanBase, config.searchScanAngle, config.searchScanSpeed);
        }
    }

    /// Full-speed pursuit along the player's breadcrumb trail.
    public class ChaseState : ManiacStateBase
    {
        public ChaseState(ManiacController maniac) : base(maniac) { }

        public override void Enter() => maniac.Breadcrumbs.Clear();

        public override void Tick(float deltaTime)
        {
            var config = maniac.Config;
            var perception = maniac.Perception;

            // Compromised wardrobe: he watched them hide — march to the spot
            // and drag the hit out. Sight rules don't apply to a known spot.
            if (maniac.CompromisedSpot.HasValue)
            {
                var spot = maniac.CompromisedSpot.Value;
                if (Time.time >= maniac.NextAttackAllowed &&
                    Vector2.Distance(maniac.Motor.Position, spot) <= config.attackRange)
                {
                    maniac.ChangeState(maniac.Attack);
                    return;
                }
                maniac.Nav.MoveTo(spot, config.chaseSpeed);
                return;
            }

            if (perception.CanSeePlayer)
                maniac.Breadcrumbs.Record(perception.LastSeenPosition);

            // In range, visible, and off cooldown -> swing. During cooldown he
            // KEEPS CHASING at full speed — no free pause for the player.
            if (perception.CanSeePlayer &&
                Time.time >= maniac.NextAttackAllowed &&
                Vector2.Distance(maniac.Motor.Position, perception.LastSeenPosition) <= config.attackRange)
            {
                maniac.ChangeState(maniac.Attack);
                return;
            }

            // MOVEMENT. If he can SEE you, line of sight is clear by definition —
            // beeline straight at you (fast, relentless, no hesitation). The moment
            // a wall comes between you (sight lost), PATH AROUND it via the A* grid
            // instead of sliding along it: he follows your breadcrumb trail through
            // the doorways you took, not into the wall. This is what stops the
            // "chases stupidly around walls" look — chase now navigates like search.
            if (perception.CanSeePlayer)
            {
                maniac.Motor.MoveTo(perception.LastSeenPosition, config.chaseSpeed);
            }
            else
            {
                var target = maniac.Breadcrumbs.NextTarget(maniac.Motor.Position, config.waypointTolerance)
                             ?? perception.LastSeenPosition;
                maniac.Nav.MoveTo(target, config.chaseSpeed);
            }
        }

        public override void Exit() => maniac.Breadcrumbs.Clear();
    }

    /// Lost you. Instead of snapping back to patrol, he HUNTS with a belief map
    /// (PlayerBeliefMap): probability seeded at your last-seen spot, biased the
    /// way you fled, spreading along corridors and COLLAPSING wherever he looks
    /// and doesn't find you. He paths to the likeliest cell, scans, and never
    /// re-checks cleared areas. The brain decides WHEN to give up (Search utility
    /// decays); this state decides WHERE. The servant passage stays a real escape
    /// (it's walkable so belief can flow there, but he only goes if it's likeliest).
    public class SearchState : ManiacStateBase
    {
        PlayerBeliefMap belief;
        Vector2 target;
        float pointDeadline;
        float lookUntil;
        float nextStep;
        bool looking;
        Vector2 scanBase;

        // Wardrobe checking. Optional: with no ManiacWardrobeSearch component on
        // the maniac these stay null/false and the state behaves exactly as it
        // did before hiding spots were searchable.
        ManiacWardrobeSearch search;
        TimeKiller.Hiding.HidingSpot checkingSpot;
        bool atDoor;

        public SearchState(ManiacController maniac) : base(maniac) { }

        public override void Enter()
        {
            maniac.Perception.ConsumeNoise();   // this hunt answers the alert
            EnsureMap();
            if (search == null) search = maniac.GetComponent<ManiacWardrobeSearch>();
            belief?.Seed(maniac.Perception.LastSeenPosition, maniac.Perception.LastSeenDirection);
            nextStep = 0f;
            checkingSpot = null;
            atDoor = false;
            PickTarget();
        }

        public override void Exit()
        {
            checkingSpot = null;
            atDoor = false;
        }

        void EnsureMap()
        {
            if (belief != null || maniac.Nav == null || !maniac.Nav.Ready) return;
            belief = new PlayerBeliefMap(maniac.Nav.WorldBounds, 1f, maniac.Nav.IsWalkable);
        }

        void PickTarget()
        {
            // Stride to a belief spot a few units out (not the adjacent cell), so he
            // hunts in confident strides and ranges outward toward where you fled.
            if (belief != null && belief.BestTargetBeyond(maniac.Motor.Position, 3f, out var t))
            {
                looking = false;
                target = t;
                checkingSpot = null;
                atDoor = false;

                // A wardrobe sitting where he was already headed is worth opening
                // on the way. He walks to the DOOR rather than the belief cell —
                // which is what finally gives the hidden player's heartbeat
                // something to climb toward.
                if (search != null && search.Ready && search.TryPickSpot(t, out var spot))
                {
                    checkingSpot = spot;
                    target = spot.transform.position;
                }

                maniac.Nav.MoveTo(target, maniac.Config.searchSpeed);
                pointDeadline = Time.time + maniac.Config.searchTravelTimeout;
            }
            else
            {
                // Belief exhausted (checked everywhere likely) — scan in place
                // until the brain gives up (Search utility decays -> Patrol).
                BeginLook(float.MaxValue);
            }
        }

        void BeginLook(float until)
        {
            looking = true;
            lookUntil = until;
            maniac.Nav.Stop();
            scanBase = CurrentFacing();
        }

        public override void Tick(float deltaTime)
        {
            // Keep the belief field alive: spread it, and collapse whatever he
            // can currently see (confirmed-empty cells drop to zero).
            if (belief != null && Time.time >= nextStep)
            {
                nextStep = Time.time + 0.25f;
                belief.Step();
                belief.Observe(maniac.Motor.Position, maniac.Perception.FacingDirection,
                    maniac.Config.sightRange, maniac.Config.sightConeAngle * 0.5f, HasLineOfSight);
            }

            if (looking)
            {
                maniac.Nav.Stop();
                // Sweep the sight cone side-to-side so a peeking player is caught.
                SweepCone(scanBase, maniac.Config.searchScanAngle, maniac.Config.searchScanSpeed);
                if (Time.time >= lookUntil)
                {
                    if (atDoor) ResolveDoor();
                    else PickTarget();
                }
                return;
            }

            maniac.Nav.MoveTo(target, maniac.Config.searchSpeed);
            if (maniac.Nav.ReachedDestination(maniac.Config.waypointTolerance) || Time.time >= pointDeadline)
            {
                // Arrived at a wardrobe he meant to check: hold at the door for
                // the dread beat before deciding. He is roughly a unit away here,
                // so a hidden player's heartbeat is at its loudest — the pause is
                // the beat, not dead time.
                if (checkingSpot != null) { atDoor = true; BeginLook(Time.time + search.Config.dreadPauseSeconds); return; }
                BeginLook(Time.time + maniac.Config.searchLookSeconds);
            }
        }

        // The door beat resolves. He opens it only where the belief map is
        // genuinely concentrated, so a player who was never sensed is never
        // found; otherwise he moves on and does not come back for a while.
        void ResolveDoor()
        {
            var spot = checkingSpot;
            checkingSpot = null;
            atDoor = false;

            if (spot == null || search == null) { PickTarget(); return; }
            search.MarkChecked(spot);

            if (search.ShouldOpen(spot, belief) && spot.Occupied)
            {
                // Caught. Same drag-out path as being watched climbing in.
                maniac.CompromiseSpot(spot.transform.position);
                return;
            }

            PickTarget();
        }

        // Physics line of sight for the belief map's Observe (walls block).
        bool HasLineOfSight(Vector2 a, Vector2 b)
        {
            foreach (var hit in Physics2D.LinecastAll(a, b, maniac.Config.sightBlockers))
            {
                if (hit.collider == null || hit.collider.isTrigger) continue;
                if (hit.collider.transform.root == maniac.transform.root) continue;
                return false;
            }
            return true;
        }
    }

    /// One swing + a BRIEF recovery, then straight back to the chase. The
    /// swing cooldown runs while chasing (gated in ChaseState) — he never
    /// stands around; the player's escape is the post-hit adrenaline burst.
    public class AttackState : ManiacStateBase
    {
        float recoverUntil;
        bool swung;

        public AttackState(ManiacController maniac) : base(maniac) { }

        public override void Enter()
        {
            maniac.Motor.Stop();
            swung = false;
            recoverUntil = Time.time + maniac.Config.attackRecoverySeconds;
            maniac.NextAttackAllowed = Time.time + maniac.Config.attackCooldown;
        }

        public override void Tick(float deltaTime)
        {
            if (!swung)
            {
                swung = true;
                EventBus.Publish(new ManiacAttackEvent { Position = maniac.Motor.Position });
                if (Vector2.Distance(maniac.Motor.Position, maniac.PlayerPosition) <= maniac.Config.attackRange * 1.25f)
                {
                    EventBus.Publish(new TimeKiller.Player.PlayerHitEvent
                    {
                        Damage = maniac.Config.damage,
                        SourcePosition = maniac.Motor.Position,
                    });
                    maniac.BeginPhaseThrough(); // shove + adrenaline + slip-through = the escape
                }
            }
            if (Time.time >= recoverUntil)
                maniac.ChangeState(maniac.Chase); // right back on the hunt
        }
    }
}
