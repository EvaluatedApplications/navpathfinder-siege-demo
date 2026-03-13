namespace NavPathfinder.Demo.Data.Combat;

public enum DamageType { Blunt, Slash, Pierce, Fire }

public record Equipment(
    DamageType DamageType = DamageType.Blunt,
    float WeaponDamage = 5f,
    float ArmorReduction = 0f,
    float AttackCooldownSec = 1.0f);
