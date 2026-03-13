using System.Collections.Immutable;
using EvalApp.Consumer;

using NavPathfinder.Demo.Data;
using NavPathfinder.Demo.Data.Combat;

namespace NavPathfinder.Demo.Steps.Combat;

public sealed class ApplyPendingDamageStep : PureStep<SiegeTickData>
{
    // Damage type effectiveness — universal RPG mechanics
    // Blunt: baseline, good vs all
    // Slash: reduced vs heavy armor (plate absorbs cuts)
    // Pierce: chain attack — damage falls off per target (handled by engine budget)
    // Fire: AoE — ignores physical armor but reduced vs magic resistance
    private static float DamageTypeMultiplier(DamageType type, float armorReduction)
    {
        if (armorReduction <= 0f) return 1f;

        return type switch
        {
            DamageType.Slash => armorReduction >= 5f ? 0.7f : 1f,
            DamageType.Pierce => 0.9f,  // slight falloff per chain target
            DamageType.Fire => 1.4f,    // strong vs physical armor
            _ => 1f                     // Blunt baseline
        };
    }

    // --- Kill-Based Leveling ---
    // Every kill: full heal to new max HP, +random stat, guaranteed toughness bump.
    // Kills are the ONLY way to heal. Random stat picks create emergent class variance.
    private static readonly string[] StatNames = ["STR", "TGH", "AGI", "PRC", "Melee", "Block", "Dodge", "Ranged"];
    private const float StatBoost = 2.0f;        // base stat gain per level
    private const float SkillBoost = 0.3f;       // combat skill gain per level
    private const float ToughnessPerKill = 0.5f;  // guaranteed HP growth every kill

    public override SiegeTickData Execute(SiegeTickData data)
    {
        if (data.PendingDamage.IsDefaultOrEmpty)
            return data;

        // Build agent lookup for armor checks and roles
        var armorLookup = new Dictionary<int, float>();
        var roleLookup = new Dictionary<int, AgentRole>();
        var gearLookup = new Dictionary<int, Equipment>();
        foreach (var a in data.Invaders) { armorLookup[a.Id] = a.Gear.ArmorReduction; roleLookup[a.Id] = a.Role; gearLookup[a.Id] = a.Gear; }
        foreach (var a in data.Defenders) { armorLookup[a.Id] = a.Gear.ArmorReduction; roleLookup[a.Id] = a.Role; gearLookup[a.Id] = a.Gear; }
        foreach (var a in data.Civilians) { roleLookup[a.Id] = a.Role; }

        // Apply damage type multipliers, aggregate, and track biggest hit per target
        var damageByTarget = new Dictionary<int, float>();
        var killingBlow = new Dictionary<int, PendingDamage>(); // biggest single hit per target
        foreach (var pd in data.PendingDamage)
        {
            float armor = armorLookup.GetValueOrDefault(pd.TargetId, 0f);
            float multiplier = DamageTypeMultiplier(pd.Type, armor);
            float adjusted = pd.Amount * multiplier;
            damageByTarget[pd.TargetId] = damageByTarget.GetValueOrDefault(pd.TargetId) + adjusted;
            if (!killingBlow.TryGetValue(pd.TargetId, out var prev) || adjusted > prev.Amount)
                killingBlow[pd.TargetId] = pd;
        }

        var events = ImmutableArray.CreateBuilder<CombatEvent>();
        var tick = data.TickNumber;

        var invaders = ApplyDamageToAgents(data.Invaders, damageByTarget, killingBlow, roleLookup, tick, events);
        var defenders = ApplyDamageToAgents(data.Defenders, damageByTarget, killingBlow, roleLookup, tick, events);

        // Track which agents scored kills for level-up
        var killedIds = new HashSet<int>();
        foreach (var agent in data.Invaders)
            if (!invaders.Any(a => a.Id == agent.Id)) killedIds.Add(agent.Id);
        foreach (var agent in data.Defenders)
            if (!defenders.Any(a => a.Id == agent.Id)) killedIds.Add(agent.Id);

        // Emit mutual kill events
        foreach (var pd in data.PendingDamage)
        {
            if (killedIds.Contains(pd.TargetId) && killedIds.Contains(pd.SourceId))
            {
                events.Add(new CombatEvent(tick, CombatEventKind.MutualKill, pd.SourceId,
                    pd.TargetId, Detail: "Mutual kill!",
                    SourceRole: roleLookup.GetValueOrDefault(pd.SourceId),
                    VictimRole: roleLookup.GetValueOrDefault(pd.TargetId),
                    WeaponType: pd.Type));
            }
        }

        // Track who scored kills for kill-milestone bonus growth
        var killers = new HashSet<int>();
        foreach (var evt in events)
        {
            if (evt.Kind == CombatEventKind.Kill && evt.TargetId.HasValue)
                killers.Add(evt.TargetId.Value); // TargetId = killer's id in Kill events
        }
        // Also check SourceId in PendingDamage for agents whose targets died
        foreach (var pd in data.PendingDamage)
        {
            if (killedIds.Contains(pd.TargetId) && pd.Amount > 0)
                killers.Add(pd.SourceId);
        }

        var updatedInvaders = LevelUpKillers(invaders, killers, tick, events);
        var updatedDefenders = LevelUpKillers(defenders, killers, tick, events);

        var allEvents = data.CombatEvents.IsDefaultOrEmpty
            ? events.ToImmutable()
            : data.CombatEvents.AddRange(events);

        return data with
        {
            Invaders = updatedInvaders,
            Defenders = updatedDefenders,
            PendingDamage = ImmutableArray<PendingDamage>.Empty,
            CombatEvents = allEvents
        };
    }

