using System.Collections.Immutable;
using System.Numerics;
using System.Text;
using NavPathfinder.Demo.Data;
using NavPathfinder.Sdk;
using NavPathfinder.Sdk.Abstractions;
using NavPathfinder.Sdk.Integration;
using NavPathfinder.Sdk.Models;
using NavPathfinder.Sdk.Services;

namespace NavPathfinder.Demo.Simulation;

public static class SiegeWorld
{
    // Set by Initialize() before CreateAsync is called.
    public static int GridCols { get; private set; } = 120;
    public static int GridRows { get; private set; } = 28;
    public const  float TickDeltaSeconds = 0.016f;

    // Multi-layer castle layout — replaces old single-wall constants.
    public static CastleLayout Layout { get; private set; } = null!;

    // Backward-compatible wall accessors — derived from outer curtain walls in Layout.
    internal static int WallTop   => (int)Layout.Walls[0].Start.Y;
    internal static int WallBot   => (int)Layout.Walls[1].Start.Y;
    internal static int WallLeft  => (int)Layout.Walls[2].Start.X;
    internal static int WallRight => (int)Layout.Walls[3].Start.X;

    // Inner bailey wall accessors — walls[4..7]
    internal static int InnerTop   => (int)Layout.Walls[4].Start.Y;
    internal static int InnerBot   => (int)Layout.Walls[5].Start.Y;
    internal static int InnerLeft  => (int)Layout.Walls[6].Start.X;
    internal static int InnerRight => (int)Layout.Walls[7].Start.X;

    // Keep wall accessors — walls[8..11]
    internal static int KeepTop   => (int)Layout.Walls[8].Start.Y;
    internal static int KeepBot   => (int)Layout.Walls[9].Start.Y;
    internal static int KeepLeft  => (int)Layout.Walls[10].Start.X;
    internal static int KeepRight => (int)Layout.Walls[11].Start.X;

    internal static int GateHoriz => GridCols / 2;
    internal static int GateVert  => (WallTop + WallBot) / 2;

    public static ImmutableArray<string>  FortressLayout { get; private set; }
    public static ImmutableArray<Vector2> SafeZones      { get; private set; }
    public static ImmutableArray<GateState> InitialGates { get; private set; }

    static SiegeWorld() => Recompute();

    /// <summary>
    /// Call once at startup before <see cref="CreateAsync"/> to size the world
    /// to the terminal dimensions.
    /// </summary>
    public static void Initialize(int cols, int rows)
    {
        GridCols = Math.Max(60, cols);
        GridRows = Math.Max(20, rows);
        Recompute();
    }

    private static void Recompute()
    {
        Layout = CastleBuilder.Build(GridCols, GridRows);
        FortressLayout = BuildLayout();
        SafeZones = BuildSafeZones();
        InitialGates = CastleBuilder.CreateInitialGates(Layout);
    }

    /// <summary>
    /// Builds safe zones for backward compatibility: 4 courtyard positions + keep centre.
    /// </summary>
    private static ImmutableArray<Vector2> BuildSafeZones()
    {
        int inL = WallLeft  + 3,  inR = WallRight - 3;
        int inT = WallTop   + 3,  inB = WallBot   - 3;
        var keepCenter = Layout.KeepPositions[0];
        return ImmutableArray.Create(
            new Vector2(inL,  inB),
            new Vector2(inL,  inT),
            new Vector2(inR,  inB),
            new Vector2(inR,  inT),
            keepCenter);
    }

    public static async Task<(NavMeshHandle Mesh, SiegeDomainContext Domain)> CreateAsync(
        NavPathfinderContext ctx, CancellationToken ct = default, float targetFps = 15f)
    {
        var world        = ctx.World;
        var baker        = world.CreateNavMeshBakerService();
        var obstacleSvc  = world.CreateDynamicObstacleService();
        var scoutSvc     = world.CreateSingleAgentService("scouts");
        var influenceSvc = world.CreateInfluenceMapService("influence");
        var separationSvc = world.CreateSeparationService("separation");
        var engagementSvc = world.CreateEngagementService("engagement");

        var sim = NavSim.Create(world)
            .WithFrameBudget(1000.0 / targetFps)
            .AddPopulation("Invader",  maxCount: 10_000, computeWeight: 1.0f)
            .AddPopulation("Defender", maxCount: 5_000,  computeWeight: 2.0f)
            .AddPopulation("Civilian", maxCount: 2_000,  computeWeight: 0.5f)
            .Build();

        var mesh   = await BakeFortressMesh(baker, ct);
        var domain = new SiegeDomainContext(sim, obstacleSvc, scoutSvc, influenceSvc,
                                            separationSvc, engagementSvc);
        return (mesh, domain);
    }

