using Godot;
using System.Collections.Generic;

[GlobalClass]
public partial class RegionDefinition : Resource
{
	[ExportCategory("Identity")]
	/// <summary>
	/// Stable content identifier for content; other systems use this value to find the same game data.
	/// For example, changing this ID makes the owning resource resolve a different registered content.
	/// </summary>
	[Export(PropertyHint.PlaceholderText, "region.core.training_region")]
	public string ContentId { get; set; } = string.Empty;

	/// <summary>
	/// Controls display name.
	/// For example, changing this text changes the name, message, key, or lookup value shown or consumed by the owning system.
	/// </summary>
	[Export(PropertyHint.PlaceholderText, "Training Region")]
	public string DisplayName { get; set; } = "Unnamed Region";

	/// <summary>
	/// Controls description.
	/// For example, changing this text changes the name, message, key, or lookup value shown or consumed by the owning system.
	/// </summary>
	[Export(PropertyHint.MultilineText)]
	public string Description { get; set; } = string.Empty;

	[ExportCategory("Journey")]
	/// <summary>
	/// Stable content identifier for encounter pool; other systems use this value to find the same game data.
	/// For example, changing this ID makes the owning resource resolve a different registered encounter pool.
	/// </summary>
	[Export(PropertyHint.PlaceholderText,
		"encounter_pool.core.training_region")]
	public string EncounterPoolContentId { get; set; } = string.Empty;

	/// <summary>
	/// When enabled, victories always return to travel and another encounter is
	/// scheduled. Disable this only for authored finite runs such as a scenario.
	/// </summary>
	[Export]
	public bool EncountersLoopIndefinitely { get; set; } = true;

	/// <summary>
	/// Number of groups in a finite run. This is ignored while
	/// EncountersLoopIndefinitely is enabled.
	/// </summary>
	[Export(PropertyHint.Range, "1,100,1")]
	public int MonsterGroupCount { get; set; } = 4;

	[ExportCategory("Encounter Timing")]

	/// <summary>
	/// Shortest Traveling-state delay that may be rolled before an encounter.
	/// Set this equal to MaximumTravelSecondsBetweenEncounters for fixed timing.
	/// </summary>
	[Export(PropertyHint.Range, "0,3600,0.1,suffix:s")]
	public float MinimumTravelSecondsBetweenEncounters { get; set; } = 4.0f;

	/// <summary>
	/// Longest Traveling-state delay that may be rolled before an encounter.
	/// A fresh value is selected at run start and after each cleared encounter.
	/// </summary>
	[Export(PropertyHint.Range, "0,3600,0.1,suffix:s")]
	public float MaximumTravelSecondsBetweenEncounters { get; set; } = 8.0f;

	[ExportCategory("Exploration")]

	/// <summary>
	/// Traveling-state seconds required to uncover the complete region map.
	/// Combat and other Journey states do not advance this timer.
	/// </summary>
	[Export(PropertyHint.Range, "1,86400,1,suffix:s")]
	public float FullExplorationTravelSeconds { get; set; } = 7200.0f;

	[ExportCategory("Graveyard Checkpoints")]

	/// <summary>
	/// Region-specific checkpoints discovered by exploration percentage. A
	/// graveyard revival rewinds saved exploration to the highest discovered one.
	/// </summary>
	[Export]
	public Godot.Collections.Array<GraveyardCheckpointDefinition>
		GraveyardCheckpoints
	{ get; set; } = new();

	[ExportCategory("Monster Difficulty")]

	/// <summary>
	/// Runtime level assigned to monsters at zero regional travel progress.
	/// The authored monster definition remains unchanged.
	/// </summary>
	[Export(PropertyHint.Range, "1,60,1")]
	public int StartingMonsterLevel { get; set; } = 1;

	/// <summary>
	/// Highest runtime monster level in this region. It is reached only when
	/// FullExplorationTravelSeconds has been reached.
	/// </summary>
	[Export(PropertyHint.Range, "1,60,1")]
	public int MaximumMonsterLevel { get; set; } = 5;