    private static ImmutableArray<SimAgent> ApplyDamageToAgents(
        ImmutableArray<SimAgent> agents,
        Dictionary<int, float> damageByTarget,
        Dictionary<int, PendingDamage> killingBlow,
        Dictionary<int, AgentRole> roleLookup,
        int tick,
        ImmutableArray<CombatEvent>.Builder events)
    {
        var result = ImmutableArray.CreateBuilder<SimAgent>(agents.Length);
        foreach (var agent in agents)
        {
            if (damageByTarget.TryGetValue(agent.Id, out float damage))
            {
                float newHealth = agent.Health - damage;
                if (newHealth <= 0)
                {
                    var blow = killingBlow.GetValueOrDefault(agent.Id);
                    var attackerRole = blow is not null ? roleLookup.GetValueOrDefault(blow.SourceId) : AgentRole.Civilian;
                    var weaponType = blow?.Type ?? DamageType.Blunt;
                    events.Add(new CombatEvent(tick, CombatEventKind.Kill, agent.Id,
                        TargetId: blow?.SourceId,
                        Damage: damage,
                        SourceRole: attackerRole,
                        VictimRole: agent.Role,
                        WeaponType: weaponType));
                    continue;
                }
                result.Add(agent with
                {
                    Health = newHealth,
                    Combat = agent.Combat with { DamageTakenThisTick = damage }
                });
            }
            else
            {
                result.Add(agent with
                {
                    Combat = agent.Combat with { DamageTakenThisTick = 0f }
                });
            }
        }
        return result.ToImmutable();
    }

    /// Kill-based leveling: each kill = level up, full heal, max HP boost, one random stat.
    /// Random stat picks over many kills create organic class divergence.
    private static ImmutableArray<SimAgent> LevelUpKillers(
        ImmutableArray<SimAgent> agents,
        HashSet<int> killers,
        int tick,
        ImmutableArray<CombatEvent>.Builder events)
    {
        var result = ImmutableArray.CreateBuilder<SimAgent>(agents.Length);
        foreach (var agent in agents)
        {
            if (!killers.Contains(agent.Id))
            {
                result.Add(agent);
                continue;
            }

            var skills = agent.Skills;
            var stats = agent.Stats;
            int newLevel = skills.Level + 1;
            int newKills = skills.KillCount + 1;

            // Random stat boost — deterministic per agent per kill
            bool isRanged = agent.Gear.DamageType is DamageType.Pierce or DamageType.Fire;
            int poolSize = isRanged ? 8 : 7;
            var rng = new Random(agent.Id * 31 + tick + newKills);
            int pick = rng.Next(poolSize);

            var newStats = stats;
            float newMelee = skills.MeleeSkill;
            float newBlock = skills.BlockSkill;
            float newDodge = skills.DodgeSkill;
            float newRanged = skills.RangedSkill;

            switch (pick)
            {
                case 0: newStats = newStats with { Strength = stats.Strength + StatBoost }; break;
                case 1: newStats = newStats with { Toughness = stats.Toughness + StatBoost }; break;
                case 2: newStats = newStats with { Agility = stats.Agility + StatBoost }; break;
                case 3: newStats = newStats with { Precision = stats.Precision + StatBoost }; break;
                case 4: newMelee += SkillBoost; break;
                case 5: newBlock += SkillBoost; break;
                case 6: newDodge += SkillBoost; break;
                case 7: newRanged += SkillBoost; break;
            }

            // Guaranteed toughness bump every kill — always some HP growth
            newStats = newStats with { Toughness = newStats.Toughness + ToughnessPerKill };
            float newMaxHealth = newStats.Toughness * 10f;

            events.Add(new CombatEvent(tick, CombatEventKind.SkillLevelUp, agent.Id,
                Detail: $"Lv{newLevel} +{StatNames[pick]} HP:{newMaxHealth:F0}",
                SourceRole: agent.Role));

            result.Add(agent with
            {
                Stats = newStats,
                MaxHealth = newMaxHealth,
                Health = newMaxHealth, // full heal — kills are the ONLY way to heal
                Skills = new AgentSkills(
                    MeleeSkill: newMelee,
                    BlockSkill: newBlock,
                    DodgeSkill: newDodge,
                    RangedSkill: newRanged,
                    Level: newLevel,
                    KillCount: newKills)
            });
        }
        return result.ToImmutable();
    }
}
