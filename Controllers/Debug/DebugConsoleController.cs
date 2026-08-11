using Godot;
using System;
using System.Collections.Generic;
using System.Text;

public partial class DebugConsoleController : Window
{
	private const int MaxOutputHistoryEntries = 20_000;
	private const int RetainedOutputHistoryEntries = 5_000;
	private static readonly ConsoleFilter[] CountedFilters =
	{
		ConsoleFilter.Threat,
		ConsoleFilter.Damage,
		ConsoleFilter.Ability,
		ConsoleFilter.Encounter,
		ConsoleFilter.Error
	};

	[ExportCategory("Dependencies")]
	/// <summary>
	/// Inspector reference used by this component for its commands dependency.
	/// Assign the matching node or resource from the scene; leaving it empty prevents that connection from working.
	/// </summary>
	[Export]
	public DebugCommandService Commands { get; set; } = null!;
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
	/// Inspector reference used by this component for its encounter dependency.
	/// Assign the matching node or resource from the scene; leaving it empty prevents that connection from working.
	/// </summary>
	[Export]
	public EncounterController Encounter { get; set; } = null!;

	[ExportCategory("Controls")]
	/// <summary>
	/// Controls debug output.
	/// For example, selecting a different value changes which debug output behavior or content the owning system uses.
	/// </summary>
	[Export]
	public RichTextLabel DebugOutput { get; set; } = null!;

	/// <summary>
	/// Controls command input.
	/// For example, selecting a different value changes which command input behavior or content the owning system uses.
	/// </summary>
	[Export]
	public LineEdit CommandInput { get; set; } = null!;

	/// <summary>
	/// Controls filter tabs.
	/// For example, selecting a different value changes which filter tabs behavior or content the owning system uses.
	/// </summary>
	[Export]
	public TabBar FilterTabs { get; set; } = null!;

	/// <summary>
	/// Controls search input.
	/// For example, selecting a different value changes which search input behavior or content the owning system uses.
	/// </summary>
	[Export]
	public LineEdit SearchInput { get; set; } = null!;

	/// <summary>
	/// Inspector reference used by this component for its pause button dependency.
	/// Assign the matching node or resource from the scene; leaving it empty prevents that connection from working.
	/// </summary>
	[Export]
	public Button PauseButton { get; set; } = null!;

	/// <summary>
	/// Inspector reference used by this component for its auto scroll button dependency.
	/// Assign the matching node or resource from the scene; leaving it empty prevents that connection from working.
	/// </summary>
	[Export]
	public Button AutoScrollButton { get; set; } = null!;

	/// <summary>
	/// Inspector reference used by this component for its clear button dependency.
	/// Assign the matching node or resource from the scene; leaving it empty prevents that connection from working.
	/// </summary>
	[Export]
	public Button ClearButton { get; set; } = null!;

	private readonly List<string> _commandHistory = new();
	private readonly List<string> _completionCandidates = new();
	private readonly Queue<ConsoleEntry> _outputHistory = new();
	private readonly Dictionary<ConsoleFilter, int> _unreadCounts = new()
	{
		[ConsoleFilter.Threat] = 0,
		[ConsoleFilter.Damage] = 0,
		[ConsoleFilter.Ability] = 0,
		[ConsoleFilter.Encounter] = 0,
		[ConsoleFilter.Error] = 0
	};
	private readonly Dictionary<ConsoleFilter, long> _lastReadSequences = new()
	{
		[ConsoleFilter.Threat] = 0,
		[ConsoleFilter.Damage] = 0,
		[ConsoleFilter.Ability] = 0,
		[ConsoleFilter.Encounter] = 0,
		[ConsoleFilter.Error] = 0
	};
	private int _historyIndex;
	private int _completionIndex;
	private int _completionCaretColumn;
	private int _pausedHistoryCount;
	private long _latestEntrySequence;
	private string _completionTextBeforeToken = string.Empty;
	private string _completionTextAfterToken = string.Empty;
	private string _completionAppliedText = string.Empty;
	private bool _isDisplayPaused;
	private bool _isAutoScrollEnabled = true;
	private ConsoleFilter _activeFilter = ConsoleFilter.All;
	private string _searchText = string.Empty;

	private readonly record struct ConsoleEntry(
		long Sequence,
		DebugLogCategory Category,
		string Message);

	private enum ConsoleFilter
	{
		All,
		Threat,
		Damage,
		Ability,
		Encounter,
		Error,
		Ids
	}

