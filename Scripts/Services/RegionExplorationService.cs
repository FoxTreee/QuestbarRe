using Godot;
using System;
using System.Collections.Generic;
using System.Text.Json;

/// <summary>
/// Owns saved travel-time progress for every region. Time advances only while
/// the active journey is Traveling, independently of whether the Map window is
/// open, and is clamped by each RegionDefinition's authored maximum.
/// </summary>
public partial class RegionExplorationService : Node
{
    private const string SavePath = "user://region_exploration_v1.json";
    private const string TemporarySavePath =
        "user://region_exploration_v1.tmp";

    [ExportCategory("Dependencies")]

    [Export]
    public JourneyStateService JourneyState { get; set; } = null!;

    [Export]
    public RegionRunController RegionRun { get; set; } = null!;

    [ExportCategory("Persistence")]

    /// <summary>
    /// Real-time interval between writes while exploration progress is dirty.
    /// </summary>
    [Export(PropertyHint.Range, "1,60,1,suffix:s")]
    public float AutoSaveIntervalSeconds { get; set; } = 5.0f;

    private readonly Dictionary<string, double> _travelSecondsByRegion =
        new(StringComparer.OrdinalIgnoreCase);

    private bool _dirty;
    private double _autoSaveElapsed;

    /// <summary>
    /// Changes whenever travel progress changes. Presenters use this cheap
    /// revision number instead of rebuilding fog and node state unnecessarily.
    /// </summary>
    public ulong Revision { get; private set; }

    /// <summary>
    /// True while the party is inside a subregion, dungeon, or town. Normal
    /// Traveling time there must not advance the surrounding region's map.
    /// </summary>
    public bool IsDestinationExcursionActive { get; private set; }

    /// <summary>
    /// Loads saved regional travel time and starts the independent timer.
    /// </summary>
    public override void _Ready()
    {
        if (!ValidateReferences())
        {
            SetProcess(false);
            return;
        }

        DebugLog.Print(Load());
        SetProcess(true);
    }

    /// <summary>
    /// Advances only the active region and only during the Traveling journey
    /// state. Combat and incapacitation therefore never count as exploration.
    /// </summary>
    public override void _Process(double delta)
    {
        if (!IsDestinationExcursionActive
            && JourneyState.CurrentState
            == JourneyStateService.JourneyState.Traveling)
        {
            AddActiveRegionTravelTime(delta);
        }

        if (!_dirty)
            return;

        _autoSaveElapsed += delta;

        if (_autoSaveElapsed < AutoSaveIntervalSeconds)
            return;

        Save();
    }

    /// <summary>
    /// Saves pending progress before the service leaves the tree.
    /// </summary>
    public override void _ExitTree()
    {
        if (_dirty)
            Save();
    }

    public double GetRegionTravelSeconds(string regionContentId)
    {
        return _travelSecondsByRegion.TryGetValue(
            regionContentId,
            out double seconds)
                ? Math.Max(0.0, seconds)
                : 0.0;
    }

    public double GetActiveRegionTravelSeconds()
    {
        RegionDefinition? region = GetActiveRegion();

        return region is null
            ? 0.0
            : Math.Min(
                GetRegionTravelSeconds(region.ContentId),
                region.FullExplorationTravelSeconds);
    }

    public float GetActiveRegionProgress()
    {
        RegionDefinition? region = GetActiveRegion();

        if (region is null
            || region.FullExplorationTravelSeconds <= 0.0f)
        {
            return 0.0f;
        }

        return Mathf.Clamp(
            (float)(GetActiveRegionTravelSeconds()
                / region.FullExplorationTravelSeconds),
            0.0f,
            1.0f);
    }

    /// <summary>
    /// Suspends or resumes only normal time accumulation. Debug commands may
    /// still deliberately change the saved regional exploration value.
    /// </summary>
    public void SetDestinationExcursionActive(bool active)
    {
        if (IsDestinationExcursionActive == active)
            return;

        IsDestinationExcursionActive = active;

        DebugLog.Print(
            active
                ? "Main-region exploration paused for destination excursion."
                : "Main-region exploration resumed after destination excursion.");
    }

