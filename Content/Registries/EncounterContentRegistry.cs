using Godot;
using System.Collections.Generic;

public partial class EncounterContentRegistry : Node
{
    [ExportCategory("Dependencies")]
    [Export]
    public MonsterContentRegistry MonsterRegistry
    { get; set; } = null!;

    [ExportCategory("Encounter Content")]
    [Export]
    public Godot.Collections.Array<EncounterDefinition>
        Definitions
    { get; set; } = new();

    private readonly Dictionary<string, EncounterDefinition>
        _definitionsById = new();

    public int Count => _definitionsById.Count;

    public override void _Ready()
    {
        Rebuild();

        DebugLog.Print(
            $"EncounterContentRegistry initialized with " +
            $"{Count} definition(s).");

        foreach (EncounterDefinition definition
            in _definitionsById.Values)
        {
            PrintDefinition(definition);
        }
    }

    public void Rebuild()
    {
        _definitionsById.Clear();

        if (!GodotObject.IsInstanceValid(MonsterRegistry))
        {
            GD.PushError(
                "EncounterContentRegistry is missing its " +
                "MonsterRegistry Inspector reference.");

            DebugLog.Print(
                "EncounterContentRegistry could not rebuild: " +
                "MonsterRegistry reference is missing.");

            return;
        }

        foreach (EncounterDefinition definition in Definitions)
        {
            Register(definition);
        }
    }

    public bool TryGet(
        string contentId,
        out EncounterDefinition definition)
    {
        string normalizedId = Normalize(contentId);

        return _definitionsById.TryGetValue(
            normalizedId,
            out definition!);
    }

    public EncounterDefinition GetRequired(
        string contentId)
    {
        if (TryGet(contentId, out EncounterDefinition definition))
            return definition;

        throw new KeyNotFoundException(
            $"No EncounterDefinition is registered for " +
            $"Content ID '{contentId}'.");
    }

    public IReadOnlyCollection<string> GetRegisteredIds()
    {
        return _definitionsById.Keys;
    }

    private void Register(EncounterDefinition definition)
    {
        if (!GodotObject.IsInstanceValid(definition))
        {
            GD.PushError(
                "EncounterContentRegistry contains a missing " +
                "EncounterDefinition.");

            return;
        }

        IReadOnlyList<string> errors =
            definition.GetValidationErrors();

        foreach (EncounterMonsterEntry entry
            in definition.MonsterComposition)
        {
            if (!GodotObject.IsInstanceValid(entry)
                || string.IsNullOrWhiteSpace(entry.MonsterContentId))
            {
                continue;
            }

            if (!MonsterRegistry.TryGet(
                entry.MonsterContentId,
                out _))
            {
                List<string> combinedErrors = new(errors)
                {
                    $"{definition.ContentId}: unknown monster " +
                    $"Content ID '{entry.MonsterContentId}'."
                };

                errors = combinedErrors;
            }
        }

        if (errors.Count > 0)
        {
            foreach (string error in errors)
            {
                GD.PushError(error);
                DebugLog.Print($"Encounter content error: {error}");
            }

            return;
        }

        string normalizedId = Normalize(definition.ContentId);

        if (!_definitionsById.TryAdd(normalizedId, definition))
        {
            string message =
                $"Duplicate encounter Content ID " +
                $"'{definition.ContentId}'.";

            GD.PushError(message);
            DebugLog.Print($"Encounter content error: {message}");
        }
    }

    private static void PrintDefinition(
        EncounterDefinition definition)
    {
        DebugLog.Print(
            $"Encounter content loaded: " +
            $"{definition.ContentId} " +
            $"('{definition.DisplayName}').");

        foreach (EncounterMonsterEntry entry
            in definition.MonsterComposition)
        {
            DebugLog.Print(
                $"  {entry.MonsterContentId}: " +
                $"{entry.MinimumCount}-{entry.MaximumCount}");
        }
    }

    private static string Normalize(string contentId)
    {
        return contentId.Trim().ToLowerInvariant();
    }
}
