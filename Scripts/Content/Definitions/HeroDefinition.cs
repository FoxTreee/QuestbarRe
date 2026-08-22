using Godot;
using System.Collections.Generic;

[GlobalClass]
public partial class HeroDefinition : Resource
{
    [ExportCategory("Identity")]

    /// <summary>
    /// Stable content identifier for content; other systems use this value to find the same game data.
    /// For example, changing this ID makes the owning resource resolve a different registered content.
    /// </summary>
    [Export(PropertyHint.PlaceholderText, "hero.core.syzygy")]
    public string ContentId { get; set; } =
        string.Empty;

    /// <summary>
    /// Controls display name.
    /// For example, changing this text changes the name, message, key, or lookup value shown or consumed by the owning system.
    /// </summary>
    [Export(PropertyHint.PlaceholderText, "Syzygy")]
    public string DisplayName { get; set; } =
        "Unnamed Hero";


    [ExportCategory("Class")]

    /// <summary>
    /// Inspector reference used by this component for its class definition dependency.
    /// Assign the matching node or resource from the scene; leaving it empty prevents that connection from working.
    /// </summary>
    [Export]
    public HeroClassDefinition ClassDefinition
    { get; set; } = null!;


    [ExportCategory("Base Attributes")]

    /// <summary>
    /// Innate Strength before equipment. Each total point currently adds two
    /// damage to both ends of every melee damage range.
    /// </summary>
    [Export(PropertyHint.Range, "0,1000,1")]
    public int BaseStrength { get; set; } = 5;

    [Export(PropertyHint.Range, "0,1000,1")]
    public int BaseAgility { get; set; } = 2;

    /// <summary>
    /// Innate Stamina before equipment. Each total point currently grants ten
    /// maximum health in addition to BaseHealth.
    /// </summary>
    [Export(PropertyHint.Range, "0,1000,1")]
    public int BaseStamina { get; set; } = 6;

    [Export(PropertyHint.Range, "0,1000,1")]
    public int BaseIntellect { get; set; } = 1;

    [Export(PropertyHint.Range, "0,1000,1")]
    public int BaseSpirit { get; set; } = 1;


    [ExportCategory("Level 1-60 Base Stat Table")]

    /// <summary>
    /// Authoritative naked stats for every level. Entry 0 is Level 1 and entry
    /// 59 is Level 60. The supplied Warrior curve starts at 5/2/6/1/1 with 100
    /// base health and ends at 28/14/60/7/12 with 2,009 base health.
    /// Every generated row remains independently editable in the Inspector.
    /// </summary>
    [Export]
    public Godot.Collections.Array<HeroLevelStatDefinition> LevelStats
    { get; set; } = CreateDefaultWarriorLevelStats();


    [ExportCategory("Derived Stat Rules")]

    /// <summary>
    /// Health shared by every starting hero before Stamina is applied.
    /// </summary>
    [Export(PropertyHint.Range, "1,1000000,1")]
    public float BaseHealth { get; set; } = 100.0f;

    [Export(PropertyHint.Range, "0,1000,1")]
    public float HealthPerStamina { get; set; } = 10.0f;

    [Export(PropertyHint.Range, "0,1000,1")]
    public float MeleeDamagePerStrength { get; set; } = 2.0f;


    [ExportCategory("Unarmed Melee")]

    [Export(PropertyHint.Range, "0,1000000,1")]
    public float UnarmedMinimumDamage { get; set; } = 2.0f;

    [Export(PropertyHint.Range, "0,1000000,1")]
    public float UnarmedMaximumDamage { get; set; } = 4.0f;

    [Export(PropertyHint.Range, "0.1,30,0.05")]
    public float UnarmedAttackInterval { get; set; } = 2.0f;


    [ExportCategory("Starting Progression")]

    /// <summary>
    /// Starting level used when no saved progression exists.
    /// </summary>
    [Export(PropertyHint.Range, "1,60,1")]
    public int StartingLevel { get; set; } = 1;

