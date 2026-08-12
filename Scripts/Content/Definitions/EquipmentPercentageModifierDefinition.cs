using Godot;
using System.Collections.Generic;

[GlobalClass]
public partial class EquipmentPercentageModifierDefinition : Resource
{
    [ExportCategory("Identity")]

    /// <summary>
    /// Stable identifier for a percentage-based equipment effect. The effect's
    /// gameplay meaning is intentionally resolved elsewhere so equipment data
    /// does not own combat formulas.
    /// </summary>
    [Export(PropertyHint.PlaceholderText, "modifier.core.critical_strike")]
    public string ModifierContentId { get; set; } =
        string.Empty;


    [ExportCategory("Value")]

    /// <summary>
    /// Percentage value supplied by this equipment modifier. A value of 2
    /// represents +2%. No gameplay formula consumes this value until the
    /// corresponding modifier mechanic is implemented.
    /// </summary>
    [Export]
    public float PercentValue { get; set; } = 0.0f;


    public IReadOnlyList<string> GetValidationErrors()
    {
        List<string> errors = new();

        if (!global::ContentId.IsValid(ModifierContentId))
        {
            errors.Add(
                $"Invalid equipment modifier Content ID " +
                $"'{ModifierContentId}'.");
        }

        return errors;
    }
}
