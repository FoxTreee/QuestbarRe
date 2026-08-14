using Godot;
using System;
using System.Collections.Generic;
using System.Text;

public partial class ItemTooltipController : Node
{
    [ExportCategory("Dependencies")]
    [Export] public BackpackWindowController Backpack { get; set; } = null!;
    [Export] public CharacterWindowController CharacterWindow { get; set; } = null!;

    /// <summary>
    /// Native formation host whose local coordinate space contains both item
    /// panels and the custom tooltip panel.
    /// </summary>
    [Export] public Window FormationHost { get; set; } = null!;

    [ExportCategory("Tooltip UI")]
    [Export] public Control TooltipWindow { get; set; } = null!;
    [Export] public Control ComparisonPanel { get; set; } = null!;
    [Export] public RichTextLabel ComparisonText { get; set; } = null!;
    [Export] public RichTextLabel HoveredText { get; set; } = null!;
    [Export] public Vector2I MouseOffset { get; set; } = new(18, 18);

    private readonly HashSet<ItemSlotView> _registered = new();
    private ItemSlotView? _hoveredSlot;
    private bool _dragWasActive;

    public override void _Ready()
    {
        SetMouseIgnoreRecursively(TooltipWindow);
        TooltipWindow.Hide();
        CharacterWindow.SelectedHeroChanged += OnSelectedHeroChanged;

        // Authored slots enter the tree before the management panels are
        // reparented into FormationHost. Discover them after every _Ready()
        // has completed so their hover subscriptions cannot depend on scene
        // or controller startup order.
        Callable.From(RegisterExistingSlots).CallDeferred();
        SetProcess(true);
    }

    public override void _Process(double delta)
    {
        bool dragging = FormationHost.GuiIsDragging();
        if (dragging)
        {
            _dragWasActive = true;
            TooltipWindow.Hide();
            return;
        }

        if (!_dragWasActive)
            return;

        _dragWasActive = false;
        ItemSlotView? slot = FindItemSlot(
            FormationHost.GuiGetHoveredControl());

        if (slot is not null && _registered.Contains(slot))
            ShowForSlot(slot);
    }

    public override void _ExitTree()
    {
        if (GodotObject.IsInstanceValid(CharacterWindow))
            CharacterWindow.SelectedHeroChanged -= OnSelectedHeroChanged;
        foreach (ItemSlotView slot in _registered)
        {
            if (!GodotObject.IsInstanceValid(slot)) continue;
            slot.HoverStarted -= ShowForSlot;
            slot.HoverEnded -= HideForSlot;
        }
    }

    public void RegisterSlot(ItemSlotView slot)
    {
        if (!_registered.Add(slot)) return;
        slot.HoverStarted += ShowForSlot;
        slot.HoverEnded += HideForSlot;
    }

    /// <summary>
    /// Registers every authored slot after the panel formation is complete.
    /// Runtime-generated expansion slots may also be found; RegisterSlot's
    /// identity set makes repeated registration harmless.
    /// </summary>
    private void RegisterExistingSlots()
    {
        RegisterSlotsRecursively(FormationHost);
    }

    private void RegisterSlotsRecursively(Node parent)
    {
        foreach (Node child in parent.GetChildren())
        {
            if (child is ItemSlotView slot)
                RegisterSlot(slot);

            RegisterSlotsRecursively(child);
        }
    }

    private void ShowForSlot(ItemSlotView slot)
    {
        _hoveredSlot = slot;
        if (FormationHost.GuiIsDragging())
        {
            TooltipWindow.Hide();
            return;
        }

        BackpackItemState? hovered = ResolveItem(slot);
        if (hovered is null) { TooltipWindow.Hide(); return; }

        HoveredText.Text = BuildTooltip(hovered, false);

        BackpackItemState? equipped = ResolveComparison(hovered);
        ComparisonPanel.Visible = equipped is not null;
        if (equipped is not null)
            ComparisonText.Text = BuildTooltip(equipped, true);

        TooltipWindow.Show();
        TooltipWindow.MoveToFront();
        Callable.From(PositionTooltip).CallDeferred();
    }

    private void HideForSlot(ItemSlotView slot)
    {
        if (!ReferenceEquals(slot, _hoveredSlot)) return;
        _hoveredSlot = null;
        TooltipWindow.Hide();
    }

    private void OnSelectedHeroChanged(HeroActorController hero)
    {
        if (GodotObject.IsInstanceValid(_hoveredSlot)) ShowForSlot(_hoveredSlot!);
    }

