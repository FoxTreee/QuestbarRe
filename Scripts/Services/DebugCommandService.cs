using Godot;
using System;
using System.Collections.Generic;
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
    public PartyController Party { get; set; } = null!;

    [Export]
    public DebugConsoleController Console { get; set; } = null!;

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

    private string ExecuteEndEncounter()
    {
        Encounter.JourneyState.EndEncounter();
        return "Encounter ended.";
    }

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

            output.AppendLine(
                $"- {hero.Name} [{contentId}]: {state}, " +
                $"HP {hero.Health.CurrentHealth}/" +
                $"{hero.Health.MaximumHealth}");
        }

        return output.ToString().TrimEnd();
    }

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
            "      .revive hero.core.starting_hero\n" +
            "      .revive PartySlot1Hero\n\n" +

            ".reviveAll\n" +
            "    Restore every equipped party hero, refill health, and rebuild combat participants.\n" +
            "    Best reset command after a Defeat.\n" +
            "    Examples:\n" +
            "      .reviveAll\n" +
            "      .reviveAll && .startEncounter encounter.core.training_swarm\n\n" +

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
            "    Ctrl+Shift+D  Toggle debug console\n" +
            "    Ctrl+Shift+R  Revive/reset all heroes\n" +
            "    Ctrl+Shift+1  Add 1 default monster\n" +
            "    Ctrl+Shift+5  Add 5 default monsters\n" +
            "    Ctrl+Shift+X  End encounter";
    }

    public override void _Ready()
    {
        SetProcessInput(true);

        DebugLog.Print(
            "DebugCommandService ready. " +
            "Type .help in the debug console for canonical commands.");
    }

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

    public void AddMonsters(int count)
    {
        Encounter.DebugAddMonsters(count);

        DebugLog.Print(
            $"Debug command completed: .addMonsters {count}");
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

            ".revive" =>
                ExecuteReviveHero(parts),

            ".reviveall" or "heroes.reset" =>
                ExecuteReviveAll(),

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

    private string ExecuteReviveAll()
    {
        ResetHeroes();

        return
            "All equipped party heroes were restored " +
            "and combat participants were refreshed.";
    }

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