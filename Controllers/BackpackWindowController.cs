using Godot;
using System.Collections.Generic;

public partial class BackpackWindowController : Node
{
    public event System.Action<
        ItemSlotView,
        ItemSlotView.SlotPurpose,
        ItemSlotView.SlotContent,
        int,
        string,
        long?,
        long?>? CharacterEquipmentDropRequested;

    public System.Func<
        ItemSlotView,
        ItemSlotView.SlotPurpose,
        ItemSlotView.SlotContent,
        int,
        string,
        long?,
        long?,
        bool>? CharacterEquipmentDropValidator { get; set; }
    [ExportCategory("Backpack UI")]

    /// <summary>
    /// Root Control of the manually authored Backpack window. All Storage and
    /// BagEquipment ItemSlotView descendants are discovered beneath this node.
    /// </summary>
    [Export]
    public Control BackpackRoot { get; set; } = null!;

    [Export]
    public LineEdit SearchEdit { get; set; } = null!;

    /// <summary>
    /// Reusable ItemSlot scene used only for capacity beyond the manually
    /// authored starting 16 slots.
    /// </summary>
    [Export]
    public PackedScene ItemSlotScene { get; set; } = null!;

    /// <summary>
    /// Horizontal container beside the base 4x4 grid. The controller adds up
    /// to four vertical columns of four expansion slots here.
    /// </summary>
    [Export]
    public HBoxContainer UpperExpansionColumns { get; set; } = null!;

    /// <summary>
    /// Eight-column grid beneath the upper storage area for capacity beyond
    /// the first 32 total storage locations.
    /// </summary>
    [Export]
    public GridContainer LowerExpansionGrid { get; set; } = null!;

    [ExportCategory("Item Presentation")]

    [Export]
    public Texture2D? FallbackItemIcon { get; set; }

    [ExportCategory("28A2 Runtime Test")]

    /// <summary>
    /// Optional local equipment definition placed in one Backpack storage slot
    /// so the authority-to-presentation path can be tested. Clear this
    /// assignment after 28A2 is verified.
    /// </summary>
    [Export]
    public EquipmentDefinition? TestStartingItem { get; set; }

    [Export(PropertyHint.Range, "0,15,1")]
    public int TestStartingStorageSlot { get; set; } = 0;

    /// <summary>
    /// Optional second unique item used to verify occupied-slot swaps. It is
    /// test seeding only and can be cleared after checkpoint 28A4.
    /// </summary>
    [Export]
    public EquipmentDefinition? TestSecondStartingItem { get; set; }

    [Export(PropertyHint.Range, "0,15,1")]
    public int TestSecondStartingStorageSlot { get; set; } = 1;

    /// <summary>
    /// Optional BagDefinition used to verify bag-only placement and calculated
    /// capacity without generating any additional UI slots yet.
    /// </summary>
    [Export]
    public BagDefinition? TestStartingBag { get; set; }

    [Export(PropertyHint.Range, "0,3,1")]
    public int TestStartingBagSlot { get; set; } = 0;

    /// <summary>
    /// Optional second bag seeded into ordinary storage for testing runtime
    /// bag equipping, replacement, removal, and bag-slot swapping.
    /// </summary>
    [Export]
    public BagDefinition? TestStoredBag { get; set; }

    [Export(PropertyHint.Range, "0,15,1")]
    public int TestStoredBagStorageSlot { get; set; } = 2;

    [ExportCategory("Currency Display")]

    [Export]
    public Label GoldValueLabel { get; set; } = null!;

    [Export]
    public Label SilverValueLabel { get; set; } = null!;

    [Export]
    public Label CopperValueLabel { get; set; } = null!;

    private readonly Dictionary<int, ItemSlotView>
        _storageSlotViews = new();

    private readonly Dictionary<int, ItemSlotView>
        _bagSlotViews = new();

    public BackpackInventoryState Inventory { get; private set; } = new();

