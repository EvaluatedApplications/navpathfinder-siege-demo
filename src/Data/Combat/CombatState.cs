namespace NavPathfinder.Demo.Data.Combat;

public record CombatState(
    float CooldownRemaining = 0f,
    int ConsecutiveHits = 0,
    float DamageTakenThisTick = 0f);
