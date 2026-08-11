using Godot;

public partial class HeroFactorySpawnProbe : Node
{
    [ExportCategory("Content")]

    /// <summary>
    /// Stable content identifier for hero; other systems use this value to find the same game data.
    /// For example, changing this ID makes the owning resource resolve a different registered hero.
    /// </summary>
    [Export]
    public string HeroContentId { get; set; } =
        "hero.core.starting_hero";


    [ExportCategory("Dependencies")]

    /// <summary>
    /// Controls factory.
    /// For example, selecting a different value changes which factory behavior or content the owning system uses.
    /// </summary>
    [Export]
    public HeroFactory Factory { get; set; } = null!;

    /// <summary>
    /// Inspector reference used by this component for its actor layer dependency.
    /// Assign the matching node or resource from the scene; leaving it empty prevents that connection from working.
    /// </summary>
    [Export]
    public Node2D ActorLayer { get; set; } = null!;

    /// <summary>
    /// Inspector reference used by this component for its formation anchor dependency.
    /// Assign the matching node or resource from the scene; leaving it empty prevents that connection from working.
    /// </summary>
    [Export]
    public Node2D FormationAnchor { get; set; } = null!;

    /// <summary>
    /// Inspector reference used by this component for its journey state dependency.
    /// Assign the matching node or resource from the scene; leaving it empty prevents that connection from working.
    /// </summary>
    [Export]
    public JourneyStateService JourneyState { get; set; } = null!;

    /// <summary>
    /// Inspector reference used by this component for its targeting dependency.
    /// Assign the matching node or resource from the scene; leaving it empty prevents that connection from working.
    /// </summary>
    [Export]
    public TargetingService Targeting { get; set; } = null!;


    /// <summary>
    /// Runs Godot setup for Hero Factory Spawn Probe when the node enters the scene tree.
    /// Uses the current node and service state; any result is applied through side effects, events, or stored fields.
    /// </summary>
    public override void _Ready()
    {
        if (!ValidateReferences())
            return;

        Callable.From(SpawnProbeHero)
            .CallDeferred();
    }

    /// <summary>
    /// Performs the spawn probe hero operation for Hero Factory Spawn Probe.
    /// Uses the current node and service state; any result is applied through side effects, events, or stored fields.
    /// </summary>
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

    /// <summary>
    /// Performs the validate references operation for Hero Factory Spawn Probe.
    /// Reads the current state and returns the resulting bool to the caller.
    /// </summary>
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

    /// <summary>
    /// Performs the require operation for Hero Factory Spawn Probe.
    /// Uses the supplied arguments and current state and returns the resulting bool to the caller.
    /// </summary>
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