    /// <summary>
    /// Maps authored Backpack slots to authoritative runtime locations,
    /// optionally seeds one test item, and renders every location from state.
    /// </summary>
    public override void _Ready()
    {
        if (!ValidateReferences())
            return;

        if (!DiscoverAndValidateSlots())
            return;

        SeedOptionalTestItem();
        SeedOptionalSecondTestItem();
        SeedOptionalTestBag();
        SeedOptionalStoredBag();
        BuildExpansionSlotViews();
        RefreshAllSlots();

        DebugLog.Print(
            $"Backpack inventory initialized. BaseCapacity=" +
            $"{BackpackInventoryState.BaseStorageSlotCount}, " +
            $"BagCapacity={Inventory.AddedBagCapacity}, " +
            $"TotalCapacity={Inventory.TotalStorageCapacity}, " +
            $"RuntimeStorageLocations=" +
            $"{Inventory.StorageLocations.Count}.");
    }

    /// <summary>
    /// Rebuilds all Backpack slot visuals from authoritative inventory state.
    /// ItemSlotView never owns or mutates the item records presented here.
    /// </summary>
    public void RefreshAllSlots()
    {
        RefreshGroup(
            BackpackLocationKind.Storage,
            _storageSlotViews);

        RefreshGroup(
            BackpackLocationKind.BagEquipment,
            _bagSlotViews);
    }

    public BackpackItemState? GetStorageItem(int storageIndex)
    {
        return Inventory.GetLocation(
            BackpackLocationKind.Storage,
            storageIndex).Item;
    }

    public bool TryExchangeStorageItem(
        int storageIndex,
        BackpackItemState? expectedStorageItem,
        BackpackItemState? incomingEquipment,
        out BackpackItemState? outgoingStorageItem,
        out string error)
    {
        bool succeeded = Inventory.TryExchangeStorageItem(
            storageIndex,
            expectedStorageItem,
            incomingEquipment,
            out outgoingStorageItem,
            out error);

        if (succeeded)
            RefreshAllSlots();

        return succeeded;
    }

    public void SetCurrencyDisplay(
        int gold,
        int silver,
        int copper)
    {
        GoldValueLabel.Text = Mathf.Max(0, gold).ToString();
        SilverValueLabel.Text = Mathf.Max(0, silver).ToString();
        CopperValueLabel.Text = Mathf.Max(0, copper).ToString();
    }

    private bool DiscoverAndValidateSlots()
    {
        _storageSlotViews.Clear();
        _bagSlotViews.Clear();

        List<ItemSlotView> discovered = new();
        CollectItemSlots(BackpackRoot, discovered);

        bool valid = true;

        foreach (ItemSlotView slotView in discovered)
        {
            ConfigureSlotInteraction(slotView);

            Dictionary<int, ItemSlotView>? destination =
                slotView.Purpose switch
                {
                    ItemSlotView.SlotPurpose.Storage => _storageSlotViews,
                    ItemSlotView.SlotPurpose.BagEquipment => _bagSlotViews,
                    _ => null
                };

            if (destination is null)
                continue;

            int expectedCount =
                slotView.Purpose == ItemSlotView.SlotPurpose.Storage
                    ? BackpackInventoryState.BaseStorageSlotCount
                    : BackpackInventoryState.BagEquipmentSlotCount;

            if (slotView.SlotIndex < 0 ||
                slotView.SlotIndex >= expectedCount)
            {
                GD.PushError(
                    $"Backpack ItemSlotView '{slotView.GetPath()}' has " +
                    $"invalid {slotView.Purpose} index " +
                    $"{slotView.SlotIndex}. Expected 0 through " +
                    $"{expectedCount - 1}.");
                valid = false;
                continue;
            }

            if (!destination.TryAdd(slotView.SlotIndex, slotView))
            {
                GD.PushError(
                    $"Backpack contains duplicate {slotView.Purpose} " +
                    $"index {slotView.SlotIndex}.");
                valid = false;
            }
        }

        valid &= ValidateDiscoveredCount(
            ItemSlotView.SlotPurpose.Storage,
            _storageSlotViews.Count,
            BackpackInventoryState.BaseStorageSlotCount);

        valid &= ValidateDiscoveredCount(
            ItemSlotView.SlotPurpose.BagEquipment,
            _bagSlotViews.Count,
            BackpackInventoryState.BagEquipmentSlotCount);

        return valid;
    }

