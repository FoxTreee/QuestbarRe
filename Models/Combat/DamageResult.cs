public readonly struct DamageResult
{
    public float RequestedDamage { get; }

    public float AppliedDamage { get; }

    public float RemainingHealth { get; }

    public bool WasLethal { get; }

    public DamageResult(
        float requestedDamage,
        float appliedDamage,
        float remainingHealth,
        bool wasLethal)
    {
        RequestedDamage = requestedDamage;
        AppliedDamage = appliedDamage;
        RemainingHealth = remainingHealth;
        WasLethal = wasLethal;
    }
}