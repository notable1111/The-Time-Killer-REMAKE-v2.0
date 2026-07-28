// The player's throat. Separate from PlayerBreathing on purpose: the lungs say
// how hard you have been working, the voice says what is happening to you right
// now. Folding them together was rejected — a system that both drones and barks
// ends up doing neither well, and the breathing already earns its silence by
// being quiet almost all the time.
//
// NON-VERBAL, same rule as the maniac. Grunts, gasps, effort, a cry. The
// survivor is hooded and faceless by design; giving them a speaking voice would
// name them, and the design decision was that nobody is named.
//
// Asset: C#/Player/Configs/PlayerVoiceConfig.asset (clips wired by Setup/33).
using UnityEngine;

namespace TimeKiller.Player
{
    [CreateAssetMenu(menuName = "TimeKiller/Configs/Player Voice", fileName = "PlayerVoiceConfig")]
    public class PlayerVoiceConfig : ScriptableObject
    {
        [Header("Taking damage")]
        [Tooltip("Grunt when you lose a health point but are still in good shape.")]
        public AudioClip[] painGrunts;
        [Tooltip("Grunt when the hit leaves you on your last point. Should sound materially worse than the ordinary one — with no health bar on screen (health is diegetic by design) this IS the health readout.")]
        public AudioClip[] painGruntsCritical;
        [Tooltip("Remaining health fraction at or below which the critical pool is used instead.")]
        [Range(0f, 1f)] public float criticalAtOrBelow = 0.34f;
        [Range(0f, 1f)] public float painVolume = 0.85f;

        [Header("Death")]
        public AudioClip[] deathCries;
        [Range(0f, 1f)] public float deathVolume = 0.95f;

        [Header("Being spotted")]
        [Tooltip("The sharp intake when he sees you. Sits UNDER the music's jumpscare sting deliberately — the sting is the film score, this is your body, and they are meant to be heard as two different things.")]
        public AudioClip[] spottedGasps;
        [Range(0f, 1f)] public float gaspVolume = 0.6f;
        [Tooltip("Seconds between gasps, so him re-acquiring you mid-chase does not make you hyperventilate.")]
        public float gaspCooldown = 9f;

        [Header("Working on a clock")]
        [Tooltip("Small strained efforts while repairing. Quiet and infrequent — this is texture, and the skill-check already owns the player's attention.")]
        public AudioClip[] repairEfforts;
        [Range(0f, 1f)] public float repairVolume = 0.45f;
        [Tooltip("Shortest gap between repair efforts.")]
        public float repairMinInterval = 3.5f;
        [Tooltip("Longest gap between repair efforts.")]
        public float repairMaxInterval = 7f;

        [Header("Feel")]
        [Tooltip("Random pitch spread on every line, so a pool of three clips does not become recognisable.")]
        [Range(0f, 0.4f)] public float pitchJitter = 0.06f;

        [Header("Removal")]
        [Tooltip("Kill switch without deleting the component.")]
        public bool enabled = true;
    }
}
