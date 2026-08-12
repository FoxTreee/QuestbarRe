using Godot;

/// <summary>
/// Captures where a status effect came from without making the status state
/// responsible for deciding what that source means.
/// </summary>
public sealed class CombatStatusEffectApplicationContext
{
    public CombatStatusEffectApplicationContext(
        Node2D? sourceActor,
        Vector2 originPosition)
    {
        SourceActor = sourceActor;
        OriginPosition = originPosition;
    }

    public Node2D? SourceActor { get; }

    public Vector2 OriginPosition { get; }

    public static CombatStatusEffectApplicationContext None { get; } =
        new(null, Vector2.Zero);
}
