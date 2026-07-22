// The maniac's brain: Patrol -> (noise) Investigate -> (sight) Chase -> Attack.
// Chase follows a breadcrumb trail of the player's recent positions — the
// player always came from somewhere reachable, so following the trail steers
// him through doorways without pathfinding. Losing sight starts the GENEROUS
// lose-sight timer (the balancing valve for his faster-than-run speed).
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

        // Sight or a fresh noise interrupts any non-chase state.
        protected bool TryEscalate()
        {
            if (maniac.Perception.CanSeePlayer)
            {
                maniac.ChangeState(maniac.Chase);
                return true;
            }
            if (maniac.Perception.HasUnhandledNoise)
            {
                maniac.ChangeState(maniac.Investigate);
                return true;
            }
            return false;
        }
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
            if (TryEscalate()) return;

            var config = maniac.Config;
            if (Time.time < pauseUntil) { maniac.Motor.Stop(); return; }

            maniac.Motor.MoveTo(maniac.Route.Waypoint(waypointIndex), config.patrolSpeed);
            if (maniac.Motor.ReachedDestination(config.waypointTolerance))
            {
                waypointIndex = (waypointIndex + 1) % Mathf.Max(1, maniac.Route.Count);
                pauseUntil = Time.time + config.waypointPauseSeconds;
            }
        }
    }

    /// Heads to the last heard noise and lingers there before giving up.
    public class InvestigateState : ManiacStateBase
    {
        float giveUpAt;
        bool arrived;

        public InvestigateState(ManiacController maniac) : base(maniac) { }

        public override void Enter()
        {
            maniac.Perception.ConsumeNoise();
            arrived = false;
            giveUpAt = float.MaxValue;
        }

        public override void Tick(float deltaTime)
        {
            var perception = maniac.Perception;
            if (perception.CanSeePlayer) { maniac.ChangeState(maniac.Chase); return; }
            if (perception.HasUnhandledNoise) { Enter(); } // fresher noise wins

            var config = maniac.Config;
            maniac.Motor.MoveTo(perception.LastNoisePosition, config.investigateSpeed);

            if (!arrived && maniac.Motor.ReachedDestination(config.waypointTolerance))
            {
                arrived = true;
                maniac.Motor.Stop();
                giveUpAt = Time.time + config.investigateSeconds;
            }
            if (Time.time >= giveUpAt)
                maniac.ChangeState(maniac.Patrol);
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

            // The generous valve: only give up well after losing sight.
            if (!perception.CanSeePlayer && perception.TimeSinceSeen > config.loseSightSeconds && maniac.Breadcrumbs.IsEmpty)
            {
                perception.ConsumeNoise();               // chase already answered it
                maniac.ChangeState(maniac.Investigate);  // search the last seen spot
                return;
            }

            var target = maniac.Breadcrumbs.NextTarget(maniac.Motor.Position, config.waypointTolerance)
                         ?? perception.LastSeenPosition;
            maniac.Motor.MoveTo(target, config.chaseSpeed);
        }

        public override void Exit() => maniac.Breadcrumbs.Clear();
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
