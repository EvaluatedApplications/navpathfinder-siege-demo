using System.Collections.Immutable;
using System.Numerics;
using NavPathfinder.Demo.Data.Combat;
using NavPathfinder.Demo.Rendering;
using NavPathfinder.Sdk.Models;

namespace NavPathfinder.Demo.Data;

public record SiegeTickData(
    // Input carried each tick
    NavMeshHandle NavMesh,
    int TickNumber,
    ImmutableArray<SimAgent> Invaders,
    ImmutableArray<SimAgent> Defenders,
    ImmutableArray<SimAgent> Civilians,
    ImmutableArray<GateState> Gates,

    // Castle layout — set once, carried every tick
    CastleLayout Castle = null!,

    // Timing (supplied by run loop)
    double WallClockMs = 0,
    double RenderMs = 0,
    double Fps = 0,

    // Spawn state — carry-forward
    float InvaderCredits  = 0f,
    float DefenderCredits = 0f,
    float CivilianCredits = 0f,
    int   InvaderPool     = 3000,
    int   DefenderPool    = 500,
    int   CivilianPool    = 200,

    // Win condition — carry-forward
    WinState WinState     = WinState.InProgress,
    int      BreachHoldTicks = 0,
    int      GateHoldTicks   = 0,

    // Castle layer control
    CastleLayer ActiveDefenceLine = CastleLayer.OuterCurtain,
    ImmutableArray<ZoneControl> Zones = default,

    // Behaviour AI — lightweight FSM state per agent
    ImmutableDictionary<int, BehaviourState>? BehaviourOverrides = null,
    int PathRecalcsThisTick = 0,

    // Stage: intermediate outputs
    ImmutableArray<GateStateChange>? GateChanges = null,
    ImmutableArray<AgentDto>? InvaderDtos = null,
    ImmutableArray<AgentDto>? DefenderDtos = null,
    ImmutableArray<AgentDto>? CivilianDtos = null,
    IReadOnlyList<float>? ThreatMap = null,
    float ThreatLevel = 0f,
    IReadOnlyList<float>? DefenceMap = null,
    ImmutableArray<IReadOnlyList<Vector2>>? BreachPaths = null,
    float InvaderMorale  = 1f,
    float DefenderMorale = 1f,
    float CivilianMorale = 1f,

    // Balance config
    SiegeBalanceConfig Config = null!,

    // Performance metrics — collected per tick for HUD
    float PathfindingMs = 0f,
    float SeparationMs = 0f,
    float CombatMs = 0f,
    float CacheHitRate = 0f,
    int ActiveAgentCount = 0,

    // Deferred combat damage & events
    ImmutableArray<PendingDamage> PendingDamage = default,
    ImmutableArray<CombatEvent> CombatEvents = default,

    // Output
    SimTickResult? SimResult = null,

    // Rendering pipeline state
    RenderBuffer? RenderBuffer = null,
    bool IsFirstFrame = false,
    string? RenderedFrame = null);
