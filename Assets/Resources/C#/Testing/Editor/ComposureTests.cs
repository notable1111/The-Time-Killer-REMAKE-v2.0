// EditMode tests for composure (sanity).
//
// The feature is two pure statics — Step and LoudnessFor — and they are static
// precisely so this suite can pin them without a scene, a light or a frame. The
// light SAMPLER is deliberately not tested here: it reads live Light2D
// components, so it needs a scene, and it is an acknowledged approximation.
// What is tested is the mechanic, which is the part that decides difficulty.
//
// The shipped-config tests are the ones that matter. They encode the user's
// three rulings of 2026-08-27 as assertions, so a later retune that quietly
// breaks one of them fails here instead of in a playtest nobody attributes.
using NUnit.Framework;
using TimeKiller.Maniac;
using TimeKiller.Player;
using TimeKiller.Sanity;
using UnityEditor;
using UnityEngine;

namespace TimeKiller.Tests
{
    public class ComposureTests
    {
        const string ComposurePath = "Assets/Resources/C#/Sanity/Configs/ComposureConfig.asset";
        const string FootstepPath = "Assets/Resources/C#/Player/Configs/PlayerFootstepConfig.asset";
        const string ManiacPath = "Assets/Resources/C#/Maniac/Configs/ManiacConfig.asset";

        static ComposureConfig Defaults() => ScriptableObject.CreateInstance<ComposureConfig>();

        static ComposureConfig ShippedOrIgnore()
        {
            var cfg = AssetDatabase.LoadAssetAtPath<ComposureConfig>(ComposurePath);
            if (cfg == null)
                Assert.Ignore($"No composure config at {ComposurePath} — run TimeKiller/Setup/58 - Create Composure Config (sanity). " +
                              "Until then the feature is uninstalled, which is a valid state.");
            return cfg;
        }

        // ---- invariants -----------------------------------------------------

        [Test]
        public void LightKeepsYouComposed()
        {
            var cfg = Defaults();
            float c = PlayerComposure.Step(cfg, 0.5f, inDarkness: false, hiding: false, deltaTime: 1f);
            Assert.That(c, Is.GreaterThan(0.5f), "standing in light must recover composure");
        }

        [Test]
        public void OnlyTotalDarknessDrains()
        {
            // The user's first ruling, as an assertion: being merely un-lit is
            // not the trigger, being in DARKNESS is. Step is told which, so this
            // pins that a "lit" step never costs anything.
            var cfg = Defaults();
            for (float start = 0.3f; start <= 1f; start += 0.1f)
                Assert.That(PlayerComposure.Step(cfg, start, false, false, 0.5f),
                            Is.GreaterThanOrEqualTo(start),
                            "a lit step must never drain, at any starting value");
        }

        [Test]
        public void TheFloorHolds()
        {
            // The user's second ruling. Twenty minutes in the dark must not take
            // composure below the floor - no death spirals in a 3 HP game.
            var cfg = Defaults();
            float c = 1f;
            for (int i = 0; i < 1200; i++) c = PlayerComposure.Step(cfg, c, true, false, 1f);
            Assert.That(c, Is.EqualTo(Mathf.Clamp01(cfg.floor)).Within(0.0001f),
                        "composure fell through the floor");
        }

        [Test]
        public void ComposureNeverExceedsFull()
        {
            var cfg = Defaults();
            float c = 1f;
            for (int i = 0; i < 100; i++) c = PlayerComposure.Step(cfg, c, false, false, 1f);
            Assert.That(c, Is.EqualTo(1f), "recovery must saturate at 1, not run past it");
        }

        [Test]
        public void HidingCostsLessThanTheDark()
        {
            // Both drain, but sitting in a box should be gentler than standing in
            // a black corridor. If this inverts, hiding becomes the worse option
            // and the anti-turtle dial has turned into a hiding ban.
            var cfg = Defaults();
            float hid = PlayerComposure.Step(cfg, 1f, false, hiding: true, deltaTime: 1f);
            float dark = PlayerComposure.Step(cfg, 1f, inDarkness: true, hiding: false, deltaTime: 1f);
            Assert.That(hid, Is.GreaterThan(dark), "hiding drained faster than open darkness");
        }

