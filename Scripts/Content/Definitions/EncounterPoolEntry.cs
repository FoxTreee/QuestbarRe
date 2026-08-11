using Godot;
using System.Collections.Generic;

[GlobalClass]
public partial class EncounterPoolEntry : Resource
{
    [ExportCategory("Encounter")]

    /// <summary>
    /// Stable content identifier for encounter; other systems use this value to find the same game data.
    /// For example, changing this ID makes the owning resource resolve a different registered encounter.
    /// </summary>
    [Export(
        PropertyHint.PlaceholderText,
        "encounter.core.training_mix")]
    public string EncounterContentId { get; set; } =
        string.Empty;

    [ExportCategory("Selection")]

    /// <summary>
    /// Controls weight, measured as a ratio or multiplier.
    /// For example, changing 1 to 2 doubles this setting's configured contribution to the system.
    /// </summary>
    [Export(PropertyHint.Range, "1,10000,1")]
    public int Weight { get; set; } = 1;

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

        if (!ContentId.IsValid(EncounterContentId))
        {
            errors.Add(
                $"Invalid encounter Content ID " +
                $"'{EncounterContentId}' in encounter pool.");
        }

        if (Weight <= 0)
        {
            errors.Add(
                $"{EncounterContentId}: Weight must be " +
                "greater than zero.");
        }

        return errors;
    }
}