    private void SeedOptionalTestItem()
    {
        if (!GodotObject.IsInstanceValid(TestStartingItem))
            return;

        BackpackItemState item = BackpackItemState.CreateEquipment(
            TestStartingItem!,
            BuildTestInstanceId(TestStartingItem.ContentId, 1));

        if (!Inventory.TryPlaceInEmptyLocation(
            BackpackLocationKind.Storage,
            TestStartingStorageSlot,
            item,
            out string error))
        {
            GD.PushError(
                $"Could not seed the 28A2 Backpack test item: {error}");
        }
    }

    private void SeedOptionalSecondTestItem()
    {
        if (!GodotObject.IsInstanceValid(TestSecondStartingItem))
            return;

        BackpackItemState item = BackpackItemState.CreateEquipment(
            TestSecondStartingItem!,
            BuildTestInstanceId(TestSecondStartingItem.ContentId, 2));

        if (!Inventory.TryPlaceInEmptyLocation(
            BackpackLocationKind.Storage,
            TestSecondStartingStorageSlot,
            item,
            out string error))
        {
            GD.PushError(
                $"Could not seed the 28A4 second test item: {error}");
        }
    }

    private void RefreshGroup(
        BackpackLocationKind kind,
        IReadOnlyDictionary<int, ItemSlotView> slotViews)
    {
        foreach (KeyValuePair<int, ItemSlotView> pair in slotViews)
        {
            BackpackInventoryLocation location =
                Inventory.GetLocation(kind, pair.Key);

            PresentLocation(pair.Value, location);
        }
    }

    private void SeedOptionalTestBag()
    {
        if (!GodotObject.IsInstanceValid(TestStartingBag))
            return;

        IReadOnlyList<string> validationErrors =
            TestStartingBag!.GetValidationErrors();

        if (validationErrors.Count > 0)
        {
            foreach (string validationError in validationErrors)
            {
                GD.PushError(
                    $"Invalid 28A3 test bag: {validationError}");
            }

            return;
        }

        BackpackItemState bag = BackpackItemState.CreateBag(
            TestStartingBag,
            BuildTestInstanceId(TestStartingBag.ContentId, 3));

        if (!Inventory.TryPlaceInEmptyLocation(
            BackpackLocationKind.BagEquipment,
            TestStartingBagSlot,
            bag,
            out string error))
        {
            GD.PushError(
                $"Could not seed the 28A3 Backpack test bag: {error}");
        }
    }

    private void SeedOptionalStoredBag()
    {
        if (!GodotObject.IsInstanceValid(TestStoredBag))
            return;

        IReadOnlyList<string> validationErrors =
            TestStoredBag!.GetValidationErrors();

        if (validationErrors.Count > 0)
        {
            foreach (string validationError in validationErrors)
                GD.PushError($"Invalid 28A5 stored test bag: {validationError}");
            return;
        }

        BackpackItemState bag = BackpackItemState.CreateBag(
            TestStoredBag,
            BuildTestInstanceId(TestStoredBag.ContentId, 4));

        if (!Inventory.TryPlaceInEmptyLocation(
            BackpackLocationKind.Storage,
            TestStoredBagStorageSlot,
            bag,
            out string error))
        {
            GD.PushError($"Could not seed the 28A5 stored test bag: {error}");
        }
    }

