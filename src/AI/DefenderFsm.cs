using System.Collections.Immutable;
using System.Numerics;
using NavPathfinder.Demo.Data;

namespace NavPathfinder.Demo.AI;

/// <summary>
/// Defender FSM transition table.
/// Flow: Sortie → Fighting ↔ Holding ↔ Reinforcing → FallingBack → LastStand
/// Sortie defenders sally outside gates, fight, then fall back when outnumbered.
/// </summary>
public static class DefenderFsm
{
    public static AgentFsm Create() => new(BuildTransitions());

    private static ImmutableArray<FsmTransition> BuildTransitions() =>
        ImmutableArray.Create(
            // ── FallingBack (forced by defence line) ───────────────
            // This is checked first as a global override — any defender behind
            // the active defence line must fall back regardless of current state.
            // Handled inline in EvaluateAgentAI before FSM evaluation.

            // ── Sortie → Fighting ──────────────────────────────────
            // Outside-wall defenders engage invaders on contact
            new FsmTransition(
                BehaviourState.Sortie,
                (agent, ctx) => ctx.AnyInvaderNear(agent.Position, ctx.CombatRadiusSq),
                BehaviourState.Fighting,
                (agent, _) => agent.Position),

            // ── Sortie → FallingBack ───────────────────────────────
            // Retreat inside if defence line has moved inward (outer curtain fallen)
            new FsmTransition(
                BehaviourState.Sortie,
                (_, ctx) => ctx.ActiveDefenceLine != CastleLayer.OuterCurtain,
                BehaviourState.FallingBack,
                (agent, ctx) => ctx.NearestGateCenter(agent.Position)),

            // ── Holding → Fighting ─────────────────────────────────
            new FsmTransition(
                BehaviourState.Holding,
                (agent, ctx) => ctx.AnyInvaderNear(agent.Position, ctx.CombatRadiusSq),
                BehaviourState.Fighting,
                (agent, _) => agent.Position),

            // ── Holding → Reinforcing ──────────────────────────────
            new FsmTransition(
                BehaviourState.Holding,
                (agent, ctx) => ShouldReinforce(agent, ctx),
                BehaviourState.Reinforcing,
                (agent, ctx) => ctx.MostThreatenedGate(ctx.ActiveDefenceLine)),

            // ── Reinforcing → Holding ──────────────────────────────
            new FsmTransition(
                BehaviourState.Reinforcing,
                (agent, ctx) => ctx.NearThreatenedGate(agent.Position, 4f),
                BehaviourState.Holding,
                (agent, ctx) => ctx.NearestGateCenter(agent.Position)),

            // ── Reinforcing → Fighting ─────────────────────────────
            new FsmTransition(
                BehaviourState.Reinforcing,
                (agent, ctx) => ctx.AnyInvaderNear(agent.Position, ctx.CombatRadiusSq),
                BehaviourState.Fighting,
                (agent, _) => agent.Position),

            // ── Fighting → FallingBack (outside wall, outnumbered) ─
            // Sortie fighters retreat inside when overwhelmed
            new FsmTransition(
                BehaviourState.Fighting,
                (agent, ctx) => ShouldRetreatFromSortie(agent, ctx),
                BehaviourState.FallingBack,
                (agent, ctx) => ctx.NearestGateCenter(agent.Position)),

            // ── Fighting → Holding ─────────────────────────────────
            new FsmTransition(
                BehaviourState.Fighting,
                (agent, ctx) =>
                {
                    float disengage = ctx.Config.CombatRadius * 1.5f;
                    return !ctx.AnyInvaderNear(agent.Position, disengage * disengage);
                },
                BehaviourState.Holding,
                (agent, ctx) => ctx.NearestGateCenter(agent.Position)),

            // ── FallingBack → Holding/LastStand ────────────────────
            new FsmTransition(
                BehaviourState.FallingBack,
                (agent, ctx) => ctx.NearInnerLayerGate(agent.Position, 4f),
                BehaviourState.Holding, // EvaluateAgentAI may override to LastStand
                (agent, ctx) => ctx.ActiveDefenceLine == CastleLayer.Keep
                    ? ctx.KeepCenter
                    : ctx.NearestGateCenter(agent.Position)));

    /// <summary>
    /// Sortie fighters outside walls should retreat when outnumbered 2:1 or more.
    /// </summary>
    private static bool ShouldRetreatFromSortie(SimAgent defender, FsmContext ctx)
    {
        if (!ctx.IsOutsideWalls(defender.Position)) return false;

        float radius = ctx.Config.CombatRadius * 2.5f;
        float radiusSq = radius * radius;
        int invaders = ctx.CountInvadersNear(defender.Position, radiusSq);
        int defenders = ctx.CountDefendersNear(defender.Position, radiusSq);

        return defenders > 0 && invaders >= defenders * 2;
    }

    private static bool ShouldReinforce(SimAgent defender, FsmContext ctx)
    {
        float gateRadiusSq = ctx.CombatRadiusSq;

        // Find my nearest gate
        GateState? myGate = null;
        float myDist = float.MaxValue;
        foreach (var g in ctx.Gates)
        {
            float d = Vector2.DistanceSquared(defender.Position, g.Center);
            if (d < myDist) { myDist = d; myGate = g; }
        }
        if (myGate is null) return false;

        // My gate must be safe
        if (ctx.AnyInvaderNear(myGate.Center, gateRadiusSq)) return false;

        // Check other gates for dangerous ratios
        foreach (var gate in ctx.Gates)
        {
            if (gate.Id == myGate.Id) continue;
            if (gate.Status == GateStatus.Open) continue;

            int attackers = CountNear(gate.Center, ctx.Invaders, gateRadiusSq);
            if (attackers == 0) continue;

            int defenders = CountNear(gate.Center, ctx.Defenders, gateRadiusSq);
            float ratio = defenders == 0 ? attackers : (float)attackers / defenders;

            if (ratio > ctx.Config.ReinforceThreshold) return true;
        }
        return false;
    }

    private static int CountNear(Vector2 pos, ImmutableArray<SimAgent> agents, float radiusSq)
    {
        int n = 0;
        foreach (var a in agents)
            if (Vector2.DistanceSquared(pos, a.Position) <= radiusSq) n++;
        return n;
    }
}
