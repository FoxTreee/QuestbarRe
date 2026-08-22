using System;

public sealed class HeroProgressionState
{
    public const int MaximumLevel = 60;

    public int Level { get; private set; } = 1;
    public double Experience { get; private set; }

    public event Action? Changed;

    /// <summary>
    /// Sets authored starting progression while clamping it to valid game bounds.
    /// This is also the entry point a future save loader can use.
    /// </summary>
    public void Configure(int level, double experience)
    {
        Level = Math.Clamp(level, 1, MaximumLevel);
        Experience = Level >= MaximumLevel
            ? 0.0
            : Math.Max(experience, 0.0);

        Changed?.Invoke();
    }

    /// <summary>
    /// Adds XP and performs every earned level-up. Each crossed ceiling is
    /// subtracted from the total so all excess XP carries into the next level.
    /// </summary>
    public int AddExperience(
        double amount,
        ExperienceCurveService curve)
    {
        if (amount <= 0.0 || Level >= MaximumLevel)
            return 0;

        Experience += amount;
        int levelsGained = 0;

        while (Level < MaximumLevel)
        {
            double required =
                curve.GetExperienceRequiredForNextLevel(Level);

            if (Experience < required)
                break;

            Experience -= required;
            Level++;
            levelsGained++;
        }

        if (Level >= MaximumLevel)
            Experience = 0.0;

        Changed?.Invoke();
        return levelsGained;
    }

    /// <summary>
    /// Adds debug levels directly, clamps at Level 60, and resets XP to zero so
    /// the resulting level always begins at the start of its authored ceiling.
    /// </summary>
    public int AddLevels(int amount)
    {
        if (amount <= 0 || Level >= MaximumLevel)
            return 0;

        int previousLevel = Level;
        Level = Math.Clamp(Level + amount, 1, MaximumLevel);
        Experience = 0.0;
        Changed?.Invoke();
        return Level - previousLevel;
    }

    /// <summary>
    /// Sets an exact debug level in either direction and resets XP to zero.
    /// Returns false when the requested level is outside the 1-60 range.
    /// </summary>
    public bool TrySetLevel(int level)
    {
        if (level < 1 || level > MaximumLevel)
            return false;

        Level = level;
        Experience = 0.0;
        Changed?.Invoke();
        return true;
    }
}