	/// <summary>
	/// Travel-time interval between difficulty updates. Set this to zero for
	/// continuous scaling; larger values create fewer, more noticeable steps.
	/// </summary>
	[Export(PropertyHint.Range, "0,86400,1,suffix:s")]
	public float DifficultyIncreaseIntervalTravelSeconds { get; set; } =
		300.0f;

	/// <summary>
	/// Percentage added to each monster definition's base maximum health at
	/// full exploration. For example, 50 produces 150% of base health.
	/// </summary>
	[Export(PropertyHint.Range, "0,10000,1,suffix:%")]
	public float MaximumHealthIncreasePercent { get; set; } = 50.0f;

	/// <summary>
	/// Percentage added to basic attacks and fixed-damage monster abilities at
	/// full exploration. For example, 25 produces 125% of base damage.
	/// </summary>
	[Export(PropertyHint.Range, "0,10000,1,suffix:%")]
	public float MaximumDamageIncreasePercent { get; set; } = 25.0f;

	[ExportCategory("Presentation")]
	/// <summary>
	/// Inspector reference used by this component for its background texture dependency.
	/// Assign the matching node or resource from the scene; leaving it empty prevents that connection from working.
	/// </summary>
	[Export]
	public Texture2D BackgroundTexture { get; set; } = null!;

