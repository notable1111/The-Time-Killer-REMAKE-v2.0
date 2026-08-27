// Tracks the clock objectives: counts fixed vs total, fires AllClocksFixedEvent
// when the last one is done (the exit unlocks), and records the win. One in the
// scene. Reads ClockObjective's static registry so spawn order never matters.
using TimeKiller.Core;
using UnityEngine;

namespace TimeKiller.Objectives
{
    public class ObjectiveManager : MonoBehaviour
    {
        public static ObjectiveManager Instance { get; private set; }

        public int Total { get; private set; }
        public int FixedCount { get; private set; }
        public bool AllFixed => Total > 0 && FixedCount >= Total;
        public bool Won { get; private set; }

        void Awake() => Instance = this;

        void Start()
        {
            Total = ClockObjective.All.Count;
            ClockObjective.AnyFixed += OnClockFixed;
            EventBus.Subscribe<GameWonEvent>(OnWon);
            DebugOverlay.Watch("Clocks", () => $"{FixedCount}/{Total}{(AllFixed ? " EXIT OPEN" : "")}{(Won ? " WON" : "")}");
            // The clocks are what this run was ABOUT — lend the tally to the end screen.
            GameFlow.ProvideSummary(() => $"{FixedCount} / {Total} clocks fixed");
        }

        void OnDestroy()
        {
            ClockObjective.AnyFixed -= OnClockFixed;
            EventBus.Unsubscribe<GameWonEvent>(OnWon);
            DebugOverlay.Unwatch("Clocks");
            if (Instance == this) Instance = null;
        }

        void OnClockFixed(ClockObjective clock)
        {
            FixedCount++;
            // Position rides along so a listener can put something on screen
            // where the clock actually is — the tally alone says a clock was
            // fixed but not which one, and an effect needs a place to happen.
            EventBus.Publish(new ClockFixedEvent
            {
                FixedCount = FixedCount,
                Total = Total,
                Position = clock != null ? clock.EffectPoint : (Vector2)transform.position,
            });
            // The same beat, said in a way nothing has to know about clocks to
            // hear. The maniac escalates on this; Objectives never learns that he
            // exists, and he never learns that clocks do. Published alongside
            // rather than instead of ClockFixedEvent, because the two answer
            // different questions: "which clock, and where" vs "how far through
            // the run are we".
            EventBus.Publish(new WorldProgressEvent { Step = FixedCount, Total = Total });
            if (AllFixed) EventBus.Publish(new AllClocksFixedEvent());
        }

        void OnWon(GameWonEvent evt)
        {
            Won = true;
            EventBus.Publish(new RunEndedEvent { Won = true, Headline = "YOU ESCAPED" });
        }
    }
}
