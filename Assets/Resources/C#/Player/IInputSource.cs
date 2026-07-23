// Contract between "who is controlling" and "what is being controlled".
// The character only ever reads this interface — swapping keyboard for a
// gamepad (co-op player 2) or an AI driver requires zero character changes.
using UnityEngine;

namespace TimeKiller.Player
{
    public interface IInputSource
    {
        Vector2 MoveInput { get; } // normalized direction, zero when no input
        bool RunHeld { get; }
        bool InteractPressed { get; } // true on the frame the interact key goes down (E)
        bool SkillCheckPressed { get; } // true on the frame the skill-check key goes down (Space) — clock repair
    }
}
