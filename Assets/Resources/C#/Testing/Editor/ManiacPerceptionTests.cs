// EditMode tests for the maniac's detection maths.
//
// These call the pure statics on ManiacPerception (RateFor / StepAwareness /
// GuessErrorFor), so there is no scene, no lighting, no physics and no frames —
// which is exactly why they are trustworthy. The play-mode probe that first
// measured these numbers gave a wrong answer twice (a teleport read as running,
// and the level's torches confounded distance) before it was made honest. None
// of those failure modes can reach this file: exposure and speed are arguments.
//
// What is deliberately NOT here: line of sight, the 1s stare, the cone sweep.
// Those need real frames and belong in PlayMode tests.
using NUnit.Framework;
using TimeKiller.Maniac;
using UnityEditor;
using UnityEngine;

namespace TimeKiller.Tests
{
    public class ManiacPerceptionTests
    {
        const string ConfigPath = "Assets/Resources/C#/Maniac/Configs/ManiacConfig.asset";

        static ManiacConfig Shipped()
        {
            var cfg = AssetDatabase.LoadAssetAtPath<ManiacConfig>(ConfigPath);
            Assert.That(cfg, Is.Not.Null, $"ManiacConfig.asset missing at {ConfigPath}");
            return cfg;
        }

        // Straight ahead, in deep shadow, standing still: the hardest honest case
        // the player can present while still being in front of him.
        static float StillInShadow(ManiacConfig cfg, float dist) =>
            ManiacPerception.RateFor(cfg, dist, 0f, cfg.ambientExposure, 0f);

        static float SecondsToSpot(ManiacConfig cfg, float rate) =>
            rate <= 0f ? float.PositiveInfinity : 1f / (rate * cfg.awarenessFillRate);

        // ---- the vision cone -------------------------------------------------

        [Test]
        public void BehindHimIsABlindSpotBeyondArmsReach()
        {
            var cfg = Shipped();
            float behind = ManiacPerception.RateFor(cfg, cfg.peripheralRange + 0.5f, 180f, 1f, 1f);
            Assert.That(behind, Is.EqualTo(0f), "He can see directly behind himself.");
        }

        [Test]
        public void PointBlankIgnoresFacingEntirely()
        {
            var cfg = Shipped();
            // Walking into his back must still get you caught — this is the rule
            // that stops him overrunning the player and standing there oblivious.
            float rate = ManiacPerception.RateFor(cfg, cfg.proximityRange - 0.1f, 180f, 0f, 0f);
            Assert.That(rate, Is.EqualTo(1f), "Point-blank detection is not ignoring the facing cone.");
        }

        [Test]
        public void PeripheralIsWeakerThanCentralAndOnlyWorksClose()
        {
            var cfg = Shipped();
            float peripheralAngle = (cfg.centralConeAngle * 0.5f + cfg.peripheralConeAngle * 0.5f) * 0.5f;
            float near = ManiacPerception.RateFor(cfg, cfg.peripheralRange - 0.5f, peripheralAngle, 1f, 1f);
            float central = ManiacPerception.RateFor(cfg, cfg.peripheralRange - 0.5f, 0f, 1f, 1f);
            float tooFar = ManiacPerception.RateFor(cfg, cfg.peripheralRange + 0.5f, peripheralAngle, 1f, 1f);

            Assert.That(near, Is.GreaterThan(0f), "Peripheral vision does nothing at all.");
            Assert.That(near, Is.LessThan(central), "Peripheral is not weaker than central.");
            Assert.That(tooFar, Is.EqualTo(0f), "Peripheral vision reaches past peripheralRange.");
        }

        [Test]
        public void NothingIsSeenBeyondSightRange()
        {
            var cfg = Shipped();
            Assert.That(ManiacPerception.RateFor(cfg, cfg.sightRange + 0.1f, 0f, 1f, 1f), Is.EqualTo(0f));
        }

        // ---- the falloff curve (defect 2 of the 2026-07-27 pass) -------------

        [Test]
        public void DetectionWeakensWithDistanceButTheMidRangeIsNotDead()
        {
            var cfg = Shipped();
            // REGRESSION: the falloff was linear to zero at sightRange, so at 6u a
            // motionless player in shadow needed ~23s of unbroken exposure and was
            // effectively invisible. Monotonic AND usable is the requirement.
            float prev = float.MaxValue;
            for (float d = 2.5f; d <= cfg.sightRange - 0.5f; d += 0.5f)
            {
                float rate = StillInShadow(cfg, d);
                Assert.That(rate, Is.LessThan(prev), $"Detection did not weaken from {d - 0.5f}u to {d}u.");
                prev = rate;
            }

            float atSixSeconds = SecondsToSpot(cfg, StillInShadow(cfg, 6f));
            Assert.That(atSixSeconds, Is.LessThan(15f),
                $"A motionless player in shadow at 6u takes {atSixSeconds:F1}s to spot — " +
                "the mid range is a dead zone again. Check sightFalloffPower.");
        }