    /// <summary>
    /// XP already earned toward the next level when no save exists.
    /// </summary>
    [Export(PropertyHint.Range, "0,100000000000000000000,1")]
    public double StartingExperience { get; set; } = 0.0;


    [ExportCategory("Runtime")]

    /// <summary>
    /// Inspector reference used by this component for its actor scene dependency.
    /// Assign the matching node or resource from the scene; leaving it empty prevents that connection from working.
    /// </summary>
    [Export]
    public PackedScene ActorScene { get; set; } = null!;


    [ExportCategory("Combat Identity")]

    /// <summary>
    /// Controls combat tag mask.
    /// For example, selecting a different value changes which combat tag mask behavior or content the owning system uses.
    /// </summary>
    [Export(PropertyHint.Flags, "Melee,Ranged,Caster,Healer,Tank,Summoner,Armored")]
    public int CombatTagMask { get; set; } =
        (int)HeroCombatTag.Melee;

    public HeroCombatTag CombatTags =>
        (HeroCombatTag)CombatTagMask;


    [ExportCategory("Combat Stance")]

    /// <summary>
    /// Controls starting combat stance.
    /// For example, selecting a different value changes which starting combat stance behavior or content the owning system uses.
    /// </summary>
    [Export]
    public HeroCombatStance StartingCombatStance
    { get; set; } = HeroCombatStance.Defensive;


    [ExportCategory("Health")]

    /// <summary>
    /// Controls maximum health, measured as health points.
    /// For example, changing 100 to 200 doubles the configured maximum health.
    /// </summary>
    [Export(PropertyHint.Range, "1,1000000,1")]
    public float MaximumHealth { get; set; } =
        100.0f;


    [ExportCategory("Attack")]

    /// <summary>
    /// Controls attack damage, measured as damage points.
    /// For example, changing 20 to 40 doubles the configured attack damage.
    /// </summary>
    [Export(PropertyHint.Range, "0,1000000,1")]
    public float AttackDamage { get; set; } =
        20.0f;

    /// <summary>
    /// Controls attack range, measured as pixels.
    /// For example, changing 28 to 56 doubles the configured attack range.
    /// </summary>
    [Export(PropertyHint.Range, "0,400,1")]
    public float AttackRange { get; set; } =
        28.0f;

    /// <summary>
    /// Controls attack interval, measured as seconds.
    /// For example, changing 1.5 to 3 makes the affected action wait twice as long between uses.
    /// </summary>
    [Export(PropertyHint.Range, "0.1,30,0.1")]
    public float AttackInterval { get; set; } =
        1.5f;

    /// <summary>
    /// Controls attack duration, measured as seconds.
    /// For example, changing 0.3 to 0.6 doubles this setting's configured contribution to the system.
    /// </summary>
    [Export(PropertyHint.Range, "0.05,5,0.05")]
    public float AttackDuration { get; set; } =
        0.3f;

    /// <summary>
    /// Controls attack release point.
    /// For example, changing 0.5 to 1 doubles this setting's configured contribution to the system.
    /// </summary>
    [Export(PropertyHint.Range, "0,1,0.05")]
    public float AttackReleasePoint { get; set; } =
        0.5f;

    /// <summary>
    /// Controls attack lunge distance, measured as pixels.
    /// For example, changing 8 to 16 doubles the configured attack lunge distance.
    /// </summary>
    [Export(PropertyHint.Range, "0,100,0.5")]
    public float AttackLungeDistance { get; set; } =
        8.0f;

    /// <summary>
    /// Controls attack delivery.
    /// For example, selecting a different value changes which attack delivery behavior or content the owning system uses.
    /// </summary>
    [Export]
    public AttackDeliveryMode AttackDelivery { get; set; } =
        AttackDeliveryMode.ImmediateImpact;


    [ExportCategory("Movement")]

    /// <summary>
    /// Controls combat move speed, measured as pixels per second.
    /// For example, changing 140 to 280 makes the affected movement or animation run about twice as fast.
    /// </summary>
    [Export(PropertyHint.Range, "0,1000,1")]
    public float CombatMoveSpeed { get; set; } =
        140.0f;


