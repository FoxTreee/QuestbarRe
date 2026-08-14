using Godot;
using System;
using System.Collections.Generic;

public partial class CharacterWindowEquipmentPanelController : Node
{
    [ExportCategory("Dependencies")]
    [Export] public CharacterWindowController CharacterWindow { get; set; } = null!;
    [Export] public Control CharacterTabRoot { get; set; } = null!;
    [Export] public BackpackWindowController Backpack { get; set; } = null!;
    [Export] public ItemTooltipController ItemTooltip { get; set; } = null!;
    [Export] public Texture2D? FallbackItemIcon { get; set; }
    [Export(PropertyHint.Range, "1,1000,1")]
    public int TemporaryHeroLevel { get; set; } = 1;

    private readonly Dictionary<EquipmentSlot, ItemSlotView> _views = new();
    private readonly Dictionary<HeroActorController,
        Dictionary<EquipmentSlot, BackpackItemState>> _instances = new();

    public override void _Ready()
    {
        if (!ValidateReferences()) return;
        DiscoverSlots();
        CharacterWindow.SelectedHeroChanged += Refresh;
        Backpack.CharacterEquipmentDropValidator = CanUnequip;
        Backpack.CharacterEquipmentDropRequested += Unequip;
        Refresh(CharacterWindow.SelectedHero);
    }

    public override void _ExitTree()
    {
        if (GodotObject.IsInstanceValid(CharacterWindow))
            CharacterWindow.SelectedHeroChanged -= Refresh;
        if (GodotObject.IsInstanceValid(Backpack))
        {
            Backpack.CharacterEquipmentDropValidator = null;
            Backpack.CharacterEquipmentDropRequested -= Unequip;
        }
    }

    public void Refresh(HeroActorController? hero)
    {
        foreach (var pair in _views)
        {
            IResolvedEquipmentProfile? profile = GodotObject.IsInstanceValid(hero)
                ? hero!.Equipment.GetItem(pair.Key) : null;
            if (profile is null)
            {
                pair.Value.ClearItemIdentity();
                pair.Value.TooltipText = string.Empty;
                continue;
            }
            BackpackItemState item = GetInstance(hero!, pair.Key, profile);
            pair.Value.SetUniqueItemIdentity(item.ItemId, item.UniqueInstanceId!.Value);
            pair.Value.SetItemTexture(item.IconTexture ?? FallbackItemIcon);
            pair.Value.TooltipText = string.Empty;
        }
    }

    private void DiscoverSlots()
    {
        List<ItemSlotView> found = new();
        CollectSlots(CharacterTabRoot, found);
        foreach (ItemSlotView view in found)
        {
            if (!view.HasCharacterEquipmentSlot) continue;
            if (!_views.TryAdd(view.CharacterEquipmentSlot, view))
            {
                GD.PushError($"Duplicate Character equipment slot '{view.CharacterEquipmentSlot}'.");
                continue;
            }
            view.DragEnabled = true;
            view.DropValidator = CanEquip;
            view.DropRequested += Equip;
            ItemTooltip.RegisterSlot(view);
        }
        if (_views.Count == 0) GD.PushError("No Character equipment slots found.");
    }

    private bool CanEquip(ItemSlotView destination,
        ItemSlotView.SlotPurpose sourcePurpose,
        ItemSlotView.SlotContent sourceContent,
        int sourceIndex, string itemId, long? stackId, long? instanceId)
    {
        HeroActorController? hero = CharacterWindow.SelectedHero;
        if (!GodotObject.IsInstanceValid(hero) ||
            sourcePurpose != ItemSlotView.SlotPurpose.Storage ||
            !destination.HasCharacterEquipmentSlot) return false;

        BackpackItemState? item = Backpack.GetStorageItem(sourceIndex);
        if (!Matches(item, itemId, instanceId) || item!.EquipmentProfile is null) return false;
        EquipmentSlot slot = destination.CharacterEquipmentSlot;
        return HeroEquipmentEligibility.CanEquip(hero!.Definition.ClassDefinition,
                TemporaryHeroLevel, item.EquipmentProfile, slot, out _) &&
            hero.Equipment.CanEquipResolved(item.EquipmentProfile, slot, out _);
    }

    private void Equip(ItemSlotView destination,
        ItemSlotView.SlotPurpose sourcePurpose,
        ItemSlotView.SlotContent sourceContent,
        int sourceIndex, string itemId, long? stackId, long? instanceId)
    {
        HeroActorController? hero = CharacterWindow.SelectedHero;
        if (!GodotObject.IsInstanceValid(hero) ||
            !CanEquip(destination, sourcePurpose, sourceContent,
                sourceIndex, itemId, stackId, instanceId)) return;

        EquipmentSlot slot = destination.CharacterEquipmentSlot;
        BackpackItemState incoming = Backpack.GetStorageItem(sourceIndex)!;
        IResolvedEquipmentProfile? oldProfile = hero!.Equipment.GetItem(slot);
        BackpackItemState? outgoing = oldProfile is null
            ? null : GetInstance(hero, slot, oldProfile);

        if (!Backpack.TryExchangeStorageItem(sourceIndex, incoming, outgoing,
            out _, out string error))
        {
            GD.PushWarning($"Equip rejected: {error}"); return;
        }
        if (!hero.Equipment.TryEquipResolved(incoming.EquipmentProfile!, slot, out error))
        {
            Backpack.TryExchangeStorageItem(sourceIndex, outgoing, incoming, out _, out _);
            GD.PushWarning($"Equip rolled back: {error}"); return;
        }
        GetInstances(hero)[slot] = incoming;
        Finish(hero, $"Equipped {incoming.ItemId} in {slot}.");
    }

