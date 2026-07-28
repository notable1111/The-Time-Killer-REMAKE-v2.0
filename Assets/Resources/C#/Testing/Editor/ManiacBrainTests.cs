// EditMode tests for the maniac's utility brain.
//
// ManiacBrain.Score is a pure static — no scene, no frames, no physics — which
// is why these run in milliseconds and give the same answer every time. That was
// always the intent (see the header of ManiacBrain.cs); the tests just never got
// written, and the 2026-07-27 pass found four real defects living in exactly this
// arithmetic. Every one of them is pinned below.
//
// Two kinds of test here, deliberately kept apart:
//   INVARIANTS use a fresh config (code defaults) and assert things that must be
//     true whatever anyone tunes — e.g. nothing may ever outrank a live chase.
//   SHIPPED-CONFIG tests load ManiacConfig.asset and assert the game as it will
//     actually play. These are meant to fail if someone detunes it, and the
//     failure message says which knob moved.
using NUnit.Framework;
using TimeKiller.Maniac;
using UnityEditor;
using UnityEngine;

namespace TimeKiller.Tests
{
    public class ManiacBrainTests
    {
        const string ConfigPath = "Assets/Resources/C#/Maniac/Configs/ManiacConfig.asset";

        const int Patrol = 0, Investigate = 1, Search = 2, Chase = 3;

        static ManiacConfig Defaults() => ScriptableObject.CreateInstance<ManiacConfig>();

        static ManiacConfig Shipped()
        {
            var cfg = AssetDatabase.LoadAssetAtPath<ManiacConfig>(ConfigPath);
            Assert.That(cfg, Is.Not.Null, $"ManiacConfig.asset missing at {ConfigPath}");
            return cfg;
        }

        /// The bar a behaviour must clear to pull him out of patrolling: the patrol
        /// baseline plus the stickiness bonus, which belongs to whatever he is
        /// already doing. Getting this wrong is what made two thirds of his
        /// hearing decorative.
        static float PatrolFloor(ManiacConfig cfg) => cfg.brainPatrolBaseline + cfg.brainStickiness;

        // ---- invariants -----------------------------------------------------

        [Test]
        public void SeeingThePlayerAlwaysWins()
        {
            var cfg = Defaults();
            // Worst case for chase: a fresh noise right under his feet, which is
            // the single loudest competing pull the brain can produce.
            var s = ManiacBrain.Score(cfg, seen: true, tSeen: 0f, tNoise: 0f, distNoise: 0f);
            Assert.That(s[Chase], Is.GreaterThanOrEqualTo(s[Investigate]),
                "A noise outranked a player he can SEE — he would break off a live chase.");
            Assert.That(s[Chase], Is.GreaterThanOrEqualTo(s[Search]));
            Assert.That(s[Chase], Is.GreaterThanOrEqualTo(s[Patrol]));
        }

        [Test]
        public void BreadcrumbGraceWindowIsProtected()
        {
            var cfg = Defaults();
            // Just after losing sight, the trail is the better lead than a noise.
            // Stickiness belongs to Chase here because that is what he is doing.
            float t = cfg.loseSightSeconds * 0.5f;
            var s = ManiacBrain.Score(cfg, seen: false, tSeen: t, tNoise: 0f, distNoise: 1f);
            Assert.That(s[Chase] + cfg.brainStickiness, Is.GreaterThan(s[Investigate]),
                "A footstep pulled him off the breadcrumb trail inside the lose-sight window.");
        }

        [Test]
        public void PatrolIsTheFloorWhenNothingIsHappening()
        {
            var cfg = Defaults();
            var s = ManiacBrain.Score(cfg, seen: false, tSeen: float.MaxValue,
                                      tNoise: float.MaxValue, distNoise: 99f);
            Assert.That(s[Patrol], Is.GreaterThan(s[Investigate]));
            Assert.That(s[Patrol], Is.GreaterThan(s[Search]));
            Assert.That(s[Patrol], Is.GreaterThan(s[Chase]));
        }

        [Test]
        public void NoiseInterestFallsOffWithDistanceAndAge()
        {
            var cfg = Defaults();
            float near = ManiacBrain.Score(cfg, false, float.MaxValue, 0f, 1f)[Investigate];
            float far = ManiacBrain.Score(cfg, false, float.MaxValue, 0f, cfg.hearingRadius)[Investigate];
            float stale = ManiacBrain.Score(cfg, false, float.MaxValue, 3f, 1f)[Investigate];

            Assert.That(near, Is.GreaterThan(far), "Distance must still bias his interest.");
            Assert.That(stale, Is.LessThan(near), "An older noise must pull less than a fresh one.");
        }

        // ---- the shipped game ------------------------------------------------

        [Test]
        public void HeReactsAcrossHisWholeHearingRadius()
        {
            var cfg = Shipped();
            // REGRESSION (2026-07-27): perception registered noise to hearingRadius
            // 9 but the brain discarded anything past 6.2u, so he "heard" you and
            // did nothing. A fresh noise anywhere he can hear must beat patrol.
            for (float d = 0f; d <= cfg.hearingRadius; d += 0.5f)
            {
                float investigate = ManiacBrain.Score(cfg, false, float.MaxValue, 0f, d)[Investigate];
                Assert.That(investigate, Is.GreaterThan(PatrolFloor(cfg)),
                    $"A fresh noise at {d}u does not beat patrol ({PatrolFloor(cfg):F2}), " +
                    $"so he ignores it — yet hearingRadius says he can hear to {cfg.hearingRadius}u. " +
                    "Check noiseFarWeight / brainNoiseWeight / brainPatrolBaseline.");
            }
        }

        [Test]
        public void AFreshCloseNoiseInterruptsAColdSearch()
        {
            var cfg = Shipped();
            // REGRESSION (2026-07-27): for the first ~5s of a hunt a footstep one
            // unit away scored 0.71 against Search's 0.98 and was ignored, so the
            // player could sprint past his back. Once the grace window has passed,
            // new information must win.
            float tSeen = cfg.loseSightSeconds + 1f;
            var s = ManiacBrain.Score(cfg, false, tSeen, 0f, 1f);
            Assert.That(s[Investigate], Is.GreaterThan(s[Search] + cfg.brainStickiness),
                "A footstep 1u away did not divert him off a cold search. " +
                "Check brainFreshNoiseBoost.");
        }

        [Test]
        public void TheFreshNoiseBoostCannotOutrankAChase()
        {
            var cfg = Shipped();
            // The boost is gated to the hunting window precisely so it can never
            // do this. If the gate is ever loosened, this is the test that fails.
            var s = ManiacBrain.Score(cfg, seen: true, tSeen: 0f, tNoise: 0f, distNoise: 0.5f);
            Assert.That(s[Chase], Is.GreaterThanOrEqualTo(s[Investigate]),
                "The fresh-noise boost is leaking into the seen case.");
        }

        [Test]
        public void HeEventuallyGivesUpAndGoesBackToPatrol()
        {
            var cfg = Shipped();
            // Past the search memory with no new stimulus, patrol must win, or he
            // hunts forever and the player can never reset the encounter.
            var s = ManiacBrain.Score(cfg, false, cfg.brainSearchMemory + 1f, float.MaxValue, 99f);
            Assert.That(s[Patrol], Is.GreaterThan(s[Search]));
            Assert.That(s[Patrol], Is.GreaterThan(s[Chase]));
        }
    }
}