    [ExportCategory("Starting Weapon Loadout")]

    /// <summary>
    /// Chooses whether this hero's normal attacks use the equipped Main Hand
    /// melee weapon or the independent Ranged weapon. Current starter heroes
    /// default to Melee.
    /// </summary>
    [Export]
    public HeroWeaponPreference StartingWeaponPreference
    { get; set; } = HeroWeaponPreference.Melee;


    /// <summary>
    /// Weapon definition equipped in Main Hand when this hero is created
    /// without saved/authoritative equipment data.
    /// </summary>
    [Export(PropertyHint.PlaceholderText, "weapon.core.example")]
    public string StartingMainHandWeaponContentId
    { get; set; } = string.Empty;

    /// <summary>
    /// Weapon definition equipped in Off Hand when this hero is created
    /// without saved/authoritative equipment data. Leave empty when unused.
    /// </summary>
    [Export(PropertyHint.PlaceholderText, "weapon.core.example")]
    public string StartingOffHandWeaponContentId
    { get; set; } = string.Empty;

    /// <summary>
    /// Weapon definition equipped in the independent Ranged slot when this hero
    /// is created without saved/authoritative equipment data.
    /// </summary>
    [Export(PropertyHint.PlaceholderText, "weapon.core.example")]
    public string StartingRangedWeaponContentId
    { get; set; } = string.Empty;


    [ExportCategory("Starting Ability Loadout")]

    /// <summary>
    /// First class ability equipped when this hero is created without saved
    /// loadout data. Leave empty for an unused slot. The ability must belong to
    /// the hero's class ability pool.
    /// </summary>
    [Export(PropertyHint.PlaceholderText, "ability.core.example")]
    public string StartingAbilitySlot1ContentId
    { get; set; } = string.Empty;

    /// <summary>
    /// Second class ability equipped when this hero is created without saved
    /// loadout data. Leave empty for an unused slot. The ability must belong to
    /// the hero's class ability pool and cannot duplicate slot 1.
    /// </summary>
    [Export(PropertyHint.PlaceholderText, "ability.core.example")]
    public string StartingAbilitySlot2ContentId
    { get; set; } = string.Empty;

    /// <summary>
    /// Enumerates the non-empty starting ability slots in slot order.
    /// Runtime systems should resolve only these IDs, never the entire class
    /// ability pool.
    /// </summary>
    public IEnumerable<string> GetStartingEquippedAbilityIds()
    {
        if (!string.IsNullOrWhiteSpace(
            StartingAbilitySlot1ContentId))
        {
            yield return
                StartingAbilitySlot1ContentId.Trim();
        }

        if (!string.IsNullOrWhiteSpace(
            StartingAbilitySlot2ContentId))
        {
            yield return
                StartingAbilitySlot2ContentId.Trim();
        }
    }

    /// <summary>
    /// Returns the authored naked stat row for a level. A malformed table falls
    /// back to the original Level 1 fields so combat can report validation
    /// errors without crashing during content iteration.
    /// </summary>
    public HeroLevelStatDefinition GetLevelStats(int level)
    {
        int index = Mathf.Clamp(
            level,
            1,
            HeroProgressionState.MaximumLevel) - 1;

        if (index < LevelStats.Count
            && GodotObject.IsInstanceValid(LevelStats[index]))
        {
            return LevelStats[index];
        }

        return new HeroLevelStatDefinition
        {
            Level = index + 1,
            Strength = BaseStrength,
            Agility = BaseAgility,
            Stamina = BaseStamina,
            Intellect = BaseIntellect,
            Spirit = BaseSpirit,
            BaseHealth = BaseHealth
        };
    }

