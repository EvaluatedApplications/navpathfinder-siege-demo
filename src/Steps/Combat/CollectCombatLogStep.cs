using System.Collections.Immutable;
using EvalApp.Consumer;

using NavPathfinder.Demo.Data;
using NavPathfinder.Demo.Data.Combat;

namespace NavPathfinder.Demo.Steps.Combat;

public sealed class CollectCombatLogStep : PureStep<SiegeTickData>
{
    private const int MaxEventsPerTick = 20;

    public override SiegeTickData Execute(SiegeTickData data)
    {
        if (data.CombatEvents.IsDefaultOrEmpty)
            return data;

        var memorable = ImmutableArray.CreateBuilder<CombatEvent>();
        foreach (var evt in data.CombatEvents)
        {
            if (IsMemorableEvent(evt))
            {
                memorable.Add(evt);
                if (memorable.Count >= MaxEventsPerTick)
                    break;
            }
        }

        return data with
        {
            CombatEvents = memorable.Count > 0 ? memorable.ToImmutable() : ImmutableArray<CombatEvent>.Empty
        };
    }

    private static bool IsMemorableEvent(CombatEvent evt) => evt.Kind switch
    {
        CombatEventKind.Kill => true,
        CombatEventKind.CriticalHit => true,
        CombatEventKind.NarrowDodge => true,
        CombatEventKind.BlockedKillingBlow => true,
        CombatEventKind.SkillLevelUp => true,
        CombatEventKind.MutualKill => true,
        CombatEventKind.LastStandHeroics => true,
        _ => false
    };
}
