using System.Collections.Immutable;
using EvalApp.Consumer;

using NavPathfinder.Demo.Data;
using NavPathfinder.Demo.Simulation;
using NavPathfinder.Sdk.Models;

namespace NavPathfinder.Demo.Steps;

/// <summary>
/// Replaces PrepareInvaderGoals + PrepareDefenderGoals + PrepareCivilianGoals.
/// Thin mapping: SimAgent → AgentDto for SDK pathfinding.
/// Goals are already computed by EvaluateAgentAI FSM — this step only applies
/// morale-based speed modifiers and maps to the SDK DTO type.
/// </summary>
public sealed class PrepareSimDtos : PureStep<SiegeTickData>
{
    public override SiegeTickData Execute(SiegeTickData data)
    {
        float invSpeed = AgentBudget.SpeedMultiplier(data.InvaderMorale);
        float defSpeed = AgentBudget.SpeedMultiplier(data.DefenderMorale);
        float civSpeed = AgentBudget.SpeedMultiplier(data.CivilianMorale)
                       * (1f + data.ThreatLevel * 0.5f);

        var invDtos = MapToDtos(data.Invaders, invSpeed);
        var defDtos = MapToDtos(data.Defenders, defSpeed);
        var civDtos = MapToDtos(data.Civilians, civSpeed);

        return data with
        {
            InvaderDtos  = invDtos,
            DefenderDtos = defDtos,
            CivilianDtos = civDtos
        };
    }

    private static ImmutableArray<AgentDto> MapToDtos(
        ImmutableArray<SimAgent> agents, float speedMultiplier)
    {
        if (agents.IsDefaultOrEmpty)
            return ImmutableArray<AgentDto>.Empty;

        var builder = ImmutableArray.CreateBuilder<AgentDto>(agents.Length);
        foreach (var a in agents)
        {
            builder.Add(new AgentDto(
                Id: a.Id,
                Position: a.Position,
                Goal: a.Goal,
                Radius: a.Radius,
                MaxSpeed: a.MaxSpeed * speedMultiplier,
                GoalChanged: a.GoalChanged));
        }
        return builder.MoveToImmutable();
    }
}