	/// <summary>
	/// Runs Godot setup for Debug Console Controller when the node enters the scene tree.
	/// Uses the current node and service state; any result is applied through side effects, events, or stored fields.
	/// </summary>
	public override void _Ready()
	{
		DebugLog.Print(
			"DebugConsoleController ready.");
			
		if (!ValidateReferences())
			return;

		_activeFilter =
			GetFilterForTab(FilterTabs.CurrentTab);

		ResetUnreadCounts();

		_searchText =
			SearchInput.Text.Trim();

		DebugLog.Subscribe(OnDebugLogMessage);
			
		Combat.CombatEventOccurred += OnCombatEventOccurred;
		CommandInput.TextSubmitted += OnCommandSubmitted;
		CommandInput.GuiInput += OnCommandInputGuiInput;
		FilterTabs.TabChanged += OnFilterTabChanged;
		SearchInput.TextChanged += OnSearchTextChanged;
		PauseButton.Pressed += ToggleDisplayPause;
		AutoScrollButton.Pressed += ToggleAutoScroll;
		ClearButton.Pressed += ClearOutput;
		CloseRequested += HideConsole;

		JourneyState.StateChanged += OnJourneyStateChanged;
		Encounter.EncounterStarted += OnEncounterStarted;
		Encounter.EncounterCompleted += OnEncounterCompleted;
		Encounter.MonsterRosterChanged += OnMonsterRosterChanged;

		ApplyAutoScrollSetting();

		AppendOutput(
			"Questbar Debug Console ready.\n" +
			"Type '.help' for available commands.");

		UpdatePauseButtonText();

		Hide();
	}

	/// <summary>
	/// Cleans up Debug Console Controller when the node leaves the scene tree.
	/// Uses the current node and service state; any result is applied through side effects, events, or stored fields.
	/// </summary>
	public override void _ExitTree()
	{
		DebugLog.Unsubscribe(OnDebugLogMessage);

		if (GodotObject.IsInstanceValid(CommandInput))
		{
			CommandInput.TextSubmitted -= OnCommandSubmitted;
		}
		
		if (GodotObject.IsInstanceValid(Combat))
		{
			Combat.CombatEventOccurred -= OnCombatEventOccurred;
		}

		if (GodotObject.IsInstanceValid(JourneyState))
		{
			JourneyState.StateChanged -= OnJourneyStateChanged;
		}

		if (GodotObject.IsInstanceValid(Encounter))
		{
			Encounter.EncounterStarted -= OnEncounterStarted;
			Encounter.EncounterCompleted -= OnEncounterCompleted;
			Encounter.MonsterRosterChanged -= OnMonsterRosterChanged;
		}
		if (GodotObject.IsInstanceValid(CommandInput))
		{
			CommandInput.TextSubmitted -= OnCommandSubmitted;

			CommandInput.GuiInput -= OnCommandInputGuiInput;
		}

		if (GodotObject.IsInstanceValid(FilterTabs))
		{
			FilterTabs.TabChanged -= OnFilterTabChanged;
		}

		if (GodotObject.IsInstanceValid(SearchInput))
		{
			SearchInput.TextChanged -= OnSearchTextChanged;
		}

		if (GodotObject.IsInstanceValid(PauseButton))
		{
			PauseButton.Pressed -= ToggleDisplayPause;
		}

		if (GodotObject.IsInstanceValid(AutoScrollButton))
		{
			AutoScrollButton.Pressed -= ToggleAutoScroll;
		}

		if (GodotObject.IsInstanceValid(ClearButton))
		{
			ClearButton.Pressed -= ClearOutput;
		}


		CloseRequested -=
			HideConsole;
	}

	/// <summary>
	/// Performs the toggle console operation for Debug Console Controller.
	/// Uses the current node and service state; any result is applied through side effects, events, or stored fields.
	/// </summary>
	public void ToggleConsole()
	{
		if (Visible)
		{
			HideConsole();
			return;
		}

		Show();
		MarkFilterRead(_activeFilter);
		GrabFocus();
		CommandInput.GrabFocus();
	}

	/// <summary>
	/// Performs the hide console operation for Debug Console Controller.
	/// Uses the current node and service state; any result is applied through side effects, events, or stored fields.
	/// </summary>
	private void HideConsole()
	{
		Hide();
	}

	/// <summary>
	/// Performs the input operation for Debug Console Controller.
	/// Uses the supplied arguments and current node state; any result is applied through side effects, events, or stored fields.
	/// </summary>
	public override void _Input(InputEvent @event)
	{
		if (!Visible
			|| @event is not InputEventKey keyEvent)
			return;

		if (!keyEvent.Pressed
			|| keyEvent.Echo
			|| keyEvent.Keycode != Key.Escape
			|| string.IsNullOrEmpty(SearchInput.Text))
		{
			return;
		}

		SearchInput.Text = string.Empty;
		OnSearchTextChanged(string.Empty);
		GetViewport().SetInputAsHandled();
	}

