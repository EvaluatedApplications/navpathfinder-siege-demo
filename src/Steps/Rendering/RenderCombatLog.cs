using EvalApp.Consumer;

using NavPathfinder.Demo.Data;
using NavPathfinder.Demo.Data.Combat;
using NavPathfinder.Demo.Rendering;

namespace NavPathfinder.Demo.Steps.Rendering;

/// <summary>
/// Renders one narrative combat headline — tells a story about the last memorable event.
/// Sticky: persists between quiet ticks so the line is always visible once combat starts.
/// </summary>
public sealed class RenderCombatLog : PureStep<SiegeTickData>
{
    private static string? _lastHeadline;

    private static readonly HashSet<CombatEventKind> _memorable = new()
    {
        CombatEventKind.Kill,
        CombatEventKind.MutualKill,
        CombatEventKind.BlockedKillingBlow,
        CombatEventKind.LastStandHeroics,
        CombatEventKind.SkillLevelUp,
    };

    public override SiegeTickData Execute(SiegeTickData data)
    {
        var sb = data.RenderBuffer!.StringBuilder;

        if (!data.CombatEvents.IsDefaultOrEmpty)
        {
            // Build level-up lookup by agent id so we can merge with kills
            var levelUps = new Dictionary<int, CombatEvent>();
            foreach (var evt in data.CombatEvents)
            {
                if (evt.Kind == CombatEventKind.SkillLevelUp)
                    levelUps[evt.AgentId] = evt;
            }

            // Find most recent kill and merge with its level-up
            for (int i = data.CombatEvents.Length - 1; i >= 0; i--)
            {
                var evt = data.CombatEvents[i];
                if (!_memorable.Contains(evt.Kind) || evt.Kind == CombatEventKind.SkillLevelUp)
                    continue;
                var line = Narrate(evt);
                if (line is null) continue;

                // If this is a kill, append the killer's level-up
                if (evt.Kind is CombatEventKind.Kill or CombatEventKind.MutualKill
                    && evt.TargetId.HasValue && levelUps.TryGetValue(evt.TargetId.Value, out var lvl))
                {
                    line += $" {ColorPalette.BoldYellow}▲{ColorPalette.Reset} {ColorPalette.BoldWhite}{lvl.Detail}{ColorPalette.Reset}";
                }

                _lastHeadline = line;
                break;
            }
        }

        if (_lastHeadline is not null)
        {
            sb.Append("  ");
            sb.Append(ColorPalette.DimGray);
            sb.Append("⚔ ");
            sb.Append(ColorPalette.Reset);
            sb.Append(_lastHeadline);
            sb.Append(ColorPalette.Reset);
            sb.Append(ColorPalette.Eol);
            sb.AppendLine();
        }

        return data;
    }

    private static string? Narrate(CombatEvent evt) => evt.Kind switch
    {
        CombatEventKind.Kill => NarrateKill(evt),
        CombatEventKind.MutualKill => NarrateMutualKill(evt),
        CombatEventKind.BlockedKillingBlow => NarrateBlock(evt),
        CombatEventKind.LastStandHeroics => NarrateLastStand(evt),
        CombatEventKind.SkillLevelUp => NarrateLevelUp(evt),
        _ => null
    };

    private static string NarrateKill(CombatEvent evt)
    {
        string killer = RoleName(evt.SourceRole);
        string victim = RoleName(evt.VictimRole);
        string kc = RoleColor(evt.SourceRole);
        string vc = RoleColor(evt.VictimRole);
        string weapon = WeaponVerb(evt.WeaponType);
        string dmg = evt.Damage.HasValue ? $" {ColorPalette.DimGray}({evt.Damage.Value:F0} dmg)" : "";

        return $"{ColorPalette.BoldRed}☠{ColorPalette.Reset} {kc}{killer}{ColorPalette.Reset} {weapon} a {vc}{victim}{ColorPalette.Reset}{dmg}";
    }

    private static string NarrateMutualKill(CombatEvent evt)
    {
        string a = RoleName(evt.SourceRole);
        string b = RoleName(evt.VictimRole);
        string ac = RoleColor(evt.SourceRole);
        string bc = RoleColor(evt.VictimRole);

        return $"{ColorPalette.BoldMagenta}⚔{ColorPalette.Reset} {ac}{a}{ColorPalette.Reset} and {bc}{b}{ColorPalette.Reset} traded fatal blows — neither survived";
    }

    private static string NarrateBlock(CombatEvent evt)
    {
        // VictimRole = the one who BLOCKED (defender of the blow), SourceRole = attacker
        string blocker = RoleName(evt.VictimRole);
        string attacker = RoleName(evt.SourceRole);
        string bc = RoleColor(evt.VictimRole);
        string ac = RoleColor(evt.SourceRole);
        string weapon = WeaponNoun(evt.WeaponType);

        return $"{ColorPalette.BoldGreen}🛡{ColorPalette.Reset} {bc}{blocker}{ColorPalette.Reset} turned aside a {ac}{attacker}{ColorPalette.Reset}'s {weapon} — barely alive!";
    }

    private static string NarrateLastStand(CombatEvent evt)
    {
        string hero = RoleName(evt.VictimRole);
        string hc = RoleColor(evt.VictimRole);

        return $"{ColorPalette.BoldWhite}⚡{ColorPalette.Reset} A wounded {hc}{hero}{ColorPalette.Reset} refuses to fall — fighting on with fury!";
    }

    private static string WeaponVerb(DamageType type) => type switch
    {
        DamageType.Slash => "cut down",
        DamageType.Pierce => "ran through",
        DamageType.Fire => "burned down",
        _ => "bludgeoned"
    };

    private static string WeaponNoun(DamageType type) => type switch
    {
        DamageType.Slash => "blade",
        DamageType.Pierce => "spear",
        DamageType.Fire => "flames",
        _ => "mace"
    };

    private static string NarrateLevelUp(CombatEvent evt)
    {
        string role = RoleName(evt.SourceRole);
        string rc = RoleColor(evt.SourceRole);
        string detail = evt.Detail ?? "leveled up";

        return $"{ColorPalette.BoldYellow}▲{ColorPalette.Reset} {rc}{role}{ColorPalette.Reset} {ColorPalette.BoldWhite}{detail}{ColorPalette.Reset}";
    }

    private static string RoleName(AgentRole role) => role switch
    {
        AgentRole.Invader => "invader",
        AgentRole.Defender => "defender",
        AgentRole.Civilian => "civilian",
        _ => "fighter"
    };

    private static string RoleColor(AgentRole role) => role switch
    {
        AgentRole.Invader => ColorPalette.InvColor,
        AgentRole.Defender => ColorPalette.DefColor,
        AgentRole.Civilian => ColorPalette.CivColor,
        _ => ColorPalette.DimGray
    };
}
