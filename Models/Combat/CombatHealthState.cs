using System;

public sealed class CombatHealthState
{
    public event Action<float, float>? HealthChanged;

    public float MaximumHealth { get; private set; }

    public float CurrentHealth { get; private set; }

    public bool IsInitialized { get; private set; }

    public bool IsAlive =>
        CurrentHealth > 0.0f;

    /// <summary>
    /// Performs the initialize operation for Combat Health State.
    /// Uses the supplied arguments and current node state; any result is applied through side effects, events, or stored fields.
    /// </summary>
    public void Initialize(float maximumHealth)
    {
        if (IsInitialized)
            return;

        MaximumHealth = MathF.Max(maximumHealth, 1.0f);

        CurrentHealth =  MaximumHealth;

        IsInitialized = true;

        NotifyHealthChanged();
    }

    /// <summary>
    /// Performs the restore to maximum operation for Combat Health State.
    /// Uses the current node and service state; any result is applied through side effects, events, or stored fields.
    /// </summary>
    public void RestoreToMaximum()
    {
        if (!IsInitialized)
            return;

        CurrentHealth =
            MaximumHealth;

        NotifyHealthChanged();
    }

    /// <summary>
    /// Applies damage to the relevant actor, resource, or presentation state.
    /// Uses the supplied arguments and current state and returns the resulting damage result to the caller.
    /// </summary>
    public DamageResult ApplyDamage(float requestedDamage)
    {
        float validDamage =
            MathF.Max(requestedDamage, 0.0f);

        if (!IsAlive || validDamage <= 0.0f)
        {
            return new DamageResult(
                requestedDamage,
                0.0f,
                CurrentHealth,
                false);
        }

        float healthBeforeDamage =
            CurrentHealth;

        CurrentHealth =
            MathF.Max(
                CurrentHealth - validDamage,
                0.0f);

        float appliedDamage =
            healthBeforeDamage - CurrentHealth;

        bool wasLethal =
            healthBeforeDamage > 0.0f
            && CurrentHealth <= 0.0f;

        NotifyHealthChanged();

        return new DamageResult(
            requestedDamage,
            appliedDamage,
            CurrentHealth,
            wasLethal);
    }

    /// <summary>
    /// Applies spell healing to the relevant actor, resource, or presentation state.
    /// Uses the supplied arguments and current state and returns the resulting float to the caller.
    /// </summary>
    public float ApplySpellHealing(float requestedHealing)
    {
        float roundedHealing =
            MathF.Round(
                MathF.Max(requestedHealing, 0.0f),
                MidpointRounding.AwayFromZero);

        return ApplyRecovery(roundedHealing);
    }

    /// <summary>
    /// Applies passive recovery to the relevant actor, resource, or presentation state.
    /// Uses the supplied arguments and current state and returns the resulting float to the caller.
    /// </summary>
    public float ApplyPassiveRecovery(float requestedRecovery)
    {
        return ApplyRecovery(requestedRecovery);
    }

    /// <summary>
    /// Applies recovery to the relevant actor, resource, or presentation state.
    /// Uses the supplied arguments and current state and returns the resulting float to the caller.
    /// </summary>
    private float ApplyRecovery(float requestedRecovery)
    {
        float validRecovery =
            MathF.Max(requestedRecovery, 0.0f);

        if (!IsAlive
            || validRecovery <= 0.0f
            || CurrentHealth >= MaximumHealth)
        {
            return 0.0f;
        }

        float healthBeforeRecovery = CurrentHealth;

        CurrentHealth = MathF.Min(
            CurrentHealth + validRecovery,
            MaximumHealth);

        float appliedRecovery =
            CurrentHealth - healthBeforeRecovery;

        NotifyHealthChanged();

        return appliedRecovery;
    }

    /// <summary>
    /// Performs the notify health changed operation for Combat Health State.
    /// Uses the current node and service state; any result is applied through side effects, events, or stored fields.
    /// </summary>
    private void NotifyHealthChanged()
    {
        HealthChanged?.Invoke(
            CurrentHealth,
            MaximumHealth);
    }
}
