using Godot;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

public partial class DebugCommandService : Node
{
	private static readonly string[] CanonicalCommandSyntax =
	{
		".help",
		".status",
		".statusTimers [on|off|toggle]",
		".completeRegionExploration",
		".clear",
		".revive <hero_id>",
		".reviveAll",
		".kill <hero_id>",
		".kill partySlot <1-5>",
		".testResource <hero_id> <mana|energy|rage|none> [spend]",
		".useAbility <hero_id> <ability_id>",
		".addItem <item_id> [quantity]",
		".addCurrency <copper_amount>",
		".spendCurrency <copper_amount>",
		".saveInventory",
		".loadInventory",
		".spawnMonster <monster_id> [count]",
		".addMonsters <count>",
		".setMonsterCount <count>",
		".startEncounter <encounter_id>",
		".startEncounterPool <pool_id>",
        ".endEncounter"
	};

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
	/// Inspector reference used by this component for its party dependency.
	/// Assign the matching node or resource from the scene; leaving it empty prevents that connection from working.
	/// </summary>
	[Export]
	public PartyController Party { get; set; } = null!;

	/// <summary>
	/// Inspector reference used by this component for its console dependency.
	/// Assign the matching node or resource from the scene; leaving it empty prevents that connection from working.
	/// </summary>
	[Export]
	public DebugConsoleController Console { get; set; } = null!;

	[Export]
	public ItemAcquisitionService ItemAcquisition { get; set; } = null!;

	[Export]
	public BackpackWindowController Backpack { get; set; } = null!;

	[Export]
	public InventoryPersistenceService InventoryPersistence { get; set; } = null!;

	/// <summary>
	/// Saved per-region Traveling time used by map fog and destination reveals.
	/// </summary>
	[Export]
	public RegionExplorationService RegionExploration { get; set; } = null!;

	/// <summary>
	/// Performs the input operation for Debug Command Service.
	/// Uses the supplied arguments and current node state; any result is applied through side effects, events, or stored fields.
	/// </summary>
	public override void _Input(InputEvent @event)
	{
		if (@event is not InputEventKey keyEvent)
			return;

		if (!keyEvent.Pressed || keyEvent.Echo)
			return;

		if (!keyEvent.CtrlPressed || !keyEvent.ShiftPressed)
			return;

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

	/// <summary>
	/// Attempts to read count without throwing when the operation cannot be completed.
	/// Uses the supplied arguments and current state and returns the resulting bool to the caller.
	/// </summary>
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

		count = Math.Clamp(count, 1, 100);
		return true;
	}

	/// <summary>
	/// Performs the execute set monster count operation for Debug Command Service.
	/// Uses the supplied arguments and current state and returns the resulting string to the caller.
	/// </summary>
	private string ExecuteSetMonsterCount(string[] parts)
	{
		if (!TryReadCount(parts, out int count))
		{
			return
				"Usage: .setMonsterCount <count>\n" +
				"Example: .setMonsterCount 20";
		}

		Encounter.DebugSpawnMonsters(count);

		return
			$"Requested at least {count} active monster(s). " +
			$"Active monsters={Encounter.ActiveMonsterCount}.";
	}

	/// <summary>
	/// Performs the execute end encounter operation for Debug Command Service.
	/// Reads the current state and returns the resulting string to the caller.
	/// </summary>
	private string ExecuteEndEncounter()
	{
		Encounter.JourneyState.EndEncounter();
		return "Encounter ended.";
	}

	/// <summary>
	/// Performs the execute start encounter operation for Debug Command Service.
	/// Uses the supplied arguments and current state and returns the resulting string to the caller.
	/// </summary>
	private string ExecuteStartEncounter(string[] parts)
	{
		if (parts.Length != 2)
		{
			return
				"Usage: .startEncounter <encounter_id>\n" +
				"Example: .startEncounter " +
				"encounter.core.training_swarm";
		}

		Encounter.TryDebugStartEncounter(
			parts[1],
			out string result);

		return result;
	}

	/// <summary>
	/// Performs the execute start encounter pool operation for Debug Command Service.
	/// Uses the supplied arguments and current state and returns the resulting string to the caller.
	/// </summary>
	private string ExecuteStartEncounterPool(string[] parts)
	{
		if (parts.Length != 2)
		{
			return
				"Usage: .startEncounterPool <pool_id>\n" +
				"Example: .startEncounterPool " +
				"encounter_pool.core.training_region";
		}

		Encounter.TryDebugStartEncounterPool(
			parts[1],
			out string result);

		return result;
	}

	// Temporary compatibility path for the pre-period command language.
	/// <summary>
	/// Performs the execute legacy start encounter operation for Debug Command Service.
	/// Uses the supplied arguments and current state and returns the resulting string to the caller.
	/// </summary>
	private string ExecuteLegacyStartEncounter(string[] parts)
	{
		if (parts.Length == 1)
		{
			Encounter.JourneyState.BeginEncounter();
			return "Encounter started using the default Journey pool.";
		}

		return ExecuteStartEncounter(
			new[] { ".startEncounter", parts[1] });
	}

	/// <summary>
	/// Creates status text from the supplied configuration and current dependencies.
	/// Reads the current state and returns the resulting string to the caller.
	/// </summary>
	private string BuildStatusText()
	{
		StringBuilder output = new();

		output.AppendLine(
			$"Journey: {Encounter.JourneyState.CurrentState}");

		output.AppendLine(
			$"Combat active: {Combat.IsCombatActive}");

		output.AppendLine(
			$"Active heroes: {Combat.HeroParticipantCount}");

		output.AppendLine(
			$"Active monsters: {Encounter.ActiveMonsterCount}");

		output.AppendLine(
			$"Status timers: " +
			(DebugPresentationSettings.StatusEffectTimersVisible
				? "ON"
				: "OFF"));

		if (GodotObject.IsInstanceValid(RegionExploration))
			output.AppendLine(RegionExploration.BuildActiveRegionStatusText());

		output.AppendLine();
		output.AppendLine("Heroes:");

		foreach (HeroActorController hero in Party.SpawnedHeroes)
		{
			if (!GodotObject.IsInstanceValid(hero))
				continue;

			string contentId =
				GetHeroContentId(hero);

			string state =
				hero.IsIncapacitated
					? "Incapacitated"
					: "Active";

			string abilityState =
				BuildHeroAbilityStateText(hero);

			output.AppendLine(
				$"- {hero.Name} [{contentId}]: {state}, " +
				$"HP {hero.Health.CurrentHealth}/" +
				$"{hero.Health.MaximumHealth}" +
				abilityState);
		}

		output.AppendLine();
		output.AppendLine("Monsters:");

		foreach (
			MonsterActorController monster
			in Combat.MonsterParticipants)
		{
			if (!GodotObject.IsInstanceValid(monster)
				|| monster.IsDead)
			{
				continue;
			}

			string targetName =
				GodotObject.IsInstanceValid(
					monster.CurrentTarget)
					? monster.CurrentTarget!.Name.ToString()
					: "None";

			string forcedTargetText = "None";

			if (monster.HasForcedTarget
				&& GodotObject.IsInstanceValid(
					monster.ForcedTarget))
			{
				forcedTargetText =
					$"{GetHeroContentId(monster.ForcedTarget!)} " +
					$"({monster.ForcedTargetSecondsRemaining:0.0}s)";
			}

			List<string> threatEntries = new();

			foreach (
				HeroActorController hero
				in Party.SpawnedHeroes)
			{
				if (!GodotObject.IsInstanceValid(hero))
					continue;

				string currentTargetMarker =
					monster.CurrentTarget == hero
						? "*"
						: string.Empty;

				threatEntries.Add(
					$"{GetHeroContentId(hero)}=" +
					$"{monster.Threat.GetThreat(hero):0.##}" +
					currentTargetMarker);
			}

			output.AppendLine(
				$"- {monster.Name} [{monster.ContentId}]: " +
				$"Target={targetName}; " +
				$"ForcedTarget={forcedTargetText}; " +
				$"Threat: {string.Join(", ", threatEntries)}");
		}

		output.AppendLine(
			"* marks the monster's current target.");

		return output.ToString().TrimEnd();
	}

