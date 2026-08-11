using Godot;
using System.Collections.Generic;

public partial class AbilityContentRegistry : Node
{
    [ExportCategory("Ability Content")]

    /// <summary>
    /// Controls definitions.
    /// For example, adding another entry gives the owning system one more configured definitions to use.
    /// </summary>
    [Export]
    public Godot.Collections.Array<AbilityDefinition>
        Definitions
    { get; set; } = new();

    private readonly Dictionary<string, AbilityDefinition>
        _definitionsById = new();

    public int Count => _definitionsById.Count;

    /// <summary>
    /// Runs Godot setup for Ability Content Registry when the node enters the scene tree.
    /// Uses the current node and service state; any result is applied through side effects, events, or stored fields.
    /// </summary>
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

    /// <summary>
    /// Performs the rebuild operation for Ability Content Registry.
    /// Uses the current node and service state; any result is applied through side effects, events, or stored fields.
    /// </summary>
    public void Rebuild()
    {
        _definitionsById.Clear();

        foreach (AbilityDefinition definition
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
        out AbilityDefinition definition)
    {
        string normalizedId = Normalize(contentId);

        return _definitionsById.TryGetValue(
            normalizedId,
            out definition!);
    }

    /// <summary>
    /// Retrieves required from the current game state.
    /// Uses the supplied arguments and current state and returns the resulting ability definition to the caller.
    /// </summary>
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

    /// <summary>
    /// Retrieves registered ids from the current game state.
    /// Reads the current state and returns the resulting i read only collection string to the caller.
    /// </summary>
    public IReadOnlyCollection<string> GetRegisteredIds()
    {
        return _definitionsById.Keys;
    }

    /// <summary>
    /// Performs the register operation for Ability Content Registry.
    /// Uses the supplied arguments and current node state; any result is applied through side effects, events, or stored fields.
    /// </summary>
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

    /// <summary>
    /// Performs the normalize operation for Ability Content Registry.
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
