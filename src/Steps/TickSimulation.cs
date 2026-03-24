using System.Collections.Immutable;
using EvalApp.Consumer;

using NavPathfinder.Demo.Data;
using NavPathfinder.Demo.Simulation;
using NavPathfinder.Sdk.Integration;
using NavPathfinder.Sdk.Models;

namespace NavPathfinder.Demo.Steps;

public sealed class TickSimulation : ContextSideEffectStep<NavPathfinderContext, SiegeDomainContext, SiegeTickData>
{
    protected override async ValueTask<SiegeTickData> ExecuteAsync(
        SiegeTickData data, NavPathfinderContext global, SiegeDomainContext domain, CancellationToken ct)
    {
        var navMesh = data.NavMesh;

        var populations = ImmutableArray.CreateBuilder<PopulationInput>();

        if (data.InvaderDtos.HasValue && data.InvaderDtos.Value.Length > 0)
            populations.Add(new("Invader", data.InvaderDtos.Value, Math.Max(0.25f, data.InvaderMorale) * 1.0f));

        if (data.DefenderDtos.HasValue && data.DefenderDtos.Value.Length > 0)
            populations.Add(new("Defender", data.DefenderDtos.Value, Math.Max(0.25f, data.DefenderMorale) * 2.0f));

        if (data.CivilianDtos.HasValue && data.CivilianDtos.Value.Length > 0)
            populations.Add(new("Civilian", data.CivilianDtos.Value, 0.5f));

        if (populations.Count == 0)
            return data;

        var input = new SimTickInput(navMesh, data.TickNumber, populations.ToImmutable());
        var result = await domain.SimulationService.TickAsync(input, ct);

        return data with { SimResult = result };
    }
}
