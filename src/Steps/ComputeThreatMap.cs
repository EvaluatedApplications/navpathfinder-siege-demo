using EvalApp.Consumer;

using NavPathfinder.Demo.Data;
using NavPathfinder.Demo.Simulation;
using NavPathfinder.Sdk.Integration;
using NavPathfinder.Sdk.Models;

namespace NavPathfinder.Demo.Steps;

/// <summary>
/// Computes battlefield threat from invader positions via <see cref="InfluenceMapService"/>.
///
/// Frame-budget aware: the throttle interval stretches automatically when the previous
/// frame was over the 16.67 ms target, so this step yields compute time to TickPopulation.
///
///   Under budget  (prevPipeMs ≤ 16.67)  →  run every 3 ticks
///   2× over budget (prevPipeMs = 33ms)  →  run every 6 ticks
///   Hard ceiling                         →  run at least every 15 ticks
///
/// Stale data is safe: ThreatLevel drives only a civilian speed boost and a HUD bar;
/// one or two stale reads are imperceptible.
/// </summary>
public sealed class ComputeThreatMap : ContextSideEffectStep<NavPathfinderContext, SiegeDomainContext, SiegeTickData>
{
    private const float  NormCeiling  = 5f;
    private const int    BaseInterval = 3;
    private const int    MaxInterval  = 15;
    private const double TargetMs     = 1000.0 / 15.0;   // 66.67 ms

    protected override async ValueTask<SiegeTickData> ExecuteAsync(
        SiegeTickData data, NavPathfinderContext global, SiegeDomainContext domain, CancellationToken ct)
    {
        // Adaptive throttle: stretch interval proportionally to how far over-budget we are.
        int interval = data.WallClockMs <= TargetMs
            ? BaseInterval
            : Math.Min(MaxInterval, (int)(data.WallClockMs / TargetMs) * BaseInterval);

        if (data.TickNumber % interval != 0 && data.ThreatMap != null)
            return data;

        var invaders = data.Invaders;

        // Build source list in a tight loop — no LINQ allocation.
        var sources = new InfluenceSourceDto[invaders.Length];
        for (int i = 0; i < invaders.Length; i++)
            sources[i] = new InfluenceSourceDto(invaders[i].Position, Strength: 1f, FalloffRadius: 10f);

        var map = await domain.InfluenceService.ComputeAsync(
            data.NavMesh, sources, ct);

        float peak        = map.Count > 0 ? map.Max() : 0f;
        float threatLevel = Math.Clamp(peak / NormCeiling, 0f, 1f);

        return data with { ThreatMap = map, ThreatLevel = threatLevel };
    }
}
