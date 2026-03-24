namespace NavPathfinder.Demo.Data.Combat;

public enum CombatEventKind
{
    Kill, CriticalHit, NarrowDodge, BlockedKillingBlow,
    SkillLevelUp, AoeMultiKill, LastStandHeroics, MutualKill
}

public record CombatEvent(
    int Tick,
    CombatEventKind Kind,
    int AgentId,
    int? TargetId = null,
    float? Damage = null,
    string? Detail = null,
    AgentRole SourceRole = AgentRole.Civilian,
    AgentRole VictimRole = AgentRole.Civilian,
    DamageType WeaponType = DamageType.Blunt);
