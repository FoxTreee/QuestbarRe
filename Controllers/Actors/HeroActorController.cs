using Godot;
using System.Collections.Generic;

public partial class HeroActorController : Node2D
{
	[Signal]
	public delegate void AttackReleasedEventHandler(
	HeroActorController attacker,
	MonsterActorController target);

	[Signal]
	public delegate void IncapacitatedEventHandler(
	HeroActorController hero);

	private enum HeroState
	{
		InFormation,
		ApproachingTarget,
		WaitingToAttack,
		Attacking,
		ReturningToFormation,
		Incapacitated
	}

	private Vector2 _visualRestPosition;
	private double _animationTime;
	private bool _movedThisFrame;
	private HeroState _state = HeroState.InFormation;
	private double _attackCooldownRemaining;
	private double _attackTimeRemaining;
	private bool _attackReleaseEmitted;
	private ActorHealthBarController _healthBar = null!;
	private readonly List<AbilityDefinition> _abilities = new();
	private readonly HeroAbilityCooldownState
		_abilityCooldowns = new();

	public HeroCombatProfile CombatProfile { get; } = new();
	public float CombatPresentationScale { get; private set; } = 1.0f;
	public bool IsIncapacitated => _state == HeroState.Incapacitated;

	public void SetCombatPresentationScale(float scale)
	{
		CombatPresentationScale =
			Mathf.Max(scale, 0.01f);
	}

	[ExportCategory("Combat Identity")]
	[Export(PropertyHint.Flags, "Melee,Ranged,Caster,Healer,Tank,Summoner,Armored")]
	public int CombatTagMask { get; set; } =
		(int)HeroCombatTag.Melee;

	public HeroCombatTag CombatTags =>
		(HeroCombatTag)CombatTagMask;

	public bool HasCombatTag(HeroCombatTag tag)
	{
		return tag != HeroCombatTag.None
			&& (CombatTags & tag) != 0;
	}

	public HeroDefinition? Definition
	{
		get;
		private set;
	}

	public IReadOnlyList<AbilityDefinition> Abilities =>
		_abilities;

	public double GetAbilityCooldownRemaining(
		string abilityContentId)
	{
		return _abilityCooldowns.GetRemainingSeconds(
			abilityContentId);
	}

	public bool IsAbilityReady(string abilityContentId)
	{
		return _abilityCooldowns.IsReady(
			abilityContentId);
	}

	public bool TryStartAbilityCooldown(
		AbilityDefinition ability)
	{
		return _abilityCooldowns.TryStart(ability);
	}

	public void Configure(
		HeroDefinition definition,
		IReadOnlyList<AbilityDefinition>? abilities = null)
	{
		if (!GodotObject.IsInstanceValid(definition))
		{
			throw new System.ArgumentNullException(
				nameof(definition));
		}

		Definition = definition;
		CombatTagMask = definition.CombatTagMask;
		TemporaryMaximumHealth = definition.MaximumHealth;
		TemporaryAttackDamage = definition.AttackDamage;
		TemporaryAttackRange = definition.AttackRange;
		TemporaryAttackInterval = definition.AttackInterval;
		TemporaryAttackDuration = definition.AttackDuration;
		TemporaryAttackReleasePoint =
			definition.AttackReleasePoint;
		TemporaryAttackLungeDistance =
			definition.AttackLungeDistance;
		TemporaryAttackDelivery = definition.AttackDelivery;
		CombatMoveSpeed = definition.CombatMoveSpeed;

		_abilities.Clear();

		if (abilities is not null)
		{
			foreach (AbilityDefinition ability in abilities)
			{
				if (!GodotObject.IsInstanceValid(ability))
					continue;

				_abilities.Add(ability);
			}
		}

		_abilityCooldowns.Configure(_abilities);
	}

	public bool TryGetAbility(
		string contentId,
		out AbilityDefinition ability)
	{
		foreach (AbilityDefinition candidate in _abilities)
		{
			if (!candidate.ContentId.Equals(
				contentId.Trim(),
				System.StringComparison.OrdinalIgnoreCase))
			{
				continue;
			}

			ability = candidate;
			return true;
		}

		ability = null!;
		return false;
	}


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

	[Export]
	public BodyBounds2D BodyBounds { get; set; } = null!;
	
	[Export]
	public Marker2D ProjectileOrigin { get; set; } = null!;

