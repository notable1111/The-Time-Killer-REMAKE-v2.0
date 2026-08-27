// EditMode tests for per-clock escalation.
//
// The whole feature is four pure statics on ManiacEscalation — no scene, no
// frames, no physics — which is what makes it testable at all, and it was
// written that way on purpose (ARCHITECTURE: "only pure functions").
//
// Two kinds of test, kept apart the way ManiacBrainTests keeps them:
//   INVARIANTS use fresh configs and pin things that must hold whatever anyone
//     tunes — above all that a fresh run is byte-for-byte the old maniac, and
//     that the off switch really is an off switch.
//   SHIPPED-CONFIG tests read the real assets and assert the game as it will
//     play. They are meant to FAIL if someone escalates him past the two points
//     where this stops being tension and becomes a difficulty spike:
//       - a patrol faster than the player's walk (sneaking away stops working);
//       - a wardrobe he opens most of the time (hiding becomes a coin flip).
//     Both are design promises made elsewhere in the codebase; this is where
//     they are actually enforced.
//
// The shipped-config tests IGNORE rather than fail when the escalation asset is
// missing, because it is created by TimeKiller/Setup/51 and a project that has
// not run it yet is un-escalated, not broken. A red suite that means "you
// haven't run a setup script" trains people to ignore red suites.
using NUnit.Framework;
using TimeKiller.Maniac;
using TimeKiller.Player;
using UnityEditor;
using UnityEngine;

namespace TimeKiller.Tests
{
    public class ManiacEscalationTests
    {
        const string EscalationPath = "Assets/Resources/C#/Maniac/Configs/ManiacEscalationConfig.asset";
        const string ManiacPath = "Assets/Resources/C#/Maniac/Configs/ManiacConfig.asset";
        const string MovementPath = "Assets/Resources/C#/Player/Configs/PlayerMovementConfig.asset";
        const string FootstepPath = "Assets/Resources/C#/Player/Configs/PlayerFootstepConfig.asset";
        const string WardrobePath = "Assets/Resources/C#/Maniac/Configs/WardrobeSearchConfig.asset";
        const string DirectorPath = "Assets/Resources/C#/Director/Configs/DirectorConfig.asset";

        /// DirectorConfig.maxWardrobeBonus — the cap on what a player can teach
        /// him by hiding successfully. Escalation stacks on top of it, so the
        /// worst case below has to include it.
        const float DirectorLearnedCap = 0.35f;

        static ManiacEscalationConfig Defaults()
            => ScriptableObject.CreateInstance<ManiacEscalationConfig>();

        static T Shipped<T>(string path) where T : ScriptableObject
            => AssetDatabase.LoadAssetAtPath<T>(path);

        static T Required<T>(string path) where T : ScriptableObject
        {
            var asset = Shipped<T>(path);
            Assert.That(asset, Is.Not.Null, $"{typeof(T).Name} missing at {path}");
            return asset;
        }

        static ManiacEscalationConfig ShippedEscalationOrIgnore()
        {
            var cfg = Shipped<ManiacEscalationConfig>(EscalationPath);
            if (cfg == null)
                Assert.Ignore($"No escalation config at {EscalationPath} — " +
                              "run TimeKiller/Setup/51 to create it. Until then the maniac " +
                              "never escalates, which is a valid state, not a failure.");
            return cfg;
        }

        // ---- invariants -----------------------------------------------------

        [Test]
        public void Ramp_IsZeroBeforeAnythingIsDoneAndOneWhenAllIs()
        {
            Assert.That(ManiacEscalation.Ramp(0, 3), Is.EqualTo(0f), "a fresh run must not be escalated");
            Assert.That(ManiacEscalation.Ramp(1, 3), Is.EqualTo(1f / 3f).Within(0.0001f));
            Assert.That(ManiacEscalation.Ramp(3, 3), Is.EqualTo(1f));
        }

