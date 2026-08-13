using Godot;
using System;
using System.Collections.Generic;

[GlobalClass]
public partial class ShieldDefinition : EquipmentDefinition
{
    [ExportCategory("Shield")]

    /// <summary>
    /// Raw armor supplied by this shield.
    /// Shields always occupy Off Hand.
    /// Armor mitigation is intentionally not implemented yet.
    /// </summary>
    [Export(PropertyHint.Range, "0,1000000,1")]
    public int ArmorValue { get; set; } = 0;


    public override IReadOnlyList<string> GetValidationErrors()
    {
        List<string> errors =
            new(base.GetValidationErrors());

        if (!ContentId.StartsWith(
            "shield.",
            StringComparison.Ordinal))
        {
            errors.Add(
                $"{ContentId}: shield Content ID must begin " +
                "with 'shield.'.");
        }

        if (ArmorValue < 0)
        {
            errors.Add(
                $"{ContentId}: ArmorValue cannot be negative.");
        }

        return errors;
    }


    /// <summary>
    /// Shields are item-side eligible for Off Hand only.
    /// Whether a particular hero/class may use shields is a separate rule.
    /// </summary>
    public override bool CanEquipInSlot(
        EquipmentSlot slot)
    {
        return slot == EquipmentSlot.OffHand;
    }
}
