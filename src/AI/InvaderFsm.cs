using System.Collections.Immutable;
using System.Numerics;
using NavPathfinder.Demo.Data;
using NavPathfinder.Demo.Simulation;

namespace NavPathfinder.Demo.AI;

/// <summary>
/// Invader FSM transition table.
/// Flow: Mustering → Advancing → Sieging → Breaching → Claiming → Pushing → (repeat deeper)
/// </summary>
public static class InvaderFsm
{
    private const float PenetrationDepth = 8.0f;
    private const float SurroundRadius   = 2.5f;
    private const int   SurroundSlots    = 24;

    public static AgentFsm Create() => new(BuildTransitions());

    private static ImmutableArray<FsmTransition> BuildTransitions() =>
        ImmutableArray.Create(
            // ── Mustering → Advancing ──────────────────────────────
            new FsmTransition(
                BehaviourState.Mustering,
                (agent, ctx) => agent.StationaryTicks >= ctx.Config.MusterDuration,
                BehaviourState.Advancing,
                (agent, ctx) => FindAdvanceTarget(agent.Position, ctx)),

            // ── Advancing → Breaching ──────────────────────────────
            // Near a breached gate — push through (priority over siege)
            new FsmTransition(
                BehaviourState.Advancing,
                (agent, ctx) => ctx.HasBreachedGateNear(agent.Position),
                BehaviourState.Breaching,
                (agent, ctx) => ComputeBreachGoal(agent, ctx)),

            // ── Advancing → Sieging ────────────────────────────────
            new FsmTransition(
                BehaviourState.Advancing,
                (agent, ctx) => ctx.HasLockedOrAttackedGateNear(agent.Position),
                BehaviourState.Sieging,
                (agent, ctx) => SurroundGoal(agent, ctx)),

            // ── Sieging → Breaching ────────────────────────────────
            new FsmTransition(
                BehaviourState.Sieging,
                (agent, ctx) => ctx.HasBreachedGateNear(agent.Position),
                BehaviourState.Breaching,
                (agent, ctx) => ComputeBreachGoal(agent, ctx)),

            // ── Breaching → Claiming ───────────────────────────────
            new FsmTransition(
                BehaviourState.Breaching,
                (agent, ctx) => ctx.InsideUnownedZone(agent.Position),
                BehaviourState.Claiming,
                (agent, ctx) => NearestUnownedZone(agent.Position, ctx)),

            // ── Claiming → Pushing ─────────────────────────────────
            new FsmTransition(
                BehaviourState.Claiming,
                (agent, ctx) => ctx.ZoneClaimedByInvader(agent.Position),
                BehaviourState.Pushing,
                (agent, ctx) => FindNextLayerGate(agent.Position, ctx)),

            // ── Pushing → Breaching ────────────────────────────────
            new FsmTransition(
                BehaviourState.Pushing,
                (agent, ctx) => ctx.HasBreachedGateNear(agent.Position),
                BehaviourState.Breaching,
                (agent, ctx) => ComputeBreachGoal(agent, ctx)),

            // ── Pushing → Sieging ──────────────────────────────────
            new FsmTransition(
                BehaviourState.Pushing,
                (agent, ctx) => ctx.HasLockedOrAttackedGateNear(agent.Position),
                BehaviourState.Sieging,
                (agent, ctx) => SurroundGoal(agent, ctx)));

    // ── Goal resolvers ──────────────────────────────────────────

    /// <summary>
    /// Advance toward nearest attackable outer gate (any status except Open).
    /// </summary>
    private static Vector2 FindAdvanceTarget(Vector2 pos, FsmContext ctx)
    {
        Vector2 best = pos;
        float minD = float.MaxValue;
        foreach (var g in ctx.Gates)
        {
            if (g.Layer != CastleLayer.OuterCurtain) continue;
            if (g.Status == GateStatus.Open) continue;
            float d = Vector2.DistanceSquared(pos, g.Center);
            if (d < minD) { minD = d; best = g.Center; }
        }
        return best;
    }

