// Minimal reusable state machine. Any feature with modes (player movement,
// enemy AI, game flow) creates IState classes and drives them through this.
using System;

namespace TimeKiller.Core
{
    public interface IState
    {
        void Enter();
        void Tick(float deltaTime);       // called from Update
        void FixedTick(float fixedDelta); // called from FixedUpdate (physics)
        void Exit();
    }

    public class StateMachine
    {
        public IState Current { get; private set; }
        public event Action<IState, IState> StateChanged; // (from, to)

        public void ChangeState(IState next)
        {
            if (next == Current) return;
            var previous = Current;
            previous?.Exit();
            Current = next;
            next?.Enter();
            StateChanged?.Invoke(previous, next);
        }

        public void Tick(float deltaTime) => Current?.Tick(deltaTime);
        public void FixedTick(float fixedDelta) => Current?.FixedTick(fixedDelta);
    }
}