    /// <summary>
    /// Creates views only for capacity unlocked beyond the manually authored
    /// base 16. Slots 16-31 fill four-slot columns to the right; later slots
    /// fill an eight-column grid beneath the upper storage area.
    /// </summary>
    private void BuildExpansionSlotViews()
    {
        const int upperExpansionCapacity = 16;
        const int slotsPerUpperColumn = 4;

        int expansionCount =
            Inventory.TotalStorageCapacity -
            BackpackInventoryState.BaseStorageSlotCount;

        int upperCount = Mathf.Min(
            expansionCount,
            upperExpansionCapacity);

        int nextStorageIndex =
            BackpackInventoryState.BaseStorageSlotCount;

        int upperColumnCount =
            (upperCount + slotsPerUpperColumn - 1) /
            slotsPerUpperColumn;

        for (int columnIndex = 0;
            columnIndex < upperColumnCount;
            columnIndex++)
        {
            VBoxContainer column = new()
            {
                Name = $"ExpansionColumn{columnIndex + 1}"
            };

            UpperExpansionColumns.AddChild(column);

            int slotsInColumn = Mathf.Min(
                slotsPerUpperColumn,
                upperCount -
                columnIndex * slotsPerUpperColumn);

            for (int rowIndex = 0;
                rowIndex < slotsInColumn;
                rowIndex++)
            {
                CreateExpansionSlot(
                    column,
                    nextStorageIndex++);
            }
        }

        while (nextStorageIndex < Inventory.TotalStorageCapacity)
        {
            CreateExpansionSlot(
                LowerExpansionGrid,
                nextStorageIndex++);
        }

        UpperExpansionColumns.Visible = upperCount > 0;
        LowerExpansionGrid.Visible =
            expansionCount > upperExpansionCapacity;
    }

    /// <summary>
    /// Instantiates one reusable storage-slot view and maps it to its existing
    /// authoritative runtime location and enables storage interaction.
    /// </summary>
    private void CreateExpansionSlot(
        Control parent,
        int storageIndex)
    {
        ItemSlotView? slotView =
            ItemSlotScene.Instantiate<ItemSlotView>();

        if (slotView is null)
        {
            GD.PushError(
                $"ItemSlotScene did not instantiate an ItemSlotView for " +
                $"Backpack storage index {storageIndex}.");
            return;
        }

        slotView.Name = $"InventorySlot{storageIndex + 1}";
        parent.AddChild(slotView);
        slotView.ConfigureSlot(
            ItemSlotView.SlotPurpose.Storage,
            storageIndex);
        ConfigureSlotInteraction(slotView);

        if (!_storageSlotViews.TryAdd(storageIndex, slotView))
        {
            GD.PushError(
                $"Backpack generated duplicate Storage index " +
                $"{storageIndex}.");
            slotView.QueueFree();
        }
    }

    /// <summary>
    /// Enables only Backpack storage movement for 28A4 and routes all drop
    /// decisions and requests through this authoritative controller.
    /// </summary>
    private void ConfigureSlotInteraction(ItemSlotView slotView)
    {
        if (slotView.Purpose == ItemSlotView.SlotPurpose.CharacterEquipment)
        {
            slotView.DragEnabled = false;
            slotView.DropValidator = null;
            return;
        }

        slotView.DragEnabled = true;
        slotView.DropValidator = CanAcceptBackpackDrop;
        slotView.DropRequested -= HandleBackpackDropRequested;
        slotView.DropRequested += HandleBackpackDropRequested;
    }

