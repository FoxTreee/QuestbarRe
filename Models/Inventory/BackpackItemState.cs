using Godot;

/// <summary>
/// Runtime identity and presentation data for one item stored in the Backpack.
/// This is deliberately separate from ItemSlotView so UI never becomes the
/// authoritative owner of an item.
/// </summary>
public sealed class BackpackItemState
{
    public string ItemId { get; }
    public string DisplayName { get; }
    public Texture2D? IconTexture { get; }
    public int Quantity { get; }
    public int MaximumStackSize { get; }
    public long? StackId { get; }
    public long? UniqueInstanceId { get; }
    public IResolvedEquipmentProfile? EquipmentProfile { get; }

    /// <summary>
    /// Capacity contributed only while this item occupies a BagEquipment
    /// location. Zero identifies an ordinary non-bag item.
    /// </summary>
    public int AddedInventorySlots { get; }

    public bool IsStackable => StackId.HasValue;

    public bool IsBag => AddedInventorySlots > 0;

    private BackpackItemState(
        string itemId,
        string displayName,
        Texture2D? iconTexture,
        int quantity,
        long? stackId,
        long? uniqueInstanceId,
        int addedInventorySlots,
        IResolvedEquipmentProfile? equipmentProfile = null,
        int maximumStackSize = 1)
    {
        ItemId = itemId;
        DisplayName = displayName;
        IconTexture = iconTexture;
        Quantity = quantity;
        MaximumStackSize = Mathf.Max(1, maximumStackSize);
        StackId = stackId;
        UniqueInstanceId = uniqueInstanceId;
        AddedInventorySlots = Mathf.Max(0, addedInventorySlots);
        EquipmentProfile = equipmentProfile;
    }

    public static BackpackItemState CreateUnique(
        string itemId,
        string displayName,
        Texture2D? iconTexture,
        long uniqueInstanceId)
    {
        return new BackpackItemState(
            itemId,
            displayName,
            iconTexture,
            1,
            null,
            uniqueInstanceId,
            0);
    }

    public static BackpackItemState CreateEquipment(
        EquipmentDefinition definition,
        long uniqueInstanceId)
    {
        IResolvedEquipmentProfile profile = definition switch
        {
            WeaponDefinition weapon => ResolvedWeaponProfile.FromDefinition(weapon),
            ArmorDefinition armor => ResolvedArmorProfile.FromDefinition(armor),
            ShieldDefinition shield => ResolvedShieldProfile.FromDefinition(shield),
            _ => throw new System.ArgumentException(
                $"Unsupported equipment definition type '{definition.GetType().Name}'.",
                nameof(definition))
        };

        return CreateEquipment(profile, uniqueInstanceId);
    }

    public static BackpackItemState CreateEquipment(
        IResolvedEquipmentProfile profile,
        long uniqueInstanceId)
    {
        return new BackpackItemState(
            profile.DefinitionContentId,
            profile.DisplayName,
            profile.IconTexture,
            1,
            null,
            uniqueInstanceId,
            0,
            profile);
    }

    public static BackpackItemState CreateStack(
        string itemId,
        string displayName,
        Texture2D? iconTexture,
        long stackId,
        int quantity,
        int maximumStackSize)
    {
        return new BackpackItemState(
            itemId,
            displayName,
            iconTexture,
            Mathf.Clamp(quantity, 1, maximumStackSize),
            stackId,
            null,
            0,
            null,
            maximumStackSize);
    }

    public static BackpackItemState CreateBag(
        BagDefinition definition,
        long uniqueInstanceId)
    {
        return new BackpackItemState(
            definition.ContentId,
            definition.DisplayName,
            definition.IconTexture,
            1,
            null,
            uniqueInstanceId,
            definition.AddedInventorySlots);
    }

    public static BackpackItemState CreateInventoryItem(
        ItemDefinition definition,
        long identity,
        int quantity = 1)
    {
        if (definition is BagDefinition bag)
            return CreateBag(bag, identity);
        if (definition is EquipmentDefinition equipment)
            return CreateEquipment(equipment, identity);
        if (definition.IsStackable)
            return CreateStack(definition.ContentId, definition.DisplayName,
                definition.IconTexture, identity, quantity,
                definition.MaximumStackSize);
        return CreateUnique(definition.ContentId, definition.DisplayName,
            definition.IconTexture, identity);
    }

    public BackpackItemState WithQuantity(int quantity)
    {
        if (!IsStackable)
            throw new System.InvalidOperationException("Only stacks can change quantity.");
        return new BackpackItemState(ItemId, DisplayName, IconTexture,
            quantity, StackId, null, 0, null, MaximumStackSize);
    }

    public BackpackItemState CreateSplitStack(long newStackId, int quantity)
    {
        if (!IsStackable)
            throw new System.InvalidOperationException("Only stacks can be split.");
        return new BackpackItemState(ItemId, DisplayName, IconTexture,
            quantity, newStackId, null, 0, null, MaximumStackSize);
    }
}
