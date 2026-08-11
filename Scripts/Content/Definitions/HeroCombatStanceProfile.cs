using Godot;
using System.Collections.Generic;

[GlobalClass]
public partial class HeroCombatStanceProfile : Resource
{
    [ExportCategory("Target Selection Weights")]

    /// <summary>
    /// Controls lowest current health weight, measured as a ratio or multiplier.
    /// For example, selecting a different value changes which lowest current health weight behavior or content the owning system uses.
    /// </summary>
    [Export(PropertyHint.Range, "0,500,1")]
    public float LowestCurrentHealthWeight
    { get; set; }

    /// <summary>
    /// Controls highest current health weight, measured as a ratio or multiplier.
    /// For example, selecting a different value changes which highest current health weight behavior or content the owning system uses.
    /// </summary>
    [Export(PropertyHint.Range, "0,500,1")]
    public float HighestCurrentHealthWeight
    { get; set; }

    /// <summary>
    /// Controls monster danger weight, measured as a ratio or multiplier.
    /// For example, selecting a different value changes which monster danger weight behavior or content the owning system uses.
    /// </summary>
    [Export(PropertyHint.Range, "0,500,1")]
    public float MonsterDangerWeight
    { get; set; }


    [ExportCategory("Party Coordination Weights")]

    /// <summary>
    /// Controls untargeted coverage bonus.
    /// For example, selecting a different value changes which untargeted coverage bonus behavior or content the owning system uses.
    /// </summary>
    [Export(PropertyHint.Range, "0,500,1")]
    public float UntargetedCoverageBonus
    { get; set; }

    /// <summary>
    /// Controls healthy ally support bonus, measured as health points.
    /// For example, selecting a different value changes which healthy ally support bonus behavior or content the owning system uses.
    /// </summary>
    [Export(PropertyHint.Range, "0,500,1")]
    public float HealthyAllySupportBonus
    { get; set; }

    /// <summary>
    /// Controls saturation penalty per hero.
    /// For example, changing 35 to 70 doubles this setting's configured contribution to the system.
    /// </summary>
    [Export(PropertyHint.Range, "0,500,1")]
    public float SaturationPenaltyPerHero
    { get; set; }

    /// <summary>
    /// Controls current target bonus.
    /// For example, changing 35 to 70 doubles this setting's configured contribution to the system.
    /// </summary>
    [Export(PropertyHint.Range, "0,500,1")]
    public float CurrentTargetBonus
    { get; set; } = 35.0f;

    /// <summary>
    /// Controls healthy ally minimum health percent, measured as a ratio or multiplier.
    /// For example, changing 60 to 120 doubles the configured healthy ally minimum health percent.
    /// </summary>
    [Export(PropertyHint.Range, "0,100,1")]
    public float HealthyAllyMinimumHealthPercent
    { get; set; } = 60.0f;


    [ExportCategory("Target Commitment")]

    /// <summary>
    /// Controls minimum target commitment seconds, measured as seconds.
    /// For example, changing 2 to 4 doubles this setting's configured contribution to the system.
    /// </summary>
    [Export(PropertyHint.Range, "0,10,0.1")]
    public float MinimumTargetCommitmentSeconds
    { get; set; } = 2.0f;

    /// <summary>
    /// Controls target reassessment interval seconds, measured as seconds.
    /// For example, changing 0.5 to 1 makes the affected action wait twice as long between uses.
    /// </summary>
    [Export(PropertyHint.Range, "0.05,5,0.05")]
    public float TargetReassessmentIntervalSeconds
    { get; set; } = 0.5f;

    /// <summary>
    /// Controls required switch advantage percent, measured as a ratio or multiplier.
    /// For example, changing 25 to 50 doubles this setting's configured contribution to the system.
    /// </summary>
    [Export(PropertyHint.Range, "0,500,1")]
    public float RequiredSwitchAdvantagePercent
    { get; set; } = 25.0f;

    /// <summary>
    /// Enables or disables log targeting decisions.
    /// For example, turn this on to enable log targeting decisions, or off to suppress that behavior.
    /// </summary>
    [Export]
    public bool LogTargetingDecisions
    { get; set; } = true;


    [ExportCategory("Aggro Weights")]

