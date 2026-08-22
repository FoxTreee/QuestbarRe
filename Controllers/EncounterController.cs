using Godot;
using System;
using System.Collections.Generic;

public partial class EncounterController : Node
{
	[Signal]
	public delegate void ActiveMonsterCountChangedEventHandler(
	int activeMonsterCount);

	[ExportCategory("Dependencies")]
	/// <summary>
	/// Inspector reference used by this component for its journey state dependency.
	/// Assign the matching node or resource from the scene; leaving it empty prevents that connection from working.
	/// </summary>
	[Export]
	public JourneyStateService JourneyState { get; set; } = null!;

	/// <summary>
	/// Inspector reference used by this component for its actor layer dependency.
	/// Assign the matching node or resource from the scene; leaving it empty prevents that connection from working.
	/// </summary>
	[Export]
	public Node2D ActorLayer { get; set; } = null!;

	/// <summary>
	/// Inspector reference used by this component for its monster spawn anchor dependency.
	/// Assign the matching node or resource from the scene; leaving it empty prevents that connection from working.
	/// </summary>
	[Export]
	public Node2D MonsterSpawnAnchor { get; set; } = null!;

	/// <summary>
	/// Controls monster factory.
	/// For example, selecting a different value changes which monster factory behavior or content the owning system uses.
	/// </summary>
	[Export]
	public MonsterFactory MonsterFactory { get; set; } = null!;

	/// <summary>
	/// Inspector reference used by this component for its encounter registry dependency.
	/// Assign the matching node or resource from the scene; leaving it empty prevents that connection from working.
	/// </summary>
	[Export]
	public EncounterContentRegistry EncounterRegistry { get; set; } = null!;

	/// <summary>
	/// Inspector reference used by this component for its encounter pool registry dependency.
	/// Assign the matching node or resource from the scene; leaving it empty prevents that connection from working.
	/// </summary>
	[Export]
	public EncounterPoolContentRegistry EncounterPoolRegistry { get; set; } = null!;

	/// <summary>
	/// Awards hero XP exactly once when this controller accepts a monster death.
	/// </summary>
	[Export]
	public HeroExperienceService Experience { get; set; } = null!;

	/// <summary>
	/// Rolls the defeated monster's own loot table exactly once after this
	/// controller accepts and removes that monster from the active roster.
	/// </summary>
	[Export]
	public MonsterLootService Loot { get; set; } = null!;

	[ExportCategory("Monster Spawn Formation")]

	/// <summary>
	/// Controls spawn rows.
	/// For example, changing 4 to 8 doubles this setting's configured contribution to the system.
	/// </summary>
	[Export(PropertyHint.Range, "1,10,1")]
	public int SpawnRows { get; set; } = 4;

	/// <summary>
	/// Controls starting spawn columns.
	/// For example, changing 2 to 4 doubles this setting's configured contribution to the system.
	/// </summary>
	[Export(PropertyHint.Range, "1,20,1")]
	public int StartingSpawnColumns { get; set; } = 2;

	/// <summary>
	/// Controls spawns per column expansion.
	/// For example, changing 6 to 12 doubles this setting's configured contribution to the system.
	/// </summary>
	[Export(PropertyHint.Range, "1,100,1")]
	public int SpawnsPerColumnExpansion { get; set; } = 6;

	/// <summary>
	/// Controls vertical spawn spacing, measured as pixels.
	/// For example, changing 24 to 48 doubles this setting's configured contribution to the system.
	/// </summary>
	[Export(PropertyHint.Range, "0,200,1")]
	public float VerticalSpawnSpacing { get; set; } = 24.0f;

	/// <summary>
	/// Controls horizontal spawn spacing, measured as pixels.
	/// For example, changing 48 to 96 doubles this setting's configured contribution to the system.
	/// </summary>
	[Export(PropertyHint.Range, "0,500,1")]
	public float HorizontalSpawnSpacing { get; set; } = 48.0f;

	[ExportCategory("Encounter Content")]
	/// <summary>
	/// Stable content identifier for default encounter pool; other systems use this value to find the same game data.
	/// For example, changing this ID makes the owning resource resolve a different registered default encounter pool.
	/// </summary>
	[Export]
	public string DefaultEncounterPoolContentId { get; set; } =
		"encounter_pool.core.training_region";

	/// <summary>
	/// Stable content identifier for default encounter; other systems use this value to find the same game data.
	/// For example, changing this ID makes the owning resource resolve a different registered default encounter.
	/// </summary>
	[Export]
	public string DefaultEncounterContentId { get; set; } =
		"encounter.core.training_mix";

