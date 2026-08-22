using Godot;
using System.Collections.Generic;

public partial class MonsterContentRegistry : Node
{
    [ExportCategory("Dependencies")]

    /// <summary>
    /// Inspector reference used by this component for its ability registry dependency.
    /// Assign the matching node or resource from the scene; leaving it empty prevents that connection from working.
    /// </summary>
    [Export]
    public AbilityContentRegistry AbilityRegistry
    { get; set; } = null!;

    /// <summary>
    /// Resolves and validates every item Content ID authored in monster loot
    /// tables before those monsters are made available to encounter content.
    /// </summary>
    [Export]
    public ItemContentRegistry ItemRegistry
    { get; set; } = null!;

    [ExportCategory("Monster Content")]
    /// <summary>
    /// Controls definitions.
    /// For example, adding another entry gives the owning system one more configured definitions to use.
    /// </summary>
    [Export]
    public Godot.Collections.Array<MonsterDefinition>
        Definitions
    {
        get;
        set;
    } = new();

    private readonly Dictionary<string, MonsterDefinition>
        _definitionsById =
            new();

    public int Count =>
        _definitionsById.Count;

    /// <summary>
    /// Runs Godot setup for Monster Content Registry when the node enters the scene tree.
    /// Uses the current node and service state; any result is applied through side effects, events, or stored fields.
    /// </summary>
    public override void _Ready()
    {
        if (!GodotObject.IsInstanceValid(AbilityRegistry))
        {
            GD.PushError(
                "MonsterContentRegistry is missing its " +
                "AbilityRegistry Inspector reference.");

            DebugLog.Print(
                "MonsterContentRegistry could not rebuild: " +
                "AbilityRegistry reference is missing.");

            return;
        }

        if (!GodotObject.IsInstanceValid(ItemRegistry))
        {
            GD.PushError(
                "MonsterContentRegistry is missing its " +
                "ItemRegistry Inspector reference.");

            DebugLog.Print(
                "MonsterContentRegistry could not rebuild: " +
                "ItemRegistry reference is missing.");

            return;
        }

        // Referenced content may appear later in sibling scene order. Rebuild
        // both registries explicitly so monster content can validate now.
        AbilityRegistry.Rebuild();
        ItemRegistry.Rebuild();
        Rebuild();

        DebugLog.Print(
            $"MonsterContentRegistry initialized with " +
            $"{Count} definition(s).");

        foreach (MonsterDefinition definition
            in _definitionsById.Values)
        {
            if (definition.AbilityContentIds.Count == 0)
                continue;

            DebugLog.Print(
                $"Monster ability loadout: " +
                $"{definition.ContentId} " +
                $"('{definition.DisplayName}') -> " +
                $"{string.Join(", ", definition.AbilityContentIds)}");
        }
    }

    /// <summary>
    /// Performs the rebuild operation for Monster Content Registry.
    /// Uses the current node and service state; any result is applied through side effects, events, or stored fields.
    /// </summary>
    public void Rebuild()
    {
        _definitionsById.Clear();

        foreach (
            MonsterDefinition definition
            in Definitions)
        {
            Register(definition);
        }
    }

    /// <summary>
    /// Attempts to get without throwing when the operation cannot be completed.
    /// Uses the supplied arguments and current state and returns the resulting bool to the caller.
    /// </summary>
    public bool TryGet(
        string contentId,
        out MonsterDefinition definition)
    {
        string normalizedId =
            Normalize(contentId);

        return _definitionsById.TryGetValue(
            normalizedId,
            out definition!);
    }

    /// <summary>
    /// Retrieves required from the current game state.
    /// Uses the supplied arguments and current state and returns the resulting monster definition to the caller.
    /// </summary>
    public MonsterDefinition GetRequired(
        string contentId)
    {
        if (TryGet(
            contentId,
            out MonsterDefinition definition))
        {
            return definition;
        }

        throw new KeyNotFoundException(
            $"No MonsterDefinition is registered for " +
            $"Content ID '{contentId}'.");
    }

    /// <summary>
    /// Retrieves registered ids from the current game state.
    /// Reads the current state and returns the resulting i read only collection string to the caller.
    /// </summary>
    public IReadOnlyCollection<string>
        GetRegisteredIds()
    {
        return _definitionsById.Keys;
    }

    /// <summary>
    /// Performs the register operation for Monster Content Registry.
    /// Uses the supplied arguments and current node state; any result is applied through side effects, events, or stored fields.
    /// </summary>
    private void Register(
        MonsterDefinition definition)
    {
        if (!GodotObject.IsInstanceValid(definition))
        {
            GD.PushError(
                "MonsterContentRegistry contains a " +
                "missing MonsterDefinition.");

            return;
        }

        List<string> errors =
            new(definition.GetValidationErrors());

        foreach (string abilityContentId
            in definition.AbilityContentIds)
        {
            if (!global::ContentId.IsValid(abilityContentId))
                continue;

            if (!AbilityRegistry.TryGet(
                abilityContentId,
                out _))
            {
                errors.Add(
                    $"{definition.ContentId}: unknown ability " +
                    $"Content ID '{abilityContentId}'.");
            }
        }

        foreach (MonsterLootEntry lootEntry in definition.LootTable)
        {
            if (!GodotObject.IsInstanceValid(lootEntry)
                || !global::ContentId.IsValid(lootEntry.ItemContentId))
            {
                continue;
            }

            if (!ItemRegistry.TryGet(
                lootEntry.ItemContentId,
                out ItemDefinition itemDefinition))
            {
                errors.Add(
                    $"{definition.ContentId}: unknown loot item " +
                    $"Content ID '{lootEntry.ItemContentId}'.");
                continue;
            }

            if (itemDefinition.IsUnique
                && (lootEntry.MinimumQuantity != 1
                    || lootEntry.MaximumQuantity != 1))
            {
                errors.Add(
                    $"{definition.ContentId}: unique loot item " +
                    $"'{lootEntry.ItemContentId}' must use a fixed " +
                    "quantity of one.");
            }
        }

        if (errors.Count > 0)
        {
            foreach (string error in errors)
            {
                GD.PushError(error);
                DebugLog.Print(
                    $"Monster content error: {error}");
            }

            return;
        }

        string normalizedId =
            Normalize(definition.ContentId);

        if (!_definitionsById.TryAdd(
            normalizedId,
            definition))
        {
            GD.PushError(
                $"Duplicate monster Content ID " +
                $"'{definition.ContentId}'.");
        }
    }

    /// <summary>
    /// Performs the normalize operation for Monster Content Registry.
    /// Uses the supplied arguments and current state and returns the resulting string to the caller.
    /// </summary>
    private static string Normalize(
        string contentId)
    {
        return contentId
            .Trim()
            .ToLowerInvariant();
    }
}
