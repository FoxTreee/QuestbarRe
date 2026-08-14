using Godot;
using System;
using System.Collections.Generic;

/// <summary>
/// Connects two authored region-map destinations with a visually adjustable
/// Line2D. Marker2D children become ordered bend points that designers can
/// add, remove, and drag directly in Godot's 2D editor.
/// </summary>
[Tool]
[GlobalClass]
public partial class RegionMapPathController : Line2D
{
    [ExportCategory("Identity")]

    /// <summary>
    /// Stable identifier for this exact path. Moving either destination or a
    /// bend marker must never change it because later save data may remember
    /// whether this path has been discovered.
    /// </summary>
    [Export(PropertyHint.PlaceholderText,
        "map_path.stonebanner_highlands.start_to_horsethief")]
    public string PathContentId { get; set; } = string.Empty;

    [ExportCategory("Connections")]

    /// <summary>
    /// First destination connected by this path.
    /// </summary>
    [Export]
    public RegionMapNodeController StartNode { get; set; } = null!;

    /// <summary>
    /// Second destination connected by this path.
    /// </summary>
    [Export]
    public RegionMapNodeController EndNode { get; set; } = null!;

    [ExportCategory("Authoring")]

    /// <summary>
    /// Private designer notes that are never displayed to the player.
    /// </summary>
    [Export(PropertyHint.MultilineText)]
    public string DesignerNotes { get; set; } = string.Empty;

    /// <summary>
    /// Enables editor and runtime processing so the road follows node and bend
    /// positions immediately without requiring manual point editing.
    /// </summary>
    public override void _Ready()
    {
        SetProcess(true);
        RefreshPathPoints();
    }

    /// <summary>
    /// Refreshes only when generated points actually change, preventing the
    /// tool script from continuously rewriting an unchanged scene property.
    /// </summary>
    public override void _Process(double delta)
    {
        RefreshPathPoints();
    }

    /// <summary>
    /// Reports missing IDs and endpoint assignments without repairing or
    /// changing designer-authored map data.
    /// </summary>
    public IReadOnlyList<string> GetValidationErrors()
    {
        List<string> errors = new();

        if (!ContentId.IsValid(PathContentId))
        {
            errors.Add(
                $"Invalid map path Content ID '{PathContentId}'. " +
                "Expected lowercase format such as " +
                "'map_path.stonebanner_highlands.start_to_horsethief'.");
        }

        if (!GodotObject.IsInstanceValid(StartNode))
            errors.Add($"{PathContentId}: StartNode is required.");

        if (!GodotObject.IsInstanceValid(EndNode))
            errors.Add($"{PathContentId}: EndNode is required.");

        if (GodotObject.IsInstanceValid(StartNode)
            && StartNode == EndNode)
        {
            errors.Add(
                $"{PathContentId}: StartNode and EndNode must be different.");
        }

        return errors;
    }

    private void RefreshPathPoints()
    {
        if (!GodotObject.IsInstanceValid(StartNode)
            || !GodotObject.IsInstanceValid(EndNode))
        {
            ClearGeneratedPoints();
            return;
        }

        List<Vector2> generatedPoints = new()
        {
            ToLocal(StartNode.GetGlobalRect().GetCenter())
        };

        foreach (Node child in GetChildren())
        {
            if (child is Marker2D bendMarker)
                generatedPoints.Add(bendMarker.Position);
        }

        generatedPoints.Add(
            ToLocal(EndNode.GetGlobalRect().GetCenter()));

        if (!PointsMatch(generatedPoints))
            Points = generatedPoints.ToArray();
    }

    private void ClearGeneratedPoints()
    {
        if (Points.Length > 0)
            Points = Array.Empty<Vector2>();
    }

    private bool PointsMatch(IReadOnlyList<Vector2> generatedPoints)
    {
        if (Points.Length != generatedPoints.Count)
            return false;

        for (int index = 0; index < Points.Length; index++)
        {
            if (Points[index].DistanceSquaredTo(generatedPoints[index])
                > 0.0001f)
            {
                return false;
            }
        }

        return true;
    }
}
