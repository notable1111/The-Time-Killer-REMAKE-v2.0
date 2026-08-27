// What the player HEARS when the maniac gets worse.
//
// Asset: C#/Audio/Configs/EscalationVoiceConfig.asset, loaded from Resources by
// EscalationVoice. Delete the asset and the feature never installs. An empty
// clip pool is silent BY DESIGN, the same contract every voice pool in this
// project uses.
using UnityEngine;

namespace TimeKiller.Audio
{
    [CreateAssetMenu(menuName = "TimeKiller/Configs/Escalation Voice", fileName = "EscalationVoiceConfig")]
    public class EscalationVoiceConfig : ScriptableObject
    {
        public const string ResourcesPath = "C#/Audio/Configs/EscalationVoiceConfig";

        [Tooltip("Master switch.")]
        public bool enabled = true;

        [Tooltip("One is chosen at random. Empty = the escalation stays silent, which is a valid shipped state rather than a bug.")]
        public AudioClip[] cues;

        [Range(0f, 1f)] public float volume = 0.85f;

        [Tooltip("Random pitch spread so repeated escalations never sound like the same file twice.")]
        [Range(0f, 0.3f)] public float pitchJitter = 0.05f;

        [Tooltip("Pitch multiplier applied per escalation step beyond the first. Below 1 he drops lower each time — the same voice getting bigger as the run goes on. 1 = every step sounds identical.")]
        [Range(0.8f, 1.2f)] public float pitchPerStep = 0.97f;

        [Tooltip("Shortest gap between two cues. The escalation event can in principle arrive twice quickly if two objectives complete together, and two of these on top of each other reads as a glitch rather than a threat.")]
        [Range(0f, 20f)] public float cooldownSeconds = 3f;
    }
}
