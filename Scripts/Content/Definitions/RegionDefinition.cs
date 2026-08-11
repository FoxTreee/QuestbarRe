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
	/// Controls monster group count, measured as a count.
	/// For example, changing 4 to 8 doubles the configured monster group count.
	/// </summary>
	[Export(PropertyHint.Range, "1,100,1")]
	public int MonsterGroupCount { get; set; } = 4;

	[ExportCategory("Completion Reward")]
	/// <summary>
	/// Stable content identifier for completion reward; other systems use this value to find the same game data.
	/// For example, changing this ID makes the owning resource resolve a different registered completion reward.
	/// </summary>
	[Export(PropertyHint.PlaceholderText, "currency.core.gold")]
	public string CompletionRewardContentId { get; set; } =
		"currency.core.gold";

	/// <summary>
	/// Controls completion reward amount, measured as a count.
	/// For example, changing 100 to 200 doubles the configured completion reward amount.
	/// </summary>
	[Export(PropertyHint.Range, "0,1000000,1")]
	public int CompletionRewardAmount { get; set; } = 100;

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

		if (MonsterGroupCount <= 0)
		{
			errors.Add(
				$"{ContentId}: MonsterGroupCount must be " +
				"greater than zero.");
		}

		if (!global::ContentId.IsValid(CompletionRewardContentId))
		{
			errors.Add(
				$"{ContentId}: invalid completion reward Content ID " +
				$"'{CompletionRewardContentId}'.");
		}

		if (CompletionRewardAmount < 0)
		{
			errors.Add(
				$"{ContentId}: CompletionRewardAmount cannot be negative.");
		}

		return errors;
	}
}