    private BackpackItemState? ResolveItem(ItemSlotView slot)
    {
        if (slot.Purpose is ItemSlotView.SlotPurpose.Storage or ItemSlotView.SlotPurpose.BagEquipment)
            return Backpack.GetBackpackItem(slot.Purpose, slot.SlotIndex);

        HeroActorController? hero = CharacterWindow.SelectedHero;
        if (!GodotObject.IsInstanceValid(hero) || !slot.HasCharacterEquipmentSlot) return null;
        IResolvedEquipmentProfile? profile = hero!.Equipment.GetItem(slot.CharacterEquipmentSlot);
        return profile is null ? null : BackpackItemState.CreateEquipment(profile, slot.UniqueInstanceId ?? 1);
    }

    private BackpackItemState? ResolveComparison(BackpackItemState hovered)
    {
        HeroActorController? hero = CharacterWindow.SelectedHero;
        if (!GodotObject.IsInstanceValid(hero) || hovered.EquipmentProfile is null) return null;
        EquipmentSlot? slot = GetComparisonSlot(hovered.EquipmentProfile);
        if (!slot.HasValue) return null;
        IResolvedEquipmentProfile? equipped = hero!.Equipment.GetItem(slot.Value);
        if (equipped is null || ReferenceEquals(equipped, hovered.EquipmentProfile)) return null;
        return BackpackItemState.CreateEquipment(equipped, 1);
    }

    private string BuildTooltip(BackpackItemState item, bool currentlyEquipped)
    {
        StringBuilder text = new();
        if (currentlyEquipped) text.AppendLine("[color=#b8b8b8]Currently Equipped[/color]");
        text.AppendLine($"[color=#ffffff][b]{Escape(item.DisplayName)}[/b][/color]");

        if (item.EquipmentProfile is IResolvedEquipmentProfile profile)
        {
            AppendEquipment(text, profile);
            HeroActorController? hero = CharacterWindow.SelectedHero;
            EquipmentSlot? slot = GetComparisonSlot(profile);
            if (GodotObject.IsInstanceValid(hero) && slot.HasValue &&
                !HeroEquipmentEligibility.CanEquip(hero!.Definition.ClassDefinition, 1,
                    profile, slot.Value, out string error))
                text.AppendLine($"[color=#ff5555]{Escape(error)}[/color]");
            else if (profile.RequiredLevel > 1)
                text.AppendLine($"Requires Level {profile.RequiredLevel}");
        }
        else if (item.IsBag)
        {
            text.AppendLine("Bag");
            text.AppendLine($"[color=#55ff55]+{item.AddedInventorySlots} Backpack Slots[/color]");
        }
        else if (item.IsStackable)
        {
            text.AppendLine($"Quantity {item.Quantity} / {item.MaximumStackSize}");
        }

        return text.ToString().TrimEnd();
    }

    private static void AppendEquipment(StringBuilder text, IResolvedEquipmentProfile profile)
    {
        switch (profile)
        {
            case ResolvedWeaponProfile weapon:
                text.AppendLine($"{FormatHandedness(weapon)}    {weapon.WeaponType}");
                text.AppendLine($"{weapon.MinimumDamage:0.##} - {weapon.MaximumDamage:0.##} Damage    Speed {weapon.AttackSpeedSeconds:0.00}");
                float dps = ((weapon.MinimumDamage + weapon.MaximumDamage) / 2f) /
                    Math.Max(0.01f, weapon.AttackSpeedSeconds);
                text.AppendLine($"({dps:0.0} damage per second)");
                break;
            case ResolvedArmorProfile armor:
                text.AppendLine($"{FormatArmorPosition(armor.EquipPosition)}    {FormatArmorCategory(armor.ArmorCategory)}");
                if (armor.ArmorValue > 0) text.AppendLine($"{armor.ArmorValue} Armor");
                break;
            case ResolvedShieldProfile shield:
                text.AppendLine("Off Hand    Shield");
                if (shield.ArmorValue > 0) text.AppendLine($"{shield.ArmorValue} Armor");
                break;
        }

        AppendStat(text, "Strength", profile.Strength);
        AppendStat(text, "Agility", profile.Agility);
        AppendStat(text, "Stamina", profile.Stamina);
        AppendStat(text, "Intellect", profile.Intellect);
        AppendStat(text, "Spirit", profile.Spirit);
        foreach (ResolvedEquipmentPercentageModifier modifier in profile.PercentageModifiers)
            text.AppendLine($"[color=#55ff55]+{modifier.PercentValue:0.##}% {Escape(HumanizeId(modifier.ModifierContentId))}[/color]");
    }

