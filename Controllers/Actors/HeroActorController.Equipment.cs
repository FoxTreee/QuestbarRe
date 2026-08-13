using Godot;

public partial class HeroActorController
{
    private readonly RandomNumberGenerator
        _weaponDamageRandom = new();

    /// <summary>
    /// Runtime weapon loadout resolved for this hero.
    /// </summary>
    public HeroEquipmentLoadout Equipment { get; } = new();

    /// <summary>
    /// Current aggregate core stats supplied by this hero's equipped gear.
    /// These values are intentionally not applied to combat yet.
    /// </summary>
    public EquipmentStatTotals EquipmentStats =>
        Equipment.TotalStats;

    /// <summary>
    /// Raw Armor currently supplied by equipped armor-bearing items.
    /// Armor does not reduce damage until its formula is explicitly designed.
    /// </summary>
    public int EquipmentArmor =>
        Equipment.TotalArmor;

    /// <summary>
    /// Determines which equipped weapon supplies normal-attack weapon damage
    /// and attack speed. Off Hand is intentionally not part of normal attack
    /// selection until Dual Wield is implemented as its own mechanic.
    /// </summary>
    public HeroWeaponPreference WeaponPreference
    { get; private set; } = HeroWeaponPreference.Melee;

    /// <summary>
    /// The weapon currently authoritative for this hero's normal weapon attack.
    /// Melee uses Main Hand. Ranged uses the independent Ranged slot.
    /// </summary>
    public ResolvedWeaponProfile? ActiveNormalAttackWeapon =>
        WeaponPreference switch
        {
            HeroWeaponPreference.Melee =>
                Equipment.MainHandWeapon,

            HeroWeaponPreference.Ranged =>
                Equipment.RangedWeapon,

            _ => null
        };


    public bool TryConfigureStartingEquipment(
        WeaponDefinition? mainHand,
        WeaponDefinition? offHand,
        WeaponDefinition? ranged,
        out string error)
    {
        bool configured =
            Equipment.TryConfigureWeapons(
                mainHand,
                offHand,
                ranged,
                out error);

        if (!configured)
            return false;

        WeaponPreference =
            Definition?.StartingWeaponPreference
            ?? HeroWeaponPreference.Melee;

        _weaponDamageRandom.Randomize();

        RefreshActiveWeaponAttackTiming();

        DebugLog.Print(
            $"{Name} weapon loadout: " +
            $"MainHand={FormatWeapon(Equipment.MainHandWeapon)}, " +
            $"OffHand={FormatWeapon(Equipment.OffHandWeapon)}, " +
            $"Ranged={FormatWeapon(Equipment.RangedWeapon)}, " +
            $"Preference={WeaponPreference}, " +
            $"Active={FormatWeapon(ActiveNormalAttackWeapon)}.");

        DebugLog.Print(
            $"{Name} equipment stats: " +
            $"{EquipmentStats}, " +
            $"Armor={EquipmentArmor}.");

        return true;
    }


    /// <summary>
    /// Changes which equipped weapon is authoritative for normal attacks.
    /// This is ready for a future loadout/preferences UI; current starter heroes
    /// remain authored as Melee.
    /// </summary>
    public bool TrySetWeaponPreference(
        HeroWeaponPreference preference,
        out string error)
    {
        error = string.Empty;

        if (!System.Enum.IsDefined(preference))
        {
            error =
                $"Weapon preference '{preference}' is invalid.";

            return false;
        }

        ResolvedWeaponProfile? preferredWeapon =
            preference switch
            {
                HeroWeaponPreference.Melee =>
                    Equipment.MainHandWeapon,

                HeroWeaponPreference.Ranged =>
                    Equipment.RangedWeapon,

                _ => null
            };

        if (preferredWeapon is null)
        {
            error =
                $"{Name} cannot prefer {preference} because the " +
                "corresponding weapon slot is empty.";

            return false;
        }

        WeaponPreference = preference;
        RefreshActiveWeaponAttackTiming();

        return true;
    }


    /// <summary>
    /// Rolls raw normal-attack damage directly from the active weapon's
    /// authored damage range. Character stats intentionally do not modify this
    /// roll yet.
    /// </summary>
    public float RollActiveWeaponDamage()
    {
        ResolvedWeaponProfile? weapon =
            ActiveNormalAttackWeapon;

        if (weapon is null)
            return 0.0f;

        int minimumDamage =
            Mathf.Max(
                Mathf.RoundToInt(weapon.MinimumDamage),
                0);

        int maximumDamage =
            Mathf.Max(
                Mathf.RoundToInt(weapon.MaximumDamage),
                minimumDamage);

        return _weaponDamageRandom.RandiRange(
            minimumDamage,
            maximumDamage);
    }


    /// <summary>
    /// Makes the active weapon's Speed value authoritative for the existing
    /// normal-attack timing system. The legacy hero AttackInterval remains only
    /// as transitional data when no active weapon is equipped.
    /// </summary>
    private void RefreshActiveWeaponAttackTiming()
    {
        ResolvedWeaponProfile? weapon =
            ActiveNormalAttackWeapon;

        if (weapon is null)
            return;

        float attackSpeedSeconds =
            Mathf.Max(
                weapon.AttackSpeedSeconds,
                0.1f);

        // Configure() runs before this starting-equipment call, and _Ready()
        // later copies TemporaryAttackInterval into CombatProfile. Setting both
        // values also keeps this method ready for future runtime weapon swaps.
        TemporaryAttackInterval =
            attackSpeedSeconds;

        CombatProfile.AttackInterval =
            attackSpeedSeconds;
    }


    private static string FormatWeapon(
        ResolvedWeaponProfile? weapon)
    {
        return weapon is null
            ? "(empty)"
            : weapon.DefinitionContentId;
    }
}
