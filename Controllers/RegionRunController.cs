using Godot;
using System.Collections.Generic;
using System.Linq;

public partial class RegionRunController : Node
{
	[ExportCategory("Dependencies")]
	/// <summary>
	/// Inspector reference used by this component for its encounter dependency.
	/// Assign the matching node or resource from the scene; leaving it empty prevents that connection from working.
	/// </summary>
	[Export]
	public EncounterController Encounter { get; set; } = null!;

	/// <summary>
	/// Inspector reference used by this component for its combat dependency.
	/// Assign the matching node or resource from the scene; leaving it empty prevents that connection from working.
	/// </summary>
	[Export]
	public CombatController Combat { get; set; } = null!;

	/// <summary>
	/// Inspector reference used by this component for its journey state dependency.
	/// Assign the matching node or resource from the scene; leaving it empty prevents that connection from working.
	/// </summary>
	[Export]
	public JourneyStateService JourneyState { get; set; } = null!;

	/// <summary>
	/// Supplies the persisted travel depth used to scale each new encounter.
	/// Combat time and destination excursions are already excluded by this service.
	/// </summary>
	[Export]
	public RegionExplorationService Exploration { get; set; } = null!;

	/// <summary>
	/// Supplies the inventory item consumed by an immediate field revival.
	/// Graveyard revival never consumes this item.
	/// </summary>
	[Export]
	public BackpackWindowController Backpack { get; set; } = null!;

	/// <summary>
	/// Inspector reference used by this component for its region presentation dependency.
	/// Assign the matching node or resource from the scene; leaving it empty prevents that connection from working.
	/// </summary>
	[Export]
	public RegionPresentationController RegionPresentation
	{
		get;
		set;
	} = null!;

	[ExportCategory("Region Run")]
	/// <summary>
	/// Inspector reference used by this component for its active region dependency.
	/// Assign the matching node or resource from the scene; leaving it empty prevents that connection from working.
	/// </summary>
	[Export]
	public RegionDefinition ActiveRegion { get; set; } = null!;

	/// <summary>
	/// Enables or disables auto start.
	/// For example, turn this on to enable auto start, or off to suppress that behavior.
	/// </summary>
	[Export]
	public bool AutoStart { get; set; } = true;

	[ExportCategory("Immediate Revival")]

	[Export(PropertyHint.PlaceholderText, "material.core.test_dust")]
	public string ImmediateReviveItemContentId { get; set; } =
		"material.core.test_dust";

	[Export(PropertyHint.PlaceholderText, "Test Dust")]
	public string ImmediateReviveItemDisplayName { get; set; } = "Test Dust";

	[Export(PropertyHint.Range, "1,99,1")]
	public int ImmediateReviveItemQuantity { get; set; } = 1;

	public bool IsRunActive { get; private set; }
	public bool IsDestinationExcursionActive { get; private set; }
	public bool IsAwaitingIncapacitationChoice { get; private set; }
	public int CompletedGroupCount { get; private set; }
	public RegionCompletionResult? LastCompletionResult { get; private set; }

	public event System.Action<RegionCompletionResult>? RegionCompleted;
	public event System.Action<HeroActorController, bool>?
		IncapacitationChoiceRequested;

	private bool HasFiniteGroupLimit =>
		!ActiveRegion.EncountersLoopIndefinitely;
	private int GroupCount => ActiveRegion.MonsterGroupCount;
	private string EncounterPoolContentId =>
		ActiveRegion.EncounterPoolContentId;

	private double _remainingTravelSeconds;
	private readonly RandomNumberGenerator _encounterTimingRandom = new();
	private bool _waitingForNextGroup;
	private bool _waitingForSurvivorsToRegroup;
	private readonly Queue<HeroActorController>
		_pendingIncapacitationChoices = new();
	private HeroActorController? _currentIncapacitatedHero;
	private CombatOutcome _incapacitationCombatOutcome = CombatOutcome.None;
	private bool _graveyardReviveChosen;

