// Sanity, called composure because it is not a meter and never appears as one.
//
// THE DESIGN, and why it is not a copy of a famous one. This game ALREADY has
// the thing most horror sanity systems drive: FearConductor is one 0..1 number
// fed by proximity, awareness and closing, and it already runs the heartbeat,
// the breathing, the drone, the sting and the screen. Bolting a second bar
// beside it would have duplicated all of that. So the question was never "how do
// we build sanity", it was "what DECISION does sanity add that the player cannot
// already make" — and the answer is the Amnesia one: the dark should cost
// something, because right now an unlit corner is a pure win.
//
// THREE USER RULINGS (2026-08-27) shape it:
//   - Only TOTAL darkness drains. Dim light is free. Torches become landmarks.
//   - There is a FLOOR: bad but survivable, no death spirals.
//   - It resets each run.
//
// ONE MECHANICAL OUTPUT: low composure makes you LOUDER, through Core's
// PlayerNoiseDial. That is aimed at his ears deliberately, because hearing is
// the sense the player controls — walk instead of run and you are quiet again,
// so it demands more care rather than punishing someone already playing well.
// Sight would just penalise a careful player anyway.
//
// WHAT IT DELIBERATELY DOES NOT TOUCH: the heartbeat and the breathing. Both are
// FINISHED features, and CLAUDE.md §3 exists because the fear system was once
// retuned unprompted from a single bot session. Adding a sanity floor to the
// heart would be exactly that mistake with a new name. Composure publishes
// ComposureChangedEvent instead, so the audio and visual lanes can present it on
// their own terms rather than having this feature reach into theirs.
//
// ⚠️ ONE CROSS-FEATURE DEPENDENCY, DELIBERATE AND ONE-WAY: this consumes
// Hiding's PlayerHidEvent, so deleting the Hiding folder would stop Sanity
// compiling. There is no Core-neutral "the player is concealed" event to use
// instead, and inventing one for a single consumer would be worse. Hiding does
// not know Sanity exists, so the arrow only points one way — the same shape as
// Director depending on Maniac. Progress deliberately does NOT work this way:
// it goes through Core's WorldProgressEvent, so Objectives stays deletable.
//
// SELF-INSTALLING, and it does not live on the player: it only needs the
// player's POSITION, so it hosts itself and looks the player up. That means no
// scene edit, no PlayerController edit, and it is present in every scene
// including any added later — the failure that left ClockEffects inert for three
// weeks and was the day's repeated lesson.
using TimeKiller.Core;
using TimeKiller.Player;
using UnityEngine;

namespace TimeKiller.Sanity
{
    public class PlayerComposure : MonoBehaviour
    {
        ComposureConfig config;
        Transform player;
        float nextSampleAt;
        bool hiding;

        static PlayerComposure instance;

        /// 1 = composed, 0 = spent. Never actually reaches 0 — the floor holds it
        /// above that — but the curve is expressed 0..1 so the config numbers read
        /// as fractions of the whole.
        public float Composure { get; private set; } = 1f;

        /// Last sampled light level and whether that counts as total darkness.
        /// Exposed for the F1 line and the Verify probe: "am I in the dark right
        /// now" must be answerable as a number, because the whole feature hinges
        /// on a threshold nobody can eyeball.
        public float LightLevel { get; private set; }
        public bool InDarkness { get; private set; }
        public bool Active => config != null && config.enabled;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => instance = null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            if (instance != null) return;
            var config = Resources.Load<ComposureConfig>(ComposureConfig.ResourcesPath);
            if (config == null) return;   // no asset = feature uninstalled, not broken

            var host = new GameObject("[PlayerComposure]");
            DontDestroyOnLoad(host);
            instance = host.AddComponent<PlayerComposure>();
            instance.config = config;
        }

        void OnEnable()
        {
            // Resets each run (ruling 3). A scene reload rebuilds the world but
            // this object survives it, so the reset is explicit rather than
            // implied by destruction.
            Composure = 1f;
            PlayerNoiseDial.Set(1f);

            EventBus.Subscribe<TimeKiller.Hiding.PlayerHidEvent>(OnHid);
            EventBus.Subscribe<TimeKiller.Hiding.PlayerUnhidEvent>(OnUnhid);
            EventBus.Subscribe<WorldProgressEvent>(OnProgress);

            // F8 spends composure instantly. Without it, checking this feature
            // means standing in a dark corner for a minute watching a number
            // crawl - which is exactly why the user reported it as "not working".
            // A feature that takes 60 seconds to observe is a feature nobody
            // observes. Dev-only; CheatHotkeys compiles to nothing in a release.
            CheatHotkeys.RegisterCheat(UnityEngine.InputSystem.Key.F8,
                "Spend composure", () => {
                    Composure = config != null ? Mathf.Clamp01(config.floor) : 0f;
                    PlayerNoiseDial.Set(LoudnessFor(config, Composure));
                    Publish();
                });

            DebugOverlay.Watch("Composure", () => !Active ? "off"
                : $"{Composure:0.00} {(InDarkness ? "DARK" : "lit ")} " +
                  $"light {LightLevel:0.00}/{config.darkAtOrBelow:0.00}  " +
                  $"loud x{PlayerNoiseDial.Multiplier:0.00}");
        }

