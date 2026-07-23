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
}
