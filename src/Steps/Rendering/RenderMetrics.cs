using System.Collections.Immutable;
using EvalApp.Consumer;

using NavPathfinder.Demo.Data;
using NavPathfinder.Demo.Rendering;

namespace NavPathfinder.Demo.Steps.Rendering;

/// <summary>
/// Step 3: Render the metrics panel — FPS sparkline, tick count, pipeline/render timing,
/// population pressure/morale, gate statuses, and threat level.
/// </summary>
public sealed class RenderMetrics : PureStep<SiegeTickData>
{
    public override SiegeTickData Execute(SiegeTickData data)
    {
        var buf = data.RenderBuffer!;
        var sb = buf.StringBuilder;
        int panelWidth = buf.Cols;

        string sep = new string('━', panelWidth);
        sb.Append(ColorPalette.HudSeparator); sb.Append(sep);
        sb.Append(ColorPalette.Eol); sb.Append(ColorPalette.Reset); sb.AppendLine();

        // Row 1 — FPS / Tick / Pipe ms / Render ms
        sb.Append("  ");
        sb.Append(ColorPalette.BoldWhite); sb.Append("FPS: "); sb.Append(ColorPalette.Reset);
        sb.Append(ColorPalette.FpsColor(data.Fps));
        sb.Append($"{data.Fps,5:F1} "); sb.Append(ColorPalette.Reset);
        BarRenderer.AppendSparkline(sb, buf.FpsHistory, buf.FpsHistoryIndex, 0f, 20f, 16);
        sb.Append("   ");
        sb.Append(ColorPalette.BoldWhite); sb.Append("Tick: "); sb.Append(ColorPalette.Reset);
        sb.Append(ColorPalette.Cyan); sb.Append($"{data.TickNumber,7}"); sb.Append(ColorPalette.Reset);
        sb.Append("   ");
        sb.Append(ColorPalette.BoldWhite); sb.Append("Pipe: "); sb.Append(ColorPalette.Reset);
        sb.Append(ColorPalette.Cyan); sb.Append($"{data.WallClockMs,6:F1} ms"); sb.Append(ColorPalette.Reset);
        sb.Append("   ");
        sb.Append(ColorPalette.BoldWhite); sb.Append("Render: "); sb.Append(ColorPalette.Reset);
        sb.Append(ColorPalette.Cyan); sb.Append($"{data.RenderMs,5:F1} ms"); sb.Append(ColorPalette.Reset);
        sb.Append(ColorPalette.Eol); sb.AppendLine();

        // Rows 2–4 — Per-population: count / pressure / morale
        AppendPopulationRow(sb, ColorPalette.InvColor, "[I] Invaders",
            data.Invaders.Length,
            data.SimResult?.GetPopulation("Invader")?.Pressure ?? 0f,
            data.SimResult?.GetPopulation("Invader")?.ElapsedMs ?? 0,
            data.InvaderMorale);

        AppendPopulationRow(sb, ColorPalette.DefColor, "[D] Defenders",
            data.Defenders.Length,
            data.SimResult?.GetPopulation("Defender")?.Pressure ?? 0f,
            data.SimResult?.GetPopulation("Defender")?.ElapsedMs ?? 0,
            data.DefenderMorale);

        AppendPopulationRow(sb, ColorPalette.CivColor, "[C] Civilians",
            data.Civilians.Length,
            data.SimResult?.GetPopulation("Civilian")?.Pressure ?? 0f,
            data.SimResult?.GetPopulation("Civilian")?.ElapsedMs ?? 0,
            data.CivilianMorale);

        // Gate statuses
        AppendGateRow(sb, data.Gates);

        // Threat level
        sb.Append("  ");
        sb.Append(ColorPalette.BoldWhite); sb.Append("Threat:  "); sb.Append(ColorPalette.Reset);
        BarRenderer.AppendThreatBar(sb, data.ThreatLevel);
        sb.Append(ColorPalette.DimGray); sb.Append($"  {data.ThreatLevel:P0}"); sb.Append(ColorPalette.Reset);
        sb.Append(ColorPalette.DimGray);
        int breachCount = data.BreachPaths.HasValue ? data.BreachPaths.Value.Length : 0;
        sb.Append(breachCount > 0
            ? $"   ⚠ {breachCount} BREACH CORRIDOR{(breachCount > 1 ? "S" : "")} — A* paths active"
            : "   InfluenceMap active · no breaches");
        sb.Append(ColorPalette.Reset);
        sb.Append(ColorPalette.Eol); sb.AppendLine();

        return data;
    }

    private static void AppendPopulationRow(System.Text.StringBuilder sb,
        string color, string label, int count, float pressure, double pipeMs, float morale)
    {
        sb.Append("  ");
        sb.Append(color);  sb.Append($"{label,-13}"); sb.Append(ColorPalette.Reset);
        sb.Append($" {count,3}  ");
        sb.Append(ColorPalette.BoldWhite); sb.Append("Pressure "); sb.Append(ColorPalette.Reset);
        BarRenderer.AppendPressureBar(sb, pressure);
        sb.Append(ColorPalette.DimGray); sb.Append($"  {pressure:F2}   "); sb.Append(ColorPalette.Reset);
        sb.Append(ColorPalette.BoldWhite); sb.Append("Morale "); sb.Append(ColorPalette.Reset);
        BarRenderer.AppendMoraleBar(sb, morale);
        sb.Append(ColorPalette.DimGray); sb.Append($"  {morale:F2}  "); sb.Append(ColorPalette.Reset);
        sb.Append(ColorPalette.DimGray); sb.Append($"pipe: {pipeMs,5:F1} ms"); sb.Append(ColorPalette.Reset);
        sb.Append(ColorPalette.Eol); sb.AppendLine();
    }

    private static void AppendGateRow(System.Text.StringBuilder sb, ImmutableArray<GateState> gates)
    {
        sb.Append("  ");
        sb.Append(ColorPalette.BoldWhite); sb.Append("Gates:   "); sb.Append(ColorPalette.Reset);
        for (int i = 0; i < gates.Length; i++)
        {
            var g = gates[i];
            string gLabel = g.Layer switch
            {
                CastleLayer.OuterCurtain => $"O{i + 1}",
                CastleLayer.InnerBailey  => $"I{i + 1 - 6}",
                _ => $"G{i + 1}"
            };
            sb.Append(ColorPalette.White); sb.Append($"{gLabel}:"); sb.Append(ColorPalette.Reset);
            sb.Append(ColorPalette.GateStatusColor(g.Status));
            sb.Append(GlyphSets.GateStatusLabel(g.Status));
            sb.Append(ColorPalette.Reset);
            if (i < gates.Length - 1) { sb.Append(ColorPalette.DimGray); sb.Append(" "); sb.Append(ColorPalette.Reset); }
        }
        sb.Append(ColorPalette.Eol); sb.AppendLine();
    }
}
