using Godot;
using System;

public partial class ItemAcquisitionService : Node
{
    [ExportCategory("Dependencies")]
    [Export] public ItemContentRegistry Registry { get; set; } = null!;
    [Export] public BackpackWindowController Backpack { get; set; } = null!;
    [Export] public PartyController Party { get; set; } = null!;

    private long _nextInstanceId = 288_000_000_000L;
    private long _nextStackId = 288_500_000_000L;

    public void AdvanceIdentityCounters(long maximumInstanceId, long maximumStackId)
    {
        if (maximumInstanceId >= _nextInstanceId)
            _nextInstanceId = maximumInstanceId + 1;
        if (maximumStackId >= _nextStackId)
            _nextStackId = maximumStackId + 1;
    }

    public bool TryAcquire(string contentId, int quantity, out string result)
    {
        result = string.Empty;
        if (quantity < 1)
        {
            result = "Item quantity must be at least 1.";
            return false;
        }
        if (!Registry.TryGet(contentId, out ItemDefinition definition))
        {
            result = $"Unknown item Content ID '{contentId}'.";
            return false;
        }
        if (definition.IsUnique && IsOwned(definition.ContentId, out string location))
        {
            result = $"Unique item '{definition.ContentId}' is already owned at {location}.";
            return false;
        }
        if (definition.IsUnique && quantity != 1)
        {
            result = $"Unique item '{definition.ContentId}' can only be acquired one at a time.";
            return false;
        }

        if (!Backpack.TryAcquireItem(
            definition, quantity, ref _nextInstanceId, ref _nextStackId, out result))
            return false;

        result = $"Added {quantity} x {definition.ContentId} to Backpack storage.";
        return true;
    }

    /// <summary>
    /// Adds monster currency loot to the persistent Backpack wallet. The wallet
    /// stores copper and derives the displayed gold and silver values from it.
    /// </summary>
    public bool TryAcquireCopper(long copperAmount, out string result)
    {
        if (!Backpack.Currency.TryAdd(copperAmount, out result))
            return false;

        result =
            $"Added {copperAmount} copper. Wallet balance: " +
            $"{Backpack.Currency}.";
        return true;
    }

    private bool IsOwned(string itemId, out string location)
    {
        if (Backpack.TryFindOwnedItem(itemId, out location)) return true;

        foreach (HeroActorController hero in Party.SpawnedHeroes)
        {
            if (!GodotObject.IsInstanceValid(hero)) continue;
            foreach (var pair in hero.Equipment.GetEquippedItems())
            {
                if (pair.Value.DefinitionContentId.Equals(itemId, StringComparison.OrdinalIgnoreCase))
                {
                    location = $"{hero.Name}/{pair.Key}";
                    return true;
                }
            }
        }
        location = string.Empty;
        return false;
    }
}
