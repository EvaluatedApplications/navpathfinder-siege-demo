using System.Collections.Immutable;
using System.Numerics;
using EvalApp.Consumer;

using NavPathfinder.Demo.Data;
using NavPathfinder.Demo.Data.Combat;
using NavPathfinder.Demo.Simulation;
using NavPathfinder.Sdk.Abstractions;
using NavPathfinder.Sdk.Integration;
using NavPathfinder.Sdk.Models;

namespace NavPathfinder.Demo.Steps;

/// <summary>
/// DF-style combat resolver using <see cref="IEngagementService.ResolveOutcomesAsync{TAgent}"/>
/// (Tier 3) with typed <see cref="SimAgent"/> access. No mapping needed — the resolver
/// receives SimAgent directly via record inheritance from GameAgent.
/// </summary>
public sealed class ResolveCombat : ContextPureStep<NavPathfinderContext, SiegeDomainContext, SiegeTickData>
{
    protected override async ValueTask<SiegeTickData> TransformAsync(
        SiegeTickData data,
        NavPathfinderContext global,
        SiegeDomainContext domain,
        CancellationToken ct)
    {
        if (data.Invaders.Length == 0 || data.Defenders.Length == 0)
            return data;

        var cfg = data.Config ?? SiegeBalanceConfig.Default;

        // Recompute combat stats from Stats×Skills×Gear×Morale before resolution
        var attackers = RecomputeCombatStats(data.Invaders, data.InvaderMorale);
        var defenders = RecomputeCombatStats(data.Defenders, data.DefenderMorale);

        // SimAgent : GameAgent — SDK receives typed agents directly (no mapping)
        // Capture layout + gates for LOS checking inside the resolver closure
        var layout = data.Castle;
        var gates = data.Gates;
        var outcomes = await domain.EngagementService.ResolveOutcomesAsync(
            (IReadOnlyList<SimAgent>)attackers, (IReadOnlyList<SimAgent>)defenders,
            cfg.CombatRadius, (a, d, dist, math) => DfResolver(a, d, dist, math, layout, gates), ct);

        if (outcomes.Length == 0)
            return TickCooldowns(data with { Invaders = attackers, Defenders = defenders });

        var pending = ImmutableArray.CreateBuilder<PendingDamage>(outcomes.Length);
        var events = ImmutableArray.CreateBuilder<CombatEvent>();
        var tick = data.TickNumber;
        var hitsPerSource = new Dictionary<int, int>();

        // Build lookup for post-processing (BlockedKillingBlow detection)
        var agentLookup = new Dictionary<int, SimAgent>(attackers.Length + defenders.Length);
        foreach (var a in attackers) agentLookup[a.Id] = a;
        foreach (var a in defenders) agentLookup[a.Id] = a;

        foreach (var o in outcomes)
        {
            if (o.Hit)
            {
                // DamageTypeId flows through the outcome — set by the resolver
                var damageType = (DamageType)o.DamageTypeId;
                pending.Add(new PendingDamage(o.TargetId, o.Damage, damageType, o.SourceId));
                hitsPerSource[o.SourceId] = hitsPerSource.GetValueOrDefault(o.SourceId) + 1;

                if (o.Crit || o.Kind == OutcomeKind.CriticalHit)
                    events.Add(new CombatEvent(tick, CombatEventKind.CriticalHit, o.SourceId,
                        o.TargetId, o.Damage, "Critical strike!"));
            }
            else
            {
                switch (o.Kind)
                {
                    case OutcomeKind.Dodge:
                        events.Add(new CombatEvent(tick, CombatEventKind.NarrowDodge, o.TargetId,
                            o.SourceId, Detail: "Dodged!"));
                        break;
                    case OutcomeKind.Block:
                        if (agentLookup.TryGetValue(o.TargetId, out var target) && target.Health < 20f)
                        {
                            var attacker = agentLookup.GetValueOrDefault(o.SourceId);
                            events.Add(new CombatEvent(tick, CombatEventKind.BlockedKillingBlow, o.TargetId,
                                o.SourceId, Detail: "Blocked a killing blow!",
                                SourceRole: attacker?.Role ?? AgentRole.Civilian,
                                VictimRole: target.Role,
                                WeaponType: (DamageType)o.DamageTypeId));
                        }
                        break;
                    default:
                        if (Random.Shared.NextSingle() < 0.10f)
                            events.Add(new CombatEvent(tick, CombatEventKind.NarrowDodge, o.TargetId,
                                o.SourceId, Detail: "Narrowly dodged!"));
                        break;
                }
            }
        }

        var updatedInvaders = UpdateConsecutiveHits(attackers, hitsPerSource);
        var updatedDefenders = UpdateConsecutiveHits(defenders, hitsPerSource);

        var allPending = data.PendingDamage.IsDefaultOrEmpty
            ? pending.ToImmutable()
            : data.PendingDamage.AddRange(pending);

        var allEvents = data.CombatEvents.IsDefaultOrEmpty
            ? events.ToImmutable()
            : data.CombatEvents.AddRange(events);

        return TickCooldowns(data with
        {
            Invaders = updatedInvaders,
            Defenders = updatedDefenders,
            PendingDamage = allPending,
            CombatEvents = allEvents
        });
    }