	/// <summary>
	/// Runs Godot setup for Region Run Controller when the node enters the scene tree.
	/// Uses the current node and service state; any result is applied through side effects, events, or stored fields.
	/// </summary>
	public override void _Ready()
	{
		if (!ValidateReferences())
		{
			SetProcess(false);
			return;
		}

		RegionPresentation.ApplyRegion(ActiveRegion);
		Combat.CombatResolved += OnCombatResolved;
		_encounterTimingRandom.Randomize();
		SetProcess(false);

		if (AutoStart)
			CallDeferred(MethodName.StartRun);
	}

	/// <summary>
	/// Cleans up Region Run Controller when the node leaves the scene tree.
	/// Uses the current node and service state; any result is applied through side effects, events, or stored fields.
	/// </summary>
	public override void _ExitTree()
	{
		if (GodotObject.IsInstanceValid(Combat))
			Combat.CombatResolved -= OnCombatResolved;
	}

	/// <summary>
	/// Updates Region Run Controller every rendered frame using the supplied frame delta.
	/// Uses the supplied arguments and current node state; any result is applied through side effects, events, or stored fields.
	/// </summary>
	public override void _Process(double delta)
	{
		if (IsDestinationExcursionActive)
			return;

		if (_waitingForSurvivorsToRegroup)
		{
			UpdateSurvivorRegroup();
			return;
		}

		if (!IsRunActive
			|| !_waitingForNextGroup
			|| JourneyState.CurrentState
				!= JourneyStateService.JourneyState.Traveling)
		{
			return;
		}

		_remainingTravelSeconds -= delta;

		if (_remainingTravelSeconds > 0.0)
			return;

		_waitingForNextGroup = false;
		StartNextGroup();
	}

	/// <summary>
	/// Pauses or resumes the main-region encounter countdown without resetting
	/// its current group progress or remaining travel time.
	/// </summary>
	public void SetDestinationExcursionActive(bool active)
	{
		if (IsDestinationExcursionActive == active)
			return;

		IsDestinationExcursionActive = active;

		DebugLog.Print(
			active
				? "Main-region run paused for destination excursion."
				: "Main-region run resumed after destination excursion.");
	}

	/// <summary>
	/// Performs the start run operation for Region Run Controller.
	/// Uses the current node and service state; any result is applied through side effects, events, or stored fields.
	/// </summary>
	public void StartRun()
	{
		if (IsRunActive)
		{
			DebugLog.Print("A region run is already active.");
			return;
		}

		if (JourneyState.CurrentState
			== JourneyStateService.JourneyState.Encounter)
		{
			DebugLog.Print(
				"Region run could not start because an encounter " +
				"is already active.");
			return;
		}

		CompletedGroupCount = 0;
		LastCompletionResult = null;
		_waitingForSurvivorsToRegroup = false;
		_pendingIncapacitationChoices.Clear();
		_currentIncapacitatedHero = null;
		IsAwaitingIncapacitationChoice = false;
		_incapacitationCombatOutcome = CombatOutcome.None;
		_graveyardReviveChosen = false;
		IsRunActive = true;
		float nextEncounterDelay = ScheduleNextEncounter();
		SetProcess(true);

		DebugLog.Print(
			$"Region run started: {ActiveRegion.DisplayName} " +
			$"({ActiveRegion.ContentId}), " +
			$"Pool={EncounterPoolContentId}, " +
			(HasFiniteGroupLimit
				? $"Groups={GroupCount}, "
				: "Groups=Infinite, ") +
			$"First encounter in " +
			$"{nextEncounterDelay:0.0}s.");
	}

	/// <summary>
	/// Performs the start next group operation for Region Run Controller.
	/// Uses the current node and service state; any result is applied through side effects, events, or stored fields.
	/// </summary>
	private void StartNextGroup()
	{
		int groupNumber = CompletedGroupCount + 1;
		double travelSeconds = Exploration.GetActiveRegionTravelSeconds();
		MonsterDifficultySnapshot difficulty =
			ActiveRegion.CreateMonsterDifficultySnapshot(travelSeconds);

		if (Encounter.TryStartEncounterPool(
			EncounterPoolContentId,
			Exploration.GetActiveRegionProgress(),
			difficulty,
			out string result))
		{
			DebugLog.Print(
				HasFiniteGroupLimit
					? $"Region group {groupNumber}/{GroupCount}: {result}"
					: $"Region encounter {groupNumber}: {result}");
			return;
		}

		StopRun(
			$"Region run stopped before encounter " +
			$"{groupNumber}: {result}");
	}

