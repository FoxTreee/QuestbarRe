using Godot;

public partial class HeroFactorySpawnProbe : Node
{
    [ExportCategory("Content")]

    [Export]
    public string HeroContentId { get; set; } =
        "hero.core.starting_hero";


    [ExportCategory("Dependencies")]

    [Export]
    public HeroFactory Factory { get; set; } = null!;

    [Export]
    public Node2D ActorLayer { get; set; } = null!;

    [Export]
    public Node2D FormationAnchor { get; set; } = null!;

    [Export]
    public JourneyStateService JourneyState { get; set; } = null!;

    [Export]
    public TargetingService Targeting { get; set; } = null!;


    public override void _Ready()
    {
        if (!ValidateReferences())
            return;

        Callable.From(SpawnProbeHero)
            .CallDeferred();
    }

    private void SpawnProbeHero()
    {
        if (!Factory.TryCreate(
            HeroContentId,
            out HeroActorController hero,
            out string error))
        {
            GD.PushError(
                $"Hero factory spawn probe failed: {error}");

            return;
        }

        hero.Name = "FactorySpawnProbeHero";
        hero.FormationAnchor = FormationAnchor;
        hero.JourneyState = JourneyState;
        hero.Targeting = Targeting;

        ActorLayer.AddChild(hero);

        DebugLog.Print(
            $"Hero factory spawn probe passed. " +
            $"ContentId={hero.Definition!.ContentId}, " +
            $"Name={hero.Name}, " +
            $"Health={hero.CombatProfile.MaximumHealth}, " +
            $"Damage={hero.CombatProfile.AttackDamage}.");
    }

    private bool ValidateReferences()
    {
        bool valid = true;

        valid &= Require(Factory, nameof(Factory));
        valid &= Require(ActorLayer, nameof(ActorLayer));
        valid &= Require(FormationAnchor, nameof(FormationAnchor));
        valid &= Require(JourneyState, nameof(JourneyState));
        valid &= Require(Targeting, nameof(Targeting));

        return valid;
    }

    private static bool Require(
        GodotObject value,
        string propertyName)
    {
        if (GodotObject.IsInstanceValid(value))
            return true;

        GD.PushError(
            $"HeroFactorySpawnProbe is missing the " +
            $"Inspector reference '{propertyName}'.");

        return false;
    }
}
