using System;
using System.Collections.Generic;

/// <summary>
/// Authoritative runtime state for the Backpack's base storage and four fixed
/// bag-equipment locations. Bag contributions create real additional runtime
/// storage locations; their visual reflow is intentionally deferred.
/// </summary>
public sealed class BackpackInventoryState
{
    public const int BaseStorageSlotCount = 16;
    public const int BagEquipmentSlotCount = 4;

    private readonly List<BackpackInventoryLocation> _storageLocations;
    private readonly List<BackpackInventoryLocation> _bagEquipmentLocations;
    private long _nextSplitStackId = 289_000_000_000L;

    public void AdvanceSplitStackIdentity(long maximumStackId)
    {
        if (maximumStackId >= _nextSplitStackId)
            _nextSplitStackId = maximumStackId + 1;
    }

    public IReadOnlyList<BackpackInventoryLocation> StorageLocations =>
        _storageLocations;

    public IReadOnlyList<BackpackInventoryLocation> BagEquipmentLocations =>
        _bagEquipmentLocations;

    public int AddedBagCapacity
    {
        get
        {
            int total = 0;

            foreach (BackpackInventoryLocation location
                in _bagEquipmentLocations)
            {
                total += location.Item?.AddedInventorySlots ?? 0;
            }

            return total;
        }
    }

    public int TotalStorageCapacity =>
        BaseStorageSlotCount + AddedBagCapacity;

    public BackpackInventoryState()
    {
        _storageLocations = CreateLocations(
            BackpackLocationKind.Storage,
            BaseStorageSlotCount);

        _bagEquipmentLocations = CreateLocations(
            BackpackLocationKind.BagEquipment,
            BagEquipmentSlotCount);
    }

    public BackpackInventoryLocation GetLocation(
        BackpackLocationKind kind,
        int index)
    {
        IReadOnlyList<BackpackInventoryLocation> locations =
            GetLocations(kind);

        if (index < 0 || index >= locations.Count)
        {
            throw new ArgumentOutOfRangeException(
                nameof(index),
                index,
                $"Backpack {kind} index must be between 0 and " +
                $"{locations.Count - 1}.");
        }

        return locations[index];
    }

    public bool TryFindItem(string itemId, out BackpackLocationKind kind, out int index)
    {
        foreach (BackpackInventoryLocation location in _storageLocations)
        {
            if (string.Equals(location.Item?.ItemId, itemId, StringComparison.OrdinalIgnoreCase))
            {
                kind = location.Kind; index = location.Index; return true;
            }
        }
        foreach (BackpackInventoryLocation location in _bagEquipmentLocations)
        {
            if (string.Equals(location.Item?.ItemId, itemId, StringComparison.OrdinalIgnoreCase))
            {
                kind = location.Kind; index = location.Index; return true;
            }
        }
        kind = default; index = -1; return false;
    }

    public bool TryRestore(
        IReadOnlyList<BackpackItemState?> bags,
        IReadOnlyList<BackpackItemState?> storage,
        out string error)
    {
        error = string.Empty;
        if (bags.Count != BagEquipmentSlotCount)
        {
            error = $"Save must contain exactly {BagEquipmentSlotCount} bag slots.";
            return false;
        }

        int capacity = BaseStorageSlotCount;
        foreach (BackpackItemState? bag in bags)
        {
            if (bag is not null && !bag.IsBag)
            {
                error = $"Saved bag slot contains non-bag '{bag.ItemId}'.";
                return false;
            }
            capacity += bag?.AddedInventorySlots ?? 0;
        }

        if (storage.Count != capacity)
        {
            error = $"Saved storage count {storage.Count} does not match capacity {capacity}.";
            return false;
        }

        for (int i = 0; i < bags.Count; i++)
            _bagEquipmentLocations[i].SetItem(bags[i]);
        while (_storageLocations.Count < capacity)
            _storageLocations.Add(new BackpackInventoryLocation(BackpackLocationKind.Storage, _storageLocations.Count));
        for (int i = 0; i < capacity; i++)
            _storageLocations[i].SetItem(storage[i]);
        if (_storageLocations.Count > capacity)
            _storageLocations.RemoveRange(capacity, _storageLocations.Count - capacity);
        return true;
    }