	/// <summary>
	/// Creates hero ability state text from the supplied configuration and current dependencies.
	/// Uses the supplied arguments and current state and returns the resulting string to the caller.
	/// </summary>
	private static string BuildHeroAbilityStateText(
		HeroActorController hero)
	{
		if (hero.Abilities.Count == 0)
			return string.Empty;

		List<string> abilityEntries = new();

		foreach (AbilityDefinition ability in hero.Abilities)
		{
			if (!GodotObject.IsInstanceValid(ability))
				continue;

			double cooldownRemaining =
				hero.GetAbilityCooldownRemaining(
					ability.ContentId);

			string cooldownState =
				cooldownRemaining > 0.0
					? $"{cooldownRemaining:0.0}s"
					: "Ready";

			abilityEntries.Add(
				$"{ability.DisplayName}={cooldownState}");
		}

		return abilityEntries.Count == 0
			? string.Empty
			: $"; Abilities: {string.Join(", ", abilityEntries)}";
	}

	/// <summary>
	/// Creates console reference text from the supplied configuration and current dependencies.
	/// Reads the current state and returns the resulting string to the caller.
	/// </summary>
	public string BuildConsoleReferenceText()
	{
		StringBuilder output = new();

		AppendCommandReference(output);

		AppendItemReference(output, ItemAcquisition.Registry);

		HeroContentRegistry heroRegistry =
			Party.Factory.Registry;

		MonsterContentRegistry monsterRegistry =
			Encounter.MonsterFactory.Registry;

		AbilityContentRegistry abilityRegistry =
			heroRegistry.AbilityRegistry;

		AppendMonsterReference(
			output,
			monsterRegistry,
			abilityRegistry);

		AppendClassAndHeroReference(
			output,
			heroRegistry,
			abilityRegistry);

		AppendAbilityReference(
			output,
			abilityRegistry);

		AppendEncounterReference(
			output,
			Encounter.EncounterRegistry);

		AppendEncounterPoolReference(
			output,
			Encounter.EncounterPoolRegistry);

		return output.ToString().TrimEnd();
	}

	/// <summary>
	/// Retrieves command completions from the current game state.
	/// Uses the supplied arguments and current state and returns the resulting i read only list string to the caller.
	/// </summary>
	public IReadOnlyList<string> GetCommandCompletions(
		string commandText,
		int caretColumn,
		out int replacementStart,
		out int replacementLength)
	{
		commandText ??= string.Empty;

		caretColumn = Math.Clamp(
			caretColumn,
			0,
			commandText.Length);

		int commandStart = FindCurrentCommandStart(
			commandText,
			caretColumn);

		replacementStart = FindTokenStart(
			commandText,
			commandStart,
			caretColumn);

		int replacementEnd = FindTokenEnd(
			commandText,
			caretColumn);

		replacementLength =
			replacementEnd - replacementStart;

		string tokenPrefix = commandText.Substring(
			replacementStart,
			caretColumn - replacementStart);

		string textBeforeToken = commandText.Substring(
			commandStart,
			replacementStart - commandStart);

		string[] completedTokens =
			textBeforeToken.Split(
				' ',
				StringSplitOptions.RemoveEmptyEntries);

		if (completedTokens.Length == 0)
		{
			return GetCommandNameCompletions(
				tokenPrefix);
		}

		string command =
			completedTokens[0].ToLowerInvariant();

		int argumentIndex =
			completedTokens.Length - 1;

		return GetArgumentCompletions(
			command,
			completedTokens,
			argumentIndex,
			tokenPrefix);
	}

	/// <summary>
	/// Performs the find current command start operation for Debug Command Service.
	/// Uses the supplied arguments and current state and returns the resulting int to the caller.
	/// </summary>
	private static int FindCurrentCommandStart(
		string commandText,
		int caretColumn)
	{
		string textBeforeCaret =
			commandText.Substring(0, caretColumn);

		int chainSeparatorIndex =
			textBeforeCaret.LastIndexOf(
				"&&",
				StringComparison.Ordinal);

		return chainSeparatorIndex < 0
			? 0
			: chainSeparatorIndex + 2;
	}

	/// <summary>
	/// Performs the find token start operation for Debug Command Service.
	/// Uses the supplied arguments and current state and returns the resulting int to the caller.
	/// </summary>
	private static int FindTokenStart(
		string commandText,
		int commandStart,
		int caretColumn)
	{
		int tokenStart = caretColumn;

		while (tokenStart > commandStart
			&& !char.IsWhiteSpace(
				commandText[tokenStart - 1])
			&& commandText[tokenStart - 1] != '&')
		{
			tokenStart--;
		}

		return tokenStart;
	}

	/// <summary>
	/// Performs the find token end operation for Debug Command Service.
	/// Uses the supplied arguments and current state and returns the resulting int to the caller.
	/// </summary>
	private static int FindTokenEnd(
		string commandText,
		int caretColumn)
	{
		int tokenEnd = caretColumn;

		while (tokenEnd < commandText.Length
			&& !char.IsWhiteSpace(commandText[tokenEnd])
			&& commandText[tokenEnd] != '&')
		{
			tokenEnd++;
		}

		return tokenEnd;
	}

	/// <summary>
	/// Retrieves command name completions from the current game state.
	/// Uses the supplied arguments and current state and returns the resulting i read only list string to the caller.
	/// </summary>
	private static IReadOnlyList<string>
		GetCommandNameCompletions(string tokenPrefix)
	{
		List<string> commandNames = new();

		foreach (string syntax in CanonicalCommandSyntax)
		{
			int firstSpaceIndex = syntax.IndexOf(' ');

			string commandName =
				firstSpaceIndex < 0
					? syntax
					: syntax.Substring(0, firstSpaceIndex);

			if (FindExactMatch(
				commandNames,
				commandName) is null)
			{
				commandNames.Add(commandName);
			}
		}

		List<string> matches = GetPrefixMatches(
			commandNames,
			tokenPrefix);

		string? exactMatch = FindExactMatch(
			matches,
			tokenPrefix);

		if (exactMatch is not null)
		{
			return new[]
			{
				AddArgumentSpaceIfNeeded(exactMatch)
			};
		}

		if (matches.Count == 1)
		{
			matches[0] =
				AddArgumentSpaceIfNeeded(matches[0]);
		}

		return matches;
	}

