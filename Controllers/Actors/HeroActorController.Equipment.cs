using Godot;

public partial class HeroActorController
{
    /// <summary>
    /// Raised after runtime equipment, derived stats, armor, and active weapon
    /// timing have all been rebuilt.
    /// </summary>
    public event System.Action<HeroActorController>? EquipmentChanged;

    private readonly RandomNumberGenerator
        _weaponDamageRandom = new();

    /// <summary>
    /// Runtime weapon loadout resolved for this hero.
    /// </summary>
    public HeroEquipmentLoadout Equipment { get; } = new();

    /// <summary>
    /// Current aggregate core stats supplied only by equipped gear.
    /// </summary>
    public EquipmentStatTotals EquipmentStats =>
        Equipment.TotalStats;

    /// <summary>
    /// Raw Armor currently supplied by equipped armor-bearing items.
    /// Armor does not reduce damage until its formula is explicitly designed.
    /// </summary>
    public int EquipmentArmor =>
        Equipment.TotalArmor;

    public int TotalStrength =>
        CurrentBaseStats.Strength + EquipmentStats.Strength;

    public int TotalAgility =>
        CurrentBaseStats.Agility + EquipmentStats.Agility;

    public int TotalStamina =>
        CurrentBaseStats.Stamina + EquipmentStats.Stamina;

    public int TotalIntellect =>
        CurrentBaseStats.Intellect + EquipmentStats.Intellect;

    public int TotalSpirit =>
        CurrentBaseStats.Spirit + EquipmentStats.Spirit;

    public HeroLevelStatDefinition CurrentBaseStats =>
        Definition?.GetLevelStats(Progression.Level)
        ?? new HeroLevelStatDefinition();

    public float MaximumHealthFromStats =>
        Mathf.Max(
            CurrentBaseStats.BaseHealth
            + TotalStamina
            * (Definition?.HealthPerStamina ?? 10.0f),
            1.0f);

    public float MeleeStrengthDamageBonus =>
        Mathf.Max(
            TotalStrength
            * (Definition?.MeleeDamagePerStrength ?? 2.0f),
            0.0f);

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
        WeaponPreference == HeroWeaponPreference.Ranged
            ? Equipment.RangedWeapon ?? Equipment.MainHandWeapon
            : Equipment.MainHandWeapon ?? Equipment.RangedWeapon;


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

        RefreshDerivedCombatStats();

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
        RefreshDerivedCombatStats();

        return true;
    }


    /// <summary>
    /// Rolls final normal-attack damage. Melee weapons and unarmed attacks add
    /// two damage per total Strength; ranged weapons keep their authored range.
    /// </summary>
    public float RollActiveWeaponDamage()
    {
        GetActiveNormalAttackDamageRange(
            out int minimumDamage,
            out int maximumDamage);

        return _weaponDamageRandom.RandiRange(
            minimumDamage,
            maximumDamage);
    }

    /// <summary>
    /// Resolves the currently active normal-attack range for combat and UI.
    /// With no weapon equipped, the hero automatically uses unarmed melee.
    /// </summary>
    public void GetActiveNormalAttackDamageRange(
        out int minimumDamage,
        out int maximumDamage)
    {
        ResolvedWeaponProfile? weapon = ActiveNormalAttackWeapon;
        bool isRanged = weapon is not null
            && ReferenceEquals(weapon, Equipment.RangedWeapon);

        float rawMinimum = weapon?.MinimumDamage
            ?? Definition?.UnarmedMinimumDamage
            ?? 2.0f;
        float rawMaximum = weapon?.MaximumDamage
            ?? Definition?.UnarmedMaximumDamage
            ?? 5.0f;
        float strengthBonus = isRanged
            ? 0.0f
            : MeleeStrengthDamageBonus;

        minimumDamage = Mathf.Max(
            Mathf.RoundToInt(rawMinimum + strengthBonus),
            0);
        maximumDamage = Mathf.Max(
            Mathf.RoundToInt(rawMaximum + strengthBonus),
            minimumDamage);
    }

    /// <summary>
    /// Resolves the Main Hand panel even when a ranged weapon is preferred.
    /// An empty Main Hand is presented as the hero's unarmed melee profile.
    /// </summary>
    public void GetMeleeDamageRange(
        out int minimumDamage,
        out int maximumDamage)
    {
        ResolvedWeaponProfile? weapon = Equipment.MainHandWeapon;
        float rawMinimum = weapon?.MinimumDamage
            ?? Definition?.UnarmedMinimumDamage
            ?? 2.0f;
        float rawMaximum = weapon?.MaximumDamage
            ?? Definition?.UnarmedMaximumDamage
            ?? 5.0f;

        minimumDamage = Mathf.Max(
            Mathf.RoundToInt(rawMinimum + MeleeStrengthDamageBonus),
            0);
        maximumDamage = Mathf.Max(
            Mathf.RoundToInt(rawMaximum + MeleeStrengthDamageBonus),
            minimumDamage);
    }

    public float MeleeAttackInterval =>
        Mathf.Max(
            Equipment.MainHandWeapon?.AttackSpeedSeconds
            ?? Definition?.UnarmedAttackInterval
            ?? 2.0f,
            0.1f);


    /// <summary>
    /// Makes the active weapon's Speed value authoritative for the existing
    /// normal-attack timing system. The legacy hero AttackInterval remains only
    /// as transitional data when no active weapon is equipped.
    /// </summary>
    private void RefreshDerivedCombatStats()
    {
        ResolvedWeaponProfile? weapon =
            ActiveNormalAttackWeapon;
        float attackSpeedSeconds =
            Mathf.Max(
                weapon?.AttackSpeedSeconds
                ?? Definition?.UnarmedAttackInterval
                ?? 2.0f,
                0.1f);

        // Configure() runs before this starting-equipment call, and _Ready()
        // later copies TemporaryAttackInterval into CombatProfile. Setting both
        // values also keeps this method ready for future runtime weapon swaps.
        TemporaryAttackInterval =
            attackSpeedSeconds;

        CombatProfile.AttackInterval =
            attackSpeedSeconds;

        TemporaryMaximumHealth = MaximumHealthFromStats;
        CombatProfile.MaximumHealth = MaximumHealthFromStats;
        Health.SetMaximumHealth(MaximumHealthFromStats);
    }

    public void NotifyRuntimeEquipmentChanged()
    {
        Equipment.RebuildEquipmentTotals();
        RefreshDerivedCombatStats();
        EquipmentChanged?.Invoke(this);
    }

    /// <summary>
    /// Rebuilds derived stats immediately after XP raises the hero's level.
    /// The same event refreshes the Character window's authoritative totals.
    /// </summary>
    private void OnProgressionStatsChanged()
    {
        RefreshDerivedCombatStats();
        EquipmentChanged?.Invoke(this);
    }


    private static string FormatWeapon(
        ResolvedWeaponProfile? weapon)
    {
        return weapon is null
            ? "(empty)"
            : weapon.DefinitionContentId;
    }
}
