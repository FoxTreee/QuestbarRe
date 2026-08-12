public sealed class CombatStatusEffectInstance
{
    public CombatStatusEffectInstance(
        CombatStatusEffectDefinition definition,
        float durationSeconds,
        CombatStatusEffectApplicationContext? applicationContext = null)
    {
        Definition = definition;
        ApplicationContext =
            applicationContext ?? CombatStatusEffectApplicationContext.None;

        RemainingSeconds = System.MathF.Max(
            durationSeconds,
            0.0f);
    }

    public CombatStatusEffectDefinition Definition { get; }

    public CombatStatusEffectApplicationContext ApplicationContext { get; }

    public string ContentId => Definition.ContentId;

    public float RemainingSeconds { get; private set; }

    public bool IsExpired => RemainingSeconds <= 0.0f;

    internal void Update(double delta)
    {
        if (IsExpired || delta <= 0.0)
            return;

        RemainingSeconds = System.MathF.Max(
            RemainingSeconds - (float)delta,
            0.0f);
    }

    internal void Refresh(float durationSeconds)
    {
        RemainingSeconds = System.MathF.Max(
            RemainingSeconds,
            System.MathF.Max(durationSeconds, 0.0f));
    }
}
