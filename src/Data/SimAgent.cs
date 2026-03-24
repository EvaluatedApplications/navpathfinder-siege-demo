using System.Collections.Immutable;
using System.Numerics;
using NavPathfinder.Demo.Data.Combat;
using NavPathfinder.Sdk.Models;

namespace NavPathfinder.Demo.Data;

/// <summary>
/// Demo agent extending <see cref="GameAgent"/> with DF-style RPG properties.
/// Inherits SDK combat stats (Attack, Defense, Health, etc.) and adds
/// game-specific fields (Role, Morale, Stats, Skills, Gear, Combat).
/// The SDK's <c>ResolveOutcomesAsync&lt;SimAgent&gt;</c> receives this typed agent
/// directly — no mapping needed.
/// </summary>
public record SimAgent(
    int Id,
    Vector2 Position,
    Vector2 Goal,
    bool GoalChanged,
    AgentRole Role,
    BehaviourState Behaviour = BehaviourState.Mustering,
    Vector2 SubGoal = default,
    float Health = 100f,
    float Morale = 1f,
    int StationaryTicks = 0,
    float MaxSpeed = 1.5f,
    float Radius = 0.5f,
    float Attack = 10f,
    float Defense = 5f,
    float MaxHealth = 100f,
    float AttackSpeed = 1.0f,
    float AttackRange = 1.0f,
    int MaxAttacksPerTick = 1,
    ImmutableArray<Vector2> CurrentWaypoints = default,
    AgentStats? Stats = null,
    AgentSkills? Skills = null,
    Equipment? Gear = null,
    CombatState? Combat = null)
    : GameAgent(Id, Position, Goal, Radius, MaxSpeed, GoalChanged,
        Attack, Defense, Health, MaxHealth, AttackSpeed, AttackRange, MaxAttacksPerTick)
{
    public AgentStats Stats { get; init; } = Stats ?? new AgentStats();
    public AgentSkills Skills { get; init; } = Skills ?? new AgentSkills();
    public Equipment Gear { get; init; } = Gear ?? new Equipment();
    public CombatState Combat { get; init; } = Combat ?? new CombatState();
}

public enum AgentRole { Invader, Defender, Civilian }
