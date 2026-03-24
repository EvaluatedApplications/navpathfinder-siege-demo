using System.Collections.Immutable;
using System.Numerics;
using EvalApp.Consumer;

using NavPathfinder.Demo.AI;
using NavPathfinder.Demo.Data;
using NavPathfinder.Demo.Simulation;
using NavPathfinder.Sdk.Models;

namespace NavPathfinder.Demo.Steps;

public sealed class ApplyMovement : PureStep<SiegeTickData>
{
    private const float WaypointReachDist = 0.15f;
    private const float GoalReachDist     = 1.5f;

    public override SiegeTickData Execute(SiegeTickData data)
    {
        float dt = SiegeWorld.TickDeltaSeconds;

        var invResult  = data.SimResult?.GetPopulation("Invader");
        var defResult  = data.SimResult?.GetPopulation("Defender");
        var civResult  = data.SimResult?.GetPopulation("Civilian");

        var gates = data.Gates;

        var updatedInvaders  = AdvanceAgents(data.Invaders,  invResult,  dt, AgentRole.Invader, gates);
        var updatedDefenders = AdvanceAgents(data.Defenders, defResult, dt, AgentRole.Defender, gates);
        var updatedCivilians = AdvanceAgents(data.Civilians, civResult, dt, AgentRole.Civilian, gates);

        return data with
        {
            Invaders  = updatedInvaders,
            Defenders = updatedDefenders,
            Civilians = updatedCivilians,
        };
    }

    private static ImmutableArray<SimAgent> AdvanceAgents(
        ImmutableArray<SimAgent> agents, PopulationTickResult? tickResult, float dt,
        AgentRole role, ImmutableArray<GateState> gates)
    {
        if (tickResult == null) return agents;

        var pathMap = tickResult.Paths
            .Where(p => p.PathFound && p.Waypoints.Count > 0)
            .ToDictionary(p => p.AgentId, p => p.Waypoints);

        var builder = ImmutableArray.CreateBuilder<SimAgent>(agents.Length);
        foreach (var agent in agents)
        {
            ImmutableArray<Vector2> waypoints = agent.CurrentWaypoints;

            if (pathMap.TryGetValue(agent.Id, out var newPath))
                waypoints = newPath.Skip(1).ToImmutableArray();

            Vector2 prevPos   = agent.Position;
            Vector2 pos       = agent.Position;
            float   distLeft  = agent.MaxSpeed * dt;

            while (distLeft > 0 && !waypoints.IsDefaultOrEmpty && waypoints.Length > 0)
            {
                var   target = waypoints[0];
                float dist   = Vector2.Distance(pos, target);

                if (dist <= WaypointReachDist || dist <= distLeft)
                {
                    pos      = target;
                    distLeft -= dist;
                    waypoints = waypoints.RemoveAt(0);
                }
                else
                {
                    pos      += Vector2.Normalize(target - pos) * distLeft;
                    distLeft  = 0;
                }
            }

            // Dead-reckoning fallback: when pathfinder fails to provide waypoints,
            // move directly toward goal. Wall-safety below prevents crossing walls.
            if (distLeft > 0
                && (waypoints.IsDefaultOrEmpty || waypoints.Length == 0)
                && !AgentFsm.IsStationaryState(agent.Behaviour))
            {
                float distToGoal = Vector2.Distance(pos, agent.Goal);
                if (distToGoal > WaypointReachDist)
                {
                    var dir = Vector2.Normalize(agent.Goal - pos);
                    pos += dir * MathF.Min(distLeft, distToGoal);
                }
            }

            // Wall-safety: if resolved position is inside a wall cell, revert
            if (SiegeWorld.IsWall((int)pos.X, (int)pos.Y))
                pos = prevPos;

            // Layer enforcement for invaders: can't be inside a wall ring unless
            // at least one gate on that layer is breached
            if (role == AgentRole.Invader)
                pos = EnforceInvaderLayer(pos, prevPos, gates);

            // Re-path when agent has exhausted waypoints but hasn't reached goal.
            // Note: FSM in EvaluateAgentAI handles the actual re-path signal;
            // this value is overwritten by the FSM before pathfinding runs.
            builder.Add(agent with
            {
                Position         = pos,
                CurrentWaypoints = waypoints,
                GoalChanged      = false,
            });
        }

        return builder.ToImmutable();
    }

    /// <summary>
    /// Hard enforcement: invaders cannot be deep inside a wall ring unless
    /// at least one gate on that layer is breached. Allows gate-approach positions.
    /// </summary>
    private static Vector2 EnforceInvaderLayer(Vector2 pos, Vector2 prevPos,
        ImmutableArray<GateState> gates)
    {
        int oL = SiegeWorld.WallLeft,  oR = SiegeWorld.WallRight;
        int oT = SiegeWorld.WallTop,   oB = SiegeWorld.WallBot;
        int iL = SiegeWorld.InnerLeft, iR = SiegeWorld.InnerRight;
        int iT = SiegeWorld.InnerTop,  iB = SiegeWorld.InnerBot;

        // Use a buffer zone so invaders can approach/siege gates without being reverted.
        // Gates sit on the wall line; surround positions extend ~3 cells past it.
        const int Buffer = 4;
        bool deepInsideOuter = pos.X > oL + Buffer && pos.X < oR - Buffer &&
                               pos.Y > oT + Buffer && pos.Y < oB - Buffer;
        bool deepInsideInner = pos.X > iL + Buffer && pos.X < iR - Buffer &&
                               pos.Y > iT + Buffer && pos.Y < iB - Buffer;

        if (deepInsideOuter && !HasBreachedLayer(gates, CastleLayer.OuterCurtain)
            && !NearAnyGate(pos, gates, CastleLayer.OuterCurtain))
            return prevPos;

        if (deepInsideInner && !HasBreachedLayer(gates, CastleLayer.InnerBailey)
            && !NearAnyGate(pos, gates, CastleLayer.InnerBailey))
            return prevPos;

        return pos;
    }

    private static bool NearAnyGate(Vector2 pos, ImmutableArray<GateState> gates, CastleLayer layer)
    {
        const float GateApproachRadiusSq = 5f * 5f; // allow within 5 cells of gate
        foreach (var g in gates)
            if (g.Layer == layer && Vector2.DistanceSquared(pos, g.Center) < GateApproachRadiusSq)
                return true;
        return false;
    }

    private static bool HasBreachedLayer(ImmutableArray<GateState> gates, CastleLayer layer)
    {
        foreach (var g in gates)
            if (g.Layer == layer && g.Status == GateStatus.Breached)
                return true;
        return false;
    }
}
