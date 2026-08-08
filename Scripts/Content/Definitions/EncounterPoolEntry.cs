using Godot;
using System.Collections.Generic;

[GlobalClass]
public partial class EncounterPoolEntry : Resource
{
    [ExportCategory("Encounter")]

    [Export(
        PropertyHint.PlaceholderText,
        "encounter.core.training_mix")]
    public string EncounterContentId { get; set; } =
        string.Empty;

    [ExportCategory("Selection")]

    [Export(PropertyHint.Range, "1,10000,1")]
    public int Weight { get; set; } = 1;

    [ExportCategory("Authoring")]

    [Export(PropertyHint.MultilineText)]
    public string DesignerNotes { get; set; } =
        string.Empty;

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
