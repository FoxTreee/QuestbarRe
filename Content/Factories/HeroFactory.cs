using Godot;
using System.Collections.Generic;

public partial class HeroFactory : Node
{
    [ExportCategory("Dependencies")]

    [Export]
    public HeroContentRegistry Registry
    { get; set; } = null!;

    [Export]
    public WeaponContentRegistry WeaponRegistry
    { get; set; } = null!;

    public override void _Ready()
    {
        if (!GodotObject.IsInstanceValid(Registry))
        {
            GD.PushError(
                "HeroFactory is missing its " +
                "Registry Inspector reference.");
        }

        if (!GodotObject.IsInstanceValid(WeaponRegistry))
        {
            GD.PushError(
                "HeroFactory is missing its " +
                "WeaponRegistry Inspector reference.");
        }
    }

    public bool TryCreate(
        string contentId,
        out HeroActorController hero,
        out string error)
    {
        hero = null!;
        error = string.Empty;

        if (!GodotObject.IsInstanceValid(Registry))
        {
            error =
                "HeroFactory has no valid registry.";

            return false;
        }

        if (!GodotObject.IsInstanceValid(WeaponRegistry))
        {
            error =
                "HeroFactory has no valid weapon registry.";

            return false;
        }

        if (!Registry.TryGet(
            contentId,
            out HeroDefinition definition))
        {
            error =
                $"Unknown hero Content ID " +
                $"'{contentId}'.";

            return false;
        }

        if (!GodotObject.IsInstanceValid(
            definition.ActorScene))
        {
            error =
                $"{definition.ContentId} has no valid " +
                "ActorScene.";

            return false;
        }

        hero =
            definition.ActorScene.Instantiate
                <HeroActorController>();

        List<AbilityDefinition> equippedAbilities = new();

        foreach (string abilityContentId
            in definition.GetStartingEquippedAbilityIds())
        {
            if (!Registry.AbilityRegistry.TryGet(
                abilityContentId,
                out AbilityDefinition ability))
            {
                error =
                    $"{definition.ContentId}: equipped ability " +
                    $"'{abilityContentId}' is not registered.";

                hero.QueueFree();
                hero = null!;
                return false;
            }

            equippedAbilities.Add(ability);
        }

        if (!TryResolveStartingWeapon(
            definition.StartingMainHandWeaponContentId,
            EquipmentSlot.MainHand,
            out WeaponDefinition? mainHand,
            out error)
            || !TryResolveStartingWeapon(
                definition.StartingOffHandWeaponContentId,
                EquipmentSlot.OffHand,
                out WeaponDefinition? offHand,
                out error)
            || !TryResolveStartingWeapon(
                definition.StartingRangedWeaponContentId,
                EquipmentSlot.Ranged,
                out WeaponDefinition? ranged,
                out error))
        {
            hero.QueueFree();
            hero = null!;
            return false;
        }

        hero.Configure(
            definition,
            equippedAbilities);

        if (!hero.TryConfigureStartingEquipment(
            mainHand,
            offHand,
            ranged,
            out error))
        {
            error =
                $"{definition.ContentId}: {error}";

            hero.QueueFree();
            hero = null!;
            return false;
        }

        return true;
    }

    private bool TryResolveStartingWeapon(
        string weaponContentId,
        EquipmentSlot slot,
        out WeaponDefinition? weapon,
        out string error)
    {
        weapon = null;
        error = string.Empty;

        if (string.IsNullOrWhiteSpace(weaponContentId))
            return true;

        string normalizedId =
            weaponContentId.Trim();

        if (!WeaponRegistry.TryGet(
            normalizedId,
            out WeaponDefinition definition))
        {
            error =
                $"Starting weapon '{normalizedId}' is not registered.";

            return false;
        }

        if (!definition.CanEquipInSlot(slot))
        {
            error =
                $"Starting weapon '{normalizedId}' is not eligible " +
                $"for {slot}. EquipPosition={definition.EquipPosition}.";

            return false;
        }

        weapon = definition;
        return true;
    }


    public HeroActorController CreateRequired(
        string contentId)
    {
        if (TryCreate(
            contentId,
            out HeroActorController hero,
            out string error))
        {
            return hero;
        }

        throw new System.InvalidOperationException(
            error);
    }
}
