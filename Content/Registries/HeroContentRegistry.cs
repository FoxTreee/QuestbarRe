using Godot;
using System.Collections.Generic;

public partial class HeroContentRegistry : Node
{
    [ExportCategory("Dependencies")]

    /// <summary>
    /// Inspector reference used by this component for its ability registry dependency.
    /// Assign the matching node or resource from the scene; leaving it empty prevents that connection from working.
    /// </summary>
    [Export]
    public AbilityContentRegistry AbilityRegistry
    { get; set; } = null!;

    [ExportCategory("Hero Content")]

    /// <summary>
    /// Controls definitions.
    /// For example, adding another entry gives the owning system one more configured definitions to use.
    /// </summary>
    [Export]
    public Godot.Collections.Array<HeroDefinition>
        Definitions
    {
        get;
        set;
    } = new();

    private readonly Dictionary<string, HeroDefinition>
        _definitionsById =
            new();

    public int Count =>
        _definitionsById.Count;

    /// <summary>
    /// Runs Godot setup for Hero Content Registry when the node enters the scene tree.
    /// Uses the current node and service state; any result is applied through side effects, events, or stored fields.
    /// </summary>
    public override void _Ready()
    {
        if (!GodotObject.IsInstanceValid(AbilityRegistry))
        {
            GD.PushError(
                "HeroContentRegistry is missing its " +
                "AbilityRegistry Inspector reference.");

            DebugLog.Print(
                "HeroContentRegistry could not rebuild: " +
                "AbilityRegistry reference is missing.");

            return;
        }

        // Ability content may appear later in sibling scene order.
        // Rebuild explicitly so hero loadouts can validate now.
        AbilityRegistry.Rebuild();
        Rebuild();

        DebugLog.Print(
            $"HeroContentRegistry initialized with " +
            $"{Count} definition(s).");

        foreach (HeroDefinition definition
            in _definitionsById.Values)
        {
            if (GodotObject.IsInstanceValid(
                definition.ClassDefinition)
                && definition.ClassDefinition.AbilityContentIds.Count > 0)
            {
                DebugLog.Print(
                    $"Class ability loadout: " +
                    $"{definition.ClassDefinition.ContentId} " +
                    $"('{definition.ClassDefinition.DisplayName}') -> " +
                    $"{string.Join(", ", definition.ClassDefinition.AbilityContentIds)}");
            }

            if (definition.AbilityContentIds.Count > 0)
            {
                DebugLog.Print(
                    $"Hero-specific ability loadout: " +
                    $"{definition.ContentId} " +
                    $"('{definition.DisplayName}') -> " +
                    $"{string.Join(", ", definition.AbilityContentIds)}");
            }
        }
    }

    /// <summary>
    /// Performs the rebuild operation for Hero Content Registry.
    /// Uses the current node and service state; any result is applied through side effects, events, or stored fields.
    /// </summary>
    public void Rebuild()
    {
        _definitionsById.Clear();

        foreach (HeroDefinition definition
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
        out HeroDefinition definition)
    {
        string normalizedId =
            Normalize(contentId);

        return _definitionsById.TryGetValue(
            normalizedId,
            out definition!);
    }

    /// <summary>
    /// Retrieves required from the current game state.
    /// Uses the supplied arguments and current state and returns the resulting hero definition to the caller.
    /// </summary>
    public HeroDefinition GetRequired(
        string contentId)
    {
        if (TryGet(
            contentId,
            out HeroDefinition definition))
        {
            return definition;
        }

        throw new KeyNotFoundException(
            $"No HeroDefinition is registered for " +
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
    /// Performs the register operation for Hero Content Registry.
    /// Uses the supplied arguments and current node state; any result is applied through side effects, events, or stored fields.
    /// </summary>
    private void Register(
        HeroDefinition definition)
    {
        if (!GodotObject.IsInstanceValid(definition))
        {
            GD.PushError(
                "HeroContentRegistry contains a " +
                "missing HeroDefinition.");

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

        if (GodotObject.IsInstanceValid(
            definition.ClassDefinition))
        {
            foreach (string abilityContentId
                in definition.ClassDefinition.AbilityContentIds)
            {
                if (!global::ContentId.IsValid(abilityContentId))
                    continue;

                if (!AbilityRegistry.TryGet(
                    abilityContentId,
                    out _))
                {
                    errors.Add(
                        $"{definition.ClassDefinition.ContentId}: " +
                        $"unknown ability Content ID " +
                        $"'{abilityContentId}'.");
                }
            }
        }

        if (errors.Count > 0)
        {
            foreach (string error in errors)
            {
                GD.PushError(error);

                DebugLog.Print(
                    $"Hero content error: {error}");
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
                $"Duplicate hero Content ID " +
                $"'{definition.ContentId}'.";

            GD.PushError(message);

            DebugLog.Print(
                $"Hero content error: {message}");
        }
    }

    /// <summary>
    /// Performs the normalize operation for Hero Content Registry.
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