    /// <summary>
    /// Seeds a complete, editable Warrior table using rounded interpolation.
    /// Rounding intentionally produces WoW-like uneven individual level gains
    /// while guaranteeing the exact approved Level 1 and Level 60 endpoints.
    /// </summary>
    private static Godot.Collections.Array<HeroLevelStatDefinition>
        CreateDefaultWarriorLevelStats()
    {
        Godot.Collections.Array<HeroLevelStatDefinition> rows = new();

        for (int level = 1;
            level <= HeroProgressionState.MaximumLevel;
            level++)
        {
            double progress = (level - 1) / 59.0;

            rows.Add(new HeroLevelStatDefinition
            {
                Level = level,
                Strength = InterpolateWhole(5, 28, progress),
                Agility = InterpolateWhole(2, 14, progress),
                Stamina = InterpolateWhole(6, 60, progress),
                Intellect = InterpolateWhole(1, 7, progress),
                Spirit = InterpolateWhole(1, 12, progress),
                BaseHealth = InterpolateWhole(100, 2009, progress)
            });
        }

        return rows;
    }

    private static int InterpolateWhole(
        int start,
        int end,
        double progress)
    {
        return (int)System.Math.Round(
            start + (end - start) * progress,
            System.MidpointRounding.AwayFromZero);
    }