        [Test]
        public void HidingInTheDarkDoesNotDoubleDrain()
        {
            // A wardrobe IS dark, so the two must not stack - that would make
            // the intended hiding rate a lie the moment the lights were out.
            var cfg = Defaults();
            float both = PlayerComposure.Step(cfg, 1f, inDarkness: true, hiding: true, 1f);
            float hidOnly = PlayerComposure.Step(cfg, 1f, inDarkness: false, hiding: true, 1f);
            Assert.That(both, Is.EqualTo(hidOnly).Within(0.0001f));
        }

        [Test]
        public void TheOffSwitchIsReallyOff()
        {
            var cfg = Defaults();
            cfg.enabled = false;
            Assert.That(PlayerComposure.Step(cfg, 0.5f, true, true, 10f), Is.EqualTo(0.5f),
                        "disabled, composure must not move at all");
            Assert.That(PlayerComposure.LoudnessFor(cfg, 0f), Is.EqualTo(1f),
                        "disabled, the loudness multiplier must be exactly 1");
            Assert.That(PlayerComposure.LoudnessFor(null, 0f), Is.EqualTo(1f),
                        "no config at all must also be an identity");
        }

        [Test]
        public void FullComposureCostsNothing()
        {
            var cfg = Defaults();
            Assert.That(PlayerComposure.LoudnessFor(cfg, 1f), Is.EqualTo(1f),
                        "a composed player must be exactly as loud as their footstep config says");
        }

        [Test]
        public void LoudnessRisesMonotonicallyAsComposureFalls()
        {
            var cfg = Defaults();
            float last = 0f;
            for (float c = 1f; c >= -0.001f; c -= 0.05f)
            {
                float loud = PlayerComposure.LoudnessFor(cfg, c);
                Assert.That(loud, Is.GreaterThanOrEqualTo(last - 0.0001f),
                            $"loudness dipped at composure {c:0.00}");
                last = loud;
            }
        }

        // ---- shipped config: the ruling that could actually break the game ---

        [Test]
        public void AtTheFloorWalkingIsStillQuieterThanRunningIsToday()
        {
            // THE test of this feature. The floor exists so a bad run stays
            // playable; it only does that if slowing down still works. Once your
            // WORST walk carries as far as a normal run, there is no quiet left
            // to buy and the run is lost several minutes before it ends - exactly
            // the death spiral the user's floor ruling was meant to prevent.
            var cfg = ShippedOrIgnore();
            var steps = AssetDatabase.LoadAssetAtPath<PlayerFootstepConfig>(FootstepPath);
            var maniac = AssetDatabase.LoadAssetAtPath<ManiacConfig>(ManiacPath);
            if (steps == null || maniac == null) Assert.Ignore("footstep or maniac config missing");

            float worst = PlayerComposure.LoudnessFor(cfg, Mathf.Clamp01(cfg.floor));
            float walkAtWorst = maniac.hearingRadius * steps.walkLoudness * worst;
            float runToday = maniac.hearingRadius * steps.runLoudness;

            Assert.That(walkAtWorst, Is.LessThan(runToday),
                $"At the floor your walk carries {walkAtWorst:0.00}u and a run carries {runToday:0.00}u. " +
                "Slowing down has stopped being an answer, so a spent run cannot be recovered. " +
                "Lower loudnessAtEmpty or raise floor.");
        }

        [Test]
        public void TheFloorIsAboveZeroAndBelowFull()
        {
            var cfg = ShippedOrIgnore();
            Assert.That(cfg.floor, Is.GreaterThan(0f), "a floor of 0 is not a floor");
            Assert.That(cfg.floor, Is.LessThan(1f), "a floor of 1 means composure can never drain at all");
        }

        [Test]
        public void DarknessIsStillARarePlaceAndNotTheWholeCastle()
        {
            // The user ruled that DIM light must be free. That is a threshold
            // judgement and this cannot verify it against the real level, but it
            // can stop the number drifting somewhere that plainly contradicts the
            // ruling - a threshold near 1 would make a torch-lit corridor "dark".
            var cfg = ShippedOrIgnore();
            Assert.That(cfg.darkAtOrBelow, Is.LessThan(0.5f),
                "darkAtOrBelow this high counts ordinary lit rooms as total darkness, which " +
                "inverts the ruling that only genuine black should cost.");
        }
    }
}
