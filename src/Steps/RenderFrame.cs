using System.Collections.Immutable;
using System.Numerics;
using System.Text;
using EvalApp.Consumer;

using NavPathfinder.Demo.Data;
using NavPathfinder.Demo.Simulation;
using NavPathfinder.Sdk.Models;

namespace NavPathfinder.Demo.Steps;

public sealed class RenderFrame : PureStep<SiegeTickData>
{
    // ── ANSI constants ────────────────────────────────────────────────────────
    private const string Reset      = "\u001b[0m";
    private const string Eol        = "\u001b[K";
    private const string DimGray    = "\u001b[90m";
    private const string BoldWhite  = "\u001b[1;97m";
    private const string White      = "\u001b[97m";
    private const string BoldRed    = "\u001b[1;31m";
    private const string BoldGreen  = "\u001b[1;32m";
    private const string BoldYellow = "\u001b[1;33m";
    private const string BoldCyan   = "\u001b[1;36m";
    private const string BoldMagenta= "\u001b[1;35m";
    private const string Green      = "\u001b[32m";
    private const string Cyan       = "\u001b[36m";
    private const string Yellow     = "\u001b[33m";
    private const string Red        = "\u001b[31m";
    private const string Magenta    = "\u001b[35m";

    // 256-color wall palettes per layer
    private const string OuterWallFg = "\u001b[38;5;137m";  // sandstone
    private const string InnerWallFg = "\u001b[38;5;249m";  // silver
    private const string KeepWallFg  = "\u001b[38;5;220m";  // gold

    // Braille density ramp: ⠁ ⠃ ⠇ ⡇ ⣇ ⣧ ⣷ ⣿
    private static readonly char[] Braille = [' ', '⠁', '⠃', '⠇', '⡇', '⣇', '⣧', '⣷', '⣿'];
    // Sparkline chars
    private static readonly char[] Spark = ['▁', '▂', '▃', '▄', '▅', '▆', '▇', '█'];

    private static int GridCols   => SiegeWorld.GridCols;
    private static int GridRows   => SiegeWorld.GridRows;
    private static int PanelWidth => SiegeWorld.GridCols;

    // ── Cached per-frame arrays ───────────────────────────────────────────────
    private static int[,]  _invCount  = new int[0, 0];
    private static int[,]  _defCount  = new int[0, 0];
    private static int[,]  _civCount  = new int[0, 0];
    private static BehaviourState[,] _domState = new BehaviourState[0, 0];
    private static readonly StringBuilder _sb = new(1 << 17);

    // Sparkline history (circular buffers)
    private const int HistLen = 30;
    private static readonly float[] _fpsHist = new float[HistLen];
    private static int _histIdx;

    private static void EnsureArrays()
    {
        int rows = GridRows, cols = GridCols;
        if (_invCount.GetLength(0) == rows && _invCount.GetLength(1) == cols) return;
        _invCount = new int[rows, cols];
        _defCount = new int[rows, cols];
        _civCount = new int[rows, cols];
        _domState = new BehaviourState[rows, cols];
    }

    public override SiegeTickData Execute(SiegeTickData data)
    {
        EnsureArrays();
        UpdateHistory(data);
        _sb.Clear();

        AppendHeader(_sb);
        AppendGrid(_sb, data);
        AppendMetrics(_sb, data);
        AppendPopulations(_sb, data);
        AppendZones(_sb, data);
        AppendBehaviours(_sb, data);
        AppendDefenceAndWin(_sb, data);
        AppendLegend(_sb);

        return data with { RenderedFrame = _sb.ToString() };
    }

    private static void UpdateHistory(SiegeTickData data)
    {
        _fpsHist[_histIdx % HistLen] = (float)data.Fps;
        _histIdx++;
    }

    // ── Header ────────────────────────────────────────────────────────────────

    private static void AppendHeader(StringBuilder sb)
    {
        string sep = new string('━', PanelWidth);
        sb.Append(DimGray); sb.Append(sep); sb.Append(Eol); sb.Append(Reset); sb.AppendLine();

        string title = "⚔  NavPathfinder SDK · Castle Siege  ⚔";
        sb.Append(BoldYellow); sb.Append(Pad(title, PanelWidth)); sb.Append(Eol); sb.Append(Reset); sb.AppendLine();

        sb.Append(DimGray); sb.Append(sep); sb.Append(Eol); sb.Append(Reset); sb.AppendLine();

        string desc = "Multi-layer concentric castle. 14 purposeful AI behaviours. All pathfinding via SDK.";
        sb.Append(DimGray); sb.Append(Pad(desc, PanelWidth)); sb.Append(Eol); sb.Append(Reset); sb.AppendLine();

        sb.Append(DimGray); sb.Append(sep); sb.Append(Eol); sb.Append(Reset); sb.AppendLine();
    }