	/// <summary>
	/// Stable content identifier for default monster; other systems use this value to find the same game data.
	/// For example, changing this ID makes the owning resource resolve a different registered default monster.
	/// </summary>
	[Export]
	public string DefaultMonsterContentId { get; set; } =
		"monster.core.training_monster";

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

	// Start a registered encounter definition -- DEBUG ONLY
	/// <summary>
	/// Attempts to debug start encounter without throwing when the operation cannot be completed.
	/// Uses the supplied arguments and current state and returns the resulting bool to the caller.
	/// </summary>
	public bool TryDebugStartEncounter(
		string contentId,
		out string result)
	{
		result = string.Empty;

		if (JourneyState.CurrentState
			== JourneyStateService.JourneyState.Encounter)
		{
			result =
				"An encounter is already active. " +
				"Use .endEncounter before starting another.";

			return false;
		}

		if (!EncounterRegistry.TryGet(
			contentId,
			out EncounterDefinition definition))
		{
			result =
				$"Unknown encounter Content ID '{contentId}'.";

			return false;
		}

		if (!TryRollEncounterComposition(
			definition,
			out List<(EncounterMonsterEntry Entry, int Count)> rolledComposition,
			out int totalMonsterCount,
			out result))
		{
			return false;
		}

		_suppressAutomaticEncounterSpawn = true;

		try
		{
			JourneyState.BeginEncounter();
		}
		finally
		{
			_suppressAutomaticEncounterSpawn = false;
		}

		if (!TrySpawnRolledComposition(
			definition,
			rolledComposition,
			null,
			out int successfullySpawned,
			out result))
		{
			JourneyState.EndEncounter();
			return false;
		}

		DebugLog.Print(
			$"Encounter definition started: " +
			$"{definition.ContentId}. " +
			$"Active monsters={_activeMonsters.Count}");

		result =
			$"Started {definition.ContentId} with " +
			$"{successfullySpawned} monster(s).";

		return true;
	}


	// Start a registered encounter pool -- DEBUG ONLY
	/// <summary>
	/// Attempts to debug start encounter pool without throwing when the operation cannot be completed.
	/// Uses the supplied arguments and current state and returns the resulting bool to the caller.
	/// </summary>
	public bool TryDebugStartEncounterPool(
		string poolContentId,
		out string result)
	{
		return TryStartEncounterPool(poolContentId, out result);
	}

	/// <summary>
	/// Attempts to start encounter pool without throwing when the operation cannot be completed.
	/// Uses the supplied arguments and current state and returns the resulting bool to the caller.
	/// </summary>
	public bool TryStartEncounterPool(
		string poolContentId,
		out string result)
	{
		return TryStartEncounterPool(
			poolContentId,
			0.0f,
			null,
			out result);
	}

	/// <summary>
	/// Starts a regional encounter using the current travel availability and a
	/// fixed difficulty snapshot shared by every monster in the encounter.
	/// </summary>
	public bool TryStartEncounterPool(
		string poolContentId,
		float regionTravelProgress,
		MonsterDifficultySnapshot? difficulty,
		out string result)
	{
		result = string.Empty;

		if (JourneyState.CurrentState
			== JourneyStateService.JourneyState.Encounter)
		{
			result =
				"An encounter is already active. " +
				"Use .endEncounter before starting another.";

			return false;
		}

		if (!EncounterPoolRegistry.TryGet(
			poolContentId,
			out EncounterPoolDefinition pool))
		{
			result =
				$"Unknown encounter pool Content ID '{poolContentId}'.";

			return false;
		}

		if (!TrySelectEncounterFromPool(
			pool,
			regionTravelProgress,
			out EncounterPoolEntry selectedEntry,
			out int roll,
			out int eligibleWeight,
			out result))
		{
			return false;
		}

		DebugLog.Print(
			$"Encounter selection: {pool.DisplayName} " +
			$"({pool.ContentId})");

		DebugLog.Print(
			$"  Roll: {roll} / {eligibleWeight}");

		DebugLog.Print(
			$"  Selected: {selectedEntry.EncounterContentId}");

		DebugLog.Print(
			$"  Weight: {selectedEntry.Weight}");

		if (!EncounterRegistry.TryGet(
			selectedEntry.EncounterContentId,
			out EncounterDefinition definition))
		{
			result =
				$"Encounter pool '{pool.ContentId}' selected unknown " +
				$"encounter '{selectedEntry.EncounterContentId}'.";

			return false;
		}

		if (!TryRollEncounterComposition(
			definition,
			out List<(EncounterMonsterEntry Entry, int Count)> rolledComposition,
			out _,
			out result))
		{
			return false;
		}

		_suppressAutomaticEncounterSpawn = true;

		try
		{
			JourneyState.BeginEncounter();
		}
		finally
		{
			_suppressAutomaticEncounterSpawn = false;
		}

		if (!TrySpawnRolledComposition(
			definition,
			rolledComposition,
			difficulty,
			out int successfullySpawned,
			out result))
		{
			JourneyState.EndEncounter();
			return false;
		}

		DebugLog.Print(
			$"Encounter pool started: {pool.ContentId}. " +
			$"Selected={definition.ContentId}. " +
			$"Active monsters={_activeMonsters.Count}");

		result =
			$"Started pool {pool.ContentId}; selected " +
			$"{definition.ContentId} with {successfullySpawned} monster(s)" +
			(difficulty is null
				? "."
				: $" at level {difficulty.MonsterLevel}, " +
					$"health x{difficulty.HealthMultiplier:0.###}, " +
					$"damage x{difficulty.DamageMultiplier:0.###}.");

		return true;
	}