	[Export]
	public HeroAbilityCooldownIndicatorController
		AbilityCooldownIndicator { get; set; } = null!;

	[ExportCategory("Travel Animation")]
	[Export(PropertyHint.Range, "0,20,0.5")]
	public float BobHeight { get; set; } = 4.0f;

	[Export(PropertyHint.Range, "0,20,0.1")]
	public float BobSpeed { get; set; } = 7.0f;

	[Export(PropertyHint.Range, "0,6.28,0.01")]
	public float BobPhaseOffset { get; set; } = 0.0f;

	[Export(PropertyHint.Range, "0,0.5,0.01")]
	public float MovementAnimationGraceTime { get; set; } = 0.15f;

	[ExportCategory("Temporary Combat Values")]
	[Export(PropertyHint.Range, "1,100000,1")]
	public float TemporaryMaximumHealth { get; set; } = 100.0f;

	[Export(PropertyHint.Range, "0,100000,1")]
	public float TemporaryAttackDamage { get; set; } = 20.0f;

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
	public AttackDeliveryMode TemporaryAttackDelivery { get; set; }
	= AttackDeliveryMode.ImmediateImpact;

	[Export]
	public TargetingService Targeting { get; set; } = null!;

	public MonsterActorController? CurrentTarget { get; private set; }

	public Vector2 FormationPosition => FormationAnchor.GlobalPosition + FormationOffset;

	public CombatHealthState Health { get; } = new();

	// Incapacitation Reset -- DEBUG ONLY
	public void DebugResetFromIncapacitation()
	{
		Health.RestoreToMaximum();

		CurrentTarget = null;

		_attackCooldownRemaining = 0.0;
		_attackTimeRemaining = 0.0;
		_attackReleaseEmitted = false;
		_initialAttackPending = false;
		_movementAnimationGraceRemaining = 0.0;
		_abilityCooldowns.Reset();

		StopMovementAnimation();

		GlobalPosition =
			FormationAnchor.GlobalPosition
			+ FormationOffset;

		_state =
			HeroState.InFormation;

		DebugLog.Print(
			$"{Name} debug-reset with " +
			$"{Health.CurrentHealth}/" +
			$"{Health.MaximumHealth} health.");
	}
	
	public void ResumeCombatAfterDebugReset()
	{
		if (IsIncapacitated)
			return;

		if (JourneyState.CurrentState
			!= JourneyStateService.JourneyState.Encounter)
		{
			ApplyJourneyState(
				JourneyState.CurrentState);

			return;
		}

		if (!Targeting.IsValidMonsterTarget(CurrentTarget))
		{
			_state =
				HeroState.InFormation;

			return;
		}

		_initialAttackPending = true;
		_attackCooldownRemaining = 0.0;
		_attackTimeRemaining = 0.0;
		_attackReleaseEmitted = false;

		_state =
			HeroState.ApproachingTarget;

		DebugLog.Print(
			$"{Name} rejoined combat against " +
			$"{CurrentTarget!.Name}.");
	}

