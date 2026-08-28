// What composure says out loud.
//
// The Sanity feature has exactly ONE mechanical output (it makes you louder, via
// Core's PlayerNoiseDial) and this event is everything else. It exists so the
// audio and visual lanes can present low composure on their own terms — a
// tightening in the score, a grain ramp, a change in how the world sounds —
// WITHOUT this feature reaching into theirs.
//
// That separation is deliberate and was the hardest constraint in the design.
// The obvious way to make sanity felt is to push it into the heartbeat and the
// breathing, and both of those are FINISHED features whose numbers were settled
// by ear. CLAUDE.md §3 exists because the fear system was once retuned unprompted
// from a single bot session. A feature that announces and lets others subscribe
// cannot commit that mistake; a feature that writes into their configs can.
namespace TimeKiller.Sanity
{
    /// Fired when composure has moved meaningfully, and on every restore.
    public struct ComposureChangedEvent
    {
        /// 1 = composed, 0 = spent. Never reaches 0 in practice — the config's
        /// floor holds it above that on purpose.
        public float Composure;
        /// Whether the player is currently in what the config calls total
        /// darkness. Carried because "why is it falling" is the first question
        /// any listener or debugger asks, and re-deriving it needs the light
        /// sampler.
        public bool InDarkness;
        /// The multiplier currently applied to the player's noise. 1 = normal.
        public float LoudnessMultiplier;
    }
}