    /// <summary>
    /// Sets the current region to its normal authored maximum. This is not a
    /// presentation bypass: fog and destinations react to the same saved time
    /// value that ordinary Traveling accumulation changes.
    /// </summary>
    public string CompleteActiveRegionExploration()
    {
        RegionDefinition? region = GetActiveRegion();

        if (region is null)
            return "No active region is available to complete.";

        SetRegionTravelTime(
            region.ContentId,
            region.FullExplorationTravelSeconds);

        string saveResult = Save();

        return
            $"Completed normal exploration time for " +
            $"{region.DisplayName} ({region.ContentId}): " +
            $"{region.FullExplorationTravelSeconds:0}s. " +
            saveResult;
    }

    /// <summary>
    /// Resets the current region to zero saved Traveling seconds. Fog and
    /// destinations return through their normal time-driven presentation.
    /// </summary>
    public string ResetActiveRegionExploration()
    {
        RegionDefinition? region = GetActiveRegion();

        if (region is null)
            return "No active region is available to reset.";

        SetRegionTravelTime(region.ContentId, 0.0);

        string saveResult = Save();

        return
            $"Reset exploration time for " +
            $"{region.DisplayName} ({region.ContentId}) to 0s. " +
            saveResult;
    }

    /// <summary>
    /// Rewinds the active region to its highest discovered graveyard checkpoint
    /// and saves immediately. With no authored checkpoint yet discovered, the
    /// region entrance at zero percent is used as a safe fallback.
    /// </summary>
    public string ReturnActiveRegionToLatestGraveyard()
    {
        RegionDefinition? region = GetActiveRegion();

        if (region is null)
            return "No active region is available for graveyard revival.";

        double previousSeconds = GetActiveRegionTravelSeconds();
        float previousPercent = GetActiveRegionProgress() * 100.0f;
        double checkpointSeconds = 0.0;
        float checkpointPercent = 0.0f;
        string graveyardName = $"{region.DisplayName} Entrance";

        if (region.TryGetLatestDiscoveredGraveyard(
            previousPercent,
            out GraveyardCheckpointDefinition graveyard))
        {
            checkpointPercent = graveyard.DiscoveryPercent;
            checkpointSeconds = region.GetGraveyardTravelSeconds(graveyard);
            graveyardName = graveyard.DisplayName;
        }

        SetRegionTravelTime(region.ContentId, checkpointSeconds);
        string saveResult = Save();

        return
            $"Returned to {graveyardName} at {checkpointPercent:0.#}% " +
            $"exploration ({previousPercent:0.#}% → " +
            $"{checkpointPercent:0.#}%; {previousSeconds:0.#}s → " +
            $"{checkpointSeconds:0.#}s). {saveResult}";
    }

    /// <summary>
    /// Adds debug travel time to the active region without exceeding its
    /// authored exploration maximum, then immediately saves the new value.
    /// </summary>
    public string AddActiveRegionExplorationTime(double secondsToAdd)
    {
        RegionDefinition? region = GetActiveRegion();

        if (region is null)
            return "No active region is available to advance.";

        if (double.IsNaN(secondsToAdd)
            || double.IsInfinity(secondsToAdd)
            || secondsToAdd <= 0.0)
        {
            return "Exploration time to add must be greater than zero.";
        }

        double currentSeconds = GetActiveRegionTravelSeconds();
        double newSeconds = Math.Min(
            region.FullExplorationTravelSeconds,
            currentSeconds + secondsToAdd);

        SetRegionTravelTime(region.ContentId, newSeconds);

        string saveResult = Save();

        return
            $"Added {secondsToAdd:0.###}s of exploration time to " +
            $"{region.DisplayName} ({region.ContentId}). " +
            $"Current time: {newSeconds:0.###}/" +
            $"{region.FullExplorationTravelSeconds:0.###}s. " +
            saveResult;
    }

    public string BuildActiveRegionStatusText()
    {
        RegionDefinition? region = GetActiveRegion();

        if (region is null)
            return "Region exploration: unavailable";

        double seconds = GetActiveRegionTravelSeconds();
        float progress = GetActiveRegionProgress() * 100.0f;
		MonsterDifficultySnapshot difficulty =
			region.CreateMonsterDifficultySnapshot(seconds);

        return
            $"Region exploration: {region.DisplayName} " +
            $"{seconds:0.0}/{region.FullExplorationTravelSeconds:0.0}s " +
            $"({progress:0.0}%); next encounter: " +
			$"Level {difficulty.MonsterLevel}, " +
			$"health x{difficulty.HealthMultiplier:0.###}, " +
			$"damage x{difficulty.DamageMultiplier:0.###}" +
            (IsDestinationExcursionActive
                ? " [PAUSED: destination excursion]"
                : string.Empty);
    }

