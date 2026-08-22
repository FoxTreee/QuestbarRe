using Godot;

public partial class ExperienceCurveService : Node
{
    [ExportCategory("Per-Level XP Requirements")]

    /// <summary>
    /// XP required to advance from levels 1 through 59. Array index 0 is the
    /// level 1-to-2 requirement, index 1 is level 2-to-3, and so on. Every
    /// level is independently editable; the doubling is only the starting data.
    /// </summary>
    [Export]
    public Godot.Collections.Array<double> ExperienceRequiredByCurrentLevel
    { get; set; } = new()
    {
        200d, 400d, 800d, 1600d, 3200d, 6400d, 12800d, 25600d,
        51200d, 102400d, 204800d, 409600d, 819200d, 1638400d,
        3276800d, 6553600d, 13107200d, 26214400d, 52428800d,
        104857600d, 209715200d, 419430400d, 838860800d,
        1677721600d, 3355443200d, 6710886400d, 13421772800d,
        26843545600d, 53687091200d, 107374182400d, 214748364800d,
        429496729600d, 858993459200d, 1717986918400d,
        3435973836800d, 6871947673600d, 13743895347200d,
        27487790694400d, 54975581388800d, 109951162777600d,
        219902325555200d, 439804651110400d, 879609302220800d,
        1759218604441600d, 3518437208883200d, 7036874417766400d,
        14073748835532800d, 28147497671065600d, 56294995342131200d,
        112589990684262400d, 225179981368524800d, 450359962737049600d,
        900719925474099200d, 1801439850948198400d, 3602879701896396800d,
        7205759403792793600d, 14411518807585587200d,
        28823037615171174400d, 57646075230342348800d
    };

    /// <summary>
    /// Returns the independently authored XP cost for the hero's current level.
    /// Level 60 has no next level and therefore returns zero.
    /// </summary>
    public double GetExperienceRequiredForNextLevel(int currentLevel)
    {
        if (currentLevel >= HeroProgressionState.MaximumLevel)
            return 0.0;

        int index = Mathf.Clamp(
            currentLevel - 1,
            0,
            ExperienceRequiredByCurrentLevel.Count - 1);

        return System.Math.Max(
            ExperienceRequiredByCurrentLevel[index],
            1.0);
    }

    public override void _Ready()
    {
        if (ExperienceRequiredByCurrentLevel.Count
            != HeroProgressionState.MaximumLevel - 1)
        {
            GD.PushError(
                "ExperienceCurveService requires exactly 59 entries: " +
                "one requirement for each level from 1 to 59.");
        }
    }
}
