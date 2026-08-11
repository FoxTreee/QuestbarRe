using Godot;
using System.Collections.Generic;

[GlobalClass]
public partial class AbilityDefinition : Resource
{
    [ExportCategory("Identity")]

    [Export(PropertyHint.PlaceholderText, "ability.core.heavy_slam")]
    public string ContentId { get; set; } =
        string.Empty;

    [Export(PropertyHint.PlaceholderText, "Heavy Slam")]
    public string DisplayName { get; set; } =
        "Unnamed Ability";

    [Export(PropertyHint.MultilineText)]
    public string Description { get; set; } =
        string.Empty;

    [ExportCategory("Timing")]

    [Export(PropertyHint.Range, "0,120,0.1")]
    public float CooldownSeconds { get; set; } =
        6.0f;

    [Export(PropertyHint.Range, "0,10,0.05")]
    public float CastTimeSeconds { get; set; } =
        1.0f;

    [ExportCategory("Targeting")]

    [Export]
    public AbilityTargetMode TargetMode { get; set; } =
        AbilityTargetMode.CurrentTarget;

    [Export(PropertyHint.Range, "0,500,1")]
    public float Range { get; set; } =
        45.0f;

    [ExportCategory("Effect")]

    [Export]
    public AbilityEffectType EffectType { get; set; } =
        AbilityEffectType.DirectDamage;

    [Export(PropertyHint.Range, "0,1000000,1")]
    public float BaseDamage { get; set; } =
        0.0f;

    [Export(PropertyHint.Range, "0,1000000,1")]
    public float BaseHealing { get; set; } =
        0.0f;

    [Export(PropertyHint.Range, "0,1000,1")]
    public float EffectRadius { get; set; } =
        0.0f;

    [Export(PropertyHint.Range, "0,60,0.1")]
    public float EffectDurationSeconds { get; set; } =
        0.0f;

    [Export(PropertyHint.Range, "0.05,60,0.05")]
    public float EffectTickIntervalSeconds { get; set; } =
        1.0f;

    [ExportCategory("Automatic Use")]

    [Export(PropertyHint.Range, "0,30,0.1")]
    public float AutoCastDelaySeconds { get; set; } =
        0.0f;

    [Export(PropertyHint.Range, "1,100,1")]
    public float AutoCastHealthThresholdPercent { get; set; } =
        50.0f;

    [ExportCategory("Authoring")]

    [Export(PropertyHint.MultilineText)]
    public string DesignerNotes { get; set; } =
        string.Empty;

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
