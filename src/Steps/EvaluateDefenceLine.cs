using EvalApp.Consumer;

using NavPathfinder.Demo.Data;

namespace NavPathfinder.Demo.Steps;

/// <summary>
/// Evaluates whether the defence line should retreat to the next inner layer.
/// Triggers when a threshold of gates on the current layer are breached.
/// </summary>
public sealed class EvaluateDefenceLine : PureStep<SiegeTickData>
{
    public override SiegeTickData Execute(SiegeTickData data)
    {
        var currentLine = data.ActiveDefenceLine;
        if (currentLine == CastleLayer.Keep)
            return data; // Can't retreat further

        var gates = data.Gates;
        float threshold = data.Config.RetreatThreshold;

        int totalOnLayer = 0, breachedOnLayer = 0;
        foreach (var gate in gates)
        {
            if (gate.Layer != currentLine) continue;
            totalOnLayer++;
            if (gate.Status == GateStatus.Breached)
                breachedOnLayer++;
        }

        if (totalOnLayer == 0)
            return data;

        float breachRatio = (float)breachedOnLayer / totalOnLayer;

        if (breachRatio >= threshold)
        {
            var nextLine = currentLine switch
            {
                CastleLayer.OuterCurtain => CastleLayer.InnerBailey,
                CastleLayer.InnerBailey => CastleLayer.Keep,
                _ => CastleLayer.Keep
            };
            return data with { ActiveDefenceLine = nextLine };
        }

        return data;
    }
}
