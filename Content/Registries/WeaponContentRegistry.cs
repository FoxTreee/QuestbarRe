using Godot;
using System.Collections.Generic;

public partial class WeaponContentRegistry : Node
{
    [ExportCategory("Weapon Content")]

    [Export]
    public Godot.Collections.Array<WeaponDefinition>
        Definitions
    { get; set; } = new();

    private readonly Dictionary<string, WeaponDefinition>
        _definitionsById = new();

    public int Count => _definitionsById.Count;


    public override void _Ready()
    {
        Rebuild();

        DebugLog.Print(
            $"WeaponContentRegistry initialized with " +
            $"{Count} definition(s).");

        foreach (WeaponDefinition definition
            in _definitionsById.Values)
        {
            DebugLog.Print(
                $"Weapon content loaded: " +
                $"{definition.ContentId} " +
                $"('{definition.DisplayName}'). " +
                $"Damage={definition.MinimumDamage:0.##}-" +
                $"{definition.MaximumDamage:0.##}, " +
                $"Speed={definition.AttackSpeedSeconds:0.00}, " +
                $"Type={definition.WeaponType}, " +
                $"Handedness={definition.Handedness}.");
        }
    }


    public void Rebuild()
    {
        _definitionsById.Clear();

        foreach (WeaponDefinition definition
            in Definitions)
        {
            Register(definition);
        }
    }


    public bool TryGet(
        string contentId,
        out WeaponDefinition definition)
    {
        string normalizedId = Normalize(contentId);

        return _definitionsById.TryGetValue(
            normalizedId,
            out definition!);
    }


    public WeaponDefinition GetRequired(
        string contentId)
    {
        if (TryGet(
            contentId,
            out WeaponDefinition definition))
        {
            return definition;
        }

        throw new KeyNotFoundException(
            $"No WeaponDefinition is registered for " +
            $"Content ID '{contentId}'.");
    }


    public IReadOnlyCollection<string> GetRegisteredIds()
    {
        return _definitionsById.Keys;
    }


    private void Register(
        WeaponDefinition definition)
    {
        if (!GodotObject.IsInstanceValid(definition))
        {
            GD.PushError(
                "WeaponContentRegistry contains a missing " +
                "WeaponDefinition.");

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
                    $"Weapon content error: {error}");
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
                $"Duplicate weapon Content ID " +
                $"'{definition.ContentId}'.";

            GD.PushError(message);
            DebugLog.Print(
                $"Weapon content error: {message}");
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
