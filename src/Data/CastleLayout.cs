using System.Collections.Immutable;
using System.Numerics;

namespace NavPathfinder.Demo.Data;

/// <summary>
/// Defines the physical layout of the multi-layer concentric castle.
/// Generated procedurally by CastleBuilder. Immutable after creation.
/// </summary>
public record CastleLayout(
    int Width,
    int Height,
    ImmutableArray<WallSegment> Walls,
    ImmutableArray<GateDefinition> GateDefinitions,
    ImmutableArray<ZoneDefinition> ZoneDefinitions,
    ImmutableArray<Vector2> KeepPositions,
    ImmutableArray<Vector2> RallyPoints);

public record WallSegment(Vector2 Start, Vector2 End, CastleLayer Layer);

public record GateDefinition(
    int Id,
    Vector2 Center,
    CastleLayer Layer,
    int Width = 3);

public record ZoneDefinition(
    string ZoneId,
    CastleLayer Layer,
    Vector2 Center,
    float Radius,
    Vector2 SpawnPoint);
