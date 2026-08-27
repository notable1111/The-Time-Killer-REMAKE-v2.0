// Every tunable of "he gets worse as the run goes on".
//
// EVERY dial here is an identity at its neutral value: moveSpeedAtFull 1,
// hearingAtFull 1, wardrobeBonusAtFull 0 reproduce the pre-escalation maniac
// byte for byte, and `enabled = false` switches the whole feature off in one
// click. That is deliberate and it is the project's habit (hearingWallMuffle 1,
// lightRiseSeconds 0, searchDoubt rank 0): a difficulty change the user cannot
// un-tune is a difficulty change nobody dares approve.
//
// WHAT IS DELIBERATELY ABSENT: chase speed, sight range, attack damage.
//   - Chase speed is already 5.2 against the player's 4.5. He is faster than you
//     on purpose, and the escape is bought back by loseSightSeconds and the
//     post-hit adrenaline window. Escalating it does not make the run tenser, it
//     removes the escape.
//   - Sight range is the FAIRNESS dial. Escalation is meant to change how much of
//     the castle he covers, not how easily he notices a player doing everything
//     right.
//   - Damage is 3 hit points, no healing. Touching it turns escalation into
//     "the last clock kills you", which is a difficulty spike, not tension.
using UnityEngine;

namespace TimeKiller.Maniac
{
    [CreateAssetMenu(fileName = "ManiacEscalationConfig",
                     menuName = "TimeKiller/Configs/Maniac Escalation")]
    public class ManiacEscalationConfig : ScriptableObject
    {
        /// Where ManiacEscalation looks for this asset. The whole
        /// Assets/Resources tree is a Resources root, so the feature installs
        /// itself in every scene and deleting the asset is the uninstall — the
        /// same self-loading contract AudioMixConfig uses.
        public const string ResourcesPath = "C#/Maniac/Configs/ManiacEscalationConfig";

        [Tooltip("Master switch. Off = he behaves exactly as he did before this feature existed, and every number below is ignored.\n\nThis is a difficulty change and it has not had the user's ear yet — leave it easy to turn off.")]
        public bool enabled = true;

        [Header("At 100% of the objectives done")]
        [Tooltip("Multiplier on his PATROL, INVESTIGATE and SEARCH speeds once every clock is fixed. NOT his chase speed.\n\nThis is the dial that fills the missing middle of the tension curve: a maniac who covers the castle faster is a maniac you MEET more often, which is build-up. A maniac who chases faster is only a maniac you escape less often, which is the panic band the curve already has too much of.\n\nTHERE IS A HARD CEILING ON THIS ONE. Patrol speed is 1.8 and the player's walk is 2.2, so anything above x1.22 makes a fully escalated patrol faster than walking — and walking away from him quietly stops working, which forces the player to run, which is the loud choice. The whole stealth layer of the endgame hangs off that. 1.15 lands at 2.07, a real change with room left; ManiacEscalationTests pins it and Setup/51 prints it.\n\n1 = no change.")]
        [Range(1f, 2f)] public float moveSpeedAtFull = 1.15f;

        [Tooltip("Multiplier on how far a noise carries to him once every clock is fixed.\n\nWhy hearing and not sight: hearing is the sense the player CONTROLS. Walk instead of run and you are quiet again, so raising it makes the late run demand more care rather than punishing a player who is already doing everything right. Sight would just make him notice a careful player anyway.\n\n1 = no change. 1.25 turns his 9u running earshot into about 11.3u by the last clock.")]
        [Range(1f, 2f)] public float hearingAtFull = 1.25f;

        [Tooltip("Added to his wardrobe check chance once every clock is fixed — hiding gets less reliable as the run goes on, which is what stops the endgame becoming 'sit in a box until he wanders off'.\n\n0 = no change. Stacks with what the Director teaches him from hides you got away with, and the SUM is held under maxWardrobeChance below.")]
        [Range(0f, 0.5f)] public float wardrobeBonusAtFull = 0.18f;

        [Tooltip("Multiplier on how long the Director waits before steering him back toward you, once every clock is fixed.\n\nThis is the dial aimed straight at DEAD AIR. The Director exists because a recorded bot session ran 80 seconds with zero detections: when he loses you he goes back to his loop and the encounter is simply over. Shortening the quiet threshold late in the run means the back half has fewer of those empty stretches, which is the other half of 'the curve has no middle'.\n\nScales the between-hints cooldown by the same factor, so the RATIO the Director was tuned with is preserved - this makes him return sooner, it does not make him spam. 22s/15s become 15.4s/10.5s at 0.7.\n\n1 = no change.")]
        [Range(0.3f, 1f)] public float hintQuietAtFull = 0.7f;

        [Header("Safety rails")]
        [Tooltip("Hard ceiling on the wardrobe check chance from ALL sources — the authored base, what the Director taught him, and escalation together.\n\nThe Director already promises that hiding can never become useless (it caps its own learned bonus at 0.35). Escalation adds a second source on top, so without a ceiling on the SUM that promise quietly stops being true in the exact part of the run where hiding matters most. Keep this well under 1.")]
        [Range(0.1f, 1f)] public float maxWardrobeChance = 0.6f;

        [Tooltip("Seconds the change takes to arrive after a clock is fixed.\n\nSnapping every number the instant the repair finishes reads as a difficulty setting being changed, not as a person reacting. Blended in, the player feels the castle tighten a few seconds after the clock lights up — near enough to connect the two, far enough that it is a mood rather than a switch. 0 = snap.")]
        [Range(0f, 20f)] public float blendSeconds = 6f;
    }
}
