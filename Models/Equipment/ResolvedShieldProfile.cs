using Godot;
using System.Collections.Generic;
using System.Linq;

public sealed class ResolvedShieldProfile :
    IResolvedEquipmentProfile,
    IArmorProvidingEquipment
{
    public string DefinitionContentId { get; }
    public string DisplayName { get; }
    public int RequiredLevel { get; }
    public Texture2D? IconTexture { get; }

    public int ArmorValue { get; }

    public int Strength { get; }
    public int Agility { get; }
    public int Stamina { get; }
    public int Intellect { get; }
    public int Spirit { get; }

    public IReadOnlyList<ResolvedEquipmentPercentageModifier>
        PercentageModifiers
    { get; }


    public ResolvedShieldProfile(
        string definitionContentId,
        string displayName,
        int requiredLevel,
        Texture2D? iconTexture,
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
        IconTexture = iconTexture;
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
        return slot == EquipmentSlot.OffHand;
    }


    public static ResolvedShieldProfile FromDefinition(
        ShieldDefinition definition)
    {
        return new ResolvedShieldProfile(
            definition.ContentId,
            definition.DisplayName,
            definition.RequiredLevel,
            definition.IconTexture,
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
