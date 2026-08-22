using Godot;
using System.Collections.Generic;

[GlobalClass]
public partial class MonsterLootEntry : Resource
{
    [ExportCategory("Item Drop")]

    /// <summary>
    /// Inventory item rolled by this monster. The chance belongs to this
    /// monster entry, so the same item can use different chances elsewhere.
    /// </summary>
    [Export(PropertyHint.PlaceholderText, "material.core.example")]
    public string ItemContentId { get; set; } = string.Empty;

    /// <summary>
    /// Independent chance for this entry to drop whenever the monster dies.
    /// Every entry in the monster's loot table receives its own roll.
    /// </summary>
    [Export(PropertyHint.Range, "0,100,0.1,suffix:%")]
    public float DropChancePercent { get; set; } = 0.0f;

    [ExportCategory("Quantity")]

    /// <summary>
    /// Smallest quantity awarded after this entry passes its drop roll.
    /// </summary>
    [Export(PropertyHint.Range, "1,1000000,1")]
    public int MinimumQuantity { get; set; } = 1;

    /// <summary>
    /// Largest quantity awarded after this entry passes its drop roll. Set it
    /// equal to MinimumQuantity when the drop should always use a fixed amount.
    /// </summary>
    [Export(PropertyHint.Range, "1,1000000,1")]
    public int MaximumQuantity { get; set; } = 1;

    /// <summary>
    /// Validates this monster-owned item drop before the monster is registered.
    /// ItemContentRegistry performs the separate registered-item check.
    /// </summary>
    public IReadOnlyList<string> GetValidationErrors()
    {
        List<string> errors = new();

        if (!global::ContentId.IsValid(ItemContentId))
        {
            errors.Add(
                $"invalid loot item Content ID '{ItemContentId}'.");
        }

        if (DropChancePercent < 0.0f || DropChancePercent > 100.0f)
        {
            errors.Add(
                $"{ItemContentId}: DropChancePercent must be between 0 and 100.");
        }

        if (MinimumQuantity < 1)
        {
            errors.Add(
                $"{ItemContentId}: MinimumQuantity must be at least one.");
        }

        if (MaximumQuantity < MinimumQuantity)
        {
            errors.Add(
                $"{ItemContentId}: MaximumQuantity must be greater than or equal to MinimumQuantity.");
        }

        return errors;
    }
}