    // ── Grid ──────────────────────────────────────────────────────────────────

    private static void AppendGrid(StringBuilder sb, SiegeTickData data)
    {
        Array.Clear(_invCount);
        Array.Clear(_defCount);
        Array.Clear(_civCount);
        Array.Clear(_domState);
        CountAgents(_invCount, data.Invaders);
        CountAgents(_defCount, data.Defenders);
        CountAgents(_civCount, data.Civilians);
        TrackDominantState(data.Invaders);
        TrackDominantState(data.Defenders);
        TrackDominantState(data.Civilians);

        var gates = data.Gates;
        var layout = data.Castle ?? SiegeWorld.Layout;

        // Pre-compute gate cell set for quick lookup
        var gateCells = BuildGateCells(gates, layout);

        // Pre-compute breach corridor cells
        var breachCells = BuildBreachCells(data);

        // Pre-compute zone info per cell (for background tinting)
        var zones = data.Zones;

        for (int r = 0; r < GridRows; r++)
        {
            string prevColor = "";
            for (int c = 0; c < GridCols; c++)
            {
                int inv = _invCount[r, c], def = _defCount[r, c], civ = _civCount[r, c];
                int total = inv + def + civ;

                string fg; char ch;

                if (gateCells.TryGetValue((r, c), out var gApp))
                {
                    fg = gApp.Color; ch = gApp.Ch;
                }
                else if (total > 0)
                {
                    (fg, ch) = AgentCell(inv, def, civ, total, r, c, zones);
                }
                else if (CastleBuilder.IsWall(layout, c, r))
                {
                    (fg, ch) = WallCell(c, r, layout);
                }
                else if (breachCells.Contains((r, c)))
                {
                    fg = "\u001b[38;5;201m"; ch = '·';
                }
                else
                {
                    (fg, ch) = FloorCell(r, c, zones);
                }

                if (fg != prevColor)
                {
                    if (prevColor != "") sb.Append(Reset);
                    if (fg != "") sb.Append(fg);
                    prevColor = fg;
                }
                sb.Append(ch);
            }
            if (prevColor != "") sb.Append(Reset);
            sb.Append(Eol);
            sb.AppendLine();
        }
    }

