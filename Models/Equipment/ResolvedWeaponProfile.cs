using System.Collections.Generic;

public sealed class ResolvedWeaponProfile
{
    public string DefinitionContentId { get; }
    public string DisplayName { get; }

    public WeaponAttackStyle AttackStyle { get; }
    public WeaponType WeaponType { get; }
    public WeaponHandedness Handedness { get; }
    public WeaponEquipPosition EquipPosition { get; }

    public float MinimumDamage { get; }
    public float MaximumDamage { get; }
    public float AttackSpeedSeconds { get; }

    public int Strength { get; }
    public int Agility { get; }
    public int Stamina { get; }
    public int Intellect { get; }
    public int Spirit { get; }

    /// <summary>
    /// Runtime snapshot of the weapon values combat and presentation may
    /// eventually consume. Today this is built from local WeaponDefinition
    /// content. A future authoritative item source can populate the same runtime
    /// shape without requiring combat to trust local item ownership.
    /// </summary>
    public ResolvedWeaponProfile(
        string definitionContentId,
        string displayName,
        WeaponAttackStyle attackStyle,
        WeaponType weaponType,
        WeaponHandedness handedness,
        WeaponEquipPosition equipPosition,
        float minimumDamage,
        float maximumDamage,
        float attackSpeedSeconds,
        int strength,
        int agility,
        int stamina,
        int intellect,
        int spirit)
    {
        DefinitionContentId = definitionContentId;
        DisplayName = displayName;
        AttackStyle = attackStyle;
        WeaponType = weaponType;
        Handedness = handedness;
        EquipPosition = equipPosition;
        MinimumDamage = minimumDamage;
        MaximumDamage = maximumDamage;
        AttackSpeedSeconds = attackSpeedSeconds;
        Strength = strength;
        Agility = agility;
        Stamina = stamina;
        Intellect = intellect;
        Spirit = spirit;
    }

    public static ResolvedWeaponProfile FromDefinition(
        WeaponDefinition definition)
    {
        return new ResolvedWeaponProfile(
            definition.ContentId,
            definition.DisplayName,
            definition.AttackStyle,
            definition.WeaponType,
            definition.Handedness,
            definition.EquipPosition,
            definition.MinimumDamage,
            definition.MaximumDamage,
            definition.AttackSpeedSeconds,
            definition.Strength,
            definition.Agility,
            definition.Stamina,
            definition.Intellect,
            definition.Spirit);
    }
}
