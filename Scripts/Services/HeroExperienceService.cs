using Godot;

public partial class HeroExperienceService : Node
{
    [ExportCategory("Dependencies")]
    [Export] public PartyController Party { get; set; } = null!;
    [Export] public ExperienceCurveService Curve { get; set; } = null!;

    [ExportCategory("Level Difference Scaling")]

    /// <summary>
    /// Percentage added or removed per level of difference. At 20, a hero one
    /// level below earns 120%; a hero one level above earns 80%.
    /// </summary>
    [Export(PropertyHint.Range, "0,100,1")]
    public double PercentPerLevelDifference { get; set; } = 20.0;

    /// <summary>
    /// Monsters this many or more levels below a hero award zero XP.
    /// </summary>
    [Export(PropertyHint.Range, "1,60,1")]
    public int ZeroExperienceLevelGap { get; set; } = 4;

    /// <summary>
	/// Awards each active hero XP for a defeated monster using its runtime level
	/// and authored base XP. Regional scaling therefore participates in the
	/// existing level-difference XP rules without changing monster content.
	/// </summary>
	public void AwardMonsterDefeat(MonsterActorController monster)
    {
        foreach (HeroActorController hero in Party.SpawnedHeroes)
        {
            if (!GodotObject.IsInstanceValid(hero)
                || hero.IsIncapacitated)
            {
                continue;
            }

			int awarded = CalculateAward(
				hero.Progression.Level,
				monster.Level,
				monster.Definition.BaseExperienceReward);

            if (awarded <= 0)
                continue;

            int levelsGained =
                hero.Progression.AddExperience(awarded, Curve);

			DebugLog.Print(
				$"{hero.Definition?.DisplayName ?? hero.Name.ToString()} " +
				$"earned {awarded} XP from Level {monster.Level} " +
				$"{monster.DisplayName}. " +
                $"Level={hero.Progression.Level}; " +
                $"XP={hero.Progression.Experience:0}/" +
                $"{Curve.GetExperienceRequiredForNextLevel(hero.Progression.Level):0}; " +
                $"LevelsGained={levelsGained}.");
        }
    }

    /// <summary>
    /// Calculates the final whole-number XP award. Equal level gives base XP;
    /// lower heroes gain 20% per level, higher heroes lose 20% per level, and
    /// the configured grey-level gap awards zero.
    /// </summary>
    public int CalculateAward(
        int heroLevel,
        int monsterLevel,
        int baseExperience)
    {
        if (baseExperience <= 0)
            return 0;

        int heroAdvantage = heroLevel - monsterLevel;

        if (heroAdvantage >= ZeroExperienceLevelGap)
            return 0;

        double multiplier = 1.0
            - heroAdvantage
            * (PercentPerLevelDifference / 100.0);

        return Mathf.Max(
            Mathf.RoundToInt(baseExperience * multiplier),
            0);
    }
}