    private static (string fg, char ch) WallCell(int c, int r, CastleLayout layout)
    {
        var layer = FindWallLayer(c, r, layout);
        bool hasH = IsWallAt(c - 1, r, layout) || IsWallAt(c + 1, r, layout);
        bool hasV = IsWallAt(c, r - 1, layout) || IsWallAt(c, r + 1, layout);

        char ch = layer switch
        {
            CastleLayer.OuterCurtain => (hasH, hasV) switch
            {
                (true, true)   => '╬',
                (true, false)  => '═',
                (false, true)  => '║',
                _              => '█'
            },
            CastleLayer.InnerBailey => (hasH, hasV) switch
            {
                (true, true)   => '┼',
                (true, false)  => '─',
                (false, true)  => '│',
                _              => '▪'
            },
            CastleLayer.Keep => (hasH, hasV) switch
            {
                (true, true)   => '╋',
                (true, false)  => '━',
                (false, true)  => '┃',
                _              => '◆'
            },
            _ => '█'
        };

        string fg = layer switch
        {
            CastleLayer.OuterCurtain => OuterWallFg,
            CastleLayer.InnerBailey  => InnerWallFg,
            CastleLayer.Keep         => KeepWallFg,
            _                        => DimGray
        };

        return (fg, ch);
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

    private static bool IsWallAt(int c, int r, CastleLayout layout)
    {
        if (c < 0 || c >= GridCols || r < 0 || r >= GridRows) return false;
        return CastleBuilder.IsWall(layout, c, r);
    }

    // Faction-specific density ramps: char identifies faction, density shown by glyph weight
    // Invaders: sword/arrow theme      ·  ×  †  ‡  ⁂  ⁂
    private static readonly char[] InvGlyphs = [' ', '·', '×', '†', '‡', '⁂', '⁂', '⁂', '⁂'];
    // Defenders: shield theme          ·  +  ‡  #  ⌂  ⌂
    private static readonly char[] DefGlyphs = [' ', '·', '+', '‡', '#', '⌂', '⌂', '⌂', '⌂'];
    // Civilians: dot theme             ·  ∘  ○  ●  ●  ●
    private static readonly char[] CivGlyphs = [' ', '·', '∘', '○', '●', '●', '●', '●', '●'];

    private static (string fg, char ch) AgentCell(int inv, int def, int civ, int total,
        int row, int col, ImmutableArray<ZoneControl> zones)
    {
        string bg = total switch
        {
            >= 10 => "\u001b[48;5;241m",
            >= 6  => "\u001b[48;5;238m",
            >= 3  => "\u001b[48;5;235m",
            _     => ""
        };

        // Character from dominant faction; color from dominant AI state
        bool invDom = inv >= def && inv >= civ;
        bool defDom = def > inv  && def >= civ;

        int density;
        char ch;
        if (invDom)
        {
            density = Math.Clamp(inv, 0, 8);
            ch = InvGlyphs[density];
        }
        else if (defDom)
        {
            density = Math.Clamp(def, 0, 8);
            ch = DefGlyphs[density];
        }
        else
        {
            density = Math.Clamp(civ, 0, 8);
            ch = CivGlyphs[density];
        }

        string fg = StateColor(_domState[row, col]);
        return (bg + fg, ch);
    }

    /// <summary>Maps each BehaviourState to a distinct ANSI 256-color.</summary>
    private static string StateColor(BehaviourState state) => state switch
    {
        // Invader states — warm spectrum (reds, oranges, magentas)
        BehaviourState.Mustering => "\u001b[38;5;131m",  // muted red — gathering (visible on dark bg)
        BehaviourState.Advancing => "\u001b[38;5;196m",  // bright red — on the move
        BehaviourState.Sieging   => "\u001b[38;5;208m",  // orange — at gate
        BehaviourState.Breaching => "\u001b[38;5;201m",  // magenta — breaking through
        BehaviourState.Claiming  => "\u001b[38;5;214m",  // gold — occupying zone
        BehaviourState.Pushing   => "\u001b[38;5;160m",  // medium red — next layer

        // Defender states — cool spectrum (greens, cyans, blues)
        BehaviourState.Holding      => "\u001b[38;5;34m",   // green — standard defense
        BehaviourState.Reinforcing  => "\u001b[38;5;45m",   // cyan — moving to help
        BehaviourState.Fighting     => "\u001b[38;5;46m",   // lime — active combat
        BehaviourState.FallingBack  => "\u001b[38;5;69m",   // blue — retreating
        BehaviourState.LastStand    => "\u001b[38;5;231m",  // white — final defense

        // Civilian states — yellows, muted
        BehaviourState.Sheltering => "\u001b[38;5;228m",  // pale yellow — moving to safety
        BehaviourState.Fleeing    => "\u001b[38;5;226m",  // bright yellow — running
        BehaviourState.Hidden     => "\u001b[38;5;101m",  // dim olive — safe, low vis

        _ => "\u001b[38;5;244m"  // gray fallback
    };

    private static (string fg, char ch) FloorCell(int r, int c, ImmutableArray<ZoneControl> zones)
    {
        if (!zones.IsDefaultOrEmpty)
        {
            foreach (var z in zones)
            {
                if (Vector2.DistanceSquared(new(c, r), z.Center) < z.Radius * z.Radius)
                {
                    if (z.Owner == AgentRole.Invader)
                        return ("\u001b[48;5;52m", ' ');
                    if (z.ContestPercent > 0.25f)
                        return ("\u001b[48;5;58m", ' ');
                }
            }
        }
        return ("", ' ');
    }

    private struct GateAppearance { public string Color; public char Ch; }

    private static Dictionary<(int r, int c), GateAppearance> BuildGateCells(
        ImmutableArray<GateState> gates, CastleLayout layout)
    {
        var result = new Dictionary<(int, int), GateAppearance>(gates.Length * 3);
        foreach (var gate in gates)
        {
            var (color, ch) = GateHeatAppearance(gate);
            int gc = (int)gate.Center.X, gr = (int)gate.Center.Y;

            // Determine orientation from wall segment
            bool isHoriz = IsHorizontalGate(gc, gr, layout);
            for (int offset = -1; offset <= 1; offset++)
            {
                int rr = isHoriz ? gr : gr + offset;
                int cc = isHoriz ? gc + offset : gc;
                if (rr >= 0 && rr < GridRows && cc >= 0 && cc < GridCols)
                    result[(rr, cc)] = new GateAppearance { Color = color, Ch = ch };
            }
        }
        return result;
    }

    private static bool IsHorizontalGate(int gc, int gr, CastleLayout layout)
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
        int breachTicks = 90;
        return gate.Status switch
        {
            GateStatus.Open => ("\u001b[38;5;240m", '·'),

            GateStatus.Locked => LerpGateColor(gate.TicksContested / 15f,
                30, 40, 226, '╬'),

            GateStatus.UnderAttack => LerpGateColor(
                gate.TicksUnderAttack / (float)breachTicks,
                208, 196, 196, '▓'),

            GateStatus.Breached => (_histIdx % 30 < 15
                ? "\u001b[38;5;231m\u001b[48;5;201m"
                : "\u001b[38;5;201m\u001b[48;5;231m", '░'),

            _ => ("\u001b[38;5;240m", '·')
        };
    }