        void OnDisable()
        {
            EventBus.Unsubscribe<TimeKiller.Hiding.PlayerHidEvent>(OnHid);
            EventBus.Unsubscribe<TimeKiller.Hiding.PlayerUnhidEvent>(OnUnhid);
            EventBus.Unsubscribe<WorldProgressEvent>(OnProgress);
            DebugOverlay.Unwatch("Composure");
            // Leave the dial where the game expects it if this is switched off.
            PlayerNoiseDial.Set(1f);
        }

        void OnHid(TimeKiller.Hiding.PlayerHidEvent e) => hiding = true;
        void OnUnhid(TimeKiller.Hiding.PlayerUnhidEvent e) => hiding = false;

        /// Progress heals. This is what replaces an item economy.
        ///
        /// Listens to Core's WorldProgressEvent rather than Objectives'
        /// ClockFixedEvent, so Sanity compiles with the Objectives folder deleted
        /// and any future objective restores composure for free. Same seam the
        /// maniac's escalation uses, and the same reason.
        void OnProgress(WorldProgressEvent e)
        {
            if (!Active) return;
            Composure = Mathf.Clamp01(Composure + config.clockRestore);
            Publish();
        }

        void Update()
        {
            if (!Active) return;

            if (Time.time >= nextSampleAt)
            {
                nextSampleAt = Time.time + config.sampleInterval;
                // The player's own glow must not count as the room being lit.
                // PlayerPosition() resolves `player` first, so it is safe to pass.
                var samplePos = PlayerPosition();
                LightLevel = LightSampler2D.LevelAt(samplePos, player);
                InDarkness = LightLevel <= config.darkAtOrBelow;
            }

            float before = Composure;
            Composure = Step(config, Composure, InDarkness, hiding, Time.deltaTime);
            PlayerNoiseDial.Set(LoudnessFor(config, Composure));

            // Only announce real movement. A float that changes in the seventh
            // decimal every frame is not news, and a listener that redraws on it
            // would be redrawing constantly.
            if (Mathf.Abs(Composure - before) > 0.001f) Publish();
        }

        void Publish() => EventBus.Publish(new ComposureChangedEvent
        {
            Composure = Composure,
            InDarkness = InDarkness,
            LoudnessMultiplier = PlayerNoiseDial.Multiplier,
        });

        Vector2 PlayerPosition()
        {
            if (player == null)
            {
                var pc = FindAnyObjectByType<PlayerController>();
                if (pc != null) player = pc.transform;
            }
            return player != null ? (Vector2)player.position : Vector2.zero;
        }

        // ---- Pure. The feature is these two functions; everything above is
        // plumbing, and they are static so the EditMode suite can pin them
        // without a scene, a light or a frame.

        /// One step of drain or recovery. Hiding and darkness do not stack: you
        /// are in a box, which IS the dark, so the wardrobe's gentler rate simply
        /// replaces the darkness rate rather than adding to it.
        public static float Step(ComposureConfig cfg, float current, bool inDarkness,
                                 bool hiding, float deltaTime)
        {
            if (cfg == null || !cfg.enabled) return current;

            float floor = Mathf.Clamp01(cfg.floor);
            float next;

            if (hiding)
                next = current - deltaTime / Mathf.Max(0.01f, cfg.secondsToSpendHiding);
            else if (inDarkness)
                next = current - deltaTime / Mathf.Max(0.01f, cfg.secondsToSpendInDark);
            else
                next = current + deltaTime / Mathf.Max(0.01f, cfg.secondsToRecoverInLight);

            return Mathf.Clamp(next, floor, 1f);
        }

        /// How much louder the player is at this composure. 1 at full composure,
        /// rising toward loudnessAtEmpty as it falls — but the floor means the
        /// full value is never actually reached, which is the point of the floor.
        public static float LoudnessFor(ComposureConfig cfg, float composure)
        {
            if (cfg == null || !cfg.enabled) return 1f;
            return Mathf.Lerp(Mathf.Max(1f, cfg.loudnessAtEmpty), 1f, Mathf.Clamp01(composure));
        }
    }
}
