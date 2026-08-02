// Fear, published so every channel can answer to ONE number.
//
// Before this, each tension channel computed its own idea of danger: the heart
// ran a distance curve with state FLOORS bolted on, breathing ran an exertion
// model off the chase flag, and the world duck ran off the heart's intensity.
// Three opinions, three curves, and a set of hard floors that made the heart
// jump from 88 to 150 bpm the instant an awareness flag flipped — audible as a
// step, which is exactly what a fear system must never be.
//
// Now FearConductor owns one smoothed 0..1 value and announces it. Anything that
// wants to escalate subscribes. No feature references another, and deleting the
// conductor leaves every subscriber running on its own fallback.
using UnityEngine;

namespace TimeKiller.Fear
{
    /// The five beats of the emotional loop. Named states are for READING the
    /// system (debug, telemetry, a future music cue) — never for driving audio,
    /// because a stage boundary is a step and steps are what this replaced.
    public enum FearStage { Safe = 0, Unease = 1, Threat = 2, Panic = 3, Aftershock = 4 }

    /// Published every frame the conductor runs. Carries the whole picture so a
    /// subscriber never has to reach back into the maniac to interpret it.
    public struct FearChangedEvent
    {
        /// 0..1, smoothed. THE number. Everything else here is context.
        public float Fear;
        /// Which beat of the loop this is — for display and cues, not for audio maths.
        public FearStage Stage;
        /// True while fear is decaying after a threat rather than responding to one.
        /// Aftershock is the difference between "he is here" and "he was", and the
        /// two feel completely different at the same fear value.
        public bool Recovering;

        // ---- visual outputs, ALREADY SCALED -------------------------------
        // Carried pre-multiplied by visualFearEnabled, visualPulseIntensity and
        // fearEffectMultiplier on purpose: a visual feature can then apply them
        // blind, without reading FearConfig or knowing the Fear feature's rules.
        // The alternative — every listener loading our config — would make the
        // master switch a thing each of them had to remember to honour.
        // With visuals off these are all simply 0, and subscribers go quiet with
        // no branch of their own.

        /// Extra vignette intensity to add at this fear level.
        public float VisualVignette;
        /// Depth of the per-beat vignette throb, 0..1.
        public float VisualPulse;
        /// Desaturation to apply, 0..1.
        public float VisualDesaturation;
    }

    /// Fired once when the maniac genuinely commits to a hunt — the moment worth
    /// a sting. Deliberately separate from FearChangedEvent: fear is continuous
    /// and this is an EVENT, and conflating them is how stings end up spamming.
    public struct FearDetectionEvent
    {
        /// Fear at the instant of detection. A sting for a distant sighting
        /// should not land as hard as one that arrives on top of you.
        public float Fear;
        /// Direct distance to the threat when it fired.
        public float Distance;
    }
}