    /// <summary>
    /// Retrieves validation errors from the current game state.
    /// Reads the current state and returns the resulting i read only list string to the caller.
    /// </summary>
    public IReadOnlyList<string> GetValidationErrors()
    {
        List<string> errors = new();

        if (!global::ContentId.IsValid(ContentId))
        {
            errors.Add(
                $"Invalid hero Content ID '{ContentId}'. " +
                "Expected lowercase format such as " +
                "'hero.core.syzygy'.");
        }

        if (string.IsNullOrWhiteSpace(DisplayName))
        {
            errors.Add(
                $"{ContentId}: DisplayName is required.");
        }

        if (!GodotObject.IsInstanceValid(ClassDefinition))
        {
            errors.Add(
                $"{ContentId}: ClassDefinition is required.");
        }
        else
        {
            foreach (string classError
                in ClassDefinition.GetValidationErrors())
            {
                errors.Add(
                    $"{ContentId}: invalid class reference: " +
                    classError);
            }
        }

        if (BaseStrength < 0 || BaseAgility < 0
            || BaseStamina < 0 || BaseIntellect < 0
            || BaseSpirit < 0)
        {
            errors.Add(
                $"{ContentId}: base attributes cannot be negative.");
        }

        if (LevelStats.Count != HeroProgressionState.MaximumLevel)
        {
            errors.Add(
                $"{ContentId}: LevelStats must contain exactly 60 rows.");
        }
        else
        {
            for (int index = 0; index < LevelStats.Count; index++)
            {
                HeroLevelStatDefinition row = LevelStats[index];

                if (!GodotObject.IsInstanceValid(row)
                    || row.Level != index + 1
                    || row.Strength < 0 || row.Agility < 0
                    || row.Stamina < 0 || row.Intellect < 0
                    || row.Spirit < 0 || row.BaseHealth <= 0.0f)
                {
                    errors.Add(
                        $"{ContentId}: LevelStats entry {index} must be " +
                        $"a valid Level {index + 1} row.");
                }
            }
        }

        if (BaseHealth <= 0.0f || HealthPerStamina < 0.0f)
        {
            errors.Add(
                $"{ContentId}: base-health values are invalid.");
        }

        if (MeleeDamagePerStrength < 0.0f
            || UnarmedMinimumDamage < 0.0f
            || UnarmedMaximumDamage < UnarmedMinimumDamage
            || UnarmedAttackInterval <= 0.0f)
        {
            errors.Add(
                $"{ContentId}: unarmed melee values are invalid.");
        }

        if (StartingLevel < 1
            || StartingLevel > HeroProgressionState.MaximumLevel)
        {
            errors.Add(
                $"{ContentId}: StartingLevel must be between 1 and 60.");
        }

        if (StartingExperience < 0.0)
        {
            errors.Add(
                $"{ContentId}: StartingExperience cannot be negative.");
        }

        if (!GodotObject.IsInstanceValid(ActorScene))
        {
            errors.Add(
                $"{ContentId}: ActorScene is required.");
        }

        if (MaximumHealth <= 0.0f)
        {
            errors.Add(
                $"{ContentId}: MaximumHealth must be " +
                "greater than zero.");
        }

        if (AttackDamage < 0.0f)
        {
            errors.Add(
                $"{ContentId}: AttackDamage cannot be " +
                "negative.");
        }

        if (AttackRange < 0.0f)
        {
            errors.Add(
                $"{ContentId}: AttackRange cannot be " +
                "negative.");
        }

        if (AttackInterval <= 0.0f)
        {
            errors.Add(
                $"{ContentId}: AttackInterval must be " +
                "greater than zero.");
        }

        if (AttackDuration <= 0.0f)
        {
            errors.Add(
                $"{ContentId}: AttackDuration must be " +
                "greater than zero.");
        }

        if (AttackReleasePoint < 0.0f
            || AttackReleasePoint > 1.0f)
        {
            errors.Add(
                $"{ContentId}: AttackReleasePoint must " +
                "be between 0 and 1.");
        }

        if (AttackLungeDistance < 0.0f)
        {
            errors.Add(
                $"{ContentId}: AttackLungeDistance cannot " +
                "be negative.");
        }

        if (CombatMoveSpeed < 0.0f)
        {
            errors.Add(
                $"{ContentId}: CombatMoveSpeed cannot be " +
                "negative.");
        }

        if (!System.Enum.IsDefined(
            typeof(HeroCombatStance),
            StartingCombatStance))
        {
            errors.Add(
                $"{ContentId}: StartingCombatStance is invalid.");
        }

        if (!System.Enum.IsDefined(StartingWeaponPreference))
        {
            errors.Add(
                $"{ContentId}: StartingWeaponPreference is invalid.");
        }

        ValidateStartingWeaponContentId(
            StartingMainHandWeaponContentId,
            "StartingMainHandWeaponContentId",
            errors);

        ValidateStartingWeaponContentId(
            StartingOffHandWeaponContentId,
            "StartingOffHandWeaponContentId",
            errors);

        ValidateStartingWeaponContentId(
            StartingRangedWeaponContentId,
            "StartingRangedWeaponContentId",
            errors);

        ValidateStartingAbilitySlot(
            StartingAbilitySlot1ContentId,
            "StartingAbilitySlot1ContentId",
            errors);

        ValidateStartingAbilitySlot(
            StartingAbilitySlot2ContentId,
            "StartingAbilitySlot2ContentId",
            errors);

        if (!string.IsNullOrWhiteSpace(
                StartingAbilitySlot1ContentId)
            && string.Equals(
                StartingAbilitySlot1ContentId.Trim(),
                StartingAbilitySlot2ContentId?.Trim(),
                System.StringComparison.OrdinalIgnoreCase))
        {
            errors.Add(
                $"{ContentId}: starting ability slots cannot equip " +
                $"the same ability twice.");
        }

        return errors;
    }

    private void ValidateStartingWeaponContentId(
        string weaponContentId,
        string propertyName,
        List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(weaponContentId))
            return;

        string normalizedId = weaponContentId.Trim();

        if (!global::ContentId.IsValid(normalizedId)
            || !normalizedId.StartsWith(
                "weapon.",
                System.StringComparison.Ordinal))
        {
            errors.Add(
                $"{ContentId}: {propertyName} contains invalid weapon " +
                $"Content ID '{weaponContentId}'.");
        }
    }

    private void ValidateStartingAbilitySlot(
        string abilityContentId,
        string propertyName,
        List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(abilityContentId))
            return;

        string normalizedId = abilityContentId.Trim();

        if (!global::ContentId.IsValid(normalizedId))
        {
            errors.Add(
                $"{ContentId}: {propertyName} contains invalid ability " +
                $"Content ID '{abilityContentId}'.");

            return;
        }

        if (!GodotObject.IsInstanceValid(ClassDefinition))
            return;

        if (!ClassDefinition.ContainsAbility(normalizedId))
        {
            errors.Add(
                $"{ContentId}: equipped ability '{normalizedId}' in " +
                $"{propertyName} is not present in class ability pool " +
                $"'{ClassDefinition.ContentId}'.");
        }
    }
}
