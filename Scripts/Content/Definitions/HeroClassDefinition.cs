using Godot;
using System;
using System.Collections.Generic;

[GlobalClass]
public partial class HeroClassDefinition : Resource
{
    [ExportCategory("Identity")]

    [Export(PropertyHint.PlaceholderText, "class.core.warrior")]
    public string ContentId { get; set; } =
        string.Empty;

    [Export(PropertyHint.PlaceholderText, "Warrior")]
    public string DisplayName { get; set; } =
        "Unnamed Class";


    [ExportCategory("Abilities")]

    [Export]
    public Godot.Collections.Array<string> AbilityContentIds
    { get; set; } = new();


    public IReadOnlyList<string> GetValidationErrors()
    {
        List<string> errors = new();

        if (!global::ContentId.IsValid(ContentId)
            || !ContentId.StartsWith(
                "class.",
                StringComparison.Ordinal))
        {
            errors.Add(
                $"Invalid class Content ID '{ContentId}'. " +
                "Expected lowercase format such as " +
                "'class.core.warrior'.");
        }

        if (string.IsNullOrWhiteSpace(DisplayName))
        {
            errors.Add(
                $"{ContentId}: DisplayName is required.");
        }

        HashSet<string> seenAbilityIds =
            new(StringComparer.OrdinalIgnoreCase);

        foreach (string abilityContentId in AbilityContentIds)
        {
            if (!global::ContentId.IsValid(abilityContentId))
            {
                errors.Add(
                    $"{ContentId}: invalid ability Content ID " +
                    $"'{abilityContentId}'.");

                continue;
            }

            if (!seenAbilityIds.Add(abilityContentId.Trim()))
            {
                errors.Add(
                    $"{ContentId}: duplicate ability Content ID " +
                    $"'{abilityContentId}'.");
            }
        }

        return errors;
    }
}
