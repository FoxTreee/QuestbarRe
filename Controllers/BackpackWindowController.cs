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
    /// Complete custom Backpack panel, including its authored title bar.
    /// Its size follows BackpackRoot's content minimum size.
    /// </summary>
    [Export]
    public Control BackpackPanelRoot { get; set; } = null!;

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

    [ExportCategory("Item Actions")]
    [Export] public StackSplitPopupController StackSplitPopup { get; set; } = null!;
    [Export] public ItemTooltipController ItemTooltip { get; set; } = null!;

    [ExportCategory("Currency Display")]

    [Export]
    public Label GoldValueLabel { get; set; } = null!;

    [Export]
    public Label SilverValueLabel { get; set; } = null!;

    [Export]
    public Label CopperValueLabel { get; set; } = null!;

    [ExportCategory("Custom Panel")]

    /// <summary>
    /// Height reserved above BackpackRoot for the reusable custom title bar.
    /// </summary>
    [Export(PropertyHint.Range, "24,64,1")]
    public float CustomTitleBarHeight { get; set; } = 36.0f;

    private readonly Dictionary<int, ItemSlotView>
        _storageSlotViews = new();

    private readonly Dictionary<int, ItemSlotView>
        _bagSlotViews = new();

    private int _pendingSplitSourceIndex = -1;
    private long _pendingSplitStackId;
    private BackpackLocationKind _pendingActionKind;
    private int _pendingActionSourceIndex = -1;
    private string _pendingActionItemId = string.Empty;
    private long? _pendingActionStackId;
    private long? _pendingActionUniqueInstanceId;
    private string _storageSearchText = string.Empty;

    public BackpackInventoryState Inventory { get; private set; } = new();
    public CurrencyWallet Currency { get; } = new();

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

        BuildExpansionSlotViews();
        RefreshAllSlots();
        StackSplitPopup.SplitConfirmed += HandleSplitConfirmed;
        StackSplitPopup.DeleteConfirmed += HandleDeleteConfirmed;
        SearchEdit.TextChanged += HandleSearchTextChanged;
        Currency.BalanceChanged += RefreshCurrencyDisplay;
        RefreshCurrencyDisplay();

        DebugLog.Print(
            $"Backpack inventory initialized. BaseCapacity=" +
            $"{BackpackInventoryState.BaseStorageSlotCount}, " +
            $"BagCapacity={Inventory.AddedBagCapacity}, " +
            $"TotalCapacity={Inventory.TotalStorageCapacity}, " +
            $"RuntimeStorageLocations=" +
            $"{Inventory.StorageLocations.Count}.");
    }

    public override void _ExitTree()
    {
        if (GodotObject.IsInstanceValid(StackSplitPopup))
        {
            StackSplitPopup.SplitConfirmed -= HandleSplitConfirmed;
            StackSplitPopup.DeleteConfirmed -= HandleDeleteConfirmed;
        }

        if (GodotObject.IsInstanceValid(SearchEdit))
            SearchEdit.TextChanged -= HandleSearchTextChanged;

        Currency.BalanceChanged -= RefreshCurrencyDisplay;
    }

    private void HandleSearchTextChanged(string searchText)
    {
        _storageSearchText = searchText?.Trim() ?? string.Empty;
        RefreshAllSlots();
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

    public BackpackItemState? GetBackpackItem(
        ItemSlotView.SlotPurpose purpose,
        int index)
    {
        BackpackLocationKind kind = purpose switch
        {
            ItemSlotView.SlotPurpose.Storage => BackpackLocationKind.Storage,
            ItemSlotView.SlotPurpose.BagEquipment => BackpackLocationKind.BagEquipment,
            _ => throw new System.ArgumentOutOfRangeException(nameof(purpose))
        };
        return Inventory.GetLocation(kind, index).Item;
    }

    public bool TryFindOwnedItem(string itemId, out string location)
    {
        if (Inventory.TryFindItem(itemId, out BackpackLocationKind kind, out int index))
        {
            location = $"Backpack/{kind}[{index}]";
            return true;
        }
        location = string.Empty;
        return false;
    }

    public bool TryAcquireItem(ItemDefinition definition, int quantity,
        ref long nextInstanceId, ref long nextStackId, out string error)
    {
        bool added = Inventory.TryAcquire(definition, quantity,
            ref nextInstanceId, ref nextStackId, out error);
        if (added) RefreshAllSlots();
        return added;
    }

    /// <summary>
    /// Returns whether ordinary Backpack storage contains the requested item
    /// quantity. Equipped items and bag-equipment slots are intentionally excluded.
    /// </summary>
    public bool HasItemQuantity(string itemId, int quantity)
    {
        return quantity > 0
            && Inventory.GetItemQuantity(itemId) >= quantity;
    }

    /// <summary>
    /// Consumes items from authoritative Backpack storage and immediately
    /// refreshes the affected stack visuals.
    /// </summary>
    public bool TryConsumeItem(
        string itemId,
        int quantity,
        out string error)
    {
        bool consumed = Inventory.TryConsumeItem(
            itemId,
            quantity,
            out error);

        if (consumed)
            RefreshAllSlots();

        return consumed;
    }

    public void RebuildAfterRestore()
    {
        RebuildExpansionSlotViews();
        RefreshAllSlots();
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

    private void RefreshCurrencyDisplay()
    {
        GoldValueLabel.Text = Currency.Gold.ToString();
        SilverValueLabel.Text = Currency.Silver.ToString();
        CopperValueLabel.Text = Currency.Copper.ToString();
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

    private void RefreshGroup(
        BackpackLocationKind kind,
        IReadOnlyDictionary<int, ItemSlotView> slotViews)
    {
        foreach (KeyValuePair<int, ItemSlotView> pair in slotViews)
        {
            BackpackInventoryLocation location =
                Inventory.GetLocation(kind, pair.Key);

            if (kind == BackpackLocationKind.Storage &&
                location.Item is BackpackItemState item &&
                !MatchesStorageSearch(item))
            {
                PresentFilteredLocation(pair.Value);
                continue;
            }

            PresentLocation(pair.Value, location);
            ConfigureSlotInteraction(pair.Value);
        }
    }

    private bool MatchesStorageSearch(BackpackItemState item)
    {
        if (string.IsNullOrEmpty(_storageSearchText))
            return true;

        return
            item.DisplayName.Contains(
                _storageSearchText,
                System.StringComparison.OrdinalIgnoreCase) ||
            item.ItemId.Contains(
                _storageSearchText,
                System.StringComparison.OrdinalIgnoreCase);
    }

    private static void PresentFilteredLocation(ItemSlotView slotView)
    {
        slotView.ClearItemIdentity();
        slotView.TooltipText = string.Empty;
        slotView.DragEnabled = false;
        slotView.DropValidator = RejectFilteredDrop;
    }

    private static bool RejectFilteredDrop(
        ItemSlotView destination,
        ItemSlotView.SlotPurpose sourcePurpose,
        ItemSlotView.SlotContent sourceContent,
        int sourceSlotIndex,
        string itemId,
        long? stackId,
        long? uniqueInstanceId)
    {
        return false;
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
            slotView.ContextActionsRequested -= HandleContextActionsRequested;
            return;
        }

        slotView.DragEnabled = true;
        slotView.DropValidator = CanAcceptBackpackDrop;
        slotView.DropRequested -= HandleBackpackDropRequested;
        slotView.DropRequested += HandleBackpackDropRequested;
        slotView.SplitRequested -= HandleSplitRequested;
        slotView.SplitRequested += HandleSplitRequested;
        slotView.ContextActionsRequested -= HandleContextActionsRequested;
        slotView.ContextActionsRequested += HandleContextActionsRequested;
        ItemTooltip.RegisterSlot(slotView);
    }

    /// <summary>
    /// Opens item actions only after resolving the slot's current authoritative
    /// record. Character equipment must be unequipped before it can be deleted.
    /// </summary>
    private void HandleContextActionsRequested(ItemSlotView sourceView)
    {
        if (!TryMapLocationKind(
            sourceView.Purpose,
            out BackpackLocationKind kind))
        {
            return;
        }

        BackpackItemState? item = Inventory.GetLocation(
            kind,
            sourceView.SlotIndex).Item;

        if (item is null)
        {
            RefreshAllSlots();
            return;
        }

        _pendingActionKind = kind;
        _pendingActionSourceIndex = sourceView.SlotIndex;
        _pendingActionItemId = item.ItemId;
        _pendingActionStackId = item.StackId;
        _pendingActionUniqueInstanceId = item.UniqueInstanceId;

        if (kind == BackpackLocationKind.Storage
            && item.IsStackable
            && item.StackId.HasValue
            && item.Quantity > 1)
        {
            _pendingSplitSourceIndex = sourceView.SlotIndex;
            _pendingSplitStackId = item.StackId.Value;
        }
        else
        {
            _pendingSplitSourceIndex = -1;
            _pendingSplitStackId = 0;
        }

        StackSplitPopup.OpenActions(
            item.DisplayName,
            item.Quantity,
            item.IsStackable);
    }

    private void HandleSplitRequested(ItemSlotView sourceView, long stackId)
    {
        if (sourceView.Purpose != ItemSlotView.SlotPurpose.Storage)
            return;

        BackpackItemState? item = GetStorageItem(sourceView.SlotIndex);
        if (item is null || !item.IsStackable || item.StackId != stackId || item.Quantity < 2)
        {
            RefreshAllSlots();
            return;
        }

        _pendingSplitSourceIndex = sourceView.SlotIndex;
        _pendingSplitStackId = stackId;
        StackSplitPopup.Open(item.DisplayName, item.Quantity);
    }

    private void HandleSplitConfirmed(int quantity)
    {
        int sourceIndex = _pendingSplitSourceIndex;
        long stackId = _pendingSplitStackId;
        ClearPendingItemAction();

        if (!Inventory.TrySplitStack(sourceIndex, stackId, quantity,
            out int destinationIndex, out string error))
        {
            GD.PushWarning($"Stack split rejected: {error}");
            RefreshAllSlots();
            return;
        }

        RefreshAllSlots();
        DebugLog.Print(
            $"Split stack {stackId}: moved {quantity} from Storage[{sourceIndex}] " +
            $"into new stack at Storage[{destinationIndex}].");
    }

    private void HandleDeleteConfirmed()
    {
        BackpackLocationKind kind = _pendingActionKind;
        int sourceIndex = _pendingActionSourceIndex;
        string itemId = _pendingActionItemId;
        long? stackId = _pendingActionStackId;
        long? uniqueInstanceId = _pendingActionUniqueInstanceId;
        ClearPendingItemAction();

        if (sourceIndex < 0)
        {
            GD.PushWarning("Item delete rejected: no item action is pending.");
            RefreshAllSlots();
            return;
        }

        if (!Inventory.TryDeleteItem(
                kind,
                sourceIndex,
                itemId,
                stackId,
                uniqueInstanceId,
                out BackpackItemState? deletedItem,
                out string error))
        {
            GD.PushWarning($"Item delete rejected: {error}");
            RefreshAllSlots();
            return;
        }

        if (kind == BackpackLocationKind.BagEquipment)
            RebuildExpansionSlotViews();

        RefreshAllSlots();
        DebugLog.Print(
            $"Deleted {deletedItem!.DisplayName} x{deletedItem.Quantity} " +
            $"from {kind}[{sourceIndex}].");
    }

    private void ClearPendingItemAction()
    {
        _pendingActionSourceIndex = -1;
        _pendingActionItemId = string.Empty;
        _pendingActionStackId = null;
        _pendingActionUniqueInstanceId = null;
        _pendingSplitSourceIndex = -1;
        _pendingSplitStackId = 0;
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
        // frame. Resize the custom panel afterward so capacity changes still
        // grow and shrink the Backpack in both directions.
        Callable.From(ResetBackpackWindowSize).CallDeferred();
    }

    private void ResetBackpackWindowSize()
    {
        if (!GodotObject.IsInstanceValid(BackpackPanelRoot)
            || !GodotObject.IsInstanceValid(BackpackRoot))
            return;

        Vector2 contentMinimum = BackpackRoot.GetCombinedMinimumSize();
        Vector2 panelMinimum = new(
            contentMinimum.X,
            contentMinimum.Y + CustomTitleBarHeight);

        BackpackPanelRoot.CustomMinimumSize = panelMinimum;
        BackpackPanelRoot.Size = panelMinimum;
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
        slotView.TooltipText = string.Empty;
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

        valid &= Require(BackpackPanelRoot, nameof(BackpackPanelRoot));
        valid &= Require(BackpackRoot, nameof(BackpackRoot));
        valid &= Require(SearchEdit, nameof(SearchEdit));
        valid &= Require(ItemSlotScene, nameof(ItemSlotScene));
        valid &= Require(
            UpperExpansionColumns,
            nameof(UpperExpansionColumns));
        valid &= Require(
            LowerExpansionGrid,
            nameof(LowerExpansionGrid));
        valid &= Require(StackSplitPopup, nameof(StackSplitPopup));
        valid &= Require(ItemTooltip, nameof(ItemTooltip));
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

}
