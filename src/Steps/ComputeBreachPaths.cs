using System.Collections.Immutable;
using System.Numerics;
using EvalApp.Consumer;

using NavPathfinder.Demo.Data;
using NavPathfinder.Demo.Simulation;
using NavPathfinder.Sdk.Integration;

namespace NavPathfinder.Demo.Steps;

/// <summary>
/// Computes A* breach corridors from each breached gate to the fortress centre
/// via <see cref="NavPathfinder.Sdk.Abstractions.ISingleAgentService.QueryPathBatchAsync"/>.
///
/// Frame-budget aware: when the previous frame was over the 16.67 ms target, this step
/// defers even a new-breach recompute by one tick, letting TickPopulation keep its
/// allocation.  A one-tick stale breach path is invisible to the player.
///
/// Conditions to skip (in priority order):
///   1. No breached gates → clear paths, return.
///   2. Gate count unchanged AND under frame budget → reuse stale paths.
///   3. Over frame budget → defer recompute for this tick (reuse stale).
/// </summary>
public sealed class ComputeBreachPaths : ContextSideEffectStep<NavPathfinderContext, SiegeDomainContext, SiegeTickData>
{
    private const double TargetMs = 1000.0 / 15.0;

    protected override async ValueTask<SiegeTickData> ExecuteAsync(
        SiegeTickData data, NavPathfinderContext global, SiegeDomainContext domain, CancellationToken ct)
    {
        var gates = data.Gates;

        // Count breached gates in one pass — no LINQ allocation.
        int breachCount = 0;
        foreach (var g in gates)
            if (g.Status == GateStatus.Breached) breachCount++;

        if (breachCount == 0)
            return data with { BreachPaths = ImmutableArray<IReadOnlyList<Vector2>>.Empty };

        // Reuse stale data if nothing changed, or if we're over frame budget.
        bool countUnchanged = data.BreachPaths.HasValue && data.BreachPaths.Value.Length == breachCount;
        bool overBudget     = data.WallClockMs > TargetMs;

        if (countUnchanged || overBudget)
            return data;

        // A* queries: one per breached gate → fortress centre.
        var fortressCenter = new Vector2(SiegeWorld.GridCols / 2f, SiegeWorld.GridRows / 2f);

        var queries = new List<(Vector2 start, Vector2 goal)>(breachCount);
        foreach (var g in gates)
            if (g.Status == GateStatus.Breached)
                queries.Add((g.Center, fortressCenter));

        var paths = await domain.ScoutService.QueryPathBatchAsync(
            data.NavMesh, queries, ct);

        return data with { BreachPaths = paths.ToImmutableArray() };
    }
}