	/// <summary>
	/// Retrieves argument completions from the current game state.
	/// Uses the supplied arguments and current state and returns the resulting i read only list string to the caller.
	/// </summary>
	private IReadOnlyList<string> GetArgumentCompletions(
		string command,
		string[] completedTokens,
		int argumentIndex,
		string tokenPrefix)
	{
		IEnumerable<string>? completionSource =
			command switch
			{
				".revive" when argumentIndex == 0 =>
					GetCurrentHeroSelectors(),

				".kill" when argumentIndex == 0 =>
					GetCurrentHeroSelectors(),

				".testresource" when argumentIndex == 0 =>
					GetCurrentHeroSelectors(),

				".testresource" when argumentIndex == 1 =>
					new[] { "mana", "energy", "rage", "none" },

				".statustimers" when argumentIndex == 0 =>
					new[] { "on", "off", "toggle" },

				".kill" when argumentIndex == 1
					&& completedTokens.Length > 1
					&& completedTokens[1].Equals(
						"partySlot",
						StringComparison.OrdinalIgnoreCase) =>
					GetPartySlotNumbers(),

				".useability" when argumentIndex == 0 =>
					GetCurrentHeroSelectors(),

				".useability" when argumentIndex == 1
					&& completedTokens.Length > 1 =>
					GetHeroAbilityIds(completedTokens[1]),

				".additem" when argumentIndex == 0 =>
					ItemAcquisition.Registry.GetRegisteredIds(),

				".spawnmonster" when argumentIndex == 0 =>
					Encounter.MonsterFactory.Registry
						.GetRegisteredIds(),

				".startencounter" when argumentIndex == 0 =>
					Encounter.EncounterRegistry
						.GetRegisteredIds(),

				".startencounterpool" when argumentIndex == 0 =>
					Encounter.EncounterPoolRegistry
						.GetRegisteredIds(),

				_ => null
			};

		if (completionSource is null)
			return Array.Empty<string>();

		List<string> matches = GetPrefixMatches(
			completionSource,
			tokenPrefix);

		bool shouldAdvanceToNextArgument =
			command == ".useability"
			&& argumentIndex == 0;

		if (shouldAdvanceToNextArgument)
		{
			string? exactMatch = FindExactMatch(
				matches,
				tokenPrefix);

			if (exactMatch is not null)
			{
				return new[] { exactMatch + " " };
			}

			if (matches.Count == 1)
			{
				matches[0] += " ";
			}
		}

		return matches;
	}

	/// <summary>
	/// Retrieves current hero selectors from the current game state.
	/// Reads the current state and returns the resulting list string to the caller.
	/// </summary>
	private List<string> GetCurrentHeroSelectors()
	{
		HashSet<string> selectors = new(
			StringComparer.OrdinalIgnoreCase);

		foreach (HeroActorController hero
			in Party.SpawnedHeroes)
		{
			if (!GodotObject.IsInstanceValid(hero))
				continue;

			selectors.Add(hero.Name.ToString());
			selectors.Add(GetHeroContentId(hero));
		}

		return new List<string>(selectors);
	}

	/// <summary>
	/// Retrieves hero ability ids from the current game state.
	/// Uses the supplied arguments and current state and returns the resulting i enumerable string to the caller.
	/// </summary>
	private IEnumerable<string> GetHeroAbilityIds(
		string heroSelector)
	{
		HashSet<string> abilityIds = new(
			StringComparer.OrdinalIgnoreCase);

		foreach (HeroActorController hero
			in Party.SpawnedHeroes)
		{
			if (!GodotObject.IsInstanceValid(hero))
				continue;

			bool selectorMatches =
				hero.Name.ToString().Equals(
					heroSelector,
					StringComparison.OrdinalIgnoreCase)
				|| GetHeroContentId(hero).Equals(
					heroSelector,
					StringComparison.OrdinalIgnoreCase);

			if (!selectorMatches)
				continue;

			foreach (AbilityDefinition ability
				in hero.Abilities)
			{
				if (GodotObject.IsInstanceValid(ability))
				{
					abilityIds.Add(ability.ContentId);
				}
			}
		}

		return abilityIds;
	}

	/// <summary>
	/// Retrieves party slot numbers from the current game state.
	/// Reads the current state and returns the resulting i enumerable string to the caller.
	/// </summary>
	private static IEnumerable<string>
		GetPartySlotNumbers()
	{
		List<string> slotNumbers = new();

		for (int slotNumber = 1;
			slotNumber <= PartyController.MaximumPartySize;
			slotNumber++)
		{
			slotNumbers.Add(
				slotNumber.ToString(
					CultureInfo.InvariantCulture));
		}

		return slotNumbers;
	}

	/// <summary>
	/// Retrieves prefix matches from the current game state.
	/// Uses the supplied arguments and current state and returns the resulting list string to the caller.
	/// </summary>
	private static List<string> GetPrefixMatches(
		IEnumerable<string> values,
		string prefix)
	{
		HashSet<string> uniqueMatches = new(
			StringComparer.OrdinalIgnoreCase);

		foreach (string value in values)
		{
			if (!string.IsNullOrWhiteSpace(value)
				&& value.StartsWith(
					prefix,
					StringComparison.OrdinalIgnoreCase))
			{
				uniqueMatches.Add(value);
			}
		}

		List<string> matches =
			new(uniqueMatches);

		matches.Sort(
			StringComparer.OrdinalIgnoreCase);

		return matches;
	}

	/// <summary>
	/// Performs the find exact match operation for Debug Command Service.
	/// Uses the supplied arguments and current state and returns the resulting string to the caller.
	/// </summary>
	private static string? FindExactMatch(
		IEnumerable<string> values,
		string requestedValue)
	{
		foreach (string value in values)
		{
			if (value.Equals(
				requestedValue,
				StringComparison.OrdinalIgnoreCase))
			{
				return value;
			}
		}

		return null;
	}

	/// <summary>
	/// Performs the add argument space if needed operation for Debug Command Service.
	/// Uses the supplied arguments and current state and returns the resulting string to the caller.
	/// </summary>
	private static string AddArgumentSpaceIfNeeded(
		string commandName)
	{
		foreach (string syntax in CanonicalCommandSyntax)
		{
			if (syntax.StartsWith(
					commandName + " ",
					StringComparison.OrdinalIgnoreCase))
			{
				return commandName + " ";
			}
		}

		return commandName;
	}

	/// <summary>
	/// Performs the append command reference operation for Debug Command Service.
	/// Uses the supplied arguments and current node state; any result is applied through side effects, events, or stored fields.
	/// </summary>
	private static void AppendCommandReference(
		StringBuilder output)
	{
		output.AppendLine("COMMANDS");
		output.AppendLine("--------");

		foreach (string syntax in CanonicalCommandSyntax)
		{
			output.AppendLine(syntax);
		}

		output.AppendLine();
		output.AppendLine(
			"Use .help for descriptions, examples, chains, " +
			"and keyboard shortcuts.");
		output.AppendLine(
			"Press Tab / Shift+Tab to cycle command and ID " +
			"completions.");
	}

	private static void AppendItemReference(
		StringBuilder output,
		ItemContentRegistry registry)
	{
		AppendSectionHeader(output, "ITEMS");
		foreach (string contentId in GetSortedIds(registry.GetRegisteredIds()))
		{
			if (registry.TryGet(contentId, out ItemDefinition definition))
				output.AppendLine($"{definition.ContentId} - {definition.DisplayName}");
		}
	}

	/// <summary>
	/// Performs the append monster reference operation for Debug Command Service.
	/// Uses the supplied arguments and current node state; any result is applied through side effects, events, or stored fields.
	/// </summary>
	private static void AppendMonsterReference(
		StringBuilder output,
		MonsterContentRegistry monsterRegistry,
		AbilityContentRegistry abilityRegistry)
	{
		AppendSectionHeader(output, "MONSTERS");

		foreach (string contentId in GetSortedIds(
			monsterRegistry.GetRegisteredIds()))
		{
			if (!monsterRegistry.TryGet(
				contentId,
				out MonsterDefinition definition))
			{
				continue;
			}

			output.AppendLine(
				$"{definition.ContentId} - " +
				definition.DisplayName);

			AppendAbilityIds(
				output,
				definition.AbilityContentIds,
				abilityRegistry,
				"  Abilities");
		}
	}

