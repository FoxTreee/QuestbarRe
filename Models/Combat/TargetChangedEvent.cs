using Godot;

public sealed class TargetChangedEvent
{
    public Node Actor { get; init; } = null!;
    public Node? PreviousTarget { get; init; }
    public Node? CurrentTarget { get; init; }
}
