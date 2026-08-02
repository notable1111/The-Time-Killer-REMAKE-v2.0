// Events for the escape objective loop (fix clocks -> open exit -> escape).
// Published on the Core EventBus; HUD, audio, exit door subscribe.
namespace TimeKiller.Objectives
{
    /// Fired when a clock is fully repaired. Carries the running tally.
    public struct ClockFixedEvent
    {
        public int FixedCount;
        public int Total;
    }

    /// Fired once, when the last clock is fixed — the exit unlocks.
    public struct AllClocksFixedEvent { }

    /// Fired when the player reaches the open exit — they escaped. You win.
    public struct GameWonEvent { }

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
