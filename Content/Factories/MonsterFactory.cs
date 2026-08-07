using Godot;

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

        monster.Configure(definition);

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