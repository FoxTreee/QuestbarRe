using Godot;
using System.Collections.Generic;

[GlobalClass]
public partial class MonsterDefinition : Resource
{
    [ExportCategory("Identity")]

    /// <summary>
    /// Stable content identifier for content; other systems use this value to find the same game data.
    /// For example, changing this ID makes the owning resource resolve a different registered content.
    /// </summary>
    [Export]
    public string ContentId { get; set; } = string.Empty;

    /// <summary>
    /// Controls display name.
    /// For example, changing this text changes the name, message, key, or lookup value shown or consumed by the owning system.
    /// </summary>
    [Export]
    public string DisplayName { get; set; } = "Unnamed Monster";


    [ExportCategory("Runtime")]

    /// <summary>
    /// Inspector reference used by this component for its actor scene dependency.
    /// Assign the matching node or resource from the scene; leaving it empty prevents that connection from working.
    /// </summary>
    [Export]
    public PackedScene ActorScene { get; set; } = null!;

    [ExportCategory("Health")]

    /// <summary>
    /// Controls maximum health, measured as health points.
    /// For example, changing 100 to 200 doubles the configured maximum health.
    /// </summary>
    [Export(PropertyHint.Range, "1,1000000,1")]
    public float MaximumHealth { get; set; } = 100.0f;


    [ExportCategory("Defense")]

    /// <summary>
    /// Chance for this monster type to dodge any offensive action that uses
    /// defender Dodge. Monsters intentionally use a simple authored value
    /// instead of the future hero Agility/gear stat pipeline.
    /// </summary>
    [Export(PropertyHint.Range, "0,100,0.1")]
    public float DodgeChancePercent { get; set; } = 5.0f;


    [ExportCategory("Attack")]

    /// <summary>
    /// Controls attack damage, measured as damage points.
    /// For example, changing 10 to 20 doubles the configured attack damage.
    /// </summary>
    [Export(PropertyHint.Range, "0,1000000,1")]
    public float AttackDamage { get; set; } = 10.0f;

    /// <summary>
    /// Controls attack range, measured as pixels.
    /// For example, changing 28 to 56 doubles the configured attack range.
    /// </summary>
    [Export(PropertyHint.Range, "0,400,1")]
    public float AttackRange { get; set; } = 28.0f;

    /// <summary>
    /// Controls attack interval, measured as seconds.
    /// For example, changing 1.5 to 3 makes the affected action wait twice as long between uses.
    /// </summary>
    [Export(PropertyHint.Range, "0.1,30,0.1")]
    public float AttackInterval { get; set; } = 1.5f;

    /// <summary>
    /// Controls attack duration, measured as seconds.
    /// For example, changing 0.3 to 0.6 doubles this setting's configured contribution to the system.
    /// </summary>
    [Export(PropertyHint.Range, "0.05,5,0.05")]
    public float AttackDuration { get; set; } = 0.3f;

    /// <summary>
    /// Controls attack release point.
    /// For example, changing 0.5 to 1 doubles this setting's configured contribution to the system.
    /// </summary>
    [Export(PropertyHint.Range, "0,1,0.05")]
    public float AttackReleasePoint { get; set; } = 0.5f;

    /// <summary>
    /// Controls attack lunge distance, measured as pixels.
    /// For example, changing 8 to 16 doubles the configured attack lunge distance.
    /// </summary>
    [Export(PropertyHint.Range, "0,100,0.5")]
    public float AttackLungeDistance { get; set; } = 8.0f;

    /// <summary>
    /// Controls attack delivery.
    /// For example, selecting a different value changes which attack delivery behavior or content the owning system uses.
    /// </summary>
    [Export]
    public AttackDeliveryMode AttackDelivery
    {
        get;
        set;
    } = AttackDeliveryMode.ImmediateImpact;

    [ExportCategory("Movement")]

    /// <summary>
    /// Controls entry speed, measured as pixels per second.
    /// For example, changing 100 to 200 makes the affected movement or animation run about twice as fast.
    /// </summary>
    [Export(PropertyHint.Range, "0,1000,1")]
    public float EntrySpeed { get; set; } =
        100.0f;

    /// <summary>
    /// Controls combat move speed, measured as pixels per second.
    /// For example, changing 100 to 200 makes the affected movement or animation run about twice as fast.
    /// </summary>
    [Export(PropertyHint.Range, "0,1000,1")]
    public float CombatMoveSpeed { get; set; } =
        100.0f;

    [ExportCategory("Targeting")]

    /// <summary>
    /// Controls preferred target tag mask.
    /// For example, selecting a different value changes which preferred target tag mask behavior or content the owning system uses.
    /// </summary>
    [Export(PropertyHint.Flags, "Melee,Ranged,Caster,Healer,Tank,Summoner,Armored")]
    public int PreferredTargetTagMask { get; set; } =
        (int)HeroCombatTag.None;

    public HeroCombatTag PreferredTargetTags =>
        (HeroCombatTag)PreferredTargetTagMask;

    /// <summary>
    /// Controls targeting style.
    /// For example, selecting a different value changes which targeting style behavior or content the owning system uses.
    /// </summary>
    [Export]
    public MonsterTargetingStyle TargetingStyle { get; set; } =
        MonsterTargetingStyle.LowestHealthHero;


    [ExportCategory("Hero Targeting Response")]

    /// <summary>
    /// Controls danger rating.
    /// For example, changing 1 to 2 doubles this setting's configured contribution to the system.
    /// </summary>
    [Export(PropertyHint.Range, "0,1000,0.1")]
    public float DangerRating { get; set; } = 1.0f;

    /// <summary>
    /// Controls preferred hero attacker count, measured as a count.
    /// For example, changing 1 to 2 doubles the configured preferred hero attacker count.
    /// </summary>
    [Export(PropertyHint.Range, "1,20,1")]
    public int PreferredHeroAttackerCount { get; set; } = 1;


    [ExportCategory("Abilities")]

    /// <summary>
    /// Stable content identifier for abilitys; other systems use this value to find the same game data.
    /// For example, changing this ID makes the owning resource resolve a different registered abilitys.
    /// </summary>
    [Export]
    public Godot.Collections.Array<string> AbilityContentIds
    { get; set; } = new();


    /// <summary>
    /// Retrieves validation errors from the current game state.
    /// Reads the current state and returns the resulting i read only list string to the caller.
    /// </summary>
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
        if (DodgeChancePercent < 0.0f
            || DodgeChancePercent > 100.0f)
        {
            errors.Add(
                $"{ContentId}: DodgeChancePercent must be " +
                "between 0 and 100.");
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
        if (DangerRating < 0.0f)
        {
            errors.Add(
                $"{ContentId}: DangerRating cannot be negative.");
        }
        if (PreferredHeroAttackerCount < 1)
        {
            errors.Add(
                $"{ContentId}: PreferredHeroAttackerCount " +
                "must be at least one.");
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
