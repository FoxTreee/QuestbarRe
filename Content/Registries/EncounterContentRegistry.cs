using Godot;
using System.Collections.Generic;

public partial class EncounterContentRegistry : Node
{
	[ExportCategory("Dependencies")]
	/// <summary>
	/// Inspector reference used by this component for its monster registry dependency.
	/// Assign the matching node or resource from the scene; leaving it empty prevents that connection from working.
	/// </summary>
	[Export]
	public MonsterContentRegistry MonsterRegistry
	{ get; set; } = null!;

	[ExportCategory("Encounter Content")]
	/// <summary>
	/// Controls definitions.
	/// For example, adding another entry gives the owning system one more configured definitions to use.
	/// </summary>
	[Export]
	public Godot.Collections.Array<EncounterDefinition>
		Definitions
	{ get; set; } = new();

	private readonly Dictionary<string, EncounterDefinition>
		_definitionsById = new();

	public int Count => _definitionsById.Count;

	/// <summary>
	/// Runs Godot setup for Encounter Content Registry when the node enters the scene tree.
	/// Uses the current node and service state; any result is applied through side effects, events, or stored fields.
	/// </summary>
	public override void _Ready()
	{
		Rebuild();

		DebugLog.Print(
			$"EncounterContentRegistry initialized with " +
			$"{Count} definition(s).");

		foreach (EncounterDefinition definition
			in _definitionsById.Values)
		{
			PrintDefinition(definition);
		}
	}

	/// <summary>
	/// Performs the rebuild operation for Encounter Content Registry.
	/// Uses the current node and service state; any result is applied through side effects, events, or stored fields.
	/// </summary>
	public void Rebuild()
	{
		_definitionsById.Clear();

		if (!GodotObject.IsInstanceValid(MonsterRegistry))
		{
			GD.PushError(
				"EncounterContentRegistry is missing its " +
				"MonsterRegistry Inspector reference.");

			DebugLog.Print(
				"EncounterContentRegistry could not rebuild: " +
				"MonsterRegistry reference is missing.");

			return;
		}

		foreach (EncounterDefinition definition in Definitions)
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
		out EncounterDefinition definition)
	{
		string normalizedId = Normalize(contentId);

		return _definitionsById.TryGetValue(
			normalizedId,
			out definition!);
	}

	/// <summary>
	/// Retrieves required from the current game state.
	/// Uses the supplied arguments and current state and returns the resulting encounter definition to the caller.
	/// </summary>
	public EncounterDefinition GetRequired(
		string contentId)
	{
		if (TryGet(contentId, out EncounterDefinition definition))
			return definition;

		throw new KeyNotFoundException(
			$"No EncounterDefinition is registered for " +
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
	/// Performs the register operation for Encounter Content Registry.
	/// Uses the supplied arguments and current node state; any result is applied through side effects, events, or stored fields.
	/// </summary>
	private void Register(EncounterDefinition definition)
	{
		if (!GodotObject.IsInstanceValid(definition))
		{
			GD.PushError(
				"EncounterContentRegistry contains a missing " +
				"EncounterDefinition.");

			return;
		}

		IReadOnlyList<string> errors =
			definition.GetValidationErrors();

		foreach (EncounterMonsterEntry entry
			in definition.MonsterComposition)
		{
			if (!GodotObject.IsInstanceValid(entry)
				|| string.IsNullOrWhiteSpace(entry.MonsterContentId))
			{
				continue;
			}

			if (!MonsterRegistry.TryGet(
				entry.MonsterContentId,
				out _))
			{
				List<string> combinedErrors = new(errors)
				{
					$"{definition.ContentId}: unknown monster " +
					$"Content ID '{entry.MonsterContentId}'."
				};

				errors = combinedErrors;
			}
		}

		if (errors.Count > 0)
		{
			foreach (string error in errors)
			{
				GD.PushError(error);
				DebugLog.Print($"Encounter content error: {error}");
			}

			return;
		}

		string normalizedId = Normalize(definition.ContentId);

		if (!_definitionsById.TryAdd(normalizedId, definition))
		{
			string message =
				$"Duplicate encounter Content ID " +
				$"'{definition.ContentId}'.";

			GD.PushError(message);
			DebugLog.Print($"Encounter content error: {message}");
		}
	}

	/// <summary>
	/// Performs the print definition operation for Encounter Content Registry.
	/// Uses the supplied arguments and current node state; any result is applied through side effects, events, or stored fields.
	/// </summary>
	private static void PrintDefinition(
		EncounterDefinition definition)
	{
		DebugLog.Print(
			$"Encounter content loaded: " +
			$"{definition.ContentId} " +
			$"('{definition.DisplayName}').");

		foreach (EncounterMonsterEntry entry
			in definition.MonsterComposition)
		{
			DebugLog.Print(
				$"  {entry.MonsterContentId}: " +
				$"{entry.MinimumCount}-{entry.MaximumCount}");
		}
	}

	/// <summary>
	/// Performs the normalize operation for Encounter Content Registry.
	/// Uses the supplied arguments and current state and returns the resulting string to the caller.
	/// </summary>
	private static string Normalize(string contentId)
	{
		return contentId.Trim().ToLowerInvariant();
	}
}
