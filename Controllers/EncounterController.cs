using System.Collections.Generic;
using Godot;

public partial class EncounterController : Node
{
	[Signal]
	public delegate void ActiveMonsterCountChangedEventHandler(
	int activeMonsterCount);
	
	[ExportCategory("Dependencies")]
	[Export]
	public JourneyStateService JourneyState { get; set; } = null!;

	[Export]
	public Node2D ActorLayer { get; set; } = null!;

	[Export]
	public Node2D MonsterSpawnAnchor { get; set; } = null!;

	[Export]
	public Node2D MonsterEntryAnchor { get; set; } = null!;

	[ExportCategory("Encounter Content")]
	[Export]
	public PackedScene MonsterScene { get; set; } = null!;

	private readonly List<MonsterActorController> _activeMonsters = new();

	public IReadOnlyList<MonsterActorController> ActiveMonsters =>
		_activeMonsters;

	public int ActiveMonsterCount =>
		_activeMonsters.Count;

	// Spawn monsters -- DEBUG ONLY
	public void DebugSpawnMonsters(int count)
	{
		int validCount =
			Mathf.Clamp(count, 1, 100);

		if (JourneyState.CurrentState
			!= JourneyStateService.JourneyState.Encounter)
		{
			JourneyState.BeginEncounter();
		}

		RemoveInvalidMonsterReferences();

		int monstersToAdd =
			Mathf.Max(
				validCount - _activeMonsters.Count,
				0);

		for (int i = 0; i < monstersToAdd; i++)
		{
			SpawnTestMonster();
		}

		GD.Print(
			$"Debug ensured {validCount} active monster(s). " +
			$"Active monsters={_activeMonsters.Count}");
	}

	public void DebugAddMonsters(int count)
	{
		int validCount =
			Mathf.Clamp(count, 1, 100);

		int countBeforeTransition =
			_activeMonsters.Count;

		if (JourneyState.CurrentState
			!= JourneyStateService.JourneyState.Encounter)
		{
			JourneyState.BeginEncounter();
		}

		int automaticallyAdded =
			_activeMonsters.Count
			- countBeforeTransition;

		int remainingToAdd =
			Mathf.Max(
				validCount - automaticallyAdded,
				0);

		for (int i = 0; i < remainingToAdd; i++)
		{
			SpawnTestMonster();
		}

		GD.Print(
			$"Debug added {validCount} monster(s). " +
			$"Active monsters={_activeMonsters.Count}");
	}

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
		RemoveInvalidMonsterReferences();

		if (_activeMonsters.Count > 0)
			return;

		SpawnTestMonster();
	}

	private MonsterActorController SpawnTestMonster()
	{
		MonsterActorController monster = 
		MonsterScene.Instantiate<MonsterActorController>();

		monster.Name = $"MonsterActor{_activeMonsters.Count + 1}";

ActorLayer.AddChild(monster);

		ActorLayer.AddChild(monster);

		monster.InitializeEntrance(
			MonsterSpawnAnchor.GlobalPosition,
			MonsterEntryAnchor.GlobalPosition);

		_activeMonsters.Add(monster);

		monster.Died += OnMonsterDied;

		EmitActiveMonsterCountChanged();


		GD.Print(
			$"Monster added to encounter. " +
			$"Active monsters={_activeMonsters.Count}");
		return monster;
	}

	private void EndEncounterPresentation()
	{
		RemoveInvalidMonsterReferences();

		foreach (
			MonsterActorController monster
			in _activeMonsters)
		{
			if (!GodotObject.IsInstanceValid(monster))
				continue;

			monster.Died -= OnMonsterDied;

			monster.QueueFree();
		}

		_activeMonsters.Clear();
		EmitActiveMonsterCountChanged();

		GD.Print(
			"Encounter monsters removed. " +
			"Active monsters=0");
	}

	private void CompleteEncounter()
	{
		GD.Print(
		"Encounter completed. Returning journey to Traveling.");

		// Use the same JourneyStateService transition call
		// currently used by your E-key test to enter Traveling.

		JourneyState.EndEncounter();
	}

	private void OnMonsterDied(
	MonsterActorController monster)
	{
		if (!GodotObject.IsInstanceValid(monster))
			return;

		bool wasRemoved =
			_activeMonsters.Remove(monster);

		if (!wasRemoved)
			return;

		monster.Died -=
			OnMonsterDied;

		EmitActiveMonsterCountChanged();

		GD.Print(
			$"{monster.Name} removed from encounter. " +
			$"Active monsters={_activeMonsters.Count}");

		monster.QueueFree();

		if (_activeMonsters.Count == 0)
		{
			CompleteEncounter();
		}
	}

	private void RemoveInvalidMonsterReferences()
	{
		_activeMonsters.RemoveAll(
			monster =>
				!GodotObject.IsInstanceValid(monster));
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
			MonsterEntryAnchor,
			nameof(MonsterEntryAnchor));

		valid &= Require(
			MonsterScene,
			nameof(MonsterScene));

		return valid;
	}

	private void EmitActiveMonsterCountChanged()
	{
		EmitSignal(
			SignalName.ActiveMonsterCountChanged,
			_activeMonsters.Count);
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
