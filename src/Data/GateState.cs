using System.Numerics;

namespace NavPathfinder.Demo.Data;

public record GateState(
    int Id,
    Vector2 Center,
    GateStatus Status,
    CastleLayer Layer,
    int TicksContested,
    int TicksUnderAttack,
    float Health = 1f);

public record GateStateChange(int GateId, GateStatus From, GateStatus To);

public enum GateStatus { Open, Locked, UnderAttack, Breached }