    private static (string color, char ch) LerpGateColor(float t, int cold, int mid, int hot, char ch)
    {
        t = Math.Clamp(t, 0f, 1f);
        int bg = t < 0.5f ? cold : (t < 0.8f ? mid : hot);
        return ($"\u001b[38;5;{bg}m", ch);
    }

    private static HashSet<(int, int)> BuildBreachCells(SiegeTickData data)
    {
        var set = new HashSet<(int, int)>();
        if (!data.BreachPaths.HasValue) return set;
        foreach (var path in data.BreachPaths.Value)
            foreach (var wp in path)
            {
                int pr = (int)wp.Y, pc = (int)wp.X;
                if (pr >= 0 && pr < GridRows && pc >= 0 && pc < GridCols)
                    set.Add((pr, pc));
            }
        return set;
    }

    // ── Metrics Panel ─────────────────────────────────────────────────────────

    private static void AppendMetrics(StringBuilder sb, SiegeTickData data)
    {
        string sep = new string('━', PanelWidth);
        sb.Append(DimGray); sb.Append(sep); sb.Append(Eol); sb.Append(Reset); sb.AppendLine();

        // Row 1 — FPS / Tick / Pipe ms / Render ms
        sb.Append("  ");
        sb.Append(BoldWhite); sb.Append("FPS: "); sb.Append(Reset);
        sb.Append(FpsColor(data.Fps)); sb.Append($"{data.Fps,5:F1} "); sb.Append(Reset);
        AppendSparkline(sb, _fpsHist, _histIdx, 0f, 20f, 16);
        sb.Append("   ");
        sb.Append(BoldWhite); sb.Append("Tick: "); sb.Append(Reset);
        sb.Append(Cyan); sb.Append($"{data.TickNumber,7}"); sb.Append(Reset);
        sb.Append("   ");
        sb.Append(BoldWhite); sb.Append("Pipe: "); sb.Append(Reset);
        sb.Append(Cyan); sb.Append($"{data.WallClockMs,6:F1} ms"); sb.Append(Reset);
        sb.Append("   ");
        sb.Append(BoldWhite); sb.Append("Render: "); sb.Append(Reset);
        sb.Append(Cyan); sb.Append($"{data.RenderMs,5:F1} ms"); sb.Append(Reset);
        sb.Append(Eol); sb.AppendLine();

        // Row 2 — Per-population: count / pressure / pipe ms / morale
        AppendPopulationRow(sb,
            color:    BoldRed,
            label:    "[I] Invaders",
            count:    data.Invaders.Length,
            pressure: data.SimResult?.GetPopulation("Invader")?.Pressure  ?? 0f,
            pipeMs:   data.SimResult?.GetPopulation("Invader")?.ElapsedMs ?? 0,
            morale:   data.InvaderMorale);

        AppendPopulationRow(sb,
            color:    BoldGreen,
            label:    "[D] Defenders",
            count:    data.Defenders.Length,
            pressure: data.SimResult?.GetPopulation("Defender")?.Pressure  ?? 0f,
            pipeMs:   data.SimResult?.GetPopulation("Defender")?.ElapsedMs ?? 0,
            morale:   data.DefenderMorale);

        AppendPopulationRow(sb,
            color:    BoldYellow,
            label:    "[C] Civilians",
            count:    data.Civilians.Length,
            pressure: data.SimResult?.GetPopulation("Civilian")?.Pressure  ?? 0f,
            pipeMs:   data.SimResult?.GetPopulation("Civilian")?.ElapsedMs ?? 0,
            morale:   data.CivilianMorale);

        // Row 3 — Gate statuses
        AppendGateRow(sb, data.Gates);

        // Row 4 — Threat level + breach corridors
        sb.Append("  ");
        sb.Append(BoldWhite); sb.Append("Threat:  "); sb.Append(Reset);
        sb.Append(ThreatBar(data.ThreatLevel));
        sb.Append($"  {data.ThreatLevel:P0}");
        sb.Append(DimGray);
        int breachCount = data.BreachPaths.HasValue ? data.BreachPaths.Value.Length : 0;
        sb.Append(breachCount > 0
            ? $"   ⚠ {breachCount} BREACH CORRIDOR{(breachCount > 1 ? "S" : "")} — A* paths active"
            : "   InfluenceMap active · no breaches");
        sb.Append(Reset);
        sb.Append(Eol); sb.AppendLine();
    }

