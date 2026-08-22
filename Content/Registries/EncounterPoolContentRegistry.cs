using Godot;
using System.Collections.Generic;

public partial class EncounterPoolContentRegistry : Node
{
	[ExportCategory("Dependencies")]
	/// <summary>
	/// Inspector reference used by this component for its encounter registry dependency.
	/// Assign the matching node or resource from the scene; leaving it empty prevents that connection from working.
	/// </summary>
	[Export]
	public EncounterContentRegistry EncounterRegistry
	{ get; set; } = null!;

	[ExportCategory("Encounter Pool Content")]
	/// <summary>
	/// Controls definitions.
	/// For example, adding another entry gives the owning system one more configured definitions to use.
	/// </summary>
	[Export]
	public Godot.Collections.Array<EncounterPoolDefinition>
		Definitions
	{ get; set; } = new();

	private readonly Dictionary<string, EncounterPoolDefinition>
		_definitionsById = new();

	public int Count => _definitionsById.Count;

	/// <summary>
	/// Runs Godot setup for Encounter Pool Content Registry when the node enters the scene tree.
	/// Uses the current node and service state; any result is applied through side effects, events, or stored fields.
	/// </summary>
	public override void _Ready()
	{
		Rebuild();

		DebugLog.Print(
			$"EncounterPoolContentRegistry initialized with " +
			$"{Count} definition(s).");

		foreach (EncounterPoolDefinition definition
			in _definitionsById.Values)
		{
			PrintDefinition(definition);
		}
	}

	/// <summary>
	/// Performs the rebuild operation for Encounter Pool Content Registry.
	/// Uses the current node and service state; any result is applied through side effects, events, or stored fields.
	/// </summary>
	public void Rebuild()
	{
		_definitionsById.Clear();

		if (!GodotObject.IsInstanceValid(EncounterRegistry))
		{
			GD.PushError(
				"EncounterPoolContentRegistry is missing its " +
				"EncounterRegistry Inspector reference.");

			DebugLog.Print(
				"EncounterPoolContentRegistry could not rebuild: " +
				"EncounterRegistry reference is missing.");

			return;
		}

		foreach (EncounterPoolDefinition definition
			in Definitions)
		{
			Register(definition);
		}
	}

	/// <summary>
	/// Attempts to get without throwing when the operation cannot be completed.
	/// Uses the supplied arguments and current state and returns the resulting bool to the caller.
	/// </summary>
	public bool TryGet(
		string contentId,
		out EncounterPoolDefinition definition)
	{
		string normalizedId = Normalize(contentId);

		return _definitionsById.TryGetValue(
			normalizedId,
			out definition!);
	}

	/// <summary>
	/// Retrieves required from the current game state.
	/// Uses the supplied arguments and current state and returns the resulting encounter pool definition to the caller.
	/// </summary>
	public EncounterPoolDefinition GetRequired(
		string contentId)
	{
		if (TryGet(
			contentId,
			out EncounterPoolDefinition definition))
		{
			return definition;
		}

		throw new KeyNotFoundException(
			$"No EncounterPoolDefinition is registered for " +
			$"Content ID '{contentId}'.");
	}

	/// <summary>
	/// Retrieves registered ids from the current game state.
	/// Reads the current state and returns the resulting i read only collection string to the caller.
	/// </summary>
	public IReadOnlyCollection<string> GetRegisteredIds()
	{
		return _definitionsById.Keys;
	}

	/// <summary>
	/// Performs the register operation for Encounter Pool Content Registry.
	/// Uses the supplied arguments and current node state; any result is applied through side effects, events, or stored fields.
	/// </summary>
	private void Register(
		EncounterPoolDefinition definition)
	{
		if (!GodotObject.IsInstanceValid(definition))
		{
			GD.PushError(
				"EncounterPoolContentRegistry contains a missing " +
				"EncounterPoolDefinition.");

			return;
		}

		List<string> errors =
			new(definition.GetValidationErrors());

		foreach (EncounterPoolEntry entry
			in definition.Entries)
		{
			if (!GodotObject.IsInstanceValid(entry)
				|| string.IsNullOrWhiteSpace(
					entry.EncounterContentId))
			{
				continue;
			}

			if (!EncounterRegistry.TryGet(
				entry.EncounterContentId,
				out _))
			{
				errors.Add(
					$"{definition.ContentId}: unknown encounter " +
					$"Content ID '{entry.EncounterContentId}'.");
			}
		}

		if (errors.Count > 0)
		{
			foreach (string error in errors)
			{
				GD.PushError(error);

				DebugLog.Print(
					$"Encounter pool content error: {error}");
			}

			return;
		}

		string normalizedId =
			Normalize(definition.ContentId);

		if (!_definitionsById.TryAdd(
			normalizedId,
			definition))
		{
			string message =
				$"Duplicate encounter pool Content ID " +
				$"'{definition.ContentId}'.";

			GD.PushError(message);

			DebugLog.Print(
				$"Encounter pool content error: {message}");
		}
	}

	/// <summary>
	/// Performs the print definition operation for Encounter Pool Content Registry.
	/// Uses the supplied arguments and current node state; any result is applied through side effects, events, or stored fields.
	/// </summary>
	private static void PrintDefinition(
		EncounterPoolDefinition definition)
	{
		DebugLog.Print(
			$"Encounter pool loaded: " +
			$"{definition.ContentId} " +
			$"('{definition.DisplayName}'). " +
			$"Total weight={definition.GetTotalWeight()}.");

		foreach (EncounterPoolEntry entry
			in definition.Entries)
		{
			DebugLog.Print(
				$"  {entry.EncounterContentId}: " +
				$"weight={entry.Weight}; available=" +
				$"{entry.AvailableFromRegionTravelPercent:0.##}-" +
				$"{entry.AvailableThroughRegionTravelPercent:0.##}%");
		}
	}

	/// <summary>
	/// Performs the normalize operation for Encounter Pool Content Registry.
	/// Uses the supplied arguments and current state and returns the resulting string to the caller.
	/// </summary>
	private static string Normalize(string contentId)
	{
		return contentId.Trim().ToLowerInvariant();
	}
}
