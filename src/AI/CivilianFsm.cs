using System.Collections.Immutable;
using System.Numerics;
using NavPathfinder.Demo.Data;

namespace NavPathfinder.Demo.AI;

/// <summary>
/// Civilian FSM transition table.
/// Flow: Sheltering → Fleeing → Hidden (in keep)
/// </summary>
public static class CivilianFsm
{
    private const float FleeRadius = 12f;
    private const float KeepProximity = 3f;

    public static AgentFsm Create() => new(BuildTransitions());

    private static ImmutableArray<FsmTransition> BuildTransitions() =>
        ImmutableArray.Create(
            // ── Sheltering → Fleeing ───────────────────────────────
            new FsmTransition(
                BehaviourState.Sheltering,
                (agent, ctx) => ctx.AnyInvaderNear(agent.Position, FleeRadius * FleeRadius),
                BehaviourState.Fleeing,
                (agent, ctx) => ctx.KeepCenter),

            // ── Sheltering → Hidden ────────────────────────────────
            new FsmTransition(
                BehaviourState.Sheltering,
                (agent, ctx) => NearKeep(agent.Position, ctx),
                BehaviourState.Hidden,
                (agent, ctx) => ctx.KeepCenter),

            // ── Fleeing → Hidden ───────────────────────────────────
            new FsmTransition(
                BehaviourState.Fleeing,
                (agent, ctx) => NearKeep(agent.Position, ctx),
                BehaviourState.Hidden,
                (agent, ctx) => ctx.KeepCenter),

            // ── Fleeing → Sheltering ───────────────────────────────
            new FsmTransition(
                BehaviourState.Fleeing,
                (agent, ctx) =>
                {
                    bool safe = !ctx.AnyInvaderNear(agent.Position, FleeRadius * FleeRadius);
                    return safe && agent.StationaryTicks >= 3;
                },
                BehaviourState.Sheltering,
                (agent, ctx) => ctx.KeepCenter));

    private static bool NearKeep(Vector2 pos, FsmContext ctx) =>
        Vector2.DistanceSquared(pos, ctx.KeepCenter) < KeepProximity * KeepProximity;
}
