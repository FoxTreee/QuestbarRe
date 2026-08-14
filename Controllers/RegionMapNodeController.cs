using Godot;
using System.Collections.Generic;

/// <summary>
/// Reusable destination placed visually on a fixed-size region map. Designers
/// move the Button directly in Godot's 2D editor while this component stores
/// the stable identity and destination metadata used by later map systems.
/// </summary>
[Tool]
[GlobalClass]
public partial class RegionMapNodeController : Button
{
    private string _displayName = "Unnamed Destination";
    private RegionMapNodeType _nodeType = RegionMapNodeType.Subregion;

    [ExportCategory("Identity")]

    /// <summary>
    /// Stable identifier for this exact location on this exact region map.
    /// Moving the node must never change this value because save data will use
    /// it to remember discovery and completion state.
    /// </summary>
    [Export(PropertyHint.PlaceholderText,
        "map_node.stonebanner_highlands.horsethief_hideout")]
    public string NodeContentId { get; set; } = string.Empty;

    /// <summary>
    /// Player-facing destination name shown after this node is discovered.
    /// </summary>
    [Export(PropertyHint.PlaceholderText, "Horsethief Hideout")]
    public string DisplayName
    {
        get => _displayName;
        set
        {
            _displayName = value;
            RefreshAuthoringPreview();
        }
    }

    [ExportCategory("Destination")]

    /// <summary>
    /// Selects the destination category without controlling its artwork.
    /// </summary>
    [Export]
    public RegionMapNodeType NodeType
    {
        get => _nodeType;
        set
        {
            _nodeType = value;
            RefreshAuthoringPreview();
        }
    }

    /// <summary>
    /// Stable ID of the subregion, dungeon, town, or connected region entered
    /// through this node. Starting locations may leave this empty.
    /// </summary>
    [Export(PropertyHint.PlaceholderText,
        "subregion.stonebanner_highlands.horsethief_hideout")]
    public string DestinationContentId { get; set; } = string.Empty;

    [ExportCategory("Exploration")]

    /// <summary>
    /// Traveling-state seconds required before this destination is revealed
    /// and becomes clickable. Zero keeps starting locations visible at once.
    /// </summary>
    [Export(PropertyHint.Range, "0,86400,1,suffix:s")]
    public float RevealAtTravelSeconds { get; set; } = 0.0f;

    [ExportCategory("Authoring")]

    /// <summary>
    /// Private designer notes that are never displayed to the player.
    /// </summary>
    [Export(PropertyHint.MultilineText)]
    public string DesignerNotes { get; set; } = string.Empty;

    /// <summary>
    /// Applies a useful editor and runtime tooltip without replacing the
    /// Button's manually authored text, icon, size, or style.
    /// </summary>
    public override void _Ready()
    {
        RefreshAuthoringPreview();
    }

    /// <summary>
    /// Returns the node center in its parent map layer. Later road and fog
    /// systems can connect to this point while designers freely move the node.
    /// </summary>
    public Vector2 GetMapCenter()
    {
        return Position + (Size * 0.5f);
    }

    /// <summary>
    /// Reports authoring mistakes without changing or repairing designer data.
    /// </summary>
    public IReadOnlyList<string> GetValidationErrors()
    {
        List<string> errors = new();

        if (!ContentId.IsValid(NodeContentId))
        {
            errors.Add(
                $"Invalid map node Content ID '{NodeContentId}'. " +
                "Expected lowercase format such as " +
                "'map_node.stonebanner_highlands.horsethief_hideout'.");
        }

        if (string.IsNullOrWhiteSpace(DisplayName))
            errors.Add($"{NodeContentId}: DisplayName is required.");

        if (NodeType != RegionMapNodeType.StartingLocation
            && !ContentId.IsValid(DestinationContentId))
        {
            errors.Add(
                $"{NodeContentId}: invalid destination Content ID " +
                $"'{DestinationContentId}'.");
        }

        return errors;
    }

    private void RefreshAuthoringPreview()
    {
        TooltipText = string.IsNullOrWhiteSpace(DisplayName)
            ? NodeType.ToString()
            : $"{DisplayName} ({NodeType})";
    }
}
