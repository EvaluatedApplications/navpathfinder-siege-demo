using EvalApp.Consumer;

using NavPathfinder.Demo.Data;
using NavPathfinder.Demo.Rendering;

namespace NavPathfinder.Demo.Steps.Rendering;

/// <summary>
/// Step 4: Render the spawn pool bars — current/max resource display for each faction.
/// </summary>
public sealed class RenderPops : PureStep<SiegeTickData>
{
    public override SiegeTickData Execute(SiegeTickData data)
    {
        var sb = data.RenderBuffer!.StringBuilder;

        sb.Append("  ");
        sb.Append(ColorPalette.BoldWhite); sb.Append("Spawn:   "); sb.Append(ColorPalette.Reset);

        sb.Append(ColorPalette.InvColor); sb.Append("[I] "); sb.Append(ColorPalette.Reset);
        BarRenderer.AppendResourceBar(sb, data.InvaderPool, data.Config.InvaderPool, ColorPalette.Red, 8);
        sb.Append(ColorPalette.DimGray); sb.Append($" {data.InvaderPool,4}  "); sb.Append(ColorPalette.Reset);

        sb.Append(ColorPalette.DefColor); sb.Append("[D] "); sb.Append(ColorPalette.Reset);
        BarRenderer.AppendResourceBar(sb, data.DefenderPool, data.Config.DefenderPool, ColorPalette.Green, 8);
        sb.Append(ColorPalette.DimGray); sb.Append($" {data.DefenderPool,4}  "); sb.Append(ColorPalette.Reset);

        sb.Append(ColorPalette.CivColor); sb.Append("[C] "); sb.Append(ColorPalette.Reset);
        BarRenderer.AppendResourceBar(sb, data.CivilianPool, data.Config.CivilianPool, ColorPalette.Yellow, 8);
        sb.Append(ColorPalette.DimGray); sb.Append($" {data.CivilianPool,4}  "); sb.Append(ColorPalette.Reset);

        sb.Append(ColorPalette.Eol); sb.AppendLine();

        return data;
    }
}
