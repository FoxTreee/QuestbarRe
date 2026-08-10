using Godot;
using System.Collections.Generic;

[GlobalClass]
public partial class HeroDefinition : Resource
{
    [ExportCategory("Identity")]

    [Export(PropertyHint.PlaceholderText, "hero.core.syzygy")]
    public string ContentId { get; set; } =
        string.Empty;

    [Export(PropertyHint.PlaceholderText, "Syzygy")]
    public string DisplayName { get; set; } =
        "Unnamed Hero";


    [ExportCategory("Class")]

    [Export]
    public HeroClassDefinition ClassDefinition
    { get; set; } = null!;


    [ExportCategory("Runtime")]

    [Export]
    public PackedScene ActorScene { get; set; } = null!;


    [ExportCategory("Combat Identity")]

    [Export(PropertyHint.Flags, "Melee,Ranged,Caster,Healer,Tank,Summoner,Armored")]
    public int CombatTagMask { get; set; } =
        (int)HeroCombatTag.Melee;

    public HeroCombatTag CombatTags =>
        (HeroCombatTag)CombatTagMask;


    [ExportCategory("Combat Stance")]

    [Export]
    public HeroCombatStance StartingCombatStance
    { get; set; } = HeroCombatStance.Defensive;

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


    [ExportCategory("Passive Stance Profile")]

    [Export]
    public HeroCombatStanceProfile PassiveStanceProfile
    { get; set; } =
        HeroCombatStanceProfile.CreatePassiveDefaults();


    [ExportCategory("Defensive Stance Profile")]

    [Export]
    public HeroCombatStanceProfile DefensiveStanceProfile
    { get; set; } =
        HeroCombatStanceProfile.CreateDefensiveDefaults();


    [ExportCategory("Aggressive Stance Profile")]

    [Export]
    public HeroCombatStanceProfile AggressiveStanceProfile
    { get; set; } =
        HeroCombatStanceProfile.CreateAggressiveDefaults();


    [ExportCategory("Health")]

    [Export(PropertyHint.Range, "1,1000000,1")]
    public float MaximumHealth { get; set; } =
        100.0f;


    [ExportCategory("Attack")]

    [Export(PropertyHint.Range, "0,1000000,1")]
    public float AttackDamage { get; set; } =
        20.0f;

    [Export(PropertyHint.Range, "0,400,1")]
    public float AttackRange { get; set; } =
        28.0f;

    [Export(PropertyHint.Range, "0.1,30,0.1")]
    public float AttackInterval { get; set; } =
        1.5f;

    [Export(PropertyHint.Range, "0.05,5,0.05")]
    public float AttackDuration { get; set; } =
        0.3f;

    [Export(PropertyHint.Range, "0,1,0.05")]
    public float AttackReleasePoint { get; set; } =
        0.5f;

    [Export(PropertyHint.Range, "0,100,0.5")]
    public float AttackLungeDistance { get; set; } =
        8.0f;

    [Export]
    public AttackDeliveryMode AttackDelivery { get; set; } =
        AttackDeliveryMode.ImmediateImpact;


    [ExportCategory("Movement")]

    [Export(PropertyHint.Range, "0,1000,1")]
    public float CombatMoveSpeed { get; set; } =
        140.0f;


    [ExportCategory("Abilities")]

    [Export]
    public Godot.Collections.Array<string> AbilityContentIds
    { get; set; } = new();


    public HeroCombatStanceProfile GetStanceProfile(
        HeroCombatStance stance)
    {
        return stance switch
        {
            HeroCombatStance.Passive =>
                PassiveStanceProfile,

            HeroCombatStance.Aggressive =>
                AggressiveStanceProfile,

            _ =>
                DefensiveStanceProfile
        };
    }


