using System.Collections.Generic;
using System.Linq;

public sealed class HeroEquipmentLoadout
{
    private readonly Dictionary
        <EquipmentSlot, IResolvedEquipmentProfile> _equippedItems =
            new();

    public EquipmentStatTotals TotalStats { get; } = new();

    /// <summary>
    /// Raw Armor supplied by all currently equipped armor-bearing items.
    /// Damage mitigation is intentionally not implemented yet.
    /// </summary>
    public int TotalArmor { get; private set; }


    public IResolvedEquipmentProfile? GetItem(
        EquipmentSlot slot)
    {
        return _equippedItems.TryGetValue(
            slot,
            out IResolvedEquipmentProfile? item)
            ? item
            : null;
    }


    public ResolvedWeaponProfile? MainHandWeapon =>
        GetItem(EquipmentSlot.MainHand)
            as ResolvedWeaponProfile;

    public ResolvedWeaponProfile? OffHandWeapon =>
        GetItem(EquipmentSlot.OffHand)
            as ResolvedWeaponProfile;

    public ResolvedWeaponProfile? RangedWeapon =>
        GetItem(EquipmentSlot.Ranged)
            as ResolvedWeaponProfile;

    public ResolvedShieldProfile? OffHandShield =>
        GetItem(EquipmentSlot.OffHand)
            as ResolvedShieldProfile;


    /// <summary>
    /// Configures the current starter weapon loadout through the same generic
    /// equipment system used by armor and shields.
    /// </summary>
    public bool TryConfigureWeapons(
        WeaponDefinition? mainHand,
        WeaponDefinition? offHand,
        WeaponDefinition? ranged,
        out string error)
    {
        error = string.Empty;

        _equippedItems.Remove(
            EquipmentSlot.MainHand);

        _equippedItems.Remove(
            EquipmentSlot.OffHand);

        _equippedItems.Remove(
            EquipmentSlot.Ranged);

        if (mainHand is not null
            && !TryEquipResolved(
                ResolvedWeaponProfile.FromDefinition(mainHand),
                EquipmentSlot.MainHand,
                out error))
        {
            return false;
        }

        if (offHand is not null
            && !TryEquipResolved(
                ResolvedWeaponProfile.FromDefinition(offHand),
                EquipmentSlot.OffHand,
                out error))
        {
            return false;
        }

        if (ranged is not null
            && !TryEquipResolved(
                ResolvedWeaponProfile.FromDefinition(ranged),
                EquipmentSlot.Ranged,
                out error))
        {
            return false;
        }

        RebuildEquipmentTotals();
        return true;
    }


    /// <summary>
    /// Resolves a local authored equipment definition into the same runtime
    /// profile shapes that a future authoritative item source can populate.
    /// </summary>
    public bool TryEquipDefinition(
        EquipmentDefinition definition,
        EquipmentSlot slot,
        out string error)
    {
        error = string.Empty;

        if (definition is null)
        {
            error =
                $"Cannot equip a null definition into {slot}.";

            return false;
        }

        IResolvedEquipmentProfile? resolved =
            definition switch
            {
                WeaponDefinition weapon =>
                    ResolvedWeaponProfile.FromDefinition(
                        weapon),

                ArmorDefinition armor =>
                    ResolvedArmorProfile.FromDefinition(
                        armor),

                ShieldDefinition shield =>
                    ResolvedShieldProfile.FromDefinition(
                        shield),

                _ => null
            };

        if (resolved is null)
        {
            error =
                $"Equipment definition '{definition.ContentId}' " +
                $"has unsupported runtime type " +
                $"'{definition.GetType().Name}'.";

            return false;
        }

        return TryEquipResolved(
            resolved,
            slot,
            out error);
    }


    /// <summary>
    /// Equips any resolved equipment profile into a compatible slot.
    /// Item placement eligibility is checked here; future hero/class/level/
    /// talent permission belongs to a separate eligibility layer.
    /// </summary>
    public bool TryEquipResolved(
        IResolvedEquipmentProfile item,
        EquipmentSlot slot,
        out string error)
    {
        if (!CanEquipResolved(item, slot, out error))
            return false;

        _equippedItems[slot] = item;
        RebuildEquipmentTotals();
        return true;
    }

    public bool CanEquipResolved(
        IResolvedEquipmentProfile item,
        EquipmentSlot slot,
        out string error)
    {
        error = string.Empty;

        if (item is null)
        {
            error =
                $"Cannot equip a null item into {slot}.";

            return false;
        }

        if (!item.CanEquipInSlot(slot))
        {
            error =
                $"Item '{item.DefinitionContentId}' cannot be equipped " +
                $"in {slot}.";

            return false;
        }

        if (slot == EquipmentSlot.OffHand
            && MainHandWeapon is ResolvedWeaponProfile mainHand
            && mainHand.Handedness
                == WeaponHandedness.TwoHanded)
        {
            error =
                $"Two-handed weapon '{mainHand.DefinitionContentId}' " +
                "prevents an Off Hand item from being equipped.";

            return false;
        }

        if (slot == EquipmentSlot.MainHand
            && item is ResolvedWeaponProfile newMainHand
            && newMainHand.Handedness
                == WeaponHandedness.TwoHanded
            && GetItem(EquipmentSlot.OffHand) is not null)
        {
            error =
                $"Two-handed weapon '{newMainHand.DefinitionContentId}' " +
                "cannot be equipped while Off Hand is occupied.";

            return false;
        }

        return true;
    }

    /// <summary>
    /// Applies hero level and authored class permissions before using the
    /// existing authoritative slot and two-handed/off-hand loadout rules.
    /// </summary>
    public bool TryEquipResolved(
        IResolvedEquipmentProfile item,
        EquipmentSlot slot,
        HeroClassDefinition heroClass,
        int heroLevel,
        out string error)
    {
        if (!HeroEquipmentEligibility.CanEquip(
            heroClass,
            heroLevel,
            item,
            slot,
            out error))
        {
            return false;
        }

        return TryEquipResolved(item, slot, out error);
    }


    public bool Unequip(
        EquipmentSlot slot)
    {
        bool removed =
            _equippedItems.Remove(slot);

        if (removed)
            RebuildEquipmentTotals();

        return removed;
    }

    public void ClearAll()
    {
        _equippedItems.Clear();
        RebuildEquipmentTotals();
    }


    /// <summary>
    /// Rebuilds all equipment-derived totals from every occupied slot.
    /// Weapon preference does not affect stat or Armor contribution.
    /// </summary>
    public void RebuildEquipmentTotals()
    {
        TotalStats.Rebuild(
            _equippedItems.Values
                .Cast<IEquipmentStatSource?>()
                .ToArray());

        TotalArmor =
            _equippedItems.Values
                .OfType<IArmorProvidingEquipment>()
                .Sum(item => item.ArmorValue);
    }


    /// <summary>
    /// Backward-compatible name retained for code that only needs to request
    /// a stat rebuild. Armor is rebuilt at the same time.
    /// </summary>
    public void RebuildTotalStats()
    {
        RebuildEquipmentTotals();
    }


    public IReadOnlyDictionary
        <EquipmentSlot, IResolvedEquipmentProfile> GetEquippedItems()
    {
        return _equippedItems;
    }
}