    private bool CanAcceptBackpackDrop(
        ItemSlotView destination,
        ItemSlotView.SlotPurpose sourcePurpose,
        ItemSlotView.SlotContent sourceContent,
        int sourceSlotIndex,
        string itemId,
        long? stackId,
        long? uniqueInstanceId)
    {
        if (sourcePurpose == ItemSlotView.SlotPurpose.CharacterEquipment)
        {
            return CharacterEquipmentDropValidator?.Invoke(
                destination,
                sourcePurpose,
                sourceContent,
                sourceSlotIndex,
                itemId,
                stackId,
                uniqueInstanceId) ?? false;
        }

        if (destination.Purpose == ItemSlotView.SlotPurpose.CharacterEquipment)
        {
            return false;
        }

        if (!TryMapLocationKind(sourcePurpose, out BackpackLocationKind sourceKind) ||
            !TryMapLocationKind(destination.Purpose, out BackpackLocationKind destinationKind))
        {
            return false;
        }

        BackpackInventoryLocation source;

        try
        {
            source = Inventory.GetLocation(
                sourceKind,
                sourceSlotIndex);
        }
        catch (System.ArgumentOutOfRangeException)
        {
            return false;
        }

        BackpackItemState? item = source.Item;
        if (item is null ||
            item.ItemId != itemId ||
            item.StackId != stackId ||
            item.UniqueInstanceId != uniqueInstanceId)
        {
            return false;
        }

        if (destinationKind == BackpackLocationKind.BagEquipment && !item.IsBag)
            return false;

        BackpackItemState? destinationItem = Inventory.GetLocation(
            destinationKind,
            destination.SlotIndex).Item;

        return
            item is not null &&
            (sourceKind != BackpackLocationKind.BagEquipment ||
                destinationItem is null ||
                destinationItem.IsBag);
    }

    private void HandleBackpackDropRequested(
        ItemSlotView destination,
        ItemSlotView.SlotPurpose sourcePurpose,
        ItemSlotView.SlotContent sourceContent,
        int sourceSlotIndex,
        string itemId,
        long? stackId,
        long? uniqueInstanceId)
    {
        if (sourcePurpose == ItemSlotView.SlotPurpose.CharacterEquipment)
        {
            CharacterEquipmentDropRequested?.Invoke(
                destination,
                sourcePurpose,
                sourceContent,
                sourceSlotIndex,
                itemId,
                stackId,
                uniqueInstanceId);
            return;
        }

        if (!TryMapLocationKind(sourcePurpose, out BackpackLocationKind sourceKind) ||
            !TryMapLocationKind(destination.Purpose, out BackpackLocationKind destinationKind))
        {
            return;
        }

        bool succeeded;
        string error;

        if (sourceKind == BackpackLocationKind.Storage &&
            destinationKind == BackpackLocationKind.Storage)
        {
            succeeded = Inventory.TryMoveOrSwapStorageItem(
                sourceSlotIndex,
                destination.SlotIndex,
                itemId,
                stackId,
                uniqueInstanceId,
                out error);
        }
        else
        {
            succeeded = Inventory.TryMoveOrSwapBagItem(
                sourceKind,
                sourceSlotIndex,
                destinationKind,
                destination.SlotIndex,
                itemId,
                stackId,
                uniqueInstanceId,
                out error);
        }

        if (!succeeded)
        {
            GD.PushWarning($"Backpack drop rejected: {error}");
            RefreshAllSlots();
            return;
        }

        RebuildExpansionSlotViews();
        RefreshAllSlots();
        DebugLog.Print(
            $"Backpack item moved: {sourceKind}[{sourceSlotIndex}] -> " +
            $"{destinationKind}[{destination.SlotIndex}]; ItemId={itemId}; " +
            $"StackId={stackId?.ToString() ?? "none"}; " +
            $"InstanceId={uniqueInstanceId?.ToString() ?? "none"}; " +
            $"TotalCapacity={Inventory.TotalStorageCapacity}.");
    }

