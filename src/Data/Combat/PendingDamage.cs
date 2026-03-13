namespace NavPathfinder.Demo.Data.Combat;

public record PendingDamage(int TargetId, float Amount, DamageType Type, int SourceId);
