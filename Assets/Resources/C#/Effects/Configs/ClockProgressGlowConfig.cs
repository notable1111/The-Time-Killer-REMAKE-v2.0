// How a clock's own light behaves WHILE you are repairing it.
//
// The clock already lights up when FIXED — ClockObjective has owned that since
// long before this file. What was missing is the Dead by Daylight half we did
// not have: their generators show progress from across the room (the pistons
// speed up as charge builds), while ours showed nothing at all until the moment
// they were done. Walk away from a half-repaired clock and there was no way to
// tell it apart from an untouched one.
//
// Everything here is relative to the light's AUTHORED values, never absolute, so
// re-tuning a clock's glow in the scene keeps working and this cannot fight it.
using UnityEngine;

namespace TimeKiller.Effects
{
    [CreateAssetMenu(menuName = "TimeKiller/Effects/Clock Progress Glow", fileName = "ClockProgressGlowConfig")]
    public class ClockProgressGlowConfig : ScriptableObject
    {
        public const string ResourcesPath = "C#/Effects/Configs/ClockProgressGlowConfig";

        [Header("The progress ramp")]
        [Tooltip("Share of the clock's authored 'fixed' brightness reached at 99% progress. Under 1 on purpose: finishing must still be a visible jump, not the last few percent of a fade.")]
        [Range(0f, 1f)] public float workingCeiling = 0.45f;

        [Tooltip("How small the light's radius starts, as a share of its authored radius. A barely-started clock should be a hint you notice up close, not a lamp visible across the hall.")]
        [Range(0.1f, 1f)] public float workingRadiusScaleMin = 0.35f;

        [Header("The earned press")]
        [Tooltip("Extra brightness on each successful press, as a share of the authored intensity. This is the beat the player feels for getting the timing right — and it is why the gold sparkle sprite is no longer the only thing marking a good press.")]
        [Range(0f, 1.5f)] public float pressPunch = 0.5f;

        [Tooltip("Seconds for that pulse to swell in. Never 0 — an instant step reads as a glitch rather than as a mechanism catching.")]
        [Range(0f, 0.3f)] public float pressAttack = 0.04f;

        [Tooltip("Seconds for it to fall away. Short: this is a tick of progress, not an event.")]
        [Range(0.05f, 2f)] public float pressRelease = 0.32f;

        [Header("Install")]
        [Tooltip("Off leaves clocks exactly as they were — dark until fixed. Deleting this asset uninstalls the feature.")]
        public bool enabled = true;
    }
}
