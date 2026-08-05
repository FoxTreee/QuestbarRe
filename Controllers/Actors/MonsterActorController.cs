using Godot;

public partial class MonsterActorController : Node2D
{
	[Signal]
	public delegate void AttackReleasedEventHandler(
	MonsterActorController attacker,
	HeroActorController target);
	
	private enum MonsterState
	{
		Entering,
		WaitingForTarget,
		ApproachingTarget,
		WaitingToAttack,
		Attacking,
		Dead
	}
	
	private Vector2 _visualRestPosition;
	private double _attackCooldownRemaining;
	private double _attackTimeRemaining;
	private bool _attackReleaseEmitted;
	
	[ExportCategory("Visuals")]
	[Export]
	public Node2D VisualRoot { get; set; } = null!;
	
	[ExportCategory("Entrance")]
	[Export(PropertyHint.Range, "0,500,1")]
	public float EntrySpeed { get; set; } = 100.0f;

	[Export(PropertyHint.Range, "0.1,20,0.1")]
	public float ArrivalDistance { get; set; } = 1.0f;

	[ExportCategory("Temporary Combat Movement")]
	[Export(PropertyHint.Range, "0,500,1")]
	public float CombatMoveSpeed { get; set; } = 100.0f;

	[Export(PropertyHint.Range, "0,400,1")]
	public float TemporaryAttackRange { get; set; } = 28.0f;

	[Export(PropertyHint.Range, "0.1,20,0.1")]
	public float CombatArrivalDistance { get; set; } = 1.0f;
	
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

	public Vector2 EntryDestination { get; private set; }

	private MonsterState _state = MonsterState.WaitingForTarget;

	public bool IsEntering => _state == MonsterState.Entering;
	
	public FacingDirection Facing { get; private set; }
	= FacingDirection.Right;

	public HeroActorController? CurrentTarget { get; private set; }

	public bool HasTarget =>
		IsValidHeroTarget(CurrentTarget);

	public void InitializeEntrance( Vector2 spawnPosition, Vector2 entryDestination)
	{
		GlobalPosition = spawnPosition;
		EntryDestination = entryDestination;

		CurrentTarget = null;
		_state = MonsterState.Entering;

		GD.Print(
			$"Monster entrance initialized. " +
			$"Spawn={spawnPosition}, " +
			$"Destination={entryDestination}");
	}

	private void UpdateEntrance(double delta)
	{
		float movementDistance =
			EntrySpeed * (float)delta;

		GlobalPosition = GlobalPosition.MoveToward(
			EntryDestination,
			movementDistance);

		if (GlobalPosition.DistanceTo(EntryDestination)
			> ArrivalDistance)
		{
			return;
		}

		GlobalPosition = EntryDestination;
		_state = MonsterState.WaitingForTarget;

		GD.Print(
			$"Monster reached encounter position " +
			$"{EntryDestination}.");
	}
	
	public override void _Ready()
	{
		if (!GodotObject.IsInstanceValid(VisualRoot))
		{
			GD.PushError(
				"MonsterActorController is missing its " +
				"VisualRoot Inspector reference.");

			SetProcess(false);
			return;
		}

		_visualRestPosition = VisualRoot.Position;
	}

	private static bool IsValidHeroTarget(HeroActorController? hero)
	{
		return hero is not null
			&& GodotObject.IsInstanceValid(hero)
			&& hero.IsInsideTree();
	}

	private Vector2 CalculateAttackPosition(HeroActorController target)
	{
		return new Vector2(
			target.GlobalPosition.X - TemporaryAttackRange,
			target.GlobalPosition.Y);
	}
	
