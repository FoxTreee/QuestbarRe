using Godot;
using System;
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

    /// <summary>
    /// Raised when this discovered map node is clicked. The map controller
    /// decides whether that click enters or retreats from the destination.
    /// </summary>
    public event Action<RegionMapNodeController>? ActionPressed;
    public event Action<RegionMapNodeController>? HoverStarted;
    public event Action<RegionMapNodeController>? HoverEnded;
    public event Action<RegionMapNodeController>? ActionTooltipChanged;

    /// <summary>
    /// Plain action text rendered by the existing custom item-tooltip panel.
    /// The node itself never changes its authored text or icon.
    /// </summary>
    public string ActionTooltipText { get; private set; } = string.Empty;

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
        set => _displayName = value;
    }

    [ExportCategory("Destination")]

    /// <summary>
    /// Selects the destination category without controlling its artwork.
    /// </summary>
    [Export]
    public RegionMapNodeType NodeType
    {
        get => _nodeType;
        set => _nodeType = value;
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
    /// Reconnects hover and click input whenever popup formation reparents the
    /// map into another viewport, exactly like the existing item-slot scene.
    /// </summary>
    public override void _EnterTree()
    {
        TooltipText = string.Empty;
        Pressed += OnPressed;
        MouseEntered += OnMouseEntered;
        MouseExited += OnMouseExited;
    }

    /// <summary>
    /// Forces map nodes to use custom-tooltip input and never Godot's default
    /// tooltip presentation, including while the scene is being authored.
    /// </summary>
    public override void _Ready()
    {
        TooltipText = string.Empty;
        MouseFilter = MouseFilterEnum.Stop;
    }

    /// <summary>
    /// Disconnects the runtime button event when the map leaves the tree.
    /// </summary>
    public override void _ExitTree()
    {
        Pressed -= OnPressed;
        MouseEntered -= OnMouseEntered;
        MouseExited -= OnMouseExited;
    }

    /// <summary>
    /// Restores the node's authored appearance and presents only the action
    /// instruction requested for an available destination.
    /// </summary>
    public void ApplyEnterActionPresentation()
    {
        SetActionTooltipText("CLICK TO ENTER");
    }

    /// <summary>
    /// Turns this same map node into the return action for its parent region.
    /// </summary>
    public void ApplyRetreatActionPresentation(string mainRegionName)
    {
        SetActionTooltipText(
            $"RETREAT\nRETURN TO {mainRegionName.ToUpperInvariant()}.");
    }

    /// <summary>
    /// Removes action text from nodes that cannot currently be selected.
    /// </summary>
    public void ApplyUnavailableActionPresentation()
    {
        SetActionTooltipText(string.Empty);
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

    private void OnPressed()
    {
        ActionPressed?.Invoke(this);
    }

    private void OnMouseEntered()
    {
        HoverStarted?.Invoke(this);
    }

    private void OnMouseExited()
    {
        HoverEnded?.Invoke(this);
    }

    private void SetActionTooltipText(string value)
    {
        TooltipText = string.Empty;

        if (ActionTooltipText == value)
            return;

        ActionTooltipText = value;
        ActionTooltipChanged?.Invoke(this);
    }
}
