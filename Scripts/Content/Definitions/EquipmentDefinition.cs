using Godot;
using System.Collections.Generic;

[GlobalClass]
public partial class EquipmentDefinition : Resource
{
    [ExportCategory("Identity")]

    /// <summary>
    /// Stable template/content identifier. This identifies what an item is,
    /// not a player's future server-issued item instance.
    /// </summary>
    [Export(PropertyHint.PlaceholderText, "equipment.core.example")]
    public string ContentId { get; set; } =
        string.Empty;

    [Export(PropertyHint.PlaceholderText, "Example Equipment")]
    public string DisplayName { get; set; } =
        "Unnamed Equipment";

    [Export(PropertyHint.MultilineText)]
    public string Description { get; set; } =
        string.Empty;


    [ExportCategory("Requirements")]

    /// <summary>
    /// Minimum hero level required to equip this item.
    /// </summary>
    [Export(PropertyHint.Range, "1,1000,1")]
    public int RequiredLevel { get; set; } = 1;


    [ExportCategory("Core Stats")]

    [Export]
    public int Strength { get; set; } = 0;

    [Export]
    public int Agility { get; set; } = 0;

    [Export]
    public int Stamina { get; set; } = 0;

    [Export]
    public int Intellect { get; set; } = 0;

    [Export]
    public int Spirit { get; set; } = 0;


    [ExportCategory("Percentage Equip Modifiers")]

    /// <summary>
    /// Data-only percentage modifiers such as future critical-strike, dodge,
    /// healing, or other equipment effects. These values are authored now but
    /// do not change gameplay until their mechanics are explicitly implemented.
    /// </summary>
    [Export]
    public Godot.Collections.Array
        <EquipmentPercentageModifierDefinition> PercentageModifiers
    { get; set; } = new();


    public virtual IReadOnlyList<string> GetValidationErrors()
    {
        List<string> errors = new();

        if (!global::ContentId.IsValid(ContentId))
        {
            errors.Add(
                $"Invalid equipment Content ID '{ContentId}'.");
        }

        if (string.IsNullOrWhiteSpace(DisplayName))
        {
            errors.Add(
                $"{ContentId}: DisplayName is required.");
        }

        if (RequiredLevel < 1)
        {
            errors.Add(
                $"{ContentId}: RequiredLevel must be at least 1.");
        }

        foreach (EquipmentPercentageModifierDefinition modifier
            in PercentageModifiers)
        {
            if (!GodotObject.IsInstanceValid(modifier))
            {
                errors.Add(
                    $"{ContentId}: PercentageModifiers contains " +
                    "a missing modifier resource.");

                continue;
            }

            foreach (string modifierError
                in modifier.GetValidationErrors())
            {
                errors.Add(
                    $"{ContentId}: invalid equipment modifier: " +
                    modifierError);
            }
        }

        return errors;
    }
}