	/// <summary>
	/// Handles the combat resolved event and updates the related game state.
	/// Uses the supplied arguments and current node state; any result is applied through side effects, events, or stored fields.
	/// </summary>
	private void OnCombatResolved(CombatOutcome outcome)
	{
		if (!IsRunActive)
			return;

		bool hasIncapacitatedHero =
			Combat.Party.SpawnedHeroes.Any(
				hero => GodotObject.IsInstanceValid(hero)
					&& hero.IsIncapacitated);

		if (outcome == CombatOutcome.Defeat)
		{
			if (hasIncapacitatedHero)
			{
				QueueIncapacitationPause(outcome);
				return;
			}

			StopRun(
				$"Region run failed after " +
				$"{CompletedGroupCount} completed encounter(s).");
			return;
		}

		if (outcome != CombatOutcome.Victory)
			return;

		CompletedGroupCount++;

		if (hasIncapacitatedHero)
		{
			QueueIncapacitationPause(outcome);
			return;
		}

		if (HasFiniteGroupLimit
			&& CompletedGroupCount >= GroupCount)
		{
			CompleteRun();
			return;
		}

		float nextEncounterDelay = ScheduleNextEncounter();

		DebugLog.Print(
			(HasFiniteGroupLimit
				? $"Region group {CompletedGroupCount}/{GroupCount} cleared. "
				: $"Region encounter {CompletedGroupCount} cleared. ") +
			$"Traveling for {nextEncounterDelay:0.0}s " +
			"before the next encounter.");
	}

	/// <summary>
	/// Stores the resolved combat result before deferring the popup flow. Defeat
	/// must remain active long enough for a solo or full-party wipe to choose a
	/// graveyard revival instead of stopping the region run immediately.
	/// </summary>
	private void QueueIncapacitationPause(CombatOutcome outcome)
	{
		_incapacitationCombatOutcome = outcome;
		_graveyardReviveChosen = false;
		Callable.From(BeginIncapacitationPause).CallDeferred();
	}

	/// <summary>
	/// Performs the begin incapacitation pause operation for Region Run Controller.
	/// Uses the current node and service state; any result is applied through side effects, events, or stored fields.
	/// </summary>
	private void BeginIncapacitationPause()
	{
		if (!IsRunActive)
			return;

		_waitingForSurvivorsToRegroup = true;
		_waitingForNextGroup = false;
		JourneyState.SetState(
			JourneyStateService.JourneyState.AwaitingIncapacitationChoice);

		DebugLog.Print(
			_incapacitationCombatOutcome == CombatOutcome.Defeat
				? "The party was defeated. Journey movement paused for " +
				  "incapacitation and graveyard choices."
				: "A hero was incapacitated. Journey movement paused while " +
				  "the surviving party returns to formation.");
	}

	/// <summary>
	/// Recalculates survivor regroup from the latest runtime state.
	/// Uses the current node and service state; any result is applied through side effects, events, or stored fields.
	/// </summary>
	private void UpdateSurvivorRegroup()
	{
		bool survivorsInFormation =
			Combat.Party.SpawnedHeroes
				.Where(hero => GodotObject.IsInstanceValid(hero)
					&& !hero.IsIncapacitated)
				.All(hero =>
					hero.GlobalPosition.DistanceTo(hero.FormationPosition)
						<= hero.CombatArrivalDistance);

		if (!survivorsInFormation)
			return;

		_waitingForSurvivorsToRegroup = false;
		IsAwaitingIncapacitationChoice = true;
		_pendingIncapacitationChoices.Clear();

		foreach (HeroActorController hero in Combat.Party.SpawnedHeroes)
		{
			if (GodotObject.IsInstanceValid(hero)
				&& hero.IsIncapacitated)
			{
				_pendingIncapacitationChoices.Enqueue(hero);
			}
		}

		int incapacitatedCount =
			_pendingIncapacitationChoices.Count;

		DebugLog.Print(
			$"Surviving party regrouped. Journey remains paused; " +
			$"{incapacitatedCount} hero(es) await an " +
			"incapacitation choice.");

		RequestNextIncapacitationChoice();
	}

