using Godot;
using System;
using System.Collections.Generic;

[GlobalClass]
public partial class BagDefinition : EquipmentDefinition
{
    [ExportCategory("Bag Capacity")]

    /// <summary>
    /// Number of general Backpack storage locations contributed while this bag
    /// occupies one of the four dedicated BagEquipment locations.
    /// </summary>
    [Export(PropertyHint.Range, "2,128,2")]
    public int AddedInventorySlots { get; set; } = 4;

    public override IReadOnlyList<string> GetValidationErrors()
    {
        List<string> errors = new(base.GetValidationErrors());

        if (!ContentId.StartsWith("bag.", StringComparison.Ordinal))
        {
            errors.Add(
                $"{ContentId}: bag Content ID must begin with 'bag.'.");
        }

        if (AddedInventorySlots < 2)
        {
            errors.Add(
                $"{ContentId}: AddedInventorySlots must be at least 2.");
        }

        if (AddedInventorySlots % 2 != 0)
        {
            errors.Add(
                $"{ContentId}: AddedInventorySlots must be divisible by 2.");
        }

        return errors;
    }
}
