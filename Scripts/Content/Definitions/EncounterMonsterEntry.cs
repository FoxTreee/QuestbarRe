using Godot;
using System.Collections.Generic;

[GlobalClass]
public partial class EncounterMonsterEntry : Resource
{
    [Export]
    public string MonsterContentId { get; set; } =
        string.Empty;

    [Export(PropertyHint.Range, "0,100,1")]
    public int MinimumCount { get; set; } = 1;

    [Export(PropertyHint.Range, "0,100,1")]
    public int MaximumCount { get; set; } = 1;

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
