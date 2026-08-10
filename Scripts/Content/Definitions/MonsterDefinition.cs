using Godot;
using System.Collections.Generic;

[GlobalClass]
public partial class MonsterDefinition : Resource
{
    [ExportCategory("Identity")]

    [Export]
    public string ContentId { get; set; } = string.Empty;

    [Export]
    public string DisplayName { get; set; } = "Unnamed Monster";


    [ExportCategory("Runtime")]

    [Export]
    public PackedScene ActorScene { get; set; } = null!;

    [ExportCategory("Health")]

    [Export(PropertyHint.Range, "1,1000000,1")]
    public float MaximumHealth { get; set; } = 100.0f;


    [ExportCategory("Attack")]

    [Export(PropertyHint.Range, "0,1000000,1")]
    public float AttackDamage { get; set; } = 10.0f;

    [Export(PropertyHint.Range, "0,400,1")]
    public float AttackRange { get; set; } = 28.0f;

    [Export(PropertyHint.Range, "0.1,30,0.1")]
    public float AttackInterval { get; set; } = 1.5f;

    [Export(PropertyHint.Range, "0.05,5,0.05")]
    public float AttackDuration { get; set; } = 0.3f;

    [Export(PropertyHint.Range, "0,1,0.05")]
    public float AttackReleasePoint { get; set; } = 0.5f;

    [Export(PropertyHint.Range, "0,100,0.5")]
    public float AttackLungeDistance { get; set; } = 8.0f;

    [Export]
    public AttackDeliveryMode AttackDelivery
    {
        get;
        set;
    } = AttackDeliveryMode.ImmediateImpact;

    [ExportCategory("Melee Engagement Slots")]

    [Export(PropertyHint.Range, "0.5,2,0.05")]
    public float MeleeSlotHorizontalSpacingMultiplier
    { get; set; } = 1.0f;

    [Export(PropertyHint.Range, "0.25,2,0.05")]
    public float MeleeSlotVerticalSpacingMultiplier
    { get; set; } = 0.75f;

    [ExportCategory("Movement")]

    [Export(PropertyHint.Range, "0,1000,1")]
    public float EntrySpeed { get; set; } =
        100.0f;

    [Export(PropertyHint.Range, "0,1000,1")]
    public float CombatMoveSpeed { get; set; } =
        100.0f;

    [ExportCategory("Targeting")]

    [Export(PropertyHint.Flags, "Melee,Ranged,Caster,Healer,Tank,Summoner,Armored")]
    public int PreferredTargetTagMask { get; set; } =
        (int)HeroCombatTag.None;

    public HeroCombatTag PreferredTargetTags =>
        (HeroCombatTag)PreferredTargetTagMask;

    [Export]
    public MonsterTargetingStyle TargetingStyle { get; set; } =
        MonsterTargetingStyle.LowestHealthHero;


    [ExportCategory("Abilities")]

    [Export]
    public Godot.Collections.Array<string> AbilityContentIds
    { get; set; } = new();


    public IReadOnlyList<string>
        GetValidationErrors()
    {
        List<string> errors =
            new();

        if (!global::ContentId.IsValid(ContentId))
        {
            errors.Add(
                $"Invalid Content ID '{ContentId}'. " +
                "Expected lowercase format such as " +
                "'monster.core.training_monster'.");
        }
        if (string.IsNullOrWhiteSpace(DisplayName))
        {
            errors.Add(
                $"{ContentId}: DisplayName is required.");
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
        if (!float.IsFinite(
                MeleeSlotHorizontalSpacingMultiplier)
            || MeleeSlotHorizontalSpacingMultiplier < 0.0f)
        {
            errors.Add(
                $"{ContentId}: melee slot horizontal spacing " +
                "must be a finite non-negative number.");
        }
        if (!float.IsFinite(
                MeleeSlotVerticalSpacingMultiplier)
            || MeleeSlotVerticalSpacingMultiplier < 0.0f)
        {
            errors.Add(
                $"{ContentId}: melee slot vertical spacing " +
                "must be a finite non-negative number.");
        }
        if (EntrySpeed < 0.0f)
        {
            errors.Add(
                $"{ContentId}: EntrySpeed cannot be " +
                "negative.");
        }
        if (CombatMoveSpeed < 0.0f)
        {
            errors.Add(
                $"{ContentId}: CombatMoveSpeed cannot be " +
                "negative.");
        }

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
}