    private static void AppendPopulationRow(StringBuilder sb,
        string color, string label, int count, float pressure, double pipeMs, float morale)
    {
        sb.Append("  ");
        sb.Append(color);  sb.Append($"{label,-13}"); sb.Append(Reset);
        sb.Append($" {count,3}  ");
        sb.Append(BoldWhite); sb.Append("Pressure "); sb.Append(Reset);
        sb.Append(PressureBar(pressure));
        sb.Append($"  {pressure:F2}   ");
        sb.Append(BoldWhite); sb.Append("Morale "); sb.Append(Reset);
        AppendMoraleBar(sb, morale);
        sb.Append($"  {morale:F2}  ");
        sb.Append(DimGray); sb.Append($"pipe: {pipeMs,5:F1} ms"); sb.Append(Reset);
        sb.Append(Eol); sb.AppendLine();
    }

    private static string PressureBar(float p)
    {
        int filled = Math.Clamp((int)Math.Round(p * 10), 0, 10);
        string color = p < 0.4f ? Green : p < 0.7f ? Yellow : BoldRed;
        return color + new string('█', filled) + DimGray + new string('░', 10 - filled) + Reset;
    }

    private static string ThreatBar(float t)
    {
        int filled = Math.Clamp((int)Math.Round(t * 10), 0, 10);
        string color = t < 0.3f ? Cyan : t < 0.65f ? Yellow : BoldRed;
        return color + new string('█', filled) + DimGray + new string('░', 10 - filled) + Reset;
    }

    private static void AppendGateRow(StringBuilder sb, ImmutableArray<GateState> gates)
    {
        sb.Append("  ");
        sb.Append(BoldWhite); sb.Append("Gates:   "); sb.Append(Reset);
        for (int i = 0; i < gates.Length; i++)
        {
            var g = gates[i];
            string label = g.Layer switch
            {
                CastleLayer.OuterCurtain => $"O{i + 1}",
                CastleLayer.InnerBailey  => $"I{i + 1 - 6}",
                _ => $"G{i + 1}"
            };
            sb.Append(White); sb.Append($"{label}:"); sb.Append(Reset);
            string sColor = g.Status switch
            {
                GateStatus.Open        => BoldCyan,
                GateStatus.Locked      => Green,
                GateStatus.UnderAttack => BoldRed,
                GateStatus.Breached    => BoldMagenta,
                _ => Reset
            };
            string sLabel = g.Status switch
            {
                GateStatus.Open        => "Open",
                GateStatus.Locked      => "Lck",
                GateStatus.UnderAttack => "ATK!",
                GateStatus.Breached    => "BRCH",
                _ => "?"
            };
            sb.Append(sColor); sb.Append(sLabel); sb.Append(Reset);
            if (i < gates.Length - 1) { sb.Append(DimGray); sb.Append(" "); sb.Append(Reset); }
        }
        sb.Append(Eol); sb.AppendLine();
    }

    private static void AppendSparkline(StringBuilder sb, float[] hist, int idx, float min, float max, int width)
    {
        sb.Append(DimGray);
        float range = max - min;
        if (range < 0.001f) range = 1f;
        for (int i = 0; i < width; i++)
        {
            int hi = (idx - width + i + HistLen * 100) % HistLen;
            float v = Math.Clamp((hist[hi] - min) / range, 0f, 1f);
            int si = Math.Clamp((int)(v * (Spark.Length - 1)), 0, Spark.Length - 1);
            sb.Append(Spark[si]);
        }
        sb.Append(Reset);
    }

    // ── Spawn Pools ─────────────────────────────────────────────────────────