	/// <summary>
	/// Handles the command submitted event and updates the related game state.
	/// Uses the supplied arguments and current node state; any result is applied through side effects, events, or stored fields.
	/// </summary>
	private void OnCommandSubmitted(string commandText)
	{
		ResetCompletionCycle();

		string trimmedCommand =
			commandText.Trim();

		CommandInput.Clear();

		if (string.IsNullOrWhiteSpace(trimmedCommand))
		{
			CommandInput.GrabFocus();
			return;
		}

		AddCommandToHistory(trimmedCommand);

		AppendOutput(
			$"> {trimmedCommand}");

		ExecuteCommandChain(trimmedCommand);

		CommandInput.GrabFocus();
	}

	/// <summary>
	/// Performs the execute command chain operation for Debug Console Controller.
	/// Uses the supplied arguments and current node state; any result is applied through side effects, events, or stored fields.
	/// </summary>
	private void ExecuteCommandChain(string commandText)
	{
		string[] commands =
			commandText.Split(
				"&&",
				StringSplitOptions.None);

		foreach (string rawCommand in commands)
		{
			if (string.IsNullOrWhiteSpace(rawCommand))
			{
				AppendOutput(
					"Invalid command chain. " +
					"Place a complete command on both sides of '&&'.");

				return;
			}
		}

		foreach (string rawCommand in commands)
		{
			ExecuteSingleCommand(rawCommand.Trim());
		}
	}

	/// <summary>
	/// Performs the execute single command operation for Debug Console Controller.
	/// Uses the supplied arguments and current node state; any result is applied through side effects, events, or stored fields.
	/// </summary>
	private void ExecuteSingleCommand(string commandText)
	{
		if (commandText.Equals(
			"clear",
			StringComparison.OrdinalIgnoreCase)
			|| commandText.Equals(
				".clear",
				StringComparison.OrdinalIgnoreCase))
		{
			ClearOutput();
			return;
		}

		string result =
			Commands.Execute(commandText);

		if (!string.IsNullOrWhiteSpace(result))
		{
			AppendOutput(result);
		}
	}

	/// <summary>
	/// Performs the append output operation for Debug Console Controller.
	/// Uses the supplied arguments and current node state; any result is applied through side effects, events, or stored fields.
	/// </summary>
	private void AppendOutput(
		string message,
		DebugLogCategory category = DebugLogCategory.General)
	{
		ConsoleEntry entry = new(
			++_latestEntrySequence,
			category,
			message);

		_outputHistory.Enqueue(entry);
		TrackUnreadEntry(entry);

		bool historyWasTrimmed =
			TrimOutputHistory();

		if (!historyWasTrimmed
			&& !_isDisplayPaused
			&& ShouldDisplay(category, message))
		{
			DebugOutput.AppendText(
				message + "\n");
		}
	}

	/// <summary>
	/// Performs the trim output history operation for Debug Console Controller.
	/// Reads the current state and returns the resulting bool to the caller.
	/// </summary>
	private bool TrimOutputHistory()
	{
		if (_outputHistory.Count
			<= MaxOutputHistoryEntries)
		{
			return false;
		}

		int entriesToRemove =
			_outputHistory.Count
			- RetainedOutputHistoryEntries;

		HashSet<ConsoleFilter> changedUnreadFilters =
			new();

		for (int i = 0; i < entriesToRemove; i++)
		{
			ConsoleEntry expiredEntry =
				_outputHistory.Dequeue();

			if (RemoveExpiredUnreadEntry(expiredEntry))
			{
				ConsoleFilter? filter =
					GetFilterForCategory(
						expiredEntry.Category);

				if (filter.HasValue)
				{
					changedUnreadFilters.Add(
						filter.Value);
				}
			}
		}

		if (_isDisplayPaused)
		{
			_pausedHistoryCount =
				Math.Max(
					_pausedHistoryCount
					- entriesToRemove,
					0);
		}

		foreach (ConsoleFilter filter
			in changedUnreadFilters)
		{
			UpdateUnreadTabTitle(filter);
		}

		RebuildVisibleOutput();

		return true;
	}

	/// <summary>
	/// Performs the validate references operation for Debug Console Controller.
	/// Reads the current state and returns the resulting bool to the caller.
	/// </summary>
	private bool ValidateReferences()
	{
		bool valid = true;

		valid &= Require(Commands, nameof(Commands));
		valid &= Require(DebugOutput, nameof(DebugOutput));
		valid &= Require(CommandInput, nameof(CommandInput));
		valid &= Require(FilterTabs, nameof(FilterTabs));
		valid &= Require(SearchInput, nameof(SearchInput));
		valid &= Require(PauseButton, nameof(PauseButton));
		valid &= Require(AutoScrollButton, nameof(AutoScrollButton));
		valid &= Require(ClearButton, nameof(ClearButton));
		valid &= Require(Combat, nameof(Combat));
		valid &= Require(JourneyState, nameof(JourneyState));
		valid &= Require(Encounter, nameof(Encounter));

		return valid;
	}

