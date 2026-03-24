using System.Collections.Immutable;
using System.Numerics;
using EvalApp.Consumer;

using NavPathfinder.Demo.Data;
using NavPathfinder.Demo.Simulation;

namespace NavPathfinder.Demo.Steps;

/// <summary>
/// Updates zone ownership based on agent positions.
/// Invaders contest zones; when ContestPercent exceeds threshold AND the layer's
/// gate has been breached, they claim the zone for forward spawning.
/// </summary>
public sealed class UpdateZoneControl : PureStep<SiegeTickData>
{
    public override SiegeTickData Execute(SiegeTickData data)
    {
        var zones = data.Zones;
        if (zones.IsDefaultOrEmpty)
            return data;

        var invaders = data.Invaders;
        var defenders = data.Defenders;
        var gates = data.Gates;
        float contestRadius = data.Config.ZoneContestRadius;
        float contestRadiusSq = contestRadius * contestRadius;
        float claimThreshold = data.Config.ZoneClaimThreshold;

        // Wall boundaries — invaders must be in the correct castle layer to contest a zone
        int outerL = SiegeWorld.WallLeft,  outerR = SiegeWorld.WallRight;
        int outerT = SiegeWorld.WallTop,   outerB = SiegeWorld.WallBot;
        int innerL = SiegeWorld.InnerLeft, innerR = SiegeWorld.InnerRight;
        int innerT = SiegeWorld.InnerTop,  innerB = SiegeWorld.InnerBot;

        var builder = ImmutableArray.CreateBuilder<ZoneControl>(zones.Length);
        foreach (var zone in zones)
        {
            int invCount = 0, defCount = 0;

            for (int i = 0; i < invaders.Length; i++)
            {
                var pos = invaders[i].Position;
                if (!IsInLayer(pos, zone.Layer, outerL, outerR, outerT, outerB,
                                                 innerL, innerR, innerT, innerB))
                    continue;
                if (Vector2.DistanceSquared(pos, zone.Center) < contestRadiusSq)
                    invCount++;
            }
            for (int i = 0; i < defenders.Length; i++)
                if (Vector2.DistanceSquared(defenders[i].Position, zone.Center) < contestRadiusSq)
                    defCount++;

            int total = invCount + defCount;
            float contest = total > 0 ? (float)invCount / total : 0f;

            // Zone can only be claimed if at least one gate on its layer is breached
            bool layerBreached = HasBreachedGate(gates, zone.Layer);

            // Track how long invaders have sustained contest above threshold
            bool aboveThreshold = layerBreached && contest > claimThreshold;
            int holdTicks = aboveThreshold ? zone.ContestHoldTicks + 1 : 0;

            // Already owned zones are easier to maintain (half threshold, no hold timer)
            bool claimed = zone.Owner == AgentRole.Invader
                ? layerBreached && contest > claimThreshold * 0.5f
                : holdTicks >= data.Config.ZoneClaimHoldTicks;

            var owner = claimed ? AgentRole.Invader : (AgentRole?)null;
            bool spawn = claimed && owner == AgentRole.Invader;

            builder.Add(zone with
            {
                Owner = owner,
                ContestPercent = contest,
                ContestHoldTicks = holdTicks,
                SpawnEnabled = spawn,
                SpawnPoint = spawn ? zone.Center : default
            });
        }

        return data with { Zones = builder.MoveToImmutable() };
    }

    private static bool HasBreachedGate(ImmutableArray<GateState> gates, CastleLayer layer)
    {
        foreach (var g in gates)
            if (g.Layer == layer && g.Status == GateStatus.Breached)
                return true;
        return false;
    }

    /// <summary>
    /// Returns true if the position is geometrically inside the correct castle layer.
    /// OuterCurtain: between outer and inner walls.
    /// InnerBailey: between inner walls and keep (approximated as inside inner walls).
    /// </summary>
    private static bool IsInLayer(Vector2 pos, CastleLayer layer,
        int oL, int oR, int oT, int oB,
        int iL, int iR, int iT, int iB) => layer switch
    {
        // Must be inside outer wall but NOT inside inner wall
        CastleLayer.OuterCurtain =>
            pos.X > oL && pos.X < oR && pos.Y > oT && pos.Y < oB &&
            !(pos.X > iL && pos.X < iR && pos.Y > iT && pos.Y < iB),

        // Must be inside inner wall
        CastleLayer.InnerBailey =>
            pos.X > iL && pos.X < iR && pos.Y > iT && pos.Y < iB,

        // Keep — must be deep inside (use inner wall for now)
        _ => pos.X > iL && pos.X < iR && pos.Y > iT && pos.Y < iB
    };
}
