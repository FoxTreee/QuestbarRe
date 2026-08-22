using Godot;

public sealed class CombatEvent
{
    public CombatEventType Type { get; init; }

    public Node Attacker { get; init; } = null!;

    public Node Target { get; init; } = null!;

    public DamageResult Damage { get; init; }

    public float AppliedHealing { get; init; }
}
