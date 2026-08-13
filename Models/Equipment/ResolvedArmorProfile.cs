using System.Collections.Generic;
using System.Linq;

public sealed class ResolvedArmorProfile :
    IResolvedEquipmentProfile,
    IArmorProvidingEquipment
{
    public string DefinitionContentId { get; }
    public string DisplayName { get; }
    public int RequiredLevel { get; }

    public ArmorEquipPosition EquipPosition { get; }
    public int ArmorValue { get; }

    public int Strength { get; }
    public int Agility { get; }
    public int Stamina { get; }
    public int Intellect { get; }
    public int Spirit { get; }

    public IReadOnlyList<ResolvedEquipmentPercentageModifier>
        PercentageModifiers
    { get; }


    public ResolvedArmorProfile(
        string definitionContentId,
        string displayName,
        int requiredLevel,
        ArmorEquipPosition equipPosition,
        int armorValue,
        int strength,
        int agility,
        int stamina,
        int intellect,
        int spirit,
        IReadOnlyList<ResolvedEquipmentPercentageModifier>
            percentageModifiers)
    {
        DefinitionContentId = definitionContentId;
        DisplayName = displayName;
        RequiredLevel = requiredLevel;
        EquipPosition = equipPosition;
        ArmorValue = armorValue;

        Strength = strength;
        Agility = agility;
        Stamina = stamina;
        Intellect = intellect;
        Spirit = spirit;

        PercentageModifiers = percentageModifiers;
    }


    public bool CanEquipInSlot(
        EquipmentSlot slot)
    {
        return EquipPosition switch
        {
            ArmorEquipPosition.Head =>
                slot == EquipmentSlot.Head,

            ArmorEquipPosition.Necklace =>
                slot == EquipmentSlot.Necklace,

            ArmorEquipPosition.Shoulders =>
                slot == EquipmentSlot.Shoulders,

            ArmorEquipPosition.Chest =>
                slot == EquipmentSlot.Chest,

            ArmorEquipPosition.Back =>
                slot == EquipmentSlot.Back,

            ArmorEquipPosition.GuildTabard =>
                slot == EquipmentSlot.GuildTabard,

            ArmorEquipPosition.Wrists =>
                slot == EquipmentSlot.Wrists,

            ArmorEquipPosition.Hands =>
                slot == EquipmentSlot.Hands,

            ArmorEquipPosition.Belt =>
                slot == EquipmentSlot.Belt,

            ArmorEquipPosition.Legs =>
                slot == EquipmentSlot.Legs,

            ArmorEquipPosition.Boots =>
                slot == EquipmentSlot.Boots,

            ArmorEquipPosition.Ring =>
                slot == EquipmentSlot.Ring1
                || slot == EquipmentSlot.Ring2,

            ArmorEquipPosition.Trinket =>
                slot == EquipmentSlot.Trinket1
                || slot == EquipmentSlot.Trinket2,

            _ => false
        };
    }


    public static ResolvedArmorProfile FromDefinition(
        ArmorDefinition definition)
    {
        return new ResolvedArmorProfile(
            definition.ContentId,
            definition.DisplayName,
            definition.RequiredLevel,
            definition.EquipPosition,
            definition.ArmorValue,
            definition.Strength,
            definition.Agility,
            definition.Stamina,
            definition.Intellect,
            definition.Spirit,
            definition.PercentageModifiers
                .Select(
                    ResolvedEquipmentPercentageModifier
                        .FromDefinition)
                .ToArray());
    }
}
