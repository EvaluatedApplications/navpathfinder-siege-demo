using EvalApp.Consumer;

using NavPathfinder.Demo.Data;
using NavPathfinder.Demo.Rendering;

namespace NavPathfinder.Demo.Steps.Rendering;

/// <summary>
/// Step 5: Render zone control — ownership, contest percentage, and status for each zone.
/// </summary>
public sealed class RenderZones : PureStep<SiegeTickData>
{
    public override SiegeTickData Execute(SiegeTickData data)
    {
        var zones = data.Zones;
        if (zones.IsDefaultOrEmpty)
            return data;

        var sb = data.RenderBuffer!.StringBuilder;

        sb.Append("  ");
        sb.Append(ColorPalette.BoldWhite); sb.Append("Zones: "); sb.Append(ColorPalette.Reset);

        foreach (var z in zones)
        {
            string label = z.ZoneId.Length > 5 ? z.ZoneId[..5].ToUpperInvariant() : z.ZoneId.ToUpperInvariant();
            string zColor = z.Owner == AgentRole.Invader ? ColorPalette.BoldRed
                          : z.ContestPercent > 0.25f ? ColorPalette.Yellow
                          : ColorPalette.DimGray;
            string owner = z.Owner == AgentRole.Invader ? "INV" : "---";
            sb.Append(zColor);
            sb.Append($"⬡{label}[{owner} {z.ContestPercent:P0}] ");
            sb.Append(ColorPalette.Reset);
        }

        sb.Append(ColorPalette.Eol); sb.AppendLine();

        return data;
    }
}
