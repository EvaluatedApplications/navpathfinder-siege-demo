using System.Collections.Immutable;
using System.Numerics;
using NavPathfinder.Demo.Data;
using NavPathfinder.Demo.Simulation;

namespace NavPathfinder.Demo.AI;

/// <summary>
/// Read-only snapshot of world state passed to FSM guard predicates and goal resolvers.
/// Avoids coupling FSM logic to SiegeTickData directly.
/// </summary>
public readonly record struct FsmContext(
    ImmutableArray<GateState> Gates,
    ImmutableArray<ZoneControl> Zones,
    ImmutableArray<SimAgent> Invaders,
    ImmutableArray<SimAgent> Defenders,
    CastleLayout Castle,
    CastleLayer ActiveDefenceLine,
    SiegeBalanceConfig Config)
{
    public float GateDetectRadiusSq => Config.GateDetectRadius * Config.GateDetectRadius;
    public float CombatRadiusSq => Config.CombatRadius * Config.CombatRadius;

    public bool HasBreachedGateNear(Vector2 pos) =>
        HasGateNear(pos, GateStatus.Breached, GateDetectRadiusSq);

    public bool HasLockedOrAttackedGateNear(Vector2 pos) =>
        HasGateNear(pos, GateStatus.Locked, GateDetectRadiusSq) ||
        HasGateNear(pos, GateStatus.UnderAttack, GateDetectRadiusSq);

    public bool HasGateNear(Vector2 pos, GateStatus status, float radiusSq)
    {
        foreach (var g in Gates)
            if (g.Status == status && Vector2.DistanceSquared(pos, g.Center) <= radiusSq)
                return true;
        return false;
    }

    public bool AnyInvaderNear(Vector2 pos, float radiusSq)
    {
        foreach (var inv in Invaders)
            if (Vector2.DistanceSquared(pos, inv.Position) <= radiusSq)
                return true;
        return false;
    }

    public bool AnyDefenderNear(Vector2 pos, float radiusSq) =>
        AnyAgentNear(Defenders, pos, radiusSq);

    public static bool AnyAgentNear(ImmutableArray<SimAgent> agents, Vector2 pos, float radiusSq)
    {
        foreach (var a in agents)
            if (Vector2.DistanceSquared(pos, a.Position) <= radiusSq)
                return true;
        return false;
    }

    public bool InsideUnownedZone(Vector2 pos)
    {
        foreach (var z in Zones)
            if (z.Owner != AgentRole.Invader &&
                Vector2.DistanceSquared(pos, z.Center) < z.Radius * z.Radius)
                return true;
        return false;
    }

    public bool ZoneClaimedByInvader(Vector2 pos)
    {
        foreach (var z in Zones)
            if (z.Owner == AgentRole.Invader &&
                Vector2.DistanceSquared(pos, z.Center) < z.Radius * z.Radius)
                return true;
        return false;
    }

    public Vector2 NearestBreachedGate(Vector2 pos)
    {
        Vector2 best = pos;
        float minD = float.MaxValue;
        foreach (var g in Gates)
        {
            if (g.Status != GateStatus.Breached) continue;
            float d = Vector2.DistanceSquared(pos, g.Center);
            if (d < minD) { minD = d; best = g.Center; }
        }
        return best;
    }

    public Vector2 NearestGateOnLayer(Vector2 pos, CastleLayer layer)
    {
        Vector2 best = pos;
        float minD = float.MaxValue;
        foreach (var g in Gates)
        {
            if (g.Layer != layer) continue;
            float d = Vector2.DistanceSquared(pos, g.Center);
            if (d < minD) { minD = d; best = g.Center; }
        }
        return best;
    }

    public Vector2 NearestAttackableGate(Vector2 pos)
    {
        Vector2 best = pos;
        float minD = float.MaxValue;
        foreach (var g in Gates)
        {
            if (g.Status == GateStatus.Open) continue;
            if (g.Layer != CastleLayer.OuterCurtain) continue;
            float d = Vector2.DistanceSquared(pos, g.Center);
            if (d < minD) { minD = d; best = g.Center; }
        }
        return best;
    }

    /// <summary>Nearest gate center of any status.</summary>
    public Vector2 NearestGateCenter(Vector2 pos)
    {
        Vector2 best = pos;
        float minD = float.MaxValue;
        foreach (var g in Gates)
        {
            float d = Vector2.DistanceSquared(pos, g.Center);
            if (d < minD) { minD = d; best = g.Center; }
        }
        return best;
    }

    public Vector2 MostThreatenedGate(CastleLayer layer)
    {
        Vector2 best = Vector2.Zero;
        int maxTicks = -1;
        foreach (var g in Gates)
        {
            if (g.Layer != layer) continue;
            if (g.TicksUnderAttack > maxTicks)
            { maxTicks = g.TicksUnderAttack; best = g.Center; }
        }
        if (maxTicks <= 0)
            foreach (var g in Gates)
                if (g.Layer == layer) return g.Center;
        return best;
    }

    public bool NearThreatenedGate(Vector2 pos, float radiusSq)
    {
        foreach (var g in Gates)
            if (g.TicksUnderAttack > 0 && Vector2.DistanceSquared(pos, g.Center) <= radiusSq)
                return true;
        return false;
    }

    public bool NearInnerLayerGate(Vector2 pos, float radiusSq)
    {
        foreach (var g in Gates)
            if (g.Layer == ActiveDefenceLine && Vector2.DistanceSquared(pos, g.Center) <= radiusSq)
                return true;
        return false;
    }

    public CastleLayer? FindAgentLayer(Vector2 pos)
    {
        float minD = float.MaxValue;
        CastleLayer? layer = null;
        foreach (var g in Gates)
        {
            float d = Vector2.DistanceSquared(pos, g.Center);
            if (d < minD) { minD = d; layer = g.Layer; }
        }
        return layer;
    }

    public bool LayerBehindDefenceLine(CastleLayer agentLayer) =>
        (int)agentLayer < (int)ActiveDefenceLine;

    public Vector2 KeepCenter =>
        Castle?.KeepPositions.IsDefaultOrEmpty != false
            ? new Vector2(60, 14)
            : Castle.KeepPositions[0];

    public bool NearKeep(Vector2 pos, float radiusSq)
    {
        if (Castle?.KeepPositions.IsDefaultOrEmpty != false) return false;
        foreach (var kp in Castle.KeepPositions)
            if (Vector2.DistanceSquared(pos, kp) < radiusSq) return true;
        return false;
    }

    /// <summary>
    /// Returns a position OUTSIDE the nearest outer gate — 5 cells outward from gate center.
    /// Used for Sortie patrol positions.
    /// </summary>
    public Vector2 OutsideNearestGate(Vector2 pos)
    {
        float minD = float.MaxValue;
        GateState? best = null;
        foreach (var g in Gates)
        {
            if (g.Layer != CastleLayer.OuterCurtain) continue;
            float d = Vector2.DistanceSquared(pos, g.Center);
            if (d < minD) { minD = d; best = g; }
        }
        return best is not null ? OutsideGatePosition(best) : pos;
    }

    /// <summary>
    /// Returns a position 5 cells outward from a gate center (outside the wall).
    /// </summary>
    public static Vector2 OutsideGatePosition(GateState gate)
    {
        int gr = (int)gate.Center.Y;
        int gc = (int)gate.Center.X;
        bool isHoriz = gr <= 4 || gr >= SiegeWorld.GridRows - 4;

        if (isHoriz)
            return gate.Center + new Vector2(0, gr < SiegeWorld.GridRows / 2 ? -5f : 5f);
        return gate.Center + new Vector2(gc < SiegeWorld.GridCols / 2 ? -5f : 5f, 0);
    }

    /// <summary>True if position is outside outer curtain walls.</summary>
    public bool IsOutsideWalls(Vector2 pos) =>
        pos.X < SiegeWorld.WallLeft || pos.X > SiegeWorld.WallRight ||
        pos.Y < SiegeWorld.WallTop  || pos.Y > SiegeWorld.WallBot;

    /// <summary>Count invaders within radius of position.</summary>
    public int CountInvadersNear(Vector2 pos, float radiusSq)
    {
        int n = 0;
        foreach (var inv in Invaders)
            if (Vector2.DistanceSquared(pos, inv.Position) <= radiusSq) n++;
        return n;
    }

    /// <summary>Count defenders within radius of position.</summary>
    public int CountDefendersNear(Vector2 pos, float radiusSq)
    {
        int n = 0;
        foreach (var def in Defenders)
            if (Vector2.DistanceSquared(pos, def.Position) <= radiusSq) n++;
        return n;
    }
}