    private static void AppendPopulations(StringBuilder sb, SiegeTickData data)
    {
        sb.Append("  ");
        sb.Append(BoldWhite); sb.Append("Spawn:   "); sb.Append(Reset);

        sb.Append(BoldRed); sb.Append("[I] "); sb.Append(Reset);
        AppendBar(sb, data.InvaderPool, data.Config.InvaderPool, Red, 8);
        sb.Append(DimGray); sb.Append($" {data.InvaderPool,4}  "); sb.Append(Reset);

        sb.Append(BoldGreen); sb.Append("[D] "); sb.Append(Reset);
        AppendBar(sb, data.DefenderPool, data.Config.DefenderPool, Green, 8);
        sb.Append(DimGray); sb.Append($" {data.DefenderPool,4}  "); sb.Append(Reset);

        sb.Append(BoldYellow); sb.Append("[C] "); sb.Append(Reset);
        AppendBar(sb, data.CivilianPool, data.Config.CivilianPool, Yellow, 8);
        sb.Append(DimGray); sb.Append($" {data.CivilianPool,4}  "); sb.Append(Reset);

        sb.Append(Eol); sb.AppendLine();
    }

    private static void AppendBar(StringBuilder sb, int current, int max, string color, int width)
    {
        int filled = max > 0 ? Math.Clamp((int)((float)current / max * width), 0, width) : 0;
        sb.Append(color);
        for (int i = 0; i < filled; i++) sb.Append('█');
        sb.Append(DimGray);
        for (int i = filled; i < width; i++) sb.Append('░');
        sb.Append(Reset);
    }

    private static void AppendMoraleBar(StringBuilder sb, float morale)
    {
        int filled = Math.Clamp((int)(morale * 8), 0, 8);
        string color = morale > 0.7f ? BoldGreen : morale > 0.4f ? Yellow : BoldRed;
        sb.Append(color);
        for (int i = 0; i < filled; i++) sb.Append('█');
        sb.Append(DimGray);
        for (int i = filled; i < 8; i++) sb.Append('▒');
        sb.Append(Reset);
    }

    // ── Zone Control ──────────────────────────────────────────────────────────

    private static void AppendZones(StringBuilder sb, SiegeTickData data)
    {
        var zones = data.Zones;
        if (zones.IsDefaultOrEmpty) return;

        sb.Append("  ");
        sb.Append(BoldWhite); sb.Append("Zones: "); sb.Append(Reset);

        foreach (var z in zones)
        {
            string label = z.ZoneId.Length > 5 ? z.ZoneId[..5].ToUpperInvariant() : z.ZoneId.ToUpperInvariant();
            string zColor = z.Owner == AgentRole.Invader ? BoldRed
                          : z.ContestPercent > 0.25f ? Yellow
                          : DimGray;
            string owner = z.Owner == AgentRole.Invader ? "INV" : "---";
            sb.Append(zColor);
            sb.Append($"⬡{label}[{owner} {z.ContestPercent:P0}] ");
            sb.Append(Reset);
        }
        sb.Append(Eol); sb.AppendLine();
    }

    // ── Behaviour Distribution ────────────────────────────────────────────────

    private static void AppendBehaviours(StringBuilder sb, SiegeTickData data)
    {
        var inv = data.Invaders;
        var def = data.Defenders;

        sb.Append("  ");
        sb.Append(BoldRed); sb.Append("INV "); sb.Append(Reset);
        AppendBehaviourCounts(sb, inv, [
            (BehaviourState.Mustering, "Mstr"),
            (BehaviourState.Advancing, "Adv"),
            (BehaviourState.Sieging,    "Sge"),
            (BehaviourState.Breaching, "Brch"),
            (BehaviourState.Claiming,  "Clm"),
            (BehaviourState.Pushing,   "Push")
        ]);

        sb.Append(DimGray); sb.Append(" │ "); sb.Append(Reset);

        sb.Append(BoldGreen); sb.Append("DEF "); sb.Append(Reset);
        AppendBehaviourCounts(sb, def, [
            (BehaviourState.Holding,     "Hld"),
            (BehaviourState.Reinforcing, "Rnf"),
            (BehaviourState.Fighting,    "Fgt"),
            (BehaviourState.FallingBack, "Fall"),
            (BehaviourState.LastStand,   "Last")
        ]);

        sb.Append(Eol); sb.AppendLine();
    }

    private static void AppendBehaviourCounts(StringBuilder sb,
        ImmutableArray<SimAgent> agents,
        (BehaviourState state, string label)[] states)
    {
        foreach (var (state, label) in states)
        {
            int count = 0;
            foreach (var a in agents)
                if (a.Behaviour == state) count++;
            sb.Append(DimGray); sb.Append($"{label}:"); sb.Append(Reset);
            sb.Append($"{count,3} ");
        }
    }

