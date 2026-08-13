/// <summary>
/// Central hero/class/level permission evaluator. Slot placement and loadout
/// conflicts remain owned by the equipment profile and loadout respectively.
/// </summary>
public static class HeroEquipmentEligibility
{
    public static bool CanEquip(
        HeroClassDefinition heroClass,
        int heroLevel,
        IResolvedEquipmentProfile item,
        EquipmentSlot slot,
        out string error)
    {
        error = string.Empty;

        if (heroClass is null)
        {
            error = "A hero class is required to evaluate equipment eligibility.";
            return false;
        }

        if (item is null)
        {
            error = "An equipment item is required to evaluate eligibility.";
            return false;
        }

        if (heroLevel < item.RequiredLevel)
        {
            error =
                $"'{item.DisplayName}' requires level {item.RequiredLevel}; " +
                $"this hero is level {heroLevel}.";
            return false;
        }

        if (!item.CanEquipInSlot(slot))
        {
            error = $"'{item.DisplayName}' cannot be equipped in {slot}.";
            return false;
        }

        if (item is ResolvedArmorProfile armor &&
            armor.ArmorCategory != ArmorCategory.None &&
            !heroClass.AllowsArmorCategory(armor.ArmorCategory))
        {
            error =
                $"{heroClass.DisplayName} cannot equip " +
                $"{armor.ArmorCategory} armor.";
            return false;
        }

        if (item is ResolvedWeaponProfile weapon &&
            !heroClass.AllowsWeaponType(weapon.WeaponType))
        {
            error =
                $"{heroClass.DisplayName} cannot equip " +
                $"{weapon.WeaponType} weapons.";
            return false;
        }

        if (item is ResolvedShieldProfile && !heroClass.CanEquipShields)
        {
            error = $"{heroClass.DisplayName} cannot equip shields.";
            return false;
        }

        return true;
    }
}
