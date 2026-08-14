using Godot;
using System.Collections.Generic;

/// <summary>
/// Authored content for a focused location entered from a region map. A
/// subregion owns its environment artwork while its encounter run will be
/// attached in the next checkpoint.
/// </summary>
[GlobalClass]
public partial class SubregionDefinition : Resource
{
    [ExportCategory("Identity")]

    /// <summary>
    /// Stable ID used by a map node's DestinationContentId.
    /// </summary>
    [Export(PropertyHint.PlaceholderText,
        "subregion.stonebanner_highlands.horsethief_hideout")]
    public string ContentId { get; set; } = string.Empty;

    /// <summary>
    /// Stable ID of the region that contains this subregion.
    /// </summary>
    [Export(PropertyHint.PlaceholderText,
        "region.core.training_region")]
    public string ParentRegionContentId { get; set; } = string.Empty;

    /// <summary>
    /// Player-facing location name used by logs and future run UI.
    /// </summary>
    [Export(PropertyHint.PlaceholderText, "Horsethief Hideout")]
    public string DisplayName { get; set; } = "Unnamed Subregion";

    /// <summary>
    /// Optional authoring description. Map-node tooltips deliberately do not
    /// expose this text to the player.
    /// </summary>
    [Export(PropertyHint.MultilineText)]
    public string Description { get; set; } = string.Empty;

    [ExportCategory("Presentation")]

    /// <summary>
    /// Background displayed while the party is inside this subregion.
    /// </summary>
    [Export]
    public Texture2D BackgroundTexture { get; set; } = null!;

    /// <summary>
    /// Repeating ground displayed while the party is inside this subregion.
    /// </summary>
    [Export]
    public Texture2D GroundTexture { get; set; } = null!;

    [ExportCategory("Authoring")]

    /// <summary>
    /// Private notes that are never displayed to the player.
    /// </summary>
    [Export(PropertyHint.MultilineText)]
    public string DesignerNotes { get; set; } = string.Empty;

    /// <summary>
    /// Reports incomplete or invalid authored content without repairing it.
    /// </summary>
    public IReadOnlyList<string> GetValidationErrors()
    {
        List<string> errors = new();

        if (!global::ContentId.IsValid(ContentId))
        {
            errors.Add(
                $"Invalid subregion Content ID '{ContentId}'. " +
                "Expected lowercase format such as " +
                "'subregion.stonebanner_highlands.horsethief_hideout'.");
        }

        if (!global::ContentId.IsValid(ParentRegionContentId))
        {
            errors.Add(
                $"{ContentId}: invalid parent region Content ID " +
                $"'{ParentRegionContentId}'.");
        }

        if (string.IsNullOrWhiteSpace(DisplayName))
            errors.Add($"{ContentId}: DisplayName is required.");

        if (!GodotObject.IsInstanceValid(BackgroundTexture))
            errors.Add($"{ContentId}: BackgroundTexture is required.");

        if (!GodotObject.IsInstanceValid(GroundTexture))
            errors.Add($"{ContentId}: GroundTexture is required.");

        return errors;
    }
}