	/// <summary>
	/// Performs the resolve current incapacitation choice operation for Region Run Controller.
	/// Uses the supplied arguments and current node state; any result is applied through side effects, events, or stored fields.
	/// </summary>
	public void ResolveCurrentIncapacitationChoice(
		IncapacitationChoice choice)
	{
		if (!IsAwaitingIncapacitationChoice
			|| !GodotObject.IsInstanceValid(_currentIncapacitatedHero))
		{
			DebugLog.Print("No incapacitated hero is awaiting a choice.");
			return;
		}

		HeroActorController hero = _currentIncapacitatedHero!;

		if (choice == IncapacitationChoice.ReviveNow
			&& !Backpack.TryConsumeItem(
				ImmediateReviveItemContentId,
				ImmediateReviveItemQuantity,
				out string consumeError))
		{
			DebugLog.Print(
				$"Immediate revival rejected for {hero.Name}: " +
				consumeError);
			IncapacitationChoiceRequested?.Invoke(hero, false);
			return;
		}

		_currentIncapacitatedHero = null;

		switch (choice)
		{
			case IncapacitationChoice.ReviveNow:
				hero.ReviveFromIncapacitation();
				Combat.DebugRefreshHeroParticipants();
				DebugLog.Print(
					$"{hero.Name} was revived immediately by consuming " +
					$"{ImmediateReviveItemQuantity} " +
					$"{ImmediateReviveItemDisplayName}.");
				break;

			case IncapacitationChoice.ReviveAtGraveyard:
				_graveyardReviveChosen = true;
				string graveyardResult =
					Exploration.ReturnActiveRegionToLatestGraveyard();
				hero.ReviveFromIncapacitation();
				Combat.DebugRefreshHeroParticipants();
				DebugLog.Print(
					$"{hero.Name} revived at the graveyard. " +
					graveyardResult);
				break;

			case IncapacitationChoice.RemainIncapacitated:
				DebugLog.Print(
					$"{hero.Name} remains incapacitated and is no longer " +
					"part of the active combat roster.");
				break;

			default:
				GD.PushError(
					$"Unknown incapacitation choice '{choice}'.");
				break;
		}

		RequestNextIncapacitationChoice();
	}

	/// <summary>
	/// Performs the request next incapacitation choice operation for Region Run Controller.
	/// Uses the current node and service state; any result is applied through side effects, events, or stored fields.
	/// </summary>
	private void RequestNextIncapacitationChoice()
	{
		while (_pendingIncapacitationChoices.Count > 0)
		{
			HeroActorController hero =
				_pendingIncapacitationChoices.Dequeue();

			if (!GodotObject.IsInstanceValid(hero)
				|| !hero.IsIncapacitated)
			{
				continue;
			}

			_currentIncapacitatedHero = hero;

			bool reviveAvailable = Backpack.HasItemQuantity(
				ImmediateReviveItemContentId,
				ImmediateReviveItemQuantity);
			IncapacitationChoiceRequested?.Invoke(
				hero,
				reviveAvailable);
			return;
		}

		FinishIncapacitationChoices();
	}

	/// <summary>
	/// Performs the finish incapacitation choices operation for Region Run Controller.
	/// Uses the current node and service state; any result is applied through side effects, events, or stored fields.
	/// </summary>
	private void FinishIncapacitationChoices()
	{
		IsAwaitingIncapacitationChoice = false;

		bool hasActiveHero =
			Combat.Party.SpawnedHeroes.Any(
				hero => GodotObject.IsInstanceValid(hero)
					&& !hero.IsIncapacitated
					&& hero.Health.IsAlive);

		CombatOutcome resolvedOutcome = _incapacitationCombatOutcome;
		bool returnedFromGraveyard = _graveyardReviveChosen;
		_incapacitationCombatOutcome = CombatOutcome.None;
		_graveyardReviveChosen = false;
		JourneyState.BeginTravel();

		if (!hasActiveHero)
		{
			StopRun(
				"All heroes remain incapacitated. The region run stopped.");
			return;
		}

		if (returnedFromGraveyard)
		{
			DebugLog.Print(
				"The active party returned from the graveyard and " +
				"resumed regional travel.");
		}
		else if (resolvedOutcome == CombatOutcome.Defeat)
		{
			DebugLog.Print(
				"The revived party recovered from defeat and resumed " +
				"regional travel.");
		}

		if (HasFiniteGroupLimit
			&& CompletedGroupCount >= GroupCount)
		{
			CompleteRun();
			return;
		}

		float nextEncounterDelay = ScheduleNextEncounter();

		DebugLog.Print(
			$"Incapacitation choices resolved. Traveling for " +
			$"{nextEncounterDelay:0.0}s before the next encounter.");
	}