        [Test]
        public void StillnessAndShadowAreRealTools()
        {
            var cfg = Shipped();
            // The stealth promise: standing still in the dark must beat running in
            // the light by a wide margin, or there is no stealth game.
            float hiding = ManiacPerception.RateFor(cfg, 5f, 0f, cfg.ambientExposure, 0f);
            float careless = ManiacPerception.RateFor(cfg, 5f, 0f, 1f, cfg.runSpeedThreshold + 1f);
            Assert.That(hiding * 3f, Is.LessThan(careless),
                "Hiding still and running lit are too close together to matter.");
        }

        // ---- awareness memory (defect 2, second half) -------------------------

        [Test]
        public void AwarenessHoldsBrieflyBeforeItDrains()
        {
            var cfg = Shipped();
            // REGRESSION: awareness used to drain from the instant cover broke, so
            // one step behind a pillar wiped a full meter in 1.25s.
            float held = ManiacPerception.StepAwareness(cfg, 1f, 0f, 0.1f, cfg.awarenessHoldSeconds * 0.5f);
            Assert.That(held, Is.EqualTo(1f), "The meter started draining during the hold window.");

            float draining = ManiacPerception.StepAwareness(cfg, 1f, 0f, 0.1f, cfg.awarenessHoldSeconds + 0.1f);
            Assert.That(draining, Is.LessThan(1f), "The meter never drains at all — he never forgets.");
        }

        [Test]
        public void HeLearnsFasterThanHeForgetsWhenThePlayerIsCareless()
        {
            var cfg = Shipped();
            // The asymmetry that made stalking impossible: a MOVING player at 5u
            // filled the meter at 0.22/s while it drained at 0.8/s, so ducking
            // behind one pillar erased everything he had built up.
            //
            // Note this is deliberately the walking case, not the still one. A
            // motionless player in shadow SHOULD out-wait him — that is the
            // stealth tool, and asserting otherwise would be asserting a worse game.
            float fill = ManiacPerception.RateFor(cfg, 5f, 0f, cfg.ambientExposure, 1f) * cfg.awarenessFillRate;
            Assert.That(fill, Is.GreaterThan(cfg.awarenessDrainRate),
                $"A walking player at 5u fills him at {fill:F2}/s but he forgets at " +
                $"{cfg.awarenessDrainRate:F2}/s — stalking cannot survive normal cover.");
        }

        [Test]
        public void AMotionlessPlayerInShadowCanOutwaitHim()
        {
            var cfg = Shipped();
            // The other side of the same coin, asserted so nobody "fixes" the test
            // above by cranking the fill rate until stillness stops working.
            float fill = StillInShadow(cfg, 5f) * cfg.awarenessFillRate;
            Assert.That(fill, Is.LessThan(cfg.awarenessDrainRate),
                "Standing still in the dark no longer buys the player anything.");
        }

        [Test]
        public void AwarenessStaysInRange()
        {
            var cfg = Shipped();
            Assert.That(ManiacPerception.StepAwareness(cfg, 0.99f, 1f, 1f, 0f), Is.EqualTo(1f));
            Assert.That(ManiacPerception.StepAwareness(cfg, 0.01f, 0f, 10f, 99f), Is.EqualTo(0f));
        }

        // ---- the suspicion guess (2026-07-28 pass) ---------------------------

        [Test]
        public void TheGuessIsWrongAtFirstAndSharpensAsHeGrowsCertain()
        {
            var cfg = Shipped();
            // REGRESSION: suspicion handed him the player's exact live position, so
            // being half-noticed was identical to being seen and he could never
            // check the wrong side of a pillar.
            float atThreshold = ManiacPerception.GuessErrorFor(cfg, cfg.suspicionThreshold);
            float halfway = ManiacPerception.GuessErrorFor(cfg, (cfg.suspicionThreshold + 1f) * 0.5f);
            float certain = ManiacPerception.GuessErrorFor(cfg, 1f);

            Assert.That(atThreshold, Is.EqualTo(cfg.suspicionGuessError).Within(0.001f),
                "A brand-new suspicion is not uncertain at all.");
            Assert.That(halfway, Is.LessThan(atThreshold), "The guess does not sharpen with certainty.");
            Assert.That(certain, Is.EqualTo(0f).Within(0.001f),
                "Even fully certain, his guess never lands on the player.");
        }

