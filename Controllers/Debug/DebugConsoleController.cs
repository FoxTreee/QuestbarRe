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

	private readonly List<string> _commandHistory = new();
	private int _historyIndex;



	public override void _Ready()
	{
		DebugLog.Print(
			"DebugConsoleController ready.");
			
		if (!ValidateReferences())
			return;

		DebugLog.Subscribe(OnDebugLogMessage);
			
		Combat.CombatEventOccurred += OnCombatEventOccurred;
		CommandInput.TextSubmitted += OnCommandSubmitted;
		CommandInput.GuiInput += OnCommandInputGuiInput;
		CloseRequested += HideConsole;

		JourneyState.StateChanged += OnJourneyStateChanged;
		Encounter.EncounterStarted += OnEncounterStarted;
		Encounter.EncounterCompleted += OnEncounterCompleted;
		Encounter.MonsterRosterChanged += OnMonsterRosterChanged;

		AppendOutput(
			"Questbar Debug Console ready.\n" +
			"Type 'help' for available commands.");

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
			StringComparison.OrdinalIgnoreCase))
		{
			DebugOutput.Clear();
			return;
		}

		string result =
			Commands.Execute(commandText);

		if (!string.IsNullOrWhiteSpace(result))
		{
			AppendOutput(result);
		}
	}

	private void AppendOutput(string message)
	{
		DebugOutput.AppendText(
			message + "\n");
	}

	private bool ValidateReferences()
	{
		bool valid = true;

		valid &= Require(Commands, nameof(Commands));
		valid &= Require(DebugOutput, nameof(DebugOutput));
		valid &= Require(CommandInput, nameof(CommandInput));
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
		string message =
			combatEvent.Type switch
			{
				CombatEventType.DamageApplied => BuildDamageMessage(combatEvent),

				CombatEventType.ActorDied => BuildDeathMessage(combatEvent),

				CombatEventType.ActorIncapacitated => BuildIncapacitationMessage(combatEvent),

				_ => $"COMBAT  {combatEvent.Type}"
			};

		AppendTimestampedOutput(message);
	}

	private void OnJourneyStateChanged(JourneyStateService.JourneyState previousState, JourneyStateService.JourneyState currentState)
	{
		AppendTimestampedOutput(
			$"JOURNEY  {previousState} → {currentState}");
	}

	private void OnEncounterStarted()
	{
		AppendTimestampedOutput(
			"ENCOUNTER  Started");
	}

	private void OnEncounterCompleted()
	{
		AppendTimestampedOutput(
			"ENCOUNTER  Completed");
	}

	private void OnMonsterRosterChanged(int activeMonsterCount)
	{
		AppendTimestampedOutput(
			$"MONSTERS  Active count={activeMonsterCount}");
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
	string message)
	{
		AppendOutput(
			$"[{timestamp:HH:mm:ss}] LOG  {message}");
	}

	private void AppendTimestampedOutput(string message)
	{
		string timestamp =
			DateTime.Now.ToString("HH:mm:ss");

		AppendOutput(
			$"[{timestamp}] {message}");
	}
}
