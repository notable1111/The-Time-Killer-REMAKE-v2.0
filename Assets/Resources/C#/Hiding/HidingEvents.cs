// Events of the hiding system. The maniac, audio and VFX subscribe — nobody
// references the hiding feature directly.
namespace TimeKiller.Hiding
{
    /// Fired when the player slips into a hiding spot.
    public struct PlayerHidEvent
    {
        public UnityEngine.Vector2 SpotPosition;
    }

    /// Fired when the player leaves a spot (voluntarily or dragged out).
    public struct PlayerUnhidEvent
    {
        public UnityEngine.Vector2 SpotPosition;
    }
}