	/// <summary>
	/// Performs the require operation for Debug Console Controller.
	/// Uses the supplied arguments and current state and returns the resulting bool to the caller.
	/// </summary>
	private static bool Require(GodotObject value, string propertyName)
	{
		if (GodotObject.IsInstanceValid(value))
			return true;

		GD.PushError(
			$"DebugConsoleController is missing " +
			$"'{propertyName}'.");

		return false;
	}

	/// <summary>
	/// Handles the combat event occurred event and updates the related game state.
	/// Uses the supplied arguments and current node state; any result is applied through side effects, events, or stored fields.
	/// </summary>
	private void OnCombatEventOccurred(CombatEvent combatEvent)
	{
		(string message, DebugLogCategory category) =
			combatEvent.Type switch
			{
				CombatEventType.DamageApplied =>
					(BuildDamageMessage(combatEvent), DebugLogCategory.Damage),

				CombatEventType.ActorDied =>
					(BuildDeathMessage(combatEvent), DebugLogCategory.Encounter),

				CombatEventType.ActorIncapacitated =>
					(BuildIncapacitationMessage(combatEvent), DebugLogCategory.Encounter),

				_ =>
					($"COMBAT  {combatEvent.Type}", DebugLogCategory.General)
			};

		AppendTimestampedOutput(message, category);
	}

	/// <summary>
	/// Handles the journey state changed event and updates the related game state.
	/// Uses the supplied arguments and current node state; any result is applied through side effects, events, or stored fields.
	/// </summary>
	private void OnJourneyStateChanged(JourneyStateService.JourneyState previousState, JourneyStateService.JourneyState currentState)
	{
		AppendTimestampedOutput(
			$"JOURNEY  {previousState} → {currentState}",
			DebugLogCategory.Encounter);
	}

	/// <summary>
	/// Handles the encounter started event and updates the related game state.
	/// Uses the current node and service state; any result is applied through side effects, events, or stored fields.
	/// </summary>
	private void OnEncounterStarted()
	{
		AppendTimestampedOutput(
			"ENCOUNTER  Started",
			DebugLogCategory.Encounter);
	}

	/// <summary>
	/// Handles the encounter completed event and updates the related game state.
	/// Uses the current node and service state; any result is applied through side effects, events, or stored fields.
	/// </summary>
	private void OnEncounterCompleted()
	{
		AppendTimestampedOutput(
			"ENCOUNTER  Completed",
			DebugLogCategory.Encounter);
	}

	/// <summary>
	/// Handles the monster roster changed event and updates the related game state.
	/// Uses the supplied arguments and current node state; any result is applied through side effects, events, or stored fields.
	/// </summary>
	private void OnMonsterRosterChanged(int activeMonsterCount)
	{
		AppendTimestampedOutput(
			$"MONSTERS  Active count={activeMonsterCount}",
			DebugLogCategory.Encounter);
	}

	/// <summary>
	/// Performs the add command to history operation for Debug Console Controller.
	/// Uses the supplied arguments and current node state; any result is applied through side effects, events, or stored fields.
	/// </summary>
	private void AddCommandToHistory( string command)
	{
		if (_commandHistory.Count > 0
			&& _commandHistory[^1]
				.Equals(
					command,
					StringComparison.Ordinal))
		{
			_historyIndex =
				_commandHistory.Count;

			return;
		}

		_commandHistory.Add(command);

		_historyIndex =
			_commandHistory.Count;
	}

	/// <summary>
	/// Performs the show history command operation for Debug Console Controller.
	/// Uses the current node and service state; any result is applied through side effects, events, or stored fields.
	/// </summary>
	private void ShowHistoryCommand()
	{
		if (_historyIndex < 0
			|| _historyIndex
				>= _commandHistory.Count)
		{
			return;
		}

		CommandInput.Text =
			_commandHistory[_historyIndex];

		CommandInput.CaretColumn =
			CommandInput.Text.Length;
	}

	/// <summary>
	/// Performs the show next command operation for Debug Console Controller.
	/// Uses the current node and service state; any result is applied through side effects, events, or stored fields.
	/// </summary>
	private void ShowNextCommand()
	{
		ResetCompletionCycle();

		if (_commandHistory.Count == 0)
			return;

		if (_historyIndex
			>= _commandHistory.Count - 1)
		{
			_historyIndex =
				_commandHistory.Count;

			CommandInput.Clear();
			return;
		}

		_historyIndex++;

		ShowHistoryCommand();
	}

