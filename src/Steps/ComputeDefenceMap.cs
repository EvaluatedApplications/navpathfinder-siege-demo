using EvalApp.Consumer;

using NavPathfinder.Demo.Data;
using NavPathfinder.Demo.Simulation;
using NavPathfinder.Sdk.Integration;
using NavPathfinder.Sdk.Models;

namespace NavPathfinder.Demo.Steps;

/// <summary>
/// Computes defender concentration map via <see cref="IInfluenceMapService"/>.
/// Mirror of <see cref="ComputeThreatMap"/> but for defenders — shows where
/// the defensive line is concentrated.
///
/// Same adaptive throttle as ComputeThreatMap — stale data is safe.
/// Used for defence line evaluation and renderer heatmaps.
/// </summary>
public sealed class ComputeDefenceMap : ContextSideEffectStep<NavPathfinderContext, SiegeDomainContext, SiegeTickData>
{
    private const float  NormCeiling  = 5f;
    private const int    BaseInterval = 3;
    private const int    MaxInterval  = 15;
    private const double TargetMs     = 1000.0 / 15.0;

    protected override async ValueTask<SiegeTickData> ExecuteAsync(
        SiegeTickData data, NavPathfinderContext global, SiegeDomainContext domain, CancellationToken ct)
    {
        int interval = data.WallClockMs <= TargetMs
            ? BaseInterval
            : Math.Min(MaxInterval, (int)(data.WallClockMs / TargetMs) * BaseInterval);

        if (data.TickNumber % interval != 0 && data.DefenceMap != null)
            return data;

        var defenders = data.Defenders;
        if (defenders.IsDefaultOrEmpty) return data;

        var sources = new InfluenceSourceDto[defenders.Length];
        for (int i = 0; i < defenders.Length; i++)
            sources[i] = new InfluenceSourceDto(defenders[i].Position, Strength: 1f, FalloffRadius: 8f);

        var map = await domain.InfluenceService.ComputeAsync(
            data.NavMesh, sources, ct);

        return data with { DefenceMap = map };
    }
}
