using Godot;
using System.Collections.Generic;

[GlobalClass]
public partial class EncounterDefinition : Resource
{
    [ExportCategory("Identity")]

    [Export]
    public string ContentId { get; set; } =
        string.Empty;

    [Export]
    public string DisplayName { get; set; } =
        "Unnamed Encounter";

    [ExportCategory("Composition")]

    [Export]
    public Godot.Collections.Array<EncounterMonsterEntry>
        MonsterComposition
    { get; set; } = new();

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
