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

        public PatrolState(ManiacController maniac) : base(maniac) { }

        public override void Enter()
        {
            waypointIndex = maniac.Route.ClosestIndex(maniac.Motor.Position);
            pauseUntil = 0f;
        }

        public override void Tick(float deltaTime)
        {
            var config = maniac.Config;
            if (Time.time < pauseUntil) { maniac.Nav.Stop(); return; }

            maniac.Nav.MoveTo(maniac.Route.Waypoint(waypointIndex), config.patrolSpeed);
            if (maniac.Nav.ReachedDestination(config.waypointTolerance))
            {
                waypointIndex = (waypointIndex + 1) % Mathf.Max(1, maniac.Route.Count);
                pauseUntil = Time.time + config.waypointPauseSeconds;
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

            // Losing sight is no longer a hard transition — the brain lowers
            // Chase's utility as time-since-seen grows (generous valve) and Search
            // takes over once the trail is truly cold. Here he just keeps pursuing.
            var target = maniac.Breadcrumbs.NextTarget(maniac.Motor.Position, config.waypointTolerance)
                         ?? perception.LastSeenPosition;
            maniac.Motor.MoveTo(target, config.chaseSpeed);
        }

        public override void Exit() => maniac.Breadcrumbs.Clear();
    }

    /// Lost you. Instead of snapping back to patrol, he HUNTS: checks where
    /// you vanished, then sweeps the nearest patrol waypoints, pausing to scan
    /// his sight cone at each. Re-acquire sight -> Chase; fresh noise ->
    /// Investigate; run out of spots -> give up to Patrol. The servant passage
    /// isn't on the patrol route, so it stays a genuine blind-spot escape.
    public class SearchState : ManiacStateBase
    {
        readonly List<Vector2> points = new List<Vector2>();
        int index;
        float pointDeadline;
        float lookUntil;
        bool looking;
        Vector2 scanBase;

        public SearchState(ManiacController maniac) : base(maniac) { }

        public override void Enter()
        {
            maniac.Perception.ConsumeNoise();   // this hunt answers the alert
            BuildPoints();
            index = 0;
            GoToCurrent();
        }

        void BuildPoints()
        {
            points.Clear();
            Vector2 origin = maniac.Perception.LastSeenPosition;
            points.Add(origin);                 // where they vanished

            var route = maniac.Route;
            int extra = Mathf.Max(0, maniac.Config.searchPoints - 1);
            if (route != null && route.Count > 0 && extra > 0)
            {
                var nearest = Enumerable.Range(0, route.Count)
                    .OrderBy(i => Vector2.Distance(origin, route.Waypoint(i)))
                    .Take(extra);
                foreach (int i in nearest) points.Add(route.Waypoint(i));
            }
        }

        void GoToCurrent()
        {
            looking = false;
            if (index >= points.Count)
            {
                // Out of spots: scan in place until the brain pulls him out
                // (Search's utility decays, then Patrol or a new stimulus wins).
                looking = true;
                lookUntil = float.MaxValue;
                maniac.Nav.Stop();
                scanBase = maniac.Perception.FacingDirection.sqrMagnitude > 0.01f
                    ? maniac.Perception.FacingDirection.normalized : Vector2.down;
                return;
            }
            maniac.Nav.MoveTo(points[index], maniac.Config.searchSpeed);
            pointDeadline = Time.time + maniac.Config.searchTravelTimeout;
        }

        public override void Tick(float deltaTime)
        {
            if (looking)
            {
                maniac.Nav.Stop();
                // Sweep the sight cone side-to-side so a peeking player is caught.
                float sweep = Mathf.Sin(Time.time * maniac.Config.searchScanSpeed) * (maniac.Config.searchScanAngle * 0.5f);
                maniac.Perception.FacingDirection = Rotate(scanBase, sweep);
                if (Time.time >= lookUntil) { index++; GoToCurrent(); }
                return;
            }

            maniac.Nav.MoveTo(points[index], maniac.Config.searchSpeed);
            if (maniac.Nav.ReachedDestination(maniac.Config.waypointTolerance) || Time.time >= pointDeadline)
            {
                looking = true;
                lookUntil = Time.time + maniac.Config.searchLookSeconds;
                maniac.Nav.Stop();
                // Scan around whatever way he's currently facing.
                scanBase = maniac.Perception.FacingDirection.sqrMagnitude > 0.01f
                    ? maniac.Perception.FacingDirection.normalized
                    : Vector2.down;
            }
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