	/// <summary>
	/// Performs the show previous command operation for Debug Console Controller.
	/// Uses the current node and service state; any result is applied through side effects, events, or stored fields.
	/// </summary>
	private void ShowPreviousCommand()
	{
		ResetCompletionCycle();

		if (_commandHistory.Count == 0)
			return;

		_historyIndex =
			Mathf.Max(
				_historyIndex - 1,
				0);

		ShowHistoryCommand();
	}

	/// <summary>
	/// Handles the command input gui input event and updates the related game state.
	/// Uses the supplied arguments and current node state; any result is applied through side effects, events, or stored fields.
	/// </summary>
	private void OnCommandInputGuiInput( InputEvent @event)
	{
		if (@event is not InputEventKey keyEvent)
			return;

		if (!keyEvent.Pressed
			|| keyEvent.Echo)
		{
			return;
		}

		switch (keyEvent.Keycode)
		{
			case Key.Up:
				ShowPreviousCommand();
				break;

			case Key.Down:
				ShowNextCommand();
				break;

			case Key.Tab:
				ApplyCommandCompletion(
					keyEvent.ShiftPressed);
				break;

			case Key.Backtab:
				ApplyCommandCompletion(true);
				break;

			default:
				return;
		}

		CommandInput.AcceptEvent();
	}

	/// <summary>
	/// Applies command completion to the relevant actor, resource, or presentation state.
	/// Uses the supplied arguments and current node state; any result is applied through side effects, events, or stored fields.
	/// </summary>
	private void ApplyCommandCompletion(bool reverse)
	{
		bool canContinueExistingCycle =
			_completionCandidates.Count > 1
			&& CommandInput.Text.Equals(
				_completionAppliedText,
				StringComparison.Ordinal)
			&& CommandInput.CaretColumn
				== _completionCaretColumn;

		if (canContinueExistingCycle)
		{
			_completionIndex = reverse
				? (_completionIndex - 1
					+ _completionCandidates.Count)
					% _completionCandidates.Count
				: (_completionIndex + 1)
					% _completionCandidates.Count;
		}
		else
		{
			StartCompletionCycle(reverse);
		}

		if (_completionCandidates.Count == 0)
			return;

		string completion =
			_completionCandidates[_completionIndex];

		string completedText =
			_completionTextBeforeToken
			+ completion
			+ _completionTextAfterToken;

		int completedCaretColumn =
			_completionTextBeforeToken.Length
			+ completion.Length;

		CommandInput.Text = completedText;
		CommandInput.CaretColumn =
			completedCaretColumn;

		_completionAppliedText = completedText;
		_completionCaretColumn =
			completedCaretColumn;

		if (_completionCandidates.Count == 1)
		{
			_completionCandidates.Clear();
		}
	}

	/// <summary>
	/// Performs the start completion cycle operation for Debug Console Controller.
	/// Uses the supplied arguments and current node state; any result is applied through side effects, events, or stored fields.
	/// </summary>
	private void StartCompletionCycle(bool reverse)
	{
		ResetCompletionCycle();

		IReadOnlyList<string> candidates =
			Commands.GetCommandCompletions(
				CommandInput.Text,
				CommandInput.CaretColumn,
				out int replacementStart,
				out int replacementLength);

		if (candidates.Count == 0)
			return;

		_completionCandidates.AddRange(candidates);

		_completionTextBeforeToken =
			CommandInput.Text.Substring(
				0,
				replacementStart);

		_completionTextAfterToken =
			CommandInput.Text.Substring(
				replacementStart
				+ replacementLength);

		_completionIndex = reverse
			? _completionCandidates.Count - 1
			: 0;
	}

	/// <summary>
	/// Resets completion cycle so the system can begin from a clean state.
	/// Uses the current node and service state; any result is applied through side effects, events, or stored fields.
	/// </summary>
	private void ResetCompletionCycle()
	{
		_completionCandidates.Clear();
		_completionIndex = 0;
		_completionCaretColumn = -1;
		_completionTextBeforeToken = string.Empty;
		_completionTextAfterToken = string.Empty;
		_completionAppliedText = string.Empty;
	}

	/// <summary>
	/// Creates damage message from the supplied configuration and current dependencies.
	/// Uses the supplied arguments and current state and returns the resulting string to the caller.
	/// </summary>
	private static string BuildDamageMessage(CombatEvent combatEvent)
	{
		return
			$"DAMAGE  " +
			$"{combatEvent.Attacker.Name} → " +
			$"{combatEvent.Target.Name} | " +
			$"{combatEvent.Damage.AppliedDamage} applied | " +
			$"{combatEvent.Damage.RemainingHealth} remaining";
	}

