using EvalApp.Consumer;

using NavPathfinder.Demo.Data;

namespace NavPathfinder.Demo.Steps;

/// <summary>
/// Pure step: evaluates staged win conditions from gate states each tick.
///
/// Layer-by-layer progression:
///   OuterBreached    — all outer curtain gates breached
///   InnerContested   — inner bailey gates under attack
///   InvadersWinning  — keep under direct assault (ActiveDefenceLine == Keep)
///   DefendersWinning — invader pool nearly exhausted (< 10% of start)
///   InvadersWon      — keep held for InvHoldTicks consecutive ticks
///   DefendersWon     — invader pool exhausted or all gates locked for DefHoldTicks
/// </summary>
public sealed class EvaluateWinCondition : PureStep<SiegeTickData>
{
    private const int WinningThreshold = 15;   // ticks all-locked → DefendersWinning state (~1s at 15fps)

    public override SiegeTickData Execute(SiegeTickData data)
    {
        if (data.WinState is WinState.InvadersWon or WinState.DefendersWon)
            return data;

        var cfg   = data.Config ?? SiegeBalanceConfig.Default;
        var gates = data.Gates;

        // Count gate states per layer
        int outerTotal = 0, outerBreached = 0;
        int innerTotal = 0, innerAttacked = 0, innerBreached = 0;
        bool allLocked = true;

        foreach (var g in gates)
        {
            if (g.Status != GateStatus.Locked) allLocked = false;

            if (g.Layer == CastleLayer.OuterCurtain)
            {
                outerTotal++;
                if (g.Status == GateStatus.Breached) outerBreached++;
            }
            else if (g.Layer == CastleLayer.InnerBailey)
            {
                innerTotal++;
                if (g.Status == GateStatus.Breached)    innerBreached++;
                if (g.Status == GateStatus.UnderAttack)  innerAttacked++;
            }
        }

        bool keepAssault = data.ActiveDefenceLine == CastleLayer.Keep;
        bool allOuterBreached = outerTotal > 0 && outerBreached == outerTotal;
        bool innerUnderPressure = innerAttacked > 0 || innerBreached > 0;
        float invaderPoolRatio = cfg.InvaderPool > 0
            ? (float)data.InvaderPool / cfg.InvaderPool
            : 0f;

        // Breach hold counter — increments when keep is under assault
        int breachHold = keepAssault ? data.BreachHoldTicks + 1 : 0;

        // Defender gate hold counter — resets the moment any gate is not locked
        int gateHold = allLocked ? data.GateHoldTicks + 1 : 0;

        WinState win = DetermineWinState(
            cfg, breachHold, gateHold, allOuterBreached,
            innerUnderPressure, keepAssault, invaderPoolRatio);

        return data with
        {
            WinState        = win,
            BreachHoldTicks = breachHold,
            GateHoldTicks   = gateHold,
        };
    }

    private static WinState DetermineWinState(
        SiegeBalanceConfig cfg, int breachHold, int gateHold,
        bool allOuterBreached, bool innerUnderPressure,
        bool keepAssault, float invaderPoolRatio)
    {
        if (breachHold >= cfg.InvHoldTicks)
            return WinState.InvadersWon;
        if (gateHold >= cfg.DefHoldTicks)
            return WinState.DefendersWon;
        if (invaderPoolRatio <= 0f)
            return WinState.DefendersWon;
        if (keepAssault)
            return WinState.InvadersWinning;
        if (invaderPoolRatio < 0.10f)
            return WinState.DefendersWinning;
        if (allOuterBreached && innerUnderPressure)
            return WinState.InnerContested;
        if (allOuterBreached)
            return WinState.OuterBreached;
        if (gateHold >= WinningThreshold)
            return WinState.DefendersWinning;
        return WinState.InProgress;
    }
}
