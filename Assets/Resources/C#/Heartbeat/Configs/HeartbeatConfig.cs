// Every tunable of the player's heartbeat in one asset.
//
// THE MODEL (design 2026-07-28): distance is the spine, state raises the floor.
//   1. Distance ALWAYS drives the rate. He crosses the room and the beat climbs
//      continuously — this is the channel the player learns to read, and it
//      works even when he has no idea you exist.
//   2. Suspicion raises a FLOOR under that. He stops and stares from across the
//      hall and your heart jumps, regardless of how far away he is.
//   3. Detection raises it higher still AND ducks the world away, so the last
//      thing you hear before he reaches you is your own body.
// Floors rather than additions: two things stacking would run the rate off the
// top and leave the loud end meaning nothing.
using UnityEngine;

namespace TimeKiller.Heartbeat
{
    [CreateAssetMenu(menuName = "TimeKiller/Configs/Heartbeat", fileName = "HeartbeatConfig")]
    public class HeartbeatConfig : ScriptableObject
    {
        [Header("Distance — the spine of the whole thing")]
        [Tooltip("Beats per minute when he is far away or absent. Usually below the audible floor, so silence.")]
        public float calmBpm = 56f;
        [Tooltip("Beats per minute when he is on top of you. Human maximum is around 200; this is terror, not exercise.")]
        public float nearBpm = 160f;
        [Tooltip("Distance at which he starts affecting your heart at all.")]
        public float heartRange = 14f;
        [Tooltip("Shape of the climb. 1 = straight line. Above 1 keeps the rise gentle far out and makes it accelerate hard as he closes, which is far more frightening than a linear ramp because the panic is in the CHANGE, not the value.")]
        [Range(0.5f, 4f)] public float distanceCurve = 2.2f;

        [Header("State floors — the rate never drops below these")]
        [Tooltip("Floor while he is Suspicious. Set above the resting rate so the 'did he see me?' beat lands even at a distance.")]
        public float suspiciousFloorBpm = 108f;
        [Tooltip("Floor while he has fully Detected you. This is the sound of being hunted.")]
        public float detectedFloorBpm = 150f;
        // Injury and sprinting floors were REMOVED on 2026-07-28. Both made the
        // heart pound with the maniac nowhere near, which is exactly what turns
        // it into background noise. Exertion is the lungs' job — see
        // BreathingConfig. The heart answers one question: how close is he?

        [Header("Adrenaline — deliberately asymmetric")]
        [Tooltip("How fast the rate CLIMBS. Adrenaline is near-instant; this should be quick.")]
        public float riseSpeed = 6f;
        [Tooltip("How fast the rate FALLS once the threat is gone. Much slower on purpose: your heart is still hammering long after he walks away, so escaping does not feel safe for several seconds. This gap is where most of the dread actually lives.")]
        public float fallSpeed = 0.7f;

        [Header("Volume")]
        [Range(0f, 1f)] public float maxVolume = 1f;
        [Tooltip("Volume of the QUIETEST audible beat, as a share of maxVolume. Scaling straight from zero made distant beats technically present and practically inaudible — the first thump you hear should already be unmistakable, and get more urgent from there rather than fading up from nothing.")]
        [Range(0f, 1f)] public float quietVolume = 0.55f;
        [Tooltip("Below this share of the rate range the heart is SILENT. A heartbeat that never stops stops being frightening — this keeps it an event.")]
        [Range(0f, 1f)] public float silenceBelow = 0.05f;
        [Tooltip("Extra volume while hidden: in a wardrobe your own body is the only thing you can hear, and it is loud enough to feel like it will give you away.")]
        public float hiddenBoost = 1.3f;

        [Header("Pitch — the body tightening")]
        [Tooltip("Playback pitch at maximum rate. A racing heart is not just a faster version of a calm one; the beat tightens and rises. Small numbers only — past about 1.2 it stops sounding like a body.")]
        [Range(1f, 1.4f)] public float pitchAtMax = 1.12f;

        [Header("Ducking — 'when he has you, you hear only this'")]
        [Tooltip("What the music and ambience drop to once he has Detected you. 0 = total silence behind the heartbeat.")]
        [Range(0f, 1f)] public float duckedWorldVolume = 0.08f;
        [Tooltip("How much of that duck also applies BEFORE he detects you, scaled by how hard your heart is going. Above 0 the score starts receding as he closes, so the heartbeat takes over the mix gradually instead of only at the last moment. This is what makes it a foreground feature rather than an accent.")]
        [Range(0f, 1f)] public float duckByIntensity = 0.75f;
        [Tooltip("Seconds for the world to drop away when he spots you. Fast — it should feel like the floor falling out.")]
        public float duckInSeconds = 0.35f;
        [Tooltip("Seconds for the world to return once he loses you. Slow, so the quiet lingers and you are not sure it is over.")]
        public float duckOutSeconds = 2.5f;

        [Header("Feel")]
        [Tooltip("Random beat-to-beat timing wobble (0.05 = 5%). A metronome reads as a machine; a heart does not.")]
        [Range(0f, 0.3f)] public float irregularity = 0.05f;
        [Tooltip("Fire one immediate extra beat the moment he detects you — the lurch your chest gives before the rate has caught up. Costs nothing and is the single most-felt moment in the system.")]
        public bool lurchOnDetected = true;
    }
}
