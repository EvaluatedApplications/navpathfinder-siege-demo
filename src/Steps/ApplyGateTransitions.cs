using EvalApp.Consumer;

using NavPathfinder.Demo.Data;
using NavPathfinder.Demo.Simulation;
using NavPathfinder.Sdk.Integration;
using NavPathfinder.Sdk.Models;
using NavPathfinder.Sdk.Services;

namespace NavPathfinder.Demo.Steps;

public sealed class ApplyGateTransitions : ContextSideEffectStep<NavPathfinderContext, SiegeDomainContext, SiegeTickData>
{
    protected override async ValueTask<SiegeTickData> ExecuteAsync(
        SiegeTickData data, NavPathfinderContext global, SiegeDomainContext domain, CancellationToken ct)
    {
        var gates = data.Gates;
        var currentMesh = data.NavMesh;

        var blockedZones = gates
            .Where(g => g.Status is GateStatus.Locked or GateStatus.UnderAttack)
            .Select(g => new NavMeshBlockedZone(g.Center, 1.5f))
            .ToList();

        var updatedMesh = await domain.ObstacleService.UpdateAsync(currentMesh, blockedZones, ct);
        return data with { NavMesh = updatedMesh };
    }
}