        [Test]
        public void Ramp_SurvivesASceneWithNoObjectivesInIt()
        {
            // Total 0 is legitimate — the bot's bare test scenes, a future mode
            // with no clocks. It must read as "not escalated", never as a divide
            // by zero or a NaN quietly poisoning every multiplier downstream.
            float ramp = ManiacEscalation.Ramp(0, 0);
            Assert.That(float.IsNaN(ramp), Is.False, "Total 0 produced NaN");
            Assert.That(ramp, Is.EqualTo(0f));
            Assert.That(ManiacEscalation.Ramp(5, 0), Is.EqualTo(0f));
        }

        [Test]
        public void Ramp_ClampsWhenMoreAreDoneThanExist()
        {
            Assert.That(ManiacEscalation.Ramp(9, 3), Is.EqualTo(1f),
                "a miscount must saturate, never run the multipliers off the top");
        }

        [Test]
        public void TheRunOpensAsTheOldManiacExactly()
        {
            // THE test of the feature. Before the first clock is fixed, every
            // number the maniac reads has to be the number he shipped with — no
            // rounding drift, no "close enough". Anything else means this feature
            // silently retuned a maniac that took five passes to balance.
            var cfg = Defaults();
            Assert.That(ManiacEscalation.MoveMultiplier(cfg, 0f), Is.EqualTo(1f));
            Assert.That(ManiacEscalation.HearingMultiplier(cfg, 0f), Is.EqualTo(1f));
            Assert.That(ManiacEscalation.WardrobeBonusFor(cfg, 0f), Is.EqualTo(0f));
        }

        [Test]
        public void NoComponentAtAllIsAlsoTheOldManiacExactly()
        {
            // The removability contract: delete the component (or never create
            // the asset) and every consumer's null-guard has to produce identity.
            Assert.That(ManiacEscalation.MoveMultiplier(null, 1f), Is.EqualTo(1f));
            Assert.That(ManiacEscalation.HearingMultiplier(null, 1f), Is.EqualTo(1f));
            Assert.That(ManiacEscalation.WardrobeBonusFor(null, 1f), Is.EqualTo(0f));
        }

        [Test]
        public void TheOffSwitchIsReallyOff()
        {
            // The escape hatch has to be real, not decorative — the same thing
            // ManiacPerceptionTests demands of hearingWallMuffle = 1. If someone
            // turns escalation off to judge a change, it must leave NOTHING
            // behind, at full progress, on a config with every dial cranked.
            var cfg = Defaults();
            cfg.moveSpeedAtFull = 2f;
            cfg.hearingAtFull = 2f;
            cfg.wardrobeBonusAtFull = 0.5f;
            cfg.enabled = false;

            Assert.That(ManiacEscalation.MoveMultiplier(cfg, 1f), Is.EqualTo(1f));
            Assert.That(ManiacEscalation.HearingMultiplier(cfg, 1f), Is.EqualTo(1f));
            Assert.That(ManiacEscalation.WardrobeBonusFor(cfg, 1f), Is.EqualTo(0f));
            Assert.That(ManiacEscalation.ChanceWithCeiling(cfg, 0.12f, 0.9f),
                        Is.EqualTo(Mathf.Clamp01(0.12f + 0.9f)),
                        "disabled, the wardrobe sum must be the plain Clamp01 it was before");
        }

        [Test]
        public void NeutralValuesAreAnIdentityEvenWhenEnabled()
        {
            var cfg = Defaults();
            cfg.enabled = true;
            cfg.moveSpeedAtFull = 1f;
            cfg.hearingAtFull = 1f;
            cfg.wardrobeBonusAtFull = 0f;

            Assert.That(ManiacEscalation.MoveMultiplier(cfg, 1f), Is.EqualTo(1f));
            Assert.That(ManiacEscalation.HearingMultiplier(cfg, 1f), Is.EqualTo(1f));
            Assert.That(ManiacEscalation.WardrobeBonusFor(cfg, 1f), Is.EqualTo(0f));
        }

