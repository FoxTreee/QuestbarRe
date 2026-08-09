using Godot;

public partial class HeroFactory : Node
{
    [ExportCategory("Dependencies")]

    [Export]
    public HeroContentRegistry Registry
    { get; set; } = null!;

    public override void _Ready()
    {
        if (!GodotObject.IsInstanceValid(Registry))
        {
            GD.PushError(
                "HeroFactory is missing its " +
                "Registry Inspector reference.");
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

        hero.Configure(definition);

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
