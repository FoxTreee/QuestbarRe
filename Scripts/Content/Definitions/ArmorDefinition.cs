using Godot;
using System;
using System.Collections.Generic;

[GlobalClass]
public partial class ArmorDefinition : EquipmentDefinition
{
    [ExportCategory("Armor")]

    /// <summary>
    /// Authored wearable position for this item.
    /// Ring items may occupy Ring1 or Ring2.
    /// Trinket items may occupy Trinket1 or Trinket2.
    /// </summary>
    [Export]
    public ArmorEquipPosition EquipPosition { get; set; } =
        ArmorEquipPosition.Head;

    /// <summary>
    /// Raw armor supplied by this item.
    /// Armor mitigation is intentionally not implemented yet.
    /// </summary>
    [Export(PropertyHint.Range, "0,1000000,1")]
    public int ArmorValue { get; set; } = 0;


    public override IReadOnlyList<string> GetValidationErrors()
    {
        List<string> errors =
            new(base.GetValidationErrors());

        if (!ContentId.StartsWith(
            "armor.",
            StringComparison.Ordinal))
        {
            errors.Add(
                $"{ContentId}: armor Content ID must begin " +
                "with 'armor.'.");
        }

        if (!Enum.IsDefined(EquipPosition))
        {
            errors.Add(
                $"{ContentId}: EquipPosition is invalid.");
        }

        if (ArmorValue < 0)
        {
            errors.Add(
                $"{ContentId}: ArmorValue cannot be negative.");
        }

        return errors;
    }


    /// <summary>
    /// Returns whether this wearable definition may occupy the requested
    /// runtime equipment slot. Hero/class/level restrictions remain separate.
    /// </summary>
    public override bool CanEquipInSlot(
        EquipmentSlot slot)
    {
        return EquipPosition switch
        {
            ArmorEquipPosition.Head =>
                slot == EquipmentSlot.Head,

            ArmorEquipPosition.Necklace =>
                slot == EquipmentSlot.Necklace,

            ArmorEquipPosition.Shoulders =>
                slot == EquipmentSlot.Shoulders,

            ArmorEquipPosition.Chest =>
                slot == EquipmentSlot.Chest,

            ArmorEquipPosition.Back =>
                slot == EquipmentSlot.Back,

            ArmorEquipPosition.GuildTabard =>
                slot == EquipmentSlot.GuildTabard,

            ArmorEquipPosition.Wrists =>
                slot == EquipmentSlot.Wrists,

            ArmorEquipPosition.Hands =>
                slot == EquipmentSlot.Hands,

            ArmorEquipPosition.Belt =>
                slot == EquipmentSlot.Belt,

            ArmorEquipPosition.Legs =>
                slot == EquipmentSlot.Legs,

            ArmorEquipPosition.Boots =>
                slot == EquipmentSlot.Boots,

            ArmorEquipPosition.Ring =>
                slot == EquipmentSlot.Ring1
                || slot == EquipmentSlot.Ring2,

            ArmorEquipPosition.Trinket =>
                slot == EquipmentSlot.Trinket1
                || slot == EquipmentSlot.Trinket2,

            _ => false
        };
    }
}
