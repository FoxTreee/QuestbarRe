public readonly struct DamageResult
{
    public float RequestedDamage { get; }

    public float AppliedDamage { get; }

    public float RemainingHealth { get; }

    public bool WasLethal { get; }

    /// <summary>
    /// Performs the damage result operation for Damage Result.
    /// Uses the supplied arguments and current state and returns the resulting damage result to the caller.
    /// </summary>
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