	/// <summary>
	/// Creates death message from the supplied configuration and current dependencies.
	/// Uses the supplied arguments and current state and returns the resulting string to the caller.
	/// </summary>
	private static string BuildDeathMessage(CombatEvent combatEvent)
		{
			return
				$"DIED  {combatEvent.Target.Name} | " +
				$"final hit by {combatEvent.Attacker.Name}";
		}

	/// <summary>
	/// Creates incapacitation message from the supplied configuration and current dependencies.
	/// Uses the supplied arguments and current state and returns the resulting string to the caller.
	/// </summary>
	private static string BuildIncapacitationMessage(CombatEvent combatEvent)
	{
		return
			$"INCAPACITATED  {combatEvent.Target.Name} | " +
			$"final hit by {combatEvent.Attacker.Name}";
	}
	
	/// <summary>
	/// Handles the debug log message event and updates the related game state.
	/// Uses the supplied arguments and current node state; any result is applied through side effects, events, or stored fields.
	/// </summary>
	private void OnDebugLogMessage(
		DateTime timestamp,
		DebugLogCategory category,
		string message)
	{
		AppendOutput(
			$"[{timestamp:HH:mm:ss.fff}] " +
			$"{GetCategoryLabel(category)}  {message}",
			category);
	}

	/// <summary>
	/// Performs the append timestamped output operation for Debug Console Controller.
	/// Uses the supplied arguments and current node state; any result is applied through side effects, events, or stored fields.
	/// </summary>
	private void AppendTimestampedOutput(
		string message,
		DebugLogCategory category)
	{
		string timestamp =
			DateTime.Now.ToString("HH:mm:ss.fff");

		AppendOutput(
			$"[{timestamp}] {message}",
			category);
	}

	/// <summary>
	/// Handles the filter tab changed event and updates the related game state.
	/// Uses the supplied arguments and current node state; any result is applied through side effects, events, or stored fields.
	/// </summary>
	private void OnFilterTabChanged(long tabIndex)
	{
		_activeFilter =
			GetFilterForTab((int)tabIndex);

		MarkFilterRead(_activeFilter);

		RebuildVisibleOutput();
	}

	/// <summary>
	/// Handles the search text changed event and updates the related game state.
	/// Uses the supplied arguments and current node state; any result is applied through side effects, events, or stored fields.
	/// </summary>
	private void OnSearchTextChanged(string searchText)
	{
		_searchText =
			searchText.Trim();

		RebuildVisibleOutput();
	}

	/// <summary>
	/// Performs the toggle display pause operation for Debug Console Controller.
	/// Uses the current node and service state; any result is applied through side effects, events, or stored fields.
	/// </summary>
	private void ToggleDisplayPause()
	{
		if (_isDisplayPaused)
		{
			_isDisplayPaused = false;
		}
		else
		{
			_pausedHistoryCount =
				_outputHistory.Count;

			_isDisplayPaused = true;
		}

		UpdatePauseButtonText();
		RebuildVisibleOutput();
	}

	/// <summary>
	/// Recalculates pause button text from the latest runtime state.
	/// Uses the current node and service state; any result is applied through side effects, events, or stored fields.
	/// </summary>
	private void UpdatePauseButtonText()
	{
		PauseButton.Text =
			_isDisplayPaused
				? "Resume"
				: "Pause";
	}

	/// <summary>
	/// Performs the toggle auto scroll operation for Debug Console Controller.
	/// Uses the current node and service state; any result is applied through side effects, events, or stored fields.
	/// </summary>
	private void ToggleAutoScroll()
	{
		_isAutoScrollEnabled =
			!_isAutoScrollEnabled;

		ApplyAutoScrollSetting();
	}

	/// <summary>
	/// Applies auto scroll setting to the relevant actor, resource, or presentation state.
	/// Uses the current node and service state; any result is applied through side effects, events, or stored fields.
	/// </summary>
	private void ApplyAutoScrollSetting()
	{
		DebugOutput.ScrollFollowing =
			_isAutoScrollEnabled;

		AutoScrollButton.Text =
			_isAutoScrollEnabled
				? "Auto Scroll: On"
				: "Auto Scroll: Off";

		if (_isAutoScrollEnabled)
		{
			ScrollToLatestLine();
		}
	}

	/// <summary>
	/// Performs the scroll to latest line operation for Debug Console Controller.
	/// Uses the current node and service state; any result is applied through side effects, events, or stored fields.
	/// </summary>
	private void ScrollToLatestLine()
	{
		int lastLine =
			DebugOutput.GetLineCount() - 1;

		if (lastLine >= 0)
		{
			DebugOutput.ScrollToLine(lastLine);
		}
	}

