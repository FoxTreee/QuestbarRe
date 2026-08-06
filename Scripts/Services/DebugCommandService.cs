using Godot;
using System;
using System.Globalization;
using System.Text;

public partial class DebugCommandService : Node
{
	[ExportCategory("Dependencies")]
	[Export]
	public EncounterController Encounter { get; set; } = null!;

	[Export]
	public CombatController Combat { get; set; } = null!;

	[Export]
	public Godot.Collections.Array<HeroActorController>
		Heroes
	{ get; set; } = new();
	
	[Export]
	public DebugConsoleController Console { get; set; } = null!;
	
	public override void _Input(
	InputEvent @event)
	{
		if (@event is not InputEventKey keyEvent)
			return;

		if (!keyEvent.Pressed || keyEvent.Echo)
			return;

		if (!keyEvent.CtrlPressed
			|| !keyEvent.ShiftPressed)
		{
			return;
		}

		switch (keyEvent.Keycode)
		{
			case Key.D:
				Console.ToggleConsole();
				break;

			case Key.R:
				ResetHeroes();
				break;

			case Key.Key1:
				AddMonsters(1);
				break;

			case Key.Key5:
				AddMonsters(5);
				break;

			case Key.X:
				EndEncounter();
				break;

			default:
				return;
		}

		GetViewport().SetInputAsHandled();
	}
	
	private static bool TryReadCount(string[] parts, out int count)
	{
		count = 0;

		if (parts.Length < 2)
			return false;

		if (!int.TryParse(
			parts[1],
			NumberStyles.Integer,
			CultureInfo.InvariantCulture,
			out count))
		{
			return false;
		}

		count =
			Math.Clamp(
				count,
				1,
				100);

		return true;
	}
	
	private string ExecuteSetMonsterCount(string[] parts)
	{
		if (!TryReadCount(parts, out int count))
		{
			return
				"Usage: monsters.set <count>\n" +
				"Example: monsters.set 5";
		}

		Encounter.DebugSpawnMonsters(count);

		return
			$"Requested {count} active monster(s). " +
			$"Active monsters={Encounter.ActiveMonsterCount}.";
	}
	
	private string ExecuteEndEncounter()
	{
		Encounter.JourneyState.EndEncounter();

		return "Encounter ended.";
	}
	
	private string ExecuteStartEncounter()
	{
		Encounter.JourneyState.BeginEncounter();

		return "Encounter started.";
	}
	
	private string BuildStatusText()
	{
		StringBuilder output =
			new();

		output.AppendLine(
			$"Journey: " +
			$"{Encounter.JourneyState.CurrentState}");

		output.AppendLine(
			$"Combat active: " +
			$"{Combat.IsCombatActive}");

		output.AppendLine(
			$"Active heroes: " +
			$"{Combat.HeroParticipantCount}");

		output.AppendLine(
			$"Active monsters: " +
			$"{Encounter.ActiveMonsterCount}");

		output.AppendLine();
		output.AppendLine("Heroes:");

		foreach (
			HeroActorController hero
			in Heroes)
		{
			if (!GodotObject.IsInstanceValid(hero))
				continue;

			string state =
				hero.IsIncapacitated
					? "Incapacitated"
					: "Active";

			output.AppendLine(
				$"- {hero.Name}: {state}, " +
				$"HP {hero.Health.CurrentHealth}/" +
				$"{hero.Health.MaximumHealth}");
		}

		return output.ToString().TrimEnd();
	}
	
	private static string BuildHelpText()
	{
		return
			"Available commands\n\n" +
			"help\n" +
			"    Show this command list.\n\n" +
			"status\n" +
			"    Show current journey, combat, hero, and monster state.\n\n" +
			"heroes.reset\n" +
			"    Restore all configured heroes and rebuild combat participation.\n\n" +
			"monsters.add <count>\n" +
			"    Add new monsters to the current encounter.\n\n" +
			"monsters.set <count>\n" +
			"    Ensure at least the requested number of monsters is active.\n\n" +
			"encounter.start\n" +
			"    Begin an encounter.\n\n" +
			"encounter.end\n" +
			"    End the current encounter.\n\n" +
			"clear\n" +
			"    Clear the console output.";
	}

	public override void _Ready()
	{
		SetProcessInput(true);

		GD.Print(
		"DebugCommandService ready. " +
		"Ctrl+Shift+D toggles the console; " +
		"Ctrl+Shift+R resets heroes; " +
		"Ctrl+Shift+1 adds one monster; " +
		"Ctrl+Shift+5 adds five monsters.");
	}

	public void ResetHeroes()
	{
		foreach (
			HeroActorController hero
			in Heroes)
		{
			if (!GodotObject.IsInstanceValid(hero))
				continue;

			hero.DebugResetFromIncapacitation();
		}

		Combat.DebugRefreshHeroParticipants();

		GD.Print(
			"Debug command completed: heroes.reset");
	}

	public void AddMonsters(int count)
	{
		Encounter.DebugAddMonsters(count);

		GD.Print(
			$"Debug command completed: " +
			$"monsters.add {count}");
	}

	public void StartEncounter()
	{
		Encounter.JourneyState.BeginEncounter();
	}

	public void EndEncounter()
	{
		Encounter.JourneyState.EndEncounter();
	}
	
	public override void _UnhandledKeyInput(InputEvent @event)
	{
		if (@event is not InputEventKey keyEvent)
			return;

		if (!keyEvent.Pressed || keyEvent.Echo)
			return;

		if (!keyEvent.CtrlPressed
			|| !keyEvent.ShiftPressed)
		{
			return;
		}

		switch (keyEvent.Keycode)
		{
			case Key.R:
				ResetHeroes();
				break;

			case Key.Key1:
				AddMonsters(1);
				break;

			case Key.Key5:
				AddMonsters(5);
				break;

			case Key.X:
				EndEncounter();
				break;

			default:
				return;
		}

		GetViewport().SetInputAsHandled();
	}
	
	public string Execute(string commandText)
{
	string[] parts =
		commandText.Trim().Split(
			' ',
			StringSplitOptions.RemoveEmptyEntries);

	if (parts.Length == 0)
		return string.Empty;

	string command =
		parts[0].ToLowerInvariant();

	return command switch
	{
		"help" =>
			BuildHelpText(),

		"status" =>
			BuildStatusText(),

		"heroes.reset" =>
			ExecuteResetHeroes(),

		"monsters.add" =>
			ExecuteAddMonsters(parts),

		"monsters.set" =>
			ExecuteSetMonsterCount(parts),

		"encounter.start" =>
			ExecuteStartEncounter(),

		"encounter.end" =>
			ExecuteEndEncounter(),

		_ =>
			$"Unknown command: {command}\n" +
            "Type 'help' for available commands."
	};
}
	
	private string ExecuteResetHeroes()
	{
		ResetHeroes();

		return
			"All configured heroes were restored " +
			"and combat participants were refreshed.";
	}
	
	private string ExecuteAddMonsters(string[] parts)
	{
		if (!TryReadCount(parts, out int count))
		{
			return
				"Usage: monsters.add <count>\n" +
				"Example: monsters.add 5";
		}

		Encounter.DebugAddMonsters(count);

		return
			$"Added {count} monster(s). " +
			$"Active monsters={Encounter.ActiveMonsterCount}.";
	}
}
