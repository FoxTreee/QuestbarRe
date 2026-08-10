using Godot;
using System;
using System.Collections.Generic;

public partial class DebugConsoleController : Window
{
	[ExportCategory("Dependencies")]
	[Export]
	public DebugCommandService Commands { get; set; } = null!;
	[Export]
	public CombatController Combat { get; set; } = null!;
	[Export]
	public JourneyStateService JourneyState { get; set; } = null!;
	[Export]
	public EncounterController Encounter { get; set; } = null!;

	[ExportCategory("Controls")]
	[Export]
	public RichTextLabel DebugOutput { get; set; } = null!;

	[Export]
	public LineEdit CommandInput { get; set; } = null!;

	[Export]
	public TabBar FilterTabs { get; set; } = null!;

	[Export]
	public LineEdit SearchInput { get; set; } = null!;

	[Export]
	public Button PauseButton { get; set; } = null!;

	[Export]
	public Button AutoScrollButton { get; set; } = null!;

	[Export]
	public Button ClearButton { get; set; } = null!;

	private readonly List<string> _commandHistory = new();
	private readonly List<ConsoleEntry> _outputHistory = new();
	private int _historyIndex;
	private int _pausedHistoryCount;
	private bool _isDisplayPaused;
	private bool _isAutoScrollEnabled = true;
	private ConsoleFilter _activeFilter = ConsoleFilter.All;
	private string _searchText = string.Empty;

	private readonly record struct ConsoleEntry(
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

	public override void _Ready()
	{
		DebugLog.Print(
			"DebugConsoleController ready.");
			
		if (!ValidateReferences())
			return;

		_activeFilter =
			GetFilterForTab(FilterTabs.CurrentTab);

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

	public void ToggleConsole()
	{
		if (Visible)
		{
			HideConsole();
			return;
		}

		Show();
		GrabFocus();
		CommandInput.GrabFocus();
	}

	private void HideConsole()
	{
		Hide();
	}

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

	private void OnCommandSubmitted(string commandText)
	{
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

	private void AppendOutput(
		string message,
		DebugLogCategory category = DebugLogCategory.General)
	{
		_outputHistory.Add(
			new ConsoleEntry(category, message));

		if (!_isDisplayPaused
			&& ShouldDisplay(category, message))
		{
			DebugOutput.AppendText(
				message + "\n");
		}
	}

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

	private static bool Require(GodotObject value, string propertyName)
	{
		if (GodotObject.IsInstanceValid(value))
			return true;

		GD.PushError(
			$"DebugConsoleController is missing " +
			$"'{propertyName}'.");

		return false;
	}

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

	private void OnJourneyStateChanged(JourneyStateService.JourneyState previousState, JourneyStateService.JourneyState currentState)
	{
		AppendTimestampedOutput(
			$"JOURNEY  {previousState} → {currentState}",
			DebugLogCategory.Encounter);
	}

	private void OnEncounterStarted()
	{
		AppendTimestampedOutput(
			"ENCOUNTER  Started",
			DebugLogCategory.Encounter);
	}

	private void OnEncounterCompleted()
	{
		AppendTimestampedOutput(
			"ENCOUNTER  Completed",
			DebugLogCategory.Encounter);
	}

	private void OnMonsterRosterChanged(int activeMonsterCount)
	{
		AppendTimestampedOutput(
			$"MONSTERS  Active count={activeMonsterCount}",
			DebugLogCategory.Encounter);
	}

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

	private void ShowNextCommand()
	{
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

	private void ShowPreviousCommand()
	{
		if (_commandHistory.Count == 0)
			return;

		_historyIndex =
			Mathf.Max(
				_historyIndex - 1,
				0);

		ShowHistoryCommand();
	}

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

			default:
				return;
		}

		CommandInput.AcceptEvent();
	}

	private static string BuildDamageMessage(CombatEvent combatEvent)
	{
		return
			$"DAMAGE  " +
			$"{combatEvent.Attacker.Name} → " +
			$"{combatEvent.Target.Name} | " +
			$"{combatEvent.Damage.AppliedDamage} applied | " +
			$"{combatEvent.Damage.RemainingHealth} remaining";
	}

	private static string BuildDeathMessage(CombatEvent combatEvent)
		{
			return
				$"DIED  {combatEvent.Target.Name} | " +
				$"final hit by {combatEvent.Attacker.Name}";
		}

	private static string BuildIncapacitationMessage(CombatEvent combatEvent)
	{
		return
			$"INCAPACITATED  {combatEvent.Target.Name} | " +
			$"final hit by {combatEvent.Attacker.Name}";
	}
	
	private void OnDebugLogMessage(
		DateTime timestamp,
		DebugLogCategory category,
		string message)
	{
		AppendOutput(
			$"[{timestamp:HH:mm:ss}] " +
			$"{GetCategoryLabel(category)}  {message}",
			category);
	}

	private void AppendTimestampedOutput(
		string message,
		DebugLogCategory category)
	{
		string timestamp =
			DateTime.Now.ToString("HH:mm:ss");

		AppendOutput(
			$"[{timestamp}] {message}",
			category);
	}

	private void OnFilterTabChanged(long tabIndex)
	{
		_activeFilter =
			GetFilterForTab((int)tabIndex);

		RebuildVisibleOutput();
	}

	private void OnSearchTextChanged(string searchText)
	{
		_searchText =
			searchText.Trim();

		RebuildVisibleOutput();
	}

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

	private void UpdatePauseButtonText()
	{
		PauseButton.Text =
			_isDisplayPaused
				? "Resume"
				: "Pause";
	}

	private void ToggleAutoScroll()
	{
		_isAutoScrollEnabled =
			!_isAutoScrollEnabled;

		ApplyAutoScrollSetting();
	}

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

	private void ScrollToLatestLine()
	{
		int lastLine =
			DebugOutput.GetLineCount() - 1;

		if (lastLine >= 0)
		{
			DebugOutput.ScrollToLine(lastLine);
		}
	}

	private ConsoleFilter GetFilterForTab(int tabIndex)
	{
		string title =
			FilterTabs.GetTabTitle(tabIndex);

		return title.Trim().ToUpperInvariant() switch
		{
			"THREAT" => ConsoleFilter.Threat,
			"DAMAGE" => ConsoleFilter.Damage,
			"ABILITY" => ConsoleFilter.Ability,
			"ENCOUNTER" => ConsoleFilter.Encounter,
			"ERROR" => ConsoleFilter.Error,
			"IDS" => ConsoleFilter.Ids,
			_ => ConsoleFilter.All
		};
	}

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

		for (int i = 0; i < visibleEntryCount; i++)
		{
			ConsoleEntry entry =
				_outputHistory[i];

			if (!ShouldDisplay(
				entry.Category,
				entry.Message))
			{
				continue;
			}

			DebugOutput.AppendText(
				entry.Message + "\n");
		}
	}

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

	private void ClearOutput()
	{
		_outputHistory.Clear();
		_pausedHistoryCount = 0;
		RebuildVisibleOutput();
	}

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
