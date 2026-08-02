// EditMode guards for the fear system's shipped tuning.
//
// The conductor itself is stateful (it smooths over frames, holds, remembers a
// peak), so it belongs in a PlayMode suite. What CAN be pinned here without a
// scene is the shape of the curves and the relationships between the numbers —
// and that is where this system's failure modes actually live, because every one
// of them is a tuning mistake rather than a logic bug:
//
//   a volume curve that starts loud       -> no room left to build (the old 0.55)
//   a curve that dips                     -> the heart SLOWS as he closes
//   an outer radius smaller than the view -> he is on screen before you feel it
//   rise slower than recovery             -> fear that fades faster than it grows
//
// Each of these shipped at some point in the old heartbeat.
using NUnit.Framework;
using TimeKiller.Fear;
using UnityEditor;
using UnityEngine;

namespace TimeKiller.Tests
{
    public class FearConfigTests
    {
        const string ConfigPath = "Assets/Resources/C#/Fear/Configs/FearConfig.asset";

        static FearConfig Shipped()
        {
            var cfg = AssetDatabase.LoadAssetAtPath<FearConfig>(ConfigPath);
            Assert.That(cfg, Is.Not.Null, $"FearConfig.asset missing at {ConfigPath} — run TimeKiller/Setup/45.");
            return cfg;
        }

        static float BpmAt(FearConfig cfg, float fear) =>
            Mathf.Lerp(cfg.minBpm, cfg.maxBpm, Mathf.Clamp01(cfg.bpmCurve.Evaluate(fear)));

        static float VolumeAt(FearConfig cfg, float fear) =>
            cfg.maxHeartVolume * Mathf.Clamp01(cfg.heartVolumeCurve.Evaluate(fear));

        [Test]
        public void TheHeartNeverSlowsOrQuietensAsFearRises()
        {
            var cfg = Shipped();
            // A dip anywhere means the heart decelerates while the maniac closes,
            // which reads as a bug to the player even if it is only a stray tangent.
            float prevBpm = -1f, prevVol = -1f;
            for (float f = 0f; f <= 1.0001f; f += 0.01f)
            {
                float bpm = BpmAt(cfg, f), vol = VolumeAt(cfg, f);
                Assert.That(bpm, Is.GreaterThanOrEqualTo(prevBpm - 0.01f),
                    $"BPM dips at fear {f:0.00} — the heart slows down as danger increases.");
                Assert.That(vol, Is.GreaterThanOrEqualTo(prevVol - 0.001f),
                    $"Volume dips at fear {f:0.00} — the heart gets quieter as danger increases.");
                prevBpm = bpm; prevVol = vol;
            }
        }

        [Test]
        public void TheRateMatchesTheDesignTarget()
        {
            var cfg = Shipped();
            Assert.That(BpmAt(cfg, 0.15f), Is.InRange(52f, 62f), "Fear 0.15 should be a slow, barely-there pulse.");
            Assert.That(BpmAt(cfg, 0.35f), Is.InRange(70f, 88f), "Fear 0.35 should be quiet but noticeable.");
            Assert.That(BpmAt(cfg, 0.60f), Is.InRange(100f, 120f), "Fear 0.60 should be clearly threatening.");
            Assert.That(BpmAt(cfg, 0.80f), Is.InRange(128f, 148f), "Fear 0.80 should be strong.");
            Assert.That(BpmAt(cfg, 1.00f), Is.InRange(152f, 172f), "Fear 1.00 should be urgent panic.");
        }

        [Test]
        public void TheQuietEndIsActuallyQuiet()
        {
            var cfg = Shipped();
            // REGRESSION: the old heartbeat ramped from quietVolume 0.55-0.72, so
            // the faintest beat you could hear was already past half volume and
            // the loud end had nowhere to go. The build IS the mechanic.
            Assert.That(VolumeAt(cfg, 0f), Is.LessThan(0.02f), "Fear 0 is not silent.");
            Assert.That(VolumeAt(cfg, 0.15f), Is.InRange(0.02f, 0.12f),
                "The first audible beat is not faint — there is no room left to build.");
            Assert.That(VolumeAt(cfg, 1f), Is.GreaterThan(VolumeAt(cfg, 0.15f) * 6f),
                "Maximum fear is not dramatically louder than the first faint beat.");
        }

        [Test]
        public void FearStartsWellBeforeHeIsOnScreen()
        {
            var cfg = Shipped();
            // The camera shows ~10.7 world units across. The old heartbeat's range
            // was ~10.4, so the warning arrived at the same moment as the sight of
            // him — which is no warning at all.
            const float cameraWidth = 10.7f;
            Assert.That(cfg.fearStartDistance, Is.GreaterThan(cameraWidth * 2f),
                $"fearStartDistance {cfg.fearStartDistance} is not comfortably beyond the ~{cameraWidth}u view.");
            Assert.That(cfg.closeDangerDistance, Is.LessThan(cfg.fearStartDistance),
                "closeDangerDistance must sit inside fearStartDistance.");
            Assert.That(cfg.closeDangerDistance, Is.GreaterThan(0.9f),
                "closeDangerDistance is at or inside his attackRange — peak fear would arrive after the swing.");
        }

        [Test]
        public void FearRisesFasterThanItFades()
        {
            var cfg = Shipped();
            // The asymmetry is the whole aftershock design. If recovery were the
            // faster of the two, escaping would feel instantly safe.
            Assert.That(cfg.riseSeconds, Is.LessThan(cfg.recoverySeconds),
                "Fear fades faster than it builds — there is no aftershock.");
            Assert.That(cfg.detectionRiseSeconds, Is.LessThan(cfg.riseSeconds),
                "The detection response is not faster than an ordinary approach.");
            Assert.That(cfg.recoveryDelaySeconds, Is.GreaterThan(0f),
                "No hold before recovery — breaking line of sight collapses fear instantly.");
        }

