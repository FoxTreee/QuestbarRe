/// <summary>
/// One stable, indexed Backpack location. The location persists while its item
/// may change, allowing authored ItemSlot views to map to real runtime state.
/// </summary>
public sealed class BackpackInventoryLocation
{
    public BackpackLocationKind Kind { get; }
    public int Index { get; }
    public BackpackItemState? Item { get; private set; }
    public bool IsEmpty => Item is null;

    public BackpackInventoryLocation(
        BackpackLocationKind kind,
        int index)
    {
        Kind = kind;
        Index = index;
    }

    internal void SetItem(BackpackItemState? item)
    {
        Item = item;
    }
}
