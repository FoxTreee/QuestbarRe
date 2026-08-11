using Godot;
using System.Collections.Generic;

[GlobalClass]
public partial class AbilityDefinition : Resource
{
    [ExportCategory("Identity")]

    /// <summary>
    /// Stable content identifier for content; other systems use this value to find the same game data.
    /// For example, changing this ID makes the owning resource resolve a different registered content.
    /// </summary>
    [Export(PropertyHint.PlaceholderText, "ability.core.heavy_slam")]
    public string ContentId { get; set; } =
        string.Empty;

    /// <summary>
    /// Controls display name.
    /// For example, changing this text changes the name, message, key, or lookup value shown or consumed by the owning system.
    /// </summary>
    [Export(PropertyHint.PlaceholderText, "Heavy Slam")]
    public string DisplayName { get; set; } =
        "Unnamed Ability";

    /// <summary>
    /// Controls description.
    /// For example, changing this text changes the name, message, key, or lookup value shown or consumed by the owning system.
    /// </summary>
    [Export(PropertyHint.MultilineText)]
    public string Description { get; set; } =
        string.Empty;

    [ExportCategory("Timing")]

    /// <summary>
    /// Controls cooldown seconds, measured as seconds.
    /// For example, changing 6 to 12 makes the affected action wait twice as long between uses.
    /// </summary>
    [Export(PropertyHint.Range, "0,120,0.1")]
    public float CooldownSeconds { get; set; } =
        6.0f;

    /// <summary>
    /// Controls cast time seconds, measured as seconds.
    /// For example, changing 1 to 2 doubles this setting's configured contribution to the system.
    /// </summary>
    [Export(PropertyHint.Range, "0,10,0.05")]
    public float CastTimeSeconds { get; set; } =
        1.0f;

    [ExportCategory("Resource")]

    /// <summary>
    /// Resource spent when a hero uses this ability. A value of 25 makes the
    /// ability require and consume 25 of the caster's Mana, Energy, or Rage.
    /// </summary>
    [Export(PropertyHint.Range, "0,100000,1")]
    public float ResourceCost { get; set; } =
        0.0f;

    /// <summary>
    /// Combo points consumed when the ability commits. A value of 5 makes the
    /// ability require and spend five accumulated combo points.
    /// </summary>
    [Export(PropertyHint.Range, "0,5,1")]
    public int ComboPointCost { get; set; } =
        0;

    [ExportCategory("Targeting")]

    /// <summary>
    /// Defines the target shape used by the ability: current target, self, one
    /// ally, one monster, or an area of effect. This describes who/how many
    /// can be targeted; it does not describe what the ability does to them.
    /// </summary>
    [Export]
    public AbilityTargetMode TargetMode { get; set; } =
        AbilityTargetMode.CurrentTarget;

    /// <summary>
    /// Chooses one candidate when TargetMode is Ally or Monster, and can also
    /// choose the anchor for a target-centered area. LowestHealth compares
    /// health percentage so actors with different maximum health are fair.
    /// </summary>
    [Export]
    public AbilityTargetSelectionStyle TargetSelectionStyle { get; set; } =
        AbilityTargetSelectionStyle.LowestHealth;

    /// <summary>
    /// Defines which side receives an AreaOfEffect ability. This value is
    /// ignored by non-area abilities.
    /// </summary>
    [Export]
    public AbilityTargetGroup AreaTargetGroup { get; set; } =
        AbilityTargetGroup.Enemies;

    /// <summary>
    /// Defines whether an AreaOfEffect is centered on the caster or on a
    /// selected target. This value is ignored by non-area abilities.
    /// </summary>
    [Export]
    public AbilityAreaOrigin AreaOrigin { get; set; } =
        AbilityAreaOrigin.Self;

    /// <summary>
    /// Radius of an AreaOfEffect in logical gameplay pixels. AOE abilities
    /// require a value greater than zero.
    /// </summary>
    [Export(PropertyHint.Range, "0,1000,1")]
    public float AreaRadius { get; set; } =
        0.0f;

