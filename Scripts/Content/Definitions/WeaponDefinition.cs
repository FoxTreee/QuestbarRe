using Godot;
using System;
using System.Collections.Generic;

[GlobalClass]
public partial class WeaponDefinition : EquipmentDefinition
{
    [ExportCategory("Weapon")]

    /// <summary>
    /// Whether this weapon performs melee or ranged normal attacks.
    /// </summary>
    [Export]
    public WeaponAttackStyle AttackStyle { get; set; } =
        WeaponAttackStyle.Melee;

    [Export]
    public WeaponType WeaponType { get; set; } =
        WeaponType.Sword;

    [Export]
    public WeaponHandedness Handedness { get; set; } =
        WeaponHandedness.OneHanded;

    /// <summary>
    /// Describes which hero weapon slot this item is eligible to occupy.
    /// Handedness is separate: a two-handed weapon can be MainHandOnly while
    /// still preventing an off-hand item from being equipped alongside it.
    /// </summary>
    [Export]
    public WeaponEquipPosition EquipPosition { get; set; } =
        WeaponEquipPosition.MainHandOnly;


    [ExportCategory("Weapon Damage")]

    /// <summary>
    /// Minimum raw damage rolled by a normal attack using this weapon.
    /// Character stats do not modify this value yet.
    /// </summary>
    [Export(PropertyHint.Range, "0,1000000,1")]
    public float MinimumDamage { get; set; } = 1.0f;

    /// <summary>
    /// Maximum raw damage rolled by a normal attack using this weapon.
    /// Character stats do not modify this value yet.
    /// </summary>
    [Export(PropertyHint.Range, "0,1000000,1")]
    public float MaximumDamage { get; set; } = 3.0f;

    /// <summary>
    /// Seconds between normal attacks when this weapon is the active weapon.
    /// This is authored as the tooltip-style weapon Speed value.
    /// </summary>
    [Export(PropertyHint.Range, "0.1,30,0.01")]
    public float AttackSpeedSeconds { get; set; } = 2.0f;


    public override IReadOnlyList<string> GetValidationErrors()
    {
        List<string> errors =
            new(base.GetValidationErrors());

        if (!ContentId.StartsWith(
            "weapon.",
            StringComparison.Ordinal))
        {
            errors.Add(
                $"{ContentId}: weapon Content ID must begin " +
                "with 'weapon.'.");
        }

        if (!Enum.IsDefined(AttackStyle))
        {
            errors.Add(
                $"{ContentId}: AttackStyle is invalid.");
        }

        if (!Enum.IsDefined(WeaponType))
        {
            errors.Add(
                $"{ContentId}: WeaponType is invalid.");
        }

        if (!Enum.IsDefined(Handedness))
        {
            errors.Add(
                $"{ContentId}: Handedness is invalid.");
        }

        if (!Enum.IsDefined(EquipPosition))
        {
            errors.Add(
                $"{ContentId}: EquipPosition is invalid.");
        }

        if (EquipPosition == WeaponEquipPosition.Ranged
            && AttackStyle != WeaponAttackStyle.Ranged)
        {
            errors.Add(
                $"{ContentId}: Ranged equip position requires " +
                "AttackStyle Ranged.");
        }

        if (EquipPosition != WeaponEquipPosition.Ranged
            && AttackStyle == WeaponAttackStyle.Ranged)
        {
            errors.Add(
                $"{ContentId}: Ranged attack-style weapons must " +
                "use the Ranged equip position.");
        }

        if (MinimumDamage < 0.0f)
        {
            errors.Add(
                $"{ContentId}: MinimumDamage cannot be negative.");
        }

        if (MaximumDamage < MinimumDamage)
        {
            errors.Add(
                $"{ContentId}: MaximumDamage cannot be less " +
                "than MinimumDamage.");
        }

        if (AttackSpeedSeconds <= 0.0f)
        {
            errors.Add(
                $"{ContentId}: AttackSpeedSeconds must be " +
                "greater than zero.");
        }

        return errors;
    }

    /// <summary>
    /// Returns whether this weapon definition is eligible for the requested
    /// hero equipment slot. Character/talent restrictions are intentionally
    /// separate from item eligibility.
    /// </summary>
    public bool CanEquipInSlot(HeroWeaponSlot slot)
    {
        return slot switch
        {
            HeroWeaponSlot.MainHand =>
                EquipPosition
                    == WeaponEquipPosition.MainHandOnly
                || EquipPosition
                    == WeaponEquipPosition.EitherHand,

            HeroWeaponSlot.OffHand =>
                EquipPosition
                    == WeaponEquipPosition.OffHandOnly
                || EquipPosition
                    == WeaponEquipPosition.EitherHand,

            HeroWeaponSlot.Ranged =>
                EquipPosition
                    == WeaponEquipPosition.Ranged,

            _ => false
        };
    }
}