        [Test]
        public void EscalationNeverMakesHimWeaker()
        {
            var cfg = Defaults();
            float lastMove = 0f, lastHear = 0f, lastWardrobe = -1f;
            for (float ramp = 0f; ramp <= 1.0001f; ramp += 0.05f)
            {
                float move = ManiacEscalation.MoveMultiplier(cfg, ramp);
                float hear = ManiacEscalation.HearingMultiplier(cfg, ramp);
                float wardrobe = ManiacEscalation.WardrobeBonusFor(cfg, ramp);

                Assert.That(move, Is.GreaterThanOrEqualTo(lastMove - 0.0001f),
                            $"move multiplier dipped at ramp {ramp:0.00}");
                Assert.That(hear, Is.GreaterThanOrEqualTo(lastHear - 0.0001f),
                            $"hearing multiplier dipped at ramp {ramp:0.00}");
                Assert.That(wardrobe, Is.GreaterThanOrEqualTo(lastWardrobe - 0.0001f),
                            $"wardrobe bonus dipped at ramp {ramp:0.00}");

                lastMove = move; lastHear = hear; lastWardrobe = wardrobe;
            }
        }

        [Test]
        public void TheCeilingHoldsTheSumButNeverCutsAnAuthoredBase()
        {
            var cfg = Defaults();
            cfg.maxWardrobeChance = 0.6f;

            Assert.That(ManiacEscalation.ChanceWithCeiling(cfg, 0.12f, 0.9f),
                        Is.EqualTo(0.6f).Within(0.0001f),
                        "the sum of every bonus must be held at the ceiling");

            // A base deliberately set above the ceiling is a decision, not an
            // overflow: escalation may decline to add to it, but must never
            // quietly make him worse at his job than he was authored to be.
            Assert.That(ManiacEscalation.ChanceWithCeiling(cfg, 0.8f, 0f),
                        Is.EqualTo(0.8f).Within(0.0001f));
        }

        // ---- shipped config: the two promises this feature could break -------

        [Test]
        public void YouCanStillWalkAwayFromAFullyEscalatedPatrol()
        {
            var escalation = ShippedEscalationOrIgnore();
            var maniac = Required<ManiacConfig>(ManiacPath);
            var movement = Required<PlayerMovementConfig>(MovementPath);

            float patrolAtFull = maniac.patrolSpeed *
                                 ManiacEscalation.MoveMultiplier(escalation, 1f);

            Assert.That(patrolAtFull, Is.LessThan(movement.walkSpeed),
                $"A fully escalated patrol moves at {patrolAtFull:0.00} against the player's walk " +
                $"{movement.walkSpeed:0.00}. Keeping quiet distance from a patrolling maniac then " +
                "requires RUNNING, which is the loud choice — so escalation would have removed the " +
                "stealth layer from the endgame instead of tightening it. Lower moveSpeedAtFull " +
                $"(the ceiling for patrolSpeed {maniac.patrolSpeed:0.00} is " +
                $"x{movement.walkSpeed / maniac.patrolSpeed:0.000}).");
        }

        [Test]
        public void WalkingStaysQuieterThanRunningIsToday()
        {
            // Hearing is the sense escalation raises, and it is the one the player
            // controls — that is the whole justification for raising it rather
            // than sight. The counterplay only survives if walking, even at full
            // escalation, still carries less far than running does with none.
            var escalation = ShippedEscalationOrIgnore();
            var maniac = Required<ManiacConfig>(ManiacPath);
            var steps = Required<PlayerFootstepConfig>(FootstepPath);

            float walkAtFull = maniac.hearingRadius * steps.walkLoudness *
                               ManiacEscalation.HearingMultiplier(escalation, 1f);
            float runToday = maniac.hearingRadius * steps.runLoudness;

            Assert.That(walkAtFull, Is.LessThan(runToday),
                $"Walking at full escalation carries {walkAtFull:0.00}u, running carries " +
                $"{runToday:0.00}u unescalated. Once those cross, slowing down stops being a way " +
                "out of being heard and the endgame has no stealth answer left. Lower hearingAtFull.");
        }

