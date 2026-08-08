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
	
	private string ExecuteStartEncounter(string[] parts)
	{
		if (parts.Length == 1)
		{
			Encounter.JourneyState.BeginEncounter();

			return "Encounter started.";
		}

		if (parts.Length != 2)
		{
			return
				"Usage: encounter.start [content_id]\n" +
				"Example: encounter.start " +
				"encounter.core.training_mix";
		}

		Encounter.TryDebugStartEncounter(
			parts[1],
			out string result);

		return result;
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
			"QUESTBAR DEBUG CONSOLE\n" +
			"Commands are intended for development/testing and may bypass normal gameplay flow.\n\n" +

			"COMMAND CHAINS\n" +
			"--------------\n" +
			"Use && between complete commands to execute them sequentially from left to right.\n" +
			"The entire chain is stored as one console-history entry, so Up Arrow recalls it all.\n" +
			"Questbar currently treats && as a sequence separator; later commands still run even if\n" +
			"an earlier command reports an error. Empty commands around && are rejected.\n" +
			"Examples:\n" +
			"  heroes.reset && encounter.start encounter.core.training_swarm\n" +
			"  heroes.reset && monster.spawn monster.core.heavy_training_monster 3\n" +
			"  encounter.end && heroes.reset && encounter.start encounter.core.heavy_patrol\n" +
			"  encounter.start encounter.core.training_mix && status\n\n" +

			"GENERAL\n" +
			"-------\n" +
			"help\n" +
			"    Show this detailed command reference.\n" +
			"    Example: help\n\n" +

			"status\n" +
			"    Print current journey/combat state, active hero count, active monster count,\n" +
			"    and each configured hero's current HP/incapacitation state.\n" +
			"    Useful when a fight looks stuck or you want to verify combat cleanup.\n" +
			"    Examples:\n" +
			"      status\n" +
			"      encounter.start encounter.core.heavy_patrol\n" +
			"      status\n\n" +

			"clear\n" +
			"    Clear the visible console history. This does not change game state and can be used\n" +
			"    inside a command chain. Commands after clear still execute normally.\n" +
			"    Examples:\n" +
			"      clear\n" +
			"      clear && status\n\n" +

			"HEROES\n" +
			"------\n" +
			"heroes.reset\n" +
			"    Restore every configured hero from incapacitation, refill health, and rebuild\n" +
			"    the active combat participant list. Use this after a Defeat before another test.\n" +
			"    Examples:\n" +
			"      heroes.reset\n" +
			"      heroes.reset\n" +
			"      encounter.start encounter.core.training_swarm\n\n" +

			"MONSTERS\n" +
			"--------\n" +
			"monster.spawn <content_id> [count]\n" +
			"    Spawn an exact monster type. If no encounter is active, this command starts one\n" +
			"    without adding the legacy/default automatic monster. Count defaults to 1 and is\n" +
			"    clamped to 1-100 per command. Best tool for testing one monster definition.\n" +
			"    Known monster IDs:\n" +
			"      monster.core.training_monster\n" +
			"      monster.core.heavy_training_monster\n" +
			"    Examples:\n" +
			"      monster.spawn monster.core.training_monster\n" +
			"      monster.spawn monster.core.training_monster 5\n" +
			"      monster.spawn monster.core.heavy_training_monster 2\n\n" +

			"monsters.add <count>\n" +
			"    Debug convenience command using the default monster type. During an active\n" +
			"    encounter, it adds the requested amount. From Traveling, starting the default\n" +
			"    encounter may spawn its normal composition first; those automatic spawns count\n" +
			"    toward this request. Count is clamped to 1-100.\n" +
			"    Examples:\n" +
			"      monsters.add 1\n" +
			"      monsters.add 5\n" +
			"      monsters.add 25\n\n" +

			"monsters.set <count>\n" +
			"    Ensure the encounter has AT LEAST this many active monsters. It only adds; it\n" +
			"    never removes monsters when the current count is already higher. If needed, it\n" +
			"    starts the default encounter first. Count is clamped to 1-100.\n" +
			"    Examples:\n" +
			"      monsters.set 5\n" +
			"      monsters.set 20\n" +
			"      monsters.set 100\n\n" +

			"ENCOUNTERS\n" +
			"----------\n" +
			"encounter.start [content_id]\n" +
			"    Start an encounter. With no ID, Questbar starts the configured default encounter\n" +
			"    (currently encounter.core.training_mix). With an ID, the registered definition is\n" +
			"    rolled and EXACTLY that composition is spawned. An explicit encounter cannot be\n" +
			"    stacked on top of another active encounter.\n" +
			"    Examples:\n" +
			"      encounter.start\n" +
			"      encounter.start encounter.core.training_mix\n" +
			"      encounter.start encounter.core.training_swarm\n" +
			"      encounter.start encounter.core.heavy_patrol\n\n" +

			"    Registered test archetypes:\n" +
			"      encounter.core.training_mix  - Training Mix\n" +
			"        2-4 Training Monsters + 0-1 Heavy Training Monster.\n" +
			"        Balanced baseline encounter for ordinary combat/regression testing.\n\n" +
			"      encounter.core.training_swarm  - Training Swarm\n" +
			"        5-8 Training Monsters.\n" +
			"        Useful for testing ranged-target preference, focus fire, retargeting, and\n" +
			"        larger groups without heavy monsters changing the pressure pattern.\n\n" +
			"      encounter.core.heavy_patrol  - Heavy Patrol\n" +
			"        0-2 Training Monsters + 2-3 Heavy Training Monsters.\n" +
			"        Useful for testing durable enemies, melee-target preference, monster attacks,\n" +
			"        and party survival against sustained pressure.\n\n" +

			"encounter.end\n" +
			"    Manually end the current encounter and return the journey to Traveling. Use this\n" +
			"    to abort a debug fight or clean up a test; it is not treated as combat Victory.\n" +
			"    Examples:\n" +
			"      encounter.end\n" +
			"      encounter.start encounter.core.heavy_patrol\n" +
			"      encounter.end\n\n" +

			"QUICK TEST RECIPES\n" +
			"------------------\n" +
			"    Reset after a wipe, then test a swarm:\n" +
			"      heroes.reset && encounter.start encounter.core.training_swarm\n\n" +
			"    Reset and test only heavy-monster behavior:\n" +
			"      heroes.reset && monster.spawn monster.core.heavy_training_monster 2\n\n" +
			"    Abort the current fight, reset, then start a Heavy Patrol:\n" +
			"      encounter.end && heroes.reset && encounter.start encounter.core.heavy_patrol\n\n" +
			"    Start Training Mix and immediately inspect state:\n" +
			"      encounter.start encounter.core.training_mix && status\n\n" +

			"KEYBOARD SHORTCUTS\n" +
			"------------------\n" +
			"    Ctrl+Shift+D  Toggle debug console\n" +
			"    Ctrl+Shift+R  Reset heroes\n" +
			"    Ctrl+Shift+1  Add 1 default monster\n" +
			"    Ctrl+Shift+5  Add 5 default monsters\n" +
			"    Ctrl+Shift+X  End encounter";
	}

	public override void _Ready()
	{
		SetProcessInput(true);

		DebugLog.Print(
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

		DebugLog.Print(
			"Debug command completed: heroes.reset");
	}

	public void AddMonsters(int count)
	{
		Encounter.DebugAddMonsters(count);

		DebugLog.Print(
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

	private string ExecuteSpawnMonster(
	string[] parts)
	{
		if (parts.Length < 2)
		{
			return
				"Usage: monster.spawn " +
				"<content_id> [count]\n" +
				"Example: monster.spawn " +
				"monster.core.training_monster 5";
		}

		string contentId =
			parts[1];

		int count =
			1;

		if (parts.Length >= 3
			&& (!int.TryParse(
					parts[2],
					NumberStyles.Integer,
					CultureInfo.InvariantCulture,
					out count)
				|| count < 1))
		{
			return
				"Count must be a positive integer.";
		}

		count = Math.Clamp(count, 1, 100);

		int spawned =
			Encounter.DebugAddMonsters(
				contentId,
				count);

		return spawned == count
			? $"Spawned {spawned} instance(s) of " +
			  $"{contentId}."
			: $"Spawned {spawned} of {count} requested " +
			  $"instance(s) of {contentId}.";
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
			ExecuteStartEncounter(parts),

		"encounter.end" =>
			ExecuteEndEncounter(),

		"monster.spawn" =>
		 ExecuteSpawnMonster(parts),

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