    private static void AppendStat(StringBuilder text, string name, int value)
    {
        if (value != 0) text.AppendLine($"[color=#55ff55]{(value > 0 ? "+" : "")}{value} {name}[/color]");
    }

    private static EquipmentSlot? GetComparisonSlot(IResolvedEquipmentProfile profile) => profile switch
    {
        ResolvedWeaponProfile weapon => weapon.EquipPosition == WeaponEquipPosition.Ranged
            ? EquipmentSlot.Ranged : weapon.EquipPosition == WeaponEquipPosition.OffHandOnly
                ? EquipmentSlot.OffHand : EquipmentSlot.MainHand,
        ResolvedShieldProfile => EquipmentSlot.OffHand,
        ResolvedArmorProfile armor => armor.EquipPosition switch
        {
            ArmorEquipPosition.Head => EquipmentSlot.Head,
            ArmorEquipPosition.Necklace => EquipmentSlot.Necklace,
            ArmorEquipPosition.Shoulders => EquipmentSlot.Shoulders,
            ArmorEquipPosition.Chest => EquipmentSlot.Chest,
            ArmorEquipPosition.Back => EquipmentSlot.Back,
            ArmorEquipPosition.GuildTabard => EquipmentSlot.GuildTabard,
            ArmorEquipPosition.Wrists => EquipmentSlot.Wrists,
            ArmorEquipPosition.Hands => EquipmentSlot.Hands,
            ArmorEquipPosition.Belt => EquipmentSlot.Belt,
            ArmorEquipPosition.Legs => EquipmentSlot.Legs,
            ArmorEquipPosition.Boots => EquipmentSlot.Boots,
            ArmorEquipPosition.Ring => EquipmentSlot.Ring1,
            ArmorEquipPosition.Trinket => EquipmentSlot.Trinket1,
            _ => null
        },
        _ => null
    };

    private void PositionTooltip()
    {
        TooltipWindow.ResetSize();
        TooltipWindow.Size = TooltipWindow.GetCombinedMinimumSize();

        Vector2I desktopMouse = DisplayServer.MouseGetPosition();
        Vector2I localMouse = desktopMouse - FormationHost.Position;
        Vector2I tooltipSize = new(
            Mathf.RoundToInt(TooltipWindow.Size.X),
            Mathf.RoundToInt(TooltipWindow.Size.Y));

        Vector2I position = localMouse + MouseOffset;
        position.X = Math.Clamp(
            position.X,
            0,
            Math.Max(0, FormationHost.Size.X - tooltipSize.X));
        position.Y = Math.Clamp(
            position.Y,
            0,
            Math.Max(0, FormationHost.Size.Y - tooltipSize.Y));

        TooltipWindow.Position = position;
    }

    private static string FormatHandedness(ResolvedWeaponProfile weapon) =>
        weapon.Handedness == WeaponHandedness.TwoHanded ? "Two-Hand" :
        weapon.EquipPosition == WeaponEquipPosition.OffHandOnly ? "Off Hand" : "One-Hand";
    private static string FormatArmorPosition(ArmorEquipPosition value) => value.ToString();
    private static string FormatArmorCategory(ArmorCategory value) => value == ArmorCategory.None ? string.Empty : value.ToString();
    private static string HumanizeId(string id)
    {
        int index = id.LastIndexOf('.');
        string value = index >= 0 ? id[(index + 1)..] : id;
        return value.Replace('_', ' ');
    }
    private static string Escape(string value) => value.Replace("[", "\\[");

    /// <summary>
    /// Prevents the floating tooltip and all of its presentation children from
    /// stealing hover away from the item slot that caused it to appear.
    /// </summary>
    private static void SetMouseIgnoreRecursively(Node parent)
    {
        if (parent is Control control)
            control.MouseFilter = Control.MouseFilterEnum.Ignore;

        foreach (Node child in parent.GetChildren())
            SetMouseIgnoreRecursively(child);
    }

    private static ItemSlotView? FindItemSlot(Control? control)
    {
        Node? current = control;
        while (current is not null)
        {
            if (current is ItemSlotView slot)
                return slot;

            current = current.GetParent();
        }

        return null;
    }
}
