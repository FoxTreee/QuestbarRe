using Godot;
using System.Collections.Generic;

public partial class MonsterContentRegistry : Node
{
    [ExportCategory("Dependencies")]

    [Export]
    public AbilityContentRegistry AbilityRegistry
    { get; set; } = null!;

    [ExportCategory("Monster Content")]
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

        // Ability content may appear later in sibling scene order.
        // Rebuild explicitly so monster loadouts can validate now.
        AbilityRegistry.Rebuild();
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

    public IReadOnlyCollection<string>
        GetRegisteredIds()
    {
        return _definitionsById.Keys;
    }

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

    private static string Normalize(
        string contentId)
    {
        return contentId
            .Trim()
            .ToLowerInvariant();
    }
}