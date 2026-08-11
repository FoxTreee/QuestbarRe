using Godot;
using System.Collections.Generic;

[GlobalClass]
public partial class EncounterMonsterEntry : Resource
{
    [ExportCategory("Monster")]

    /// <summary>
    /// Stable content identifier for monster; other systems use this value to find the same game data.
    /// For example, changing this ID makes the owning resource resolve a different registered monster.
    /// </summary>
    [Export(
        PropertyHint.PlaceholderText,
        "monster.core.training_monster")]
    public string MonsterContentId { get; set; } =
        string.Empty;

    [ExportCategory("Count Range")]

    /// <summary>
    /// Controls minimum count, measured as a count.
    /// For example, changing 1 to 2 doubles the configured minimum count.
    /// </summary>
    [Export(PropertyHint.Range, "0,100,1")]
    public int MinimumCount { get; set; } = 1;

    /// <summary>
    /// Controls maximum count, measured as a count.
    /// For example, changing 1 to 2 doubles the configured maximum count.
    /// </summary>
    [Export(PropertyHint.Range, "0,100,1")]
    public int MaximumCount { get; set; } = 1;

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
