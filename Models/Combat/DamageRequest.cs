using Godot;

public readonly struct DamageRequest
{
    public Node Source { get; }

    public Node Target { get; }

    public float RequestedDamage { get; }

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