    /// <summary>
    /// Defines how this ability determines whether a selected target is in
    /// range. Fixed uses the authored Range value. BasicAttackRange inherits
    /// the caster's real basic-attack reach, including combat spacing, body
    /// radii, lunge distance, presentation scale, and melee engagement slots.
    /// </summary>
    [Export]
    public AbilityRangeMode RangeMode { get; set; } =
        AbilityRangeMode.Fixed;

    /// <summary>
    /// Maximum distance to a selected single target or target-centered area
    /// anchor, measured in logical gameplay pixels when RangeMode is Fixed.
    /// A value of zero continues to mean unlimited range. This value is
    /// ignored when RangeMode is BasicAttackRange.
    /// </summary>
    [Export(PropertyHint.Range, "0,500,1")]
    public float Range { get; set; } =
        45.0f;

    [ExportCategory("Effect")]

    /// <summary>
    /// Controls effect type.
    /// For example, selecting a different value changes which effect type behavior or content the owning system uses.
    /// </summary>
    [Export]
    public AbilityEffectType EffectType { get; set; } =
        AbilityEffectType.DirectDamage;

    /// <summary>
    /// Defines how a direct-damage ability calculates its requested damage.
    /// Fixed uses BaseDamage. BasicAttackMultiplier scales the caster's normal
    /// attack damage so finishers can remain tied to the hero's gear/stats.
    /// </summary>
    [Export]
    public AbilityDamageCalculationMode DamageCalculationMode { get; set; } =
        AbilityDamageCalculationMode.Fixed;

    /// <summary>
    /// Controls base damage, measured as damage points. This is used by fixed
    /// direct-damage abilities and by damage-over-time effects.
    /// </summary>
    [Export(PropertyHint.Range, "0,1000000,1")]
    public float BaseDamage { get; set; } =
        0.0f;

    /// <summary>
    /// Multiplier applied to the caster's basic attack damage when
    /// DamageCalculationMode is BasicAttackMultiplier. A value of 2 means
    /// exactly 200% of normal basic attack damage.
    /// </summary>
    [Export(PropertyHint.Range, "0,20,0.05")]
    public float BasicAttackDamageMultiplier { get; set; } =
        1.0f;

    /// <summary>
    /// Controls base healing.
    /// For example, changing 0 to 1 doubles this setting's configured contribution to the system.
    /// </summary>
    [Export(PropertyHint.Range, "0,1000000,1")]
    public float BaseHealing { get; set; } =
        0.0f;

    /// <summary>
    /// Controls effect duration seconds, measured as seconds.
    /// For example, changing 0 to 1 doubles this setting's configured contribution to the system.
    /// </summary>
    [Export(PropertyHint.Range, "0,60,0.1")]
    public float EffectDurationSeconds { get; set; } =
        0.0f;

    /// <summary>
    /// Controls effect tick interval seconds, measured as seconds.
    /// For example, changing 1 to 2 makes the affected action wait twice as long between uses.
    /// </summary>
    [Export(PropertyHint.Range, "0.05,60,0.05")]
    public float EffectTickIntervalSeconds { get; set; } =
        1.0f;

    [ExportCategory("Automatic Use")]

    /// <summary>
    /// Controls auto cast delay seconds, measured as seconds.
    /// For example, changing 0 to 1 makes the affected action wait twice as long between uses.
    /// </summary>
    [Export(PropertyHint.Range, "0,30,0.1")]
    public float AutoCastDelaySeconds { get; set; } =
        0.0f;

    /// <summary>
    /// Controls auto cast health threshold percent, measured as a ratio or multiplier.
    /// For example, changing 50 to 100 doubles the configured auto cast health threshold percent.
    /// </summary>
    [Export(PropertyHint.Range, "1,100,1")]
    public float AutoCastHealthThresholdPercent { get; set; } =
        50.0f;

