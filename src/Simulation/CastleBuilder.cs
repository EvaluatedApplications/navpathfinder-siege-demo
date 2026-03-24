using System.Collections.Immutable;
using System.Numerics;
using NavPathfinder.Demo.Data;

namespace NavPathfinder.Demo.Simulation;

/// <summary>
/// Procedurally generates a multi-layer concentric castle layout sized to the terminal.
/// </summary>
public static class CastleBuilder
{
    public static CastleLayout Build(int cols, int rows)
    {
        // Outer curtain — takes ~85% of grid, centred
        int outerTop    = Math.Max(2, rows / 9);
        int outerBot    = rows - outerTop - 1;
        int outerLeft   = Math.Max(4, cols / 25);
        int outerRight  = cols - outerLeft - 1;

        // Inner bailey — ~50% of grid, centred
        int innerMarginH = (outerRight - outerLeft) / 5;
        int innerMarginV = (outerBot - outerTop) / 4;
        int innerLeft    = outerLeft + innerMarginH;
        int innerRight   = outerRight - innerMarginH;
        int innerTop     = outerTop + innerMarginV;
        int innerBot     = outerBot - innerMarginV;

        // Keep — small central area
        int keepMarginH  = (innerRight - innerLeft) / 3;
        int keepMarginV  = (innerBot - innerTop) / 3;
        int keepLeft     = innerLeft + keepMarginH;
        int keepRight    = innerRight - keepMarginH;
        int keepTop      = innerTop + keepMarginV;
        int keepBot      = innerBot - keepMarginV;

        int midCol = cols / 2;

        // ── Walls ────────────────────────────────────────────────────
        var walls = ImmutableArray.CreateBuilder<WallSegment>();

        // Outer curtain (4 walls)
        walls.Add(new(new(outerLeft, outerTop), new(outerRight, outerTop), CastleLayer.OuterCurtain));   // N
        walls.Add(new(new(outerLeft, outerBot), new(outerRight, outerBot), CastleLayer.OuterCurtain));   // S
        walls.Add(new(new(outerLeft, outerTop), new(outerLeft, outerBot), CastleLayer.OuterCurtain));    // W
        walls.Add(new(new(outerRight, outerTop), new(outerRight, outerBot), CastleLayer.OuterCurtain));  // E

        // Inner bailey (4 walls)
        walls.Add(new(new(innerLeft, innerTop), new(innerRight, innerTop), CastleLayer.InnerBailey));
        walls.Add(new(new(innerLeft, innerBot), new(innerRight, innerBot), CastleLayer.InnerBailey));
        walls.Add(new(new(innerLeft, innerTop), new(innerLeft, innerBot), CastleLayer.InnerBailey));
        walls.Add(new(new(innerRight, innerTop), new(innerRight, innerBot), CastleLayer.InnerBailey));

        // Keep (4 walls)
        walls.Add(new(new(keepLeft, keepTop), new(keepRight, keepTop), CastleLayer.Keep));
        walls.Add(new(new(keepLeft, keepBot), new(keepRight, keepBot), CastleLayer.Keep));
        walls.Add(new(new(keepLeft, keepTop), new(keepLeft, keepBot), CastleLayer.Keep));
        walls.Add(new(new(keepRight, keepTop), new(keepRight, keepBot), CastleLayer.Keep));

        // ── Gates ────────────────────────────────────────────────────
        // 6 outer gates: N-left, N-right, S-left, S-right, W, E
        int outerThird = (outerRight - outerLeft) / 3;
        int outerMidV  = (outerTop + outerBot) / 2;
        var gates = ImmutableArray.Create(
            new GateDefinition(0, new(outerLeft + outerThird, outerTop),     CastleLayer.OuterCurtain),  // N-left
            new GateDefinition(1, new(outerRight - outerThird, outerTop),    CastleLayer.OuterCurtain),  // N-right
            new GateDefinition(2, new(outerLeft + outerThird, outerBot),     CastleLayer.OuterCurtain),  // S-left
            new GateDefinition(3, new(outerRight - outerThird, outerBot),    CastleLayer.OuterCurtain),  // S-right
            new GateDefinition(4, new(outerLeft, outerMidV),                 CastleLayer.OuterCurtain),  // W
            new GateDefinition(5, new(outerRight, outerMidV),                CastleLayer.OuterCurtain),  // E
            // 2 inner gates: N, S
            new GateDefinition(6, new(midCol, innerTop),                     CastleLayer.InnerBailey),   // Inner N
            new GateDefinition(7, new(midCol, innerBot),                     CastleLayer.InnerBailey));  // Inner S

        // ── Zones ────────────────────────────────────────────────────
        // 4 outer courtyards (between outer and inner walls) + 1 inner courtyard
        int innerMidC = (innerLeft + innerRight) / 2;
        int innerMidR = (innerTop + innerBot) / 2;
        float outerZoneR = Math.Min(innerMarginH, innerMarginV) * 0.8f;
        float innerZoneR = Math.Min(keepMarginH, keepMarginV) * 0.8f;

        var zones = ImmutableArray.Create(
            new ZoneDefinition("outer-nw", CastleLayer.OuterCurtain,
                new((outerLeft + innerLeft) / 2f, (outerTop + innerTop) / 2f), outerZoneR,
                new((outerLeft + innerLeft) / 2f, (outerTop + innerTop) / 2f)),
            new ZoneDefinition("outer-ne", CastleLayer.OuterCurtain,
                new((innerRight + outerRight) / 2f, (outerTop + innerTop) / 2f), outerZoneR,
                new((innerRight + outerRight) / 2f, (outerTop + innerTop) / 2f)),
            new ZoneDefinition("outer-sw", CastleLayer.OuterCurtain,
                new((outerLeft + innerLeft) / 2f, (innerBot + outerBot) / 2f), outerZoneR,
                new((outerLeft + innerLeft) / 2f, (innerBot + outerBot) / 2f)),
            new ZoneDefinition("outer-se", CastleLayer.OuterCurtain,
                new((innerRight + outerRight) / 2f, (innerBot + outerBot) / 2f), outerZoneR,
                new((innerRight + outerRight) / 2f, (innerBot + outerBot) / 2f)),
            new ZoneDefinition("inner-court", CastleLayer.InnerBailey,
                new(innerMidC, innerMidR), innerZoneR,
                new(innerMidC, innerMidR)));

        // ── Keep positions (centre of the keep) ──────────────────────
        var keepPositions = ImmutableArray.Create(
            new Vector2((keepLeft + keepRight) / 2f, (keepTop + keepBot) / 2f));

        // ── Rally points outside outer gates ─────────────────────────
        float rallyDist = 6f;
        var rallyPoints = ImmutableArray.Create(
            new Vector2(outerLeft + outerThird, outerTop - rallyDist),      // N-left rally
            new Vector2(outerRight - outerThird, outerTop - rallyDist),     // N-right rally
            new Vector2(outerLeft + outerThird, outerBot + rallyDist),      // S-left rally
            new Vector2(outerRight - outerThird, outerBot + rallyDist),     // S-right rally
            new Vector2(outerLeft - rallyDist, outerMidV),                  // W rally
            new Vector2(outerRight + rallyDist, outerMidV));                // E rally

        return new CastleLayout(cols, rows, walls.ToImmutable(), gates, zones, keepPositions, rallyPoints);
    }

