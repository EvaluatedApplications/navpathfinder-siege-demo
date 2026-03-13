namespace NavPathfinder.Demo.Data;

public enum WinState
{
    InProgress,
    OuterBreached,       // All outer layer gates breached
    InnerContested,      // Inner gates under attack
    InvadersWinning,     // Keep under direct assault
    DefendersWinning,    // Invader pool nearly exhausted
    InvadersWon,         // Keep fell — held for required ticks
    DefendersWon,        // Invader pool exhausted
}
