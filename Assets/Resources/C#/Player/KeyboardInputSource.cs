// Keyboard implementation of IInputSource: WASD to move, Shift to run.
// Uses the new Input System so gamepad support later is a sibling class,
// not a rewrite.
using UnityEngine;
using UnityEngine.InputSystem;

namespace TimeKiller.Player
{
    public class KeyboardInputSource : MonoBehaviour, IInputSource
    {
        public Vector2 MoveInput { get; private set; }
        public bool RunHeld { get; private set; }
        public bool InteractPressed { get; private set; }
        public bool SkillCheckPressed { get; private set; }

        void Update()
        {
            var kb = Keyboard.current;
            if (kb == null) { MoveInput = Vector2.zero; RunHeld = false; InteractPressed = false; SkillCheckPressed = false; return; }
            InteractPressed = kb.eKey.wasPressedThisFrame;
            SkillCheckPressed = kb.spaceKey.wasPressedThisFrame;

            var move = new Vector2(
                (kb.dKey.isPressed ? 1f : 0f) - (kb.aKey.isPressed ? 1f : 0f),
                (kb.wKey.isPressed ? 1f : 0f) - (kb.sKey.isPressed ? 1f : 0f));

            // Normalize so diagonal movement isn't faster than straight movement.
            MoveInput = move.sqrMagnitude > 1f ? move.normalized : move;
            RunHeld = kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed;
        }
    }
}
