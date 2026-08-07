using Godot;
using System.Collections.Generic;

public partial class MonsterContentRegistry : Node
{
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
        Rebuild();

        GD.Print(
            $"MonsterContentRegistry initialized with " +
            $"{Count} definition(s).");
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

        IReadOnlyList<string> errors =
            definition.GetValidationErrors();

        if (errors.Count > 0)
        {
            foreach (string error in errors)
            {
                GD.PushError(error);
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