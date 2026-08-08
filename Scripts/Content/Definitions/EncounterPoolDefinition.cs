using Godot;
using System;
using System.Collections.Generic;

[GlobalClass]
public partial class EncounterPoolDefinition : Resource
{
    [ExportCategory("Identity")]

    [Export(
        PropertyHint.PlaceholderText,
        "encounter_pool.core.training_region")]
    public string ContentId { get; set; } =
        string.Empty;

    [Export(PropertyHint.PlaceholderText, "Training Region")]
    public string DisplayName { get; set; } =
        "Unnamed Encounter Pool";

    [Export(PropertyHint.MultilineText)]
    public string Description { get; set; } =
        string.Empty;

    [ExportCategory("Encounter Selection")]

    [Export]
    public Godot.Collections.Array<EncounterPoolEntry>
        Entries
    { get; set; } = new();

    [ExportCategory("Authoring")]

    [Export(PropertyHint.MultilineText)]
    public string DesignerNotes { get; set; } =
        string.Empty;

    public int GetTotalWeight()
    {
        int totalWeight = 0;

        foreach (EncounterPoolEntry entry in Entries)
        {
            if (!GodotObject.IsInstanceValid(entry))
                continue;

            totalWeight += Math.Max(entry.Weight, 0);
        }

        return totalWeight;
    }

    public IReadOnlyList<string> GetValidationErrors()
    {
        List<string> errors = new();

        if (!global::ContentId.IsValid(ContentId))
        {
            errors.Add(
                $"Invalid encounter pool Content ID '{ContentId}'. " +
                "Expected lowercase format such as " +
                "'encounter_pool.core.training_region'.");
        }

        if (string.IsNullOrWhiteSpace(DisplayName))
        {
            errors.Add(
                $"{ContentId}: DisplayName is required.");
        }

        if (Entries.Count == 0)
        {
            errors.Add(
                $"{ContentId}: Entries must contain " +
                "at least one encounter.");

            return errors;
        }

        HashSet<string> seenEncounterIds =
            new(StringComparer.OrdinalIgnoreCase);

        foreach (EncounterPoolEntry entry in Entries)
        {
            if (!GodotObject.IsInstanceValid(entry))
            {
                errors.Add(
                    $"{ContentId}: Entries contains " +
                    "a missing entry.");

                continue;
            }

            foreach (string error in entry.GetValidationErrors())
            {
                errors.Add($"{ContentId}: {error}");
            }

            if (!string.IsNullOrWhiteSpace(
                entry.EncounterContentId)
                && !seenEncounterIds.Add(
                    entry.EncounterContentId.Trim()))
            {
                errors.Add(
                    $"{ContentId}: duplicate encounter " +
                    $"Content ID '{entry.EncounterContentId}'.");
            }
        }

        if (GetTotalWeight() <= 0)
        {
            errors.Add(
                $"{ContentId}: total encounter weight " +
                "must be greater than zero.");
        }

        return errors;
    }
}