    public bool TryAcquire(
        ItemDefinition definition,
        int quantity,
        ref long nextInstanceId,
        ref long nextStackId,
        out string error)
    {
        error = string.Empty;
        BackpackItemState?[] proposed = SnapshotItems(_storageLocations);
        int remaining = quantity;

        if (definition.IsStackable)
        {
            for (int i = 0; i < proposed.Length && remaining > 0; i++)
            {
                BackpackItemState? stack = proposed[i];
                if (stack is null || !stack.IsStackable ||
                    !stack.ItemId.Equals(definition.ContentId, StringComparison.OrdinalIgnoreCase)) continue;
                int added = Math.Min(remaining, definition.MaximumStackSize - stack.Quantity);
                if (added <= 0) continue;
                proposed[i] = stack.WithQuantity(stack.Quantity + added);
                remaining -= added;
            }
        }

        int requiredEmptySlots = definition.IsStackable
            ? (remaining + definition.MaximumStackSize - 1) / definition.MaximumStackSize
            : remaining;
        int emptySlots = 0;
        foreach (BackpackItemState? item in proposed) if (item is null) emptySlots++;
        if (emptySlots < requiredEmptySlots)
        {
            error = $"Backpack needs {requiredEmptySlots} empty slot(s), but only {emptySlots} remain.";
            return false;
        }

        for (int i = 0; i < proposed.Length && remaining > 0; i++)
        {
            if (proposed[i] is not null) continue;
            int amount = definition.IsStackable
                ? Math.Min(remaining, definition.MaximumStackSize) : 1;
            long identity = definition.IsStackable ? nextStackId++ : nextInstanceId++;
            proposed[i] = BackpackItemState.CreateInventoryItem(definition, identity, amount);
            remaining -= amount;
        }

        for (int i = 0; i < proposed.Length; i++)
            _storageLocations[i].SetItem(proposed[i]);
        return true;
    }

    /// <summary>
    /// Places an eligible item only into an empty location. Movement, swapping,
    /// and stacking will be added by their dedicated checkpoints.
    /// </summary>
    public bool TryPlaceInEmptyLocation(
        BackpackLocationKind kind,
        int index,
        BackpackItemState item,
        out string error)
    {
        error = string.Empty;

        if (item is null)
        {
            error = "Cannot place a null item in the Backpack.";
            return false;
        }

        if (kind == BackpackLocationKind.BagEquipment && !item.IsBag)
        {
            error =
                $"Item '{item.ItemId}' is not a bag and cannot occupy " +
                "a BagEquipment location.";
            return false;
        }

        BackpackInventoryLocation location;

        try
        {
            location = GetLocation(kind, index);
        }
        catch (ArgumentOutOfRangeException exception)
        {
            error = exception.Message;
            return false;
        }

        if (!location.IsEmpty)
        {
            error = $"Backpack {kind} slot {index} is already occupied.";
            return false;
        }

        location.SetItem(item);

        if (kind == BackpackLocationKind.BagEquipment)
            RebuildStorageCapacity();

        return true;
    }

