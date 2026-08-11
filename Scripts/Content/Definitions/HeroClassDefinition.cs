using Godot;
using System;
using System.Collections.Generic;

[GlobalClass]
public partial class HeroClassDefinition : Resource
{
    [ExportCategory("Identity")]

    /// <summary>
    /// Stable content identifier for content; other systems use this value to find the same game data.
    /// For example, changing this ID makes the owning resource resolve a different registered content.
    /// </summary>
    [Export(PropertyHint.PlaceholderText, "class.core.warrior")]
    public string ContentId { get; set; } =
        string.Empty;

    /// <summary>
    /// Controls display name.
    /// For example, changing this text changes the name, message, key, or lookup value shown or consumed by the owning system.
    /// </summary>
    [Export(PropertyHint.PlaceholderText, "Warrior")]
    public string DisplayName { get; set; } =
        "Unnamed Class";


    [ExportCategory("Resource")]

    /// <summary>
    /// Defines the class's combat resource, capacity, starting amount, and
    /// regeneration. Leave empty for classes that do not use a resource.
    /// </summary>
    [Export]
    public HeroResourceDefinition? ResourceDefinition
    { get; set; }


    [ExportCategory("Abilities")]

    /// <summary>
    /// Stable content identifier for abilitys; other systems use this value to find the same game data.
    /// For example, changing this ID makes the owning resource resolve a different registered abilitys.
    /// </summary>
    [Export]
    public Godot.Collections.Array<string> AbilityContentIds
    { get; set; } = new();


    /// <summary>
    /// Retrieves validation errors from the current game state.
    /// Reads the current state and returns the resulting i read only list string to the caller.
    /// </summary>
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

        if (GodotObject.IsInstanceValid(ResourceDefinition))
        {
            foreach (string resourceError
                in ResourceDefinition!.GetValidationErrors())
            {
                errors.Add(
                    $"{ContentId}: invalid resource definition: " +
                    resourceError);
            }
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
