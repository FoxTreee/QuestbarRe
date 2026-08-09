using System;

public sealed class CombatHealthState
{
    public event Action<float, float>? HealthChanged;

    public float MaximumHealth { get; private set; }

    public float CurrentHealth { get; private set; }

    public bool IsInitialized { get; private set; }

    public bool IsAlive =>
        CurrentHealth > 0.0f;

    public void Initialize(float maximumHealth)
    {
        if (IsInitialized)
            return;

        MaximumHealth = MathF.Max(maximumHealth, 1.0f);

        CurrentHealth =  MaximumHealth;

        IsInitialized = true;

        NotifyHealthChanged();
    }

    public void RestoreToMaximum()
    {
        if (!IsInitialized)
            return;

        CurrentHealth =
            MaximumHealth;

        NotifyHealthChanged();
    }

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

    private void NotifyHealthChanged()
    {
        HealthChanged?.Invoke(
            CurrentHealth,
            MaximumHealth);
    }
}