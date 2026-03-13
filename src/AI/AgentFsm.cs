using System.Collections.Immutable;
using System.Numerics;
using NavPathfinder.Demo.Data;

namespace NavPathfinder.Demo.AI;

/// <summary>
/// A single FSM transition: from state, guard predicate, target state, and goal resolver.
/// Transitions are evaluated in order — first matching guard wins.
/// </summary>
public sealed class FsmTransition
{
    public BehaviourState From { get; }
    public Func<SimAgent, FsmContext, bool> Guard { get; }
    public BehaviourState To { get; }
    public Func<SimAgent, FsmContext, Vector2> GoalResolver { get; }

    public FsmTransition(
        BehaviourState from,
        Func<SimAgent, FsmContext, bool> guard,
        BehaviourState to,
        Func<SimAgent, FsmContext, Vector2> goalResolver)
    {
        From = from;
        Guard = guard;
        To = to;
        GoalResolver = goalResolver;
    }
}

/// <summary>
/// Data-driven FSM engine. Evaluates a list of transitions in priority order.
/// First matching guard fires — returns new state, goal, and changed flag.
/// If no transition matches, agent stays in current state.
/// </summary>
public sealed class AgentFsm
{
    private readonly ImmutableArray<FsmTransition> _transitions;
    private readonly Func<SimAgent, FsmContext, Vector2>? _defaultGoalResolver;

    public AgentFsm(
        ImmutableArray<FsmTransition> transitions,
        Func<SimAgent, FsmContext, Vector2>? defaultGoalResolver = null)
    {
        _transitions = transitions;
        _defaultGoalResolver = defaultGoalResolver;
    }

    /// <summary>
    /// Evaluate the FSM for a single agent. Returns the (possibly new) state,
    /// resolved goal, and whether a transition fired.
    /// </summary>
    public (BehaviourState State, Vector2 Goal, bool Changed) Evaluate(
        SimAgent agent, FsmContext ctx)
    {
        foreach (var t in _transitions)
        {
            if (t.From != agent.Behaviour) continue;
            if (!t.Guard(agent, ctx)) continue;

            var goal = t.GoalResolver(agent, ctx);
            return (t.To, goal, true);
        }

        // No transition — keep current state, optionally refresh goal
        return (agent.Behaviour, agent.Goal, false);
    }

    /// <summary>
    /// Returns true if the given state is considered "stationary" for StationaryTicks tracking.
    /// </summary>
    public static bool IsStationaryState(BehaviourState state) => state switch
    {
        BehaviourState.Mustering => true,
        BehaviourState.Sieging   => true,
        BehaviourState.Holding   => true,
        BehaviourState.Fighting  => true,
        BehaviourState.Claiming  => true,
        BehaviourState.Hidden    => true,
        BehaviourState.LastStand => true,
        _                        => false
    };
}
