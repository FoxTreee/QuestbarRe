using Godot;
using System.Collections.Generic;

public partial class HeroActorController : Node2D
{
	[Signal]
	public delegate void AttackReleasedEventHandler(
	HeroActorController attacker,
	MonsterActorController target);

	[Signal]
	public delegate void AbilityReleasedEventHandler(
	HeroActorController caster,
	HeroActorController target,
	AbilityDefinition ability);

	[Signal]
	public delegate void IncapacitatedEventHandler(
	HeroActorController hero);

	private enum HeroState
	{
		InFormation,
		ApproachingTarget,
		WaitingToAttack,
		Attacking,
		UsingAbility,
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
	private AbilityDefinition? _activeAbility;
	private HeroActorController? _activeAbilityTarget;
	private double _abilityCastTimeRemaining;
	private IReadOnlyList<HeroActorController> _partyMembers =
		System.Array.Empty<HeroActorController>();

	public HeroCombatProfile CombatProfile { get; } = new();
	public float CombatPresentationScale { get; private set; } = 1.0f;
	public bool IsIncapacitated => _state == HeroState.Incapacitated;
	private bool UsesMeleeEngagementSlots =>
		HasCombatTag(HeroCombatTag.Melee);

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

	public bool IsUsingAbility =>
		_activeAbility is not null;

