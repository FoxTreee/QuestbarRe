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
        source.SetItem(destinationItem);
        destination.SetItem(sourceItem);
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
