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

    [ExportCategory("Targeting")]

    /// <summary>
    /// Controls target mode.
    /// For example, selecting a different value changes which target mode behavior or content the owning system uses.
    /// </summary>
    [Export]
    public AbilityTargetMode TargetMode { get; set; } =
        AbilityTargetMode.CurrentTarget;

    /// <summary>
    /// Controls range, measured as pixels.
    /// For example, changing 45 to 90 doubles the configured range.
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
    /// Controls base damage, measured as damage points.
    /// For example, changing 0 to 1 doubles the configured base damage.
    /// </summary>
    [Export(PropertyHint.Range, "0,1000000,1")]
    public float BaseDamage { get; set; } =
        0.0f;

    /// <summary>
    /// Controls base healing.
    /// For example, changing 0 to 1 doubles this setting's configured contribution to the system.
    /// </summary>
    [Export(PropertyHint.Range, "0,1000000,1")]
    public float BaseHealing { get; set; } =
        0.0f;

    /// <summary>
    /// Controls effect radius, measured as pixels.
    /// For example, changing 0 to 1 doubles the configured effect radius.
    /// </summary>
    [Export(PropertyHint.Range, "0,1000,1")]
    public float EffectRadius { get; set; } =
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

        if (Range < 0.0f)
        {
            errors.Add(
                $"{ContentId}: Range cannot be negative.");
        }

        if (BaseDamage < 0.0f)
        {
            errors.Add(
                $"{ContentId}: BaseDamage cannot be negative.");
        }

        if (BaseHealing < 0.0f)
        {
            errors.Add(
                $"{ContentId}: BaseHealing cannot be negative.");
        }

        if (EffectRadius < 0.0f)
        {
            errors.Add(
                $"{ContentId}: EffectRadius cannot be negative.");
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
            if (TargetMode != AbilityTargetMode.Self)
            {
                errors.Add(
                    $"{ContentId}: AreaTaunt abilities must use " +
                    "the Self target mode.");
            }

            if (EffectRadius <= 0.0f)
            {
                errors.Add(
                    $"{ContentId}: AreaTaunt abilities require an " +
                    "EffectRadius greater than zero.");
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
            if (TargetMode != AbilityTargetMode.LowestHealthAlly)
            {
                errors.Add(
                    $"{ContentId}: DirectHealing abilities must " +
                    "use the LowestHealthAlly target mode.");
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
                    $"{ContentId}: DamageOverTime abilities must " +
                    "use the CurrentTarget target mode.");
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
