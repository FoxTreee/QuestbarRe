using Godot;
using System.Collections.Generic;

[GlobalClass]
public partial class EncounterMonsterEntry : Resource
{
    [ExportCategory("Monster")]

    [Export(
        PropertyHint.PlaceholderText,
        "monster.core.training_monster")]
    public string MonsterContentId { get; set; } =
        string.Empty;

    [ExportCategory("Count Range")]

    [Export(PropertyHint.Range, "0,100,1")]
    public int MinimumCount { get; set; } = 1;

    [Export(PropertyHint.Range, "0,100,1")]
    public int MaximumCount { get; set; } = 1;

    [ExportCategory("Authoring")]

    [Export(PropertyHint.MultilineText)]
    public string DesignerNotes { get; set; } =
        string.Empty;

    public IReadOnlyList<string> GetValidationErrors()
    {
        List<string> errors = new();

        if (!ContentId.IsValid(MonsterContentId))
        {
            errors.Add(
                $"Invalid monster Content ID " +
                $"'{MonsterContentId}' in encounter composition.");
        }

        if (MinimumCount < 0)
        {
            errors.Add(
                $"{MonsterContentId}: MinimumCount cannot " +
                "be negative.");
        }

        if (MaximumCount < MinimumCount)
        {
            errors.Add(
                $"{MonsterContentId}: MaximumCount must be " +
                "greater than or equal to MinimumCount.");
        }

        return errors;
    }
}