    public static SiegeTickData CreateInitialState(NavMeshHandle mesh, SiegeBalanceConfig? config = null) =>
        new SiegeTickData(
            NavMesh:    mesh,
            TickNumber: 0,
            Invaders:   ImmutableArray<SimAgent>.Empty,
            Defenders:  ImmutableArray<SimAgent>.Empty,
            Civilians:  ImmutableArray<SimAgent>.Empty,
            Gates:      InitialGates,
            Castle:     Layout,
            Zones:      CastleBuilder.CreateInitialZones(Layout),
            Config:     config ?? SiegeBalanceConfig.Default,
            InvaderPool:  (config ?? SiegeBalanceConfig.Default).InvaderPool,
            DefenderPool: (config ?? SiegeBalanceConfig.Default).DefenderPool,
            CivilianPool: (config ?? SiegeBalanceConfig.Default).CivilianPool);

    public static SiegeTickData NextTickState(SiegeTickData completed) =>
        new SiegeTickData(
            NavMesh:    completed.NavMesh,
            TickNumber: completed.TickNumber + 1,
            Invaders:   completed.Invaders,
            Defenders:  completed.Defenders,
            Civilians:  completed.Civilians,
            Gates:      completed.Gates,
            Castle:     completed.Castle,
            Config:     completed.Config,
            // Spawn state carry-forward
            InvaderCredits:   completed.InvaderCredits,
            DefenderCredits:  completed.DefenderCredits,
            CivilianCredits:  completed.CivilianCredits,
            InvaderPool:      completed.InvaderPool,
            DefenderPool:     completed.DefenderPool,
            CivilianPool:     completed.CivilianPool,
            // Win & defence carry-forward
            WinState:          completed.WinState,
            BreachHoldTicks:   completed.BreachHoldTicks,
            GateHoldTicks:     completed.GateHoldTicks,
            ActiveDefenceLine: completed.ActiveDefenceLine,
            Zones:             completed.Zones,
            // Carry stale compute data
            ThreatMap:    completed.ThreatMap,
            ThreatLevel:  completed.ThreatLevel,
            DefenceMap:   completed.DefenceMap,
            BreachPaths:  completed.BreachPaths,
            // Performance metrics carry-forward for predictive spawn budgeting
            PathfindingMs:    completed.PathfindingMs,
            ActiveAgentCount: completed.ActiveAgentCount,
            CacheHitRate:     completed.CacheHitRate,
            // Combat carry-forward — deferred damage applied next tick
            PendingDamage:    completed.PendingDamage,
            // Carry render buffer (preserves FPS history ring buffer)
            RenderBuffer:     completed.RenderBuffer);

    // ── NavMesh ───────────────────────────────────────────────────────────────

    private static async Task<NavMeshHandle> BakeFortressMesh(
        INavMeshBakerService baker, CancellationToken ct)
    {
        int cols = GridCols, rows = GridRows;
        int vC = cols + 1, vR = rows + 1;

        var vertices = new List<Vector2>(vC * vR);
        for (int r = 0; r <= rows; r++)
            for (int c = 0; c <= cols; c++)
                vertices.Add(new Vector2(c, r));

        var triangles = new List<(int, int, int)>(cols * rows * 2);
        for (int r = 0; r < rows; r++)
            for (int c = 0; c < cols; c++)
            {
                int bl = r * vC + c, br = r * vC + c + 1;
                int tl = (r+1) * vC + c, tr = (r+1) * vC + c + 1;
                triangles.Add((bl, br, tl));
                triangles.Add((br, tr, tl));
            }

        var blocked = new List<IReadOnlyList<Vector2>>();
        for (int r = 0; r < rows; r++)
            for (int c = 0; c < cols; c++)
                if (IsWall(c, r))
                    blocked.Add(new[]
                    {
                        new Vector2(c,   r),   new Vector2(c+1, r),
                        new Vector2(c+1, r+1), new Vector2(c,   r+1)
                    });

        return await baker.BakeAsync(vertices, triangles, blocked, ct);
    }