	/// <summary>
	/// Retrieves filter for tab from the current game state.
	/// Uses the supplied arguments and current state and returns the resulting console filter to the caller.
	/// </summary>
	private ConsoleFilter GetFilterForTab(int tabIndex)
	{
		string title =
			FilterTabs.GetTabTitle(tabIndex)
				.Trim();

		foreach (ConsoleFilter filter in CountedFilters)
		{
			string baseTitle =
				GetFilterTitle(filter);

			if (title.Equals(
					baseTitle,
					StringComparison.OrdinalIgnoreCase)
				|| title.StartsWith(
					baseTitle + " (",
					StringComparison.OrdinalIgnoreCase))
			{
				return filter;
			}
		}

		return title.ToUpperInvariant() switch
		{
			"IDS" => ConsoleFilter.Ids,
			_ => ConsoleFilter.All
		};
	}

	/// <summary>
	/// Performs the track unread entry operation for Debug Console Controller.
	/// Uses the supplied arguments and current node state; any result is applied through side effects, events, or stored fields.
	/// </summary>
	private void TrackUnreadEntry(ConsoleEntry entry)
	{
		ConsoleFilter? filter =
			GetFilterForCategory(entry.Category);

		if (!filter.HasValue)
			return;

		bool isBeingViewed =
			Visible
			&& _activeFilter == filter.Value;

		if (isBeingViewed)
		{
			_lastReadSequences[filter.Value] =
				entry.Sequence;

			return;
		}

		_unreadCounts[filter.Value]++;
		UpdateUnreadTabTitle(filter.Value);
	}

	/// <summary>
	/// Performs the remove expired unread entry operation for Debug Console Controller.
	/// Uses the supplied arguments and current state and returns the resulting bool to the caller.
	/// </summary>
	private bool RemoveExpiredUnreadEntry(ConsoleEntry entry)
	{
		ConsoleFilter? filter =
			GetFilterForCategory(entry.Category);

		if (!filter.HasValue
			|| entry.Sequence
				<= _lastReadSequences[filter.Value]
			|| _unreadCounts[filter.Value] == 0)
		{
			return false;
		}

		_unreadCounts[filter.Value]--;

		return true;
	}

	/// <summary>
	/// Performs the mark filter read operation for Debug Console Controller.
	/// Uses the supplied arguments and current node state; any result is applied through side effects, events, or stored fields.
	/// </summary>
	private void MarkFilterRead(ConsoleFilter filter)
	{
		if (!_unreadCounts.ContainsKey(filter))
			return;

		_lastReadSequences[filter] =
			_latestEntrySequence;

		if (_unreadCounts[filter] == 0)
			return;

		_unreadCounts[filter] = 0;
		UpdateUnreadTabTitle(filter);
	}

	/// <summary>
	/// Resets unread counts so the system can begin from a clean state.
	/// Uses the current node and service state; any result is applied through side effects, events, or stored fields.
	/// </summary>
	private void ResetUnreadCounts()
	{
		foreach (ConsoleFilter filter in CountedFilters)
		{
			_unreadCounts[filter] = 0;
			_lastReadSequences[filter] =
				_latestEntrySequence;

			UpdateUnreadTabTitle(filter);
		}
	}

	/// <summary>
	/// Recalculates unread tab title from the latest runtime state.
	/// Uses the supplied arguments and current node state; any result is applied through side effects, events, or stored fields.
	/// </summary>
	private void UpdateUnreadTabTitle(ConsoleFilter filter)
	{
		int tabIndex = FindTabIndex(filter);

		if (tabIndex < 0)
			return;

		string baseTitle =
			GetFilterTitle(filter);

		int unreadCount =
			_unreadCounts[filter];

		FilterTabs.SetTabTitle(
			tabIndex,
			unreadCount > 0
				? $"{baseTitle} ({unreadCount})"
				: baseTitle);
	}

	/// <summary>
	/// Performs the find tab index operation for Debug Console Controller.
	/// Uses the supplied arguments and current state and returns the resulting int to the caller.
	/// </summary>
	private int FindTabIndex(ConsoleFilter filter)
	{
		for (int tabIndex = 0;
			tabIndex < FilterTabs.TabCount;
			tabIndex++)
		{
			if (GetFilterForTab(tabIndex) == filter)
				return tabIndex;
		}

		return -1;
	}

