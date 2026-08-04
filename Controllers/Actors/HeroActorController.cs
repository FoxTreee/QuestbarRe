using Godot;
using System.Collections.Generic;

public partial class HeroActorController : Node2D
{
	private Vector2 _visualRestPosition;
	private double _animationTime;
	private bool _isTraveling;
    private bool _isEncounterActive;

    [ExportCategory("Formation")]
	[Export]
	public Node2D FormationAnchor { get; set; } = null!;

	[Export]
	public Vector2 FormationOffset { get; set; } = Vector2.Zero;
	
	[ExportCategory("Dependencies")]
	[Export]
	public JourneyStateService JourneyState { get; set; } = null!;

	[ExportCategory("Visuals")]
	[Export]
	public Node2D VisualRoot { get; set; } = null!;

	[ExportCategory("Travel Animation")]
	[Export(PropertyHint.Range, "0,20,0.5")]
	public float BobHeight { get; set; } = 4.0f;

	[Export(PropertyHint.Range, "0,20,0.1")]
	public float BobSpeed { get; set; } = 7.0f;

	[Export(PropertyHint.Range, "0,6.28,0.01")]
	public float BobPhaseOffset { get; set; } = 0.0f;

    [ExportCategory("Temporary Combat Movement")]
    [Export(PropertyHint.Range, "0,500,1")]
    public float CombatMoveSpeed { get; set; } = 140.0f;

    [Export(PropertyHint.Range, "0,400,1")]
    public float TemporaryAttackRange { get; set; } = 28.0f;

    [Export(PropertyHint.Range, "0.1,20,0.1")]
    public float CombatArrivalDistance { get; set; } = 1.0f;

    [Export]
    public TargetingService Targeting { get; set; } = null!;

    public MonsterActorController? CurrentTarget { get; private set; }

    public Vector2 FormationPosition => FormationAnchor.GlobalPosition + FormationOffset;

	public override void _Ready()
{
	if (!ValidateReferences())
	{
		SetProcess(false);
		return;
	}

	_visualRestPosition = VisualRoot.Position;

	JourneyState.StateChanged += OnJourneyStateChanged;

	ApplyJourneyState(JourneyState.CurrentState);
	SnapToFormation();

	GD.Print(
		$"HeroActor initialized at formation position " +
		$"{FormationPosition}.");
}

	public override void _ExitTree()
	{
		if (GodotObject.IsInstanceValid(JourneyState))
		{
			JourneyState.StateChanged -=
				OnJourneyStateChanged;
		}
	}

    public override void _Process(double delta)
    {
        if (_isTraveling)
        {
            UpdateTravelAnimation(delta);
            return;
        }

        if (_isEncounterActive)
            UpdateCombatApproach(delta);
    }

    private void UpdateTravelAnimation(double delta)
    {
        _animationTime += delta;

        float bobOffset =
            Mathf.Abs(
                Mathf.Sin(
                    (float)(_animationTime * BobSpeed)
                    + BobPhaseOffset))
            * BobHeight;

        VisualRoot.Position =
            _visualRestPosition
            + Vector2.Up * bobOffset;
    }

    private void UpdateCombatApproach(double delta)
    {
        if (!Targeting.IsValidMonsterTarget(CurrentTarget))
            return;

        Vector2 targetPosition =
            CurrentTarget!.GlobalPosition;

        Vector2 attackPosition =
            new(
                targetPosition.X + TemporaryAttackRange,
                targetPosition.Y);

        float movementDistance =
            CombatMoveSpeed * (float)delta;

        GlobalPosition = GlobalPosition.MoveToward(
            attackPosition,
            movementDistance);

        if (GlobalPosition.DistanceTo(attackPosition)
            <= CombatArrivalDistance)
        {
            GlobalPosition = attackPosition;
        }
    }

    private void OnJourneyStateChanged(
		JourneyStateService.JourneyState previousState,
		JourneyStateService.JourneyState currentState)
	{
		ApplyJourneyState(currentState);
	}

    private void ApplyJourneyState(JourneyStateService.JourneyState state)
    {
        _isTraveling = state == JourneyStateService.JourneyState.Traveling;

        _isEncounterActive = state == JourneyStateService.JourneyState.Encounter;

        _animationTime = 0.0;
        VisualRoot.Position = _visualRestPosition;

        if (_isTraveling)
        {
            // Temporary until animated return-to-formation is added.
            SnapToFormation();
        }
    }

    private bool ValidateReferences()
	{
		bool valid = true;

		valid &= Require(FormationAnchor, nameof(FormationAnchor));
		valid &= Require(JourneyState, nameof(JourneyState));
		valid &= Require(VisualRoot, nameof(VisualRoot));
        valid &= Require(Targeting, nameof(Targeting));

        return valid;
	}
	
	private static bool Require(GodotObject value, string propertyName)
{
	if (GodotObject.IsInstanceValid(value))
		return true;

	GD.PushError(
		$"HeroActorController is missing the " +
		$"Inspector reference '{propertyName}'.");

	return false;
}
	
	public void SnapToFormation()
	{
		GlobalPosition = FormationPosition;
	}

    public void RefreshTarget(IReadOnlyList<MonsterActorController> candidates)
    {
        if (CurrentTarget is not null
            && Targeting.IsValidMonsterTarget(CurrentTarget)
            && ContainsTarget(candidates, CurrentTarget))
        {
            return;
        }

        MonsterActorController? previousTarget =
            CurrentTarget;

        CurrentTarget =
            Targeting.SelectPriorityMonster(candidates);

        if (CurrentTarget == previousTarget)
            return;

        if (CurrentTarget is null)
        {
            GD.Print(
                $"{Name} has no valid monster target.");

            return;
        }

        GD.Print(
            $"{Name} targeted {CurrentTarget.Name} " +
            $"at X={CurrentTarget.GlobalPosition.X}.");
    }

    private static bool ContainsTarget(IReadOnlyList<MonsterActorController> candidates, MonsterActorController target)
    {
        foreach (MonsterActorController candidate in candidates)
        {
            if (candidate == target)
                return true;
        }

        return false;
    }

    public void ClearTarget()
    {
        if (CurrentTarget is null)
            return;

        CurrentTarget = null;

        GD.Print($"{Name} cleared its monster target.");
    }
}