	public override void _Ready()
	{
		if (!ValidateReferences())
		{
			SetProcess(false);
			return;
		}

		InitializeCombatProfile();

		Health.Initialize(CombatProfile.MaximumHealth);
		_visualRestPosition = VisualRoot.Position;

		JourneyState.StateChanged += OnJourneyStateChanged;
        _healthBar.Bind(Health);
		AbilityCooldownIndicator.Bind(this);
        ApplyJourneyState(JourneyState.CurrentState);
		SnapToFormation();

		DebugLog.Print(
			$"HeroActor initialized at formation position " +
			$"{FormationPosition}." +
			$"{Name} initialized with " +
			$"{Health.CurrentHealth}/" +
			$"{Health.MaximumHealth} health. " +
			$"Combat tags={CombatTags}.");
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
		_abilityCooldowns.Update(delta);
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
				UpdateMovementPresentation(delta);

				break;

			case HeroState.WaitingToAttack:

				UpdateWaitingToAttack(delta);

				break;

			case HeroState.Attacking:

				UpdateAttack(delta);

				break;

			case HeroState.ReturningToFormation:

				UpdateReturnToFormation(delta);
				UpdateMovementPresentation(delta);

				break;

			case HeroState.Incapacitated:

				StopMovementAnimation();

				break;
		}
	}

	private double _movementAnimationGraceRemaining;

	private void UpdateMovementPresentation(double delta)
	{
		if (_movedThisFrame)
		{
			_movementAnimationGraceRemaining =
				MovementAnimationGraceTime;
		}
		else
		{
			_movementAnimationGraceRemaining =
				Mathf.Max(
					0.0,
					_movementAnimationGraceRemaining - delta);
		}

		if (_movedThisFrame
			|| _movementAnimationGraceRemaining > 0.0)
		{
			UpdateMovementAnimation(delta);
			return;
		}

		StopMovementAnimation();
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

	private bool _initialAttackPending;

	private float GetBodyClearanceDistance(
		MonsterActorController target)
	{
		return CombatSpacing.GetBodyClearanceDistance(
			CombatProfile.CombatRadius,
			target.CombatProfile.CombatRadius,
			CombatProfile.AttackLungeDistance,
			target.CombatProfile.AttackLungeDistance,
			CombatPresentationScale,
			target.CombatPresentationScale);
	}

	private float GetRequiredAttackDistance(MonsterActorController target)
	{
		return CombatSpacing.GetRequiredCenterDistance(
			CombatProfile.AttackRange,
			CombatProfile.CombatRadius,
			target.CombatProfile.CombatRadius,
			CombatProfile.AttackLungeDistance,
			target.CombatProfile.AttackLungeDistance,
			CombatPresentationScale,
			target.CombatPresentationScale);
	}

    private Vector2 CalculateApproachPosition(MonsterActorController target)
    {
        float requiredCenterDistance = GetRequiredAttackDistance(target);

        float horizontalDifference = target.GlobalPosition.X - GlobalPosition.X;

        float directionToTarget = Mathf.Sign(horizontalDifference);

        float destinationX = target.GlobalPosition.X - directionToTarget * requiredCenterDistance;

        return new Vector2( destinationX, target.GlobalPosition.Y);
    }

	private void InitializeCombatProfile()
	{
		CombatProfile.AttackRange = TemporaryAttackRange;
		CombatProfile.CombatRadius =
			BodyBounds.GetHorizontalRadiusInParentSpace()
			* Mathf.Abs(VisualRoot.Scale.X);
        CombatProfile.AttackInterval = TemporaryAttackInterval;
		CombatProfile.MoveSpeed = CombatMoveSpeed;
		CombatProfile.AttackDelivery = TemporaryAttackDelivery;
		CombatProfile.MaximumHealth = TemporaryMaximumHealth;
		CombatProfile.AttackDamage = TemporaryAttackDamage;
		CombatProfile.AttackDuration = TemporaryAttackDuration;
        CombatProfile.AttackLungeDistance = TemporaryAttackLungeDistance;
    }

	private bool IsTargetWithinAttackRange(MonsterActorController target)
	{
		float minimumCenterDistance =
			GetBodyClearanceDistance(target);

		float requiredCenterDistance =
			GetRequiredAttackDistance(target);

		float horizontalDistance =
			Mathf.Abs( GlobalPosition.X - target.GlobalPosition.X);

		float scaledTolerance =
			AttackRangeTolerance
			* CombatPresentationScale;

		return horizontalDistance
			>= minimumCenterDistance - scaledTolerance
			&& horizontalDistance
			<= requiredCenterDistance + scaledTolerance;
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

		DebugLog.Print(
			$"{Name} now faces {Facing} toward " +
			$"{CurrentTarget.Name}.");
	}

	private void PrepareToAttack()
	{
		if (_initialAttackPending)
		{
			_initialAttackPending = false;
			_attackCooldownRemaining = 0.0;

			BeginAttack();
			return;
		}

		_state = HeroState.WaitingToAttack;
	}

	private void UpdateCombatApproach(double delta)
	{
		if (!Targeting.IsValidMonsterTarget(CurrentTarget))
			return;

		MonsterActorController target =
			CurrentTarget!;

		bool targetWithinRange =
			IsTargetWithinAttackRange(target);

		bool verticallyAligned =
			Mathf.Abs(
				GlobalPosition.Y
				- target.GlobalPosition.Y)
			<= CombatArrivalDistance;

		if (targetWithinRange && verticallyAligned)
		{
			PrepareToAttack();
			return;
		}

		Vector2 previousPosition = GlobalPosition;
		Vector2 approachPosition = CalculateApproachPosition(target);
		float movementDistance = CombatProfile.MoveSpeed * (float)delta;
		GlobalPosition = GlobalPosition.MoveToward( approachPosition, movementDistance);
		_movedThisFrame = !GlobalPosition.IsEqualApprox(previousPosition);
		targetWithinRange = IsTargetWithinAttackRange(target);

		verticallyAligned =
			Mathf.Abs(
				GlobalPosition.Y
				- target.GlobalPosition.Y)
			<= CombatArrivalDistance;

		if (!targetWithinRange || !verticallyAligned)
			return;

		PrepareToAttack();
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
		_attackTimeRemaining = CombatProfile.AttackDuration;
		_attackReleaseEmitted = false;

		StopMovementAnimation();

		DebugLog.Print(
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

		// AttackReleased is synchronous. The resulting impact can kill the
		// target, retarget this hero, or end the encounter before this attack
		// frame resumes. Respect that newer state instead of letting stale
		// attack processing overwrite it.
		if (_state != HeroState.Attacking)
		{
			VisualRoot.Position = _visualRestPosition;
			return;
		}

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
			* CombatPresentationScale
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
			CombatProfile.AttackInterval;

		if (!Targeting.IsValidMonsterTarget(CurrentTarget))
		{
			_state = HeroState.WaitingToAttack;
			return;
		}

		bool targetStillInRange = IsTargetWithinAttackRange(CurrentTarget!);

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
	}

	public void EnterIncapacitatedState()
	{
		if (IsIncapacitated)
			return;

		_state = HeroState.Incapacitated;
		CurrentTarget = null;

		_attackCooldownRemaining = 0.0;
		_attackTimeRemaining = 0.0;
		_attackReleaseEmitted = false;
		_initialAttackPending = false;
		_movementAnimationGraceRemaining = 0.0;

		StopMovementAnimation();

		DebugLog.Print(
			$"{Name} entered its Incapacitated state.");

		EmitSignal(
			SignalName.Incapacitated,
			this);
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
			CombatProfile.MoveSpeed * (float)delta;

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

		DebugLog.Print(
			$"{Name} returned to formation at " +
			$"{FormationPosition}.");
	}

	private void ApplyJourneyState(JourneyStateService.JourneyState state)
	{
		if (IsIncapacitated)
		{
			StopMovementAnimation();
			return;
		}
		
		_animationTime = 0.0;
		_attackTimeRemaining = 0.0;
		_attackCooldownRemaining = 0.0;
		_attackReleaseEmitted = false;
		_initialAttackPending = false;
		_movementAnimationGraceRemaining = 0.0;

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
		valid &= Require(BodyBounds, nameof(BodyBounds));
		valid &= Require(Targeting, nameof(Targeting));
		valid &= Require(ProjectileOrigin, nameof(ProjectileOrigin));
		valid &= Require(
			AbilityCooldownIndicator,
			nameof(AbilityCooldownIndicator));

        if (GodotObject.IsInstanceValid(VisualRoot))
        {
            _healthBar =
                VisualRoot.GetNodeOrNull
                    <ActorHealthBarController>(
                        "ActorHealthBar")!;

            valid &= Require(
                _healthBar,
                "VisualRoot/ActorHealthBar");
        }

        return valid;
	}
	
	public void SnapToFormation()
	{
		GlobalPosition = FormationPosition;
	}

	public void RefreshTarget(IReadOnlyList<MonsterActorController> candidates)
	{
		if (IsIncapacitated)
			return;
		
		if (CurrentTarget is not null
			&& Targeting.IsValidMonsterTarget(CurrentTarget)
			&& ContainsTarget(candidates, CurrentTarget))
		{
			return;
		}

		MonsterActorController? previousTarget =
			CurrentTarget;

		CurrentTarget = Targeting.SelectPriorityMonster(candidates);

		if (CurrentTarget == previousTarget)
			return;

		if (CurrentTarget is null)
		{
			DebugLog.Print(
				$"{Name} has no valid monster target.");

			return;
		}

		_initialAttackPending = true;
		
		DebugLog.Print(
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
		_initialAttackPending = false;

		if (IsIncapacitated)
			return;

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

		DebugLog.Print($"{Name} cleared its monster target.");
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