    [ExportCategory("Authoring")]

    /// <summary>
    /// Controls designer notes.
    /// For example, changing this text changes the name, message, key, or lookup value shown or consumed by the owning system.
    /// </summary>
    [Export(PropertyHint.MultilineText)]
    public string DesignerNotes { get; set; } =
        string.Empty;

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
                $"Invalid ability Content ID '{ContentId}'. " +
                "Expected lowercase format such as " +
                "'ability.core.heavy_slam'.");
        }

        if (string.IsNullOrWhiteSpace(DisplayName))
        {
            errors.Add(
                $"{ContentId}: DisplayName is required.");
        }

        if (CooldownSeconds < 0.0f)
        {
            errors.Add(
                $"{ContentId}: CooldownSeconds cannot be " +
                "negative.");
        }

        if (CastTimeSeconds < 0.0f)
        {
            errors.Add(
                $"{ContentId}: CastTimeSeconds cannot be " +
                "negative.");
        }

        if (ResourceCost < 0.0f)
        {
            errors.Add(
                $"{ContentId}: ResourceCost cannot be negative.");
        }

        if (ComboPointCost < 0
            || ComboPointCost > HeroComboPointState.MaximumPoints)
        {
            errors.Add(
                $"{ContentId}: ComboPointCost must be between 0 and " +
                $"{HeroComboPointState.MaximumPoints}.");
        }

        if (!System.Enum.IsDefined(
            typeof(AbilityRangeMode),
            RangeMode))
        {
            errors.Add(
                $"{ContentId}: RangeMode is invalid.");
        }

        if (Range < 0.0f)
        {
            errors.Add(
                $"{ContentId}: Range cannot be negative.");
        }

        if (RangeMode == AbilityRangeMode.BasicAttackRange)
        {
            bool validBasicAttackRangeTarget =
                TargetMode == AbilityTargetMode.CurrentTarget
                || TargetMode == AbilityTargetMode.Monster
                || (TargetMode == AbilityTargetMode.AreaOfEffect
                    && AreaOrigin == AbilityAreaOrigin.Target
                    && AreaTargetGroup == AbilityTargetGroup.Enemies);

            if (!validBasicAttackRangeTarget)
            {
                errors.Add(
                    $"{ContentId}: BasicAttackRange currently requires " +
                    "CurrentTarget, Monster, or an enemy target-centered " +
                    "AreaOfEffect target.");
            }
        }

        if (BaseDamage < 0.0f)
        {
            errors.Add(
                $"{ContentId}: BaseDamage cannot be negative.");
        }

        if (!System.Enum.IsDefined(
            typeof(AbilityDamageCalculationMode),
            DamageCalculationMode))
        {
            errors.Add(
                $"{ContentId}: DamageCalculationMode is invalid.");
        }

        if (EffectType == AbilityEffectType.DirectDamage
            && DamageCalculationMode
                == AbilityDamageCalculationMode.BasicAttackMultiplier
            && BasicAttackDamageMultiplier <= 0.0f)
        {
            errors.Add(
                $"{ContentId}: BasicAttackDamageMultiplier must be " +
                "greater than zero for multiplier-based direct damage.");
        }

        if (BaseHealing < 0.0f)
        {
            errors.Add(
                $"{ContentId}: BaseHealing cannot be negative.");
        }

        if (AreaRadius < 0.0f)
        {
            errors.Add(
                $"{ContentId}: AreaRadius cannot be negative.");
        }

        if (TargetMode == AbilityTargetMode.AreaOfEffect
            && AreaRadius <= 0.0f)
        {
            errors.Add(
                $"{ContentId}: AreaOfEffect abilities require an " +
                "AreaRadius greater than zero.");
        }

        if (TargetMode == AbilityTargetMode.AreaOfEffect
            && AreaOrigin == AbilityAreaOrigin.Target
            && AreaTargetGroup == AbilityTargetGroup.Everyone)
        {
            errors.Add(
                $"{ContentId}: target-centered AreaOfEffect abilities " +
                "cannot use AreaTargetGroup Everyone yet. Choose Allies " +
                "or Enemies, or center the area on Self.");
        }

        if (EffectDurationSeconds < 0.0f)
        {
            errors.Add(
                $"{ContentId}: EffectDurationSeconds cannot be " +
                "negative.");
        }

        if (EffectTickIntervalSeconds <= 0.0f)
        {
            errors.Add(
                $"{ContentId}: EffectTickIntervalSeconds must be " +
                "greater than zero.");
        }

        if (AutoCastDelaySeconds < 0.0f)
        {
            errors.Add(
                $"{ContentId}: AutoCastDelaySeconds cannot be " +
                "negative.");
        }

        if (AutoCastHealthThresholdPercent <= 0.0f
            || AutoCastHealthThresholdPercent > 100.0f)
        {
            errors.Add(
                $"{ContentId}: AutoCastHealthThresholdPercent " +
                "must be greater than zero and no more than 100.");
        }

        if (EffectType == AbilityEffectType.AreaTaunt)
        {
            if (TargetMode != AbilityTargetMode.AreaOfEffect)
            {
                errors.Add(
                    $"{ContentId}: AreaTaunt abilities must use " +
                    "the AreaOfEffect target mode.");
            }

            if (AreaTargetGroup != AbilityTargetGroup.Enemies)
            {
                errors.Add(
                    $"{ContentId}: AreaTaunt currently requires " +
                    "AreaTargetGroup Enemies.");
            }

            if (AreaOrigin != AbilityAreaOrigin.Self)
            {
                errors.Add(
                    $"{ContentId}: AreaTaunt currently requires " +
                    "AreaOrigin Self.");
            }

            if (EffectDurationSeconds <= 0.0f)
            {
                errors.Add(
                    $"{ContentId}: AreaTaunt abilities require an " +
                    "EffectDurationSeconds value greater than zero.");
            }
        }

        if (EffectType == AbilityEffectType.DirectHealing)
        {
            bool validHealingTarget =
                TargetMode == AbilityTargetMode.Self
                || TargetMode == AbilityTargetMode.Ally;

            if (!validHealingTarget)
            {
                errors.Add(
                    $"{ContentId}: DirectHealing currently supports " +
                    "Self or Ally targeting. AOE healing will use the " +
                    "generic AreaOfEffect model when its effect resolver " +
                    "is implemented.");
            }

            if (BaseHealing <= 0.0f)
            {
                errors.Add(
                    $"{ContentId}: DirectHealing abilities require " +
                    "BaseHealing greater than zero.");
            }
        }

        if (EffectType == AbilityEffectType.DamageOverTime)
        {
            if (CastTimeSeconds > 0.0f)
            {
                errors.Add(
                    $"{ContentId}: DamageOverTime abilities that " +
                    "replace a basic attack currently require a " +
                    "zero-second CastTimeSeconds value.");
            }

            if (TargetMode != AbilityTargetMode.CurrentTarget)
            {
                errors.Add(
                    $"{ContentId}: DamageOverTime abilities that " +
                    "replace a basic attack currently require the " +
                    "CurrentTarget target mode.");
            }

            if (BaseDamage <= 0.0f)
            {
                errors.Add(
                    $"{ContentId}: DamageOverTime abilities require " +
                    "BaseDamage greater than zero.");
            }

            if (EffectDurationSeconds <= 0.0f)
            {
                errors.Add(
                    $"{ContentId}: DamageOverTime abilities require " +
                    "EffectDurationSeconds greater than zero.");
            }

            if (EffectTickIntervalSeconds
                > EffectDurationSeconds)
            {
                errors.Add(
                    $"{ContentId}: EffectTickIntervalSeconds cannot " +
                    "exceed EffectDurationSeconds for a " +
                    "DamageOverTime ability.");
            }
        }

        return errors;
    }
}
