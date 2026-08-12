using Godot;
using System.Collections.Generic;

public partial class HeroActorController : Node2D, ICombatStatusEffectOwner
{
	[Signal]
	public delegate void AttackReleasedEventHandler(
	HeroActorController attacker,
	MonsterActorController target);

	[Signal]
	public delegate void AbilityReleasedEventHandler(
	HeroActorController caster,
	Node2D target,
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
	private HeroResourceDefinition? _debugResourceDefinition;
	private readonly List<AbilityDefinition> _abilities = new();
	private readonly HeroAbilityCooldownState
		_abilityCooldowns = new();
	private AbilityDefinition? _activeAbility;
	private Node2D? _activeAbilityTarget;
	private double _abilityCastTimeRemaining;
	private double _targetCommitmentRemainingSeconds;
	private double _targetReassessmentRemainingSeconds;
	private bool _isPartySupportActive;
	private HeroActorController? _partySupportAlly;
	private IReadOnlyList<HeroActorController> _partyMembers =
		System.Array.Empty<HeroActorController>();
	private System.Func<HeroActorController, bool>?
		_tryUsePriorityAbilityBeforeBasicAttack;

	public HeroCombatProfile CombatProfile { get; } = new();
	public float CombatPresentationScale { get; private set; } = 1.0f;
	public bool IsIncapacitated => _state == HeroState.Incapacitated;
	public HeroCombatStance CombatStance =>
		CombatProfile.CombatStance;
	public double TargetCommitmentRemainingSeconds =>
		_targetCommitmentRemainingSeconds;
	public double TargetReassessmentRemainingSeconds =>
		_targetReassessmentRemainingSeconds;
	public bool IsPartySupportActive =>
		_isPartySupportActive;
	public HeroActorController? PartySupportAlly =>
		_partySupportAlly;
	private bool UsesMeleeEngagementSlots =>
		HasCombatTag(HeroCombatTag.Melee);

	public HeroCombatStanceProfile ActiveStanceProfile =>
		Targeting.GetStanceProfile(CombatStance);

	/// <summary>
	/// Updates combat presentation scale and applies the new value to the owning system.
	/// Uses the supplied arguments and current node state; any result is applied through side effects, events, or stored fields.
	/// </summary>
	public void SetCombatPresentationScale(float scale)
	{
		CombatPresentationScale =
			Mathf.Max(scale, 0.01f);
	}

	[ExportCategory("Combat Identity")]
	/// <summary>
	/// Controls combat tag mask.
	/// For example, selecting a different value changes which combat tag mask behavior or content the owning system uses.
	/// </summary>
	[Export(PropertyHint.Flags, "Melee,Ranged,Caster,Healer,Tank,Summoner,Armored")]
	public int CombatTagMask { get; set; } =
		(int)HeroCombatTag.Melee;

	public HeroCombatTag CombatTags =>
		(HeroCombatTag)CombatTagMask;

	/// <summary>
	/// Performs the has combat tag operation for Hero Actor Controller.
	/// Uses the supplied arguments and current state and returns the resulting bool to the caller.
	/// </summary>
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

	public HeroResourceState Resource { get; } = new();
	public HeroComboPointState ComboPoints { get; } = new();

	/// <summary>
	/// Owns temporary combat status effects currently active on this hero.
	/// The state tracks identity and duration only; individual effects decide
	/// their gameplay behavior in later checkpoints.
	/// </summary>
	public CombatStatusEffectState StatusEffects { get; } = new();
	public bool UsesComboPoints =>
		Definition?.ClassDefinition.ContentId
			.Equals(
				"class.core.rogue",
				System.StringComparison.OrdinalIgnoreCase)
			== true;

	/// <summary>
	/// Adds one combo point for a rogue after a basic attack deals real damage.
	/// Non-rogues and zero-damage outcomes cannot change combo state.
	/// </summary>
	public bool TryAddComboPointFromDamage(float appliedDamage)
	{
		return UsesComboPoints
			&& appliedDamage > 0.0f
			&& ComboPoints.TryAddPoint();
	}

	/// <summary>
	/// Temporarily assigns a standard 100-point resource pool for UI testing.
	/// It regenerates ten points every two seconds and does not alter class data.
	/// </summary>
	public void DebugConfigureResource(HeroResourceType resourceType)
	{
		if (resourceType == HeroResourceType.None)
		{
			_debugResourceDefinition = null;
			Resource.Configure(
				Definition?.ClassDefinition.ResourceDefinition);
			return;
		}

		_debugResourceDefinition = new HeroResourceDefinition
		{
			ResourceType = resourceType,
			MaximumAmount = 100.0f,
			StartFull = true,
			RegenerationAmount = 10.0f,
			RegenerationIntervalSeconds = 2.0f
		};

		Resource.Configure(_debugResourceDefinition);
	}

	/// <summary>
	/// Spends resource through the normal atomic runtime API for UI testing.
	/// Returns false without changing the pool when the cost is unaffordable.
	/// </summary>
	public bool DebugTrySpendResource(float amount)
	{
		return Resource.TrySpend(amount);
	}

	/// <summary>
	/// Retrieves ability cooldown remaining from the current game state.
	/// Uses the supplied arguments and current state and returns the resulting double to the caller.
	/// </summary>
	public double GetAbilityCooldownRemaining(
		string abilityContentId)
	{
		return _abilityCooldowns.GetRemainingSeconds(
			abilityContentId);
	}

	/// <summary>
	/// Performs the is ability ready operation for Hero Actor Controller.
	/// Uses the supplied arguments and current state and returns the resulting bool to the caller.
	/// </summary>
	public bool IsAbilityReady(string abilityContentId)
	{
		return _abilityCooldowns.IsReady(
			abilityContentId);
	}

	/// <summary>
	/// Returns whether this hero can pay the ability's configured resource
	/// cost. Zero-cost abilities remain usable by heroes with no resource.
	/// </summary>
	public bool CanAffordAbility(AbilityDefinition ability)
	{
		return GodotObject.IsInstanceValid(ability)
			&& (ability.ResourceCost <= 0.0f
				|| (Resource.HasResource
					&& Resource.CurrentAmount
						>= ability.ResourceCost));
	}

	/// <summary>
	/// Returns whether this hero currently has enough combo points for the
	/// ability's authored combo-point cost. Abilities with no combo cost always
	/// pass this check.
	/// </summary>
	public bool HasRequiredComboPoints(AbilityDefinition ability)
	{
		return GodotObject.IsInstanceValid(ability)
			&& ComboPoints.CanSpend(
				Mathf.Max(ability.ComboPointCost, 0));
	}

	/// <summary>
	/// Commits an ability after its target and cast/windup have already been
	/// validated. Commit is the authoritative point where resource is spent and
	/// cooldown begins. If either side cannot complete, neither cost is kept.
	/// </summary>
	public bool TryCommitAbility(AbilityDefinition ability)
	{
		if (!GodotObject.IsInstanceValid(ability)
			|| !TryGetAbility(ability.ContentId, out _)
			|| !IsAbilityReady(ability.ContentId)
			|| !CanAffordAbility(ability)
			|| !HasRequiredComboPoints(ability))
		{
			return false;
		}

		float resourceCost = Mathf.Max(ability.ResourceCost, 0.0f);
		int comboPointCost = Mathf.Max(ability.ComboPointCost, 0);

		if (resourceCost > 0.0f
			&& !Resource.TrySpend(resourceCost))
		{
			return false;
		}

		if (comboPointCost > 0
			&& !ComboPoints.TrySpend(comboPointCost))
		{
			if (resourceCost > 0.0f)
				Resource.Restore(resourceCost);

			return false;
		}

		if (!_abilityCooldowns.TryStart(ability))
		{
			if (resourceCost > 0.0f)
				Resource.Restore(resourceCost);

			if (comboPointCost > 0)
				ComboPoints.Restore(comboPointCost);

			return false;
		}

		DebugLog.Print(
			$"{Name} committed ability " +
			$"'{ability.DisplayName}'. " +
			$"ResourceCost={resourceCost:0.##}; " +
			$"ComboPointCost={comboPointCost}; " +
			$"Cooldown={ability.CooldownSeconds:0.##}s.",
			DebugLogCategory.Ability);

		return true;
	}

	public bool IsUsingAbility =>
		_activeAbility is not null;

	public bool IsPerformingBasicAttack =>
		_state == HeroState.Attacking;

	/// <summary>
	/// Injects CombatController's automatic-ability decision before this hero
	/// begins a new basic attack. The actor owns execution; CombatController
	/// remains the authority for whether an ability's additional auto-use rules
	/// are currently satisfied.
	/// </summary>
	public void SetAutomaticAbilityPriorityResolver(
		System.Func<HeroActorController, bool>? resolver)
	{
		_tryUsePriorityAbilityBeforeBasicAttack = resolver;
	}

	/// <summary>
	/// Attempts to begin ability without throwing when the operation cannot be completed.
	/// Uses the supplied arguments and current state and returns the resulting bool to the caller.
	/// </summary>
	public bool TryBeginAbility(
		AbilityDefinition ability,
		Node2D target)
	{
		if (!GodotObject.IsInstanceValid(ability)
			|| !GodotObject.IsInstanceValid(target)
			|| IsIncapacitated
			|| !Health.IsAlive
			|| IsUsingAbility
			|| IsPerformingBasicAttack
			|| !TryGetAbility(ability.ContentId, out _)
			|| !IsAbilityReady(ability.ContentId)
			|| !CanAffordAbility(ability)
			|| !HasRequiredComboPoints(ability)
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

	/// <summary>
	/// Performs the configure operation for Hero Actor Controller.
	/// Uses the supplied arguments and current node state; any result is applied through side effects, events, or stored fields.
	/// </summary>
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
		ComboPoints.Reset();
		StatusEffects.Clear();
		CombatTagMask = definition.CombatTagMask;
		CombatProfile.CombatStance =
			definition.StartingCombatStance;
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
		_debugResourceDefinition = null;
		Resource.Configure(
			definition.ClassDefinition.ResourceDefinition);

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

	/// <summary>
	/// Updates combat stance and applies the new value to the owning system.
	/// Uses the supplied arguments and current node state; any result is applied through side effects, events, or stored fields.
	/// </summary>
	public void SetCombatStance(HeroCombatStance stance)
	{
		if (!System.Enum.IsDefined(
			typeof(HeroCombatStance),
			stance))
		{
			throw new System.ArgumentOutOfRangeException(
				nameof(stance),
				stance,
				"Unknown hero combat stance.");
		}

		if (CombatStance == stance)
			return;

		CombatProfile.CombatStance = stance;
		ResetTargetDecisionTimers();

		if (ActiveStanceProfile.LogTargetingDecisions)
		{
			DebugLog.Print(
				$"{Name} changed combat stance to " +
				$"{CombatStance}.");
		}
	}

	/// <summary>
	/// Attempts to get ability without throwing when the operation cannot be completed.
	/// Uses the supplied arguments and current state and returns the resulting bool to the caller.
	/// </summary>
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

	/// <summary>
	/// Performs the is valid ability target operation for Hero Actor Controller.
	/// Uses the supplied arguments and current state and returns the resulting bool to the caller.
	/// </summary>
	private bool IsValidAbilityTarget(
		AbilityDefinition ability,
		Node2D target)
	{
		return ability.TargetMode switch
		{
			AbilityTargetMode.CurrentTarget =>
				target is MonsterActorController currentMonster
				&& currentMonster == CurrentTarget
				&& Targeting.IsValidMonsterTarget(currentMonster)
				&& IsWithinAbilityRange(ability, currentMonster),

			AbilityTargetMode.Self =>
				target == this,

			AbilityTargetMode.Ally =>
				target is HeroActorController ally
				&& IsLivingPartyMember(ally)
				&& IsWithinAbilityRange(ability, ally),

			AbilityTargetMode.Monster =>
				target is MonsterActorController monster
				&& Targeting.IsValidMonsterTarget(monster)
				&& IsWithinAbilityRange(ability, monster),

			AbilityTargetMode.AreaOfEffect =>
				IsValidAreaAnchor(ability, target),

			_ => false
		};
	}

	/// <summary>
	/// Validates the anchor point for an AOE without resolving the effect.
	/// Self-centered areas anchor on the caster. Target-centered areas anchor
	/// on a living actor from the configured target group.
	/// </summary>
	private bool IsValidAreaAnchor(
		AbilityDefinition ability,
		Node2D target)
	{
		if (ability.AreaOrigin == AbilityAreaOrigin.Self)
			return target == this;

		return ability.AreaTargetGroup switch
		{
			AbilityTargetGroup.Allies =>
				target is HeroActorController ally
				&& IsLivingPartyMember(ally)
				&& IsWithinAbilityRange(ability, ally),

			AbilityTargetGroup.Enemies =>
				target is MonsterActorController monster
				&& Targeting.IsValidMonsterTarget(monster)
				&& IsWithinAbilityRange(ability, monster),

			AbilityTargetGroup.Everyone =>
				(target is HeroActorController hero
					&& IsLivingPartyMember(hero)
					&& IsWithinAbilityRange(ability, hero))
				|| (target is MonsterActorController enemy
					&& Targeting.IsValidMonsterTarget(enemy)
					&& IsWithinAbilityRange(ability, enemy)),

			_ => false
		};
	}

	/// <summary>
	/// Determines whether a resolved ability target is currently within the
	/// ability's authored range semantics. Fixed range uses logical gameplay
	/// distance. BasicAttackRange delegates to the same spatial rules the hero
	/// uses for normal attacks so melee finishers do not disagree with combat
	/// spacing or engagement slots.
	/// </summary>
	public bool IsWithinAbilityRange(
		AbilityDefinition ability,
		Node2D target)
	{
		if (ability is null
			|| target is null
			|| !GodotObject.IsInstanceValid(target))
		{
			return false;
		}

		if (target == this)
			return true;

		return ability.RangeMode switch
		{
			AbilityRangeMode.Fixed =>
				IsWithinFixedAbilityRange(ability, target),

			AbilityRangeMode.BasicAttackRange =>
				target is MonsterActorController monster
				&& IsWithinBasicAttackRange(monster),

			_ => false
		};
	}

	private bool IsWithinFixedAbilityRange(
		AbilityDefinition ability,
		Node2D target)
	{
		float range = Mathf.Max(ability.Range, 0.0f);

		if (range <= 0.0f)
			return true;

		return GlobalPosition.DistanceSquaredTo(
			target.GlobalPosition) <= range * range;
	}

	/// <summary>
	/// Reports whether this hero is in a legal normal-attack position against
	/// the supplied monster. Melee heroes with an engagement reservation use
	/// the reservation-aware distance; other attacks use the same horizontal
	/// reach and vertical alignment rules as the normal attack state machine.
	/// </summary>
	public bool IsWithinBasicAttackRange(
		MonsterActorController target)
	{
		if (!Targeting.IsValidMonsterTarget(target))
			return false;

		if (HasMeleeEngagementReservation(target))
		{
			return IsTargetWithinMeleeEngagementRange(target);
		}

		bool targetWithinRange =
			IsTargetWithinAttackRange(target);

		bool verticallyAligned =
			Mathf.Abs(
				GlobalPosition.Y
				- target.GlobalPosition.Y)
			<= GetNonMeleeVerticalAlignmentTolerance();

		return targetWithinRange && verticallyAligned;
	}

	/// <summary>
	/// Performs the is living party member operation for Hero Actor Controller.
	/// Uses the supplied arguments and current state and returns the resulting bool to the caller.
	/// </summary>
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
	/// <summary>
	/// Inspector reference used by this component for its formation anchor dependency.
	/// Assign the matching node or resource from the scene; leaving it empty prevents that connection from working.
	/// </summary>
	[Export]
	public Node2D FormationAnchor { get; set; } = null!;

	/// <summary>
	/// Controls formation offset, measured as pixels.
	/// For example, changing 2 to 4 doubles this setting's configured contribution to the system.
	/// </summary>
	[Export]
	public Vector2 FormationOffset { get; set; } = Vector2.Zero;
	
	[ExportCategory("Dependencies")]
	/// <summary>
	/// Inspector reference used by this component for its journey state dependency.
	/// Assign the matching node or resource from the scene; leaving it empty prevents that connection from working.
	/// </summary>
	[Export]
	public JourneyStateService JourneyState { get; set; } = null!;

	[ExportCategory("Visuals")]
	/// <summary>
	/// Inspector reference used by this component for its visual root dependency.
	/// Assign the matching node or resource from the scene; leaving it empty prevents that connection from working.
	/// </summary>
	[Export]
	public Node2D VisualRoot { get; set; } = null!;

	/// <summary>
	/// Controls body bounds.
	/// For example, selecting a different value changes which body bounds behavior or content the owning system uses.
	/// </summary>
	[Export]
	public BodyBounds2D BodyBounds { get; set; } = null!;
	
	/// <summary>
	/// Inspector reference used by this component for its projectile origin dependency.
	/// Assign the matching node or resource from the scene; leaving it empty prevents that connection from working.
	/// </summary>
	[Export]
	public Marker2D ProjectileOrigin { get; set; } = null!;

	/// <summary>
	/// Inspector reference used by this component for its ability cooldown indicator dependency.
	/// Assign the matching node or resource from the scene; leaving it empty prevents that connection from working.
	/// </summary>
	[Export]
	public HeroAbilityCooldownIndicatorController
	AbilityCooldownIndicatorSlot1 { get; set; } = null!;

	[Export]
	public HeroAbilityCooldownIndicatorController
	AbilityCooldownIndicatorSlot2 { get; set; } = null!;

	/// <summary>
	/// Inspector reference to the reusable Mana, Energy, or Rage bar beneath
	/// this hero's health bar. Assign VisualRoot/HeroResourceBar.
	/// </summary>
	[Export]
	public HeroResourceBarController ResourceBar
	{ get; set; } = null!;

	/// <summary>
	/// Five-square rogue combo display beneath the resource bar. Assign the
	/// instantiated VisualRoot/HeroComboPointDisplay scene.
	/// </summary>
	[Export]
	public HeroComboPointDisplayController ComboPointDisplay
	{ get; set; } = null!;

	[ExportCategory("Travel Animation")]
	/// <summary>
	/// Controls bob height, measured as pixels.
	/// For example, changing 4 to 8 doubles the configured bob height.
	/// </summary>
	[Export(PropertyHint.Range, "0,20,0.5")]
	public float BobHeight { get; set; } = 4.0f;

	/// <summary>
	/// Controls bob speed, measured as pixels per second.
	/// For example, changing 7 to 14 makes the affected movement or animation run about twice as fast.
	/// </summary>
	[Export(PropertyHint.Range, "0,20,0.1")]
	public float BobSpeed { get; set; } = 7.0f;

	/// <summary>
	/// Controls bob phase offset, measured as pixels.
	/// For example, changing 0 to 1 doubles this setting's configured contribution to the system.
	/// </summary>
	[Export(PropertyHint.Range, "0,6.28,0.01")]
	public float BobPhaseOffset { get; set; } = 0.0f;

	/// <summary>
	/// Controls movement animation grace time, measured as seconds.
	/// For example, changing 0.15 to 0.3 doubles this setting's configured contribution to the system.
	/// </summary>
	[Export(PropertyHint.Range, "0,0.5,0.01")]
	public float MovementAnimationGraceTime { get; set; } = 0.15f;

	[ExportCategory("Temporary Combat Values")]
	/// <summary>
	/// Controls temporary maximum health, measured as health points.
	/// For example, changing 100 to 200 doubles the configured temporary maximum health.
	/// </summary>
	[Export(PropertyHint.Range, "1,100000,1")]
	public float TemporaryMaximumHealth { get; set; } = 100.0f;

	/// <summary>
	/// Controls temporary attack damage, measured as damage points.
	/// For example, changing 20 to 40 doubles the configured temporary attack damage.
	/// </summary>
	[Export(PropertyHint.Range, "0,100000,1")]
	public float TemporaryAttackDamage { get; set; } = 20.0f;

	[ExportCategory("Temporary Combat Movement")]
	/// <summary>
	/// Controls combat move speed, measured as pixels per second.
	/// For example, changing 140 to 280 makes the affected movement or animation run about twice as fast.
	/// </summary>
	[Export(PropertyHint.Range, "0,500,1")]
	public float CombatMoveSpeed { get; set; } = 140.0f;

	/// <summary>
	/// Controls temporary attack range, measured as pixels.
	/// For example, changing 28 to 56 doubles the configured temporary attack range.
	/// </summary>
	[Export(PropertyHint.Range, "0,400,1")]
	public float TemporaryAttackRange { get; set; } = 28.0f;

	/// <summary>
	/// Controls combat arrival distance, measured as pixels.
	/// For example, changing 1 to 2 doubles the configured combat arrival distance.
	/// </summary>
	[Export(PropertyHint.Range, "0.1,20,0.1")]
	public float CombatArrivalDistance { get; set; } = 1.0f;

	/// <summary>
	/// Controls attack range tolerance, measured as pixels.
	/// For example, changing 3 to 6 doubles the configured attack range tolerance.
	/// </summary>
	[Export(PropertyHint.Range, "0.1,20,0.1")]
	public float AttackRangeTolerance { get; set; } = 3.0f;
	
	/// <summary>
	/// Controls facing dead zone.
	/// For example, changing 1 to 2 doubles this setting's configured contribution to the system.
	/// </summary>
	[Export(PropertyHint.Range, "0,10,0.1")]
	public float FacingDeadZone { get; set; } = 1.0f;

	[ExportCategory("Melee Engagement Slots")]
	/// <summary>
	/// Controls melee slot horizontal spacing multiplier, measured as pixels.
	/// For example, changing 1 to 2 doubles this setting's configured contribution to the system.
	/// </summary>
	[Export(PropertyHint.Range, "0.5,2,0.05")]
	public float MeleeSlotHorizontalSpacingMultiplier
	{ get; set; } = 1.0f;

	/// <summary>
	/// Controls melee slot vertical spacing multiplier, measured as pixels.
	/// For example, changing 0.75 to 1.5 doubles this setting's configured contribution to the system.
	/// </summary>
	[Export(PropertyHint.Range, "0.25,2,0.05")]
	public float MeleeSlotVerticalSpacingMultiplier
	{ get; set; } = 0.75f;

	[ExportCategory("Hero Separation")]
	/// <summary>
	/// Controls hero separation horizontal range multiplier, measured as pixels.
	/// For example, changing 1 to 2 doubles the configured hero separation horizontal range multiplier.
	/// </summary>
	[Export(PropertyHint.Range, "0,3,0.05")]
	public float HeroSeparationHorizontalRangeMultiplier
	{ get; set; } = 1.0f;

	/// <summary>
	/// Controls hero separation vertical spacing multiplier, measured as pixels.
	/// For example, changing 0.75 to 1.5 doubles this setting's configured contribution to the system.
	/// </summary>
	[Export(PropertyHint.Range, "0,3,0.05")]
	public float HeroSeparationVerticalSpacingMultiplier
	{ get; set; } = 0.75f;

	/// <summary>
	/// Controls hero separation speed, measured as pixels per second.
	/// For example, changing 24 to 48 makes the affected movement or animation run about twice as fast.
	/// </summary>
	[Export(PropertyHint.Range, "0,100,1")]
	public float HeroSeparationSpeed
	{ get; set; } = 24.0f;

	[ExportCategory("Temporary Attack Cycle")]
	/// <summary>
	/// Controls temporary attack interval, measured as seconds.
	/// For example, changing 1.5 to 3 makes the affected action wait twice as long between uses.
	/// </summary>
	[Export(PropertyHint.Range, "0.1,10,0.1")]
	public float TemporaryAttackInterval { get; set; } = 1.5f;

	/// <summary>
	/// Controls temporary attack duration, measured as seconds.
	/// For example, changing 0.3 to 0.6 doubles this setting's configured contribution to the system.
	/// </summary>
	[Export(PropertyHint.Range, "0.05,2,0.05")]
	public float TemporaryAttackDuration { get; set; } = 0.3f;

	/// <summary>
	/// Controls temporary attack lunge distance, measured as pixels.
	/// For example, changing 8 to 16 doubles the configured temporary attack lunge distance.
	/// </summary>
	[Export(PropertyHint.Range, "0,30,0.5")]
	public float TemporaryAttackLungeDistance { get; set; } = 8.0f;

	/// <summary>
	/// Controls temporary attack release point.
	/// For example, changing 0.5 to 1 doubles this setting's configured contribution to the system.
	/// </summary>
	[Export(PropertyHint.Range, "0,1,0.05")]
	public float TemporaryAttackReleasePoint { get; set; } = 0.5f;
	
	/// <summary>
	/// Controls temporary attack delivery.
	/// For example, selecting a different value changes which temporary attack delivery behavior or content the owning system uses.
	/// </summary>
	[Export]
	public AttackDeliveryMode TemporaryAttackDelivery { get; set; }
	= AttackDeliveryMode.ImmediateImpact;

	[ExportCategory("Passive Recovery")]
	/// <summary>
	/// Controls traveling recovery percent per second, measured as a ratio or multiplier.
	/// For example, changing 2.5 to 5 doubles this setting's configured contribution to the system.
	/// </summary>
	[Export(PropertyHint.Range, "0,10,0.1")]
	public float TravelingRecoveryPercentPerSecond
	{ get; set; } = 2.5f;

	/// <summary>
	/// Inspector reference used by this component for its targeting dependency.
	/// Assign the matching node or resource from the scene; leaving it empty prevents that connection from working.
	/// </summary>
	[Export]
	public TargetingService Targeting { get; set; } = null!;

	public MonsterActorController? CurrentTarget { get; private set; }

	public Vector2 FormationPosition => FormationAnchor.GlobalPosition + FormationOffset;

	public CombatHealthState Health { get; } = new();

	public MeleeEngagementSlotSet MeleeEngagementSlots { get; } =
		new();

	/// <summary>
	/// Updates party members and applies the new value to the owning system.
	/// Uses the supplied arguments and current node state; any result is applied through side effects, events, or stored fields.
	/// </summary>
	public void SetPartyMembers(
		IReadOnlyList<HeroActorController> partyMembers)
	{
		_partyMembers = partyMembers
			?? System.Array.Empty<HeroActorController>();
	}

	/// <summary>
	/// Performs the revive from incapacitation operation for Hero Actor Controller.
	/// Uses the current node and service state; any result is applied through side effects, events, or stored fields.
	/// </summary>
	public void ReviveFromIncapacitation()
	{
		ReleaseMeleeEngagementSlot(CurrentTarget);
		Health.RestoreToMaximum();
		Resource.RestoreToMaximum();
		MeleeEngagementSlots.Clear();

		CurrentTarget = null;
		ResetTargetDecisionTimers();

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
			$"{Name} revived with " +
			$"{Health.CurrentHealth}/" +
			$"{Health.MaximumHealth} health.");
	}

	// Incapacitation Reset -- DEBUG ONLY
	/// <summary>
	/// Performs the debug reset from incapacitation operation for Hero Actor Controller.
	/// Uses the current node and service state; any result is applied through side effects, events, or stored fields.
	/// </summary>
	public void DebugResetFromIncapacitation()
	{
		ReviveFromIncapacitation();
	}
	
	/// <summary>
	/// Performs the resume combat after debug reset operation for Hero Actor Controller.
	/// Uses the current node and service state; any result is applied through side effects, events, or stored fields.
	/// </summary>
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

	/// <summary>
	/// Runs Godot setup for Hero Actor Controller when the node enters the scene tree.
	/// Uses the current node and service state; any result is applied through side effects, events, or stored fields.
	/// </summary>
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
		ResourceBar.Bind(Resource);
		ComboPointDisplay.Bind(ComboPoints, UsesComboPoints);
		AbilityCooldownIndicatorSlot1.Bind(this, 0);
		AbilityCooldownIndicatorSlot2.Bind(this, 1);
		ApplyJourneyState(JourneyState.CurrentState);
		SnapToFormation();

		DebugLog.Print(
			$"HeroActor initialized at formation position " +
			$"{FormationPosition}." +
			$"{Name} initialized with " +
			$"{Health.CurrentHealth}/" +
			$"{Health.MaximumHealth} health. " +
			$"Combat tags={CombatTags}. " +
			$"Stance={CombatStance}.");
	}
	
	public FacingDirection Facing { get; private set; }
	= FacingDirection.Left;

	/// <summary>
	/// Cleans up Hero Actor Controller when the node leaves the scene tree.
	/// Uses the current node and service state; any result is applied through side effects, events, or stored fields.
	/// </summary>
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

	/// <summary>
	/// Updates Hero Actor Controller every rendered frame using the supplied frame delta.
	/// Uses the supplied arguments and current node state; any result is applied through side effects, events, or stored fields.
	/// </summary>
	public override void _Process(double delta)
	{
		StatusEffects.Update(delta);
		_abilityCooldowns.Update(delta);
		Resource.Update(
			delta,
			_debugResourceDefinition
				?? Definition?.ClassDefinition.ResourceDefinition);
		UpdatePassiveRecovery(delta);
		UpdateTargetDecisionTimers(delta);
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

	/// <summary>
	/// Recalculates passive recovery from the latest runtime state.
	/// Uses the supplied arguments and current node state; any result is applied through side effects, events, or stored fields.
	/// </summary>
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

	/// <summary>
	/// Recalculates target decision timers from the latest runtime state.
	/// Uses the supplied arguments and current node state; any result is applied through side effects, events, or stored fields.
	/// </summary>
	private void UpdateTargetDecisionTimers(double delta)
	{
		if (JourneyState.CurrentState
				!= JourneyStateService.JourneyState.Encounter
			|| !Targeting.IsValidMonsterTarget(CurrentTarget))
		{
			return;
		}

		_targetCommitmentRemainingSeconds =
			Mathf.Max(
				0.0,
				_targetCommitmentRemainingSeconds - delta);

		_targetReassessmentRemainingSeconds =
			Mathf.Max(
				0.0,
				_targetReassessmentRemainingSeconds - delta);
	}

	/// <summary>
	/// Performs the begin target commitment operation for Hero Actor Controller.
	/// Uses the supplied arguments and current node state; any result is applied through side effects, events, or stored fields.
	/// </summary>
	private void BeginTargetCommitment(
		float? commitmentSeconds = null)
	{
		_targetCommitmentRemainingSeconds =
			Mathf.Max(
				commitmentSeconds
					?? ActiveStanceProfile
						.MinimumTargetCommitmentSeconds,
				0.0f);

		_targetReassessmentRemainingSeconds =
			Mathf.Max(
				ActiveStanceProfile
					.TargetReassessmentIntervalSeconds,
				0.0f);
	}

	/// <summary>
	/// Performs the restart target reassessment timer operation for Hero Actor Controller.
	/// Uses the current node and service state; any result is applied through side effects, events, or stored fields.
	/// </summary>
	private void RestartTargetReassessmentTimer()
	{
		_targetReassessmentRemainingSeconds =
			Mathf.Max(
				ActiveStanceProfile
					.TargetReassessmentIntervalSeconds,
				0.0f);
	}

	/// <summary>
	/// Resets target decision timers so the system can begin from a clean state.
	/// Uses the current node and service state; any result is applied through side effects, events, or stored fields.
	/// </summary>
	private void ResetTargetDecisionTimers()
	{
		_targetCommitmentRemainingSeconds = 0.0;
		_targetReassessmentRemainingSeconds = 0.0;
		_isPartySupportActive = false;
		_partySupportAlly = null;
	}

	private double _movementAnimationGraceRemaining;

	/// <summary>
	/// Recalculates movement presentation from the latest runtime state.
	/// Uses the supplied arguments and current node state; any result is applied through side effects, events, or stored fields.
	/// </summary>
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

	/// <summary>
	/// Recalculates movement animation from the latest runtime state.
	/// Uses the supplied arguments and current node state; any result is applied through side effects, events, or stored fields.
	/// </summary>
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

	/// <summary>
	/// Performs the stop movement animation operation for Hero Actor Controller.
	/// Uses the current node and service state; any result is applied through side effects, events, or stored fields.
	/// </summary>
	private void StopMovementAnimation()
	{
		_animationTime = 0.0;
		VisualRoot.Position = _visualRestPosition;
	}

	private bool _initialAttackPending;

	/// <summary>
	/// Retrieves melee slot horizontal distance from the current game state.
	/// Uses the supplied arguments and current state and returns the resulting float to the caller.
	/// </summary>
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

	/// <summary>
	/// Retrieves melee slot vertical distance from the current game state.
	/// Uses the supplied arguments and current state and returns the resulting float to the caller.
	/// </summary>
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

	/// <summary>
	/// Attempts to get melee engagement position without throwing when the operation cannot be completed.
	/// Uses the supplied arguments and current state and returns the resulting bool to the caller.
	/// </summary>
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

	/// <summary>
	/// Performs the has melee engagement reservation operation for Hero Actor Controller.
	/// Uses the supplied arguments and current state and returns the resulting bool to the caller.
	/// </summary>
	private bool HasMeleeEngagementReservation(
		MonsterActorController target)
	{
		return UsesMeleeEngagementSlots
			&& target.MeleeEngagementSlots.TryGetReservation(
				this,
				out _);
	}

	/// <summary>
	/// Performs the is target within melee engagement range operation for Hero Actor Controller.
	/// Uses the supplied arguments and current state and returns the resulting bool to the caller.
	/// </summary>
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

	/// <summary>
	/// Performs the has reached melee engagement position operation for Hero Actor Controller.
	/// Uses the supplied arguments and current state and returns the resulting bool to the caller.
	/// </summary>
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

	/// <summary>
	/// Performs the release melee engagement slot operation for Hero Actor Controller.
	/// Uses the supplied arguments and current node state; any result is applied through side effects, events, or stored fields.
	/// </summary>
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

	/// <summary>
	/// Retrieves body clearance distance from the current game state.
	/// Uses the supplied arguments and current state and returns the resulting float to the caller.
	/// </summary>
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

	/// <summary>
	/// Retrieves required attack distance from the current game state.
	/// Uses the supplied arguments and current state and returns the resulting float to the caller.
	/// </summary>
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

	/// <summary>
	/// Performs the calculate approach position operation for Hero Actor Controller.
	/// Uses the supplied arguments and current state and returns the resulting vector2 to the caller.
	/// </summary>
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

	/// <summary>
	/// Performs the initialize combat profile operation for Hero Actor Controller.
	/// Uses the current node and service state; any result is applied through side effects, events, or stored fields.
	/// </summary>
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

	/// <summary>
	/// Performs the is target within attack range operation for Hero Actor Controller.
	/// Uses the supplied arguments and current state and returns the resulting bool to the caller.
	/// </summary>
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

	/// <summary>
	/// Retrieves scaled combat radius from the current game state.
	/// Reads the current state and returns the resulting float to the caller.
	/// </summary>
	private float GetScaledCombatRadius()
	{
		return Mathf.Max(
			0.0f,
			CombatProfile.CombatRadius)
			* CombatPresentationScale;
	}

	/// <summary>
	/// Retrieves hero separation vertical leash from the current game state.
	/// Reads the current state and returns the resulting float to the caller.
	/// </summary>
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

	/// <summary>
	/// Retrieves non melee vertical alignment tolerance from the current game state.
	/// Reads the current state and returns the resulting float to the caller.
	/// </summary>
	private float GetNonMeleeVerticalAlignmentTolerance()
	{
		return AttackRangeTolerance
			+ GetHeroSeparationVerticalLeash();
	}

	/// <summary>
	/// Retrieves combat separation anchor y from the current game state.
	/// Uses the supplied arguments and current state and returns the resulting float to the caller.
	/// </summary>
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

	/// <summary>
	/// Performs the is valid separation peer operation for Hero Actor Controller.
	/// Uses the supplied arguments and current state and returns the resulting bool to the caller.
	/// </summary>
	private bool IsValidSeparationPeer(
		HeroActorController? partyMember)
	{
		return partyMember is not null
			&& partyMember != this
			&& GodotObject.IsInstanceValid(partyMember)
			&& partyMember.IsInsideTree()
			&& !partyMember.IsIncapacitated;
	}

	/// <summary>
	/// Applies hero separation to the relevant actor, resource, or presentation state.
	/// Uses the supplied arguments and current node state; any result is applied through side effects, events, or stored fields.
	/// </summary>
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

	/// <summary>
	/// Recalculates facing toward target from the latest runtime state.
	/// Uses the current node and service state; any result is applied through side effects, events, or stored fields.
	/// </summary>
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

	/// <summary>
	/// Performs the prepare to attack operation for Hero Actor Controller.
	/// Uses the current node and service state; any result is applied through side effects, events, or stored fields.
	/// </summary>
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

	/// <summary>
	/// Recalculates combat approach from the latest runtime state.
	/// Uses the supplied arguments and current node state; any result is applied through side effects, events, or stored fields.
	/// </summary>
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

	/// <summary>
	/// Recalculates waiting to attack from the latest runtime state.
	/// Uses the supplied arguments and current node state; any result is applied through side effects, events, or stored fields.
	/// </summary>
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

	/// <summary>
	/// Performs the begin attack operation for Hero Actor Controller.
	/// Uses the current node and service state; any result is applied through side effects, events, or stored fields.
	/// </summary>
	private void BeginAttack()
	{
		if (!Targeting.IsValidMonsterTarget(CurrentTarget))
			return;

		// Abilities have universal priority over BEGINNING a new basic attack.
		// The current attack is never interrupted; this gate only runs at the
		// moment a new basic attack would otherwise start.
		if (_tryUsePriorityAbilityBeforeBasicAttack?.Invoke(this)
			== true)
		{
			DebugLog.Print(
				$"{Name} deferred a basic attack because a " +
				"higher-priority ability began.",
				DebugLogCategory.Ability);

			return;
		}

		_state = HeroState.Attacking;
		_attackTimeRemaining = CombatProfile.AttackDuration;
		_attackReleaseEmitted = false;

		StopMovementAnimation();

		DebugLog.Print(
			$"{Name} began attacking {CurrentTarget!.Name}.");
	}

	/// <summary>
	/// Recalculates ability from the latest runtime state.
	/// Uses the supplied arguments and current node state; any result is applied through side effects, events, or stored fields.
	/// </summary>
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
		Node2D target = _activeAbilityTarget;

		_abilityCastTimeRemaining -= delta;

		if (_abilityCastTimeRemaining > 0.0)
			return;

		if (!TryCommitAbility(ability))
		{
			DebugLog.Print(
				$"{Name} could not commit ability " +
				$"'{ability.DisplayName}'. Cast cancelled before release.",
				DebugLogCategory.Ability);

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

	/// <summary>
	/// Performs the resume after ability operation for Hero Actor Controller.
	/// Uses the current node and service state; any result is applied through side effects, events, or stored fields.
	/// </summary>
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

	/// <summary>
	/// Resets ability cast so the system can begin from a clean state.
	/// Uses the current node and service state; any result is applied through side effects, events, or stored fields.
	/// </summary>
	private void ClearAbilityCast()
	{
		_activeAbility = null;
		_activeAbilityTarget = null;
		_abilityCastTimeRemaining = 0.0;
	}

	/// <summary>
	/// Recalculates attack from the latest runtime state.
	/// Uses the supplied arguments and current node state; any result is applied through side effects, events, or stored fields.
	/// </summary>
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

	/// <summary>
	/// Performs the end attack operation for Hero Actor Controller.
	/// Uses the current node and service state; any result is applied through side effects, events, or stored fields.
	/// </summary>
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

	/// <summary>
	/// Performs the enter incapacitated state operation for Hero Actor Controller.
	/// Uses the current node and service state; any result is applied through side effects, events, or stored fields.
	/// </summary>
	public void EnterIncapacitatedState()
	{
		if (IsIncapacitated)
			return;

		_state = HeroState.Incapacitated;
		ComboPoints.Reset();
		ReleaseMeleeEngagementSlot(CurrentTarget);
		MeleeEngagementSlots.Clear();
		CurrentTarget = null;
		ResetTargetDecisionTimers();

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

	/// <summary>
	/// Attempts to emit attack release without throwing when the operation cannot be completed.
	/// Uses the supplied arguments and current node state; any result is applied through side effects, events, or stored fields.
	/// </summary>
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

	/// <summary>
	/// Recalculates return to formation from the latest runtime state.
	/// Uses the supplied arguments and current node state; any result is applied through side effects, events, or stored fields.
	/// </summary>
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

	/// <summary>
	/// Applies journey state to the relevant actor, resource, or presentation state.
	/// Uses the supplied arguments and current node state; any result is applied through side effects, events, or stored fields.
	/// </summary>
	private void ApplyJourneyState(JourneyStateService.JourneyState state)
	{
		if (state
			!= JourneyStateService.JourneyState.Encounter)
		{
			ReleaseMeleeEngagementSlot(CurrentTarget);
			ResetTargetDecisionTimers();
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

	/// <summary>
	/// Handles the journey state changed event and updates the related game state.
	/// Uses the supplied arguments and current node state; any result is applied through side effects, events, or stored fields.
	/// </summary>
	private void OnJourneyStateChanged(
		JourneyStateService.JourneyState previousState,
		JourneyStateService.JourneyState currentState)
	{
		ApplyJourneyState(currentState);
	}

	/// <summary>
	/// Performs the validate references operation for Hero Actor Controller.
	/// Reads the current state and returns the resulting bool to the caller.
	/// </summary>
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
		AbilityCooldownIndicatorSlot1,
		nameof(AbilityCooldownIndicatorSlot1));

		valid &= Require(
		AbilityCooldownIndicatorSlot2,
		nameof(AbilityCooldownIndicatorSlot2));
		
		valid &= Require(ResourceBar, nameof(ResourceBar));
		
		valid &= Require(
			ComboPointDisplay,
			nameof(ComboPointDisplay));

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
	
	/// <summary>
	/// Performs the snap to formation operation for Hero Actor Controller.
	/// Uses the current node and service state; any result is applied through side effects, events, or stored fields.
	/// </summary>
	public void SnapToFormation()
	{
		GlobalPosition = FormationPosition;
	}

	/// <summary>
	/// Performs the refresh target operation for Hero Actor Controller.
	/// Uses the supplied arguments and current node state; any result is applied through side effects, events, or stored fields.
	/// </summary>
	public void RefreshTarget(IReadOnlyList<MonsterActorController> candidates)
	{
		if (IsIncapacitated)
			return;

		bool hasValidCurrentTarget =
			CurrentTarget is not null
			&& Targeting.IsValidMonsterTarget(CurrentTarget)
			&& ContainsTarget(candidates, CurrentTarget);

		if (hasValidCurrentTarget
			&& (_state == HeroState.Attacking
				|| _state == HeroState.UsingAbility))
		{
			return;
		}

		bool wasPartySupportActive =
			_isPartySupportActive;

		if (TryRefreshPartySupportTarget(
			candidates,
			hasValidCurrentTarget))
		{
			return;
		}

		_isPartySupportActive = false;
		_partySupportAlly = null;

		if (wasPartySupportActive)
		{
			_targetCommitmentRemainingSeconds = 0.0;
			_targetReassessmentRemainingSeconds = 0.0;
		}

		if (hasValidCurrentTarget
			&& (_targetCommitmentRemainingSeconds > 0.0
				|| _targetReassessmentRemainingSeconds > 0.0))
		{
			return;
		}

		MonsterActorController? previousTarget =
			CurrentTarget;

		MonsterActorController? proposedTarget =
			Targeting.SelectPriorityMonster(
				this,
				candidates,
				_partyMembers,
				out HeroTargetDecision decision);

		float requiredSwitchScore = 0.0f;

		if (hasValidCurrentTarget
			&& proposedTarget != previousTarget
			&& !DoesTargetSwitchMeetAdvantage(
				decision,
				out requiredSwitchScore))
		{
			RestartTargetReassessmentTimer();
			return;
		}

		if (proposedTarget == previousTarget)
		{
			RestartTargetReassessmentTimer();
			return;
		}

		ApplyTargetChange(
			proposedTarget,
			previousTarget);

		if (ActiveStanceProfile.LogTargetingDecisions)
		{
			PrintTargetDecision(
				decision,
				previousTarget,
				requiredSwitchScore);
		}
		
	}

	/// <summary>
	/// Attempts to refresh party support target without throwing when the operation cannot be completed.
	/// Uses the supplied arguments and current state and returns the resulting bool to the caller.
	/// </summary>
	private bool TryRefreshPartySupportTarget(
		IReadOnlyList<MonsterActorController> candidates,
		bool hasValidCurrentTarget)
	{
		if (!Targeting.TrySelectPartySupportTarget(
			this,
			candidates,
			_partyMembers,
			out HeroTargetDecision decision)
			|| decision.SelectedTarget is null)
		{
			return false;
		}

		MonsterActorController rescueTarget =
			decision.SelectedTarget;

		bool wasAlreadyRescuingSameAlly =
			_isPartySupportActive
			&& _partySupportAlly
				== decision.RescueAlly;

		_isPartySupportActive = true;
		_partySupportAlly = decision.RescueAlly;

		if (hasValidCurrentTarget
			&& CurrentTarget == rescueTarget)
		{
			if (!wasAlreadyRescuingSameAlly)
			{
				BeginTargetCommitment(
					ActiveStanceProfile
						.RescueTargetCommitmentSeconds);
			}

			if (!wasAlreadyRescuingSameAlly
				&& ActiveStanceProfile.LogTargetingDecisions)
			{
				PrintPartySupportDecision(decision);
			}

			return true;
		}

		MonsterActorController? previousTarget =
			CurrentTarget;

		ApplyTargetChange(
			rescueTarget,
			previousTarget,
			ActiveStanceProfile
				.RescueTargetCommitmentSeconds);

		if (ActiveStanceProfile.LogTargetingDecisions)
			PrintPartySupportDecision(decision);

		return true;
	}

	/// <summary>
	/// Applies target change to the relevant actor, resource, or presentation state.
	/// Uses the supplied arguments and current node state; any result is applied through side effects, events, or stored fields.
	/// </summary>
	private void ApplyTargetChange(
		MonsterActorController? proposedTarget,
		MonsterActorController? previousTarget,
		float? commitmentSeconds = null)
	{
		CurrentTarget = proposedTarget;
		ReleaseMeleeEngagementSlot(previousTarget);

		if (CurrentTarget is null)
		{
			ResetTargetDecisionTimers();

			DebugLog.Print(
				$"{Name} has no valid monster target.");

			return;
		}

		_initialAttackPending = true;
		BeginTargetCommitment(commitmentSeconds);
		TryGetMeleeEngagementPosition(
			CurrentTarget,
			out _);

		DebugLog.Print(
			$"{Name} targeted {CurrentTarget.Name} " +
			$"at X={CurrentTarget.GlobalPosition.X}.");

		if (_state != HeroState.UsingAbility
			&& JourneyState.CurrentState
				== JourneyStateService.JourneyState.Encounter)
		{
			_state = HeroState.ApproachingTarget;
		}
	}

	/// <summary>
	/// Performs the does target switch meet advantage operation for Hero Actor Controller.
	/// Uses the supplied arguments and current state and returns the resulting bool to the caller.
	/// </summary>
	private bool DoesTargetSwitchMeetAdvantage(
		HeroTargetDecision decision,
		out float requiredSwitchScore)
	{
		requiredSwitchScore = 0.0f;

		if (decision.SelectedScore is null
			|| decision.CurrentTargetScore is null)
		{
			return false;
		}

		float currentScore =
			decision.CurrentTargetScore.TotalScore;

		float requiredAdvantage =
			Mathf.Max(
				Mathf.Abs(currentScore),
				1.0f)
			* Mathf.Max(
				ActiveStanceProfile
					.RequiredSwitchAdvantagePercent,
				0.0f)
			/ 100.0f;

		requiredSwitchScore =
			currentScore + requiredAdvantage;

		return decision.SelectedScore.TotalScore
			> requiredSwitchScore
			|| Mathf.IsEqualApprox(
				decision.SelectedScore.TotalScore,
				requiredSwitchScore);
	}

	/// <summary>
	/// Performs the print target decision operation for Hero Actor Controller.
	/// Uses the supplied arguments and current node state; any result is applied through side effects, events, or stored fields.
	/// </summary>
	private void PrintTargetDecision(
		HeroTargetDecision decision,
		MonsterActorController? previousTarget,
		float requiredSwitchScore)
	{
		if (decision.SelectedScore is not HeroTargetScore score)
			return;

		string previousTargetText = previousTarget is not null
			&& GodotObject.IsInstanceValid(previousTarget)
				? previousTarget.Name.ToString()
				: "None";

		string switchRequirementText =
			decision.CurrentTargetScore is null
				? "Initial"
				: requiredSwitchScore.ToString("0.##");

		DebugLog.Print(
			$"{Name} [{CombatStance}] target decision: " +
			$"Selected={score.Target.Name}; " +
			$"Previous={previousTargetText}; " +
			$"Score={score.TotalScore:0.##}; " +
			$"Health=" +
			$"{score.LowestHealthScore + score.HighestHealthScore:0.##}; " +
			$"Danger={score.DangerScore:0.##}; " +
			$"Coverage={score.CoverageScore:0.##}; " +
			$"Support={score.HealthyAllySupportScore:0.##}; " +
			$"Saturation=-{score.SaturationPenalty:0.##}; " +
			$"Aggro={score.AggroScore:0.##}; " +
			$"Stickiness={score.CurrentTargetScore:0.##}; " +
			$"Attackers={score.OtherHeroAttackerCount}/" +
			$"{score.PreferredHeroAttackerCount}; " +
			$"SwitchRequired={switchRequirementText}; " +
			$"Commitment=" +
			$"{ActiveStanceProfile.MinimumTargetCommitmentSeconds:0.##}s.");
	}

	/// <summary>
	/// Performs the print party support decision operation for Hero Actor Controller.
	/// Uses the supplied arguments and current node state; any result is applied through side effects, events, or stored fields.
	/// </summary>
	private void PrintPartySupportDecision(
		HeroTargetDecision decision)
	{
		if (decision.SelectedTarget is null
			|| decision.RescueAlly is null)
		{
			return;
		}

		string supportReason = decision.SelectionRule switch
		{
			"CriticalAllyRescue" => "CriticalRescue",
			"VulnerableAllyRescue" => "VulnerableRescue",
			_ => decision.SelectionRule
		};

		DebugLog.Print(
			$"{Name} [{CombatStance}] party support: {supportReason}; " +
			$"Ally={decision.RescueAlly.Name}; " +
			$"Health={decision.RescueAllyHealthPercent:0.##}%; " +
			$"Pressure={decision.RescuePressure}; " +
			$"Target={decision.SelectedTarget.Name}; " +
			$"Danger={decision.SelectedRuleValue:0.##}; " +
			$"Attackers={decision.OtherHeroAttackerCount + 1}/" +
			$"{decision.PreferredHeroAttackerCount}; " +
			$"Commitment=" +
			$"{ActiveStanceProfile.RescueTargetCommitmentSeconds:0.##}s.");
	}

	/// <summary>
	/// Performs the contains target operation for Hero Actor Controller.
	/// Uses the supplied arguments and current state and returns the resulting bool to the caller.
	/// </summary>
	private static bool ContainsTarget(IReadOnlyList<MonsterActorController> candidates, MonsterActorController target)
	{
		foreach (MonsterActorController candidate in candidates)
		{
			if (candidate == target)
				return true;
		}

		return false;
	}

	/// <summary>
	/// Resets target so the system can begin from a clean state.
	/// Uses the current node and service state; any result is applied through side effects, events, or stored fields.
	/// </summary>
	public void ClearTarget()
	{
		if (CurrentTarget is null)
			return;

		ReleaseMeleeEngagementSlot(CurrentTarget);
		CurrentTarget = null;
		_initialAttackPending = false;
		ResetTargetDecisionTimers();

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
	/// <summary>
	/// Performs the require operation for Hero Actor Controller.
	/// Uses the supplied arguments and current state and returns the resulting bool to the caller.
	/// </summary>
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