	/// <summary>
	/// Rolls and arms a fresh encounter delay. Only frames spent in the normal
	/// Traveling journey state decrement the resulting countdown.
	/// </summary>
	private float ScheduleNextEncounter()
	{
		float minimum = Mathf.Max(
			ActiveRegion.MinimumTravelSecondsBetweenEncounters,
			0.0f);
		float maximum = Mathf.Max(
			ActiveRegion.MaximumTravelSecondsBetweenEncounters,
			minimum);
		float delay = _encounterTimingRandom.RandfRange(minimum, maximum);

		_waitingForNextGroup = true;
		_remainingTravelSeconds = delay;
		return delay;
	}

	/// <summary>
	/// Performs the complete run operation for Region Run Controller.
	/// Uses the current node and service state; any result is applied through side effects, events, or stored fields.
	/// </summary>
	private void CompleteRun()
	{
		IsRunActive = false;
		_waitingForNextGroup = false;
		_waitingForSurvivorsToRegroup = false;
		SetProcess(false);

		LastCompletionResult = new RegionCompletionResult(
			ActiveRegion.ContentId,
			ActiveRegion.DisplayName,
			CompletedGroupCount);

		DebugLog.Print(
			$"Region complete: {ActiveRegion.DisplayName}; defeated " +
			$"{CompletedGroupCount}/{GroupCount} monster groups.");

		RegionCompleted?.Invoke(LastCompletionResult);
	}

	/// <summary>
	/// Performs the stop run operation for Region Run Controller.
	/// Uses the supplied arguments and current node state; any result is applied through side effects, events, or stored fields.
	/// </summary>
	private void StopRun(string message)
	{
		IsRunActive = false;
		_waitingForNextGroup = false;
		SetProcess(false);
		DebugLog.Print(message);
	}

	/// <summary>
	/// Performs the validate references operation for Region Run Controller.
	/// Reads the current state and returns the resulting bool to the caller.
	/// </summary>
	private bool ValidateReferences()
	{
		bool valid = true;
		valid &= Require(Encounter, nameof(Encounter));
		valid &= Require(Combat, nameof(Combat));
		valid &= Require(JourneyState, nameof(JourneyState));
		valid &= Require(Exploration, nameof(Exploration));
		valid &= Require(Backpack, nameof(Backpack));
		valid &= Require(
			RegionPresentation,
			nameof(RegionPresentation));

		if (!GodotObject.IsInstanceValid(ActiveRegion))
		{
			GD.PushError(
				"RegionRunController requires an Active Region resource.");
			return false;
		}

		IReadOnlyList<string> regionErrors =
			ActiveRegion.GetValidationErrors();

		foreach (string error in regionErrors)
			GD.PushError($"RegionRunController: {error}");

		if (!ContentId.IsValid(ImmediateReviveItemContentId))
		{
			GD.PushError(
				$"RegionRunController has invalid immediate-revive item ID " +
				$"'{ImmediateReviveItemContentId}'.");
			valid = false;
		}

		if (string.IsNullOrWhiteSpace(ImmediateReviveItemDisplayName))
		{
			GD.PushError(
				"RegionRunController requires an immediate-revive item name.");
			valid = false;
		}

		if (ImmediateReviveItemQuantity < 1)
		{
			GD.PushError(
				"RegionRunController immediate-revive item quantity must " +
				"be at least one.");
			valid = false;
		}

		return valid && regionErrors.Count == 0;
	}

	/// <summary>
	/// Performs the require operation for Region Run Controller.
	/// Uses the supplied arguments and current state and returns the resulting bool to the caller.
	/// </summary>
	private static bool Require(
		GodotObject value,
		string propertyName)
	{
		if (GodotObject.IsInstanceValid(value))
			return true;

		GD.PushError(
			$"RegionRunController is missing the Inspector " +
			$"reference '{propertyName}'.");

		return false;
	}
}
