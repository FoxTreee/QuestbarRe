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
    public event Action<RegionMapNodeController>? DestinationEntered;
    public event Action<RegionMapNodeController>? DestinationRetreated;

    public RegionMapNodeController? ActiveDestination { get; private set; }

    private readonly List<RegionMapNodeController> _destinations = new();
    private readonly List<RegionMapFogRevealArea> _revealAreas = new();
    private readonly HashSet<string> _revealedDestinationIds =
        new(StringComparer.OrdinalIgnoreCase);

    private ShaderMaterial? _fogMaterial;
    private ulong _lastExplorationRevision = ulong.MaxValue;
    private Vector2 _lastFogSize = Vector2.Zero;
    private string _lastRegionContentId = string.Empty;
    private JourneyStateService.JourneyState? _lastJourneyState;
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

        if (_destinations.Count == 0)
        {
            GD.PushWarning(
                "RegionMapExplorationController found no " +
                "RegionMapNodeController descendants under DestinationLayer.");
        }

        foreach (RegionMapNodeController destination in _destinations)
        {
            destination.ActionPressed += OnDestinationActionPressed;

            if (destination.NodeType == RegionMapNodeType.Graveyard
                && (!GodotObject.IsInstanceValid(RegionRun.ActiveRegion)
                    || !RegionRun.ActiveRegion.TryGetGraveyard(
                        destination.DestinationContentId,
                        out _)))
            {
                GD.PushError(
                    $"Graveyard map node '{destination.NodeContentId}' " +
                    $"references unknown regional graveyard " +
                    $"'{destination.DestinationContentId}'.");
            }
        }

        _revealAreas.Sort(
            (left, right) => left.RevealAtTravelSeconds.CompareTo(
                right.RevealAtTravelSeconds));

        RefreshPresentation(force: true);
        SetProcess(true);
    }

    /// <summary>
    /// Disconnects authored map nodes and guarantees regional exploration is
    /// not left paused if this presenter is removed while a destination is active.
    /// </summary>
    public override void _ExitTree()
    {
        foreach (RegionMapNodeController destination in _destinations)
        {
            if (GodotObject.IsInstanceValid(destination))
                destination.ActionPressed -= OnDestinationActionPressed;
        }

        if (GodotObject.IsInstanceValid(Exploration))
            Exploration.SetDestinationExcursionActive(false);

        if (GodotObject.IsInstanceValid(RegionRun))
            RegionRun.SetDestinationExcursionActive(false);
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

        JourneyStateService.JourneyState journeyState =
            RegionRun.JourneyState.CurrentState;

        if (!force
            && _lastExplorationRevision == Exploration.Revision
            && _lastFogSize.IsEqualApprox(FogOverlay.Size)
            && _lastJourneyState == journeyState
            && _lastRegionContentId.Equals(
                region.ContentId,
                StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        _lastExplorationRevision = Exploration.Revision;
        _lastFogSize = FogOverlay.Size;
        _lastRegionContentId = region.ContentId;
        _lastJourneyState = journeyState;

        double travelSeconds = Exploration.GetActiveRegionTravelSeconds();
        float progress = Exploration.GetActiveRegionProgress();
        ApplyDestinationVisibility(
            region,
            travelSeconds,
            progress >= 1.0f);
        ApplyFog(travelSeconds, progress);
        _initialStateApplied = true;
    }

    private void ApplyDestinationVisibility(
        RegionDefinition region,
        double travelSeconds,
        bool regionFullyExplored)
    {
        foreach (RegionMapNodeController destination in _destinations)
        {
            if (!GodotObject.IsInstanceValid(destination))
                continue;

            bool shouldReveal =
                regionFullyExplored
                || travelSeconds >= GetRevealTravelSeconds(
                    destination,
                    region)
                || ReferenceEquals(destination, ActiveDestination);

            bool isActive = ReferenceEquals(
                destination,
                ActiveDestination);
            bool canUse = isActive || CanEnterDestination(destination);
            bool blockedByAnotherDestination =
                ActiveDestination is not null
                && !isActive;

            destination.Visible = shouldReveal;
            destination.Disabled =
                !shouldReveal
                || !canUse
                || blockedByAnotherDestination;

            ApplyDestinationActionPresentation(
                destination,
                region,
                shouldReveal,
                canUse,
                blockedByAnotherDestination);

            if (!shouldReveal)
            {
                if (!string.IsNullOrWhiteSpace(destination.NodeContentId))
                {
                    _revealedDestinationIds.Remove(
                        destination.NodeContentId);
                }

                continue;
            }

            if (string.IsNullOrWhiteSpace(destination.NodeContentId)
                || !_revealedDestinationIds.Add(
                    destination.NodeContentId))
            {
                continue;
            }

            if (_initialStateApplied)
            {
                string discoveryType =
                    destination.NodeType == RegionMapNodeType.Graveyard
                        ? "graveyard"
                        : "destination";

                DebugLog.Print(
                    $"New {discoveryType} discovered: " +
                    $"{destination.DisplayName} " +
                    $"({destination.NodeContentId}).");

                DestinationDiscovered?.Invoke(destination);
            }
        }
    }

    private void OnDestinationActionPressed(
        RegionMapNodeController destination)
    {
        if (!GodotObject.IsInstanceValid(destination)
            || !destination.Visible
            || destination.Disabled)
        {
            return;
        }

        if (ReferenceEquals(destination, ActiveDestination))
        {
            RetreatActiveDestination();
            return;
        }

        if (ActiveDestination is not null
            || !CanEnterDestination(destination))
        {
            return;
        }

        ActiveDestination = destination;
        Exploration.SetDestinationExcursionActive(true);
        RegionRun.SetDestinationExcursionActive(true);
        RefreshPresentation(force: true);

        DebugLog.Print(
            $"Entered map destination: " +
            $"{destination.DisplayName} " +
            $"({destination.DestinationContentId}).");

        DestinationEntered?.Invoke(destination);
    }

    /// <summary>
    /// Returns from the active destination through the same state transition
    /// used by clicking its map node. Returns false when none is active.
    /// </summary>
    public bool RetreatActiveDestination()
    {
        if (!GodotObject.IsInstanceValid(ActiveDestination))
            return false;

        RegionMapNodeController destination = ActiveDestination!;
        ActiveDestination = null;
        Exploration.SetDestinationExcursionActive(false);
        RegionRun.SetDestinationExcursionActive(false);
        RefreshPresentation(force: true);

        DebugLog.Print(
            $"Retreated from map destination: " +
            $"{destination.DisplayName} " +
            $"({destination.DestinationContentId}).");

        DestinationRetreated?.Invoke(destination);
        return true;
    }

    private void ApplyDestinationActionPresentation(
        RegionMapNodeController destination,
        RegionDefinition region,
        bool shouldReveal,
        bool canEnter,
        bool blockedByAnotherDestination)
    {
        if (destination.NodeType == RegionMapNodeType.Graveyard
            && shouldReveal
            && region.TryGetGraveyard(
                destination.DestinationContentId,
                out GraveyardCheckpointDefinition graveyard))
        {
            destination.ApplyGraveyardPresentation(
                graveyard.DiscoveryPercent);
            return;
        }

        if (!shouldReveal || !canEnter || blockedByAnotherDestination)
        {
            destination.ApplyUnavailableActionPresentation();
            return;
        }

        if (ReferenceEquals(destination, ActiveDestination))
        {
            destination.ApplyRetreatActionPresentation(
                RegionRun.ActiveRegion.DisplayName);
            return;
        }

        destination.ApplyEnterActionPresentation();
    }

    private bool CanEnterDestination(
        RegionMapNodeController destination)
    {
        return destination.NodeType == RegionMapNodeType.Subregion
            && ContentId.IsValid(destination.DestinationContentId)
            && RegionRun.JourneyState.CurrentState
                == JourneyStateService.JourneyState.Traveling;
    }

    /// <summary>
    /// Graveyard reveal timing comes from region gameplay data rather than a
    /// duplicated value on its map marker. Other node types keep authored seconds.
    /// </summary>
    private static double GetRevealTravelSeconds(
        RegionMapNodeController destination,
        RegionDefinition region)
    {
        if (destination.NodeType == RegionMapNodeType.Graveyard
            && region.TryGetGraveyard(
                destination.DestinationContentId,
                out GraveyardCheckpointDefinition graveyard))
        {
            return region.GetGraveyardTravelSeconds(graveyard);
        }

        return destination.RevealAtTravelSeconds;
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