    /// <summary>
    /// Controls avoid aggro penalty.
    /// For example, selecting a different value changes which avoid aggro penalty behavior or content the owning system uses.
    /// </summary>
    [Export(PropertyHint.Range, "0,500,1")]
    public float AvoidAggroPenalty
    { get; set; }

    /// <summary>
    /// Controls seek aggro bonus.
    /// For example, selecting a different value changes which seek aggro bonus behavior or content the owning system uses.
    /// </summary>
    [Export(PropertyHint.Range, "0,500,1")]
    public float SeekAggroBonus
    { get; set; }


    [ExportCategory("Defensive Rescue")]

    /// <summary>
    /// Enables or disables rescue vulnerable allies.
    /// For example, turn this on to enable rescue vulnerable allies, or off to suppress that behavior.
    /// </summary>
    [Export]
    public bool RescueVulnerableAllies
    { get; set; }

    /// <summary>
    /// Controls rescue ally health threshold percent, measured as a ratio or multiplier.
    /// For example, changing 50 to 100 doubles the configured rescue ally health threshold percent.
    /// </summary>
    [Export(PropertyHint.Range, "0,100,1")]
    public float RescueAllyHealthThresholdPercent
    { get; set; } = 50.0f;

    /// <summary>
    /// Controls minimum rescue pressure, measured as a count.
    /// For example, changing 1 to 2 doubles this setting's configured contribution to the system.
    /// </summary>
    [Export(PropertyHint.Range, "1,20,1")]
    public int MinimumRescuePressure
    { get; set; } = 1;

    /// <summary>
    /// Controls rescue target commitment seconds, measured as seconds.
    /// For example, changing 2 to 4 doubles this setting's configured contribution to the system.
    /// </summary>
    [Export(PropertyHint.Range, "0,10,0.1")]
    public float RescueTargetCommitmentSeconds
    { get; set; } = 2.0f;


    [ExportCategory("Future Critical Support")]

    /// <summary>
    /// Controls critical ally rescue bonus.
    /// For example, selecting a different value changes which critical ally rescue bonus behavior or content the owning system uses.
    /// </summary>
    [Export(PropertyHint.Range, "0,500,1")]
    public float CriticalAllyRescueBonus
    { get; set; }

    /// <summary>
    /// Enables or disables rescue critical allies.
    /// For example, turn this on to enable rescue critical allies, or off to suppress that behavior.
    /// </summary>
    [Export]
    public bool RescueCriticalAllies
    { get; set; }


    /// <summary>
    /// Creates passive defaults from the supplied configuration and current dependencies.
    /// Reads the current state and returns the resulting hero combat stance profile to the caller.
    /// </summary>
    public static HeroCombatStanceProfile
        CreatePassiveDefaults()
    {
        return new HeroCombatStanceProfile
        {
            LowestCurrentHealthWeight = 100.0f,
            MonsterDangerWeight = 10.0f,
            HealthyAllySupportBonus = 50.0f,
            SaturationPenaltyPerHero = 10.0f,
            CurrentTargetBonus = 35.0f,
            AvoidAggroPenalty = 100.0f,
            CriticalAllyRescueBonus = 80.0f,
            RescueCriticalAllies = true
        };
    }


    /// <summary>
    /// Creates defensive defaults from the supplied configuration and current dependencies.
    /// Reads the current state and returns the resulting hero combat stance profile to the caller.
    /// </summary>
    public static HeroCombatStanceProfile
        CreateDefensiveDefaults()
    {
        return new HeroCombatStanceProfile
        {
            HighestCurrentHealthWeight = 10.0f,
            MonsterDangerWeight = 100.0f,
            UntargetedCoverageBonus = 35.0f,
            SaturationPenaltyPerHero = 15.0f,
            CurrentTargetBonus = 35.0f,
            SeekAggroBonus = 75.0f,
            CriticalAllyRescueBonus = 100.0f,
            RescueVulnerableAllies = true,
            RescueCriticalAllies = true
        };
    }


