using Godot;
using System;
using System.Collections.Generic;

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
	public MonsterFactory MonsterFactory { get; set; } = null!;

	[ExportCategory("Monster Spawn Formation")]

	[Export(PropertyHint.Range, "1,10,1")]
	public int SpawnRows { get; set; } = 4;

	[Export(PropertyHint.Range, "1,20,1")]
	public int StartingSpawnColumns { get; set; } = 2;

	[Export(PropertyHint.Range, "1,100,1")]
	public int SpawnsPerColumnExpansion { get; set; } = 6;

	[Export(PropertyHint.Range, "0,200,1")]
	public float VerticalSpawnSpacing { get; set; } = 24.0f;

	[Export(PropertyHint.Range, "0,500,1")]
	public float HorizontalSpawnSpacing { get; set; } = 48.0f;

	[ExportCategory("Encounter Content")]
	[Export]
	public string DefaultMonsterContentId{ get; set; } = "monster.core.training_monster";

	public IReadOnlyList<MonsterActorController> ActiveMonsters => _activeMonsters;

	private readonly List<MonsterActorController> _activeMonsters = new();
	private int _nextMonsterDebugId = 1;

	private readonly RandomNumberGenerator _spawnRandom = new();

	private readonly HashSet<Vector2I> _usedSpawnSlots = new();

	private int _spawnSequenceCount;
	private bool _suppressAutomaticEncounterSpawn;

	public event Action? EncounterStarted;
	public event Action? EncounterCompleted;
	public event Action<int>? MonsterRosterChanged;
	public int ActiveMonsterCount => _activeMonsters.Count;

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
			SpawnMonster(DefaultMonsterContentId);
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
			SpawnMonster(DefaultMonsterContentId);
		}

		GD.Print(
			$"Debug added {validCount} monster(s). " +
			$"Active monsters={_activeMonsters.Count}");
	}

	//DEBUG ONLY
	public int DebugAddMonsters(
	string contentId,
	int count)
	{
		int validCount = Mathf.Clamp(count, 1, 100);
		int successfullySpawned = 0;

		if (JourneyState.CurrentState
			!= JourneyStateService.JourneyState.Encounter)
		{
			_suppressAutomaticEncounterSpawn = true;

			try
			{
				JourneyState.BeginEncounter();
			}
			finally
			{
				_suppressAutomaticEncounterSpawn = false;
			}
		}

		while (successfullySpawned < validCount)
		{
			MonsterActorController? monster =
				SpawnMonster(contentId);

			if (monster is null)
				break;

			successfullySpawned++;
		}

		return successfullySpawned;
	}

	public override void _Ready()
	{
		if (!ValidateReferences())
			return;

		_spawnRandom.Randomize();

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

	private void ResetSpawnFormation()
	{
		_spawnSequenceCount = 0;
		_usedSpawnSlots.Clear();

		GD.Print(
			"Monster spawn formation reset.");
	}

	private void BeginEncounterPresentation()
	{
		RemoveInvalidMonsterReferences();

		if (_activeMonsters.Count > 0)
			return;

		ResetSpawnFormation();

		if (!_suppressAutomaticEncounterSpawn)
		{
			SpawnMonster(
				DefaultMonsterContentId);
		}

		EncounterStarted?.Invoke();
	}

	private Vector2 GetNextRandomGridSpawnPosition()
	{
		_spawnSequenceCount++;

		int expansionInterval =
			Mathf.Max(
				SpawnsPerColumnExpansion,
				1);

		int availableColumns =
			Mathf.Max(
				StartingSpawnColumns,
				1)
			+ (_spawnSequenceCount
				/ expansionInterval);

		int rows =
			Mathf.Max(
				SpawnRows,
				1);

		List<Vector2I> availableSlots =
			new();

		for (int column = 0;
			column < availableColumns;
			column++)
		{
			for (int row = 0;
				row < rows;
				row++)
			{
				Vector2I slot =
					new(
						column,
						row);

				if (!_usedSpawnSlots.Contains(slot))
				{
					availableSlots.Add(slot);
				}
			}
		}

		if (availableSlots.Count == 0)
		{
			_usedSpawnSlots.Clear();

			for (int column = 0;
				column < availableColumns;
				column++)
			{
				for (int row = 0;
					row < rows;
					row++)
				{
					availableSlots.Add(
						new Vector2I(
							column,
							row));
				}
			}

			GD.Print(
				"Monster spawn grid exhausted. " +
				"Spawn slot shuffle bag reset.");
		}

		int randomIndex =
			_spawnRandom.RandiRange(
				0,
				availableSlots.Count - 1);

		Vector2I selectedSlot =
			availableSlots[randomIndex];

		_usedSpawnSlots.Add(
			selectedSlot);

		float centeredRow =
			selectedSlot.Y
			- ((rows - 1) / 2.0f);

		Vector2 offset =
			new(
				-selectedSlot.X
					* HorizontalSpawnSpacing,

				centeredRow
					* VerticalSpawnSpacing);

		Vector2 spawnPosition =
			MonsterSpawnAnchor.GlobalPosition
			+ offset;

		GD.Print(
			$"Spawn slot selected: " +
			$"Column={selectedSlot.X}, " +
			$"Row={selectedSlot.Y}, " +
			$"Position={spawnPosition}, " +
			$"UnlockedColumns={availableColumns}.");

		return spawnPosition;
	}

	private MonsterActorController? SpawnMonster(string contentId)
	{
		if (!MonsterFactory.TryCreate(
			contentId,
			out MonsterActorController monster,
			out string error))
		{
			GD.PushError(error);
			return null;
		}

		monster.Name =
			$"{monster.DisplayName.Replace(" ", string.Empty)}" +
			$"{_nextMonsterDebugId++}";

		ActorLayer.AddChild(monster);

		monster.GlobalPosition =
			GetNextRandomGridSpawnPosition();

		_activeMonsters.Add(monster);

		monster.Died +=
			OnMonsterDied;

		EmitActiveMonsterCountChanged();

		GD.Print(
			$"{monster.Name} spawned from " +
			$"{monster.ContentId}. " +
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

		EncounterCompleted?.Invoke();
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
			MonsterFactory,
			nameof(MonsterFactory));

		if (string.IsNullOrWhiteSpace(DefaultMonsterContentId))
		{
			GD.PushError( "EncounterController requires a default " + "monster Content ID.");

			valid = false;
		}
		else if (
			GodotObject.IsInstanceValid(MonsterFactory)
			&& !MonsterFactory.Registry.TryGet(
				DefaultMonsterContentId,
				out _))
		{
			GD.PushError(
				$"EncounterController's default monster " +
				$"Content ID '{DefaultMonsterContentId}' " +
				$"is not registered.");

			valid = false;
		}

		if (SpawnRows < 1)
		{
			GD.PushError(
				"EncounterController requires at least " +
				"one monster spawn row.");

			valid = false;
		}

		if (StartingSpawnColumns < 1)
		{
			GD.PushError(
				"EncounterController requires at least " +
				"one starting spawn column.");

			valid = false;
		}

		if (SpawnsPerColumnExpansion < 1)
		{
			GD.PushError(
				"EncounterController's spawn expansion " +
				"interval must be at least one.");

			valid = false;
		}

		if (VerticalSpawnSpacing < 0.0f
			|| HorizontalSpawnSpacing < 0.0f)
		{
			GD.PushError(
				"Monster spawn spacing cannot be negative.");

			valid = false;
		}

		return valid;
	}

	private void EmitActiveMonsterCountChanged()
	{
		EmitSignal(
			SignalName.ActiveMonsterCountChanged,
			_activeMonsters.Count);

		MonsterRosterChanged?.Invoke(
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
