// EditMode tests for the chase beeline's stall watch.
//
// The arithmetic in ChaseState.Stalled is nearly trivial; what these guard is the
// TUNING, which is the part that can silently go wrong. Two ways to break it:
// set the threshold so high that ordinary chasing reads as grinding (he hands
// every chase to the navigator and the relentless straight-line feel is gone), or
// so low that a maniac stopped dead by a door frame never trips it (which is the
// defect this was written for).
//
// Why the defect existed at all: sight is a centre-to-centre linecast and his body
// is ~0.6u wide, so line of sight does not imply a walkable straight line.
// Measured on CastleWingLDtk by TimeKiller/Verify/Maniac Chase Grind (2026-08-02):
// 9.46% of all sightings had a beeline his body could not complete, rising to
// 18.28% at 6-7u — i.e. worst exactly at sightRange, the moment he acquires you.
using NUnit.Framework;
using TimeKiller.Maniac;
using UnityEditor;

namespace TimeKiller.Tests
{
    public class ManiacChaseTests
    {
        const string ConfigPath = "Assets/Resources/C#/Maniac/Configs/ManiacConfig.asset";

        static ManiacConfig Shipped()
        {
            var cfg = AssetDatabase.LoadAssetAtPath<ManiacConfig>(ConfigPath);
            Assert.That(cfg, Is.Not.Null, $"ManiacConfig.asset missing at {ConfigPath}");
            return cfg;
        }

        /// Ground he covers in one window when nothing is in his way.
        static float FullSpeedTravel(ManiacConfig cfg) => cfg.chaseSpeed * cfg.beelineStallWindow;

        [Test]
        public void AnUnobstructedChaseIsNeverMistakenForGrinding()
        {
            var cfg = Shipped();
            float window = cfg.beelineStallWindow;

            Assert.That(ChaseState.Stalled(cfg, FullSpeedTravel(cfg), window), Is.False,
                "A maniac running at full chase speed is being flagged as stuck.");

            // He also legitimately runs below top speed: accelerating out of a
            // state change, or sliding along a wall he is merely brushing. Neither
            // is grinding, and neither should hand the chase to the navigator.
            Assert.That(ChaseState.Stalled(cfg, FullSpeedTravel(cfg) * 0.6f, window), Is.False,
                "Sliding along a wall at 60% speed reads as stuck — he will path instead of charge.");
        }

        [Test]
        public void BeingStoppedDeadByGeometryIsCaught()
        {
            var cfg = Shipped();
            float window = cfg.beelineStallWindow;

            Assert.That(ChaseState.Stalled(cfg, 0f, window), Is.True,
                "A maniac who has not moved at all is not being detected as stuck.");

            // The real case is not perfectly zero — pressed into a door frame he
            // still creeps a little as the collider resolves.
            Assert.That(ChaseState.Stalled(cfg, FullSpeedTravel(cfg) * 0.1f, window), Is.True,
                "Grinding on a corner at 10% progress is being read as a normal chase.");
        }

        [Test]
        public void TheStallWatchHasAWorkingGapBetweenChasingAndStuck()
        {
            var cfg = Shipped();
            // If these ever meet, the setting is either always-on or never-on and
            // the two tests above would both still pass at the boundary.
            Assert.That(cfg.beelineStallFraction, Is.GreaterThan(0f).And.LessThan(1f),
                "beelineStallFraction outside (0,1): the stall watch is always or never firing.");
            Assert.That(cfg.beelineStallWindow, Is.GreaterThan(0f),
                "beelineStallWindow is 0 — every check would measure zero travel and always stall.");
            Assert.That(cfg.beelineNavSeconds, Is.GreaterThan(0f),
                "beelineNavSeconds is 0 — the navigator would be handed control for no time at all.");
        }

        [Test]
        public void TheFallbackIsTemporarySoTheBeelineStaysTheDefault()
        {
            var cfg = Shipped();
            // The straight line is the intended look — fast, relentless, no
            // hesitation. The navigator is the exception, so its takeover must be
            // short enough to be a stumble rather than a change of character.
            Assert.That(cfg.beelineNavSeconds, Is.LessThan(cfg.loseSightSeconds),
                $"The navigator drives for {cfg.beelineNavSeconds}s but he only keeps chasing " +
                $"for {cfg.loseSightSeconds}s after losing sight — the fallback outlives the chase.");
        }
    }
}
