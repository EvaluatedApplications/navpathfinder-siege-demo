using EvalApp.Consumer;

using NavPathfinder.Demo.Data;
using NavPathfinder.Demo.Rendering;

namespace NavPathfinder.Demo.Steps.Rendering;

/// <summary>
/// Step 7: Render the defence line indicator, win/loss banner, and SDK branding.
/// Always renders regardless of win state.
/// </summary>
public sealed class RenderStatus : PureStep<SiegeTickData>
{
    public override SiegeTickData Execute(SiegeTickData data)
    {
        var sb = data.RenderBuffer!.StringBuilder;

        // Defence line indicator
        sb.Append("  ");
        sb.Append(ColorPalette.BoldWhite); sb.Append("Defence: "); sb.Append(ColorPalette.Reset);
        var (lineColor, lineName, lineChar) = data.ActiveDefenceLine switch
        {
            CastleLayer.OuterCurtain => (ColorPalette.OuterWallFg, " OUTER CURTAIN ", "═══"),
            CastleLayer.InnerBailey  => (ColorPalette.InnerWallFg, " INNER BAILEY ",  "───"),
            CastleLayer.Keep         => (ColorPalette.KeepWallFg,  " KEEP ",          "━━━"),
            _                        => (ColorPalette.DimGray,     " ??? ",           "---")
        };
        sb.Append(lineColor); sb.Append(lineChar); sb.Append(lineName); sb.Append(lineChar);
        sb.Append(ColorPalette.Reset);

        // Win state banner
        var (wColor, wLabel) = data.WinState switch
        {
            WinState.OuterBreached    => (ColorPalette.BoldYellow,           "  ⚠ OUTER WALLS BREACHED"),
            WinState.InnerContested   => ("\u001b[1;38;5;208m",              "  ⚠ INNER GATES UNDER SIEGE"),
            WinState.InvadersWinning  => (ColorPalette.BoldRed,              "  🔥 KEEP UNDER ASSAULT"),
            WinState.DefendersWinning => (ColorPalette.BoldGreen,            "  ⛨ INVADER FORCES DEPLETED"),
            WinState.InvadersWon      => ("\u001b[1;38;5;196m\u001b[48;5;52m", "  🏴 THE CASTLE HAS FALLEN"),
            WinState.DefendersWon     => ("\u001b[1;38;5;46m\u001b[48;5;22m",  "  🛡 THE CASTLE STANDS"),
            _                         => (ColorPalette.DimGray,              "  Siege in progress"),
        };
        sb.Append(wColor); sb.Append(wLabel); sb.Append(ColorPalette.Reset);
        sb.Append(ColorPalette.Eol); sb.AppendLine();

        // SDK branding
        sb.Append("  ");
        sb.Append(ColorPalette.DimGray);
        sb.Append("SDK: NavPathfinder v1.0   Pipeline: EvalApp v2 (Licensed)");
        sb.Append(ColorPalette.Reset);
        sb.Append(ColorPalette.Eol); sb.AppendLine();

        return data;
    }
}