        [Test]
        public void AwarenessColoursFearWithoutManufacturingIt()
        {
            var cfg = Shipped();
            // THE central design rule. These are MULTIPLIERS, so a far-away enemy
            // (proximity ~0) cannot produce panic whatever state he is in. The old
            // system used floors and a Detected maniac 30u away snapped the heart
            // straight to 150 bpm.
            Assert.That(cfg.detectedMultiplier, Is.GreaterThan(cfg.suspiciousMultiplier),
                "Being detected is not more frightening than being suspected.");
            Assert.That(cfg.suspiciousMultiplier, Is.GreaterThan(cfg.unawareMultiplier),
                "Suspicion is not more frightening than being unnoticed.");

            // The proof of the rule: maximum awareness at the outer edge must stay
            // far below the panic threshold.
            float atEdge = Mathf.Clamp01(cfg.distanceCurve.Evaluate(0.02f)) * cfg.detectedMultiplier
                           * (1f + cfg.closingBoost);
            Assert.That(atEdge, Is.LessThan(cfg.threatAt),
                $"A maniac detecting you at the outer radius reaches fear {atEdge:0.00}, which is " +
                "already Threat. Awareness is manufacturing fear instead of colouring it.");
        }

        /// Fear from a detection at a given distance, mirroring FearConductor.
        static float DetectedAt(FearConfig cfg, float distance)
        {
            float t = Mathf.InverseLerp(cfg.closeDangerDistance, cfg.fearStartDistance, distance);
            float prox = Mathf.Clamp01(1f - t);
            return Mathf.Clamp01(Mathf.Clamp01(cfg.distanceCurve.Evaluate(prox)) * cfg.detectedMultiplier);
        }

        [Test]
        public void DetectionIsAMultiplierAndNotAFloor()
        {
            var cfg = Shipped();
            // THE actual requirement from the design brief: "Do not simply add
            // fixed BPM floors such as Suspicious = 108 or Detected = 150. Those
            // create unnatural jumps." A floor makes fear CONSTANT across distance
            // once the flag flips; a multiplier keeps distance governing. That is
            // the property worth pinning, and it holds at any tuning.
            //
            // This test previously asserted that detection at 10u must not reach
            // Panic. That was NOT a design requirement — it was a value picked
            // during an unprompted retune from a single bot session, and the
            // awareness ladder has since been restored to the user-approved
            // values. How frightening a mid-range sighting should be is HIS call;
            // the test's job is to catch a floor sneaking back in, not to encode
            // a difficulty preference.
            float far = DetectedAt(cfg, 20f);
            float mid = DetectedAt(cfg, 10f);
            float near = DetectedAt(cfg, 5f);

            Assert.That(far, Is.LessThan(mid),
                $"Detected fear is the same at 20u ({far:0.00}) and 10u ({mid:0.00}) — " +
                "distance has stopped governing, which means a floor is in play.");
            Assert.That(mid, Is.LessThanOrEqualTo(near),
                "Detected fear does not increase as he closes.");
            Assert.That(far, Is.LessThan(cfg.panicAt),
                $"Being detected at 20u already reaches {far:0.00} — a threat that " +
                "far away should not produce panic on awareness alone.");
        }

        [Test]
        public void DetectionScalesSmoothlyWithDistance()
        {
            var cfg = Shipped();
            // No cliff anywhere along the approach. A step here would be audible
            // as the heartbeat lurching for no visible reason.
            float prev = -1f;
            for (float d = cfg.fearStartDistance; d >= cfg.closeDangerDistance; d -= 1f)
            {
                float f = DetectedAt(cfg, d);
                Assert.That(f, Is.GreaterThanOrEqualTo(prev - 0.001f),
                    $"Fear drops as he gets closer, at {d}u.");
                if (prev >= 0f)
                    Assert.That(f - prev, Is.LessThan(0.2f),
                        $"Fear jumps {f - prev:0.00} in a single metre at {d}u — that is a step, not a build.");
                prev = f;
            }
        }

        [Test]
        public void TheAmbienceDucksWithoutMutingTheLevel()
        {
            var cfg = Shipped();
            // REGRESSION: the old duck pulled the world to 0.08 — effectively
            // muting the castle, and taking the maniac's own footsteps with it at
            // exactly the moment the player most needs to hear where he is.
            Assert.That(cfg.ambienceAtMaxFear, Is.GreaterThan(0.5f),
                $"Ambience drops to {cfg.ambienceAtMaxFear} at max fear — that is muting the level, not ducking it.");
            Assert.That(cfg.ambienceAtMaxFear, Is.LessThan(1f), "Ambience never ducks at all.");
            Assert.That(cfg.duckOutSeconds, Is.GreaterThan(cfg.duckInSeconds),
                "The world returns faster than it recedes — the quiet should linger.");
        }

        [Test]
        public void TheStingCannotSpam()
        {
            var cfg = Shipped();
            // His Detected flag flickers every time a pillar breaks line of sight,
            // so without a real cooldown the sting becomes a rhythm section.
            Assert.That(cfg.stingCooldown, Is.GreaterThan(5f),
                $"A {cfg.stingCooldown}s sting cooldown will let repeated AI state changes spam it.");
            Assert.That(cfg.stingNeedsFear, Is.GreaterThan(0f),
                "A sting can fire at zero fear — a jump scare for a threat that cannot reach you.");
        }
    }
}