    public string Save()
    {
        RegionExplorationSaveData data = new()
        {
            Version = 1,
            TravelSecondsByRegion =
                new Dictionary<string, double>(
                    _travelSecondsByRegion,
                    StringComparer.OrdinalIgnoreCase)
        };

        string json = JsonSerializer.Serialize(
            data,
            new JsonSerializerOptions { WriteIndented = true });

        using (FileAccess file = FileAccess.Open(
            TemporarySavePath,
            FileAccess.ModeFlags.Write))
        {
            if (file is null)
                return $"Could not open {TemporarySavePath} for writing.";

            file.StoreString(json);
            file.Flush();
        }

        string temporaryAbsolute =
            ProjectSettings.GlobalizePath(TemporarySavePath);
        string saveAbsolute = ProjectSettings.GlobalizePath(SavePath);

        try
        {
            if (System.IO.File.Exists(saveAbsolute))
            {
                System.IO.File.Replace(
                    temporaryAbsolute,
                    saveAbsolute,
                    null);
            }
            else
            {
                System.IO.File.Move(temporaryAbsolute, saveAbsolute);
            }
        }
        catch (Exception exception)
        {
            return
                $"Could not atomically save region exploration: " +
                exception.Message;
        }

        _dirty = false;
        _autoSaveElapsed = 0.0;
        return $"Region exploration saved to {SavePath}.";
    }

    public string Load()
    {
        if (!FileAccess.FileExists(SavePath))
            return "No region exploration save found; starting unexplored.";

        using FileAccess file = FileAccess.Open(
            SavePath,
            FileAccess.ModeFlags.Read);

        if (file is null)
            return $"Could not open {SavePath}.";

        RegionExplorationSaveData? data;

        try
        {
            data = JsonSerializer.Deserialize<RegionExplorationSaveData>(
                file.GetAsText());
        }
        catch (Exception exception)
        {
            return
                $"Region exploration save is invalid JSON: " +
                exception.Message;
        }

        if (data is null || data.Version != 1 || data.TravelSecondsByRegion is null)
            return "Region exploration save version is unsupported.";

        _travelSecondsByRegion.Clear();

        foreach ((string contentId, double seconds)
            in data.TravelSecondsByRegion)
        {
            if (!ContentId.IsValid(contentId)
                || double.IsNaN(seconds)
                || double.IsInfinity(seconds)
                || seconds < 0.0)
            {
                continue;
            }

            _travelSecondsByRegion[contentId] = seconds;
        }

        _dirty = false;
        _autoSaveElapsed = 0.0;
        Revision++;
        return $"Region exploration loaded from {SavePath}.";
    }

    private void AddActiveRegionTravelTime(double delta)
    {
        RegionDefinition? region = GetActiveRegion();

        if (region is null || delta <= 0.0)
            return;

        double current = GetRegionTravelSeconds(region.ContentId);
        double maximum = region.FullExplorationTravelSeconds;

        if (current >= maximum)
            return;

        SetRegionTravelTime(
            region.ContentId,
            Math.Min(maximum, current + delta));
    }

    private void SetRegionTravelTime(
        string regionContentId,
        double seconds)
    {
        _travelSecondsByRegion[regionContentId] = Math.Max(0.0, seconds);
        _dirty = true;
        Revision++;
    }

    private RegionDefinition? GetActiveRegion()
    {
        return GodotObject.IsInstanceValid(RegionRun)
            && GodotObject.IsInstanceValid(RegionRun.ActiveRegion)
                ? RegionRun.ActiveRegion
                : null;
    }

    private bool ValidateReferences()
    {
        bool valid = true;

        if (!GodotObject.IsInstanceValid(JourneyState))
        {
            GD.PushError(
                "RegionExplorationService is missing JourneyState.");
            valid = false;
        }

        if (!GodotObject.IsInstanceValid(RegionRun))
        {
            GD.PushError(
                "RegionExplorationService is missing RegionRun.");
            valid = false;
        }

        return valid;
    }
}

public sealed class RegionExplorationSaveData
{
    public int Version { get; set; }

    public Dictionary<string, double> TravelSecondsByRegion
    {
        get;
        set;
    } = new();
}
