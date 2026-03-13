using EvalApp.Consumer;

using NavPathfinder.Demo.Data;

namespace NavPathfinder.Demo.Steps;

/// <summary>
/// Collects performance metrics from the completed tick for HUD display.
/// Runs after all SDK steps so timing data is available.
/// </summary>
public sealed class CollectMetrics : PureStep<SiegeTickData>
{
    public override SiegeTickData Execute(SiegeTickData data)
    {
        var invaders = data.Invaders;
        var defenders = data.Defenders;
        var civilians = data.Civilians;

        int activeCount = invaders.Length + defenders.Length + civilians.Length;

        float pathMs = 0f;
        float cacheHit = 0f;
        if (data.SimResult != null)
        {
            // Sum ElapsedMs across all populations
            foreach (var pop in data.SimResult.Populations)
                pathMs += (float)pop.ElapsedMs;

            // Approximate cache hit: agents that didn't re-path this tick
            cacheHit = activeCount > 0
                ? 1f - ((float)data.PathRecalcsThisTick / activeCount)
                : 0f;
        }

        return data with
        {
            ActiveAgentCount = activeCount,
            PathfindingMs = pathMs,
            CacheHitRate = Math.Clamp(cacheHit, 0f, 1f)
        };
    }
}
