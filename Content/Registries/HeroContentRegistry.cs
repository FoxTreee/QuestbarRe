using Godot;
using System.Collections.Generic;

public partial class HeroContentRegistry : Node
{
    [ExportCategory("Dependencies")]

    [Export]
    public AbilityContentRegistry AbilityRegistry
    { get; set; } = null!;

    [ExportCategory("Hero Content")]

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
            if (definition.AbilityContentIds.Count == 0)
                continue;

            DebugLog.Print(
                $"Hero ability loadout: " +
                $"{definition.ContentId} " +
                $"('{definition.DisplayName}') -> " +
                $"{string.Join(", ", definition.AbilityContentIds)}");
        }
    }

    public void Rebuild()
    {
        _definitionsById.Clear();

        foreach (HeroDefinition definition
            in Definitions)
        {
            Register(definition);
        }
    }

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

    public IReadOnlyCollection<string>
        GetRegisteredIds()
    {
        return _definitionsById.Keys;
    }

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

    private static string Normalize(
        string contentId)
    {
        return contentId
            .Trim()
            .ToLowerInvariant();
    }
}
