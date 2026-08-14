using Godot;
using System;
using System.Collections.Generic;

/// <summary>
/// Presents saved region exploration on the current map. It reveals authored
/// destination buttons and sends editor-placed reveal areas to the fog shader;
/// it never owns or modifies the underlying map artwork.
/// </summary>
public partial class RegionMapExplorationController : Node
{
    private const int MaximumRevealAreaCount = 32;

    [ExportCategory("Dependencies")]

    [Export]
    public RegionExplorationService Exploration { get; set; } = null!;

    [Export]
    public RegionRunController RegionRun { get; set; } = null!;

    [Export]
    public ColorRect FogOverlay { get; set; } = null!;

    [Export]
    public Control DestinationLayer { get; set; } = null!;

    [Export]
    public Node RevealAreasRoot { get; set; } = null!;

    public event Action<RegionMapNodeController>? DestinationDiscovered;

    private readonly List<RegionMapNodeController> _destinations = new();
    private readonly List<RegionMapFogRevealArea> _revealAreas = new();
    private readonly HashSet<string> _revealedDestinationIds =
        new(StringComparer.OrdinalIgnoreCase);

    private ShaderMaterial? _fogMaterial;
    private ulong _lastExplorationRevision = ulong.MaxValue;
    private Vector2 _lastFogSize = Vector2.Zero;
    private string _lastRegionContentId = string.Empty;
    private bool _initialStateApplied;
    private bool _warnedAboutRevealAreaLimit;

    /// <summary>
    /// Discovers authored children, duplicates the material for this map only,
    /// and applies the initial saved exploration state.
    /// </summary>
    public override void _Ready()
    {
        if (!ValidateReferences())
        {
            SetProcess(false);
            return;
        }

        if (FogOverlay.Material is not ShaderMaterial sourceMaterial)
        {
            GD.PushError(
                "RegionMapExplorationController requires a ShaderMaterial " +
                "on FogOverlay.");
            SetProcess(false);
            return;
        }

        _fogMaterial = (ShaderMaterial)sourceMaterial.Duplicate();
        FogOverlay.Material = _fogMaterial;

        DiscoverDescendants(DestinationLayer, _destinations);
        DiscoverDescendants(RevealAreasRoot, _revealAreas);

        _revealAreas.Sort(
            (left, right) => left.RevealAtTravelSeconds.CompareTo(
                right.RevealAtTravelSeconds));

        RefreshPresentation(force: true);
        SetProcess(true);
    }

    /// <summary>
    /// Rebuilds only when saved progress, the active region, or map size has
    /// changed. Growing reveal areas still update smoothly because travel time
    /// increments the exploration revision while Traveling.
    /// </summary>
    public override void _Process(double delta)
    {
        RefreshPresentation(force: false);
    }

    private void RefreshPresentation(bool force)
    {
        RegionDefinition? region = GetActiveRegion();

        if (region is null || _fogMaterial is null)
            return;

        if (!force
            && _lastExplorationRevision == Exploration.Revision
            && _lastFogSize.IsEqualApprox(FogOverlay.Size)
            && _lastRegionContentId.Equals(
                region.ContentId,
                StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        _lastExplorationRevision = Exploration.Revision;
        _lastFogSize = FogOverlay.Size;
        _lastRegionContentId = region.ContentId;

        double travelSeconds = Exploration.GetActiveRegionTravelSeconds();
        ApplyDestinationVisibility(travelSeconds);
        ApplyFog(travelSeconds, Exploration.GetActiveRegionProgress());
        _initialStateApplied = true;
    }

    private void ApplyDestinationVisibility(double travelSeconds)
    {
        foreach (RegionMapNodeController destination in _destinations)
        {
            if (!GodotObject.IsInstanceValid(destination))
                continue;

            bool shouldReveal =
                travelSeconds >= destination.RevealAtTravelSeconds;

            destination.Visible = shouldReveal;
            destination.Disabled = !shouldReveal;

            if (!shouldReveal
                || string.IsNullOrWhiteSpace(destination.NodeContentId)
                || !_revealedDestinationIds.Add(
                    destination.NodeContentId))
            {
                continue;
            }

            if (_initialStateApplied)
            {
                DebugLog.Print(
                    $"New destination discovered: " +
                    $"{destination.DisplayName} " +
                    $"({destination.NodeContentId}).");

                DestinationDiscovered?.Invoke(destination);
            }
        }
    }

    private void ApplyFog(double travelSeconds, float progress)
    {
        if (progress >= 1.0f)
        {
            FogOverlay.Hide();
            return;
        }

        FogOverlay.Show();

        Vector2 mapSize = FogOverlay.Size;

        if (mapSize.X <= 0.0f || mapSize.Y <= 0.0f)
            return;

        Vector2[] centers = new Vector2[MaximumRevealAreaCount];
        float[] radii = new float[MaximumRevealAreaCount];
        float[] feathers = new float[MaximumRevealAreaCount];
        int revealCount = 0;

        Transform2D toFogLocal =
            FogOverlay.GetGlobalTransform().AffineInverse();

        foreach (RegionMapFogRevealArea area in _revealAreas)
        {
            if (!GodotObject.IsInstanceValid(area))
                continue;

            float currentRadius = area.GetCurrentRadius(travelSeconds);

            if (currentRadius <= 0.0f)
                continue;

            if (revealCount >= MaximumRevealAreaCount)
            {
                if (!_warnedAboutRevealAreaLimit)
                {
                    _warnedAboutRevealAreaLimit = true;
                    GD.PushWarning(
                        $"Region map supports the first " +
                        $"{MaximumRevealAreaCount} active fog reveal areas.");
                }

                break;
            }

            Vector2 fogLocalCenter =
                toFogLocal * area.GlobalPosition;

            centers[revealCount] = new Vector2(
                fogLocalCenter.X / mapSize.X,
                fogLocalCenter.Y / mapSize.Y);
            radii[revealCount] = currentRadius;
            feathers[revealCount] = area.FeatherPixels;
            revealCount++;
        }

        _fogMaterial!.SetShaderParameter("map_size", mapSize);
        _fogMaterial.SetShaderParameter("reveal_count", revealCount);
        _fogMaterial.SetShaderParameter("reveal_centers", centers);
        _fogMaterial.SetShaderParameter("reveal_radii", radii);
        _fogMaterial.SetShaderParameter("reveal_feathers", feathers);
    }

    private RegionDefinition? GetActiveRegion()
    {
        return GodotObject.IsInstanceValid(RegionRun.ActiveRegion)
            ? RegionRun.ActiveRegion
            : null;
    }

    private static void DiscoverDescendants<T>(Node root, List<T> results)
        where T : Node
    {
        foreach (Node child in root.GetChildren())
        {
            if (child is T match)
                results.Add(match);

            DiscoverDescendants(child, results);
        }
    }

    private bool ValidateReferences()
    {
        bool valid = true;
        valid &= Require(Exploration, nameof(Exploration));
        valid &= Require(RegionRun, nameof(RegionRun));
        valid &= Require(FogOverlay, nameof(FogOverlay));
        valid &= Require(DestinationLayer, nameof(DestinationLayer));
        valid &= Require(RevealAreasRoot, nameof(RevealAreasRoot));
        return valid;
    }

    private static bool Require(GodotObject value, string propertyName)
    {
        if (GodotObject.IsInstanceValid(value))
            return true;

        GD.PushError(
            $"RegionMapExplorationController is missing " +
            $"'{propertyName}'.");
        return false;
    }
}
