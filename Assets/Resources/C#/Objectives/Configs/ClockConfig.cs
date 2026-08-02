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

        [Header("Working noise — the repair itself is the commitment")]
        [Tooltip("Loudness of the steady winding noise a clock makes WHILE being repaired, whether or not you are hitting the presses.\n\nWithout this a skilled player is completely silent: hit every press and repairing is free, so all the danger lives in fumbling and evaporates the moment someone gets good at the mini-game. That is backwards — working a clock should BE the commitment, and skill should buy a shorter exposure rather than total safety.\n\n0.3 x hearingRadius 9 = about 2.7u reach, deliberately well under a miss (3.6u calm, 7.9u panicked), so a fumble is still clearly worse than simply being there. Set to 0 for the old silent-if-perfect behaviour.")]
        [Range(0f, 1f)] public float repairNoiseLoudness = 0.3f;
        [Tooltip("Seconds between working noises. Slow enough to read as winding rather than a klaxon, frequent enough that a long repair is a sustained risk.")]
        [Range(0.3f, 4f)] public float repairNoiseInterval = 1.2f;

        [Header("Fear coupling — the repair knows he is coming")]
        [Tooltip("Scale the mini-game's difficulty by the FearConductor's 0..1 value.\n\nUntil now the repair ignored the maniac completely: fixing a clock with him across the castle was mechanically identical to fixing one with him closing on you. All the danger was fictional, and the screen you stare at while locked in place and most vulnerable was the one screen that did not know he existed.\n\nOff = exactly the old behaviour.")]
        public bool fearAffectsRepair = true;
        [Tooltip("Marker speed multiplier at MAXIMUM fear. DEFAULT 1 = the sweep NEVER changes speed, on purpose.\n\nA constant rhythm stays learnable: you can internalise it and then execute it while terrified. Speeding it up forces you to re-learn the timing at the exact moment you are least able to, which reads as unfair even when it is not — and because a miss makes noise that draws him, a faster sweep spirals: more presses, more misses, more noise, more fear, faster sweep.")]
        [Range(1f, 3f)] public float fearSpeedBoost = 1f;
        [Tooltip("Fraction of the target zone that SURVIVES at maximum fear. 0.85 is a light garnish, not the difficulty: it gives the IMAGE of your window closing as he nears without ever turning the press into a coin flip. At 0.65 with a speed boost the window measured 49 ms at max fear — about three frames, below human timing precision.\n\nThe real teeth are in fearMissNoiseBoost below: the skill check stays fair and the COST of failing it rises.")]
        [Range(0.3f, 1f)] public float fearZoneShrink = 0.85f;

        [Header("Miss noise (ties the repair to the hunt)")]
        [Tooltip("Loudness of the noise a MISS makes (0..1). Quiet by design: only heard if the maniac is nearby (hearing = loudness x radius).")]
        [Range(0f, 1f)] public float missNoiseLoudness = 0.4f;
        [Tooltip("Multiplier on the miss noise at MAXIMUM fear — the stakes, rather than the difficulty.\n\nThis is the design: the skill check itself never gets harder, so a press you earned is never taken from you. What changes is what a fumble COSTS. Calm, a miss is a small setback. Panicking with him nearby, the same miss is a beacon. Player agency stays intact and the tense moment still gets genuinely more dangerous.\n\n2.2 x 0.4 = 0.88 loudness at full panic, close to a running footstep, so it carries about twice as far as a calm fumble.")]
        [Range(1f, 4f)] public float fearMissNoiseBoost = 2.2f;

        [Header("Miss ring — the mistake you can SEE leave the clock")]
        [Tooltip("Draw an expanding ring on a miss, sized to how far the noise ACTUALLY carried.\n\nThe rule 'fumbling summons him' was previously unlearnable: the noise reached him silently and the consequence arrived a minute later, by which time no player connects it to the press they got wrong. A rule the player cannot learn is not a mechanic, it is bad luck. The ring is honest — its radius is loudness x his hearingRadius, so it shows the real reach, not a decoration.")]
        public bool showMissRing = true;
        [Tooltip("Seconds the ring takes to expand to its full radius and fade. Short: it is a flinch, not a light show.")]
        [Range(0.15f, 2f)] public float missRingSeconds = 0.55f;
        [Tooltip("Ring colour when calm.")]
        public Color missRingCalmColor = new Color(0.85f, 0.78f, 0.55f, 0.75f);
        [Tooltip("Ring colour at maximum fear — warmer, so a costly fumble looks costly.")]
        public Color missRingPanicColor = new Color(0.95f, 0.35f, 0.25f, 0.9f);

        [Header("Fixed-clock light")]
        [Tooltip("Whether a fixed clock lights up (green Light2D) — the map visibly brightens as you win.")]
        public bool lightWhenFixed = true;
    }
}
