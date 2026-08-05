using Godot;
using System.Collections.Generic;

public partial class HeroActorController : Node2D
{
	[Signal]
	public delegate void AttackReleasedEventHandler(
	HeroActorController attacker,
	MonsterActorController target);

	private enum HeroState
	{
		InFormation,
		ApproachingTarget,
		WaitingToAttack,
		Attacking,
		ReturningToFormation,
		Dead
	}
	
	private Vector2 _visualRestPosition;
	private double _animationTime;
	private bool _movedThisFrame;
	private HeroState _state = HeroState.InFormation;
	private double _attackCooldownRemaining;
	private double _attackTimeRemaining;
	private bool _attackReleaseEmitted;


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

	[Export(PropertyHint.Range, "0.1,20,0.1")]
	public float AttackRangeTolerance { get; set; } = 3.0f;
	
	[Export(PropertyHint.Range, "0,10,0.1")]
	public float FacingDeadZone { get; set; } = 1.0f;

	[ExportCategory("Temporary Attack Cycle")]
	[Export(PropertyHint.Range, "0.1,10,0.1")]
	public float TemporaryAttackInterval { get; set; } = 1.5f;

	[Export(PropertyHint.Range, "0.05,2,0.05")]
	public float TemporaryAttackDuration { get; set; } = 0.3f;

	[Export(PropertyHint.Range, "0,30,0.5")]
	public float TemporaryAttackLungeDistance { get; set; } = 8.0f;

	[Export(PropertyHint.Range, "0,1,0.05")]
	public float TemporaryAttackReleasePoint { get; set; } = 0.5f;

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
	
	public FacingDirection Facing { get; private set; }
	= FacingDirection.Left;

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
		_movedThisFrame = false;
		UpdateFacingTowardTarget();

