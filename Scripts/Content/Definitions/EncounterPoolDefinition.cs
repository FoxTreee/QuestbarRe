using Godot;
using System;
using System.Collections.Generic;

[GlobalClass]
public partial class EncounterPoolDefinition : Resource
{
	[ExportCategory("Identity")]

	/// <summary>
	/// Stable content identifier for content; other systems use this value to find the same game data.
	/// For example, changing this ID makes the owning resource resolve a different registered content.
	/// </summary>
	[Export(
		PropertyHint.PlaceholderText,
		"encounter_pool.core.training_region")]
	public string ContentId { get; set; } =
		string.Empty;

	/// <summary>
	/// Controls display name.
	/// For example, changing this text changes the name, message, key, or lookup value shown or consumed by the owning system.
	/// </summary>
	[Export(PropertyHint.PlaceholderText, "Training Region")]
	public string DisplayName { get; set; } =
		"Unnamed Encounter Pool";

	/// <summary>
	/// Controls description.
	/// For example, changing this text changes the name, message, key, or lookup value shown or consumed by the owning system.
	/// </summary>
	[Export(PropertyHint.MultilineText)]
	public string Description { get; set; } =
		string.Empty;

	[ExportCategory("Encounter Selection")]

	/// <summary>
	/// Controls entries.
	/// For example, adding another entry gives the owning system one more configured entries to use.
	/// </summary>
	[Export]
	public Godot.Collections.Array<EncounterPoolEntry>
		Entries
	{ get; set; } = new();

	[ExportCategory("Authoring")]

	/// <summary>
	/// Controls designer notes.
	/// For example, changing this text changes the name, message, key, or lookup value shown or consumed by the owning system.
	/// </summary>
	[Export(PropertyHint.MultilineText)]
	public string DesignerNotes { get; set; } =
		string.Empty;

	/// <summary>
	/// Retrieves total weight from the current game state.
	/// Reads the current state and returns the resulting int to the caller.
	/// </summary>
	public int GetTotalWeight()
	{
		int totalWeight = 0;

		foreach (EncounterPoolEntry entry in Entries)
		{
			if (!GodotObject.IsInstanceValid(entry))
				continue;

			totalWeight += Math.Max(entry.Weight, 0);
		}

		return totalWeight;
	}

	/// <summary>
	/// Returns the combined weight of entries available at the supplied regional
	/// travel progress. Locked and retired encounters do not affect the roll.
	/// </summary>
	public int GetEligibleWeight(float regionTravelProgress)
	{
		int totalWeight = 0;

		foreach (EncounterPoolEntry entry in Entries)
		{
			if (!GodotObject.IsInstanceValid(entry)
				|| !entry.IsAvailableAtRegionTravelProgress(
					regionTravelProgress))
			{
				continue;
			}

			totalWeight += Math.Max(entry.Weight, 0);
		}

		return totalWeight;
	}

	/// <summary>
	/// Retrieves validation errors from the current game state.
	/// Reads the current state and returns the resulting i read only list string to the caller.
	/// </summary>
	public IReadOnlyList<string> GetValidationErrors()
	{
		List<string> errors = new();

		if (!global::ContentId.IsValid(ContentId))
		{
			errors.Add(
				$"Invalid encounter pool Content ID '{ContentId}'. " +
				"Expected lowercase format such as " +
				"'encounter_pool.core.training_region'.");
		}

		if (string.IsNullOrWhiteSpace(DisplayName))
		{
			errors.Add(
				$"{ContentId}: DisplayName is required.");
		}

		if (Entries.Count == 0)
		{
			errors.Add(
				$"{ContentId}: Entries must contain " +
				"at least one encounter.");

			return errors;
		}

		HashSet<string> seenEncounterIds =
			new(StringComparer.OrdinalIgnoreCase);

		foreach (EncounterPoolEntry entry in Entries)
		{
			if (!GodotObject.IsInstanceValid(entry))
			{
				errors.Add(
					$"{ContentId}: Entries contains " +
					"a missing entry.");

				continue;
			}

			foreach (string error in entry.GetValidationErrors())
			{
				errors.Add($"{ContentId}: {error}");
			}

			if (!string.IsNullOrWhiteSpace(
				entry.EncounterContentId)
				&& !seenEncounterIds.Add(
					entry.EncounterContentId.Trim()))
			{
				errors.Add(
					$"{ContentId}: duplicate encounter " +
					$"Content ID '{entry.EncounterContentId}'.");
			}
		}

		if (GetTotalWeight() <= 0)
		{
			errors.Add(
				$"{ContentId}: total encounter weight " +
				"must be greater than zero.");
		}
		else
		{
			ValidateTravelAvailabilityCoverage(errors);
		}

		return errors;
	}

	/// <summary>
	/// Verifies that at least one positive-weight encounter is available across
	/// the entire 0-100% regional travel range, preventing infinite runs from
	/// stopping in an accidentally unauthored gap.
	/// </summary>
	private void ValidateTravelAvailabilityCoverage(List<string> errors)
	{
		List<EncounterPoolEntry> orderedEntries = new();

		foreach (EncounterPoolEntry entry in Entries)
		{
			if (!GodotObject.IsInstanceValid(entry)
				|| entry.Weight <= 0
				|| entry.AvailableFromRegionTravelPercent < 0.0f
				|| entry.AvailableThroughRegionTravelPercent
					< entry.AvailableFromRegionTravelPercent)
			{
				continue;
			}

			orderedEntries.Add(entry);
		}

		orderedEntries.Sort((left, right) =>
			left.AvailableFromRegionTravelPercent.CompareTo(
				right.AvailableFromRegionTravelPercent));

		float coveredThrough = 0.0f;
		bool hasCoverage = false;

		foreach (EncounterPoolEntry entry in orderedEntries)
		{
			if (!hasCoverage)
			{
				if (entry.AvailableFromRegionTravelPercent > 0.0f)
				{
					errors.Add(
						$"{ContentId}: encounter availability has no " +
						$"coverage from 0% to " +
						$"{entry.AvailableFromRegionTravelPercent:0.##}%.");
				}

				hasCoverage = true;
				coveredThrough =
					entry.AvailableThroughRegionTravelPercent;
				continue;
			}

			if (entry.AvailableFromRegionTravelPercent > coveredThrough)
			{
				errors.Add(
					$"{ContentId}: encounter availability has no coverage " +
					$"from {coveredThrough:0.##}% to " +
					$"{entry.AvailableFromRegionTravelPercent:0.##}%.");
			}

			coveredThrough = Math.Max(
				coveredThrough,
				entry.AvailableThroughRegionTravelPercent);
		}

		if (!hasCoverage || coveredThrough < 100.0f)
		{
			errors.Add(
				$"{ContentId}: encounter availability must cover " +
				"maximum regional travel progress (100%).");
		}
	}
}