	/// <summary>
	/// Performs the append class and hero reference operation for Debug Command Service.
	/// Uses the supplied arguments and current node state; any result is applied through side effects, events, or stored fields.
	/// </summary>
	private static void AppendClassAndHeroReference(
		StringBuilder output,
		HeroContentRegistry heroRegistry,
		AbilityContentRegistry abilityRegistry)
	{
		AppendSectionHeader(output, "CLASSES AND HEROES");

		Dictionary<string, HeroClassDefinition> classesById =
			new(StringComparer.OrdinalIgnoreCase);

		Dictionary<string, List<HeroDefinition>> heroesByClassId =
			new(StringComparer.OrdinalIgnoreCase);

		List<HeroDefinition> heroesWithoutClass = new();

		foreach (string heroContentId in GetSortedIds(
			heroRegistry.GetRegisteredIds()))
		{
			if (!heroRegistry.TryGet(
				heroContentId,
				out HeroDefinition hero))
			{
				continue;
			}

			if (!GodotObject.IsInstanceValid(
				hero.ClassDefinition))
			{
				heroesWithoutClass.Add(hero);
				continue;
			}

			HeroClassDefinition heroClass =
				hero.ClassDefinition;

			classesById[heroClass.ContentId] =
				heroClass;

			if (!heroesByClassId.TryGetValue(
				heroClass.ContentId,
				out List<HeroDefinition>? classHeroes))
			{
				classHeroes = new List<HeroDefinition>();
				heroesByClassId.Add(
					heroClass.ContentId,
					classHeroes);
			}

			classHeroes.Add(hero);
		}

		foreach (string classContentId in GetSortedIds(
			classesById.Keys))
		{
			HeroClassDefinition heroClass =
				classesById[classContentId];

			output.AppendLine(
				$"{heroClass.ContentId} - " +
				heroClass.DisplayName);

			AppendAbilityIds(
				output,
				heroClass.AbilityContentIds,
				abilityRegistry,
				"  Class abilities");

			List<HeroDefinition> classHeroes =
				heroesByClassId[classContentId];

			classHeroes.Sort(
				(left, right) =>
					StringComparer.OrdinalIgnoreCase.Compare(
						left.ContentId,
						right.ContentId));

			output.AppendLine("  Heroes:");

			foreach (HeroDefinition hero in classHeroes)
			{
				output.AppendLine(
					$"    {hero.ContentId} - " +
					hero.DisplayName);

				AppendAbilityIds(
					output,
					hero.GetStartingEquippedAbilityIds(),
					abilityRegistry,
					"      Hero abilities");
			}
		}

		if (heroesWithoutClass.Count == 0)
			return;

		heroesWithoutClass.Sort(
			(left, right) =>
				StringComparer.OrdinalIgnoreCase.Compare(
					left.ContentId,
					right.ContentId));

		output.AppendLine("No class assigned:");

		foreach (HeroDefinition hero in heroesWithoutClass)
		{
			output.AppendLine(
				$"  {hero.ContentId} - " +
				hero.DisplayName);

			AppendAbilityIds(
				output,
				hero.GetStartingEquippedAbilityIds(),
				abilityRegistry,
				"    Hero abilities");
		}
	}

	/// <summary>
	/// Performs the append ability reference operation for assigned.
	/// Uses the supplied arguments and current node state; any result is applied through side effects, events, or stored fields.
	/// </summary>
	private static void AppendAbilityReference(
		StringBuilder output,
		AbilityContentRegistry abilityRegistry)
	{
		AppendSectionHeader(output, "ABILITIES");

		foreach (string contentId in GetSortedIds(
			abilityRegistry.GetRegisteredIds()))
		{
			if (!abilityRegistry.TryGet(
				contentId,
				out AbilityDefinition definition))
			{
				continue;
			}

			output.AppendLine(
				$"{definition.ContentId} - " +
				definition.DisplayName);
		}
	}

	/// <summary>
	/// Performs the append encounter reference operation for assigned.
	/// Uses the supplied arguments and current node state; any result is applied through side effects, events, or stored fields.
	/// </summary>
	private static void AppendEncounterReference(
		StringBuilder output,
		EncounterContentRegistry encounterRegistry)
	{
		AppendSectionHeader(output, "ENCOUNTERS");

		foreach (string contentId in GetSortedIds(
			encounterRegistry.GetRegisteredIds()))
		{
			if (!encounterRegistry.TryGet(
				contentId,
				out EncounterDefinition definition))
			{
				continue;
			}

			output.AppendLine(
				$"{definition.ContentId} - " +
				definition.DisplayName);
		}
	}

	/// <summary>
	/// Performs the append encounter pool reference operation for assigned.
	/// Uses the supplied arguments and current node state; any result is applied through side effects, events, or stored fields.
	/// </summary>
	private static void AppendEncounterPoolReference(
		StringBuilder output,
		EncounterPoolContentRegistry encounterPoolRegistry)
	{
		AppendSectionHeader(output, "ENCOUNTER POOLS");

		foreach (string contentId in GetSortedIds(
			encounterPoolRegistry.GetRegisteredIds()))
		{
			if (!encounterPoolRegistry.TryGet(
				contentId,
				out EncounterPoolDefinition definition))
			{
				continue;
			}

			output.AppendLine(
				$"{definition.ContentId} - " +
				definition.DisplayName);
		}
	}

	/// <summary>
	/// Performs the append ability ids operation for assigned.
	/// Uses the supplied arguments and current node state; any result is applied through side effects, events, or stored fields.
	/// </summary>
	private static void AppendAbilityIds(
	StringBuilder output,
	IEnumerable<string> abilityContentIds,
	AbilityContentRegistry abilityRegistry,
	string label)
{
	List<string> sortedAbilityIds =
		new(abilityContentIds);

	if (sortedAbilityIds.Count == 0)
	{
		output.AppendLine($"{label}: None");
		return;
	}

	output.AppendLine($"{label}:");

	sortedAbilityIds.Sort(
		StringComparer.OrdinalIgnoreCase);

	foreach (string abilityContentId in sortedAbilityIds)
	{
		string displayName = "Unregistered";

		if (abilityRegistry.TryGet(
			abilityContentId,
			out AbilityDefinition definition))
		{
			displayName = definition.DisplayName;
		}

		int leadingSpaceCount =
			label.Length - label.TrimStart().Length;

		string entryIndent =
			new(' ', leadingSpaceCount + 2);

		output.AppendLine(
			entryIndent +
				$"{abilityContentId} - {displayName}");
	}
}

	/// <summary>
	/// Performs the append section header operation for assigned.
	/// Uses the supplied arguments and current node state; any result is applied through side effects, events, or stored fields.
	/// </summary>
	private static void AppendSectionHeader(
		StringBuilder output,
		string title)
	{
		output.AppendLine();
		output.AppendLine();
		output.AppendLine(title);
		output.AppendLine(
			new string('-', title.Length));
	}

	/// <summary>
	/// Retrieves sorted ids from the current game state.
	/// Uses the supplied arguments and current state and returns the resulting list string to the caller.
	/// </summary>
	private static List<string> GetSortedIds(
		IEnumerable<string> contentIds)
	{
		List<string> sortedIds =
			new(contentIds);

		sortedIds.Sort(
			StringComparer.OrdinalIgnoreCase);

		return sortedIds;
	}

