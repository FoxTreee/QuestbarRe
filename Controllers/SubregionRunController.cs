using Godot;
using System;

/// <summary>
/// Bridges region-map destination state to subregion presentation. Encounter
/// waves are intentionally excluded until the enter/retreat loop is proven.
/// </summary>
public partial class SubregionRunController : Node
{
    [ExportCategory("Dependencies")]

    [Export]
    public SubregionContentRegistry ContentRegistry { get; set; } = null!;

    [Export]
    public RegionMapExplorationController RegionMap { get; set; } = null!;

    [Export]
    public RegionRunController RegionRun { get; set; } = null!;

    [Export]
    public RegionPresentationController RegionPresentation
    { get; set; } = null!;

    public SubregionDefinition? ActiveSubregion { get; private set; }

    /// <summary>
    /// Subscribes after validating the four shared runtime dependencies.
    /// </summary>
    public override void _Ready()
    {
        if (!ValidateReferences())
            return;

        RegionMap.DestinationEntered += OnDestinationEntered;
        RegionMap.DestinationRetreated += OnDestinationRetreated;
    }

    /// <summary>
    /// Detaches map events owned by this controller.
    /// </summary>
    public override void _ExitTree()
    {
        if (!GodotObject.IsInstanceValid(RegionMap))
            return;

        RegionMap.DestinationEntered -= OnDestinationEntered;
        RegionMap.DestinationRetreated -= OnDestinationRetreated;
    }

    private void OnDestinationEntered(
        RegionMapNodeController destination)
    {
        if (destination.NodeType != RegionMapNodeType.Subregion)
            return;

        if (!ContentRegistry.TryGet(
            destination.DestinationContentId,
            out SubregionDefinition subregion))
        {
            RejectEntry(
                $"No valid SubregionDefinition is registered for " +
                $"'{destination.DestinationContentId}'.");
            return;
        }

        if (!subregion.ParentRegionContentId.Equals(
            RegionRun.ActiveRegion.ContentId,
            StringComparison.OrdinalIgnoreCase))
        {
            RejectEntry(
                $"Subregion '{subregion.ContentId}' belongs to " +
                $"'{subregion.ParentRegionContentId}', not active region " +
                $"'{RegionRun.ActiveRegion.ContentId}'.");
            return;
        }

        ActiveSubregion = subregion;
        RegionPresentation.ApplySubregion(subregion);

        DebugLog.Print(
            $"Subregion entered: {subregion.DisplayName} " +
            $"({subregion.ContentId}). Main-region run paused.");
    }

    private void OnDestinationRetreated(
        RegionMapNodeController destination)
    {
        ActiveSubregion = null;
        RegionPresentation.ApplyRegion(RegionRun.ActiveRegion);

        DebugLog.Print(
            $"Returned to region: {RegionRun.ActiveRegion.DisplayName} " +
            $"({RegionRun.ActiveRegion.ContentId}). Main-region run resumed.");
    }

    private void RejectEntry(string message)
    {
        GD.PushError(message);
        DebugLog.Print($"Subregion entry rejected: {message}");
        RegionMap.RetreatActiveDestination();
    }

    private bool ValidateReferences()
    {
        bool valid = true;
        valid &= Require(ContentRegistry, nameof(ContentRegistry));
        valid &= Require(RegionMap, nameof(RegionMap));
        valid &= Require(RegionRun, nameof(RegionRun));
        valid &= Require(RegionPresentation, nameof(RegionPresentation));
        return valid;
    }

    private static bool Require(GodotObject value, string propertyName)
    {
        if (GodotObject.IsInstanceValid(value))
            return true;

        GD.PushError(
            $"SubregionRunController is missing the Inspector reference " +
            $"'{propertyName}'.");
        return false;
    }
}
