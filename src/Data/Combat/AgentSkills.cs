namespace NavPathfinder.Demo.Data.Combat;

public record AgentSkills(
    float MeleeSkill = 1f,
    float BlockSkill = 1f,
    float DodgeSkill = 1f,
    float RangedSkill = 1f,
    int Level = 0,
    int KillCount = 0);
