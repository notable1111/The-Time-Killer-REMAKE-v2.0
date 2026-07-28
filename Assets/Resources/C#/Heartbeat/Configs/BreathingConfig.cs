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

        [Header("Volume")]
        [Tooltip("Below this exertion there is NO breathing at all. This is what keeps the calm parts of the map silent instead of a permanent panting bed.")]
        [Range(0f, 1f)] public float silenceBelow = 0.22f;
        [Tooltip("Volume when fully winded.")]
        [Range(0f, 1f)] public float windedVolume = 0.7f;

        [Header("Rate")]
        [Tooltip("Playback pitch when barely winded — slow, controlled.")]
        [Range(0.5f, 1.5f)] public float easyPitch = 0.9f;
        [Tooltip("Playback pitch when fully winded — fast and shallow.")]
        [Range(0.5f, 2f)] public float windedPitch = 1.4f;

        [Header("The recovery breath")]
        [Tooltip("One long breath when the chase ends — the moment you realise he has lost you. Only fires if you were actually winded above this much, so it cannot trigger after a two-second scare.")]
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