    /// <summary>
    /// Breach goal: push through the nearest breached gate deep into the interior.
    /// </summary>
    private static Vector2 ComputeBreachGoal(SimAgent agent, FsmContext ctx)
    {
        // Find nearest breached gate
        GateState? nearest = null;
        float minD = float.MaxValue;
        foreach (var g in ctx.Gates)
        {
            if (g.Status != GateStatus.Breached) continue;
            float d = Vector2.DistanceSquared(agent.Position, g.Center);
            if (d < minD) { minD = d; nearest = g; }
        }
        if (nearest is null) return agent.Position;

        // Push inward from gate center
        int gr = (int)nearest.Center.Y;
        int gc = (int)nearest.Center.X;
        float midY = SiegeWorld.GridRows / 2f;
        float midX = SiegeWorld.GridCols / 2f;

        bool isHoriz = gr == SiegeWorld.WallTop || gr == SiegeWorld.WallBot
                    || gr == SiegeWorld.InnerTop || gr == SiegeWorld.InnerBot
                    || gr == SiegeWorld.KeepTop  || gr == SiegeWorld.KeepBot;

        float inward = isHoriz
            ? (gr < midY ? PenetrationDepth : -PenetrationDepth)
            : (gc < midX ? PenetrationDepth : -PenetrationDepth);

        return isHoriz
            ? nearest.Center + new Vector2(0, inward)
            : nearest.Center + new Vector2(inward, 0);
    }

    /// <summary>
    /// Surround a locked gate for siege pressure.
    /// Uses an outward-facing semicircle so all positions are reachable
    /// from outside the wall (no positions behind the gate inside the castle).
    /// </summary>
    private static Vector2 SurroundGoal(SimAgent agent, FsmContext ctx)
    {
        // Find nearest locked/under-attack gate
        GateState? nearest = null;
        float minD = float.MaxValue;
        foreach (var g in ctx.Gates)
        {
            if (g.Status != GateStatus.Locked && g.Status != GateStatus.UnderAttack) continue;
            float d = Vector2.DistanceSquared(agent.Position, g.Center);
            if (d < minD) { minD = d; nearest = g; }
        }
        if (nearest is null) return agent.Position;

        // Determine outward direction (away from castle center)
        int gr = (int)nearest.Center.Y;
        int gc = (int)nearest.Center.X;
        float midY = SiegeWorld.GridRows / 2f;
        float midX = SiegeWorld.GridCols / 2f;

        bool isHoriz = gr == SiegeWorld.WallTop || gr == SiegeWorld.WallBot
                    || gr == SiegeWorld.InnerTop || gr == SiegeWorld.InnerBot
                    || gr == SiegeWorld.KeepTop  || gr == SiegeWorld.KeepBot;

        float outwardAngle = isHoriz
            ? (gr < midY ? -MathF.PI / 2f : MathF.PI / 2f)
            : (gc < midX ? MathF.PI : 0f);

        // Spread slots across a semicircle facing outward
        int slot = agent.Id % SurroundSlots;
        float t = SurroundSlots > 1 ? (float)slot / (SurroundSlots - 1) : 0.5f;
        float angle = outwardAngle - MathF.PI / 2f + t * MathF.PI;

        return nearest.Center + new Vector2(
            MathF.Cos(angle) * SurroundRadius,
            MathF.Sin(angle) * SurroundRadius);
    }

    private static Vector2 NearestUnownedZone(Vector2 pos, FsmContext ctx)
    {
        Vector2 best = pos;
        float minD = float.MaxValue;
        foreach (var z in ctx.Zones)
        {
            if (z.Owner == AgentRole.Invader) continue;
            float d = Vector2.DistanceSquared(pos, z.Center);
            if (d < minD) { minD = d; best = z.Center; }
        }
        return best;
    }

    private static Vector2 FindNextLayerGate(Vector2 pos, FsmContext ctx)
    {
        var nextLayer = NextLayer(ctx.ActiveDefenceLine);
        Vector2 best = pos;
        float minD = float.MaxValue;
        foreach (var g in ctx.Gates)
        {
            if (g.Layer != nextLayer) continue;
            float d = Vector2.DistanceSquared(pos, g.Center);
            if (d < minD) { minD = d; best = g.Center; }
        }
        // If nothing on next layer, try deeper
        if (best == pos && nextLayer != CastleLayer.Keep)
        {
            var deeper = NextLayer(nextLayer);
            foreach (var g in ctx.Gates)
            {
                if (g.Layer != deeper) continue;
                float d = Vector2.DistanceSquared(pos, g.Center);
                if (d < minD) { minD = d; best = g.Center; }
            }
        }
        return best;
    }

    private static CastleLayer NextLayer(CastleLayer layer) => layer switch
    {
        CastleLayer.OuterCurtain => CastleLayer.InnerBailey,
        CastleLayer.InnerBailey  => CastleLayer.Keep,
        _                        => CastleLayer.Keep
    };
}
