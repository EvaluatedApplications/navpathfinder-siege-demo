using System.Collections.Immutable;
using EvalApp.Consumer;

using NavPathfinder.Demo.Data;
using NavPathfinder.Demo.Rendering;

namespace NavPathfinder.Demo.Steps.Rendering;

/// <summary>
/// Step 6: Render AI behaviour state distribution — counts of agents in each FSM state per faction.
/// </summary>
public sealed class RenderBehaviours : PureStep<SiegeTickData>
{
    public override SiegeTickData Execute(SiegeTickData data)
    {
        var sb = data.RenderBuffer!.StringBuilder;
        var inv = data.Invaders;
        var def = data.Defenders;

        sb.Append("  ");
        sb.Append(ColorPalette.InvColor); sb.Append("INV "); sb.Append(ColorPalette.Reset);
        AppendBehaviourCounts(sb, inv, [
            (BehaviourState.Mustering, "Mstr"),
            (BehaviourState.Advancing, "Adv"),
            (BehaviourState.Sieging,   "Sge"),
            (BehaviourState.Breaching, "Brch"),
            (BehaviourState.Claiming,  "Clm"),
            (BehaviourState.Pushing,   "Push")
        ]);

        sb.Append(ColorPalette.DimGray); sb.Append(" │ "); sb.Append(ColorPalette.Reset);

        sb.Append(ColorPalette.DefColor); sb.Append("DEF "); sb.Append(ColorPalette.Reset);
        AppendBehaviourCounts(sb, def, [
            (BehaviourState.Sortie,      "Srt"),
            (BehaviourState.Holding,     "Hld"),
            (BehaviourState.Reinforcing, "Rnf"),
            (BehaviourState.Fighting,    "Fgt"),
            (BehaviourState.FallingBack, "Fall"),
            (BehaviourState.LastStand,   "Last")
        ]);

        sb.Append(ColorPalette.Eol); sb.AppendLine();

        return data;
    }

    private static void AppendBehaviourCounts(System.Text.StringBuilder sb,
        ImmutableArray<SimAgent> agents,
        (BehaviourState state, string label)[] states)
    {
        foreach (var (state, label) in states)
        {
            int count = 0;
            foreach (var a in agents)
                if (a.Behaviour == state) count++;
            sb.Append(ColorPalette.DimGray); sb.Append($"{label}:"); sb.Append(ColorPalette.Reset);
            sb.Append($"{count,3} ");
        }
    }
}
