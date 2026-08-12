using System;

public sealed class HeroEquipmentLoadout
{
    public ResolvedWeaponProfile? MainHand
    { get; private set; }

    public ResolvedWeaponProfile? OffHand
    { get; private set; }

    public ResolvedWeaponProfile? Ranged
    { get; private set; }


    public bool TryConfigure(
        WeaponDefinition? mainHand,
        WeaponDefinition? offHand,
        WeaponDefinition? ranged,
        out string error)
    {
        error = string.Empty;

        if (!ValidateSlot(
            mainHand,
            HeroWeaponSlot.MainHand,
            out error))
        {
            return false;
        }

        if (!ValidateSlot(
            offHand,
            HeroWeaponSlot.OffHand,
            out error))
        {
            return false;
        }

        if (!ValidateSlot(
            ranged,
            HeroWeaponSlot.Ranged,
            out error))
        {
            return false;
        }

        if (mainHand is not null
            && mainHand.Handedness
                == WeaponHandedness.TwoHanded
            && offHand is not null)
        {
            error =
                $"Two-handed weapon '{mainHand.ContentId}' " +
                "occupies the hero's hand setup and cannot be " +
                $"combined with off-hand weapon '{offHand.ContentId}'.";

            return false;
        }

        MainHand =
            mainHand is null
                ? null
                : ResolvedWeaponProfile.FromDefinition(mainHand);

        OffHand =
            offHand is null
                ? null
                : ResolvedWeaponProfile.FromDefinition(offHand);

        Ranged =
            ranged is null
                ? null
                : ResolvedWeaponProfile.FromDefinition(ranged);

        return true;
    }


    private static bool ValidateSlot(
        WeaponDefinition? weapon,
        HeroWeaponSlot slot,
        out string error)
    {
        error = string.Empty;

        if (weapon is null)
            return true;

        if (weapon.CanEquipInSlot(slot))
            return true;

        error =
            $"Weapon '{weapon.ContentId}' with equip position " +
            $"{weapon.EquipPosition} cannot be equipped in " +
            $"{slot}.";

        return false;
    }
}