	/// <summary>
	/// Attempts to roll encounter composition without throwing when the operation cannot be completed.
	/// Uses the supplied arguments and current state and returns the resulting bool to the caller.
	/// </summary>
	private bool TryRollEncounterComposition(
		EncounterDefinition definition,
		out List<(EncounterMonsterEntry Entry, int Count)> rolledComposition,
		out int totalMonsterCount,
		out string result)
	{
		rolledComposition = new();
		totalMonsterCount = 0;
		result = string.Empty;

		foreach (EncounterMonsterEntry entry
			in definition.MonsterComposition)
		{
			int count =
				_spawnRandom.RandiRange(
					entry.MinimumCount,
					entry.MaximumCount);

			rolledComposition.Add((entry, count));
			totalMonsterCount += count;
		}

		if (totalMonsterCount == 0)
		{
			result =
				$"Encounter '{definition.ContentId}' rolled zero monsters.";

			return false;
		}

		DebugLog.Print(
			$"Encounter roll: {definition.DisplayName} " +
			$"({definition.ContentId})");

		foreach ((EncounterMonsterEntry entry, int count)
			in rolledComposition)
		{
			DebugLog.Print(
				$"  {entry.MonsterContentId}: {count}");
		}

		return true;
	}

	/// <summary>
	/// Attempts to spawn rolled composition without throwing when the operation cannot be completed.
	/// Uses the supplied arguments and current state and returns the resulting bool to the caller.
	/// </summary>
	private bool TrySpawnRolledComposition(
		EncounterDefinition definition,
		IReadOnlyList<(EncounterMonsterEntry Entry, int Count)> rolledComposition,
		MonsterDifficultySnapshot? difficulty,
		out int successfullySpawned,
		out string result)
	{
		successfullySpawned = 0;
		result = string.Empty;

		foreach ((EncounterMonsterEntry entry, int count)
			in rolledComposition)
		{
			for (int i = 0; i < count; i++)
			{
				MonsterActorController? monster =
					SpawnMonster(entry.MonsterContentId, difficulty);

				if (monster is null)
				{
					DebugLog.Print(
						$"Encounter '{definition.ContentId}' failed " +
						$"while spawning {entry.MonsterContentId}.");

					result =
						$"Encounter start failed after spawning " +
						$"{successfullySpawned} monster(s).";

					return false;
				}

				successfullySpawned++;
			}
		}

		return true;
	}

	// Spawn monsters -- DEBUG ONLY
	/// <summary>
	/// Performs the debug spawn monsters operation for Encounter Controller.
	/// Uses the supplied arguments and current node state; any result is applied through side effects, events, or stored fields.
	/// </summary>
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