    /// <summary>
    /// DF-style combat resolver with direct SimAgent access.
    /// Stats × Skills × Equipment drive hit/damage/crit/dodge/block.
    /// DamageTypeId propagates through the outcome for type-based armor interactions.
    /// </summary>
    private static IEnumerable<EngagementOutcome> DfResolver(
        SimAgent attacker, SimAgent defender, float distance, IGameMath math,
        CastleLayout layout, ImmutableArray<GateState> gates)
    {
        // Ranged attacks require line-of-sight — can't shoot through walls
        bool attackerIsRanged = attacker.Gear.DamageType is DamageType.Pierce or DamageType.Fire;
        bool defenderIsRanged = defender.Gear.DamageType is DamageType.Pierce or DamageType.Fire;

        // Ranged LOS: defenders can shoot outward from walls (defender advantage),
        // but invaders cannot shoot through walls inward.
        bool attackerHasLos = !attackerIsRanged ||
            attacker.Role == AgentRole.Defender ||
            SiegeWorld.HasLineOfSight(attacker.Position, defender.Position, layout, gates);
        bool defenderHasLos = !defenderIsRanged ||
            defender.Role == AgentRole.Defender ||
            SiegeWorld.HasLineOfSight(defender.Position, attacker.Position, layout, gates);

        // Attacker → Defender (skip if ranged with no LOS)
        if (attackerHasLos)
        {
        float attackChance = MathF.Min(1f, attacker.AttackSpeed * 0.5f);
        if (Random.Shared.NextSingle() <= attackChance)
        {
            float hitChance = math.HitChance(attacker.Attack, defender.Defense, distance, attacker.AttackRange);
            if (Random.Shared.NextSingle() < hitChance)
            {
                // Dodge — defender's agility × dodge skill (direct property access)
                float dodgeChance = math.DodgeChance(defender.Stats.Agility, defender.Skills.DodgeSkill);
                if (Random.Shared.NextSingle() < dodgeChance)
                {
                    yield return EngagementOutcome.Dodged(attacker.Id, defender.Id);
                }
                else
                {
                    // Block — defender's toughness × block skill
                    float blockChance = math.BlockChance(defender.Stats.Toughness, defender.Skills.BlockSkill);
                    if (Random.Shared.NextSingle() < blockChance)
                    {
                        yield return EngagementOutcome.Blocked(attacker.Id, defender.Id);
                    }
                    else
                    {
                        float dmg = math.DamageRoll(attacker.Attack, defender.Defense);
                        // Apply damage type modifier (resistance/weakness)
                        dmg *= math.DamageModifier((int)attacker.Gear.DamageType, defender.Gear.ArmorReduction);
                        bool crit = math.IsCritical(attacker.Attack, defender.Defense);
                        if (crit) dmg = math.CritDamage(dmg);
                        yield return new EngagementOutcome(attacker.Id, defender.Id, dmg, true, crit,
                            crit ? OutcomeKind.CriticalHit : OutcomeKind.Hit,
                            DamageTypeId: (int)attacker.Gear.DamageType);
                    }
                }
            }
            else
            {
                yield return EngagementOutcome.Miss(attacker.Id, defender.Id);
            }
        }
        } // attackerHasLos

        // Defender counter-attack (skip if ranged with no LOS)
        if (defenderHasLos)
        {
        float defAttackChance = MathF.Min(1f, defender.AttackSpeed * 0.5f);
        if (Random.Shared.NextSingle() <= defAttackChance)
        {
            float hitChance = math.HitChance(defender.Attack, attacker.Defense, distance, defender.AttackRange);
            if (Random.Shared.NextSingle() < hitChance)
            {
                float dodgeChance = math.DodgeChance(attacker.Stats.Agility, attacker.Skills.DodgeSkill);
                if (Random.Shared.NextSingle() < dodgeChance)
                {
                    yield return EngagementOutcome.Dodged(defender.Id, attacker.Id);
                }
                else
                {
                    float blockChance = math.BlockChance(attacker.Stats.Toughness, attacker.Skills.BlockSkill);
                    if (Random.Shared.NextSingle() < blockChance)
                    {
                        yield return EngagementOutcome.Blocked(defender.Id, attacker.Id);
                    }
                    else
                    {
                        float dmg = math.DamageRoll(defender.Attack, attacker.Defense);
                        dmg *= math.DamageModifier((int)defender.Gear.DamageType, attacker.Gear.ArmorReduction);
                        bool crit = math.IsCritical(defender.Attack, attacker.Defense);
                        if (crit) dmg = math.CritDamage(dmg);
                        yield return new EngagementOutcome(defender.Id, attacker.Id, dmg, true, crit,
                            crit ? OutcomeKind.CriticalHit : OutcomeKind.Hit,
                            DamageTypeId: (int)defender.Gear.DamageType);
                    }
                }
            }
            else
            {
                yield return EngagementOutcome.Miss(defender.Id, attacker.Id);
            }
        }
        } // defenderHasLos
    }