    private bool CanUnequip(ItemSlotView destination,
        ItemSlotView.SlotPurpose sourcePurpose,
        ItemSlotView.SlotContent sourceContent,
        int sourceIndex, string itemId, long? stackId, long? instanceId)
    {
        HeroActorController? hero = CharacterWindow.SelectedHero;
        if (!GodotObject.IsInstanceValid(hero) ||
            destination.Purpose != ItemSlotView.SlotPurpose.Storage ||
            !Enum.IsDefined(typeof(EquipmentSlot), sourceIndex)) return false;

        EquipmentSlot slot = (EquipmentSlot)sourceIndex;
        IResolvedEquipmentProfile? current = hero!.Equipment.GetItem(slot);
        if (current is null || !Matches(GetInstance(hero, slot, current), itemId, instanceId))
            return false;
        BackpackItemState? incoming = Backpack.GetStorageItem(destination.SlotIndex);
        return incoming is null || (incoming.EquipmentProfile is not null &&
            HeroEquipmentEligibility.CanEquip(hero.Definition.ClassDefinition,
                TemporaryHeroLevel, incoming.EquipmentProfile, slot, out _) &&
            hero.Equipment.CanEquipResolved(incoming.EquipmentProfile, slot, out _));
    }

    private void Unequip(ItemSlotView destination,
        ItemSlotView.SlotPurpose sourcePurpose,
        ItemSlotView.SlotContent sourceContent,
        int sourceIndex, string itemId, long? stackId, long? instanceId)
    {
        HeroActorController? hero = CharacterWindow.SelectedHero;
        if (!GodotObject.IsInstanceValid(hero) ||
            !CanUnequip(destination, sourcePurpose, sourceContent,
                sourceIndex, itemId, stackId, instanceId)) return;

        EquipmentSlot slot = (EquipmentSlot)sourceIndex;
        IResolvedEquipmentProfile oldProfile = hero!.Equipment.GetItem(slot)!;
        BackpackItemState outgoing = GetInstance(hero, slot, oldProfile);
        BackpackItemState? incoming = Backpack.GetStorageItem(destination.SlotIndex);
        bool changed = incoming is null
            ? hero.Equipment.Unequip(slot)
            : hero.Equipment.TryEquipResolved(incoming.EquipmentProfile!, slot, out _);

        string error = string.Empty;
        if (!changed || !Backpack.TryExchangeStorageItem(destination.SlotIndex,
            incoming, outgoing, out _, out error))
        {
            hero.Equipment.TryEquipResolved(oldProfile, slot, out _);
            GD.PushWarning($"Unequip rolled back: {error}"); return;
        }
        Dictionary<EquipmentSlot, BackpackItemState> items = GetInstances(hero);
        if (incoming is null) items.Remove(slot); else items[slot] = incoming;
        Finish(hero, $"Unequipped {outgoing.ItemId} from {slot}.");
    }

    private void Finish(HeroActorController hero, string message)
    {
        hero.NotifyRuntimeEquipmentChanged();
        Refresh(hero);
        DebugLog.Print(message);
    }

    private BackpackItemState GetInstance(HeroActorController hero,
        EquipmentSlot slot, IResolvedEquipmentProfile profile)
    {
        Dictionary<EquipmentSlot, BackpackItemState> items = GetInstances(hero);
        if (!items.TryGetValue(slot, out BackpackItemState? item) ||
            !ReferenceEquals(item.EquipmentProfile, profile))
        {
            item = BackpackItemState.CreateEquipment(profile, BuildInstanceId(hero, slot, profile));
            items[slot] = item;
        }
        return item;
    }

    public BackpackItemState GetEquipmentInstance(
        HeroActorController hero,
        EquipmentSlot slot,
        IResolvedEquipmentProfile profile) =>
        GetInstance(hero, slot, profile);

    public void SetRestoredEquipmentInstance(
        HeroActorController hero,
        EquipmentSlot slot,
        BackpackItemState? item)
    {
        Dictionary<EquipmentSlot, BackpackItemState> items = GetInstances(hero);
        if (item is null) items.Remove(slot); else items[slot] = item;
    }

    public void RefreshAfterRestore()
    {
        HeroActorController? hero = CharacterWindow.SelectedHero;
        if (GodotObject.IsInstanceValid(hero)) Refresh(hero);
    }

    private Dictionary<EquipmentSlot, BackpackItemState> GetInstances(HeroActorController hero)
    {
        if (!_instances.TryGetValue(hero, out var items))
        {
            items = new(); _instances.Add(hero, items);
        }
        return items;
    }

    private static bool Matches(BackpackItemState? item, string id, long? instanceId) =>
        item is not null && item.ItemId == id && item.UniqueInstanceId == instanceId;

    private static long BuildInstanceId(HeroActorController hero,
        EquipmentSlot slot, IResolvedEquipmentProfile profile) =>
        287_000_000_000L + (uint)HashCode.Combine(
            hero.GetInstanceId(), (int)slot, profile.DefinitionContentId);

    private static void CollectSlots(Node parent, List<ItemSlotView> results)
    {
        foreach (Node child in parent.GetChildren())
        {
            if (child is ItemSlotView view) results.Add(view);
            CollectSlots(child, results);
        }
    }

    private bool ValidateReferences() =>
        Require(CharacterWindow, nameof(CharacterWindow)) &
        Require(CharacterTabRoot, nameof(CharacterTabRoot)) &
        Require(Backpack, nameof(Backpack)) &
        Require(ItemTooltip, nameof(ItemTooltip));

    private static bool Require(GodotObject value, string name)
    {
        if (GodotObject.IsInstanceValid(value)) return true;
        GD.PushError($"Character equipment controller is missing '{name}'.");
        return false;
    }
}
