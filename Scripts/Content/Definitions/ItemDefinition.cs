using Godot;
using System.Collections.Generic;

[GlobalClass]
public partial class ItemDefinition : Resource
{
    [ExportCategory("Identity")]
    [Export(PropertyHint.PlaceholderText, "item.core.example")]
    public string ContentId { get; set; } = string.Empty;
    [Export] public string DisplayName { get; set; } = "Unnamed Item";
    [Export(PropertyHint.MultilineText)] public string Description { get; set; } = string.Empty;
    [Export] public bool IsUnique { get; set; }

    [ExportCategory("Inventory")]
    [Export(PropertyHint.Range, "1,1000000,1")]
    public int MaximumStackSize { get; set; } = 1;

    [ExportCategory("Presentation")]
    [Export] public Texture2D? IconTexture { get; set; }

    public bool IsStackable => MaximumStackSize > 1;

    public virtual IReadOnlyList<string> GetValidationErrors()
    {
        List<string> errors = new();
        if (!global::ContentId.IsValid(ContentId))
            errors.Add($"Invalid item Content ID '{ContentId}'.");
        if (string.IsNullOrWhiteSpace(DisplayName))
            errors.Add($"{ContentId}: DisplayName is required.");
        if (MaximumStackSize < 1)
            errors.Add($"{ContentId}: MaximumStackSize must be at least 1.");
        if (IsUnique && MaximumStackSize != 1)
            errors.Add($"{ContentId}: Unique items must have MaximumStackSize 1.");
        return errors;
    }
}