    /// <summary>
    /// Atomically moves or swaps one storage item after verifying that the
    /// drag payload still identifies the item currently in the source slot.
    /// </summary>
    public bool TryMoveOrSwapStorageItem(
        int sourceIndex,
        int destinationIndex,
        string expectedItemId,
        long? expectedStackId,
        long? expectedUniqueInstanceId,
        out string error)
    {
        error = string.Empty;

        if (!TryGetStorageLocation(sourceIndex, out BackpackInventoryLocation? source) ||
            !TryGetStorageLocation(destinationIndex, out BackpackInventoryLocation? destination))
        {
            error =
                $"Storage movement requires valid runtime indices. " +
                $"Source={sourceIndex}, Destination={destinationIndex}, " +
                $"Capacity={_storageLocations.Count}.";
            return false;
        }

        if (sourceIndex == destinationIndex)
        {
            error = "Source and destination storage slots are the same.";
            return false;
        }

        BackpackItemState? sourceItem = source!.Item;

        if (sourceItem is null)
        {
            error = $"Storage source slot {sourceIndex} is empty.";
            return false;
        }

        if (!SourceIdentityMatches(
            sourceItem,
            expectedItemId,
            expectedStackId,
            expectedUniqueInstanceId))
        {
            error =
                $"Rejected stale storage drag from slot {sourceIndex}; " +
                "the authoritative item identity no longer matches the payload.";
            return false;
        }

        BackpackItemState? destinationItem = destination!.Item;

        if (sourceItem.IsStackable &&
            destinationItem?.IsStackable == true &&
            sourceItem.ItemId.Equals(destinationItem.ItemId,
                StringComparison.OrdinalIgnoreCase))
        {
            int available = destinationItem.MaximumStackSize - destinationItem.Quantity;
            if (available > 0)
            {
                int transferred = Math.Min(sourceItem.Quantity, available);
                destination.SetItem(destinationItem.WithQuantity(
                    destinationItem.Quantity + transferred));
                int remaining = sourceItem.Quantity - transferred;
                source.SetItem(remaining == 0
                    ? null
                    : sourceItem.WithQuantity(remaining));
                return true;
            }
        }

        source.SetItem(destinationItem);
        destination.SetItem(sourceItem);
        return true;
    }

    public bool TrySplitStack(
        int sourceIndex,
        long expectedStackId,
        int splitQuantity,
        out int destinationIndex,
        out string error)
    {
        destinationIndex = -1;
        error = string.Empty;

        if (!TryGetStorageLocation(sourceIndex, out BackpackInventoryLocation? source) ||
            source!.Item is not BackpackItemState item ||
            !item.IsStackable || item.StackId != expectedStackId)
        {
            error = "The source stack no longer matches the split request.";
            return false;
        }

        if (splitQuantity < 1 || splitQuantity >= item.Quantity)
        {
            error = $"Split quantity must be between 1 and {item.Quantity - 1}.";
            return false;
        }

        for (int index = 0; index < _storageLocations.Count; index++)
        {
            if (_storageLocations[index].IsEmpty)
            {
                destinationIndex = index;
                break;
            }
        }

        if (destinationIndex < 0)
        {
            error = "No empty Backpack storage slot is available for the new stack.";
            return false;
        }

        source.SetItem(item.WithQuantity(item.Quantity - splitQuantity));
        _storageLocations[destinationIndex].SetItem(
            item.CreateSplitStack(_nextSplitStackId++, splitQuantity));
        return true;
    }

    /// <summary>
    /// Exchanges one validated storage record with a Character equipment
    /// record. Null incomingEquipment represents unequipping into empty space.
    /// </summary>
    public bool TryExchangeStorageItem(
        int storageIndex,
        BackpackItemState? expectedStorageItem,
        BackpackItemState? incomingEquipment,
        out BackpackItemState? outgoingStorageItem,
        out string error)
    {
        outgoingStorageItem = null;
        error = string.Empty;

        if (!TryGetStorageLocation(storageIndex, out BackpackInventoryLocation? location))
        {
            error = $"Storage index {storageIndex} is outside current capacity.";
            return false;
        }

        if (!ReferenceEquals(location!.Item, expectedStorageItem))
        {
            error = "Storage contents changed before the equipment transaction committed.";
            return false;
        }

        outgoingStorageItem = location.Item;
        location.SetItem(incomingEquipment);
        return true;
    }

