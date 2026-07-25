// Events the maniac publishes on the Core EventBus. UI, audio (chase music!),
// and the future sanity system subscribe to these — nobody references him.
namespace TimeKiller.Maniac
{
    /// Fired when the AI state machine changes state (Patrol/Investigate/Chase/Attack).
    public struct ManiacStateChangedEvent
    {
        public string StateName;
    }

    /// Why the noise channel fired. Both cases drive Investigate identically,
    /// so gameplay never needs to look — but they are different phenomena, and
    /// the playtest telemetry counted them as ONE number it could not split.
    /// Sound is the player being audible; Suspicion is his own sight meter
    /// flickering. A run reporting "heard 50 noises" was unreadable without it.
    public enum NoiseCause { Sound, Suspicion }

    /// Fired when a footstep was loud enough and close enough to be heard, or
    /// when sight-driven suspicion first flickers up. See NoiseCause. Default
    /// is Sound, so a publisher that ignores the field stays correct.
    public struct ManiacHeardNoiseEvent
    {
        public UnityEngine.Vector2 NoisePosition;
        public NoiseCause Cause;
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