    /// <summary>
    /// Cell-level wall test — delegates to CastleBuilder for multi-layer support.
    /// </summary>
    public static bool IsWall(int c, int r) => CastleBuilder.IsWall(Layout, c, r);

    /// <summary>
    /// Bresenham line-of-sight check. Returns false if any wall cell lies between from and to.
    /// Gate cells block LOS when the gate is Locked or UnderAttack; open/breached gates allow LOS.
    /// </summary>
    public static bool HasLineOfSight(Vector2 from, Vector2 to,
        CastleLayout layout, ImmutableArray<GateState> gates)
    {
        int x0 = (int)from.X, y0 = (int)from.Y;
        int x1 = (int)to.X,   y1 = (int)to.Y;

        int dx = Math.Abs(x1 - x0), dy = Math.Abs(y1 - y0);
        int sx = x0 < x1 ? 1 : -1, sy = y0 < y1 ? 1 : -1;
        int err = dx - dy;

        while (true)
        {
            // Skip the start and end cells (agents standing there)
            if ((x0 != (int)from.X || y0 != (int)from.Y) &&
                (x0 != (int)to.X   || y0 != (int)to.Y))
            {
                if (IsWallForLos(layout, gates, x0, y0))
                    return false;
            }

            if (x0 == x1 && y0 == y1) break;
            int e2 = 2 * err;
            if (e2 > -dy) { err -= dy; x0 += sx; }
            if (e2 <  dx) { err += dx; y0 += sy; }
        }
        return true;
    }

    /// <summary>
    /// Wall check for LOS — same as IsWall but closed gates also block.
    /// </summary>
    private static bool IsWallForLos(CastleLayout layout, ImmutableArray<GateState> gates,
        int col, int row)
    {
        foreach (var wall in layout.Walls)
        {
            int sx = (int)wall.Start.X, sy = (int)wall.Start.Y;
            int ex = (int)wall.End.X,   ey = (int)wall.End.Y;

            bool onWall = false;
            if (sy == ey && row == sy && col >= Math.Min(sx, ex) && col <= Math.Max(sx, ex))
                onWall = true;
            if (sx == ex && col == sx && row >= Math.Min(sy, ey) && row <= Math.Max(sy, ey))
                onWall = true;

            if (onWall)
            {
                // Check if this is a gate opening AND the gate is passable
                if (IsGateOpenForLos(layout, gates, col, row))
                    continue; // open/breached gate — LOS passes
                return true;  // solid wall or closed gate
            }
        }
        return false;
    }

    private static bool IsGateOpenForLos(CastleLayout layout, ImmutableArray<GateState> gates,
        int col, int row)
    {
        foreach (var gateDef in layout.GateDefinitions)
        {
            int halfW = gateDef.Width / 2;
            int gx = (int)gateDef.Center.X, gy = (int)gateDef.Center.Y;
            bool atGate = (row == gy && Math.Abs(col - gx) <= halfW) ||
                          (col == gx && Math.Abs(row - gy) <= halfW);
            if (!atGate) continue;

            // Find runtime gate state
            foreach (var gs in gates)
            {
                if (gs.Id == gateDef.Id)
                    return gs.Status is GateStatus.Open or GateStatus.Breached;
            }
            return true; // no state found — assume open
        }
        return false; // not a gate position
    }

    private static ImmutableArray<string> BuildLayout()
    {
        var rows = new string[GridRows];
        for (int r = 0; r < GridRows; r++)
        {
            var sb = new StringBuilder(GridCols);
            for (int c = 0; c < GridCols; c++)
                sb.Append(IsWall(c, r) ? '#' : '.');
            rows[r] = sb.ToString();
        }
        return ImmutableArray.Create(rows);
    }
}
