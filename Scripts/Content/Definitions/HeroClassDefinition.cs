using Godot;
using System;
using System.Collections.Generic;

[GlobalClass]
public partial class HeroClassDefinition : Resource
{
    public const int MaximumClassAbilityCount = 6;

    [ExportCategory("Identity")]

    [Export(PropertyHint.PlaceholderText, "class.core.warrior")]
    public string ContentId { get; set; } =
        string.Empty;

    [Export(PropertyHint.PlaceholderText, "Warrior")]
    public string DisplayName { get; set; } =
        "Unnamed Class";


    [ExportCategory("Resource")]

    /// <summary>
    /// Defines the class's combat resource, capacity, starting amount, and
    /// regeneration. Leave empty for classes that do not use a resource.
    /// </summary>
    [Export]
    public HeroResourceDefinition? ResourceDefinition
    { get; set; }


    [ExportCategory("Equipment Permissions")]

    /// <summary>
    /// Armor proficiencies this class may equip. ArmorCategory.None is for
    /// accessories and does not need to be included.
    /// </summary>
    [Export]
    public Godot.Collections.Array<ArmorCategory> AllowedArmorCategories
    { get; set; } = new();

    /// <summary>
    /// Weapon families this class may equip. Permissions are content data and
    /// are deliberately not hard-coded into inventory or drag/drop logic.
    /// </summary>
    [Export]
    public Godot.Collections.Array<WeaponType> AllowedWeaponTypes
    { get; set; } = new();

    [Export]
    public bool CanEquipShields { get; set; }


    [ExportCategory("Ability Pool")]

    /// <summary>
    /// The complete class ability pool. A class may author up to six abilities,
    /// but individual heroes equip only two of them at a time through their
    /// starting loadout. Being present here makes an ability available to the
    /// class; it does not automatically make the ability active in combat.
    /// </summary>
    [Export]
    public Godot.Collections.Array<string> AbilityContentIds
    { get; set; } = new();


    public IReadOnlyList<string> GetValidationErrors()
    {
        List<string> errors = new();

        if (!global::ContentId.IsValid(ContentId)
            || !ContentId.StartsWith(
                "class.",
                StringComparison.Ordinal))
        {
            errors.Add(
                $"Invalid class Content ID '{ContentId}'. " +
                "Expected lowercase format such as " +
                "'class.core.warrior'.");
        }

        if (string.IsNullOrWhiteSpace(DisplayName))
        {
            errors.Add(
                $"{ContentId}: DisplayName is required.");
        }

        if (GodotObject.IsInstanceValid(ResourceDefinition))
        {
            foreach (string resourceError
                in ResourceDefinition!.GetValidationErrors())
            {
                errors.Add(
                    $"{ContentId}: invalid resource definition: " +
                    resourceError);
            }
        }

        if (AbilityContentIds.Count > MaximumClassAbilityCount)
        {
            errors.Add(
                $"{ContentId}: class ability pool contains " +
                $"{AbilityContentIds.Count} abilities, but the maximum is " +
                $"{MaximumClassAbilityCount}.");
        }

        HashSet<string> seenAbilityIds =
            new(StringComparer.OrdinalIgnoreCase);

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

        HashSet<ArmorCategory> seenArmorCategories = new();
        foreach (ArmorCategory armorCategory in AllowedArmorCategories)
        {
            if (!Enum.IsDefined(armorCategory) || armorCategory == ArmorCategory.None)
            {
                errors.Add($"{ContentId}: invalid allowed armor category '{armorCategory}'.");
                continue;
            }

            if (!seenArmorCategories.Add(armorCategory))
                errors.Add($"{ContentId}: duplicate allowed armor category '{armorCategory}'.");
        }

        HashSet<WeaponType> seenWeaponTypes = new();
        foreach (WeaponType weaponType in AllowedWeaponTypes)
        {
            if (!Enum.IsDefined(weaponType))
            {
                errors.Add($"{ContentId}: invalid allowed weapon type '{weaponType}'.");
                continue;
            }

            if (!seenWeaponTypes.Add(weaponType))
                errors.Add($"{ContentId}: duplicate allowed weapon type '{weaponType}'.");
        }

        return errors;
    }

    public bool AllowsArmorCategory(ArmorCategory armorCategory)
    {
        if (armorCategory == ArmorCategory.None)
            return true;

        return AllowedArmorCategories.Contains(armorCategory);
    }

    public bool AllowsWeaponType(WeaponType weaponType)
    {
        return AllowedWeaponTypes.Contains(weaponType);
    }

    /// <summary>
    /// Returns whether the supplied ability ID belongs to this class's authored
    /// ability pool.
    /// </summary>
    public bool ContainsAbility(string abilityContentId)
    {
        if (string.IsNullOrWhiteSpace(abilityContentId))
            return false;

        foreach (string candidate in AbilityContentIds)
        {
            if (string.Equals(
                candidate?.Trim(),
                abilityContentId.Trim(),
                StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
