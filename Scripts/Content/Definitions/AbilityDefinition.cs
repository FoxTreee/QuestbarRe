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

    [Export(PropertyHint.Range, "0.1,120,0.1")]
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

    [Export(PropertyHint.Range, "0,1000000,1")]
    public float BaseDamage { get; set; } =
        0.0f;

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

        if (CooldownSeconds <= 0.0f)
        {
            errors.Add(
                $"{ContentId}: CooldownSeconds must be " +
                "greater than zero.");
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

        return errors;
    }
}
