using Godot;
using System.Collections.Generic;

[GlobalClass]
public partial class EncounterDefinition : Resource
{
    [ExportCategory("Identity")]

    /// <summary>
    /// Stable content identifier for content; other systems use this value to find the same game data.
    /// For example, changing this ID makes the owning resource resolve a different registered content.
    /// </summary>
    [Export(PropertyHint.PlaceholderText, "encounter.core.training_mix")]
    public string ContentId { get; set; } =
        string.Empty;

    /// <summary>
    /// Controls display name.
    /// For example, changing this text changes the name, message, key, or lookup value shown or consumed by the owning system.
    /// </summary>
    [Export(PropertyHint.PlaceholderText, "Training Mix")]
    public string DisplayName { get; set; } =
        "Unnamed Encounter";

    /// <summary>
    /// Controls description.
    /// For example, changing this text changes the name, message, key, or lookup value shown or consumed by the owning system.
    /// </summary>
    [Export(PropertyHint.MultilineText)]
    public string Description { get; set; } =
        string.Empty;

    [ExportCategory("Composition")]

    /// <summary>
    /// Controls monster composition, measured as pixels.
    /// For example, adding another entry gives the owning system one more configured monster composition to use.
    /// </summary>
    [Export]
    public Godot.Collections.Array<EncounterMonsterEntry>
        MonsterComposition
    { get; set; } = new();

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
                $"Invalid encounter Content ID '{ContentId}'. " +
                "Expected lowercase format such as " +
                "'encounter.core.training_mix'.");
        }

        if (string.IsNullOrWhiteSpace(DisplayName))
        {
            errors.Add(
                $"{ContentId}: DisplayName is required.");
        }

        if (MonsterComposition.Count == 0)
        {
            errors.Add(
                $"{ContentId}: MonsterComposition must " +
                "contain at least one entry.");

            return errors;
        }

        bool canSpawnAnyMonster = false;

        foreach (EncounterMonsterEntry entry in MonsterComposition)
        {
            if (!GodotObject.IsInstanceValid(entry))
            {
                errors.Add(
                    $"{ContentId}: MonsterComposition contains " +
                    "a missing entry.");

                continue;
            }

            foreach (string error in entry.GetValidationErrors())
            {
                errors.Add($"{ContentId}: {error}");
            }

            if (entry.MaximumCount > 0)
                canSpawnAnyMonster = true;
        }

        if (!canSpawnAnyMonster)
        {
            errors.Add(
                $"{ContentId}: MonsterComposition cannot " +
                "produce any monsters.");
        }

        return errors;
    }
}