    public IReadOnlyList<string> GetValidationErrors()
    {
        List<string> errors = new();

        if (!global::ContentId.IsValid(ContentId))
        {
            errors.Add(
                $"Invalid hero Content ID '{ContentId}'. " +
                "Expected lowercase format such as " +
                "'hero.core.syzygy'.");
        }

        if (string.IsNullOrWhiteSpace(DisplayName))
        {
            errors.Add(
                $"{ContentId}: DisplayName is required.");
        }

        if (!GodotObject.IsInstanceValid(ClassDefinition))
        {
            errors.Add(
                $"{ContentId}: ClassDefinition is required.");
        }
        else
        {
            foreach (string classError
                in ClassDefinition.GetValidationErrors())
            {
                errors.Add(
                    $"{ContentId}: invalid class reference: " +
                    classError);
            }
        }

        if (!GodotObject.IsInstanceValid(ActorScene))
        {
            errors.Add(
                $"{ContentId}: ActorScene is required.");
        }

        if (MaximumHealth <= 0.0f)
        {
            errors.Add(
                $"{ContentId}: MaximumHealth must be " +
                "greater than zero.");
        }

        if (AttackDamage < 0.0f)
        {
            errors.Add(
                $"{ContentId}: AttackDamage cannot be " +
                "negative.");
        }

        if (AttackRange < 0.0f)
        {
            errors.Add(
                $"{ContentId}: AttackRange cannot be " +
                "negative.");
        }

        if (AttackInterval <= 0.0f)
        {
            errors.Add(
                $"{ContentId}: AttackInterval must be " +
                "greater than zero.");
        }

        if (AttackDuration <= 0.0f)
        {
            errors.Add(
                $"{ContentId}: AttackDuration must be " +
                "greater than zero.");
        }

        if (AttackReleasePoint < 0.0f
            || AttackReleasePoint > 1.0f)
        {
            errors.Add(
                $"{ContentId}: AttackReleasePoint must " +
                "be between 0 and 1.");
        }

        if (AttackLungeDistance < 0.0f)
        {
            errors.Add(
                $"{ContentId}: AttackLungeDistance cannot " +
                "be negative.");
        }

        if (CombatMoveSpeed < 0.0f)
        {
            errors.Add(
                $"{ContentId}: CombatMoveSpeed cannot be " +
                "negative.");
        }

        if (!System.Enum.IsDefined(
            typeof(HeroCombatStance),
            StartingCombatStance))
        {
            errors.Add(
                $"{ContentId}: StartingCombatStance is invalid.");
        }

        if (MinimumTargetCommitmentSeconds < 0.0f)
        {
            errors.Add(
                $"{ContentId}: MinimumTargetCommitmentSeconds " +
                "cannot be negative.");
        }

        if (TargetReassessmentIntervalSeconds <= 0.0f)
        {
            errors.Add(
                $"{ContentId}: TargetReassessmentIntervalSeconds " +
                "must be greater than zero.");
        }

        if (RequiredSwitchAdvantagePercent < 0.0f)
        {
            errors.Add(
                $"{ContentId}: RequiredSwitchAdvantagePercent " +
                "cannot be negative.");
        }

        AppendStanceProfileErrors(
            errors,
            PassiveStanceProfile,
            "PassiveStanceProfile");

        AppendStanceProfileErrors(
            errors,
            DefensiveStanceProfile,
            "DefensiveStanceProfile");

        AppendStanceProfileErrors(
            errors,
            AggressiveStanceProfile,
            "AggressiveStanceProfile");

        HashSet<string> seenAbilityIds =
            new(System.StringComparer.OrdinalIgnoreCase);

        foreach (string abilityContentId in AbilityContentIds)
        {
            if (!global::ContentId.IsValid(abilityContentId))
            {
                errors.Add(
                    $"{ContentId}: invalid ability Content ID " +
                    $"'{abilityContentId}'.");

                continue;
            }

            if (!seenAbilityIds.Add(abilityContentId.Trim()))
            {
                errors.Add(
                    $"{ContentId}: duplicate ability Content ID " +
                    $"'{abilityContentId}'.");
            }
        }

        return errors;
    }


    private void AppendStanceProfileErrors(
        ICollection<string> errors,
        HeroCombatStanceProfile profile,
        string profileName)
    {
        if (!GodotObject.IsInstanceValid(profile))
        {
            errors.Add(
                $"{ContentId}: {profileName} is required.");

            return;
        }

        foreach (string profileError
            in profile.GetValidationErrors(profileName))
        {
            errors.Add(
                $"{ContentId}: {profileError}");
        }
    }
}
