public sealed class HeroTargetDecision
{
    public MonsterActorController? SelectedTarget
    { get; init; }

    public int ValidCandidateCount
    { get; init; }

    public string SelectionRule
    { get; init; } = string.Empty;

    public float SelectedRuleValue
    { get; init; }

    public HeroTargetScore? SelectedScore
    { get; init; }

    public HeroTargetScore? CurrentTargetScore
    { get; init; }

    public HeroActorController? RescueAlly
    { get; init; }

    public float RescueAllyHealthPercent
    { get; init; }

    public int RescuePressure
    { get; init; }

    public int OtherHeroAttackerCount
    { get; init; }

    public int PreferredHeroAttackerCount
    { get; init; }
}
