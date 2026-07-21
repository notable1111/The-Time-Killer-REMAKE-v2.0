// Tracks which of the 8 directions the character is facing based on movement
// input, and announces changes on the EventBus (animation listens to this).
// With keyboard input the vector only ever points at the 8 exact octants, so
// direct angle mapping is stable — no hysteresis needed. If analog sticks come
// later (co-op), revisit with a small angular deadzone at octant borders.
using TimeKiller.Core;
using UnityEngine;

namespace TimeKiller.Player
{
    public class PlayerFacing : MonoBehaviour
    {
        public FacingDirection Current { get; private set; } = FacingDirection.Down;

        public void UpdateFromInput(Vector2 move)
        {
            if (move.sqrMagnitude < 0.01f) return; // keep last facing when idle

            // Angle from straight Down, clockwise 0..360 — matches the enum order
            // (Down, DownLeft, Left, ...), which goes clockwise on screen.
            float angle = -Vector2.SignedAngle(Vector2.down, move);
            if (angle < 0f) angle += 360f;

            // Each octant is 45° wide, centered on its direction.
            var next = (FacingDirection)(Mathf.RoundToInt(angle / 45f) % 8);

            if (next == Current) return;
            Current = next;
            EventBus.Publish(new PlayerFacingChangedEvent { Direction = next });
        }
    }
}