	private void UpdateFacingTowardTarget()
	{
		if (!IsValidHeroTarget(CurrentTarget))
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
		if (!IsValidHeroTarget(CurrentTarget))
		{
			CurrentTarget = null;
			_state = MonsterState.WaitingForTarget;
			return;
		}

		Vector2 attackPosition =
			CalculateAttackPosition(CurrentTarget!);

		float movementDistance =
			CombatMoveSpeed * (float)delta;

		GlobalPosition = GlobalPosition.MoveToward(
			attackPosition,
			movementDistance);

		if (GlobalPosition.DistanceTo(attackPosition)
			> CombatArrivalDistance)
		{
			return;
		}

		GlobalPosition = attackPosition;
		_attackCooldownRemaining = 0.0;
		_state = MonsterState.WaitingToAttack;

		GD.Print(
			$"{Name} reached attack position for " +
			$"{CurrentTarget!.Name} at {attackPosition}.");
	}

	private void UpdateWaitingToAttack(double delta)
	{
		StopAttackPresentation();

		if (!IsValidHeroTarget(CurrentTarget))
		{
			CurrentTarget = null;
			_state = MonsterState.WaitingForTarget;
			return;
		}

		Vector2 attackPosition =
			CalculateAttackPosition(CurrentTarget!);

		bool targetMovedOutOfRange =
			GlobalPosition.DistanceTo(attackPosition)
			> CombatArrivalDistance;

		if (targetMovedOutOfRange)
		{
			_state = MonsterState.ApproachingTarget;
			return;
		}

		_attackCooldownRemaining -= delta;

		if (_attackCooldownRemaining > 0.0)
			return;

		BeginAttack();
	}

	public bool TryEngage( HeroActorController attacker)
	{
		if (!IsValidHeroTarget(attacker))
			return false;

		if (HasTarget)
			return false;

		CurrentTarget = attacker;
		_state = MonsterState.ApproachingTarget;

		GD.Print(
			$"{Name} engaged {attacker.Name} " +
			$"and interrupted its entrance.");

		return true;
	}
	
	private void BeginAttack()
	{
		if (!IsValidHeroTarget(CurrentTarget))
			return;

		_state = MonsterState.Attacking;
		_attackTimeRemaining = TemporaryAttackDuration;
		_attackReleaseEmitted = false;

		StopAttackPresentation();

		GD.Print(
			$"{Name} began attacking {CurrentTarget!.Name}.");
	}
	
	private void UpdateAttack(double delta)
	{
		if (!IsValidHeroTarget(CurrentTarget))
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

		Vector2 attackDirection =
			Facing == FacingDirection.Left
				? Vector2.Left
				: Vector2.Right;

		VisualRoot.Position =
			_visualRestPosition
			+ attackDirection
			* TemporaryAttackLungeDistance
			* lungeCurve;

		if (_attackTimeRemaining > 0.0)
			return;

		EndAttack();
	}
	
	public override void _Process(double delta)
	{
		UpdateFacingTowardTarget();
		
		switch (_state)
		{
			case MonsterState.Entering:
				UpdateEntrance(delta);
				break;

			case MonsterState.WaitingForTarget:
				break;

			case MonsterState.ApproachingTarget:
				UpdateCombatApproach(delta);
				break;

			case MonsterState.WaitingToAttack:
				UpdateWaitingToAttack(delta);
				break;

			case MonsterState.Attacking:
				UpdateAttack(delta);
				break;

			case MonsterState.Dead:
				break;
		}
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

		if (!IsValidHeroTarget(CurrentTarget))
			return;

		_attackReleaseEmitted = true;

		EmitSignal(
			SignalName.AttackReleased,
			this,
			CurrentTarget!);
	}

	private void EndAttack()
	{
		StopAttackPresentation();

		_attackTimeRemaining = 0.0;
		_attackCooldownRemaining =
			TemporaryAttackInterval;

		if (!IsValidHeroTarget(CurrentTarget))
		{
			CurrentTarget = null;
			_state = MonsterState.WaitingForTarget;
			return;
		}

		Vector2 attackPosition =
			CalculateAttackPosition(CurrentTarget!);

		bool targetStillInRange =
			GlobalPosition.DistanceTo(attackPosition)
			<= CombatArrivalDistance;

		_state =
			targetStillInRange
				? MonsterState.WaitingToAttack
				: MonsterState.ApproachingTarget;
	}
	
	private void StopAttackPresentation()
	{
		VisualRoot.Position = _visualRestPosition;
	}
}
