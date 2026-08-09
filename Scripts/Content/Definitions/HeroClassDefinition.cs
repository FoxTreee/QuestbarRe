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

        return errors;
    }
}
