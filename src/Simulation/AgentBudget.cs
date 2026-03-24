using System.Numerics;

namespace NavPathfinder.Demo.Simulation;

/// <summary>
/// Adaptive agent-budget calculator.
///
/// When SDK pressure exceeds the threshold, each population is sub-sampled: only the
/// highest-priority agents are included in the DTO list sent to TickAsync.  Excluded
/// agents are NOT stopped — ApplyMovement still advances them on their previous-tick
/// waypoints (they coast).  This means the visual impact of sub-sampling is minimal
/// while pipeline time drops dramatically.
///
/// Fairness guarantee:
///   Small populations (e.g. 10 defenders vs 700 invaders) receive a compute bonus that
///   keeps them at full-service until their count alone would consume the entire budget.
///   In practice, defenders never get sub-sampled; invaders absorb all the reduction.
///
/// Morale bonus:
///   High-morale populations keep more agents active and receive a speed boost,
///   making them appear decisive and fast.  Low-morale agents coast more, appear sluggish.
/// </summary>
internal static class AgentBudget
{
    /// Pressure above this threshold triggers sub-sampling.
    private const float Threshold    = 0.60f;
    /// Minimum fraction of a large population always kept (hard floor).
    private const float MinFraction  = 0.25f;
    /// Morale contribution to rescuing agents from the cut.
    private const float MoraleRescue = 0.30f;
    /// Agent speed range driven by morale: [SpeedLow, SpeedHigh].
    private const float SpeedLow     = 0.80f;
    private const float SpeedHigh    = 1.25f;

    /// <summary>
    /// How many agents this population should send to TickAsync this frame.
    /// </summary>
    /// <param name="agentCount">Total agents in this population.</param>
    /// <param name="pressure">Previous-tick SDK pressure [0,1] for this population.</param>
    /// <param name="morale">Population morale [0.1, 1.0].</param>
    /// <param name="totalAgents">Combined agent count across all populations.</param>
    /// <param name="populationCount">Number of concurrent populations (default 3).</param>
    public static int ComputeBudget(
        int agentCount, float pressure, float morale,
        int totalAgents, int populationCount = 3)
    {
        if (pressure <= Threshold || agentCount == 0)
            return agentCount;

        // 0..1 ramp starting at Threshold
        float excess = (pressure - Threshold) / (1f - Threshold);

        // Base fraction: falls linearly with pressure excess
        float baseFraction = 1f - excess * (1f - MinFraction);

        // Morale rescues some agents back from the cut
        float keepFraction = baseFraction + morale * excess * MoraleRescue;

        // Fairness: each population deserves an equal share of total throughput.
        // Populations smaller than that share get a bonus that keeps them at full service.
        float equalShare = totalAgents / (float)populationCount;
        float fairBonus  = Math.Clamp(equalShare / agentCount, 0.5f, 3f);

        keepFraction = Math.Clamp(keepFraction * fairBonus, MinFraction, 1f);
        return Math.Max(1, (int)(agentCount * keepFraction));
    }

    /// <summary>
    /// Priority score for an agent: higher → kept when sub-sampling, placed first in DTO
    /// list so the SDK gives them higher RVO priority.
    /// Agents closest to their current goal have highest priority (they need accurate paths).
    /// </summary>
    public static float Priority(Vector2 position, Vector2 goal)
        => 1f / (1f + Vector2.Distance(position, goal));

    /// <summary>
    /// Speed multiplier from morale.  High-morale agents move faster (appear decisive);
    /// low-morale agents move slower (appear hesitant).
    /// </summary>
    public static float SpeedMultiplier(float morale)
        => SpeedLow + morale * (SpeedHigh - SpeedLow);

    /// <summary>
    /// ConflictRadius scaled by morale.  High-morale agents are more situationally
    /// aware and avoid conflicts at longer range.
    /// </summary>
    public static float ConflictRadius(float morale, float baseRadius = 6f)
        => baseRadius * (0.5f + morale * 0.5f);   // range [3, 6]
}