    // ── Defence Line + Win Banner ─────────────────────────────────────────────

    private static void AppendDefenceAndWin(StringBuilder sb, SiegeTickData data)
    {
        // Defence line indicator
        sb.Append("  ");
        sb.Append(BoldWhite); sb.Append("Defence: "); sb.Append(Reset);
        (string lineColor, string lineName, string lineChar) = data.ActiveDefenceLine switch
        {
            CastleLayer.OuterCurtain => (OuterWallFg, " OUTER CURTAIN ", "═══"),
            CastleLayer.InnerBailey  => (InnerWallFg, " INNER BAILEY ",  "───"),
            CastleLayer.Keep         => (KeepWallFg,  " KEEP ",          "━━━"),
            _                        => (DimGray,     " ??? ",           "---")
        };
        sb.Append(lineColor); sb.Append(lineChar); sb.Append(lineName); sb.Append(lineChar); sb.Append(Reset);

        // Win state
        (string wColor, string wLabel) = data.WinState switch
        {
            WinState.OuterBreached    => (BoldYellow,  "  ⚠ OUTER WALLS BREACHED"),
            WinState.InnerContested   => ("\u001b[38;5;208m", "  ⚠ INNER GATES UNDER SIEGE"),
            WinState.InvadersWinning  => (BoldRed,     "  🔥 KEEP UNDER ASSAULT"),
            WinState.DefendersWinning => (BoldGreen,   "  ⛨ INVADER FORCES DEPLETED"),
            WinState.InvadersWon      => (BoldRed,     "  🏴 THE CASTLE HAS FALLEN"),
            WinState.DefendersWon     => (BoldGreen,   "  🛡 THE CASTLE STANDS"),
            _                         => (DimGray,     "  Siege in progress"),
        };
        sb.Append(wColor); sb.Append(wLabel); sb.Append(Reset);

        sb.Append(Eol); sb.AppendLine();

        // SDK branding
        sb.Append("  ");
        sb.Append(DimGray);
        sb.Append("SDK: NavPathfinder v1.0   Pipeline: EvalApp v2 (Licensed)");
        sb.Append(Reset);
        sb.Append(Eol); sb.AppendLine();
    }

    // ── Legend ─────────────────────────────────────────────────────────────────

