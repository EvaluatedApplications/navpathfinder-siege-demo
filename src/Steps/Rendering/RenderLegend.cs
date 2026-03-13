using EvalApp.Consumer;

using NavPathfinder.Demo.Data;
using NavPathfinder.Demo.Rendering;

namespace NavPathfinder.Demo.Steps.Rendering;

/// <summary>
/// Step 8: Render the legend — wall types, faction glyph key, and AI state colour reference.
/// Only runs on first frame or terminal resize.
/// </summary>
public sealed class RenderLegend : PureStep<SiegeTickData>
{
    public override SiegeTickData Execute(SiegeTickData data)
    {
        var sb = data.RenderBuffer!.StringBuilder;
        int panelWidth = data.RenderBuffer.Cols;

        string sep = new string('━', panelWidth);
        sb.Append(ColorPalette.HudSeparator); sb.Append(sep);
        sb.Append(ColorPalette.Eol); sb.Append(ColorPalette.Reset); sb.AppendLine();

        // Row 1: Walls + Gates
        sb.Append("  ");
        sb.Append(ColorPalette.OuterWallFg); sb.Append("═║ "); sb.Append(ColorPalette.Reset);
        sb.Append(ColorPalette.DimGray); sb.Append("Outer  "); sb.Append(ColorPalette.Reset);
        sb.Append(ColorPalette.InnerWallFg); sb.Append("─│ "); sb.Append(ColorPalette.Reset);
        sb.Append(ColorPalette.DimGray); sb.Append("Inner  "); sb.Append(ColorPalette.Reset);
        sb.Append(ColorPalette.KeepWallFg); sb.Append("━┃ "); sb.Append(ColorPalette.Reset);
        sb.Append(ColorPalette.DimGray); sb.Append("Keep  "); sb.Append(ColorPalette.Reset);

        sb.Append(ColorPalette.DimGray); sb.Append("│ "); sb.Append(ColorPalette.Reset);

        sb.Append(ColorPalette.BoldCyan);    sb.Append("░"); sb.Append(ColorPalette.Reset);
        sb.Append(ColorPalette.DimGray);     sb.Append("Open "); sb.Append(ColorPalette.Reset);
        sb.Append(ColorPalette.BoldGreen);   sb.Append("╬"); sb.Append(ColorPalette.Reset);
        sb.Append(ColorPalette.DimGray);     sb.Append("Locked "); sb.Append(ColorPalette.Reset);
        sb.Append(ColorPalette.BoldRed);     sb.Append("▓"); sb.Append(ColorPalette.Reset);
        sb.Append(ColorPalette.DimGray);     sb.Append("UnderAtk "); sb.Append(ColorPalette.Reset);
        sb.Append(ColorPalette.BoldMagenta); sb.Append("░"); sb.Append(ColorPalette.Reset);
        sb.Append(ColorPalette.DimGray);     sb.Append("Breached"); sb.Append(ColorPalette.Reset);

        sb.Append(ColorPalette.DimGray); sb.Append(" │ "); sb.Append(ColorPalette.Reset);
        sb.Append(ColorPalette.DimGray); sb.Append("Ranged blocked by walls (defenders fire over)");
        sb.Append(ColorPalette.Reset);
        sb.Append(ColorPalette.Eol); sb.AppendLine();

        // Row 2: Faction glyphs + density + leaders
        sb.Append("  ");
        sb.Append(ColorPalette.InvColor); sb.Append("·×†‡ "); sb.Append(ColorPalette.Reset);
        sb.Append(ColorPalette.DimGray); sb.Append("Invaders  "); sb.Append(ColorPalette.Reset);
        sb.Append(ColorPalette.DefColor); sb.Append("·+‡# "); sb.Append(ColorPalette.Reset);
        sb.Append(ColorPalette.DimGray); sb.Append("Defenders  "); sb.Append(ColorPalette.Reset);
        sb.Append(ColorPalette.CivColor); sb.Append("·∘○● "); sb.Append(ColorPalette.Reset);
        sb.Append(ColorPalette.DimGray); sb.Append("Civilians"); sb.Append(ColorPalette.Reset);

        sb.Append(ColorPalette.DimGray); sb.Append(" │ "); sb.Append(ColorPalette.Reset);

        sb.Append(ColorPalette.InvColor); sb.Append("★ "); sb.Append(ColorPalette.Reset);
        sb.Append(ColorPalette.DimGray); sb.Append("Inv Leader  "); sb.Append(ColorPalette.Reset);
        sb.Append(ColorPalette.DefColor); sb.Append("★ "); sb.Append(ColorPalette.Reset);
        sb.Append(ColorPalette.DimGray); sb.Append("Def Leader"); sb.Append(ColorPalette.Reset);

        sb.Append(ColorPalette.DimGray); sb.Append("  │ "); sb.Append(ColorPalette.Reset);
        sb.Append(ColorPalette.DimGray); sb.Append("Density: low→high  Kill = Level Up + Heal");
        sb.Append(ColorPalette.Reset);
        sb.Append(ColorPalette.Eol); sb.AppendLine();

        // Row 3: AI state colour key — all states
        sb.Append("  ");
        AppendStateKey(sb, BehaviourState.Advancing, "Adv");
        AppendStateKey(sb, BehaviourState.Sieging,   "Sge");
        AppendStateKey(sb, BehaviourState.Breaching, "Brch");
        AppendStateKey(sb, BehaviourState.Claiming,  "Clm");
        AppendStateKey(sb, BehaviourState.Pushing,   "Psh");

        sb.Append(ColorPalette.DimGray); sb.Append("│ "); sb.Append(ColorPalette.Reset);

        AppendStateKey(sb, BehaviourState.Holding,     "Hld");
        AppendStateKey(sb, BehaviourState.Sortie,      "Srt");
        AppendStateKey(sb, BehaviourState.Reinforcing, "Rnf");
        AppendStateKey(sb, BehaviourState.Fighting,    "Fgt");
        AppendStateKey(sb, BehaviourState.FallingBack, "Ret");
        AppendStateKey(sb, BehaviourState.LastStand,   "Lst");

        sb.Append(ColorPalette.DimGray); sb.Append("│ "); sb.Append(ColorPalette.Reset);

        AppendStateKey(sb, BehaviourState.Sheltering, "Shl");
        AppendStateKey(sb, BehaviourState.Fleeing,    "Fle");
        AppendStateKey(sb, BehaviourState.Hidden,     "Hdn");
        AppendStateKey(sb, BehaviourState.Mustering,  "Mst");

        sb.Append(ColorPalette.Eol); sb.AppendLine();

        // Row 4: Unit types per faction
        sb.Append("  ");
        sb.Append(ColorPalette.InvColor); sb.Append("⚔ "); sb.Append(ColorPalette.Reset);
        sb.Append(ColorPalette.DimGray); sb.Append("Swordsmen(Slash)  "); sb.Append(ColorPalette.Reset);
        sb.Append(ColorPalette.InvColor); sb.Append("🔥"); sb.Append(ColorPalette.Reset);
        sb.Append(ColorPalette.DimGray); sb.Append("Mages(Fire)"); sb.Append(ColorPalette.Reset);

        sb.Append(ColorPalette.DimGray); sb.Append("  │ "); sb.Append(ColorPalette.Reset);

        sb.Append(ColorPalette.DefColor); sb.Append("🛡 "); sb.Append(ColorPalette.Reset);
        sb.Append(ColorPalette.DimGray); sb.Append("Guards(Blunt)  "); sb.Append(ColorPalette.Reset);
        sb.Append(ColorPalette.DefColor); sb.Append("🏹"); sb.Append(ColorPalette.Reset);
        sb.Append(ColorPalette.DimGray); sb.Append("Archers(Pierce)  "); sb.Append(ColorPalette.Reset);
        sb.Append(ColorPalette.DefColor); sb.Append("⚔ "); sb.Append(ColorPalette.Reset);
        sb.Append(ColorPalette.DimGray); sb.Append("Sortie(Slash)"); sb.Append(ColorPalette.Reset);

        sb.Append(ColorPalette.DimGray); sb.Append("  │ "); sb.Append(ColorPalette.Reset);
        sb.Append(ColorPalette.CivColor); sb.Append("○ "); sb.Append(ColorPalette.Reset);
        sb.Append(ColorPalette.DimGray); sb.Append("Civilians(unarmed)"); sb.Append(ColorPalette.Reset);
        sb.Append(ColorPalette.Eol); sb.AppendLine();

        return data;
    }

    private static void AppendStateKey(System.Text.StringBuilder sb, BehaviourState state, string label)
    {
        sb.Append(ColorPalette.StateColor(state)); sb.Append("■"); sb.Append(ColorPalette.Reset);
        sb.Append(ColorPalette.DimGray); sb.Append(label); sb.Append(' '); sb.Append(ColorPalette.Reset);
    }
}
