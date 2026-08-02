// Every tunable of the player's breathing.
//
// Breathing is EXERTION, not fear (design correction 2026-07-28). The first
// version followed the heartbeat's fear level, so the player panted whenever the
// maniac was merely nearby and the sound was effectively always on — which made
// it wallpaper. It now works the way a body does: you get winded from RUNNING
// and from being CHASED, it takes a few seconds to build, and when the chase
// ends you take one long recovery breath and settle.
//
// This is NOT stamina. Nothing here affects movement, and no meter is shown —
// it is purely how the player sounds. The team cancelled stamina as a mechanic
// and this deliberately does not reintroduce it by the back door.
using UnityEngine;

namespace TimeKiller.Heartbeat
{
    [CreateAssetMenu(menuName = "TimeKiller/Configs/Breathing", fileName = "BreathingConfig")]
    public class BreathingConfig : ScriptableObject
    {
        [Header("Getting winded")]
        [Tooltip("Seconds of BEING CHASED before you are fully out of breath. Only an active chase counts — ordinary running used to as well, and because the player runs almost constantly that made breathing a permanent bed nobody heard.")]
        public float secondsToWinded = 6f;
        [Tooltip("Seconds to recover back to calm once he is gone. Longer than it took to build, because catching your breath always takes longer than losing it.")]
        public float secondsToRecover = 11f;

        [Header("Breath cycle — alternating inhale/exhale one-shots")]
        // Research 2026-08-02 (CRI Middleware's player-breathing system, and how
        // Alien: Isolation drives Ripley's breathing off its Stealth value):
        // "the basis for a breathing system is the alternance between inhale and
        // exhale sounds" — sequential one-shots, shuffled from pools, with
        // intensity driving the SEQUENCING RATE.
        //
        // This replaced one looping clip pitched 0.9 -> 1.4. That approach failed
        // three ways: a loop has a seam the ear locks onto, one sample means
        // every breath is identical, and pitching a breath up 40% does not sound
        // faster — it sounds like a SMALLER PERSON. Rate carries effort; pitch
        // barely moves.
        [Tooltip("Seconds held between an inhale and its exhale, when calm. Short — the turnaround at the top of a breath is quick.")]
        public float calmHold = 0.25f;
        [Tooltip("Seconds of rest after the exhale, when calm. This is the long pause, and shortening it is what makes breathing sound frightened.")]
        public float calmRest = 2.15f;
        [Tooltip("Hold between inhale and exhale when fully winded.")]
        public float panicHold = 0.10f;
        [Tooltip("Rest after the exhale when fully winded. The gap almost vanishes — that alone reads as panic, with no pitch change at all.")]
        public float panicRest = 0.85f;
        [Tooltip("Playback pitch at full exertion. Deliberately TINY compared to the old 1.4: past about 1.1 the player stops sounding winded and starts sounding like a different, smaller person.")]
        [Range(1f, 1.3f)] public float panicPitch = 1.06f;

        [Header("Volume")]
        [Tooltip("Below this exertion there is NO breathing at all. This is what keeps the calm parts of the map silent instead of a permanent panting bed.")]
        [Range(0f, 1f)] public float silenceBelow = 0.22f;
        [Tooltip("Volume when fully winded.")]
        [Range(0f, 1f)] public float windedVolume = 0.7f;

        // easyPitch/windedPitch (0.9 -> 1.4) were removed 2026-08-02 with the
        // looping breath they drove. Rate is carried by the cycle gaps above now;
        // pitch only jitters slightly around panicPitch. A config field that
        // outlives its reader is a trap for the next person.

        [Header("The recovery breath")]
        [Tooltip("Seconds he must stay OFF you before the relief breath fires (user ruling 2026-07-28: 8-9s). Relief is not instant — and 'Detected' drops every time line of sight breaks behind a pillar, so firing immediately meant the breath went off mid-chase. Re-acquiring you cancels it and the wait starts over.")]
        public float recoveryDelaySeconds = 8.5f;
        [Tooltip("How fast you stop panting DURING that wait, as a share of the normal recovery rate. Well below 1 because adrenaline does not stop the moment he turns away — and because at full rate you would be near silent by the time the exhale lands, which makes it come out of nowhere.")]
        [Range(0f, 1f)] public float settleDecayScale = 0.35f;
        [Tooltip("One long breath when the chase ends — the moment you realise he has lost you. Only fires if you were actually winded above this much, so it cannot trigger after a two-second scare. Judged when the chase ENDS, not when the breath fires, so the wait itself cannot cancel a breath you earned.")]
        [Range(0f, 1f)] public float recoveryNeedsExertion = 0.45f;
        [Range(0f, 1f)] public float recoveryVolume = 0.8f;
        [Tooltip("Exertion is dropped to this immediately after the recovery breath, so the long exhale IS the recovery rather than playing over continued panting.")]
        [Range(0f, 1f)] public float exertionAfterRecovery = 0.25f;
        [Tooltip("How far the HEARTBEAT steps back while the recovery breath plays. The breath sits well below the heartbeat even normalised, and the heart is still at chase rate for seconds after he gives up — without this the exhale is simply buried.")]
        [Range(0f, 1f)] public float heartSoftenFactor = 0.35f;
        [Tooltip("Share of the breath's length that the heart stays softened for. Below 1 so the heart is already coming back before the exhale ends.")]
        [Range(0f, 1f)] public float heartSoftenShare = 0.8f;

        [Header("Holding your breath (hiding)")]
        [Tooltip("Hold your breath inside a hiding spot while he is actually hunting. Set false to disable holding entirely.")]
        public bool holdWhileHiding = true;
        [Tooltip("Volume while holding — not zero. A held breath is a strained, tiny sound; silence reads as broken audio rather than as tension.")]
        [Range(0f, 1f)] public float heldVolume = 0.05f;

        [Header("Feel")]
        [Tooltip("How fast the audible breathing chases the exertion value. Purely smoothing — the pace of the build is secondsToWinded.")]
        public float smoothing = 2.5f;
    }
}
