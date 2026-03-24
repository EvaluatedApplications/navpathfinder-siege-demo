using System.Numerics;

namespace NavPathfinder.Demo.Data;

/// <summary>
/// Represents a contestable area of the castle.
/// When invaders hold >50% contest, they claim it for forward spawning.
/// </summary>
public record ZoneControl(
    string ZoneId,
    CastleLayer Layer,
    Vector2 Center,
    float Radius,
    AgentRole? Owner = null,
    float ContestPercent = 0f,
    int ContestHoldTicks = 0,
    bool SpawnEnabled = false,
    Vector2 SpawnPoint = default);

public enum CastleLayer { OuterCurtain, InnerBailey, Keep }