		switch (_state)
		{
			case HeroState.InFormation:

				if (JourneyState.CurrentState
					== JourneyStateService.JourneyState.Traveling)
				{
					UpdateMovementAnimation(delta);
				}

				break;

			case HeroState.ApproachingTarget:

				UpdateCombatApproach(delta);

				if (_movedThisFrame)
					UpdateMovementAnimation(delta);
				else
					StopMovementAnimation();

				break;

			case HeroState.WaitingToAttack:

				UpdateWaitingToAttack(delta);

				break;

			case HeroState.Attacking:

				UpdateAttack(delta);

				break;

			case HeroState.ReturningToFormation:

				UpdateReturnToFormation(delta);

				if (_movedThisFrame)
					UpdateMovementAnimation(delta);
				else
					StopMovementAnimation();

				break;

			case HeroState.Dead:

				StopMovementAnimation();

				break;
		}
	}

	private void UpdateMovementAnimation(double delta)
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

	private void StopMovementAnimation()
	{
		_animationTime = 0.0;
		VisualRoot.Position = _visualRestPosition;
	}

	private Vector2 CalculateApproachPosition(MonsterActorController target)
	{
		float preferredAttackX =
			target.GlobalPosition.X
			+ TemporaryAttackRange;

		float destinationX =
			Mathf.Min(
				GlobalPosition.X,
				preferredAttackX);

		return new Vector2(
			destinationX,
			target.GlobalPosition.Y);
	}

	private bool IsTargetWithinAttackRange(MonsterActorController target)
	{
		float horizontalDistance =
			GlobalPosition.X
			- target.GlobalPosition.X;

		return horizontalDistance
			<= TemporaryAttackRange
			+ AttackRangeTolerance;
	}
	
	private void UpdateFacingTowardTarget()
	{
		if (!Targeting.IsValidMonsterTarget(CurrentTarget))
			return;

		float horizontalDifference =
			CurrentTarget!.GlobalPosition.X
			- GlobalPosition.X;

		if (Mathf.Abs(horizontalDifference)
			<= FacingDeadZone)
		{
			return;
		}

		FacingDirection newFacing =
			horizontalDifference < 0.0f
				? FacingDirection.Left
				: FacingDirection.Right;

		if (Facing == newFacing)
			return;

		Facing = newFacing;

		GD.Print(
			$"{Name} now faces {Facing} toward " +
			$"{CurrentTarget.Name}.");
	}

	private void UpdateCombatApproach(double delta)
	{
		if (!Targeting.IsValidMonsterTarget(CurrentTarget))
			return;

		Vector2 previousPosition =
			GlobalPosition;

		Vector2 approachPosition =
			CalculateApproachPosition(CurrentTarget!);

		float movementDistance =
			CombatMoveSpeed * (float)delta;

		GlobalPosition = GlobalPosition.MoveToward(
			approachPosition,
			movementDistance);

		_movedThisFrame =
			!GlobalPosition.IsEqualApprox(previousPosition);

		if (!IsTargetWithinAttackRange(CurrentTarget!))
			return;

		bool isVerticallyAligned =
			Mathf.Abs(
				GlobalPosition.Y
				- CurrentTarget!.GlobalPosition.Y)
			<= CombatArrivalDistance;

		if (!isVerticallyAligned)
			return;

		_attackCooldownRemaining = 0.0;
		_state = HeroState.WaitingToAttack;
	}

	private void UpdateWaitingToAttack(double delta)
	{
		StopMovementAnimation();

		if (!Targeting.IsValidMonsterTarget(CurrentTarget))
			return;

		bool targetMovedOutOfRange = !IsTargetWithinAttackRange(CurrentTarget!);

		bool targetMovedToAnotherY = Mathf.Abs(GlobalPosition.Y - CurrentTarget!.GlobalPosition.Y) > AttackRangeTolerance;

		if (targetMovedOutOfRange
			|| targetMovedToAnotherY)
		{
			_state = HeroState.ApproachingTarget;
			return;
		}

		_attackCooldownRemaining -= delta;

		if (_attackCooldownRemaining > 0.0)
			return;

		BeginAttack();
	}

	private void BeginAttack()
	{

		if (!Targeting.IsValidMonsterTarget(CurrentTarget))
			return;

		_state = HeroState.Attacking;
		_attackTimeRemaining = TemporaryAttackDuration;
		_attackReleaseEmitted = false;

		StopMovementAnimation();

		GD.Print(
			$"{Name} began attacking {CurrentTarget!.Name}.");
	}

	private void UpdateAttack(double delta)
	{
		if (!Targeting.IsValidMonsterTarget(CurrentTarget))
		{
			EndAttack();
			return;
		}

		_attackTimeRemaining -= delta;

		float duration =
			Mathf.Max(TemporaryAttackDuration, 0.001f);

		float progress =
			1.0f
			- (float)(_attackTimeRemaining / duration);

		progress = Mathf.Clamp(
			progress,
			0.0f,
			1.0f);

		TryEmitAttackRelease(progress);

		float lungeCurve =
			Mathf.Sin(progress * Mathf.Pi);

		VisualRoot.Position =
			_visualRestPosition
			+ Vector2.Left
			* TemporaryAttackLungeDistance
			* lungeCurve;

		if (_attackTimeRemaining > 0.0)
			return;

		EndAttack();
	}

	private void EndAttack()
	{
		VisualRoot.Position =
			_visualRestPosition;

		_attackTimeRemaining = 0.0;
		_attackCooldownRemaining =
			TemporaryAttackInterval;

		if (!Targeting.IsValidMonsterTarget(CurrentTarget))
		{
			_state = HeroState.WaitingToAttack;
			return;
		}

		bool targetStillInRange =
	IsTargetWithinAttackRange(CurrentTarget!);

		bool targetStillAligned =
			Mathf.Abs(
				GlobalPosition.Y
				- CurrentTarget!.GlobalPosition.Y)
			<= AttackRangeTolerance;

		_state =
			targetStillInRange
			&& targetStillAligned
				? HeroState.WaitingToAttack
				: HeroState.ApproachingTarget;

		_state =
			targetStillInRange
				? HeroState.WaitingToAttack
				: HeroState.ApproachingTarget;
	}

	private void TryEmitAttackRelease(float attackProgress)
	{
		if (_attackReleaseEmitted)
			return;

		if (attackProgress
			< TemporaryAttackReleasePoint)
		{
			return;
		}

		if (!Targeting.IsValidMonsterTarget(CurrentTarget))
			return;

		_attackReleaseEmitted = true;

		EmitSignal(
			SignalName.AttackReleased,
			this,
			CurrentTarget!);
	}

	private void UpdateReturnToFormation(double delta)
	{
		Vector2 previousPosition =
			GlobalPosition;

		Vector2 destination =
			FormationPosition;

		float movementDistance =
			CombatMoveSpeed * (float)delta;

		GlobalPosition = GlobalPosition.MoveToward(
			destination,
			movementDistance);

		_movedThisFrame =
			!GlobalPosition.IsEqualApprox(previousPosition);

		if (GlobalPosition.DistanceTo(destination)
			> CombatArrivalDistance)
		{
			return;
		}

		GlobalPosition = destination;
		_state = HeroState.InFormation;
		_animationTime = 0.0;

		VisualRoot.Position =
			_visualRestPosition;

		GD.Print(
			$"{Name} returned to formation at " +
			$"{FormationPosition}.");
	}
	private void ApplyJourneyState(
	JourneyStateService.JourneyState state)
	{
		_animationTime = 0.0;
		_attackTimeRemaining = 0.0;
		_attackCooldownRemaining = 0.0;
		_attackReleaseEmitted = false;

		VisualRoot.Position = _visualRestPosition;

		if (state
			== JourneyStateService.JourneyState.Encounter)
		{
			if (Targeting.IsValidMonsterTarget(CurrentTarget))
			{
				_state = HeroState.ApproachingTarget;
			}

			return;
		}

		bool isAtFormation =
			GlobalPosition.DistanceTo(FormationPosition)
			<= CombatArrivalDistance;

		_state =
			isAtFormation
				? HeroState.InFormation
				: HeroState.ReturningToFormation;
	}

	private void OnJourneyStateChanged(
		JourneyStateService.JourneyState previousState,
		JourneyStateService.JourneyState currentState)
	{
		ApplyJourneyState(currentState);
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

		if (JourneyState.CurrentState == JourneyStateService.JourneyState.Encounter)
		{
			_state = HeroState.ApproachingTarget;
		}
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

		if (JourneyState.CurrentState
			== JourneyStateService.JourneyState.Traveling)
		{
			bool isAtFormation =
				GlobalPosition.DistanceTo(FormationPosition)
				<= CombatArrivalDistance;

			_state =
				isAtFormation
					? HeroState.InFormation
					: HeroState.ReturningToFormation;
		}

		GD.Print($"{Name} cleared its monster target.");
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
}
