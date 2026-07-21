// The three movement states. Each state decides the motor's target velocity
// and when to hand control to a sibling state. Future states (Hiding,
// Exhausted, Hurt) plug in the same way.
using TimeKiller.Core;

namespace TimeKiller.Player
{
    public abstract class PlayerStateBase : IState
    {
        protected readonly PlayerController player;
        protected PlayerStateBase(PlayerController player) => this.player = player;

        public virtual void Enter() { }
        public virtual void Tick(float deltaTime) { }
        public virtual void FixedTick(float fixedDelta) { }
        public virtual void Exit() { }
    }

    public class IdleState : PlayerStateBase
    {
        public IdleState(PlayerController p) : base(p) { }

        public override void Enter() => player.Motor.SetTargetVelocity(UnityEngine.Vector2.zero);

        public override void Tick(float dt)
        {
            if (player.Input.MoveInput.sqrMagnitude > 0.01f)
                player.ChangeState(player.Input.RunHeld ? (IState)player.Run : player.Walk);
        }
    }

    public class WalkState : PlayerStateBase
    {
        public WalkState(PlayerController p) : base(p) { }

        public override void Tick(float dt)
        {
            var move = player.Input.MoveInput;
            if (move.sqrMagnitude < 0.01f) { player.ChangeState(player.Idle); return; }
            if (player.Input.RunHeld) { player.ChangeState(player.Run); return; }
            player.Motor.SetTargetVelocity(move * player.Config.walkSpeed);
        }
    }

    public class RunState : PlayerStateBase
    {
        public RunState(PlayerController p) : base(p) { }

        public override void Tick(float dt)
        {
            var move = player.Input.MoveInput;
            if (move.sqrMagnitude < 0.01f) { player.ChangeState(player.Idle); return; }
            if (!player.Input.RunHeld) { player.ChangeState(player.Walk); return; }
            player.Motor.SetTargetVelocity(move * player.Config.runSpeed);
            // Future: stamina drain hooks in here (see time-killer design: stamina system).
        }
    }
}