    /// <summary>
    /// Atomically moves or swaps an item when at least one endpoint is a bag
    /// slot. The complete post-move capacity and every required relocation are
    /// validated before authoritative state changes.
    /// </summary>
    public bool TryMoveOrSwapBagItem(
        BackpackLocationKind sourceKind,
        int sourceIndex,
        BackpackLocationKind destinationKind,
        int destinationIndex,
        string expectedItemId,
        long? expectedStackId,
        long? expectedUniqueInstanceId,
        out string error)
    {
        error = string.Empty;

        if (sourceKind == BackpackLocationKind.Storage &&
            destinationKind == BackpackLocationKind.Storage)
        {
            error = "Bag transaction requires at least one bag-equipment endpoint.";
            return false;
        }

        if (!TryGetLocation(sourceKind, sourceIndex, out BackpackInventoryLocation? source) ||
            !TryGetLocation(destinationKind, destinationIndex, out BackpackInventoryLocation? destination))
        {
            error = "Bag movement referenced a location outside the current Backpack.";
            return false;
        }

        if (sourceKind == destinationKind && sourceIndex == destinationIndex)
        {
            error = "Source and destination locations are the same.";
            return false;
        }

        BackpackItemState? sourceItem = source!.Item;
        BackpackItemState? destinationItem = destination!.Item;

        if (sourceItem is null)
        {
            error = $"Backpack {sourceKind} slot {sourceIndex} is empty.";
            return false;
        }

        if (!SourceIdentityMatches(
            sourceItem,
            expectedItemId,
            expectedStackId,
            expectedUniqueInstanceId))
        {
            error = "Rejected stale bag drag; authoritative identity no longer matches.";
            return false;
        }

        if (destinationKind == BackpackLocationKind.BagEquipment && !sourceItem.IsBag)
        {
            error = $"Item '{sourceItem.ItemId}' is not a bag.";
            return false;
        }

        if (sourceKind == BackpackLocationKind.BagEquipment &&
            destinationItem is not null &&
            !destinationItem.IsBag)
        {
            error =
                $"Item '{destinationItem.ItemId}' cannot be swapped into a bag slot.";
            return false;
        }

        BackpackItemState?[] proposedBags = SnapshotItems(_bagEquipmentLocations);
        BackpackItemState?[] proposedStorage = SnapshotItems(_storageLocations);

        SetProposedItem(proposedStorage, proposedBags, sourceKind, sourceIndex, destinationItem);
        SetProposedItem(proposedStorage, proposedBags, destinationKind, destinationIndex, sourceItem);

        int proposedCapacity = BaseStorageSlotCount;
        foreach (BackpackItemState? bag in proposedBags)
            proposedCapacity += bag?.AddedInventorySlots ?? 0;

        if (destinationKind == BackpackLocationKind.Storage &&
            destinationIndex >= proposedCapacity)
        {
            error =
                $"The destination storage slot would disappear when this bag is removed. " +
                $"Choose a slot below {proposedCapacity}.";
            return false;
        }

        List<BackpackItemState> displacedItems = new();
        for (int index = proposedCapacity; index < proposedStorage.Length; index++)
        {
            if (proposedStorage[index] is BackpackItemState item)
                displacedItems.Add(item);
        }

        for (int index = 0; index < displacedItems.Count; index++)
        {
            int emptyIndex = FindEmptyIndex(proposedStorage, proposedCapacity);
            if (emptyIndex < 0)
            {
                error =
                    $"Cannot reduce Backpack capacity to {proposedCapacity}; " +
                    $"{displacedItems.Count - index} item(s) would have no surviving slot.";
                return false;
            }

            proposedStorage[emptyIndex] = displacedItems[index];
        }

        CommitBagItems(proposedBags);
        CommitStorageItems(proposedStorage, proposedCapacity);
        return true;
    }

