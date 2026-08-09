using Godot;
using System.Collections.Generic;

public partial class AbilityContentRegistry : Node
{
    [ExportCategory("Ability Content")]

    [Export]
    public Godot.Collections.Array<AbilityDefinition>
        Definitions
    { get; set; } = new();

    private readonly Dictionary<string, AbilityDefinition>
        _definitionsById = new();

    public int Count => _definitionsById.Count;

    public override void _Ready()
    {
        Rebuild();

        DebugLog.Print(
            $"AbilityContentRegistry initialized with " +
            $"{Count} definition(s).");

        foreach (AbilityDefinition definition
            in _definitionsById.Values)
        {
            DebugLog.Print(
                $"Ability content loaded: " +
                $"{definition.ContentId} " +
                $"('{definition.DisplayName}'). " +
                $"Cooldown={definition.CooldownSeconds:0.##}s, " +
                $"Cast={definition.CastTimeSeconds:0.##}s, " +
                $"Range={definition.Range:0.##}, " +
                $"Damage={definition.BaseDamage:0.##}, " +
                $"Target={definition.TargetMode}.");
        }
    }

    public void Rebuild()
    {
        _definitionsById.Clear();

        foreach (AbilityDefinition definition
            in Definitions)
        {
            Register(definition);
        }
    }

    public bool TryGet(
        string contentId,
        out AbilityDefinition definition)
    {
        string normalizedId = Normalize(contentId);

        return _definitionsById.TryGetValue(
            normalizedId,
            out definition!);
    }

    public AbilityDefinition GetRequired(
        string contentId)
    {
        if (TryGet(
            contentId,
            out AbilityDefinition definition))
        {
            return definition;
        }

        throw new KeyNotFoundException(
            $"No AbilityDefinition is registered for " +
            $"Content ID '{contentId}'.");
    }

    public IReadOnlyCollection<string> GetRegisteredIds()
    {
        return _definitionsById.Keys;
    }

    private void Register(
        AbilityDefinition definition)
    {
        if (!GodotObject.IsInstanceValid(definition))
        {
            GD.PushError(
                "AbilityContentRegistry contains a missing " +
                "AbilityDefinition.");

            return;
        }

        IReadOnlyList<string> errors =
            definition.GetValidationErrors();

        if (errors.Count > 0)
        {
            foreach (string error in errors)
            {
                GD.PushError(error);
                DebugLog.Print(
                    $"Ability content error: {error}");
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
                $"Duplicate ability Content ID " +
                $"'{definition.ContentId}'.";

            GD.PushError(message);
            DebugLog.Print(
                $"Ability content error: {message}");
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
