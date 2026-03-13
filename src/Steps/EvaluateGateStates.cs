using System.Collections.Immutable;
using System.Numerics;
using EvalApp.Consumer;

using NavPathfinder.Demo.Data;

namespace NavPathfinder.Demo.Steps;

public sealed class EvaluateGateStates : PureStep<SiegeTickData>
{
    private const float GateRadius  = 3.0f;

    public override SiegeTickData Execute(SiegeTickData data)
    {
        var cfg        = data.Config ?? SiegeBalanceConfig.Default;
        int breachTicks = cfg.BreachTicks;
        int repairTicks = cfg.RepairTicks;
        var changes      = ImmutableArray.CreateBuilder<GateStateChange>();
        var updatedGates = ImmutableArray.CreateBuilder<GateState>(data.Gates.Length);

        var invaders  = data.Invaders;
        var defenders = data.Defenders;
        float gateRadiusSq = GateRadius * GateRadius;

        foreach (var gate in data.Gates)
        {
            bool hasInvaders  = HasAgentNear(invaders,  gate.Center, gateRadiusSq);
            bool hasDefenders = HasAgentNear(defenders, gate.Center, gateRadiusSq);

            GateState newGate = gate.Status switch
            {
                // Open → Locked when defenders hold with no attackers present
                GateStatus.Open when hasDefenders && !hasInvaders =>
                    Transition(gate with { TicksContested = 0, TicksUnderAttack = 0 }, GateStatus.Locked, changes),

                // Locked: count up while defenders hold uncontested
                GateStatus.Locked when !hasInvaders =>
                    gate with { TicksContested = gate.TicksContested + 1 },

                // Locked → UnderAttack when invaders arrive
                GateStatus.Locked when hasInvaders =>
                    Transition(gate with { TicksContested = 0, TicksUnderAttack = 1 }, GateStatus.UnderAttack, changes),

                // UnderAttack: increment until breach threshold
                GateStatus.UnderAttack when hasInvaders && gate.TicksUnderAttack < breachTicks =>
                    gate with { TicksUnderAttack = gate.TicksUnderAttack + 1 },

                // UnderAttack → Breached after sustained assault
                GateStatus.UnderAttack when gate.TicksUnderAttack >= breachTicks =>
                    Transition(gate with { TicksUnderAttack = 0, TicksContested = 0 }, GateStatus.Breached, changes),

                // UnderAttack → Locked when defenders drive invaders off
                GateStatus.UnderAttack when !hasInvaders =>
                    Transition(gate with { TicksUnderAttack = 0, TicksContested = 0 }, GateStatus.Locked, changes),

                // Breached + defenders uncontested → accumulate repair progress
                GateStatus.Breached when hasDefenders && !hasInvaders =>
                    gate.TicksContested + 1 >= repairTicks
                        ? Transition(gate with { TicksContested = 0 }, GateStatus.Locked, changes)
                        : gate with { TicksContested = gate.TicksContested + 1 },

                // Breached + invaders present → reset repair progress
                GateStatus.Breached when hasInvaders =>
                    gate with { TicksContested = 0 },

                // Breached with no one present → hold repair state
                GateStatus.Breached => gate,

                _ => gate
            };

            updatedGates.Add(newGate);
        }

        return data with
        {
            GateChanges  = changes.ToImmutable(),
            Gates = updatedGates.ToImmutable()
        };
    }

    private static GateState Transition(GateState gate, GateStatus to, ImmutableArray<GateStateChange>.Builder changes)
    {
        changes.Add(new GateStateChange(gate.Id, gate.Status, to));
        return gate with { Status = to };
    }

    private static bool HasAgentNear(ImmutableArray<SimAgent> agents, Vector2 center, float radiusSq)
    {
        for (int i = 0; i < agents.Length; i++)
            if (Vector2.DistanceSquared(agents[i].Position, center) <= radiusSq)
                return true;
        return false;
    }
}
