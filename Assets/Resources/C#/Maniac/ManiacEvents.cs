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
    /// Blood is the third, and it is not a sound at all — it is something he
    /// SEES on the floor by standing near it. It shares this channel because the
    /// consequence is identical (go and look over there) and because a separate
    /// channel would have meant a second copy of every investigate transition.
    /// ⚠️ TestTelemetry currently counts anything that is not Suspicion as
    /// HeardSound; that must be split before blood tracking is A/B'd, or the
    /// measurement will attribute floor-reading to hearing.
    public enum NoiseCause { Sound, Suspicion, Blood }

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

    /// He has just got worse — the run's objective moved on and he changed with
    /// it. Announced rather than merely computed so audio, telemetry and the
    /// session recorder can mark the beat, and so "did he actually escalate?" is
    /// a line in a log instead of an impression of how the run felt.
    public struct ManiacEscalatedEvent
    {
        /// Objectives done, and out of how many.
        public int Step;
        public int Total;
        /// 0..1 — where he is heading. This is the TARGET, not the blended live
        /// value: the event fires on the beat, and the change arrives over the
        /// next few seconds (ManiacEscalationConfig.blendSeconds).
        public float Intensity;
    }

    // ---- Messages the maniac ACCEPTS. Everything above is something he says;
    // these two are things said to him. They are declared here rather than in the
    // Director feature that sends them so the Director stays deletable: with these
    // in Director, removing that folder would stop the maniac compiling, which is
    // exactly the coupling the "cleanly removable" rule exists to prevent.

    /// "Go and look over there." A REGION, never a position.
    ///
    /// Modelled on Alien: Isolation's two-brain split: a director that knows where
    /// the player is steers the creature toward their AREA and never hands over
    /// the location, so the creature still has to see or hear them. The Area here
    /// is deliberately smeared by DirectorConfig.hintError — if that error ever
    /// shrinks below his sight range, arriving at a hint becomes finding the
    /// player and the whole thing has quietly become a cheat.
    ///
    /// Nothing that handles this may write awareness, LastSeenPosition or belief.
    public struct ManiacHintEvent
    {
        public UnityEngine.Vector2 Area;
        public float Radius;
        public bool Desperate;
    }

    /// The player has taught him something. Unlocked by what they DO (hiding
    /// successfully), never by dying — punishing failure is the one thing that
    /// reads as unfair.
    public struct ManiacLearnedEvent
    {
        /// Added to the wardrobe check chance.
        public float WardrobeBonus;
        public int SuccessfulHides;
    }
}
