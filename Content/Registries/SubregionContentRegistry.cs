using Godot;
using System;
using System.Collections.Generic;

/// <summary>
/// Resolves authored subregions by stable content ID so map nodes never hold
/// direct scene-specific presentation references.
/// </summary>
public partial class SubregionContentRegistry : Node
{
    [ExportCategory("Subregion Content")]

    /// <summary>
    /// Every subregion definition available to the current game build.
    /// </summary>
    [Export]
    public Godot.Collections.Array<SubregionDefinition> Definitions
    { get; set; } = new();

    private readonly Dictionary<string, SubregionDefinition>
        _definitionsById = new(StringComparer.OrdinalIgnoreCase);

    public int Count => _definitionsById.Count;

    /// <summary>
    /// Validates and indexes the authored definitions after scene references
    /// have resolved.
    /// </summary>
    public override void _Ready()
    {
        Rebuild();
        DebugLog.Print(
            $"SubregionContentRegistry initialized with {Count} " +
            "definition(s).");
    }

    /// <summary>
    /// Rebuilds the lookup without changing any authored resources.
    /// </summary>
    public void Rebuild()
    {
        _definitionsById.Clear();

        foreach (SubregionDefinition definition in Definitions)
            Register(definition);
    }

    /// <summary>
    /// Resolves a subregion without throwing when content is missing.
    /// </summary>
    public bool TryGet(
        string contentId,
        out SubregionDefinition definition)
    {
        return _definitionsById.TryGetValue(
            Normalize(contentId),
            out definition!);
    }

    private void Register(SubregionDefinition definition)
    {
        if (!GodotObject.IsInstanceValid(definition))
        {
            GD.PushError(
                "SubregionContentRegistry contains a missing definition.");
            return;
        }

        IReadOnlyList<string> errors = definition.GetValidationErrors();

        if (errors.Count > 0)
        {
            foreach (string error in errors)
            {
                GD.PushError(error);
                DebugLog.Print($"Subregion content error: {error}");
            }

            return;
        }

        string normalizedId = Normalize(definition.ContentId);

        if (_definitionsById.TryAdd(normalizedId, definition))
            return;

        string message =
            $"Duplicate subregion Content ID '{definition.ContentId}'.";
        GD.PushError(message);
        DebugLog.Print($"Subregion content error: {message}");
    }

    private static string Normalize(string contentId)
    {
        return contentId.Trim().ToLowerInvariant();
    }
}
