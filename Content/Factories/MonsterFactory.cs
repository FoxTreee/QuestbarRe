using Godot;
using System.Collections.Generic;

public partial class MonsterFactory : Node
{
    [ExportCategory("Dependencies")]
    /// <summary>
    /// Inspector reference used by this component for its registry dependency.
    /// Assign the matching node or resource from the scene; leaving it empty prevents that connection from working.
    /// </summary>
    [Export]
    public MonsterContentRegistry Registry
    {
        get;
        set;
    } = null!;

    /// <summary>
    /// Runs Godot setup for Monster Factory when the node enters the scene tree.
    /// Uses the current node and service state; any result is applied through side effects, events, or stored fields.
    /// </summary>
    public override void _Ready()
    {
        if (!GodotObject.IsInstanceValid(Registry))
        {
            GD.PushError(
                "MonsterFactory is missing its " +
                "Registry Inspector reference.");
        }
    }

    /// <summary>
    /// Attempts to create without throwing when the operation cannot be completed.
    /// Uses the supplied arguments and current state and returns the resulting bool to the caller.
    /// </summary>
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

    /// <summary>
    /// Creates required from the supplied configuration and current dependencies.
    /// Uses the supplied arguments and current state and returns the resulting monster actor controller to the caller.
    /// </summary>
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