		DebugLog.Print(
			$"Debug ensured {validCount} active monster(s). " +
			$"Active monsters={_activeMonsters.Count}");
	}

	/// <summary>
	/// Performs the debug add monsters operation for Encounter Controller.
	/// Uses the supplied arguments and current node state; any result is applied through side effects, events, or stored fields.
	/// </summary>
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

		DebugLog.Print(
			$"Debug added {validCount} monster(s). " +
			$"Active monsters={_activeMonsters.Count}");
	}

	//DEBUG ONLY
	/// <summary>
	/// Performs the debug add monsters operation for Encounter Controller.
	/// Uses the supplied arguments and current state and returns the resulting int to the caller.
	/// </summary>
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

	/// <summary>
	/// Runs Godot setup for Encounter Controller when the node enters the scene tree.
	/// Uses the current node and service state; any result is applied through side effects, events, or stored fields.
	/// </summary>
	public override void _Ready()
	{
		if (!ValidateReferences())
			return;

		_spawnRandom.Randomize();

		JourneyState.StateChanged += OnJourneyStateChanged;

		ApplyJourneyState(JourneyState.CurrentState);
	}

	/// <summary>
	/// Cleans up Encounter Controller when the node leaves the scene tree.
	/// Uses the current node and service state; any result is applied through side effects, events, or stored fields.
	/// </summary>
	public override void _ExitTree()
	{
		if (GodotObject.IsInstanceValid(JourneyState))
		{
			JourneyState.StateChanged -=
				OnJourneyStateChanged;
		}
	}

	/// <summary>
	/// Handles the journey state changed event and updates the related game state.
	/// Uses the supplied arguments and current node state; any result is applied through side effects, events, or stored fields.
	/// </summary>
	private void OnJourneyStateChanged(
		JourneyStateService.JourneyState previousState,
		JourneyStateService.JourneyState currentState)
	{
		ApplyJourneyState(currentState);
	}

	/// <summary>
	/// Applies journey state to the relevant actor, resource, or presentation state.
	/// Uses the supplied arguments and current node state; any result is applied through side effects, events, or stored fields.
	/// </summary>
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

	/// <summary>
	/// Resets spawn formation so the system can begin from a clean state.
	/// Uses the current node and service state; any result is applied through side effects, events, or stored fields.
	/// </summary>
	private void ResetSpawnFormation()
	{
		_spawnSequenceCount = 0;
		_usedSpawnSlots.Clear();

		DebugLog.Print(
			"Monster spawn formation reset.");
	}

	/// <summary>
	/// Performs the begin encounter presentation operation for Encounter Controller.
	/// Uses the current node and service state; any result is applied through side effects, events, or stored fields.
	/// </summary>
	private void BeginEncounterPresentation()
	{
		RemoveInvalidMonsterReferences();

		if (_activeMonsters.Count > 0)
			return;

		ResetSpawnFormation();

		if (!_suppressAutomaticEncounterSpawn)
		{
			StartDefaultJourneyEncounter();
		}

		EncounterStarted?.Invoke();
	}

	/// <summary>
	/// Performs the start default journey encounter operation for Encounter Controller.
	/// Uses the current node and service state; any result is applied through side effects, events, or stored fields.
	/// </summary>
	private void StartDefaultJourneyEncounter()
	{
		EncounterDefinition? selectedDefinition = null;

		if (EncounterPoolRegistry.TryGet(
			DefaultEncounterPoolContentId,
			out EncounterPoolDefinition pool))
		{
			if (TrySelectEncounterFromPool(
				pool,
				0.0f,
				out EncounterPoolEntry selectedEntry,
				out int roll,
				out int eligibleWeight,
				out string selectionError))
			{
				DebugLog.Print(
					$"Encounter selection: {pool.DisplayName} " +
					$"({pool.ContentId})");

				DebugLog.Print(
					$"  Roll: {roll} / {eligibleWeight}");

				DebugLog.Print(
					$"  Selected: {selectedEntry.EncounterContentId}");

				DebugLog.Print(
					$"  Weight: {selectedEntry.Weight}");

				if (EncounterRegistry.TryGet(
					selectedEntry.EncounterContentId,
					out EncounterDefinition resolvedDefinition))
				{
					selectedDefinition = resolvedDefinition;
				}
				else
				{
					DebugLog.Print(
						$"Selected encounter " +
						$"'{selectedEntry.EncounterContentId}' " +
						"could not be resolved. Falling back to " +
						$"{DefaultEncounterContentId}.");
				}
			}
			else
			{
				DebugLog.Print(
					$"Encounter pool selection failed: " +
					$"{selectionError} Falling back to " +
					$"{DefaultEncounterContentId}.");
			}
		}
		else
		{
			DebugLog.Print(
				$"Default encounter pool " +
				$"'{DefaultEncounterPoolContentId}' is not registered. " +
				$"Falling back to {DefaultEncounterContentId}.");
		}

		if (selectedDefinition is not null)
		{
			StartJourneyEncounterDefinition(selectedDefinition);
			return;
		}

		if (!EncounterRegistry.TryGet(
			DefaultEncounterContentId,
			out EncounterDefinition fallbackDefinition))
		{
			DebugLog.Print(
				$"Default encounter '{DefaultEncounterContentId}' " +
				$"is not registered. Falling back to " +
				$"{DefaultMonsterContentId}.");

			SpawnMonster(DefaultMonsterContentId);
			return;
		}

		StartJourneyEncounterDefinition(fallbackDefinition);
	}

	/// <summary>
	/// Attempts to select encounter from pool without throwing when the operation cannot be completed.
	/// Uses the supplied arguments and current state and returns the resulting bool to the caller.
	/// </summary>
	private bool TrySelectEncounterFromPool(
		EncounterPoolDefinition pool,
		float regionTravelProgress,
		out EncounterPoolEntry selectedEntry,
		out int roll,
		out int eligibleWeight,
		out string result)
	{
		selectedEntry = null!;
		roll = 0;
		eligibleWeight = 0;
		result = string.Empty;

		eligibleWeight = pool.GetEligibleWeight(regionTravelProgress);

		if (eligibleWeight <= 0)
		{
			result =
				$"Encounter pool '{pool.ContentId}' has no eligible " +
				$"encounters at {regionTravelProgress * 100.0f:0.##}% " +
				"regional travel progress.";

			return false;
		}

		roll = _spawnRandom.RandiRange(1, eligibleWeight);
		int cumulativeWeight = 0;

		foreach (EncounterPoolEntry entry in pool.Entries)
		{
			if (!GodotObject.IsInstanceValid(entry)
				|| entry.Weight <= 0
				|| !entry.IsAvailableAtRegionTravelProgress(
					regionTravelProgress))
			{
				continue;
			}

			cumulativeWeight += entry.Weight;

			if (roll <= cumulativeWeight)
			{
				selectedEntry = entry;
				return true;
			}
		}

		result =
			$"Encounter pool '{pool.ContentId}' could not map " +
			$"roll {roll} to an entry.";

		return false;
	}

	/// <summary>
	/// Performs the start journey encounter definition operation for Encounter Controller.
	/// Uses the supplied arguments and current node state; any result is applied through side effects, events, or stored fields.
	/// </summary>
	private void StartJourneyEncounterDefinition(
		EncounterDefinition definition)
	{
		if (!TryRollEncounterComposition(
			definition,
			out List<(EncounterMonsterEntry Entry, int Count)> rolledComposition,
			out _,
			out string result))
		{
			DebugLog.Print(
				$"Journey encounter could not roll: {result} " +
				$"Falling back to {DefaultMonsterContentId}.");

			SpawnMonster(DefaultMonsterContentId);
			return;
		}

		if (!TrySpawnRolledComposition(
			definition,
			rolledComposition,
			null,
			out int successfullySpawned,
			out result))
		{
			DebugLog.Print(
				$"Journey encounter spawn failed: {result}");
			return;
		}

		DebugLog.Print(
			$"Journey encounter definition started: " +
			$"{definition.ContentId}. " +
			$"Active monsters={successfullySpawned}");
	}

	/// <summary>
	/// Retrieves next random grid spawn position from the current game state.
	/// Reads the current state and returns the resulting vector2 to the caller.
	/// </summary>
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

			DebugLog.Print(
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

		DebugLog.Print(
			$"Spawn slot selected: " +
			$"Column={selectedSlot.X}, " +
			$"Row={selectedSlot.Y}, " +
			$"Position={spawnPosition}, " +
			$"UnlockedColumns={availableColumns}.");

		return spawnPosition;
	}

	/// <summary>
	/// Performs the spawn monster operation for Encounter Controller.
	/// Uses the supplied arguments and current state and returns the resulting monster actor controller to the caller.
	/// </summary>
	private MonsterActorController? SpawnMonster(
		string contentId,
		MonsterDifficultySnapshot? difficulty = null)
	{
		if (!MonsterFactory.TryCreate(
			contentId,
			difficulty,
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

		DebugLog.Print(
			$"{monster.Name} spawned from " +
			$"{monster.ContentId}. " +
			$"Active monsters={_activeMonsters.Count}");

		return monster;
	}

	/// <summary>
	/// Performs the end encounter presentation operation for Encounter Controller.
	/// Uses the current node and service state; any result is applied through side effects, events, or stored fields.
	/// </summary>
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

		DebugLog.Print(
			"Encounter monsters removed. " +
			"Active monsters=0");
	}

	/// <summary>
	/// Performs the complete encounter operation for Encounter Controller.
	/// Uses the current node and service state; any result is applied through side effects, events, or stored fields.
	/// </summary>
	private void CompleteEncounter()
	{
		DebugLog.Print(
		"Encounter completed. Returning journey to Traveling.");

		EncounterCompleted?.Invoke();
		JourneyState.EndEncounter();
	}

	/// <summary>
	/// Performs the end encounter as defeat operation for Encounter Controller.
	/// Uses the current node and service state; any result is applied through side effects, events, or stored fields.
	/// </summary>
	public void EndEncounterAsDefeat()
	{
		if (JourneyState.CurrentState
			!= JourneyStateService.JourneyState.Encounter)
		{
			return;
		}

		DebugLog.Print(
			"Encounter ended in defeat. " +
			"Returning journey to Traveling.");

		JourneyState.EndEncounter();
	}

	/// <summary>
	/// Handles the monster died event and updates the related game state.
	/// Uses the supplied arguments and current node state; any result is applied through side effects, events, or stored fields.
	/// </summary>
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

		if (GodotObject.IsInstanceValid(monster.Definition))
		{
			Experience.AwardMonsterDefeat(monster);
			Loot.AwardMonsterDefeat(monster);
		}

		EmitActiveMonsterCountChanged();

		DebugLog.Print(
			$"{monster.Name} removed from encounter. " +
			$"Active monsters={_activeMonsters.Count}");

		monster.QueueFree();

		if (_activeMonsters.Count == 0)
		{
			CompleteEncounter();
		}
	}

	/// <summary>
	/// Performs the remove invalid monster references operation for Encounter Controller.
	/// Uses the current node and service state; any result is applied through side effects, events, or stored fields.
	/// </summary>
	private void RemoveInvalidMonsterReferences()
	{
		_activeMonsters.RemoveAll(
			monster =>
				!GodotObject.IsInstanceValid(monster));
	}

	/// <summary>
	/// Performs the validate references operation for Encounter Controller.
	/// Reads the current state and returns the resulting bool to the caller.
	/// </summary>
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

		valid &= Require(
			EncounterRegistry,
			nameof(EncounterRegistry));

		valid &= Require(
			EncounterPoolRegistry,
			nameof(EncounterPoolRegistry));

		valid &= Require(
			Experience,
			nameof(Experience));

		valid &= Require(
			Loot,
			nameof(Loot));

		if (string.IsNullOrWhiteSpace(DefaultEncounterPoolContentId))
		{
			GD.PushError(
				"EncounterController requires a default " +
				"encounter pool Content ID.");

			valid = false;
		}
		else if (
			GodotObject.IsInstanceValid(EncounterPoolRegistry)
			&& !EncounterPoolRegistry.TryGet(
				DefaultEncounterPoolContentId,
				out _))
		{
			GD.PushError(
				$"EncounterController's default encounter pool " +
				$"Content ID '{DefaultEncounterPoolContentId}' " +
				"is not registered.");

			valid = false;
		}

		if (string.IsNullOrWhiteSpace(DefaultEncounterContentId))
		{
			GD.PushError(
				"EncounterController requires a default " +
				"encounter Content ID.");

			valid = false;
		}
		else if (
			GodotObject.IsInstanceValid(EncounterRegistry)
			&& !EncounterRegistry.TryGet(
				DefaultEncounterContentId,
				out _))
		{
			GD.PushError(
				$"EncounterController's default encounter " +
				$"Content ID '{DefaultEncounterContentId}' " +
				$"is not registered.");

			valid = false;
		}

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

	/// <summary>
	/// Performs the emit active monster count changed operation for Encounter Controller.
	/// Uses the current node and service state; any result is applied through side effects, events, or stored fields.
	/// </summary>
	private void EmitActiveMonsterCountChanged()
	{
		EmitSignal(
			SignalName.ActiveMonsterCountChanged,
			_activeMonsters.Count);

		MonsterRosterChanged?.Invoke(
			_activeMonsters.Count);
	}

	/// <summary>
	/// Performs the require operation for Encounter Controller.
	/// Uses the supplied arguments and current state and returns the resulting bool to the caller.
	/// </summary>
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