    private bool TryGetLocation(
        BackpackLocationKind kind,
        int index,
        out BackpackInventoryLocation? location)
    {
        IReadOnlyList<BackpackInventoryLocation> locations;

        try
        {
            locations = GetLocations(kind);
        }
        catch (ArgumentOutOfRangeException)
        {
            location = null;
            return false;
        }

        if (index < 0 || index >= locations.Count)
        {
            location = null;
            return false;
        }

        location = locations[index];
        return true;
    }

    private static BackpackItemState?[] SnapshotItems(
        IReadOnlyList<BackpackInventoryLocation> locations)
    {
        BackpackItemState?[] items = new BackpackItemState?[locations.Count];
        for (int index = 0; index < locations.Count; index++)
            items[index] = locations[index].Item;
        return items;
    }

    private static void SetProposedItem(
        BackpackItemState?[] storage,
        BackpackItemState?[] bags,
        BackpackLocationKind kind,
        int index,
        BackpackItemState? item)
    {
        if (kind == BackpackLocationKind.Storage)
            storage[index] = item;
        else
            bags[index] = item;
    }

    private static int FindEmptyIndex(
        BackpackItemState?[] storage,
        int survivingCapacity)
    {
        int limit = Math.Min(survivingCapacity, storage.Length);
        for (int index = 0; index < limit; index++)
        {
            if (storage[index] is null)
                return index;
        }
        return -1;
    }

    private void CommitBagItems(BackpackItemState?[] proposedBags)
    {
        for (int index = 0; index < proposedBags.Length; index++)
            _bagEquipmentLocations[index].SetItem(proposedBags[index]);
    }

    private void CommitStorageItems(
        BackpackItemState?[] proposedStorage,
        int proposedCapacity)
    {
        while (_storageLocations.Count < proposedCapacity)
        {
            _storageLocations.Add(new BackpackInventoryLocation(
                BackpackLocationKind.Storage,
                _storageLocations.Count));
        }

        for (int index = 0; index < proposedCapacity; index++)
        {
            BackpackItemState? item =
                index < proposedStorage.Length ? proposedStorage[index] : null;
            _storageLocations[index].SetItem(item);
        }

        if (_storageLocations.Count > proposedCapacity)
        {
            _storageLocations.RemoveRange(
                proposedCapacity,
                _storageLocations.Count - proposedCapacity);
        }
    }

    private bool TryGetStorageLocation(
        int index,
        out BackpackInventoryLocation? location)
    {
        if (index < 0 || index >= _storageLocations.Count)
        {
            location = null;
            return false;
        }

        location = _storageLocations[index];
        return true;
    }

    private static bool SourceIdentityMatches(
        BackpackItemState item,
        string expectedItemId,
        long? expectedStackId,
        long? expectedUniqueInstanceId)
    {
        return
            item.ItemId == expectedItemId &&
            item.StackId == expectedStackId &&
            item.UniqueInstanceId == expectedUniqueInstanceId;
    }

    private IReadOnlyList<BackpackInventoryLocation> GetLocations(
        BackpackLocationKind kind)
    {
        return kind switch
        {
            BackpackLocationKind.Storage => _storageLocations,
            BackpackLocationKind.BagEquipment => _bagEquipmentLocations,
            _ => throw new ArgumentOutOfRangeException(
                nameof(kind),
                kind,
                "Unsupported Backpack location kind.")
        };
    }

    private void RebuildStorageCapacity()
    {
        while (_storageLocations.Count < TotalStorageCapacity)
        {
            _storageLocations.Add(
                new BackpackInventoryLocation(
                    BackpackLocationKind.Storage,
                    _storageLocations.Count));
        }
    }

    private static List<BackpackInventoryLocation> CreateLocations(
        BackpackLocationKind kind,
        int count)
    {
        List<BackpackInventoryLocation> locations = new(count);

        for (int index = 0; index < count; index++)
        {
            locations.Add(
                new BackpackInventoryLocation(kind, index));
        }

        return locations;
    }
}
