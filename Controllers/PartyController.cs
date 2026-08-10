using Godot;
using System.Collections.Generic;

public partial class PartyController : Node
{
    [Signal]
    public delegate void PartySpawnedEventHandler(
        int heroCount);

    public const int MaximumPartySize = 5;


    [ExportCategory("Temporary Equipped Party")]

    [Export]
    public Godot.Collections.Array<string>
        EquippedHeroContentIds
    {
        get;
        set;
    } = new()
    {
        string.Empty,
        string.Empty,
        string.Empty,
        string.Empty,
        string.Empty
    };


    [ExportCategory("Party Formation")]

    [Export]
    public Godot.Collections.Array<Marker2D>
        PartySlotAnchors
    {
        get;
        set;
    } = new();


    [ExportCategory("Dependencies")]

    [Export]
    public HeroFactory Factory { get; set; } = null!;

    [Export]
    public Node2D ActorLayer { get; set; } = null!;

    [Export]
    public JourneyStateService JourneyState { get; set; } = null!;

    [Export]
    public TargetingService Targeting { get; set; } = null!;


    private readonly List<HeroActorController>
        _spawnedHeroes = new();

    public IReadOnlyList<HeroActorController>
        SpawnedHeroes => _spawnedHeroes;

    public int SpawnedHeroCount =>
        _spawnedHeroes.Count;


    public override void _Ready()
    {
        if (!ValidateConfiguration())
            return;

        Callable.From(SpawnEquippedParty)
            .CallDeferred();
    }

    private void SpawnEquippedParty()
    {
        for (int slotIndex = 0;
            slotIndex < MaximumPartySize;
            slotIndex++)
        {
            string contentId =
                EquippedHeroContentIds[slotIndex]
                    ?.Trim()
                ?? string.Empty;

            if (string.IsNullOrEmpty(contentId))
                continue;

            int slotNumber = slotIndex + 1;

            if (!Factory.TryCreate(
                contentId,
                out HeroActorController hero,
                out string error))
            {
                GD.PushError(
                    $"Party slot {slotNumber} could not " +
                    $"spawn '{contentId}': {error}");

                continue;
            }

            Marker2D formationAnchor =
                PartySlotAnchors[slotIndex];

            hero.Name =
                $"PartySlot{slotNumber}Hero";

            hero.FormationAnchor = formationAnchor;
            hero.FormationOffset = Vector2.Zero;
            hero.JourneyState = JourneyState;
            hero.Targeting = Targeting;

            ActorLayer.AddChild(hero);
            _spawnedHeroes.Add(hero);

            DebugLog.Print(
                $"Party slot {slotNumber} spawned " +
                $"'{contentId}' at " +
                $"{formationAnchor.GlobalPosition}.");
        }

		foreach (HeroActorController hero in _spawnedHeroes)
		{
			hero.SetPartyMembers(_spawnedHeroes);
		}

        EmitSignal(
            SignalName.PartySpawned,
            SpawnedHeroCount);

        DebugLog.Print(
            $"PartyController initialized with " +
            $"{SpawnedHeroCount} equipped hero(es).");
    }

    private bool ValidateConfiguration()
    {
        bool valid = true;

        valid &= Require(Factory, nameof(Factory));
        valid &= Require(ActorLayer, nameof(ActorLayer));
        valid &= Require(JourneyState, nameof(JourneyState));
        valid &= Require(Targeting, nameof(Targeting));

        if (EquippedHeroContentIds.Count
            != MaximumPartySize)
        {
            GD.PushError(
                $"PartyController requires exactly " +
                $"{MaximumPartySize} equipped-hero ID " +
                $"entries, including empty slots.");

            valid = false;
        }

        if (PartySlotAnchors.Count
            != MaximumPartySize)
        {
            GD.PushError(
                $"PartyController requires exactly " +
                $"{MaximumPartySize} party-slot anchors.");

            valid = false;
        }
        else
        {
            HashSet<ulong> anchorIds = new();

            for (int slotIndex = 0;
                slotIndex < PartySlotAnchors.Count;
                slotIndex++)
            {
                Marker2D anchor =
                    PartySlotAnchors[slotIndex];

                if (!GodotObject.IsInstanceValid(anchor))
                {
                    GD.PushError(
                        $"PartyController is missing the " +
                        $"PartySlot{slotIndex + 1} anchor.");

                    valid = false;
                    continue;
                }

                if (!anchorIds.Add(anchor.GetInstanceId()))
                {
                    GD.PushError(
                        $"PartyController assigns the same " +
                        $"anchor to more than one party slot.");

                    valid = false;
                }
            }
        }

        return valid;
    }

    private static bool Require(
        GodotObject value,
        string propertyName)
    {
        if (GodotObject.IsInstanceValid(value))
            return true;

        GD.PushError(
            $"PartyController is missing the " +
            $"Inspector reference '{propertyName}'.");

        return false;
    }
}
