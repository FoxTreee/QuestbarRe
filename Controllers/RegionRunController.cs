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
	/// Inspector reference used by this component for its reward ledger dependency.
	/// Assign the matching node or resource from the scene; leaving it empty prevents that connection from working.
	/// </summary>
	[Export]
	public RewardLedgerService RewardLedger { get; set; } = null!;

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
	/// Controls travel seconds between groups, measured as seconds.
	/// For example, changing 5 to 10 makes the affected action wait twice as long between uses.
	/// </summary>
	[Export(PropertyHint.Range, "0,300,0.1")]
	public float TravelSecondsBetweenGroups { get; set; } = 5.0f;

	/// <summary>
	/// Enables or disables auto start.
	/// For example, turn this on to enable auto start, or off to suppress that behavior.
	/// </summary>
	[Export]
	public bool AutoStart { get; set; } = true;

	public bool IsRunActive { get; private set; }
	public bool IsAwaitingIncapacitationChoice { get; private set; }
	public int CompletedGroupCount { get; private set; }
	public RegionCompletionResult? LastCompletionResult { get; private set; }

	public event System.Action<RegionCompletionResult>? RegionCompleted;
	public event System.Action<HeroActorController, bool>?
		IncapacitationChoiceRequested;

	private int GroupCount => ActiveRegion.MonsterGroupCount;
	private string EncounterPoolContentId =>
		ActiveRegion.EncounterPoolContentId;

	private double _remainingTravelSeconds;
	private bool _waitingForNextGroup;
	private bool _waitingForSurvivorsToRegroup;
	private readonly Queue<HeroActorController>
		_pendingIncapacitationChoices = new();
	private HeroActorController? _currentIncapacitatedHero;

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
		IsRunActive = true;
		_waitingForNextGroup = true;
		_remainingTravelSeconds = TravelSecondsBetweenGroups;
		SetProcess(true);

		DebugLog.Print(
			$"Region run started: {ActiveRegion.DisplayName} " +
			$"({ActiveRegion.ContentId}), " +
			$"Pool={EncounterPoolContentId}, " +
			$"Groups={GroupCount}, " +
			$"First encounter in " +
			$"{TravelSecondsBetweenGroups:0.0}s.");
	}

	/// <summary>
	/// Performs the start next group operation for Region Run Controller.
	/// Uses the current node and service state; any result is applied through side effects, events, or stored fields.
	/// </summary>
	private void StartNextGroup()
	{
		int groupNumber = CompletedGroupCount + 1;

		if (Encounter.TryStartEncounterPool(
			EncounterPoolContentId,
			out string result))
		{
			DebugLog.Print(
				$"Region group {groupNumber}/{GroupCount}: {result}");
			return;
		}

		StopRun(
			$"Region run stopped before group " +
			$"{groupNumber}/{GroupCount}: {result}");
	}

	/// <summary>
	/// Handles the combat resolved event and updates the related game state.
	/// Uses the supplied arguments and current node state; any result is applied through side effects, events, or stored fields.
	/// </summary>
	private void OnCombatResolved(CombatOutcome outcome)
	{
		if (!IsRunActive)
			return;

		if (outcome == CombatOutcome.Defeat)
		{
			StopRun(
				$"Region run failed after " +
				$"{CompletedGroupCount}/{GroupCount} completed groups.");
			return;
		}

		if (outcome != CombatOutcome.Victory)
			return;

		CompletedGroupCount++;
		bool hasIncapacitatedHero =
			Combat.Party.SpawnedHeroes.Any(
				hero => GodotObject.IsInstanceValid(hero)
					&& hero.IsIncapacitated);

		if (hasIncapacitatedHero)
		{
			Callable.From(BeginIncapacitationPause).CallDeferred();
			return;
		}

		if (CompletedGroupCount >= GroupCount)
		{
			CompleteRun();
			return;
		}

		_waitingForNextGroup = true;
		_remainingTravelSeconds = TravelSecondsBetweenGroups;

		DebugLog.Print(
			$"Region group {CompletedGroupCount}/{GroupCount} cleared. " +
			$"Traveling for {TravelSecondsBetweenGroups:0.0}s " +
			"before the next group.");
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
			"A hero was incapacitated. Journey movement paused while " +
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
			$"{incapacitatedCount} hero(es) await Revive or " +
			"Incapacitate choice.");

		RequestNextIncapacitationChoice();
	}

	/// <summary>
	/// Performs the resolve current incapacitation choice operation for Region Run Controller.
	/// Uses the supplied arguments and current node state; any result is applied through side effects, events, or stored fields.
	/// </summary>
	public void ResolveCurrentIncapacitationChoice(bool revive)
	{
		if (!IsAwaitingIncapacitationChoice
			|| !GodotObject.IsInstanceValid(_currentIncapacitatedHero))
		{
			DebugLog.Print("No incapacitated hero is awaiting a choice.");
			return;
		}

		HeroActorController hero = _currentIncapacitatedHero!;
		_currentIncapacitatedHero = null;

		if (revive)
		{
			hero.ReviveFromIncapacitation();
			Combat.DebugRefreshHeroParticipants();
			DebugLog.Print($"{hero.Name} was revived.");
		}
		else
		{
			DebugLog.Print(
				$"{hero.Name} remains incapacitated and is no longer " +
				"part of the active combat roster.");
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

			// Revive is temporarily available for testing. A future
			// eligibility service will provide this value from party
			// abilities and consumable inventory.
			IncapacitationChoiceRequested?.Invoke(hero, true);
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
		JourneyState.BeginTravel();

		if (CompletedGroupCount >= GroupCount)
		{
			CompleteRun();
			return;
		}

		_waitingForNextGroup = true;
		_remainingTravelSeconds = TravelSecondsBetweenGroups;

		DebugLog.Print(
			$"Incapacitation choices resolved. Traveling for " +
			$"{TravelSecondsBetweenGroups:0.0}s before the next group.");
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

		int rewardBalance = RewardLedger.Grant(
			ActiveRegion.CompletionRewardContentId,
			ActiveRegion.CompletionRewardAmount);

		LastCompletionResult = new RegionCompletionResult(
			ActiveRegion.ContentId,
			ActiveRegion.DisplayName,
			CompletedGroupCount,
			ActiveRegion.CompletionRewardContentId,
			ActiveRegion.CompletionRewardAmount,
			rewardBalance);

		DebugLog.Print(
			$"Region complete: {ActiveRegion.DisplayName}; defeated " +
			$"{CompletedGroupCount}/{GroupCount} monster groups; " +
			$"reward={ActiveRegion.CompletionRewardAmount} " +
			$"{ActiveRegion.CompletionRewardContentId}; " +
			$"balance={rewardBalance}.");

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
		valid &= Require(RewardLedger, nameof(RewardLedger));
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
