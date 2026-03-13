using System.Text;
using EvalApp.Consumer;

using NavPathfinder.Demo.Data;
using NavPathfinder.Demo.Rendering;

namespace NavPathfinder.Demo.Steps.Rendering;

/// <summary>
/// Step 9: Serialize the accumulated StringBuilder output into the RenderedFrame string.
/// Also prepends the header and appends EOL clearing.
/// </summary>
public sealed class SerializeFrame : PureStep<SiegeTickData>
{
    public override SiegeTickData Execute(SiegeTickData data)
    {
        var buf = data.RenderBuffer!;
        var output = new StringBuilder(buf.StringBuilder.Length + 512);

        // Header
        AppendHeader(output, buf.Cols);

        // All panel content accumulated by previous steps
        output.Append(buf.StringBuilder);

        return data with { RenderedFrame = output.ToString() };
    }

    private static void AppendHeader(StringBuilder sb, int panelWidth)
    {
        string sep = new string('━', panelWidth);
        sb.Append(ColorPalette.HudSeparator); sb.Append(sep);
        sb.Append(ColorPalette.Eol); sb.Append(ColorPalette.Reset); sb.AppendLine();

        string title = "⚔  NavPathfinder SDK · Castle Siege  ⚔";
        sb.Append(ColorPalette.BoldYellow);
        sb.Append(BarRenderer.Pad(title, panelWidth));
        sb.Append(ColorPalette.Eol); sb.Append(ColorPalette.Reset); sb.AppendLine();

        sb.Append(ColorPalette.HudSeparator); sb.Append(sep);
        sb.Append(ColorPalette.Eol); sb.Append(ColorPalette.Reset); sb.AppendLine();

        string desc = "Multi-layer concentric castle. 14 purposeful AI behaviours. All pathfinding via SDK.";
        sb.Append(ColorPalette.DimGray);
        sb.Append(BarRenderer.Pad(desc, panelWidth));
        sb.Append(ColorPalette.Eol); sb.Append(ColorPalette.Reset); sb.AppendLine();

        sb.Append(ColorPalette.HudSeparator); sb.Append(sep);
        sb.Append(ColorPalette.Eol); sb.Append(ColorPalette.Reset); sb.AppendLine();
    }
}
