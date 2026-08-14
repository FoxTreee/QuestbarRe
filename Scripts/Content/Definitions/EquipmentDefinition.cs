using Godot;
using System.Collections.Generic;

[GlobalClass]
public partial class EquipmentDefinition : ItemDefinition
{
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


    public override IReadOnlyList<string> GetValidationErrors()
    {
        List<string> errors = new(base.GetValidationErrors());

        if (MaximumStackSize != 1)
            errors.Add($"{ContentId}: Equipment must have MaximumStackSize 1.");

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

    /// <summary>
    /// Returns whether this item definition is eligible for the requested
    /// equipment slot. Specific equipment types override this with their own
    /// authored placement rules.
    /// </summary>
    public virtual bool CanEquipInSlot(EquipmentSlot slot)
    {
        return false;
    }
}