	/// <summary>
	/// Retrieves filter for category from the current game state.
	/// Uses the supplied arguments and current state and returns the resulting console filter to the caller.
	/// </summary>
	private static ConsoleFilter? GetFilterForCategory(
		DebugLogCategory category)
	{
		return category switch
		{
			DebugLogCategory.Threat => ConsoleFilter.Threat,
			DebugLogCategory.Damage => ConsoleFilter.Damage,
			DebugLogCategory.Ability => ConsoleFilter.Ability,
			DebugLogCategory.Encounter => ConsoleFilter.Encounter,
			DebugLogCategory.Error => ConsoleFilter.Error,
			_ => null
		};
	}

	/// <summary>
	/// Retrieves filter title from the current game state.
	/// Uses the supplied arguments and current state and returns the resulting string to the caller.
	/// </summary>
	private static string GetFilterTitle(ConsoleFilter filter)
	{
		return filter switch
		{
			ConsoleFilter.Threat => "Threat",
			ConsoleFilter.Damage => "Damage",
			ConsoleFilter.Ability => "Ability",
			ConsoleFilter.Encounter => "Encounter",
			ConsoleFilter.Error => "Error",
			ConsoleFilter.Ids => "IDs",
			_ => "All"
		};
	}

	/// <summary>
	/// Performs the rebuild visible output operation for Debug Console Controller.
	/// Uses the current node and service state; any result is applied through side effects, events, or stored fields.
	/// </summary>
	private void RebuildVisibleOutput()
	{
		DebugOutput.Clear();

		if (_activeFilter == ConsoleFilter.Ids)
		{
			AppendReferenceOutput();
			return;
		}

		int visibleEntryCount =
			_isDisplayPaused
				? Math.Min(
					_pausedHistoryCount,
					_outputHistory.Count)
				: _outputHistory.Count;

		int historyIndex = 0;
		StringBuilder visibleOutput = new();

		foreach (ConsoleEntry entry in _outputHistory)
		{
			if (historyIndex >= visibleEntryCount)
			{
				break;
			}

			historyIndex++;

			if (!ShouldDisplay(
				entry.Category,
				entry.Message))
			{
				continue;
			}

			visibleOutput
				.Append(entry.Message)
				.Append('\n');
		}

		if (visibleOutput.Length > 0)
		{
			DebugOutput.AppendText(
				visibleOutput.ToString());
		}
	}

	/// <summary>
	/// Performs the should display operation for Debug Console Controller.
	/// Uses the supplied arguments and current state and returns the resulting bool to the caller.
	/// </summary>
	private bool ShouldDisplay(
		DebugLogCategory category,
		string message)
	{
		bool categoryMatches =
			_activeFilter switch
			{
				ConsoleFilter.All => true,
				ConsoleFilter.Threat => category == DebugLogCategory.Threat,
				ConsoleFilter.Damage => category == DebugLogCategory.Damage,
				ConsoleFilter.Ability => category == DebugLogCategory.Ability,
				ConsoleFilter.Encounter => category == DebugLogCategory.Encounter,
				ConsoleFilter.Error => category == DebugLogCategory.Error,
				ConsoleFilter.Ids => false,
				_ => false
			};

		if (!categoryMatches)
			return false;

		return string.IsNullOrEmpty(_searchText)
			|| message.Contains(
				_searchText,
				StringComparison.OrdinalIgnoreCase);
	}

	/// <summary>
	/// Resets output so the system can begin from a clean state.
	/// Uses the current node and service state; any result is applied through side effects, events, or stored fields.
	/// </summary>
	private void ClearOutput()
	{
		_outputHistory.Clear();
		_pausedHistoryCount = 0;
		ResetUnreadCounts();
		RebuildVisibleOutput();
	}

	/// <summary>
	/// Performs the append reference output operation for Debug Console Controller.
	/// Uses the current node and service state; any result is applied through side effects, events, or stored fields.
	/// </summary>
	private void AppendReferenceOutput()
	{
		string referenceText =
			Commands.BuildConsoleReferenceText();

		string[] lines =
			referenceText.Replace(
				"\r\n",
				"\n")
			.Split('\n');

		foreach (string line in lines)
		{
			if (!string.IsNullOrEmpty(_searchText)
				&& !line.Contains(
					_searchText,
					StringComparison.OrdinalIgnoreCase))
			{
				continue;
			}

			DebugOutput.AppendText(
				line + "\n");
		}
	}

	/// <summary>
	/// Retrieves category label from the current game state.
	/// Uses the supplied arguments and current state and returns the resulting string to the caller.
	/// </summary>
	private static string GetCategoryLabel(DebugLogCategory category)
	{
		return category switch
		{
			DebugLogCategory.Threat => "THREAT",
			DebugLogCategory.Damage => "DAMAGE",
			DebugLogCategory.Ability => "ABILITY",
			DebugLogCategory.Encounter => "ENCOUNTER",
			DebugLogCategory.Error => "ERROR",
			_ => "LOG"
		};
	}
}