    /// <summary>
    /// Creates aggressive defaults from the supplied configuration and current dependencies.
    /// Reads the current state and returns the resulting hero combat stance profile to the caller.
    /// </summary>
    public static HeroCombatStanceProfile
        CreateAggressiveDefaults()
    {
        return new HeroCombatStanceProfile
        {
            HighestCurrentHealthWeight = 100.0f,
            MonsterDangerWeight = 15.0f,
            UntargetedCoverageBonus = 50.0f,
            SaturationPenaltyPerHero = 10.0f,
            CurrentTargetBonus = 50.0f,
            SeekAggroBonus = 100.0f,
            CriticalAllyRescueBonus = 150.0f,
            RescueCriticalAllies = true
        };
    }


    /// <summary>
    /// Retrieves validation errors from the current game state.
    /// Uses the supplied arguments and current state and returns the resulting i read only list string to the caller.
    /// </summary>
    public IReadOnlyList<string> GetValidationErrors(
        string profileName)
    {
        List<string> errors = new();

        AddNonNegativeError(errors, profileName,
            nameof(LowestCurrentHealthWeight),
            LowestCurrentHealthWeight);
        AddNonNegativeError(errors, profileName,
            nameof(HighestCurrentHealthWeight),
            HighestCurrentHealthWeight);
        AddNonNegativeError(errors, profileName,
            nameof(MonsterDangerWeight),
            MonsterDangerWeight);
        AddNonNegativeError(errors, profileName,
            nameof(UntargetedCoverageBonus),
            UntargetedCoverageBonus);
        AddNonNegativeError(errors, profileName,
            nameof(HealthyAllySupportBonus),
            HealthyAllySupportBonus);
        AddNonNegativeError(errors, profileName,
            nameof(SaturationPenaltyPerHero),
            SaturationPenaltyPerHero);
        AddNonNegativeError(errors, profileName,
            nameof(CurrentTargetBonus),
            CurrentTargetBonus);
        AddNonNegativeError(errors, profileName,
            nameof(AvoidAggroPenalty),
            AvoidAggroPenalty);
        AddNonNegativeError(errors, profileName,
            nameof(SeekAggroBonus),
            SeekAggroBonus);
        AddNonNegativeError(errors, profileName,
            nameof(CriticalAllyRescueBonus),
            CriticalAllyRescueBonus);

        if (HealthyAllyMinimumHealthPercent < 0.0f
            || HealthyAllyMinimumHealthPercent > 100.0f)
        {
            errors.Add(
                $"{profileName}." +
                $"{nameof(HealthyAllyMinimumHealthPercent)} " +
                "must be between 0 and 100.");
        }

        if (MinimumTargetCommitmentSeconds < 0.0f)
        {
            errors.Add(
                $"{profileName}." +
                $"{nameof(MinimumTargetCommitmentSeconds)} " +
                "cannot be negative.");
        }

        if (TargetReassessmentIntervalSeconds <= 0.0f)
        {
            errors.Add(
                $"{profileName}." +
                $"{nameof(TargetReassessmentIntervalSeconds)} " +
                "must be greater than zero.");
        }

        if (RequiredSwitchAdvantagePercent < 0.0f)
        {
            errors.Add(
                $"{profileName}." +
                $"{nameof(RequiredSwitchAdvantagePercent)} " +
                "cannot be negative.");
        }

        if (RescueAllyHealthThresholdPercent < 0.0f
            || RescueAllyHealthThresholdPercent > 100.0f)
        {
            errors.Add(
                $"{profileName}." +
                $"{nameof(RescueAllyHealthThresholdPercent)} " +
                "must be between 0 and 100.");
        }

        if (MinimumRescuePressure < 1)
        {
            errors.Add(
                $"{profileName}." +
                $"{nameof(MinimumRescuePressure)} " +
                "must be at least one.");
        }

        if (RescueTargetCommitmentSeconds < 0.0f)
        {
            errors.Add(
                $"{profileName}." +
                $"{nameof(RescueTargetCommitmentSeconds)} " +
                "cannot be negative.");
        }

        return errors;
    }


    /// <summary>
    /// Performs the add non negative error operation for Hero Combat Stance Profile.
    /// Uses the supplied arguments and current node state; any result is applied through side effects, events, or stored fields.
    /// </summary>
    private static void AddNonNegativeError(
        ICollection<string> errors,
        string profileName,
        string propertyName,
        float value)
    {
        if (value >= 0.0f)
            return;

        errors.Add(
            $"{profileName}.{propertyName} cannot be negative.");
    }
}
