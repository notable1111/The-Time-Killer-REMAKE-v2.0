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

        /// Re-issue the motor command from whatever MoveInput says RIGHT NOW.
        ///
        /// Called from both Tick and FixedTick. For a human this is a no-op on the
        /// second call — the keyboard is polled in Update, so the value cannot
        /// change between two render frames and the target velocity is identical.
        /// It matters for a SCRIPTED driver, whose heading is recomputed every
        /// physics step: without this the motor would be steered by a command up
        /// to a whole render frame old, which at 4x timeScale is 3.3 physics steps
        /// of driving in a stale direction. The maniac never had this problem —
        /// ManiacMotor re-solves its heading inside its own FixedUpdate — and that
        /// asymmetry is what made accelerated playtests unreadable.
        ///
        /// Deciding a physics command on the physics clock is also just where it
        /// belongs; state TRANSITIONS deliberately stay in Tick.
        protected void DriveMotor(float speed) =>
            player.Motor.SetTargetVelocity(player.Input.MoveInput * speed * player.SpeedMultiplier);
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
            DriveMotor(player.Config.walkSpeed);
        }

        public override void FixedTick(float fixedDelta) => DriveMotor(player.Config.walkSpeed);
    }

    public class RunState : PlayerStateBase
    {
        public RunState(PlayerController p) : base(p) { }

        public override void Tick(float dt)
        {
            var move = player.Input.MoveInput;
            if (move.sqrMagnitude < 0.01f) { player.ChangeState(player.Idle); return; }
            if (!player.Input.RunHeld) { player.ChangeState(player.Walk); return; }
            // Adrenaline (post-hit) can push this past the maniac's chase speed —
            // the escape window is outrunning him, not him stopping.
            DriveMotor(player.Config.runSpeed);
            // Future: stamina drain hooks in here (see time-killer design: stamina system).
        }

        public override void FixedTick(float fixedDelta) => DriveMotor(player.Config.runSpeed);
    }
}
