using System.Collections.Immutable;
using System.Numerics;
using EvalApp.Consumer;

using NavPathfinder.Demo.Data;
using NavPathfinder.Demo.Simulation;
using NavPathfinder.Sdk.Integration;
using NavPathfinder.Sdk.Models;

namespace NavPathfinder.Demo.Steps;

/// <summary>
/// Context-aware step: delegates position-correction to <see cref="ISeparationService"/>.
///
/// Separation applies within each faction only — cross-faction proximity
/// is handled by <see cref="ResolveCombat"/>.
/// </summary>
public sealed class EnforceSeparation : ContextPureStep<NavPathfinderContext, SiegeDomainContext, SiegeTickData>
{
    private static readonly SeparationOptions Options = new(MinDistance: 1.0f, Iterations: 2, CellSize: 1.0f);

    protected override async ValueTask<SiegeTickData> TransformAsync(
        SiegeTickData data,
        NavPathfinderContext global,
        SiegeDomainContext domain,
        CancellationToken ct)
    {
        var svc = domain.SeparationService;

        var invaders  = data.Invaders;
        var defenders = data.Defenders;
        var civilians = data.Civilians;

        var invDtos = ToAgentDtos(invaders);
        var defDtos = ToAgentDtos(defenders);
        var civDtos = ToAgentDtos(civilians);

        var invPositions = await svc.SeparateAsync(invDtos, Options, ct);
        var defPositions = await svc.SeparateAsync(defDtos, Options, ct);
        var civPositions = await svc.SeparateAsync(civDtos, Options, ct);

        return data with
        {
            Invaders  = ApplyCorrectedPositions(invaders,  invPositions),
            Defenders = ApplyCorrectedPositions(defenders, defPositions),
            Civilians = ApplyCorrectedPositions(civilians, civPositions),
        };
    }

    private static IReadOnlyList<AgentDto> ToAgentDtos(ImmutableArray<SimAgent> agents)
    {
        var result = new AgentDto[agents.Length];
        for (int i = 0; i < agents.Length; i++)
            result[i] = new AgentDto(agents[i].Id, agents[i].Position, agents[i].Goal);
        return result;
    }

    private static ImmutableArray<SimAgent> ApplyCorrectedPositions(
        ImmutableArray<SimAgent> agents, ImmutableArray<Vector2> positions)
    {
        if (agents.Length == 0) return agents;
        var builder = ImmutableArray.CreateBuilder<SimAgent>(agents.Length);
        for (int i = 0; i < agents.Length; i++)
        {
            var clamped = new Vector2(
                Math.Clamp(positions[i].X, 0.5f, SiegeWorld.GridCols - 0.5f),
                Math.Clamp(positions[i].Y, 0.5f, SiegeWorld.GridRows - 0.5f));
            // If separation pushed the agent into a wall, fall back to pre-separation position.
            if (SiegeWorld.IsWall((int)clamped.X, (int)clamped.Y))
                clamped = agents[i].Position;
            // If the fallback is also in a wall (should not happen after ApplyMovement fix,
            // but kept as a safety net), don't update the position at all this tick.
            if (SiegeWorld.IsWall((int)clamped.X, (int)clamped.Y))
            {
                builder.Add(agents[i]);
                continue;
            }
            builder.Add(agents[i] with { Position = clamped });
        }
        return builder.ToImmutable();
    }
}
