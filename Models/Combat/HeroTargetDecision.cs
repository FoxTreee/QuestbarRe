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
}
