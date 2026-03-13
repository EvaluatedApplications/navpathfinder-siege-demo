using EvalApp.Consumer;

using NavPathfinder.Demo.Data;

namespace NavPathfinder.Demo.Steps;

/// <summary>
/// Pure step: derives morale for each population from gate states grouped by layer.
///
/// Morale is a [0.1, 1.0] scalar that feeds three downstream systems:
///   1. Agent speed  — high morale = faster, decisive movement
///   2. Agent budget — high morale populations keep more agents active under pressure
///   3. RVO priority — agents sorted by priority score; high morale = wider ConflictRadius
///
/// Layer-aware gate-state mapping:
///   Outer breaches (6 gates) have moderate impact — the castle still has inner defences.
///   Inner breaches (2 gates) have severe impact — the keep is directly threatened.
///   ActiveDefenceLine at Keep drastically impacts defender morale.
/// </summary>
public sealed class ComputeMorale : PureStep<SiegeTickData>
{
    public override SiegeTickData Execute(SiegeTickData data)
    {
        var gates = data.Gates;

        int outerBreached = 0, outerLocked = 0, outerAttacked = 0, outerOpen = 0;
        int innerBreached = 0, innerLocked = 0, innerAttacked = 0, innerOpen = 0;
        foreach (var g in gates)
        {
            if (g.Layer == CastleLayer.OuterCurtain)
                CountGate(g.Status, ref outerBreached, ref outerLocked, ref outerAttacked, ref outerOpen);
            else if (g.Layer == CastleLayer.InnerBailey)
                CountGate(g.Status, ref innerBreached, ref innerLocked, ref innerAttacked, ref innerOpen);
        }

        float invMorale = ComputeInvaderMorale(
            outerBreached, outerLocked, outerOpen, outerAttacked,
            innerBreached, innerLocked, innerOpen, innerAttacked);

        float defMorale = ComputeDefenderMorale(
            outerBreached, outerLocked, outerAttacked,
            innerBreached, innerLocked, innerAttacked,
            data.ActiveDefenceLine);

        float civMorale = ComputeCivilianMorale(
            outerBreached, outerAttacked,
            innerBreached, innerAttacked,
            data.ActiveDefenceLine);

        return data with
        {
            InvaderMorale  = invMorale,
            DefenderMorale = defMorale,
            CivilianMorale = civMorale,
        };
    }

    private static void CountGate(GateStatus status, ref int breached, ref int locked, ref int attacked, ref int open)
    {
        if      (status == GateStatus.Breached)    breached++;
        else if (status == GateStatus.Locked)      locked++;
        else if (status == GateStatus.UnderAttack) attacked++;
        else                                        open++;
    }

    /// <summary>
    /// Invaders: outer breaches give moderate boost, inner breaches give big boost.
    /// </summary>
    private static float ComputeInvaderMorale(
        int outerBreached, int outerLocked, int outerOpen, int outerAttacked,
        int innerBreached, int innerLocked, int innerOpen, int innerAttacked)
    {
        return Math.Clamp(
            0.5f
            + outerBreached * 0.10f + outerOpen * 0.04f - outerLocked * 0.04f + outerAttacked * 0.05f
            + innerBreached * 0.30f + innerOpen * 0.08f - innerLocked * 0.06f + innerAttacked * 0.12f,
            0.1f, 1.0f);
    }

    /// <summary>
    /// Defenders: outer locked = pride, inner breaches = severe morale hit.
    /// Defence line at Keep causes significant penalty.
    /// </summary>
    private static float ComputeDefenderMorale(
        int outerBreached, int outerLocked, int outerAttacked,
        int innerBreached, int innerLocked, int innerAttacked,
        CastleLayer activeLine)
    {
        float keepPenalty = activeLine == CastleLayer.Keep ? -0.25f : 0f;
        return Math.Clamp(
            0.5f
            + outerLocked * 0.08f - outerBreached * 0.10f + outerAttacked * 0.03f
            + innerLocked * 0.20f - innerBreached * 0.35f + innerAttacked * 0.05f
            + keepPenalty,
            0.1f, 1.0f);
    }

    /// <summary>
    /// Civilians: threatened by any hostile activity, inner breaches are terrifying.
    /// </summary>
    private static float ComputeCivilianMorale(
        int outerBreached, int outerAttacked,
        int innerBreached, int innerAttacked,
        CastleLayer activeLine)
    {
        float keepPenalty = activeLine == CastleLayer.Keep ? -0.30f : 0f;
        return Math.Clamp(
            1.0f
            - outerBreached * 0.10f - outerAttacked * 0.05f
            - innerBreached * 0.30f - innerAttacked * 0.15f
            + keepPenalty,
            0.1f, 1.0f);
    }
}
