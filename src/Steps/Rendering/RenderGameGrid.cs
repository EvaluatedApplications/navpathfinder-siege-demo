using System.Collections.Immutable;
using System.Numerics;
using EvalApp.Consumer;

using NavPathfinder.Demo.Data;
using NavPathfinder.Demo.Rendering;
using NavPathfinder.Demo.Simulation;

namespace NavPathfinder.Demo.Steps.Rendering;

/// <summary>
/// Step 2: Render the game grid — walls, agents, gates, breach corridors, and floor zones.
/// Writes directly to the RenderBuffer's StringBuilder output.
/// </summary>
public sealed class RenderGameGrid : PureStep<SiegeTickData>
{
    public override SiegeTickData Execute(SiegeTickData data)
    {
        var buf = data.RenderBuffer!;
        var sb = buf.StringBuilder;
        int rows = SiegeWorld.GridRows, cols = SiegeWorld.GridCols;

        var gates = data.Gates;
        var layout = data.Castle ?? SiegeWorld.Layout;
        var zones = data.Zones;

        var gateCells = BuildGateCells(gates, layout, rows, cols);
        var breachCells = BuildBreachCells(data, rows, cols);

        for (int r = 0; r < rows; r++)
        {
            string prevColor = "";
            for (int c = 0; c < cols; c++)
            {
                int inv = buf.InvCount[r, c], def = buf.DefCount[r, c], civ = buf.CivCount[r, c];
                int total = inv + def + civ;

                string fg; char ch;

                if (gateCells.TryGetValue((r, c), out var gApp))
                {
                    fg = gApp.Color; ch = gApp.Ch;
                }
                else if (total > 0)
                {
                    (fg, ch) = AgentCell(inv, def, civ, total, r, c, buf, zones);
                }
                else if (CastleBuilder.IsWall(layout, c, r))
                {
                    (fg, ch) = WallCell(c, r, layout, cols, rows);
                }
                else if (breachCells.Contains((r, c)))
                {
                    fg = ColorPalette.TerrainBackground(c, r) + ColorPalette.BreachPathFg; ch = '·';
                }
                else
                {
                    (fg, ch) = FloorCell(r, c, zones);
                }

                if (fg != prevColor)
                {
                    if (prevColor != "") sb.Append(ColorPalette.Reset);
                    if (fg != "") sb.Append(fg);
                    prevColor = fg;
                }
                sb.Append(ch);
            }
            if (prevColor != "") sb.Append(ColorPalette.Reset);
            sb.Append(ColorPalette.Eol);
            sb.AppendLine();
        }

        return data;
    }

    private static (string fg, char ch) AgentCell(int inv, int def, int civ, int total,
        int row, int col, RenderBuffer buf, ImmutableArray<ZoneControl> zones)
    {
        string bg = ColorPalette.DensityBackground(total);
        if (bg == "")
            bg = ColorPalette.TerrainBackground(col, row);

        // Leader override — highest-level agent gets a crown glyph
        bool isInvLeader = buf.InvLeaderCell == (row, col);
        bool isDefLeader = buf.DefLeaderCell == (row, col);

        char ch;
        string fg;
        if (isInvLeader)
        {
            ch = '★';
            fg = ColorPalette.InvColor;
        }
        else if (isDefLeader)
        {
            ch = '★';
            fg = ColorPalette.DefColor;
        }
        else
        {
            ch = GlyphSets.AgentGlyph(inv, def, civ);
            bool invDom = inv >= def && inv >= civ;
            bool defDom = def > inv  && def >= civ;
            BehaviourState state = invDom ? buf.InvDomState[row, col]
                                 : defDom ? buf.DefDomState[row, col]
                                 : buf.CivDomState[row, col];
            fg = ColorPalette.StateColor(state);
        }

        return (bg + fg, ch);
    }

    private static (string fg, char ch) WallCell(int c, int r, CastleLayout layout, int cols, int rows)
    {
        var layer = FindWallLayer(c, r, layout);
        bool hasH = IsWallAt(c - 1, r, layout, cols, rows) || IsWallAt(c + 1, r, layout, cols, rows);
        bool hasV = IsWallAt(c, r - 1, layout, cols, rows) || IsWallAt(c, r + 1, layout, cols, rows);
        string wallBg = ColorPalette.WallBackground(layer);
        return (wallBg + ColorPalette.WallColor(layer), GlyphSets.WallChar(layer, hasH, hasV));
    }

