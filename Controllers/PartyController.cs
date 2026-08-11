using Godot;
using System.Collections.Generic;

public partial class PartyController : Node
{
	[Signal]
	public delegate void PartySpawnedEventHandler(
		int heroCount);

	public const int MaximumPartySize = 5;


	[ExportCategory("Temporary Equipped Party")]

	/// <summary>
	/// Stable content identifier for equipped heros; other systems use this value to find the same game data.
	/// For example, changing this ID makes the owning resource resolve a different registered equipped heros.
	/// </summary>
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

	/// <summary>
	/// Controls party slot anchors.
	/// For example, adding another entry gives the owning system one more configured party slot anchors to use.
	/// </summary>
	[Export]
	public Godot.Collections.Array<Marker2D>
		PartySlotAnchors
	{
		get;
		set;
	} = new();


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


	private readonly List<HeroActorController>
		_spawnedHeroes = new();

	public IReadOnlyList<HeroActorController>
		SpawnedHeroes => _spawnedHeroes;

	public int SpawnedHeroCount =>
		_spawnedHeroes.Count;


	/// <summary>
	/// Runs Godot setup for Party Controller when the node enters the scene tree.
	/// Uses the current node and service state; any result is applied through side effects, events, or stored fields.
	/// </summary>
	public override void _Ready()
	{
		if (!ValidateConfiguration())
			return;

		Callable.From(SpawnEquippedParty)
			.CallDeferred();
	}

	/// <summary>
	/// Performs the spawn equipped party operation for Party Controller.
	/// Uses the current node and service state; any result is applied through side effects, events, or stored fields.
	/// </summary>
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

	/// <summary>
	/// Performs the validate configuration operation for Party Controller.
	/// Reads the current state and returns the resulting bool to the caller.
	/// </summary>
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

	/// <summary>
	/// Performs the require operation for Party Controller.
	/// Uses the supplied arguments and current state and returns the resulting bool to the caller.
	/// </summary>
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