	public bool TryBeginAbility(
		AbilityDefinition ability,
		HeroActorController target)
	{
		if (!GodotObject.IsInstanceValid(ability)
			|| !GodotObject.IsInstanceValid(target)
			|| IsIncapacitated
			|| !Health.IsAlive
			|| IsUsingAbility
			|| !TryGetAbility(ability.ContentId, out _)
			|| !IsAbilityReady(ability.ContentId)
			|| !IsValidAbilityTarget(ability, target))
		{
			return false;
		}

		_activeAbility = ability;
		_activeAbilityTarget = target;
		_abilityCastTimeRemaining =
			Mathf.Max(ability.CastTimeSeconds, 0.0f);

		_attackTimeRemaining = 0.0;
		_attackReleaseEmitted = false;
		_initialAttackPending = false;
		_state = HeroState.UsingAbility;

		StopMovementAnimation();

		DebugLog.Print(
			$"{Name} began using ability " +
			$"'{ability.DisplayName}' on {target.Name}. " +
			$"Cast={ability.CastTimeSeconds:0.##}s.");

		return true;
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

	private bool IsValidAbilityTarget(
		AbilityDefinition ability,
		HeroActorController target)
	{
		return ability.EffectType switch
		{
			AbilityEffectType.DirectHealing =>
				IsLivingPartyMember(target),

			AbilityEffectType.AreaTaunt =>
				target == this,

			_ => false
		};
	}

	private bool IsLivingPartyMember(
		HeroActorController target)
	{
		if (!GodotObject.IsInstanceValid(target)
			|| target.IsIncapacitated
			|| !target.Health.IsAlive)
		{
			return false;
		}

		foreach (HeroActorController partyMember
			in _partyMembers)
		{
			if (partyMember == target)
				return true;
		}

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

	[ExportCategory("Melee Engagement Slots")]
	[Export(PropertyHint.Range, "0.5,2,0.05")]
	public float MeleeSlotHorizontalSpacingMultiplier
	{ get; set; } = 1.0f;

	[Export(PropertyHint.Range, "0.25,2,0.05")]
	public float MeleeSlotVerticalSpacingMultiplier
	{ get; set; } = 0.75f;

	[ExportCategory("Hero Separation")]
	[Export(PropertyHint.Range, "0,3,0.05")]
	public float HeroSeparationHorizontalRangeMultiplier
	{ get; set; } = 1.0f;

	[Export(PropertyHint.Range, "0,3,0.05")]
	public float HeroSeparationVerticalSpacingMultiplier
	{ get; set; } = 0.75f;

	[Export(PropertyHint.Range, "0,100,1")]
	public float HeroSeparationSpeed
	{ get; set; } = 24.0f;

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

	[ExportCategory("Passive Recovery")]
	[Export(PropertyHint.Range, "0,10,0.1")]
	public float TravelingRecoveryPercentPerSecond
	{ get; set; } = 2.5f;

	[Export]
	public TargetingService Targeting { get; set; } = null!;

	public MonsterActorController? CurrentTarget { get; private set; }

	public Vector2 FormationPosition => FormationAnchor.GlobalPosition + FormationOffset;

	public CombatHealthState Health { get; } = new();

	public MeleeEngagementSlotSet MeleeEngagementSlots { get; } =
		new();

	public void SetPartyMembers(
		IReadOnlyList<HeroActorController> partyMembers)
	{
		_partyMembers = partyMembers
			?? System.Array.Empty<HeroActorController>();
	}

	// Incapacitation Reset -- DEBUG ONLY
	public void DebugResetFromIncapacitation()
	{
		ReleaseMeleeEngagementSlot(CurrentTarget);
		Health.RestoreToMaximum();
		MeleeEngagementSlots.Clear();

		CurrentTarget = null;

		_attackCooldownRemaining = 0.0;
		_attackTimeRemaining = 0.0;
		_attackReleaseEmitted = false;
		_initialAttackPending = false;
		_movementAnimationGraceRemaining = 0.0;
		_abilityCooldowns.Reset();
		ClearAbilityCast();

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
		ClearAbilityCast();
		ReleaseMeleeEngagementSlot(CurrentTarget);
		MeleeEngagementSlots.Clear();

		if (GodotObject.IsInstanceValid(JourneyState))
		{
			JourneyState.StateChanged -=
				OnJourneyStateChanged;
		}
	}

	public override void _Process(double delta)
	{
		_abilityCooldowns.Update(delta);
		UpdatePassiveRecovery(delta);
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

			case HeroState.UsingAbility:

				UpdateAbility(delta);

				break;

			case HeroState.ReturningToFormation:

				UpdateReturnToFormation(delta);
				UpdateMovementPresentation(delta);

				break;

			case HeroState.Incapacitated:

				StopMovementAnimation();

				break;
		}

		ApplyHeroSeparation(delta);
	}

	private void UpdatePassiveRecovery(double delta)
	{
		if (JourneyState.CurrentState
				!= JourneyStateService.JourneyState.Traveling
			|| !Health.IsAlive
			|| Health.CurrentHealth >= Health.MaximumHealth)
		{
			return;
		}

		float recoveryPercent = Mathf.Max(
			TravelingRecoveryPercentPerSecond,
			0.0f);

		float requestedRecovery =
			Health.MaximumHealth
			* recoveryPercent
			/ 100.0f
			* (float)delta;

		Health.ApplyPassiveRecovery(requestedRecovery);
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

	private float GetMeleeSlotHorizontalDistance(
		MonsterActorController target)
	{
		float bodyClearanceDistance =
			GetBodyClearanceDistance(target);

		float adjustedDistance =
			GetRequiredAttackDistance(target)
			* Mathf.Max(
				MeleeSlotHorizontalSpacingMultiplier,
				0.0f);

		return Mathf.Max(
			bodyClearanceDistance,
			adjustedDistance);
	}

	private float GetMeleeSlotVerticalDistance(
		MonsterActorController target)
	{
		float scaledHeroRadius =
			Mathf.Max(0.0f, CombatProfile.CombatRadius)
			* CombatPresentationScale;

		float scaledTargetRadius =
			Mathf.Max(0.0f, target.CombatProfile.CombatRadius)
			* target.CombatPresentationScale;

		return Mathf.Max(
			CombatArrivalDistance * CombatPresentationScale,
			(scaledHeroRadius + scaledTargetRadius)
				* Mathf.Max(
					MeleeSlotVerticalSpacingMultiplier,
					0.0f));
	}

	private bool TryGetMeleeEngagementPosition(
		MonsterActorController target,
		out Vector2 engagementPosition)
	{
		engagementPosition = Vector2.Zero;

		if (!UsesMeleeEngagementSlots)
			return false;

		float horizontalDistance =
			GetMeleeSlotHorizontalDistance(target);

		float verticalDistance =
			GetMeleeSlotVerticalDistance(target);

		bool alreadyReserved =
			target.MeleeEngagementSlots.TryGetReservation(
				this,
				out MeleeEngagementSlot slot);

		if (!alreadyReserved
			&& !target.MeleeEngagementSlots.TryReserveClosest(
				this,
				target.GlobalPosition,
				horizontalDistance,
				verticalDistance,
				out slot))
		{
			return false;
		}

		engagementPosition =
			MeleeEngagementSlotSet.GetWorldPosition(
				slot,
				target.GlobalPosition,
				horizontalDistance,
				verticalDistance);

		if (!alreadyReserved)
		{
			DebugLog.Print(
				$"{Name} reserved {slot} melee slot on " +
				$"{target.Name}.");
		}

		return true;
	}

	private bool HasMeleeEngagementReservation(
		MonsterActorController target)
	{
		return UsesMeleeEngagementSlots
			&& target.MeleeEngagementSlots.TryGetReservation(
				this,
				out _);
	}

	private bool IsTargetWithinMeleeEngagementRange(
		MonsterActorController target)
	{
		float horizontalDistance =
			GetMeleeSlotHorizontalDistance(target);

		float verticalDistance =
			GetMeleeSlotVerticalDistance(target);

		float maximumDistance =
			new Vector2(
				horizontalDistance,
				verticalDistance).Length();

		float scaledTolerance =
			AttackRangeTolerance
			* CombatPresentationScale;

		return GlobalPosition.DistanceTo(target.GlobalPosition)
			<= maximumDistance + scaledTolerance;
	}

	private bool HasReachedMeleeEngagementPosition(
		MonsterActorController target,
		Vector2 engagementPosition)
	{
		if (GlobalPosition.DistanceTo(engagementPosition)
			<= CombatArrivalDistance)
		{
			return true;
		}

		// A slot can extend beyond the top or bottom of the legal ground
		// area. If the ground constraint stops the vertical movement, the
		// hero may still engage after reaching the reserved slot's side.
		return Mathf.Abs(
			GlobalPosition.X
			- engagementPosition.X)
			<= CombatArrivalDistance
			&& IsTargetWithinMeleeEngagementRange(target);
	}

	private void ReleaseMeleeEngagementSlot(
		MonsterActorController? target)
	{
		if (target is null
			|| !GodotObject.IsInstanceValid(target))
		{
			return;
		}

		target.MeleeEngagementSlots.Release(this);
	}

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
		float horizontalDistance = Mathf.Abs(horizontalDifference);
		float scaledTolerance =
			AttackRangeTolerance
			* CombatPresentationScale;

		if (horizontalDistance
			<= requiredCenterDistance + scaledTolerance)
		{
			return new Vector2(
				GlobalPosition.X,
				target.GlobalPosition.Y);
		}

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
		float requiredCenterDistance =
			GetRequiredAttackDistance(target);

		float horizontalDistance =
			Mathf.Abs( GlobalPosition.X - target.GlobalPosition.X);

		float scaledTolerance =
			AttackRangeTolerance
			* CombatPresentationScale;

		return horizontalDistance
			<= requiredCenterDistance + scaledTolerance;
	}

	private float GetScaledCombatRadius()
	{
		return Mathf.Max(
			0.0f,
			CombatProfile.CombatRadius)
			* CombatPresentationScale;
	}

	private float GetHeroSeparationVerticalLeash()
	{
		float verticalMultiplier = Mathf.Max(
			HeroSeparationVerticalSpacingMultiplier,
			0.0f);

		if (verticalMultiplier <= 0.0f)
			return 0.0f;

		float ownRadius = GetScaledCombatRadius();
		float maximumSpacing = 0.0f;

		foreach (HeroActorController partyMember
			in _partyMembers)
		{
			if (!IsValidSeparationPeer(partyMember))
				continue;

			float combinedRadius =
				ownRadius
				+ partyMember.GetScaledCombatRadius();

			maximumSpacing = Mathf.Max(
				maximumSpacing,
				combinedRadius * verticalMultiplier);
		}

		return maximumSpacing;
	}

	private float GetNonMeleeVerticalAlignmentTolerance()
	{
		return AttackRangeTolerance
			+ GetHeroSeparationVerticalLeash();
	}

	private float GetCombatSeparationAnchorY(
		MonsterActorController target)
	{
		if (UsesMeleeEngagementSlots
			&& target.MeleeEngagementSlots.TryGetReservation(
				this,
				out MeleeEngagementSlot slot))
		{
			return MeleeEngagementSlotSet.GetWorldPosition(
				slot,
				target.GlobalPosition,
				GetMeleeSlotHorizontalDistance(target),
				GetMeleeSlotVerticalDistance(target)).Y;
		}

		return target.GlobalPosition.Y;
	}

	private bool IsValidSeparationPeer(
		HeroActorController? partyMember)
	{
		return partyMember is not null
			&& partyMember != this
			&& GodotObject.IsInstanceValid(partyMember)
			&& partyMember.IsInsideTree()
			&& !partyMember.IsIncapacitated;
	}

	private void ApplyHeroSeparation(double delta)
	{
		if (JourneyState.CurrentState
				!= JourneyStateService.JourneyState.Encounter
			|| IsIncapacitated
			|| !Targeting.IsValidMonsterTarget(CurrentTarget))
		{
			return;
		}

		float horizontalMultiplier = Mathf.Max(
			HeroSeparationHorizontalRangeMultiplier,
			0.0f);

		float verticalMultiplier = Mathf.Max(
			HeroSeparationVerticalSpacingMultiplier,
			0.0f);

		float separationSpeed = Mathf.Max(
			HeroSeparationSpeed,
			0.0f)
			* CombatPresentationScale;

		if (horizontalMultiplier <= 0.0f
			|| verticalMultiplier <= 0.0f
			|| separationSpeed <= 0.0f)
		{
			return;
		}

		float ownRadius = GetScaledCombatRadius();
		float verticalPush = 0.0f;

		foreach (HeroActorController partyMember
			in _partyMembers)
		{
			if (!IsValidSeparationPeer(partyMember))
				continue;

			float combinedRadius =
				ownRadius
				+ partyMember.GetScaledCombatRadius();

			float horizontalRange =
				combinedRadius * horizontalMultiplier;

			float desiredVerticalSpacing =
				combinedRadius * verticalMultiplier;

			if (horizontalRange <= 0.0f
				|| desiredVerticalSpacing <= 0.0f)
			{
				continue;
			}

			float horizontalDistance = Mathf.Abs(
				GlobalPosition.X
				- partyMember.GlobalPosition.X);

			if (horizontalDistance >= horizontalRange)
				continue;

			float verticalDifference =
				GlobalPosition.Y
				- partyMember.GlobalPosition.Y;

			float verticalDistance = Mathf.Abs(
				verticalDifference);

			if (verticalDistance >= desiredVerticalSpacing)
				continue;

			float direction;

			if (!Mathf.IsZeroApprox(verticalDifference))
			{
				direction = Mathf.Sign(verticalDifference);
			}
			else
			{
				direction = GetInstanceId()
					< partyMember.GetInstanceId()
						? -1.0f
						: 1.0f;
			}

			float horizontalWeight =
				1.0f
				- horizontalDistance / horizontalRange;

			float verticalWeight =
				1.0f
				- verticalDistance
					/ desiredVerticalSpacing;

			verticalPush +=
				direction
				* horizontalWeight
				* verticalWeight;
		}

		verticalPush = Mathf.Clamp(
			verticalPush,
			-1.0f,
			1.0f);

		if (Mathf.IsZeroApprox(verticalPush))
			return;

		float verticalLeash =
			GetHeroSeparationVerticalLeash();

		if (verticalLeash <= 0.0f)
			return;

		float anchorY = GetCombatSeparationAnchorY(
			CurrentTarget!);

		float minimumY = anchorY - verticalLeash;
		float maximumY = anchorY + verticalLeash;

		if ((GlobalPosition.Y <= minimumY
				&& verticalPush < 0.0f)
			|| (GlobalPosition.Y >= maximumY
				&& verticalPush > 0.0f))
		{
			return;
		}

		float nextY =
			GlobalPosition.Y
			+ verticalPush
				* separationSpeed
				* (float)delta;

		if (GlobalPosition.Y >= minimumY
			&& GlobalPosition.Y <= maximumY)
		{
			nextY = Mathf.Clamp(
				nextY,
				minimumY,
				maximumY);
		}

		GlobalPosition = new Vector2(
			GlobalPosition.X,
			nextY);
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

		if (TryGetMeleeEngagementPosition(
			target,
			out Vector2 meleeEngagementPosition))
		{
			if (HasReachedMeleeEngagementPosition(
				target,
				meleeEngagementPosition))
			{
				PrepareToAttack();
				return;
			}

			Vector2 previousMeleePosition = GlobalPosition;

			float meleeMovementDistance =
				CombatProfile.MoveSpeed * (float)delta;

			GlobalPosition = GlobalPosition.MoveToward(
				meleeEngagementPosition,
				meleeMovementDistance);

			_movedThisFrame =
				!GlobalPosition.IsEqualApprox(
					previousMeleePosition);

			if (!HasReachedMeleeEngagementPosition(
				target,
				meleeEngagementPosition))
			{
				return;
			}

			PrepareToAttack();
			return;
		}

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

		MonsterActorController target = CurrentTarget!;

		bool hasMeleeReservation =
			HasMeleeEngagementReservation(target);

		bool targetMovedOutOfRange =
			hasMeleeReservation
				? !IsTargetWithinMeleeEngagementRange(target)
				: !IsTargetWithinAttackRange(target);

		bool targetMovedToAnotherY =
			!hasMeleeReservation
			&& Mathf.Abs(
				GlobalPosition.Y
				- target.GlobalPosition.Y)
				> GetNonMeleeVerticalAlignmentTolerance();

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

	private void UpdateAbility(double delta)
	{
		if (_activeAbility is null
			|| !GodotObject.IsInstanceValid(_activeAbility)
			|| _activeAbilityTarget is null
			|| !GodotObject.IsInstanceValid(_activeAbilityTarget)
			|| !IsValidAbilityTarget(
				_activeAbility,
				_activeAbilityTarget))
		{
			ClearAbilityCast();
			ResumeAfterAbility();
			return;
		}

		AbilityDefinition ability = _activeAbility;
		HeroActorController target = _activeAbilityTarget;

		_abilityCastTimeRemaining -= delta;

		if (_abilityCastTimeRemaining > 0.0)
			return;

		if (!TryStartAbilityCooldown(ability))
		{
			ClearAbilityCast();
			ResumeAfterAbility();
			return;
		}

		DebugLog.Print(
			$"{Name} released ability " +
			$"'{ability.DisplayName}' on {target.Name}.");

		EmitSignal(
			SignalName.AbilityReleased,
			this,
			target,
			ability);

		if (_state != HeroState.UsingAbility)
		{
			ClearAbilityCast();
			return;
		}

		ClearAbilityCast();
		ResumeAfterAbility();
	}

	private void ResumeAfterAbility()
	{
		if (IsIncapacitated)
			return;

		_attackCooldownRemaining = 0.0;

		if (JourneyState.CurrentState
			== JourneyStateService.JourneyState.Encounter
			&& Targeting.IsValidMonsterTarget(CurrentTarget))
		{
			MonsterActorController target = CurrentTarget!;

			bool hasMeleeReservation =
				HasMeleeEngagementReservation(target);

			bool targetInRange =
				hasMeleeReservation
					? IsTargetWithinMeleeEngagementRange(target)
					: IsTargetWithinAttackRange(target);

			_state = targetInRange
				? HeroState.WaitingToAttack
				: HeroState.ApproachingTarget;

			return;
		}

		bool isAtFormation =
			GlobalPosition.DistanceTo(FormationPosition)
			<= CombatArrivalDistance;

		_state = isAtFormation
			? HeroState.InFormation
			: HeroState.ReturningToFormation;
	}

	private void ClearAbilityCast()
	{
		_activeAbility = null;
		_activeAbilityTarget = null;
		_abilityCastTimeRemaining = 0.0;
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

		MonsterActorController target = CurrentTarget!;

		bool hasMeleeReservation =
			HasMeleeEngagementReservation(target);

		bool targetStillInRange =
			hasMeleeReservation
				? IsTargetWithinMeleeEngagementRange(target)
				: IsTargetWithinAttackRange(target);

		bool targetStillAligned =
			hasMeleeReservation
			|| Mathf.Abs(
				GlobalPosition.Y
				- target.GlobalPosition.Y)
				<= GetNonMeleeVerticalAlignmentTolerance();

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
		ReleaseMeleeEngagementSlot(CurrentTarget);
		MeleeEngagementSlots.Clear();
		CurrentTarget = null;

		_attackCooldownRemaining = 0.0;
		_attackTimeRemaining = 0.0;
		_attackReleaseEmitted = false;
		_initialAttackPending = false;
		_movementAnimationGraceRemaining = 0.0;
		ClearAbilityCast();

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
		if (state
			!= JourneyStateService.JourneyState.Encounter)
		{
			ReleaseMeleeEngagementSlot(CurrentTarget);
		}

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

		if (IsUsingAbility)
			return;

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

		ReleaseMeleeEngagementSlot(previousTarget);

		if (CurrentTarget is null)
		{
			DebugLog.Print(
				$"{Name} has no valid monster target.");

			return;
		}

		_initialAttackPending = true;
		TryGetMeleeEngagementPosition(
			CurrentTarget!,
			out _);
		
		DebugLog.Print(
			$"{Name} targeted {CurrentTarget.Name} " +
			$"at X={CurrentTarget.GlobalPosition.X}.");

		if (_state != HeroState.UsingAbility
			&& JourneyState.CurrentState == JourneyStateService.JourneyState.Encounter)
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

		ReleaseMeleeEngagementSlot(CurrentTarget);
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