    private void RebuildExpansionSlotViews()
    {
        List<int> generatedIndices = new();
        foreach (int index in _storageSlotViews.Keys)
        {
            if (index >= BackpackInventoryState.BaseStorageSlotCount)
                generatedIndices.Add(index);
        }

        foreach (int index in generatedIndices)
            _storageSlotViews.Remove(index);

        FreeContainerChildren(UpperExpansionColumns);
        FreeContainerChildren(LowerExpansionGrid);
        BuildExpansionSlotViews();

        // QueueFree removes the old expansion controls at the end of the
        // frame. Reset the Window afterward so Wrap Controls recalculates both
        // growth and shrinkage from the Backpack's new minimum content size.
        Callable.From(ResetBackpackWindowSize).CallDeferred();
    }

    private void ResetBackpackWindowSize()
    {
        Window backpackWindow = BackpackRoot.GetWindow();

        if (!GodotObject.IsInstanceValid(backpackWindow))
            return;

        backpackWindow.ResetSize();
    }

    private static void FreeContainerChildren(Control container)
    {
        foreach (Node child in container.GetChildren())
        {
            container.RemoveChild(child);
            child.QueueFree();
        }
    }

    private static bool TryMapLocationKind(
        ItemSlotView.SlotPurpose purpose,
        out BackpackLocationKind kind)
    {
        if (purpose == ItemSlotView.SlotPurpose.Storage)
        {
            kind = BackpackLocationKind.Storage;
            return true;
        }

        if (purpose == ItemSlotView.SlotPurpose.BagEquipment)
        {
            kind = BackpackLocationKind.BagEquipment;
            return true;
        }

        kind = default;
        return false;
    }

    private void PresentLocation(
        ItemSlotView slotView,
        BackpackInventoryLocation location)
    {
        BackpackItemState? item = location.Item;

        if (item is null)
        {
            slotView.ClearItemIdentity();
            slotView.TooltipText = string.Empty;
            return;
        }

        if (item.IsStackable)
        {
            slotView.SetStackableItemIdentity(
                item.ItemId,
                item.StackId!.Value,
                item.Quantity);
        }
        else
        {
            slotView.SetUniqueItemIdentity(
                item.ItemId,
                item.UniqueInstanceId!.Value);
        }

        slotView.SetItemTexture(item.IconTexture ?? FallbackItemIcon);
        slotView.TooltipText = item.DisplayName;
    }

    private static void CollectItemSlots(
        Node parent,
        List<ItemSlotView> results)
    {
        foreach (Node child in parent.GetChildren())
        {
            if (child is ItemSlotView slotView)
                results.Add(slotView);

            CollectItemSlots(child, results);
        }
    }

    private static bool ValidateDiscoveredCount(
        ItemSlotView.SlotPurpose purpose,
        int actual,
        int expected)
    {
        if (actual == expected)
            return true;

        GD.PushError(
            $"Backpack expected {expected} authored {purpose} slots " +
            $"but discovered {actual}.");

        return false;
    }

    private bool ValidateReferences()
    {
        bool valid = true;

        valid &= Require(BackpackRoot, nameof(BackpackRoot));
        valid &= Require(SearchEdit, nameof(SearchEdit));
        valid &= Require(ItemSlotScene, nameof(ItemSlotScene));
        valid &= Require(
            UpperExpansionColumns,
            nameof(UpperExpansionColumns));
        valid &= Require(
            LowerExpansionGrid,
            nameof(LowerExpansionGrid));
        valid &= Require(GoldValueLabel, nameof(GoldValueLabel));
        valid &= Require(SilverValueLabel, nameof(SilverValueLabel));
        valid &= Require(CopperValueLabel, nameof(CopperValueLabel));

        return valid;
    }

    private static bool Require(
        GodotObject value,
        string propertyName)
    {
        if (GodotObject.IsInstanceValid(value))
            return true;

        GD.PushError(
            $"BackpackWindowController is missing Inspector " +
            $"reference '{propertyName}'.");

        return false;
    }

    private static long BuildTestInstanceId(string itemId, int seedNumber)
    {
        return
            280_200_000_000L +
            seedNumber * 10_000_000_000L +
            (uint)itemId.GetHashCode();
    }
}
