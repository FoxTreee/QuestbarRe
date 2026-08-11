using Godot;

public readonly struct DamageRequest
{
    public Node Source { get; }

    public Node Target { get; }

    public float RequestedDamage { get; }

    /// <summary>
    /// Performs the damage request operation for Damage Request.
    /// Uses the supplied arguments and current state and returns the resulting damage request to the caller.
    /// </summary>
    public DamageRequest(
        Node source,
        Node target,
        float requestedDamage)
    {
        Source = source;
        Target = target;
        RequestedDamage = requestedDamage;
    }
}