	/// <summary>
	/// Creates help text from the supplied configuration and current dependencies.
	/// Reads the current state and returns the resulting string to the caller.
	/// </summary>
	private static string BuildHelpText()
	{
		return
			"QUESTBAR DEBUG CONSOLE\n" +
			"Canonical commands use: .<action> <ID/arguments>\n" +
			"Content IDs are stable data identifiers, not display names.\n" +
			"Older pre-period commands remain temporary compatibility aliases but are no longer\n" +
			"documented as the preferred syntax.\n\n" +

			"COMMAND CHAINS\n" +
			"--------------\n" +
			"Use && between complete commands to execute them sequentially from left to right.\n" +
			"The whole chain is stored as one history entry, so Up Arrow recalls the recipe.\n" +
			"Later commands still run if an earlier command reports an error.\n" +
			"Examples:\n" +
			"  .reviveAll && .startEncounterPool encounter_pool.core.training_region\n" +
			"  .reviveAll && .spawnMonster monster.core.heavy_training_monster 3\n" +
			"  .endEncounter && .reviveAll && .startEncounter encounter.core.heavy_patrol\n" +
			"  .startEncounter encounter.core.training_mix && .status\n\n" +

			"GENERAL\n" +
			"-------\n" +
			".help\n" +
			"    Show this command reference.\n" +
			"    Example: .help\n\n" +

			".status\n" +
			"    Print Journey/combat state, active hero/monster counts, and each hero's HP.\n" +
			"    Useful for verifying cleanup, wipes, or apparently stuck fights.\n" +
			"    Examples:\n" +
			"      .status\n" +
			"      .startEncounter encounter.core.heavy_patrol && .status\n\n" +

			".statusTimers [on|off|toggle]\n" +
			"    Show or change the developer status-effect timer overlay.\n" +
			"    This only affects labels such as [FRZ 2.4]; status gameplay continues normally.\n" +
			"    Omitting the argument prints the current state. The development default is ON.\n" +
			"    Examples:\n" +
			"      .statusTimers\n" +
			"      .statusTimers off\n" +
			"      .statusTimers on\n" +
			"      .statusTimers toggle\n\n" +

			".completeRegionExploration\n" +
			"    Set the active region's saved Traveling time to its authored maximum.\n" +
			"    Fog and destinations then reveal through the normal exploration system.\n" +
			"    This is not a visual fog bypass and the resulting progress is saved.\n" +
			"    Example: .completeRegionExploration\n\n" +

			".clear\n" +
			"    Clear visible console history without changing game state. Chainable with &&.\n" +
			"    Examples:\n" +
			"      .clear\n" +
			"      .clear && .status\n\n" +

			"HEROES\n" +
			"------\n" +
			".revive <hero_id>\n" +
			"    Restore one hero to full health and return it to the active hero roster.\n" +
			"    Accepts a HeroDefinition content ID or a runtime party-slot name.\n" +
			"    If multiple slots use the same hero content ID, use PartySlotNHero to choose.\n" +
			"    Matching is case-insensitive.\n" +
			"    Examples:\n" +
			"      .revive hero.core.syzygy\n" +
			"      .revive PartySlot1Hero\n\n" +

			".reviveAll\n" +
			"    Restore every equipped party hero, refill health, and rebuild combat participants.\n" +
			"    Best reset command after a Defeat.\n" +
			"    Examples:\n" +
			"      .reviveAll\n" +
			"      .reviveAll && .startEncounter encounter.core.training_swarm\n\n" +

			".kill <hero_id>\n" +
			".kill partySlot <1-5>\n" +
			"    Incapacitate one equipped hero through the normal combat cleanup path.\n" +
			"    Accepts a HeroDefinition content ID, runtime party-slot name, or slot number.\n" +
			"    Use this to verify monster target reacquisition after its target dies.\n" +
			"    Examples:\n" +
			"      .kill hero.core.syzygy\n" +
			"      .kill PartySlot1Hero\n" +
			"      .kill partySlot 1\n" +
			"      .kill partySlot(1)\n\n" +

			".testResource <hero_id> <mana|energy|rage|none> [spend]\n" +
			"    Temporarily assign a 100-point resource without changing class data.\n" +
			"    The optional spend amount defaults to 50; the pool regenerates 10 every 2s.\n" +
			"    Use none to remove the test override and hide the bar again.\n" +
			"    Examples:\n" +
			"      .testResource PartySlot1Hero energy\n" +
			"      .testResource PartySlot2Hero mana 75\n" +
			"      .testResource PartySlot1Hero none\n\n" +

			".useAbility <hero_id> <ability_id>\n" +
			"    Execute one equipped hero ability through normal cooldown enforcement.\n" +
			"    Use .status to inspect whether the ability is ready or cooling down.\n" +
			"    Examples:\n" +
			"      .useAbility hero.core.syzygy ability.core.taunt\n" +
			"      .useAbility PartySlot1Hero ability.core.taunt\n\n" +

			"MONSTERS\n" +
			"--------\n" +
			".spawnMonster <monster_id> [count]\n" +
			"    Spawn an exact MonsterDefinition. If Traveling, starts a debug encounter without\n" +
			"    injecting the normal automatic encounter composition. Count defaults to 1 and is\n" +
			"    clamped to 1-100.\n" +
			"    Known IDs:\n" +
			"      monster.core.training_monster\n" +
			"      monster.core.heavy_training_monster\n" +
			"    Examples:\n" +
			"      .spawnMonster monster.core.training_monster\n" +
			"      .spawnMonster monster.core.training_monster 5\n" +
			"      .spawnMonster monster.core.heavy_training_monster 2\n\n" +

			".addMonsters <count>\n" +
			"    Convenience command that adds the default monster type. If Traveling, normal\n" +
			"    encounter startup may occur first. Count is clamped to 1-100.\n" +
			"    Examples:\n" +
			"      .addMonsters 1\n" +
			"      .addMonsters 25\n\n" +

			".setMonsterCount <count>\n" +
			"    Ensure AT LEAST this many active monsters exist. Adds missing monsters but never\n" +
			"    removes extras. Count is clamped to 1-100.\n" +
			"    Examples:\n" +
			"      .setMonsterCount 20\n" +
			"      .setMonsterCount 100\n\n" +

			"ENCOUNTERS\n" +
			"----------\n" +
			".startEncounter <encounter_id>\n" +
			"    Start one exact EncounterDefinition. This bypasses pool selection, making it the\n" +
			"    deterministic command for testing a specific composition.\n" +
			"    Registered IDs:\n" +
			"      encounter.core.training_mix    - 2-4 Training + 0-1 Heavy\n" +
			"      encounter.core.training_swarm  - 5-8 Training\n" +
			"      encounter.core.heavy_patrol    - 0-2 Training + 2-3 Heavy\n" +
			"    Examples:\n" +
			"      .startEncounter encounter.core.training_mix\n" +
			"      .startEncounter encounter.core.training_swarm\n" +
			"      .startEncounter encounter.core.heavy_patrol\n\n" +

			".startEncounterPool <pool_id>\n" +
			"    Start an encounter by rolling a registered EncounterPoolDefinition. This is the\n" +
			"    closest debug equivalent to normal Journey encounter selection.\n" +
			"    Current pool:\n" +
			"      encounter_pool.core.training_region\n" +
			"        Training Mix=60, Training Swarm=25, Heavy Patrol=15\n" +
			"    Examples:\n" +
			"      .startEncounterPool encounter_pool.core.training_region\n" +
			"      .reviveAll && .startEncounterPool encounter_pool.core.training_region\n\n" +

			".endEncounter\n" +
			"    Abort the active encounter and return Journey to Traveling. This is cleanup, not\n" +
			"    a combat Victory.\n" +
			"    Examples:\n" +
			"      .endEncounter\n" +
			"      .endEncounter && .reviveAll\n\n" +

			"QUICK TEST RECIPES\n" +
			"------------------\n" +
			"    Reset after a wipe and roll the normal Training Region pool:\n" +
			"      .reviveAll && .startEncounterPool encounter_pool.core.training_region\n\n" +
			"    Reset and force a Training Swarm:\n" +
			"      .reviveAll && .startEncounter encounter.core.training_swarm\n\n" +
			"    Reset and test only heavy-monster behavior:\n" +
			"      .reviveAll && .spawnMonster monster.core.heavy_training_monster 2\n\n" +
			"    Abort, reset, then force Heavy Patrol:\n" +
			"      .endEncounter && .reviveAll && .startEncounter encounter.core.heavy_patrol\n\n" +
			"    Force Training Mix and immediately inspect state:\n" +
			"      .startEncounter encounter.core.training_mix && .status\n\n" +

			"KEYBOARD SHORTCUTS\n" +
			"------------------\n" +
			"    Tab           Next command/ID completion\n" +
			"    Shift+Tab     Previous command/ID completion\n" +
			"    Ctrl+Shift+D  Toggle debug console\n" +
			"    Ctrl+Shift+R  Revive/reset all heroes\n" +
			"    Ctrl+Shift+1  Add 1 default monster\n" +
			"    Ctrl+Shift+5  Add 5 default monsters\n" +
			"    Ctrl+Shift+X  End encounter";
	}

