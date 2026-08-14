using Godot;
using System;
using System.Collections.Generic;

public partial class ItemContentRegistry : Node
{
    [ExportCategory("All Inventory-Capable Content")]
    [Export] public Godot.Collections.Array<ItemDefinition> Definitions { get; set; } = new();

    private readonly Dictionary<string, ItemDefinition> _byId =
        new(StringComparer.OrdinalIgnoreCase);

    public override void _Ready() => Rebuild();

    public void Rebuild()
    {
        _byId.Clear();
        foreach (ItemDefinition definition in Definitions)
        {
            if (!GodotObject.IsInstanceValid(definition))
            {
                GD.PushError("ItemContentRegistry contains a missing definition.");
                continue;
            }

            IReadOnlyList<string> errors = definition.GetValidationErrors();
            if (errors.Count > 0)
            {
                foreach (string error in errors) GD.PushError(error);
                continue;
            }

            if (!_byId.TryAdd(definition.ContentId.Trim(), definition))
                GD.PushError($"Duplicate item Content ID '{definition.ContentId}'.");
        }

        DebugLog.Print($"ItemContentRegistry initialized with {_byId.Count} definition(s).");
    }

    public bool TryGet(string contentId, out ItemDefinition definition) =>
        _byId.TryGetValue(contentId?.Trim() ?? string.Empty, out definition!);

    public IReadOnlyCollection<string> GetRegisteredIds() => _byId.Keys;
}
