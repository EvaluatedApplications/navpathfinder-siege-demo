namespace NavPathfinder.Demo.Data;

/// <summary>
/// All tunable balance constants for a siege simulation run.
/// Embed in <see cref="SiegeTickData"/> so pure steps can read config without context injection.
///
/// Empirically tuned: headless sim plateaus at ~3900 agents @ ~39ms/tick.
/// With rendering (~25ms), total lands near 66ms → 15fps interactive target.
/// </summary>
public record SiegeBalanceConfig(
    // Spawn rates — credit gain per tick. High rates fill populations fast
    // so the demo reaches its spectacular equilibrium within seconds.
    float InvBaseRate   = 200f,
    float DefBaseRate   = 100f,
    float CivBaseRate   = 70f,

    // Maximum on-screen before spawning pauses (per-population soft caps).
    // Set above natural equilibrium (~3900 total) so combat attrition is the
    // real population governor, not an artificial ceiling.
    int   InvBaseMax    = 5000,
    int   DefBaseMax    = 3000,
    int   CivBaseMax    = 1000,

    // Gate FSM thresholds (tick counts at 15fps)
    int   BreachTicks   = 24,     // gates fall faster under sustained assault
    int   RepairTicks   = 14,     // repairs take longer (harder to recover)
    int   DefHoldTicks  = 75,
    int   InvHoldTicks  = 38,

    // Combat stats and range — agents must be adjacent to fight (no through-wall combat)
    // Invaders are slightly stronger per-agent; defenders compensate with fortifications
    // and chokepoints. Invaders should grind to a slow, inevitable win.
    float InvAttack     = 20f,    // invader base attack power (battle-hardened siege army)
    float InvDefense    = 7f,     // invader base defense (field armour)
    float DefAttack     = 22f,    // defender base attack power (garrison troops)
    float DefDefense    = 9f,     // defender base defense (wall advantage offsets lower stats)
    // Combat engagement radius — must be large enough for ranged units (Pierce/Fire)
    // Melee units naturally miss at distance due to low AttackRange
    float CombatRadius  = 2.0f,

    // Gate detection radius — matches EvaluateGateStates.GateRadius for FSM transitions
    // Separate from CombatRadius so siege/breach detection isn't affected by combat range
    float GateDetectRadius = 3.0f,

    // Starting pool sizes — large enough that pool exhaustion never throttles spawning.
    // The natural equilibrium (~3900 agents) governs population, not pool limits.
    int   InvaderPool   = 99_999,
    int   DefenderPool  = 99_999,
    int   CivilianPool  = 9_999,

    // Multi-layer castle
    int   OuterGateCount = 6,    // N×2, S×2, E, W
    int   InnerGateCount = 2,    // N, S

    // Zone claiming
    float ZoneClaimThreshold  = 0.5f,   // 50% contest to claim
    int   ZoneClaimHoldTicks  = 8,      // must hold for ~0.5s at 15fps
    float ZoneContestRadius   = 8f,     // how close agents must be

    // Defence line retreat
    float RetreatThreshold    = 0.66f,  // fall back when 66% of layer gates breached

    // Behaviour AI (tick counts at 15fps)
    int   MusterDuration      = 3,      // ~0.2s brief rally then advance
    float ReinforceThreshold  = 3f,     // attacker:defender ratio to trigger reinforcement
    float FleeProximity       = 5f,     // civilian flee trigger distance from combat

    // Target frame rate
    float TargetFps     = 15f)
{
    public double TargetFrameMs => 1000.0 / TargetFps;
    public static readonly SiegeBalanceConfig Default = new();
}
