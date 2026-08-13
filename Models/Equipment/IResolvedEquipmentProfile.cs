using System.Collections.Generic;

public interface IResolvedEquipmentProfile : IEquipmentStatSource
{
    string DefinitionContentId { get; }
    string DisplayName { get; }
    int RequiredLevel { get; }

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
