// Tunables for the clock-repair objective + its skill-check mini-game
// (design 2026-07-24: timed presses, quiet noise on miss). One asset drives
// every clock. Asset: C#/Objectives/Configs/ClockConfig.asset.
using UnityEngine;

namespace TimeKiller.Objectives
{
    [CreateAssetMenu(menuName = "TimeKiller/Configs/Clock", fileName = "ClockConfig")]
    public class ClockConfig : ScriptableObject
    {
        [Header("Interaction")]
        [Tooltip("Press E within this range of a broken clock to start repairing it (measured to the clock center).")]
        public float interactRange = 2.2f;
        [Tooltip("If the maniac gets this close while you're repairing, you're kicked out and must run.")]
        public float interruptRange = 3f;

        [Header("Skill-check mini-game")]
        [Tooltip("How fast the marker sweeps the bar (full sweeps per second).")]
        public float markerSpeed = 0.75f;
        [Tooltip("Width of the target zone (fraction of the bar, 0..1). Wider = easier.")]
        [Range(0.05f, 0.5f)] public float zoneWidth = 0.18f;
        [Tooltip("Progress added by a successful (in-zone) press. 0.2 => ~5 hits to finish a clock.")]
        [Range(0.05f, 1f)] public float progressPerHit = 0.2f;
        [Tooltip("Progress lost on a miss (out-of-zone press). Kept small — misses are forgiving.")]
        [Range(0f, 0.5f)] public float missPenalty = 0.05f;

        [Header("Miss noise (ties the repair to the hunt)")]
        [Tooltip("Loudness of the noise a MISS makes (0..1). Quiet by design: only heard if the maniac is nearby (hearing = loudness x radius).")]
        [Range(0f, 1f)] public float missNoiseLoudness = 0.4f;

        [Header("Fixed-clock light")]
        [Tooltip("Whether a fixed clock lights up (green Light2D) — the map visibly brightens as you win.")]
        public bool lightWhenFixed = true;
    }
}