    /// <summary>
    /// Returns initial gate states from the castle layout — all gates locked.
    /// </summary>
    public static ImmutableArray<GateState> CreateInitialGates(CastleLayout layout)
    {
        var builder = ImmutableArray.CreateBuilder<GateState>(layout.GateDefinitions.Length);
        foreach (var def in layout.GateDefinitions)
            builder.Add(new GateState(def.Id, def.Center, GateStatus.Locked, def.Layer, 0, 0));
        return builder.MoveToImmutable();
    }

    /// <summary>
    /// Returns initial zone control — all zones unowned.
    /// </summary>
    public static ImmutableArray<ZoneControl> CreateInitialZones(CastleLayout layout)
    {
        var builder = ImmutableArray.CreateBuilder<ZoneControl>(layout.ZoneDefinitions.Length);
        foreach (var def in layout.ZoneDefinitions)
            builder.Add(new ZoneControl(def.ZoneId, def.Layer, def.Center, def.Radius));
        return builder.MoveToImmutable();
    }

    /// <summary>
    /// Cell-level wall test for NavMesh baking and rendering.
    /// Returns true if the cell at (col, row) is a wall.
    /// </summary>
    public static bool IsWall(CastleLayout layout, int col, int row)
    {
        foreach (var wall in layout.Walls)
        {
            int sx = (int)wall.Start.X, sy = (int)wall.Start.Y;
            int ex = (int)wall.End.X,   ey = (int)wall.End.Y;

            // Horizontal wall
            if (sy == ey && row == sy && col >= Math.Min(sx, ex) && col <= Math.Max(sx, ex))
            {
                if (!IsGateOpening(layout, col, row))
                    return true;
            }
            // Vertical wall
            if (sx == ex && col == sx && row >= Math.Min(sy, ey) && row <= Math.Max(sy, ey))
            {
                if (!IsGateOpening(layout, col, row))
                    return true;
            }
        }
        return false;
    }

    private static bool IsGateOpening(CastleLayout layout, int col, int row)
    {
        foreach (var gate in layout.GateDefinitions)
        {
            int halfW = gate.Width / 2;
            int gx = (int)gate.Center.X, gy = (int)gate.Center.Y;
            if (row == gy && Math.Abs(col - gx) <= halfW) return true;
            if (col == gx && Math.Abs(row - gy) <= halfW) return true;
        }
        return false;
    }
}
