using Godot;
using System.Collections.Generic;

public partial class MonsterFactory : Node
{
    [ExportCategory("Dependencies")]
    [Export]
    public MonsterContentRegistry Registry
    {
        get;
        set;
    } = null!;

    public override void _Ready()
    {
        if (!GodotObject.IsInstanceValid(Registry))
        {
            GD.PushError(
                "MonsterFactory is missing its " +
                "Registry Inspector reference.");
        }
    }

    public bool TryCreate(
        string contentId,
        out MonsterActorController monster,
        out string error)
    {
        monster = null!;
        error = string.Empty;

        if (!GodotObject.IsInstanceValid(Registry))
        {
            error =
                "MonsterFactory has no valid registry.";

            return false;
        }

        if (!Registry.TryGet(
            contentId,
            out MonsterDefinition definition))
        {
            error =
                $"Unknown monster Content ID " +
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

        monster =
            definition.ActorScene.Instantiate
                <MonsterActorController>();

        List<AbilityDefinition> abilities = new();

        foreach (string abilityContentId
            in definition.AbilityContentIds)
        {
            if (Registry.AbilityRegistry.TryGet(
                abilityContentId,
                out AbilityDefinition ability))
            {
                abilities.Add(ability);
            }
        }

        monster.Configure(
            definition,
            abilities);

        return true;
    }

    public MonsterActorController CreateRequired(
        string contentId)
    {
        if (TryCreate(
            contentId,
            out MonsterActorController monster,
            out string error))
        {
            return monster;
        }

        throw new System.InvalidOperationException(
            error);
    }
}