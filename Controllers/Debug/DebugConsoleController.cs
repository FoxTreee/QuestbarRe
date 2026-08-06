using Godot;
using System;

public partial class DebugConsoleController : Window
{
	[ExportCategory("Dependencies")]
	[Export]
	public DebugCommandService Commands { get; set; } = null!;

	[ExportCategory("Controls")]
	[Export]
	public RichTextLabel DebugOutput { get; set; } = null!;

	[Export]
	public LineEdit CommandInput { get; set; } = null!;
	
	[Export]
	public CombatController Combat { get; set; } = null!;

	public override void _Ready()
	{
		GD.Print(
			"DebugConsoleController ready.");
			
		if (!ValidateReferences())
			return;
			
			Combat.CombatEventOccurred += OnCombatEventOccurred;
			CommandInput.TextSubmitted += OnCommandSubmitted;
			CloseRequested += HideConsole;

		AppendOutput(
			"Questbar Debug Console ready.\n" +
			"Type 'help' for available commands.");

		Hide();
	}

	public override void _ExitTree()
	{
		if (GodotObject.IsInstanceValid(CommandInput))
		{
			CommandInput.TextSubmitted -= OnCommandSubmitted;
		}
		
		if (GodotObject.IsInstanceValid(Combat))
		{
			Combat.CombatEventOccurred -= OnCombatEventOccurred;
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

	private void OnCommandSubmitted(
		string commandText)
	{
		string trimmedCommand =
			commandText.Trim();

		CommandInput.Clear();

		if (string.IsNullOrWhiteSpace(trimmedCommand))
		{
			CommandInput.GrabFocus();
			return;
		}

		AppendOutput(
			$"> {trimmedCommand}");

		if (trimmedCommand.Equals(
			"clear",
			System.StringComparison.OrdinalIgnoreCase))
		{
			DebugOutput.Clear();
			CommandInput.GrabFocus();
			return;
		}

		string result =
			Commands.Execute(trimmedCommand);

		if (!string.IsNullOrWhiteSpace(result))
		{
			AppendOutput(result);
		}

		CommandInput.GrabFocus();
	}

	private void AppendOutput(string message)
	{
		DebugOutput.AppendText(
			message + "\n");
	}

	private bool ValidateReferences()
	{
		bool valid = true;

		valid &= Require(
			Commands,
			nameof(Commands));

		valid &= Require(
			DebugOutput,
			nameof(DebugOutput));

		valid &= Require(
			CommandInput,
			nameof(CommandInput));
			
		valid &= Require(
			Combat,
			nameof(Combat));

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
	
	private void OnCombatEventOccurred( CombatEvent combatEvent)
	{
		string message =
			combatEvent.Type switch
			{
				CombatEventType.DamageApplied =>
					BuildDamageMessage(combatEvent),

				CombatEventType.ActorDied =>
					BuildDeathMessage(combatEvent),

				CombatEventType.ActorIncapacitated =>
					BuildIncapacitationMessage(combatEvent),

				_ =>
					$"COMBAT  {combatEvent.Type}"
			};

		AppendTimestampedOutput(message);
	}
	
	private static string BuildDamageMessage(
	CombatEvent combatEvent)
	{
		return
			$"DAMAGE  " +
			$"{combatEvent.Attacker.Name} → " +
			$"{combatEvent.Target.Name} | " +
			$"{combatEvent.Damage.AppliedDamage} applied | " +
			$"{combatEvent.Damage.RemainingHealth} remaining";
	}

private static string BuildDeathMessage(
	CombatEvent combatEvent)
	{
		return
			$"DIED  {combatEvent.Target.Name} | " +
			$"final hit by {combatEvent.Attacker.Name}";
	}

private static string BuildIncapacitationMessage(
	CombatEvent combatEvent)
	{
		return
			$"INCAPACITATED  {combatEvent.Target.Name} | " +
			$"final hit by {combatEvent.Attacker.Name}";
	}
	
	private void AppendTimestampedOutput(
	string message)
	{
		string timestamp =
			DateTime.Now.ToString("HH:mm:ss");

		AppendOutput(
			$"[{timestamp}] {message}");
	}
}