    /// <summary>
    /// Recompute Attack/Defense/AttackSpeed/MaxAttacksPerTick from Stats×Skills×Gear×Morale.
    /// This runs each tick so skill growth and morale changes take immediate effect.
    /// </summary>
    private static ImmutableArray<SimAgent> RecomputeCombatStats(
        ImmutableArray<SimAgent> agents, float morale)
    {
        var result = ImmutableArray.CreateBuilder<SimAgent>(agents.Length);
        foreach (var agent in agents)
        {
            var stats = agent.Stats;
            var skills = agent.Skills;
            var gear = agent.Gear;

            float precisionBonus = 1f + (stats.Precision - 10f) * 0.02f;
            float attack = (stats.Strength + gear.WeaponDamage) * skills.MeleeSkill * morale * precisionBonus;
            float defense = (stats.Toughness + gear.ArmorReduction) * skills.BlockSkill
                          + stats.Agility * skills.DodgeSkill * 0.3f;
            float baseSpeed = stats.Agility * 0.1f * skills.MeleeSkill;
            float attackSpeed = baseSpeed / MathF.Max(gear.AttackCooldownSec, 0.1f);

            int maxTargets = gear.DamageType switch
            {
                DamageType.Blunt  => 1,
                DamageType.Slash  => 1,
                DamageType.Pierce => 3,
                DamageType.Fire   => 999,
                _ => 1
            };

            // Range scales with damage type — 1 tile ≈ 1 room
            float attackRange = gear.DamageType switch
            {
                DamageType.Pierce => 1.2f + skills.RangedSkill * 0.05f,  // arrows: next room
                DamageType.Fire   => 1.3f + skills.RangedSkill * 0.08f,  // magic: 1-2 rooms
                _ => 1.0f // melee (Blunt/Slash) — same room / doorway
            };

            result.Add(agent with
            {
                Attack = attack,
                Defense = defense,
                MaxHealth = stats.Toughness * 10f,
                AttackSpeed = attackSpeed,
                AttackRange = attackRange,
                MaxAttacksPerTick = maxTargets
            });
        }
        return result.ToImmutable();
    }

    private static ImmutableArray<SimAgent> UpdateConsecutiveHits(
        ImmutableArray<SimAgent> agents, Dictionary<int, int> hitsPerSource)
    {
        if (hitsPerSource.Count == 0) return agents;

        var result = ImmutableArray.CreateBuilder<SimAgent>(agents.Length);
        foreach (var agent in agents)
        {
            if (hitsPerSource.TryGetValue(agent.Id, out int hits))
            {
                result.Add(agent with
                {
                    Combat = agent.Combat with
                    {
                        ConsecutiveHits = agent.Combat.ConsecutiveHits + hits
                    }
                });
            }
            else
            {
                if (agent.Combat.ConsecutiveHits > 0)
                    result.Add(agent with { Combat = agent.Combat with { ConsecutiveHits = 0 } });
                else
                    result.Add(agent);
            }
        }
        return result.ToImmutable();
    }

    private static SiegeTickData TickCooldowns(SiegeTickData data)
    {
        var invaders = TickAgentCooldowns(data.Invaders);
        var defenders = TickAgentCooldowns(data.Defenders);
        return data with { Invaders = invaders, Defenders = defenders };
    }

    private static ImmutableArray<SimAgent> TickAgentCooldowns(ImmutableArray<SimAgent> agents)
    {
        var result = ImmutableArray.CreateBuilder<SimAgent>(agents.Length);
        foreach (var agent in agents)
        {
            if (agent.Combat.CooldownRemaining > 0)
            {
                result.Add(agent with
                {
                    Combat = agent.Combat with
                    {
                        CooldownRemaining = agent.Combat.CooldownRemaining - 1
                    }
                });
            }
            else
            {
                result.Add(agent);
            }
        }
        return result.ToImmutable();
    }
}
