namespace NavPathfinder.Demo.Data;

/// <summary>
/// Unified behaviour states for all agent roles during a siege.
/// Every state represents purposeful action — no idle wandering.
/// Only state transitions trigger SDK pathfinding calls.
/// </summary>
public enum BehaviourState
{
    // ── Invader states ──────────────────────────────────────────
    Mustering,      // Gathering at rally point before assault wave
    Advancing,      // Moving toward target gate in formation
    Sieging,         // Clustered at gate, applying breach pressure (stationary)
    Breaching,      // Pouring through broken gate into courtyard
    Claiming,       // Occupying zone to secure forward spawn (stationary)
    Pushing,        // Advancing to next layer's gates

    // ── Defender states ─────────────────────────────────────────
    Sortie,         // Sallying outside gates to intercept invaders
    Holding,        // Stationed at assigned gate, blocking (stationary)
    Reinforcing,    // Moving to gate under heavy attack
    Fighting,       // Active melee at contested point (stationary)
    FallingBack,    // Orderly retreat to next defensive line
    LastStand,      // Defending keep, no further retreat (stationary)

    // ── Civilian states ─────────────────────────────────────────
    Sheltering,     // Moving toward safest inner area
    Fleeing,        // Running from nearby combat (proximity trigger)
    Hidden          // Reached safety, stays put (zero pathfinding cost)
}
