using Godot;

[GlobalClass]
public partial class ItemSlotVisualCatalog : Resource
{
    [ExportCategory("Generic")]
    [Export] public Texture2D? GenericEmptySlot { get; set; }

    [ExportCategory("Armor")]
    [Export] public Texture2D? Head { get; set; }
    [Export] public Texture2D? Necklace { get; set; }
    [Export] public Texture2D? Shoulders { get; set; }
    [Export] public Texture2D? Chest { get; set; }
    [Export] public Texture2D? Back { get; set; }
    [Export] public Texture2D? Wrists { get; set; }
    [Export] public Texture2D? Hands { get; set; }
    [Export] public Texture2D? Belt { get; set; }
    [Export] public Texture2D? Legs { get; set; }
    [Export] public Texture2D? Boots { get; set; }

    [ExportCategory("Accessories")]
    [Export] public Texture2D? Ring { get; set; }
    [Export] public Texture2D? Trinket { get; set; }

    [ExportCategory("Bags")]
    [Export] public Texture2D? Bag { get; set; }

    [ExportCategory("Weapons")]
    [Export] public Texture2D? MainHand { get; set; }
    [Export] public Texture2D? OffHand { get; set; }
    [Export] public Texture2D? Ranged { get; set; }

    /// <summary>
    /// Returns the authored empty-slot artwork for a non-character slot purpose.
    /// BagEquipment uses the dedicated faded bag silhouette while ordinary
    /// storage uses the generic empty-slot artwork.
    /// </summary>
    public Texture2D? GetEmptySlotTexture(
        ItemSlotView.SlotPurpose purpose)
    {
        return purpose switch
        {
            ItemSlotView.SlotPurpose.BagEquipment =>
                Bag,

            _ =>
                GenericEmptySlot
        };
    }

    /// <summary>
    /// Returns the authored empty-slot artwork for a character equipment slot.
    /// Paired ring and trinket slots intentionally share one visual.
    /// </summary>
    public Texture2D? GetEmptySlotTexture(EquipmentSlot slot)
    {
        return slot switch
        {
            EquipmentSlot.Head => Head,
            EquipmentSlot.Necklace => Necklace,
            EquipmentSlot.Shoulders => Shoulders,
            EquipmentSlot.Chest => Chest,
            EquipmentSlot.Back => Back,
            EquipmentSlot.Wrists => Wrists,
            EquipmentSlot.Hands => Hands,
            EquipmentSlot.Belt => Belt,
            EquipmentSlot.Legs => Legs,
            EquipmentSlot.Boots => Boots,
            EquipmentSlot.Ring1 or EquipmentSlot.Ring2 => Ring,
            EquipmentSlot.Trinket1 or EquipmentSlot.Trinket2 => Trinket,
            EquipmentSlot.MainHand => MainHand,
            EquipmentSlot.OffHand => OffHand,
            EquipmentSlot.Ranged => Ranged,
            _ => GenericEmptySlot
        };
    }
}