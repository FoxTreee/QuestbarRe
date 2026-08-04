using System.Collections.Generic;
using Godot;

public partial class EncounterController : Node
{
    [Signal]
    public delegate void ActiveMonsterCountChangedEventHandler(
    int activeMonsterCount);
    
	[ExportCategory("Dependencies")]
	[Export]
	public JourneyStateService JourneyState { get; set; } = null!;

	[Export]
	public Node2D ActorLayer { get; set; } = null!;

	[Export]
	public Node2D MonsterSpawnAnchor { get; set; } = null!;

    [Export]
    public Node2D MonsterEntryAnchor { get; set; } = null!;

    [ExportCategory("Encounter Content")]
	[Export]
	public PackedScene MonsterScene { get; set; } = null!;

    private readonly List<MonsterActorController> _activeMonsters = new();

    public IReadOnlyList<MonsterActorController> ActiveMonsters =>
        _activeMonsters;

    public int ActiveMonsterCount =>
        _activeMonsters.Count;

    public override void _Ready()
	{
		if (!ValidateReferences())
			return;

		JourneyState.StateChanged += OnJourneyStateChanged;

		ApplyJourneyState(JourneyState.CurrentState);
	}

	public override void _ExitTree()
	{
		if (GodotObject.IsInstanceValid(JourneyState))
		{
			JourneyState.StateChanged -=
				OnJourneyStateChanged;
		}
	}

	private void OnJourneyStateChanged(
		JourneyStateService.JourneyState previousState,
		JourneyStateService.JourneyState currentState)
	{
		ApplyJourneyState(currentState);
	}

	private void ApplyJourneyState(
		JourneyStateService.JourneyState state)
	{
		if (state
			== JourneyStateService.JourneyState.Encounter)
		{
			BeginEncounterPresentation();
			return;
		}

		EndEncounterPresentation();
	}

    private void BeginEncounterPresentation()
    {
        RemoveInvalidMonsterReferences();

        if (_activeMonsters.Count > 0)
            return;

        SpawnTestMonster();
    }

    private void SpawnTestMonster()
    {
        MonsterActorController monster =
            MonsterScene.Instantiate<MonsterActorController>();

        ActorLayer.AddChild(monster);

        monster.InitializeEntrance(
            MonsterSpawnAnchor.GlobalPosition,
            MonsterEntryAnchor.GlobalPosition);

        _activeMonsters.Add(monster);
        EmitActiveMonsterCountChanged();

        GD.Print(
            $"Monster added to encounter. " +
            $"Active monsters={_activeMonsters.Count}");
    }

    private void EndEncounterPresentation()
    {
        RemoveInvalidMonsterReferences();

        foreach (MonsterActorController monster in _activeMonsters)
        {
            if (GodotObject.IsInstanceValid(monster))
                monster.QueueFree();
        }

        _activeMonsters.Clear();
        EmitActiveMonsterCountChanged();

        GD.Print(
            "Encounter monsters removed. Active monsters=0");
    }

    private void RemoveInvalidMonsterReferences()
    {
        _activeMonsters.RemoveAll(
            monster =>
                !GodotObject.IsInstanceValid(monster));
    }

    private bool ValidateReferences()
	{
		bool valid = true;

		valid &= Require(
			JourneyState,
			nameof(JourneyState));

		valid &= Require(
			ActorLayer,
			nameof(ActorLayer));

		valid &= Require(
			MonsterSpawnAnchor,
			nameof(MonsterSpawnAnchor));

        valid &= Require(
			MonsterEntryAnchor,
			nameof(MonsterEntryAnchor));

        valid &= Require(
			MonsterScene,
			nameof(MonsterScene));

		return valid;
	}

    private void EmitActiveMonsterCountChanged()
    {
        EmitSignal(
            SignalName.ActiveMonsterCountChanged,
            _activeMonsters.Count);
    }

    private static bool Require(
		GodotObject value,
		string propertyName)
	{
		if (GodotObject.IsInstanceValid(value))
			return true;

		GD.PushError(
			$"EncounterController is missing the " +
			$"Inspector reference '{propertyName}'.");

		return false;
	}
}
