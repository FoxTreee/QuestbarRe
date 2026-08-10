public sealed class HeroTargetScore
{
    public MonsterActorController Target
    { get; init; } = null!;

    public float LowestHealthScore
    { get; init; }

    public float HighestHealthScore
    { get; init; }

    public float DangerScore
    { get; init; }

    public float CoverageScore
    { get; init; }

    public float HealthyAllySupportScore
    { get; init; }

    public float SaturationPenalty
    { get; init; }

    public float AggroScore
    { get; init; }

    public float CurrentTargetScore
    { get; init; }

    public float TotalScore
    { get; init; }

    public int OtherHeroAttackerCount
    { get; init; }

    public int PreferredHeroAttackerCount
    { get; init; }
}
