using Godot;

public readonly struct CombatHitRequest
{
    public Node Source { get; }
    public Node Target { get; }
    public CombatDodgeRule DodgeRule { get; }
    public AbilityDefinition? Ability { get; }

    public CombatHitRequest(
        Node source,
        Node target,
        CombatDodgeRule dodgeRule,
        AbilityDefinition? ability = null)
    {
        Source = source;
        Target = target;
        DodgeRule = dodgeRule;
        Ability = ability;
    }
}
