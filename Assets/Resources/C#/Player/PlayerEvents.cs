// Events the player publishes on the Core EventBus. Other systems (UI, audio,
// enemies, future stamina) subscribe to these instead of referencing the player.
namespace TimeKiller.Player
{
    // 8 directions, counter-clockwise from Down. Index order matters: animation
    // sets store their clips in arrays indexed by (int)FacingDirection.
    public enum FacingDirection
    {
        Down, DownLeft, Left, UpLeft, Up, UpRight, Right, DownRight
    }

    /// Fired when the movement state machine changes state (Idle/Walk/Run).
    public struct PlayerStateChangedEvent
    {
        public string StateName;
    }

    /// Fired when the character turns to face a new direction (drives animation).
    public struct PlayerFacingChangedEvent
    {
        public FacingDirection Direction;
    }

    /// Fired on each foot-contact animation frame while moving. Loudness is the
    /// stealth-relevant noise level — enemies will use this to hear the player.
    public struct PlayerFootstepEvent
    {
        public UnityEngine.Vector2 Position;
        public bool IsRunning;
        public float Loudness; // 0..1
    }

    /// Published by anything that damages the player (maniac attack, future
    /// hazards). PlayerHealth consumes it — attackers never touch the player.
    public struct PlayerHitEvent
    {
        public int Damage;
        public UnityEngine.Vector2 SourcePosition; // shove pushes away from here
    }

    /// Fired whenever health changes (UI, audio, sanity system subscribe).
    public struct PlayerHealthChangedEvent
    {
        public int Current;
        public int Max;
    }

    /// Fired on death (v1: followed by an immediate respawn — placeholder rule).
    public struct PlayerDiedEvent
    {
        public UnityEngine.Vector2 Position;
    }
}
