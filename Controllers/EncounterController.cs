using Godot;

public partial class EncounterController : Node
{
	[ExportCategory("Dependencies")]
	[Export]
	public JourneyStateService JourneyState { get; set; } = null!;

	[Export]
	public Node2D ActorLayer { get; set; } = null!;

	[Export]
	public Node2D MonsterSpawnAnchor { get; set; } = null!;

	[ExportCategory("Encounter Content")]
	[Export]
	public PackedScene MonsterScene { get; set; } = null!;

	private Node2D? _activeMonster;

	public override void _Ready()
	{
		if (!ValidateReferences())
			return;

		JourneyState.StateChanged += OnJourneyStateChanged;

		ApplyJourneyState(JourneyState.CurrentState);
	}

	public override void _ExitTree()
	{
		if (GodotObject.IsInstanceValid(JourneyState))
		{
			JourneyState.StateChanged -=
				OnJourneyStateChanged;
		}
	}

	private void OnJourneyStateChanged(
		JourneyStateService.JourneyState previousState,
		JourneyStateService.JourneyState currentState)
	{
		ApplyJourneyState(currentState);
	}

	private void ApplyJourneyState(
		JourneyStateService.JourneyState state)
	{
		if (state
			== JourneyStateService.JourneyState.Encounter)
		{
			BeginEncounterPresentation();
			return;
		}

		EndEncounterPresentation();
	}

	private void BeginEncounterPresentation()
	{
		if (GodotObject.IsInstanceValid(_activeMonster))
			return;

		_activeMonster =
			MonsterScene.Instantiate<Node2D>();

		ActorLayer.AddChild(_activeMonster);

		_activeMonster.GlobalPosition =
			MonsterSpawnAnchor.GlobalPosition;

		GD.Print(
			$"Monster instantiated at " +
			$"{_activeMonster.GlobalPosition}.");
	}

	private void EndEncounterPresentation()
	{
		if (!GodotObject.IsInstanceValid(_activeMonster))
			return;

		_activeMonster.QueueFree();
		_activeMonster = null;

		GD.Print("Active monster removed.");
	}

	private bool ValidateReferences()
	{
		bool valid = true;

		valid &= Require(
			JourneyState,
			nameof(JourneyState));

		valid &= Require(
			ActorLayer,
			nameof(ActorLayer));

		valid &= Require(
			MonsterSpawnAnchor,
			nameof(MonsterSpawnAnchor));

		valid &= Require(
			MonsterScene,
			nameof(MonsterScene));

		return valid;
	}

	private static bool Require(
		GodotObject value,
		string propertyName)
	{
		if (GodotObject.IsInstanceValid(value))
			return true;

		GD.PushError(
			$"EncounterController is missing the " +
			$"Inspector reference '{propertyName}'.");

		return false;
	}
}