    private static CastleLayer FindWallLayer(int c, int r, CastleLayout layout)
    {
        foreach (var wall in layout.Walls)
        {
            int sx = (int)wall.Start.X, sy = (int)wall.Start.Y;
            int ex = (int)wall.End.X,   ey = (int)wall.End.Y;
            if (sy == ey && r == sy && c >= Math.Min(sx, ex) && c <= Math.Max(sx, ex))
                return wall.Layer;
            if (sx == ex && c == sx && r >= Math.Min(sy, ey) && r <= Math.Max(sy, ey))
                return wall.Layer;
        }
        return CastleLayer.OuterCurtain;
    }

    private static bool IsWallAt(int c, int r, CastleLayout layout, int cols, int rows)
    {
        if (c < 0 || c >= cols || r < 0 || r >= rows) return false;
        return CastleBuilder.IsWall(layout, c, r);
    }

    private static (string fg, char ch) FloorCell(int r, int c, ImmutableArray<ZoneControl> zones)
    {
        string terrainBg = ColorPalette.TerrainBackground(c, r);

        if (!zones.IsDefaultOrEmpty)
        {
            foreach (var z in zones)
            {
                if (Vector2.DistanceSquared(new(c, r), z.Center) < z.Radius * z.Radius)
                {
                    if (z.Owner == AgentRole.Invader)
                        return (ColorPalette.BgInvZone, ' ');
                    if (z.ContestPercent > 0.25f)
                        return (ColorPalette.BgContest, ' ');
                }
            }
        }
        return (terrainBg, ' ');
    }

    private struct GateAppearance { public string Color; public char Ch; }

    private static Dictionary<(int r, int c), GateAppearance> BuildGateCells(
        ImmutableArray<GateState> gates, CastleLayout layout, int rows, int cols)
    {
        var result = new Dictionary<(int, int), GateAppearance>(gates.Length * 3);
        foreach (var gate in gates)
        {
            var (color, ch) = GateHeatAppearance(gate);
            int gc = (int)gate.Center.X, gr = (int)gate.Center.Y;
            bool isHoriz = IsHorizontalGate(gr, layout);
            for (int offset = -1; offset <= 1; offset++)
            {
                int rr = isHoriz ? gr : gr + offset;
                int cc = isHoriz ? gc + offset : gc;
                if (rr >= 0 && rr < rows && cc >= 0 && cc < cols)
                    result[(rr, cc)] = new GateAppearance { Color = color, Ch = ch };
            }
        }
        return result;
    }

    private static bool IsHorizontalGate(int gr, CastleLayout layout)
    {
        foreach (var wall in layout.Walls)
        {
            int sy = (int)wall.Start.Y, ey = (int)wall.End.Y;
            if (sy == ey && gr == sy) return true;
        }
        return false;
    }

    private static (string color, char ch) GateHeatAppearance(GateState gate)
    {
        const int breachTicks = 90;
        return gate.Status switch
        {
            GateStatus.Open => ("\u001b[38;5;240m", '·'),
            GateStatus.Locked => LerpGateColor(gate.TicksContested / 15f, 30, 40, 226, '╬'),
            GateStatus.UnderAttack => LerpGateColor(
                gate.TicksUnderAttack / (float)breachTicks, 208, 196, 196, '▓'),
            GateStatus.Breached => ("\u001b[38;5;201m\u001b[48;5;231m", '░'),
            _ => ("\u001b[38;5;240m", '·')
        };
    }

    private static (string color, char ch) LerpGateColor(float t, int cold, int mid, int hot, char ch)
    {
        t = Math.Clamp(t, 0f, 1f);
        int bg = t < 0.5f ? cold : (t < 0.8f ? mid : hot);
        return ($"\u001b[38;5;{bg}m", ch);
    }

    private static HashSet<(int, int)> BuildBreachCells(SiegeTickData data, int rows, int cols)
    {
        var set = new HashSet<(int, int)>();
        if (!data.BreachPaths.HasValue) return set;
        foreach (var path in data.BreachPaths.Value)
            foreach (var wp in path)
            {
                int pr = (int)wp.Y, pc = (int)wp.X;
                if (pr >= 0 && pr < rows && pc >= 0 && pc < cols)
                    set.Add((pr, pc));
            }
        return set;
    }
}
