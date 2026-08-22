using Godot;
using System.Collections.Generic;

[GlobalClass]
public partial class GraveyardCheckpointDefinition : Resource
{
	[ExportCategory("Identity")]

	/// <summary>
	/// Stable ID linked by the informational Graveyard node on the region map.
	/// </summary>
	[Export(PropertyHint.PlaceholderText,
		"graveyard.stonebanner_highlands.trailhead")]
	public string ContentId { get; set; } = string.Empty;

	/// <summary>
	/// Player-facing graveyard name used in revival and debug messages.
	/// </summary>
	[Export(PropertyHint.PlaceholderText, "Trailhead Graveyard")]
	public string DisplayName { get; set; } = "Unnamed Graveyard";

	[ExportCategory("Checkpoint")]

	/// <summary>
	/// Region exploration percentage that discovers this graveyard and becomes
	/// the rollback point after choosing Revive at Graveyard.
	/// </summary>
	[Export(PropertyHint.Range, "0,100,0.1,suffix:%")]
	public float DiscoveryPercent { get; set; } = 0.0f;

	[ExportCategory("Authoring")]

	[Export(PropertyHint.MultilineText)]
	public string DesignerNotes { get; set; } = string.Empty;

	public IReadOnlyList<string> GetValidationErrors()
	{
		List<string> errors = new();

		if (!global::ContentId.IsValid(ContentId))
		{
			errors.Add(
				$"Invalid graveyard Content ID '{ContentId}'.");
		}

		if (string.IsNullOrWhiteSpace(DisplayName))
			errors.Add($"{ContentId}: DisplayName is required.");

		if (DiscoveryPercent < 0.0f || DiscoveryPercent > 100.0f)
		{
			errors.Add(
				$"{ContentId}: DiscoveryPercent must be between 0 and 100.");
		}

		return errors;
	}
}