	/// <summary>
	/// Inspector reference used by this component for its ground texture dependency.
	/// Assign the matching node or resource from the scene; leaving it empty prevents that connection from working.
	/// </summary>
	[Export]
	public Texture2D GroundTexture { get; set; } = null!;

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
				$"Invalid region Content ID '{ContentId}'. " +
				"Expected lowercase format such as " +
				"'region.core.training_region'.");
		}

		if (string.IsNullOrWhiteSpace(DisplayName))
			errors.Add($"{ContentId}: DisplayName is required.");

		if (!global::ContentId.IsValid(EncounterPoolContentId))
		{
			errors.Add(
				$"{ContentId}: invalid encounter pool Content ID " +
				$"'{EncounterPoolContentId}'.");
		}

		if (!EncountersLoopIndefinitely
			&& MonsterGroupCount <= 0)
		{
			errors.Add(
				$"{ContentId}: MonsterGroupCount must be " +
				"greater than zero.");
		}

		if (MinimumTravelSecondsBetweenEncounters < 0.0f)
		{
			errors.Add(
				$"{ContentId}: MinimumTravelSecondsBetweenEncounters " +
				"cannot be negative.");
		}

		if (MaximumTravelSecondsBetweenEncounters
			< MinimumTravelSecondsBetweenEncounters)
		{
			errors.Add(
				$"{ContentId}: MaximumTravelSecondsBetweenEncounters " +
				"must be greater than or equal to the minimum.");
		}

		if (FullExplorationTravelSeconds <= 0.0f)
		{
			errors.Add(
				$"{ContentId}: FullExplorationTravelSeconds must be " +
				"greater than zero.");
		}

		HashSet<string> seenGraveyardIds =
			new(System.StringComparer.OrdinalIgnoreCase);

		foreach (GraveyardCheckpointDefinition graveyard
			in GraveyardCheckpoints)
		{
			if (!GodotObject.IsInstanceValid(graveyard))
			{
				errors.Add(
					$"{ContentId}: GraveyardCheckpoints contains a " +
					"missing entry.");
				continue;
			}

			foreach (string error in graveyard.GetValidationErrors())
				errors.Add($"{ContentId}: {error}");

			if (global::ContentId.IsValid(graveyard.ContentId)
				&& !seenGraveyardIds.Add(graveyard.ContentId.Trim()))
			{
				errors.Add(
					$"{ContentId}: duplicate graveyard Content ID " +
					$"'{graveyard.ContentId}'.");
			}
		}

		if (StartingMonsterLevel < 1 || StartingMonsterLevel > 60)
		{
			errors.Add(
				$"{ContentId}: StartingMonsterLevel must be " +
				"between 1 and 60.");
		}

		if (MaximumMonsterLevel < StartingMonsterLevel
			|| MaximumMonsterLevel > 60)
		{
			errors.Add(
				$"{ContentId}: MaximumMonsterLevel must be between " +
				"StartingMonsterLevel and 60.");
		}

		if (DifficultyIncreaseIntervalTravelSeconds < 0.0f)
		{
			errors.Add(
				$"{ContentId}: DifficultyIncreaseIntervalTravelSeconds " +
				"cannot be negative.");
		}

		if (MaximumHealthIncreasePercent < 0.0f)
		{
			errors.Add(
				$"{ContentId}: MaximumHealthIncreasePercent cannot " +
				"be negative.");
		}

		if (MaximumDamageIncreasePercent < 0.0f)
		{
			errors.Add(
				$"{ContentId}: MaximumDamageIncreasePercent cannot " +
				"be negative.");
		}

		return errors;
	}

	/// <summary>
	/// Finds the discovered graveyard with the greatest checkpoint percentage.
	/// Array order is irrelevant, so regions may author any number of graveyards.
	/// </summary>
	public bool TryGetLatestDiscoveredGraveyard(
		float explorationPercent,
		out GraveyardCheckpointDefinition graveyard)
	{
		graveyard = null!;
		float clampedPercent = Mathf.Clamp(
			explorationPercent,
			0.0f,
			100.0f);
		float latestPercent = -1.0f;

		foreach (GraveyardCheckpointDefinition candidate
			in GraveyardCheckpoints)
		{
			if (!GodotObject.IsInstanceValid(candidate)
				|| candidate.DiscoveryPercent > clampedPercent + 0.001f
				|| candidate.DiscoveryPercent <= latestPercent)
			{
				continue;
			}

			graveyard = candidate;
			latestPercent = candidate.DiscoveryPercent;
		}

		return GodotObject.IsInstanceValid(graveyard);
	}

	public bool TryGetGraveyard(
		string graveyardContentId,
		out GraveyardCheckpointDefinition graveyard)
	{
		foreach (GraveyardCheckpointDefinition candidate
			in GraveyardCheckpoints)
		{
			if (GodotObject.IsInstanceValid(candidate)
				&& candidate.ContentId.Equals(
					graveyardContentId?.Trim(),
					System.StringComparison.OrdinalIgnoreCase))
			{
				graveyard = candidate;
				return true;
			}
		}

		graveyard = null!;
		return false;
	}

	public double GetGraveyardTravelSeconds(
		GraveyardCheckpointDefinition graveyard)
	{
		return FullExplorationTravelSeconds
			* (Mathf.Clamp(graveyard.DiscoveryPercent, 0.0f, 100.0f)
				/ 100.0);
	}

	/// <summary>
	/// Captures the level, health, and damage scaling for a newly starting
	/// encounter. Existing monsters never change when travel time advances.
	/// </summary>
	public MonsterDifficultySnapshot CreateMonsterDifficultySnapshot(
		double regionTravelSeconds)
	{
		double clampedTravelSeconds = System.Math.Clamp(
			regionTravelSeconds,
			0.0,
			FullExplorationTravelSeconds);

		double difficultyTravelSeconds = clampedTravelSeconds;

		if (DifficultyIncreaseIntervalTravelSeconds > 0.0f
			&& clampedTravelSeconds < FullExplorationTravelSeconds)
		{
			difficultyTravelSeconds = System.Math.Floor(
				clampedTravelSeconds
				/ DifficultyIncreaseIntervalTravelSeconds)
				* DifficultyIncreaseIntervalTravelSeconds;
		}

		float progress = Mathf.Clamp(
			(float)(difficultyTravelSeconds
				/ FullExplorationTravelSeconds),
			0.0f,
			1.0f);

		int levelRange = MaximumMonsterLevel - StartingMonsterLevel;
		int monsterLevel = StartingMonsterLevel
			+ Mathf.FloorToInt(levelRange * progress);

		float healthMultiplier = 1.0f
			+ (MaximumHealthIncreasePercent / 100.0f) * progress;
		float damageMultiplier = 1.0f
			+ (MaximumDamageIncreasePercent / 100.0f) * progress;

		return new MonsterDifficultySnapshot(
			monsterLevel,
			healthMultiplier,
			damageMultiplier,
			clampedTravelSeconds,
			progress);
	}
}