    private static void AppendLegend(StringBuilder sb)
    {
        string sep = new string('━', PanelWidth);
        sb.Append(DimGray); sb.Append(sep); sb.Append(Eol); sb.Append(Reset); sb.AppendLine();

        sb.Append("  ");
        sb.Append(OuterWallFg); sb.Append("[═] "); sb.Append(Reset);
        sb.Append(DimGray); sb.Append("Outer  "); sb.Append(Reset);
        sb.Append(InnerWallFg); sb.Append("[─] "); sb.Append(Reset);
        sb.Append(DimGray); sb.Append("Inner  "); sb.Append(Reset);
        sb.Append(KeepWallFg); sb.Append("[━] "); sb.Append(Reset);
        sb.Append(DimGray); sb.Append("Keep  "); sb.Append(Reset);

        sb.Append(DimGray); sb.Append("│ "); sb.Append(Reset);

        // Faction identification by glyph shape
        sb.Append(BoldRed); sb.Append("×† "); sb.Append(Reset);
        sb.Append(DimGray); sb.Append("Inv  "); sb.Append(Reset);
        sb.Append(BoldGreen); sb.Append("+# "); sb.Append(Reset);
        sb.Append(DimGray); sb.Append("Def  "); sb.Append(Reset);
        sb.Append(BoldYellow); sb.Append("∘● "); sb.Append(Reset);
        sb.Append(DimGray); sb.Append("Civ"); sb.Append(Reset);

        sb.Append(Eol); sb.AppendLine();

        // Second legend row: AI state color key
        sb.Append("  ");
        sb.Append(StateColor(BehaviourState.Advancing)); sb.Append("■"); sb.Append(Reset);
        sb.Append(DimGray); sb.Append("Adv "); sb.Append(Reset);
        sb.Append(StateColor(BehaviourState.Sieging)); sb.Append("■"); sb.Append(Reset);
        sb.Append(DimGray); sb.Append("Sge "); sb.Append(Reset);
        sb.Append(StateColor(BehaviourState.Breaching)); sb.Append("■"); sb.Append(Reset);
        sb.Append(DimGray); sb.Append("Brch "); sb.Append(Reset);
        sb.Append(StateColor(BehaviourState.Claiming)); sb.Append("■"); sb.Append(Reset);
        sb.Append(DimGray); sb.Append("Clm "); sb.Append(Reset);

        sb.Append(DimGray); sb.Append("│ "); sb.Append(Reset);

        sb.Append(StateColor(BehaviourState.Holding)); sb.Append("■"); sb.Append(Reset);
        sb.Append(DimGray); sb.Append("Hld "); sb.Append(Reset);
        sb.Append(StateColor(BehaviourState.Reinforcing)); sb.Append("■"); sb.Append(Reset);
        sb.Append(DimGray); sb.Append("Rnf "); sb.Append(Reset);
        sb.Append(StateColor(BehaviourState.Fighting)); sb.Append("■"); sb.Append(Reset);
        sb.Append(DimGray); sb.Append("Fgt "); sb.Append(Reset);
        sb.Append(StateColor(BehaviourState.FallingBack)); sb.Append("■"); sb.Append(Reset);
        sb.Append(DimGray); sb.Append("Ret "); sb.Append(Reset);
        sb.Append(StateColor(BehaviourState.LastStand)); sb.Append("■"); sb.Append(Reset);
        sb.Append(DimGray); sb.Append("Lst"); sb.Append(Reset);

        sb.Append(DimGray); sb.Append(" │ "); sb.Append(Reset);

        sb.Append(StateColor(BehaviourState.Sheltering)); sb.Append("■"); sb.Append(Reset);
        sb.Append(DimGray); sb.Append("Shl "); sb.Append(Reset);
        sb.Append(StateColor(BehaviourState.Fleeing)); sb.Append("■"); sb.Append(Reset);
        sb.Append(DimGray); sb.Append("Fle "); sb.Append(Reset);
        sb.Append(StateColor(BehaviourState.Hidden)); sb.Append("■"); sb.Append(Reset);
        sb.Append(DimGray); sb.Append("Hdn"); sb.Append(Reset);

        sb.Append(DimGray); sb.Append("  ╬locked ▓atk ░breach"); sb.Append(Reset);

        sb.Append(Eol); sb.AppendLine();
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    private static string FpsColor(double fps) =>
        fps >= 13 ? Green : fps >= 8 ? Yellow : Red;

    private static string Pad(string text, int width)
    {
        if (text.Length >= width) return text[..width];
        int left  = (width - text.Length) / 2;
        int right = width - text.Length - left;
        return new string(' ', left) + text + new string(' ', right);
    }

    private static void CountAgents(int[,] counts, ImmutableArray<SimAgent> agents)
    {
        foreach (var a in agents)
        {
            int col = (int)a.Position.X, row = (int)a.Position.Y;
            if (row >= 0 && row < GridRows && col >= 0 && col < GridCols)
                counts[row, col]++;
        }
    }

    /// <summary>
    /// Records the most "urgent" BehaviourState per cell.
    /// Higher enum ordinals are more active, so we keep the max.
    /// Tie-breaking: combat/breach states win over passive states.
    /// </summary>
    private static void TrackDominantState(ImmutableArray<SimAgent> agents)
    {
        foreach (var a in agents)
        {
            int col = (int)a.Position.X, row = (int)a.Position.Y;
            if (row >= 0 && row < GridRows && col >= 0 && col < GridCols)
            {
                int current = StatePriority(_domState[row, col]);
                int incoming = StatePriority(a.Behaviour);
                if (incoming > current)
                    _domState[row, col] = a.Behaviour;
            }
        }
    }

    /// <summary>Priority for selecting dominant state — higher = more interesting to display.</summary>
    private static int StatePriority(BehaviourState s) => s switch
    {
        BehaviourState.Hidden      => 0,
        BehaviourState.Sheltering  => 1,
        BehaviourState.Mustering   => 2,
        BehaviourState.Holding     => 3,
        BehaviourState.Advancing   => 4,
        BehaviourState.Reinforcing => 5,
        BehaviourState.FallingBack => 6,
        BehaviourState.Pushing     => 7,
        BehaviourState.Claiming    => 8,
        BehaviourState.Fleeing     => 9,
        BehaviourState.Sieging     => 10,
        BehaviourState.Fighting    => 11,
        BehaviourState.Breaching   => 12,
        BehaviourState.LastStand   => 13,
        _                          => 0
    };
}
