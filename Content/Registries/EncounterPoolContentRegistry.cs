using Godot;
using System.Collections.Generic;

public partial class EncounterPoolContentRegistry : Node
{
    [ExportCategory("Dependencies")]
    [Export]
    public EncounterContentRegistry EncounterRegistry
    { get; set; } = null!;

    [ExportCategory("Encounter Pool Content")]
    [Export]
    public Godot.Collections.Array<EncounterPoolDefinition>
        Definitions
    { get; set; } = new();

    private readonly Dictionary<string, EncounterPoolDefinition>
        _definitionsById = new();

    public int Count => _definitionsById.Count;

    public override void _Ready()
    {
        Rebuild();

        DebugLog.Print(
            $"EncounterPoolContentRegistry initialized with " +
            $"{Count} definition(s).");

        foreach (EncounterPoolDefinition definition
            in _definitionsById.Values)
        {
            PrintDefinition(definition);
        }
    }

    public void Rebuild()
    {
        _definitionsById.Clear();

        if (!GodotObject.IsInstanceValid(EncounterRegistry))
        {
            GD.PushError(
                "EncounterPoolContentRegistry is missing its " +
                "EncounterRegistry Inspector reference.");

            DebugLog.Print(
                "EncounterPoolContentRegistry could not rebuild: " +
                "EncounterRegistry reference is missing.");

            return;
        }

        foreach (EncounterPoolDefinition definition
            in Definitions)
        {
            Register(definition);
        }
    }

    public bool TryGet(
        string contentId,
        out EncounterPoolDefinition definition)
    {
        string normalizedId = Normalize(contentId);

        return _definitionsById.TryGetValue(
            normalizedId,
            out definition!);
    }

    public EncounterPoolDefinition GetRequired(
        string contentId)
    {
        if (TryGet(
            contentId,
            out EncounterPoolDefinition definition))
        {
            return definition;
        }

        throw new KeyNotFoundException(
            $"No EncounterPoolDefinition is registered for " +
            $"Content ID '{contentId}'.");
    }

    public IReadOnlyCollection<string> GetRegisteredIds()
    {
        return _definitionsById.Keys;
    }

    private void Register(
        EncounterPoolDefinition definition)
    {
        if (!GodotObject.IsInstanceValid(definition))
        {
            GD.PushError(
                "EncounterPoolContentRegistry contains a missing " +
                "EncounterPoolDefinition.");

            return;
        }

        List<string> errors =
            new(definition.GetValidationErrors());

        foreach (EncounterPoolEntry entry
            in definition.Entries)
        {
            if (!GodotObject.IsInstanceValid(entry)
                || string.IsNullOrWhiteSpace(
                    entry.EncounterContentId))
            {
                continue;
            }

            if (!EncounterRegistry.TryGet(
                entry.EncounterContentId,
                out _))
            {
                errors.Add(
                    $"{definition.ContentId}: unknown encounter " +
                    $"Content ID '{entry.EncounterContentId}'.");
            }
        }

        if (errors.Count > 0)
        {
            foreach (string error in errors)
            {
                GD.PushError(error);

                DebugLog.Print(
                    $"Encounter pool content error: {error}");
            }

            return;
        }

        string normalizedId =
            Normalize(definition.ContentId);

        if (!_definitionsById.TryAdd(
            normalizedId,
            definition))
        {
            string message =
                $"Duplicate encounter pool Content ID " +
                $"'{definition.ContentId}'.";

            GD.PushError(message);

            DebugLog.Print(
                $"Encounter pool content error: {message}");
        }
    }

    private static void PrintDefinition(
        EncounterPoolDefinition definition)
    {
        DebugLog.Print(
            $"Encounter pool loaded: " +
            $"{definition.ContentId} " +
            $"('{definition.DisplayName}'). " +
            $"Total weight={definition.GetTotalWeight()}.");

        foreach (EncounterPoolEntry entry
            in definition.Entries)
        {
            DebugLog.Print(
                $"  {entry.EncounterContentId}: " +
                $"weight={entry.Weight}");
        }
    }

    private static string Normalize(string contentId)
    {
        return contentId.Trim().ToLowerInvariant();
    }
}
