using Godot;
using System.Collections.Generic;

[GlobalClass]
public partial class RegionDefinition : Resource
{
	[ExportCategory("Identity")]
	[Export(PropertyHint.PlaceholderText, "region.core.training_region")]
	public string ContentId { get; set; } = string.Empty;

	[Export(PropertyHint.PlaceholderText, "Training Region")]
	public string DisplayName { get; set; } = "Unnamed Region";

	[Export(PropertyHint.MultilineText)]
	public string Description { get; set; } = string.Empty;

	[ExportCategory("Journey")]
	[Export(PropertyHint.PlaceholderText,
		"encounter_pool.core.training_region")]
	public string EncounterPoolContentId { get; set; } = string.Empty;

	[Export(PropertyHint.Range, "1,100,1")]
	public int MonsterGroupCount { get; set; } = 4;

	[ExportCategory("Completion Reward")]
	[Export(PropertyHint.PlaceholderText, "currency.core.gold")]
	public string CompletionRewardContentId { get; set; } =
		"currency.core.gold";

	[Export(PropertyHint.Range, "0,1000000,1")]
	public int CompletionRewardAmount { get; set; } = 100;

	[ExportCategory("Presentation")]
	[Export]
	public Texture2D BackgroundTexture { get; set; } = null!;

	[Export]
	public Texture2D GroundTexture { get; set; } = null!;

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
