// Every tunable of the Director — the macro brain that keeps encounters alive.
//
// Modelled on Alien: Isolation's two-brain split (research 2026-08-02): a
// director that always knows where the player is and periodically points the
// creature at their area WITHOUT handing over the position, while the creature
// finds them with its own senses. That game's players trust it precisely because
// it never cheats, and the tension survives players who think about how it works.
//
// The problem it solves here is measured, not theoretical: a recorded bot session
// ran **80 seconds and the maniac never once detected the player**. He loses you,
// the belief map decays, he returns to patrol, and the encounter simply dies. No
// amount of tuning his senses fixes that — nothing was steering him back.
//
// Asset: C#/Director/Configs/DirectorConfig.asset (created by Setup/45).
using UnityEngine;

namespace TimeKiller.Director
{
    [CreateAssetMenu(menuName = "TimeKiller/Configs/Director", fileName = "DirectorConfig")]
    public class DirectorConfig : ScriptableObject
    {
        [Header("Master")]
        [Tooltip("Turn the Director off without deleting it. With this false the maniac behaves exactly as he did before the feature existed — no hints, no learning.")]
        public bool enabled = true;

        [Header("When to nudge him")]
        [Tooltip("Seconds of NOTHING HAPPENING (he is unaware, no chase, no investigation) before the Director points him at the player's area. Too short and he is always on top of you; too long and the level goes dead, which is the failure this exists to fix.")]
        public float hintAfterQuietSeconds = 22f;
        [Tooltip("Shortest gap between hints, so a player who keeps breaking line of sight is not hounded by a new nudge every few seconds.")]
        public float minSecondsBetweenHints = 15f;
        [Tooltip("He must be at least this far away before a hint is worth issuing. Nudging him toward someone he is already next to would be pointless and would risk reading as a cheat.")]
        public float minHintDistance = 14f;

        [Header("How vague the hint is — this is the no-cheating dial")]
        [Tooltip("World units of error added to the player's position before the hint is sent. This is the LIE that keeps him honest: at 0 the Director is handing over your exact location and he is cheating, whatever the code says. Should comfortably exceed his sightRange (7) so arriving at the hint does not mean finding you.")]
        public float hintError = 9f;
        [Tooltip("Radius he will wander inside once he arrives. A hint is an AREA to sweep, not a destination to stand on.")]
        public float hintRadius = 7f;
        [Tooltip("Extra error added for every 10s the Director has gone without the maniac sensing the player at all. A stale guess should be a worse guess.")]
        public float errorGrowthPer10s = 2.5f;
        [Tooltip("Ceiling on that growth, so an old hint stays a hint rather than becoming a random point on the map.")]
        public float maxHintError = 20f;

        [Header("Learning — unlocked by what the player DOES, never by dying")]
        [Tooltip("Successful hides (you hid, he hunted, he never found you) before wardrobe-checking starts climbing. Isolation gates behaviours on player metrics like this so the creature appears to adapt; gating on DEATHS instead would punish players for losing, which is the one thing that reads as unfair.")]
        public int hidesBeforeLearning = 2;
        [Tooltip("Added to the wardrobe check chance per successful hide beyond the threshold. Small: this should creep up over a session, not flip on.")]
        [Range(0f, 0.5f)] public float wardrobeBonusPerHide = 0.06f;
        [Tooltip("Ceiling on the learned bonus. Below 1 on purpose — hiding must never become useless, or the whole verb dies. Your own playtests already measured hiding as inert (81.8% vs 81.9% death rate); the fix is to make it a real decision, not to remove it.")]
        [Range(0f, 1f)] public float maxWardrobeBonus = 0.35f;

        [Header("Doubt — a perfect search reads as a robot")]
        [Tooltip("Chance that he walks to the SECOND or third likeliest place instead of the best one. Isolation deliberately searches sub-optimally and backtracks to simulate uncertainty; always taking the optimal cell is what makes a hunter look like a pathfinder.")]
        [Range(0f, 1f)] public float doubtChance = 0.3f;
        [Tooltip("Chance he returns to somewhere he ALREADY cleared, as though second-guessing himself. Rare — this is a flavour beat, and overdone it just looks broken.")]
        [Range(0f, 0.5f)] public float backtrackChance = 0.12f;
    }
}
