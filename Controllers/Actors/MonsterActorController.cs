using Godot;

public partial class MonsterActorController : Node2D
{
	[Signal]
	public delegate void AttackReleasedEventHandler(
	MonsterActorController attacker,
	HeroActorController target);

	[Signal]
	public delegate void DiedEventHandler(
	MonsterActorController monster);

	private enum MonsterState
	{
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
	public bool HasValidTarget => IsValidHeroTarget(CurrentTarget);

	[ExportCategory("Visuals")]
	[Export]
	public Node2D VisualRoot { get; set; } = null!;

	[Export]
	public Marker2D ImpactOrigin { get; set; } = null!;

	[ExportCategory("Combat Movement")]
	[Export(PropertyHint.Range, "0.1,20,0.1")]
	public float CombatArrivalDistance { get; set; } = 1.0f;
	
	[Export(PropertyHint.Range, "0,10,0.1")]
	public float FacingDeadZone { get; set; } = 1.0f;

    public float AttackDamage { get; set; }

    public MonsterDefinition Definition
    {
        get;
        private set;
    } = null!;

    public string ContentId =>
        Definition.ContentId;

    public string DisplayName =>
        Definition.DisplayName;

    public MonsterCombatProfile CombatProfile { get; } = new();

	public Vector2 ImpactPosition => ImpactOrigin.GlobalPosition;

	private MonsterState _state = MonsterState.WaitingForTarget;
	
	public FacingDirection Facing { get; private set; }
	= FacingDirection.Right;

	public HeroActorController? CurrentTarget { get; private set; }

	public bool IsDead => _state == MonsterState.Dead;

	public CombatHealthState Health { get; } = new();

	public bool HasTarget => IsValidHeroTarget(CurrentTarget);

    public void Configure(MonsterDefinition definition)
    {
        if (!GodotObject.IsInstanceValid(definition))
        {
            throw new System.ArgumentNullException(
                nameof(definition));
        }

        Definition = definition;
    }

	public void RefreshTargetValidity()
	{
		if (IsDead)
			return;

		if (IsValidHeroTarget(CurrentTarget))
			return;

		CurrentTarget = null;

		_attackCooldownRemaining = 0.0;
		_attackTimeRemaining = 0.0;
		_attackReleaseEmitted = false;

		StopAttackPresentation();

		_state =
			MonsterState.WaitingForTarget;

		GD.Print(
			$"{Name} released its invalid hero target.");
	}

	public void EnterDeadState()
	{
		if (IsDead)
			return;

		_state = MonsterState.Dead;
		CurrentTarget = null;

		_attackCooldownRemaining = 0.0;
		_attackTimeRemaining = 0.0;
		_attackReleaseEmitted = false;

		StopAttackPresentation();

		GD.Print(
			$"{Name} entered its Dead state.");

		EmitSignal(
			SignalName.Died,
			this);
	}

    private void ApplyDefinition()
    {
        VisualRoot.Scale = Definition.VisualScale;
        VisualRoot.Modulate = Definition.VisualModulate;
        CombatProfile.MaximumHealth = Definition.MaximumHealth;
        CombatProfile.AttackDamage = Definition.AttackDamage;
        CombatProfile.AttackRange =  Definition.AttackRange;
        CombatProfile.AttackInterval = Definition.AttackInterval;
        CombatProfile.AttackDuration =  Definition.AttackDuration;
        CombatProfile.AttackReleasePoint = Definition.AttackReleasePoint;
        CombatProfile.AttackLungeDistance = Definition.AttackLungeDistance;
        CombatProfile.MoveSpeed = Definition.CombatMoveSpeed;
        CombatProfile.AttackDelivery = Definition.AttackDelivery;
    }

    public override void _Ready()
	{
        if (!GodotObject.IsInstanceValid(Definition))
        {
            GD.PushError(
                $"{Name} cannot initialize because no " +
                "MonsterDefinition was configured.");

            SetProcess(false);
            return;
        }

        System.Collections.Generic.IReadOnlyList<string>
    definitionErrors =
        Definition.GetValidationErrors();

        if (definitionErrors.Count > 0)
        {
            foreach (string error in definitionErrors)
            {
                GD.PushError(error);
            }

            SetProcess(false);
            return;
        }

        if (!GodotObject.IsInstanceValid(VisualRoot))
		{
			GD.PushError(
				"MonsterActorController is missing its " +
				"VisualRoot Inspector reference.");

			SetProcess(false);
			return;
		}

		if (!GodotObject.IsInstanceValid(ImpactOrigin))
		{
			GD.PushError(
				"MonsterActorController is missing its " +
				"ImpactOrigin Inspector reference.");

			SetProcess(false);
			return;
		}

        ApplyDefinition();

        Health.Initialize(
            CombatProfile.MaximumHealth);
        _visualRestPosition = VisualRoot.Position;

        GD.Print(
			$"{Name} initialized as " +
			$"{Definition.ContentId} " +
			$"('{Definition.DisplayName}') with " +
			$"{Health.CurrentHealth}/" +
			$"{Health.MaximumHealth} health.");
    }

	private static bool IsValidHeroTarget(HeroActorController? hero)
	{
		return hero is not null
			&& GodotObject.IsInstanceValid(hero)
			&& hero.IsInsideTree()
			&& !hero.IsIncapacitated;
	}

	private Vector2 CalculateAttackPosition(HeroActorController target)
	{
		return new Vector2(
			target.GlobalPosition.X - CombatProfile.AttackRange,
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
			CombatProfile.MoveSpeed * (float)delta;

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

    public bool TryAcquireTarget( HeroActorController target)
    {
        if (IsDead)
            return false;

        if (!IsValidHeroTarget(target))
            return false;

        if (HasValidTarget)
            return false;

        CurrentTarget = target;
        _state = MonsterState.ApproachingTarget;

        GD.Print(
            $"{Name} locked onto {target.Name}.");

        return true;
    }

    private void BeginAttack()
	{
		if (!IsValidHeroTarget(CurrentTarget))
			return;

		_state = MonsterState.Attacking;

        _attackTimeRemaining = CombatProfile.AttackDuration;

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

        float duration = Mathf.Max( CombatProfile.AttackDuration, 0.001f);

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
            * CombatProfile.AttackLungeDistance
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
			< CombatProfile.AttackReleasePoint)
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
			CombatProfile.AttackInterval;

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