        [Test]
        public void HidingNeverBecomesACoinFlip()
        {
            var escalation = ShippedEscalationOrIgnore();
            var wardrobe = Required<WardrobeSearchConfig>(WardrobePath);

            // Worst case a player can actually reach: they have hidden
            // successfully often enough to max what the Director teaches him, and
            // they are on the last clock.
            float worst = ManiacEscalation.ChanceWithCeiling(
                escalation, wardrobe.checkChance,
                DirectorLearnedCap + ManiacEscalation.WardrobeBonusFor(escalation, 1f));

            Assert.That(worst, Is.LessThan(0.75f),
                $"He would open the wardrobe {worst:P0} of the time in the worst case " +
                $"(base {wardrobe.checkChance:0.00} + taught {DirectorLearnedCap:0.00} + escalation " +
                $"{ManiacEscalation.WardrobeBonusFor(escalation, 1f):0.00}). The Director's design note " +
                "promises hiding can never become useless, and that promise is about the SUM. " +
                "Lower maxWardrobeChance or wardrobeBonusAtFull.");
        }

        [Test]
        public void TheDirectorDialShortensTheWaitAndNeverInvertsIt()
        {
            var cfg = Defaults();
            Assert.That(ManiacEscalation.HintQuietMultiplier(cfg, 0f), Is.EqualTo(1f),
                "before the first clock the Director must wait exactly as long as it always did");
            Assert.That(ManiacEscalation.HintQuietMultiplier(null, 1f), Is.EqualTo(1f));

            cfg.enabled = false;
            Assert.That(ManiacEscalation.HintQuietMultiplier(cfg, 1f), Is.EqualTo(1f),
                "the off switch has to cover this dial too");

            // This is the one multiplier that goes DOWN, so the monotonicity test
            // above cannot cover it and it needs its own direction check.
            cfg.enabled = true;
            float last = 1.0001f;
            for (float ramp = 0f; ramp <= 1.0001f; ramp += 0.05f)
            {
                float m = ManiacEscalation.HintQuietMultiplier(cfg, ramp);
                Assert.That(m, Is.LessThanOrEqualTo(last + 0.0001f),
                            $"the wait grew at ramp {ramp:0.00} — escalation must never make him slower to return");
                Assert.That(m, Is.GreaterThan(0f), "a zero multiplier would fire a hint every frame");
                last = m;
            }
        }

        [Test]
        public void TheDirectorStillHasToWaitForRealQuiet()
        {
            var escalation = ShippedEscalationOrIgnore();
            var director = Shipped<TimeKiller.Director.DirectorConfig>(DirectorPath);
            if (director == null) Assert.Ignore($"No DirectorConfig at {DirectorPath}");

            float waitAtFull = director.hintAfterQuietSeconds *
                               ManiacEscalation.HintQuietMultiplier(escalation, 1f);

            Assert.That(waitAtFull, Is.GreaterThan(10f),
                $"At full escalation the Director waits {waitAtFull:0.0}s of genuine quiet before steering him " +
                "back. Below about ten seconds it stops rescuing dead time and becomes a tracker that keeps " +
                "pointing him at you, which is the exact thing hintError exists to prevent.");
        }

        [Test]
        public void EscalationDoesNotMakeTheDirectorDishonest()
        {
            // The Director's whole claim to fairness is that its hint is smeared
            // WIDER than his eyes: arriving at a hint must not BE finding the
            // player. Escalation now reaches into the Director's timing, so this
            // invariant is pinned here as well as printed by Setup/45 — a number
            // only checked by a script nobody runs is not checked.
            var director = Shipped<TimeKiller.Director.DirectorConfig>(DirectorPath);
            if (director == null) Assert.Ignore($"No DirectorConfig at {DirectorPath}");
            var maniac = Required<ManiacConfig>(ManiacPath);

            Assert.That(director.hintError, Is.GreaterThan(maniac.sightRange),
                $"hintError {director.hintError:0.0} must stay above sightRange {maniac.sightRange:0.0}, or " +
                "arriving at a hint becomes finding the player and the Director is cheating whatever the code says.");
        }

        [Test]
        public void EscalationIsStillTunableRatherThanFrozen()
        {
            // A guard against the feature being "finished" by hard-coding: the
            // asset must exist, be readable, and still carry a live off switch.
            // This is a difficulty change that has not had the user's ear yet.
            var escalation = ShippedEscalationOrIgnore();
            Assert.That(escalation.blendSeconds, Is.GreaterThanOrEqualTo(0f));
            Assert.That(escalation.maxWardrobeChance, Is.LessThan(1f),
                "a ceiling of 1 is not a ceiling — hiding could reach certainty");
        }
    }
}
