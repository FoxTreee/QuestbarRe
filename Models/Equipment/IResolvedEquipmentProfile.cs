using Godot;
using System.Collections.Generic;

public interface IResolvedEquipmentProfile : IEquipmentStatSource
{
    string DefinitionContentId { get; }
    string DisplayName { get; }
    int RequiredLevel { get; }

    /// <summary>
    /// Local definition icon snapshot currently used by character/inventory UI.
    /// A later server-authoritative item payload can override this while still
    /// exposing the same runtime property shape to presentation code.
    /// </summary>
    Texture2D? IconTexture { get; }

    /// <summary>
    /// Runtime snapshots of authored percentage-based equipment effects.
    /// They are preserved here but intentionally not combined or applied yet,
    /// because modifier stacking/gameplay rules have not been designed.
    /// </summary>
    IReadOnlyList<ResolvedEquipmentPercentageModifier>
        PercentageModifiers
    { get; }

    /// <summary>
    /// Returns whether this resolved item is eligible to occupy the requested
    /// equipment slot. Character, class, level, and talent permission remain
    /// separate validation layers.
    /// </summary>
    bool CanEquipInSlot(EquipmentSlot slot);
}
