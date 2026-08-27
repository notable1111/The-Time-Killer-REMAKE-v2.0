// Events for the escape objective loop (fix clocks -> open exit -> escape).
// Published on the Core EventBus; HUD, audio, exit door subscribe.
namespace TimeKiller.Objectives
{
    /// Fired when a clock is fully repaired. Carries the running tally.
    public struct ClockFixedEvent
    {
        public int FixedCount;
        public int Total;
        /// Where the clock IS — the face, not the floor under it (see
        /// ClockObjective.EffectPoint). Added 2026-08-04 because the tally alone
        /// cannot place an effect, and until then the biggest progress moment in
        /// the game had nowhere on screen to happen.
        public UnityEngine.Vector2 Position;
    }

    /// Fired once, when the last clock is fixed — the exit unlocks.
    public struct AllClocksFixedEvent { }

    /// Fired when the player reaches the open exit — they escaped. You win.
    public struct GameWonEvent { }

    /// A skill check the player EARNED, announced so the press can be seen and
    /// heard.
    ///
    /// The mirror of ClockMissEvent, and it existed nowhere until 2026-08-04: a
    /// good press called AddProgress and nothing else, so the one beat the
    /// mini-game rewards you for was the only beat with no feedback at all. A
    /// fumble had a ring and a noise; success had a bar that moved.
    ///
    /// NOT published on the press that FINISHES a clock — that press belongs to
    /// ClockFixedEvent, and firing both would stack a small burst under a big
    /// one on the same frame.
    public struct ClockHitEvent
    {
        /// The clock's face (ClockObjective.EffectPoint), not the player.
        public UnityEngine.Vector2 Position;
        /// 0..1 progress AFTER this press, so a visual can build toward done.
        public float Progress;
        /// 0..1 fear at the moment of the press.
        public float Fear;
    }

    /// A fumbled skill check, announced so the mistake can be SEEN as well as
    /// heard.
    ///
    /// The noise a miss makes already reached the maniac through the hearing
    /// system, but silently: the player felt the consequence minutes later, when
    /// he arrived, and never connected it to the press they got wrong. A rule the
    /// player cannot learn is not a mechanic, it is just bad luck. Carrying the
    /// real loudness lets a visual size itself to the actual noise rather than
    /// inventing one.
    public struct ClockMissEvent
    {
        public UnityEngine.Vector2 Position;
        /// The loudness actually published to the world — already fear-scaled.
        public float Loudness;
        /// 0..1 fear at the moment of the miss.
        public float Fear;
    }
}
