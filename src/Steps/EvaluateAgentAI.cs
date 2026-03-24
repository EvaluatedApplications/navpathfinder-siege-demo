using System.Collections.Immutable;
using System.Numerics;
using EvalApp.Consumer;

using NavPathfinder.Demo.AI;
using NavPathfinder.Demo.Data;
using NavPathfinder.Demo.Simulation;

namespace NavPathfinder.Demo.Steps;

/// <summary>
/// Replaces UpdateBehaviours + AssignSubGoals.
/// Evaluates all agents through data-driven FSMs, producing updated
/// BehaviourState, Goal, GoalChanged, and StationaryTicks.
/// </summary>
public sealed class EvaluateAgentAI : PureStep<SiegeTickData>
{
    private static readonly AgentFsm InvaderEngine  = InvaderFsm.Create();
    private static readonly AgentFsm DefenderEngine = DefenderFsm.Create();
    private static readonly AgentFsm CivilianEngine = CivilianFsm.Create();

    public override SiegeTickData Execute(SiegeTickData data)
    {
        var cfg = data.Config ?? SiegeBalanceConfig.Default;

        var fsmCtx = new FsmContext(
            data.Gates,
            data.Zones,
            data.Invaders,
            data.Defenders,
            data.Castle,
            data.ActiveDefenceLine,
            cfg);

        int pathRecalcs = 0;

        var invaders  = EvalPopulation(data.Invaders,  InvaderEngine,  fsmCtx, ref pathRecalcs);
        var defenders = EvalDefenders(data.Defenders, DefenderEngine, fsmCtx, ref pathRecalcs);
        var civilians = EvalPopulation(data.Civilians, CivilianEngine, fsmCtx, ref pathRecalcs);

        return data with
        {
            Invaders = invaders,
            Defenders = defenders,
            Civilians = civilians,
            PathRecalcsThisTick = pathRecalcs
        };
    }

    private static ImmutableArray<SimAgent> EvalPopulation(
        ImmutableArray<SimAgent> agents,
        AgentFsm fsm,
        FsmContext ctx,
        ref int pathRecalcs)
    {
        if (agents.IsDefaultOrEmpty) return agents;

        var builder = ImmutableArray.CreateBuilder<SimAgent>(agents.Length);
        foreach (var agent in agents)
        {
            var (state, goal, changed) = fsm.Evaluate(agent, ctx);

            // Stuck-agent detection: if no FSM transition fired but agent has
            // exhausted waypoints and is far from goal, request re-path.
            // Without this, agents whose paths fail or end early are stuck forever.
            if (!changed
                && (agent.CurrentWaypoints.IsDefaultOrEmpty || agent.CurrentWaypoints.Length == 0)
                && Vector2.Distance(agent.Position, goal) > 1.5f
                && !AgentFsm.IsStationaryState(agent.Behaviour))
            {
                changed = true;
            }

            int stationaryTicks = changed
                ? 0
                : (AgentFsm.IsStationaryState(agent.Behaviour)
                    ? agent.StationaryTicks + 1
                    : agent.StationaryTicks);

            if (changed) pathRecalcs++;

            builder.Add(agent with
            {
                Behaviour = state,
                Goal = goal,
                GoalChanged = changed,
                StationaryTicks = stationaryTicks
            });
        }
        return builder.MoveToImmutable();
    }

    /// <summary>
    /// Defenders have special FallingBack override: if the active defence line
    /// has moved inward and a defender is still on an outer layer, force FallingBack.
    /// </summary>
    private static ImmutableArray<SimAgent> EvalDefenders(
        ImmutableArray<SimAgent> defenders,
        AgentFsm fsm,
        FsmContext ctx,
        ref int pathRecalcs)
    {
        if (defenders.IsDefaultOrEmpty) return defenders;

        var builder = ImmutableArray.CreateBuilder<SimAgent>(defenders.Length);
        foreach (var def in defenders)
        {
            // Check if defender should be forced to fall back
            var forced = CheckFallBack(def, ctx);
            if (forced.HasValue)
            {
                pathRecalcs++;
                builder.Add(def with
                {
                    Behaviour = BehaviourState.FallingBack,
                    Goal = forced.Value,
                    GoalChanged = true,
                    StationaryTicks = 0
                });
                continue;
            }

            var (state, goal, changed) = fsm.Evaluate(def, ctx);

            // Stuck-agent detection for defenders (same as EvalPopulation)
            if (!changed
                && (def.CurrentWaypoints.IsDefaultOrEmpty || def.CurrentWaypoints.Length == 0)
                && Vector2.Distance(def.Position, goal) > 1.5f
                && !AgentFsm.IsStationaryState(def.Behaviour))
            {
                changed = true;
            }

            // If transitioning to Holding when defence line is Keep, override to LastStand
            if (changed && state == BehaviourState.Holding &&
                ctx.ActiveDefenceLine == CastleLayer.Keep)
            {
                state = BehaviourState.LastStand;
                goal = ctx.KeepCenter;
            }

            int stationaryTicks = changed
                ? 0
                : (AgentFsm.IsStationaryState(def.Behaviour)
                    ? def.StationaryTicks + 1
                    : def.StationaryTicks);

            if (changed) pathRecalcs++;

            builder.Add(def with
            {
                Behaviour = state,
                Goal = goal,
                GoalChanged = changed,
                StationaryTicks = stationaryTicks
            });
        }
        return builder.MoveToImmutable();
    }

    /// <summary>
    /// If active defence line moved inward and this defender is on an outer layer,
    /// force FallingBack toward the new defence line.
    /// </summary>
    private static Vector2? CheckFallBack(SimAgent def, FsmContext ctx)
    {
        if (def.Behaviour == BehaviourState.FallingBack) return null;
        if (ctx.ActiveDefenceLine == CastleLayer.OuterCurtain) return null;

        // Determine which layer the defender is on
        var layer = ctx.FindAgentLayer(def.Position);
        if (layer is null) return null;

        // If behind the defence line (on an outer layer), fall back
        if (ctx.LayerBehindDefenceLine(layer.Value))
        {
            return ctx.ActiveDefenceLine == CastleLayer.Keep
                ? ctx.KeepCenter
                : ctx.NearestGateOnLayer(def.Position, ctx.ActiveDefenceLine);
        }
        return null;
    }
}