	/// <summary>
	/// Runs Godot setup for assigned when the node enters the scene tree.
	/// Uses the current node and service state; any result is applied through side effects, events, or stored fields.
	/// </summary>
	public override void _Ready()
	{
		SetProcessInput(true);

		DebugLog.Print(
			"DebugCommandService ready. " +
			"Type .help in the debug console for canonical commands.");
	}

	/// <summary>
	/// Resets heroes so the system can begin from a clean state.
	/// Uses the current node and service state; any result is applied through side effects, events, or stored fields.
	/// </summary>
	public void ResetHeroes()
	{
		foreach (HeroActorController hero in Party.SpawnedHeroes)
		{
			if (!GodotObject.IsInstanceValid(hero))
				continue;

			hero.DebugResetFromIncapacitation();
		}

		Combat.DebugRefreshHeroParticipants();

		DebugLog.Print(
			"Debug command completed: .reviveAll");
	}

	/// <summary>
	/// Performs the add monsters operation for assigned.
	/// Uses the supplied arguments and current node state; any result is applied through side effects, events, or stored fields.
	/// </summary>
	public void AddMonsters(int count)
	{
		Encounter.DebugAddMonsters(count);

		DebugLog.Print(
			$"Debug command completed: .addMonsters {count}");
	}

	/// <summary>
	/// Performs the start encounter operation for assigned.
	/// Uses the current node and service state; any result is applied through side effects, events, or stored fields.
	/// </summary>
	public void StartEncounter()
	{
		Encounter.JourneyState.BeginEncounter();
	}

	/// <summary>
	/// Performs the end encounter operation for assigned.
	/// Uses the current node and service state; any result is applied through side effects, events, or stored fields.
	/// </summary>
	public void EndEncounter()
	{
		Encounter.JourneyState.EndEncounter();
	}