        // ---- hearing through walls (2026-08-02 pass) -------------------------

        static TimeKiller.Player.PlayerFootstepConfig Footsteps()
        {
            var cfg = AssetDatabase.LoadAssetAtPath<TimeKiller.Player.PlayerFootstepConfig>(
                "Assets/Resources/C#/Player/Configs/PlayerFootstepConfig.asset");
            Assert.That(cfg, Is.Not.Null, "PlayerFootstepConfig.asset missing.");
            return cfg;
        }

        [Test]
        public void EveryWallShortensHowFarANoiseCarries()
        {
            var cfg = Shipped();
            // REGRESSION: hearing was the only sense that ignored geometry. A
            // footstep two rooms away through solid stone set the noise fields
            // exactly as a footstep taken beside him did.
            float prev = ManiacPerception.HeardRadiusFor(cfg, 1f, 0);
            Assert.That(prev, Is.EqualTo(cfg.hearingRadius).Within(0.001f),
                "An unobstructed noise no longer carries the full hearing radius.");

            for (int walls = 1; walls <= 4; walls++)
            {
                float carried = ManiacPerception.HeardRadiusFor(cfg, 1f, walls);
                Assert.That(carried, Is.LessThan(prev),
                    $"Wall {walls} did not muffle the noise any further than {walls - 1} did.");
                prev = carried;
            }
        }

        [Test]
        public void TheShippedConfigActuallyMufflesAtAll()
        {
            var cfg = Shipped();
            // hearingWallMuffle = 1 is a legitimate setting — it restores the old
            // behaviour exactly — which is precisely why shipping it by accident
            // has to fail loudly rather than silently undo this pass.
            Assert.That(cfg.hearingWallMuffle, Is.LessThan(1f),
                "hearingWallMuffle is 1: walls have stopped muffling and his hearing is geometry-blind again.");
        }

        [Test]
        public void AMuffleOfOneReproducesTheOldGeometryBlindHearing()
        {
            var cfg = UnityEngine.Object.Instantiate(Shipped());   // a copy — never touch the asset
            try
            {
                cfg.hearingWallMuffle = 1f;
                Assert.That(ManiacPerception.HeardRadiusFor(cfg, 1f, 3),
                    Is.EqualTo(ManiacPerception.HeardRadiusFor(cfg, 1f, 0)).Within(0.001f),
                    "The escape hatch back to the old behaviour does not work.");
            }
            finally { UnityEngine.Object.DestroyImmediate(cfg); }
        }

        [Test]
        public void AWallIsCoverForWalkingButNotForSprinting()
        {
            var cfg = Shipped();
            var steps = Footsteps();
            // The point of the whole change: putting a wall between you must make
            // WALKING genuinely safe, while running remains a mistake you can be
            // caught for if he happens to be right on the other side of it.
            float walking = ManiacPerception.HeardRadiusFor(cfg, steps.walkLoudness, 1);
            float running = ManiacPerception.HeardRadiusFor(cfg, steps.runLoudness, 1);

            Assert.That(walking, Is.LessThan(2f),
                $"Walking still carries {walking:F2}u through a wall — a wall is not cover.");
            Assert.That(running, Is.GreaterThan(walking * 2f),
                "Running and walking through a wall are too close together for the choice to matter.");
            Assert.That(running, Is.GreaterThan(1.5f),
                $"Running only carries {running:F2}u through a wall — one wall has become a total cloak.");
        }

        [Test]
        public void HeCannotOutrunTheSuspiciousApproach()
        {
            var cfg = Shipped();
            // The whole point of the stare-and-slow-approach: a suspicion must be
            // escapable at a WALK, or the beat is just a slower death sentence.
            var movement = AssetDatabase.LoadAssetAtPath<TimeKiller.Player.PlayerMovementConfig>(
                "Assets/Resources/C#/Player/Configs/PlayerMovementConfig.asset");
            Assert.That(movement, Is.Not.Null, "PlayerMovementConfig.asset missing.");
            Assert.That(cfg.suspiciousApproachSpeed, Is.LessThan(movement.walkSpeed),
                $"He closes on a suspicion at {cfg.suspiciousApproachSpeed} but the player only " +
                $"walks at {movement.walkSpeed} — backing away is impossible.");
        }
    }
}
