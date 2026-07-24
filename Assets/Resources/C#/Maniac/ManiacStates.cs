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
    }

    /// Walks the waypoint loop, pausing at each stop to "look around".
    public class PatrolState : ManiacStateBase
    {
        int waypointIndex;
        float pauseUntil;
        float waypointDeadline;   // give up on a waypoint he can't reach

        public PatrolState(ManiacController maniac) : base(maniac) { }

        public override void Enter()
        {
            waypointIndex = maniac.Route.ClosestIndex(maniac.Motor.Position);
            pauseUntil = 0f;
            waypointDeadline = Time.time + maniac.Config.patrolWaypointTimeout;
        }

        public override void Tick(float deltaTime)
        {
            var config = maniac.Config;
            if (Time.time < pauseUntil) { maniac.Nav.Stop(); return; }

            maniac.Nav.MoveTo(maniac.Route.Waypoint(waypointIndex), config.patrolSpeed);
            // Advance when reached OR when he's spent too long trying — a waypoint
            // wedged against a pillar/corner must never freeze the whole patrol.
            if (maniac.Nav.ReachedDestination(config.waypointTolerance) || Time.time >= waypointDeadline)
            {
                waypointIndex = (waypointIndex + 1) % Mathf.Max(1, maniac.Route.Count);
                pauseUntil = Time.time + config.waypointPauseSeconds;
                waypointDeadline = pauseUntil + config.patrolWaypointTimeout;
            }
        }
    }

    /// Heads to the last heard noise and lingers there. The brain decides when
    /// to leave (Investigate's utility fades as the noise gets old) — this state
    /// just does the moving. Always chases the freshest noise position, which
    /// perception keeps up to date.
    public class InvestigateState : ManiacStateBase
    {
        public InvestigateState(ManiacController maniac) : base(maniac) { }

        public override void Enter() => maniac.Perception.ConsumeNoise();

        public override void Tick(float deltaTime)
        {
            // Nav parks him on the spot once reached (velocity settles), which
            // reads as "stopped there, looking". Fresh noise -> the target moves.
            maniac.Nav.MoveTo(maniac.Perception.LastNoisePosition, maniac.Config.investigateSpeed);
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

        public SearchState(ManiacController maniac) : base(maniac) { }

        public override void Enter()
        {
            maniac.Perception.ConsumeNoise();   // this hunt answers the alert
            EnsureMap();
            belief?.Seed(maniac.Perception.LastSeenPosition, maniac.Perception.LastSeenDirection);
            nextStep = 0f;
            PickTarget();
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
            scanBase = maniac.Perception.FacingDirection.sqrMagnitude > 0.01f
                ? maniac.Perception.FacingDirection.normalized : Vector2.down;
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
                float sweep = Mathf.Sin(Time.time * maniac.Config.searchScanSpeed) * (maniac.Config.searchScanAngle * 0.5f);
                maniac.Perception.FacingDirection = Rotate(scanBase, sweep);
                if (Time.time >= lookUntil) PickTarget();
                return;
            }

            maniac.Nav.MoveTo(target, maniac.Config.searchSpeed);
            if (maniac.Nav.ReachedDestination(maniac.Config.waypointTolerance) || Time.time >= pointDeadline)
                BeginLook(Time.time + maniac.Config.searchLookSeconds);
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

        static Vector2 Rotate(Vector2 v, float degrees)
        {
            float r = degrees * Mathf.Deg2Rad;
            float c = Mathf.Cos(r), s = Mathf.Sin(r);
            return new Vector2(v.x * c - v.y * s, v.x * s + v.y * c);
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