	/// <summary>
	/// Performs the unhandled key input operation for assigned.
	/// Uses the supplied arguments and current node state; any result is applied through side effects, events, or stored fields.
	/// </summary>
	public override void _UnhandledKeyInput(InputEvent @event)
	{
		if (@event is not InputEventKey keyEvent)
			return;

		if (!keyEvent.Pressed || keyEvent.Echo)
			return;

		if (!keyEvent.CtrlPressed || !keyEvent.ShiftPressed)
			return;

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

	/// <summary>
	/// Performs the execute spawn monster operation for assigned.
	/// Uses the supplied arguments and current state and returns the resulting string to the caller.
	/// </summary>
	private string ExecuteSpawnMonster(string[] parts)
	{
		if (parts.Length < 2)
		{
			return
				"Usage: .spawnMonster <monster_id> [count]\n" +
				"Example: .spawnMonster " +
				"monster.core.training_monster 5";
		}

		string contentId = parts[1];
		int count = 1;

		if (parts.Length >= 3
			&& (!int.TryParse(
					parts[2],
					NumberStyles.Integer,
					CultureInfo.InvariantCulture,
					out count)
				|| count < 1))
		{
			return "Count must be a positive integer.";
		}

		count = Math.Clamp(count, 1, 100);

		int spawned =
			Encounter.DebugAddMonsters(
				contentId,
				count);

		return spawned == count
			? $"Spawned {spawned} instance(s) of {contentId}."
			: $"Spawned {spawned} of {count} requested " +
			  $"instance(s) of {contentId}.";
	}

	/// <summary>
	/// Performs the execute operation for assigned.
	/// Uses the supplied arguments and current state and returns the resulting string to the caller.
	/// </summary>
	public string Execute(string commandText)
	{
		string[] parts =
			commandText.Trim().Split(
				' ',
				StringSplitOptions.RemoveEmptyEntries);

		if (parts.Length == 0)
			return string.Empty;

		string command = parts[0].ToLowerInvariant();

		return command switch
		{
			".help" or "help" =>
				BuildHelpText(),

			".status" or "status" =>
				BuildStatusText(),

			".statustimers" =>
				ExecuteStatusTimers(parts),

			".completeregionexploration" =>
				ExecuteCompleteRegionExploration(parts),

			".revive" =>
				ExecuteReviveHero(parts),

			".reviveall" or "heroes.reset" =>
				ExecuteReviveAll(),

			".kill" =>
				ExecuteKillHero(parts),

			".testresource" =>
				ExecuteTestResource(parts),

			".useability" =>
				ExecuteUseAbility(parts),

			".additem" =>
				ExecuteAddItem(parts),

			".addcurrency" =>
				ExecuteCurrencyChange(parts, spend: false),

			".spendcurrency" =>
				ExecuteCurrencyChange(parts, spend: true),

			".saveinventory" =>
				parts.Length == 1 ? InventoryPersistence.Save() : "Usage: .saveInventory",

			".loadinventory" =>
				parts.Length == 1 ? InventoryPersistence.Load() : "Usage: .loadInventory",

			".spawnmonster" or "monster.spawn" =>
				ExecuteSpawnMonster(parts),

			".addmonsters" or "monsters.add" =>
				ExecuteAddMonsters(parts),

			".setmonstercount" or "monsters.set" =>
				ExecuteSetMonsterCount(parts),

			".startencounter" =>
				ExecuteStartEncounter(parts),

			".startencounterpool" =>
				ExecuteStartEncounterPool(parts),

			".endencounter" or "encounter.end" =>
				ExecuteEndEncounter(),

			"encounter.start" =>
				ExecuteLegacyStartEncounter(parts),

			_ =>
				$"Unknown command: {parts[0]}\n" +
                "Type '.help' for available commands."
		};
	}

	/// <summary>
	/// Completes the active region by setting its saved Traveling time to the
	/// same authored maximum reached through ordinary idle exploration.
	/// </summary>
	private string ExecuteCompleteRegionExploration(string[] parts)
	{
		if (parts.Length != 1)
			return "Usage: .completeRegionExploration";

		if (!GodotObject.IsInstanceValid(RegionExploration))
		{
			return
				"RegionExplorationService is not assigned to " +
				"DebugCommandService.";
		}

		return RegionExploration.CompleteActiveRegionExploration();
	}

	private string ExecuteAddItem(string[] parts)
	{
		if (parts.Length < 2 || parts.Length > 3)
			return "Usage: .addItem <item_id> [quantity]";

		int quantity = 1;
		if (parts.Length == 3 && (!int.TryParse(parts[2], out quantity) || quantity < 1))
			return "Quantity must be a positive whole number.";

		ItemAcquisition.TryAcquire(parts[1], quantity, out string result);
		return result;
	}

	private string ExecuteCurrencyChange(string[] parts, bool spend)
	{
		string command = spend ? ".spendCurrency" : ".addCurrency";
		if (parts.Length != 2 ||
			!long.TryParse(parts[1], NumberStyles.Integer,
				CultureInfo.InvariantCulture, out long amount) ||
			amount <= 0)
		{
			return $"Usage: {command} <positive_copper_amount>";
		}

		string error;
		bool succeeded = spend
			? Backpack.Currency.TrySpend(amount, out error)
			: Backpack.Currency.TryAdd(amount, out error);

		if (!succeeded)
			return error;

		return
			$"{(spend ? "Spent" : "Added")} {amount} copper. " +
			$"Balance={Backpack.Currency}.";
	}

	/// <summary>
	/// Shows or changes the global developer status-effect timer overlay.
	/// This changes presentation only; active status effects continue running.
	/// </summary>
	private static string ExecuteStatusTimers(string[] parts)
	{
		if (parts.Length == 1)
		{
			return
				$"Status effect debug timers: " +
				(DebugPresentationSettings.StatusEffectTimersVisible
					? "ON"
					: "OFF") + ".";
		}

		if (parts.Length != 2)
			return BuildStatusTimersUsage();

		string option = parts[1].Trim().ToLowerInvariant();

		switch (option)
		{
			case "on":
				DebugPresentationSettings
					.SetStatusEffectTimersVisible(true);
				break;

			case "off":
				DebugPresentationSettings
					.SetStatusEffectTimersVisible(false);
				break;

			case "toggle":
				DebugPresentationSettings
					.ToggleStatusEffectTimersVisible();
				break;

			default:
				return BuildStatusTimersUsage();
		}

		return
			$"Status effect debug timers: " +
			(DebugPresentationSettings.StatusEffectTimersVisible
				? "ON"
				: "OFF") + ".";
	}

	private static string BuildStatusTimersUsage()
	{
		return
			"Usage: .statusTimers [on|off|toggle]\n" +
			"Examples: .statusTimers off | .statusTimers on | " +
			".statusTimers toggle";
	}

	/// <summary>
	/// Assigns a temporary resource to one hero and immediately spends part of
	/// it so the bar color, fill, and timed regeneration can be verified.
	/// </summary>
	private string ExecuteTestResource(string[] parts)
	{
		if (parts.Length < 3 || parts.Length > 4)
		{
			return
				"Usage: .testResource <hero_id> " +
				"<mana|energy|rage|none> [spend]\n" +
				"Example: .testResource PartySlot1Hero energy 50";
		}

		if (!System.Enum.TryParse(
			parts[2],
			true,
			out HeroResourceType resourceType)
			|| !System.Enum.IsDefined(
				typeof(HeroResourceType),
				resourceType))
		{
			return "Resource type must be mana, energy, rage, or none.";
		}

		float spendAmount = 50.0f;

		if (parts.Length == 4
			&& (!float.TryParse(
				parts[3],
				NumberStyles.Float,
				CultureInfo.InvariantCulture,
				out spendAmount)
				|| spendAmount < 0.0f
				|| spendAmount > 100.0f))
		{
			return "Spend amount must be a number from 0 to 100.";
		}

		string requestedHeroId = parts[1];
		List<HeroActorController> matches = new();

		foreach (HeroActorController hero in Party.SpawnedHeroes)
		{
			if (!GodotObject.IsInstanceValid(hero))
				continue;

			if (hero.Name.ToString().Equals(
					requestedHeroId,
					StringComparison.OrdinalIgnoreCase)
				|| GetHeroContentId(hero).Equals(
					requestedHeroId,
					StringComparison.OrdinalIgnoreCase))
			{
				matches.Add(hero);
			}
		}

		if (matches.Count == 0)
		{
			return
				$"Unknown hero ID '{requestedHeroId}'.\n" +
				BuildAvailableHeroIdsText();
		}

		if (matches.Count > 1)
		{
			return
				$"Hero ID '{requestedHeroId}' matches multiple " +
				"party members. Use a runtime PartySlotNHero name.";
		}

		HeroActorController selectedHero = matches[0];
		selectedHero.DebugConfigureResource(resourceType);

		if (resourceType == HeroResourceType.None)
		{
			return
				$"Removed the temporary resource from " +
				$"{selectedHero.Name}.";
		}

		selectedHero.DebugTrySpendResource(spendAmount);

		return
			$"{selectedHero.Name} now has a temporary " +
			$"{resourceType} pool. Current=" +
			$"{selectedHero.Resource.CurrentAmount:0.##}/" +
			$"{selectedHero.Resource.MaximumAmount:0.##}; " +
			"regeneration=10 every 2 seconds.";
	}

	/// <summary>
	/// Executes one equipped hero ability through the normal targeting and
	/// cooldown path, returning a readable result to the debug console.
	/// </summary>
	private string ExecuteUseAbility(string[] parts)
	{
		if (parts.Length != 3)
		{
			return
				"Usage: .useAbility <hero_id> <ability_id>\n" +
				"Example: .useAbility hero.core.syzygy " +
				"ability.core.taunt";
		}

		string requestedHeroId = parts[1];
		string abilityContentId = parts[2];
		List<HeroActorController> matches = new();

		foreach (HeroActorController hero in Party.SpawnedHeroes)
		{
			if (!GodotObject.IsInstanceValid(hero))
				continue;

			bool runtimeNameMatches =
				hero.Name.ToString().Equals(
					requestedHeroId,
					StringComparison.OrdinalIgnoreCase);

			bool contentIdMatches =
				GetHeroContentId(hero).Equals(
					requestedHeroId,
					StringComparison.OrdinalIgnoreCase);

			if (runtimeNameMatches || contentIdMatches)
				matches.Add(hero);
		}

		if (matches.Count == 0)
		{
			return
				$"Unknown hero ID '{requestedHeroId}'.\n" +
				BuildAvailableHeroIdsText();
		}

		if (matches.Count > 1)
		{
			return
				$"Hero ID '{requestedHeroId}' matches multiple " +
				"party members. Use a runtime PartySlotNHero name.";
		}

		Combat.TryUseHeroAbility(
			matches[0],
			abilityContentId,
			out string result);

		return result;
	}

	/// <summary>
	/// Performs the execute revive hero operation for assigned.
	/// Uses the supplied arguments and current state and returns the resulting string to the caller.
	/// </summary>
	private string ExecuteReviveHero(string[] parts)
	{
		if (parts.Length != 2)
		{
			return
				"Usage: .revive <hero_id>\n" +
				"Example: .revive hero.core.starting_hero";
		}

		string requestedHeroId = parts[1];
		List<HeroActorController> matches = new();

		foreach (HeroActorController hero in Party.SpawnedHeroes)
		{
			if (!GodotObject.IsInstanceValid(hero))
				continue;

			bool runtimeNameMatches =
				hero.Name.ToString().Equals(
					requestedHeroId,
					StringComparison.OrdinalIgnoreCase);

			bool contentIdMatches =
				GetHeroContentId(hero).Equals(
					requestedHeroId,
					StringComparison.OrdinalIgnoreCase);

			if (!runtimeNameMatches && !contentIdMatches)
			{
				continue;
			}

			matches.Add(hero);
		}

		if (matches.Count > 1)
		{
			StringBuilder output = new();

			output.AppendLine(
				$"Hero ID '{requestedHeroId}' matches " +
				$"multiple party members.");

			output.AppendLine(
				"Use one runtime party-slot name:");

			foreach (HeroActorController hero in matches)
			{
				output.AppendLine(
					$"- {hero.Name}");
			}

			return output.ToString().TrimEnd();
		}

		if (matches.Count == 1)
		{
			HeroActorController hero = matches[0];

			hero.DebugResetFromIncapacitation();
			Combat.DebugRefreshHeroParticipants();

			DebugLog.Print(
				$"Debug command completed: .revive {hero.Name}");

			return
				$"Revived {hero.Name}. " +
				$"HP={hero.Health.CurrentHealth}/" +
				$"{hero.Health.MaximumHealth}.";
		}

		return
			$"Unknown hero ID '{requestedHeroId}'.\n" +
			BuildAvailableHeroIdsText();
	}

	/// <summary>
	/// Performs the execute kill hero operation for assigned.
	/// Uses the supplied arguments and current state and returns the resulting string to the caller.
	/// </summary>
	private string ExecuteKillHero(string[] parts)
	{
		if (!TryReadHeroSelector(
			parts,
			out string requestedHeroId,
			out string usageError))
		{
			return usageError;
		}

		List<HeroActorController> matches = new();

		foreach (HeroActorController hero in Party.SpawnedHeroes)
		{
			if (!GodotObject.IsInstanceValid(hero))
				continue;

			bool runtimeNameMatches =
				hero.Name.ToString().Equals(
					requestedHeroId,
					StringComparison.OrdinalIgnoreCase);

			bool contentIdMatches =
				GetHeroContentId(hero).Equals(
					requestedHeroId,
					StringComparison.OrdinalIgnoreCase);

			if (runtimeNameMatches || contentIdMatches)
			{
				matches.Add(hero);
			}
		}

		if (matches.Count > 1)
		{
			StringBuilder output = new();

			output.AppendLine(
				$"Hero ID '{requestedHeroId}' matches " +
				$"multiple party members.");

			output.AppendLine(
				"Use one runtime party-slot name:");

			foreach (HeroActorController hero in matches)
			{
				output.AppendLine(
					$"- {hero.Name}");
			}

			return output.ToString().TrimEnd();
		}

		if (matches.Count == 0)
		{
			return
				$"Unknown hero ID '{requestedHeroId}'.\n" +
				BuildAvailableHeroIdsText();
		}

		HeroActorController selectedHero = matches[0];

		if (selectedHero.IsIncapacitated
			|| !selectedHero.Health.IsAlive)
		{
			return $"{selectedHero.Name} is already incapacitated.";
		}

		if (!Combat.DebugIncapacitateHero(selectedHero))
		{
			return
				$"Could not incapacitate {selectedHero.Name}.";
		}

		DebugLog.Print(
			$"Debug command completed: .kill {selectedHero.Name}");

		return
			$"Incapacitated {selectedHero.Name} " +
			$"({GetHeroContentId(selectedHero)}).";
	}

	/// <summary>
	/// Attempts to read hero selector without throwing when the operation cannot be completed.
	/// Uses the supplied arguments and current state and returns the resulting bool to the caller.
	/// </summary>
	private static bool TryReadHeroSelector(
		string[] parts,
		out string requestedHeroId,
		out string error)
	{
		requestedHeroId = string.Empty;
		error =
			"Usage:\n" +
			"  .kill <hero_id>\n" +
			"  .kill partySlot <1-5>\n" +
			"Examples:\n" +
			"  .kill hero.core.syzygy\n" +
			"  .kill partySlot 1";

		if (parts.Length == 3
			&& parts[1].Equals(
				"partySlot",
				StringComparison.OrdinalIgnoreCase))
		{
			if (!TryBuildPartySlotRuntimeName(
				parts[2],
				out requestedHeroId))
			{
				error = "Party slot must be a number from 1 to 5.";
				return false;
			}

			return true;
		}

		if (parts.Length != 2)
			return false;

		string selector = parts[1];

		const string partySlotPrefix = "partySlot(";

		if (selector.StartsWith(
				partySlotPrefix,
				StringComparison.OrdinalIgnoreCase)
			&& selector.EndsWith(
				")",
				StringComparison.Ordinal))
		{
			string slotText = selector.Substring(
				partySlotPrefix.Length,
				selector.Length
					- partySlotPrefix.Length
					- 1);

			if (!TryBuildPartySlotRuntimeName(
				slotText,
				out requestedHeroId))
			{
				error = "Party slot must be a number from 1 to 5.";
				return false;
			}

			return true;
		}

		requestedHeroId = selector;
		return true;
	}

	/// <summary>
	/// Attempts to build party slot runtime name without throwing when the operation cannot be completed.
	/// Uses the supplied arguments and current state and returns the resulting bool to the caller.
	/// </summary>
	private static bool TryBuildPartySlotRuntimeName(
		string slotText,
		out string runtimeName)
	{
		runtimeName = string.Empty;

		if (!int.TryParse(
				slotText,
				NumberStyles.Integer,
				CultureInfo.InvariantCulture,
				out int slotNumber)
			|| slotNumber < 1
			|| slotNumber > PartyController.MaximumPartySize)
		{
			return false;
		}

		runtimeName =
			$"PartySlot{slotNumber}Hero";

		return true;
	}

	/// <summary>
	/// Performs the execute revive all operation for assigned.
	/// Reads the current state and returns the resulting string to the caller.
	/// </summary>
	private string ExecuteReviveAll()
	{
		ResetHeroes();

		return
			"All equipped party heroes were restored " +
			"and combat participants were refreshed.";
	}

	/// <summary>
	/// Creates available hero ids text from the supplied configuration and current dependencies.
	/// Reads the current state and returns the resulting string to the caller.
	/// </summary>
	private string BuildAvailableHeroIdsText()
	{
		StringBuilder output = new(
			"Current party heroes:");

		foreach (HeroActorController hero in Party.SpawnedHeroes)
		{
			if (!GodotObject.IsInstanceValid(hero))
				continue;

			output.AppendLine();
			output.Append(
				$"- {hero.Name} " +
				$"({GetHeroContentId(hero)})");
		}

		return output.ToString();
	}

	/// <summary>
	/// Retrieves hero content id from the current game state.
	/// Uses the supplied arguments and current state and returns the resulting string to the caller.
	/// </summary>
	private static string GetHeroContentId(
		HeroActorController hero)
	{
		if (GodotObject.IsInstanceValid(hero.Definition)
			&& !string.IsNullOrWhiteSpace(
				hero.Definition!.ContentId))
		{
			return hero.Definition.ContentId.Trim();
		}

		return hero.Name.ToString();
	}

	/// <summary>
	/// Performs the execute add monsters operation for assigned.
	/// Uses the supplied arguments and current state and returns the resulting string to the caller.
	/// </summary>
	private string ExecuteAddMonsters(string[] parts)
	{
		if (!TryReadCount(parts, out int count))
		{
			return
				"Usage: .addMonsters <count>\n" +
				"Example: .addMonsters 5";
		}

		Encounter.DebugAddMonsters(count);

		return
			$"Added {count} monster(s). " +
			$"Active monsters={Encounter.ActiveMonsterCount}.";
	}
}
