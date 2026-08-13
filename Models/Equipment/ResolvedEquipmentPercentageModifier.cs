public sealed class ResolvedEquipmentPercentageModifier
{
    public string ModifierContentId { get; }
    public float PercentValue { get; }

    public ResolvedEquipmentPercentageModifier(
        string modifierContentId,
        float percentValue)
    {
        ModifierContentId = modifierContentId;
        PercentValue = percentValue;
    }


    public static ResolvedEquipmentPercentageModifier FromDefinition(
        EquipmentPercentageModifierDefinition definition)
    {
        return new ResolvedEquipmentPercentageModifier(
            definition.ModifierContentId,
            definition.PercentValue);
    }
}
