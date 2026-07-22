// Events the maniac publishes on the Core EventBus. UI, audio (chase music!),
// and the future sanity system subscribe to these — nobody references him.
namespace TimeKiller.Maniac
{
    /// Fired when the AI state machine changes state (Patrol/Investigate/Chase/Attack).
    public struct ManiacStateChangedEvent
    {
        public string StateName;
    }

    /// Fired when a footstep was loud enough and close enough to be heard.
    public struct ManiacHeardNoiseEvent
    {
        public UnityEngine.Vector2 NoisePosition;
    }

    /// Fired the moment line-of-sight on the player is established (the "spotted!" sting).
    public struct ManiacSpottedPlayerEvent
    {
        public UnityEngine.Vector2 PlayerPosition;
    }

    /// Fired when he swings at the player (hit or not — the swing itself).
    public struct ManiacAttackEvent
    {
        public UnityEngine.Vector2 Position;
    }
}
