using Godot;
using System.Collections.Generic;

[GlobalClass]
public partial class HeroCombatStanceProfile : Resource
{
    [ExportCategory("Target Selection Weights")]

    [Export(PropertyHint.Range, "0,500,1")]
    public float LowestCurrentHealthWeight
    { get; set; }

    [Export(PropertyHint.Range, "0,500,1")]
    public float HighestCurrentHealthWeight
    { get; set; }

    [Export(PropertyHint.Range, "0,500,1")]
    public float MonsterDangerWeight
    { get; set; }


    [ExportCategory("Party Coordination Weights")]

    [Export(PropertyHint.Range, "0,500,1")]
    public float UntargetedCoverageBonus
    { get; set; }

    [Export(PropertyHint.Range, "0,500,1")]
    public float HealthyAllySupportBonus
    { get; set; }

    [Export(PropertyHint.Range, "0,500,1")]
    public float SaturationPenaltyPerHero
    { get; set; }

    [Export(PropertyHint.Range, "0,500,1")]
    public float CurrentTargetBonus
    { get; set; } = 35.0f;

    [Export(PropertyHint.Range, "0,100,1")]
    public float HealthyAllyMinimumHealthPercent
    { get; set; } = 60.0f;


    [ExportCategory("Target Commitment")]

    [Export(PropertyHint.Range, "0,10,0.1")]
    public float MinimumTargetCommitmentSeconds
    { get; set; } = 2.0f;

    [Export(PropertyHint.Range, "0.05,5,0.05")]
    public float TargetReassessmentIntervalSeconds
    { get; set; } = 0.5f;

    [Export(PropertyHint.Range, "0,500,1")]
    public float RequiredSwitchAdvantagePercent
    { get; set; } = 25.0f;

    [Export]
    public bool LogTargetingDecisions
    { get; set; } = true;


    [ExportCategory("Aggro Weights")]

    [Export(PropertyHint.Range, "0,500,1")]
    public float AvoidAggroPenalty
    { get; set; }

    [Export(PropertyHint.Range, "0,500,1")]
    public float SeekAggroBonus
    { get; set; }


    [ExportCategory("Defensive Rescue")]

    [Export]
    public bool RescueVulnerableAllies
    { get; set; }

    [Export(PropertyHint.Range, "0,100,1")]
    public float RescueAllyHealthThresholdPercent
    { get; set; } = 50.0f;

    [Export(PropertyHint.Range, "1,20,1")]
    public int MinimumRescuePressure
    { get; set; } = 1;

    [Export(PropertyHint.Range, "0,10,0.1")]
    public float RescueTargetCommitmentSeconds
    { get; set; } = 2.0f;


    [ExportCategory("Future Critical Support")]

    [Export(PropertyHint.Range, "0,500,1")]
    public float CriticalAllyRescueBonus
    { get; set; }

    [Export]
    public bool RescueCriticalAllies
    { get; set; }


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
