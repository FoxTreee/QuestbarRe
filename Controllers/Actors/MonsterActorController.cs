using Godot;
using System.Collections.Generic;


public partial class MonsterActorController : Node2D, ICombatStatusEffectOwner
{
	[Signal]
	public delegate void AttackReleasedEventHandler(
	MonsterActorController attacker,
	HeroActorController target);

	[Signal]
	public delegate void AbilityReleasedEventHandler(
		MonsterActorController attacker,
		HeroActorController target,
		AbilityDefinition ability);

	[Signal]
	public delegate void DiedEventHandler(
	MonsterActorController monster);

	[Signal]
	public delegate void ForcedTargetEndedEventHandler(
		MonsterActorController monster);

	private enum MonsterState
	{
		WaitingForTarget,
		ApproachingTarget,
		WaitingToAttack,
		Attacking,
		UsingAbility,
		Dead
	}

	private Vector2 _visualRestPosition;
	private double _attackCooldownRemaining;
	private double _attackTimeRemaining;
	private bool _attackReleaseEmitted;
	private ActorHealthBarController _healthBar = null!;

	private readonly List<AbilityDefinition> _abilities = new();
	private readonly Dictionary<AbilityDefinition, double>
		_abilityCooldowns = new();
	private AbilityDefinition? _activeAbility;
	private double _abilityCastTimeRemaining;
	private HeroActorController? _forcedTarget;
	private double _forcedTargetTimeRemaining;

	private SceneBoundaryService _sceneBoundaries = null!;
	private readonly RandomNumberGenerator _forcedMovementRandom = new();
	private CombatStatusEffectInstance? _activeForcedMovementEffect;
	private Vector2 _panicStartPosition;
	private Vector2 _panicDirection;
	private double _panicDirectionTimeRemaining;

	public bool HasValidTarget => IsValidHeroTarget(CurrentTarget);

	[ExportCategory("Visuals")]
	/// <summary>
	/// Inspector reference used by this component for its presentation root dependency.
	/// Assign the matching node or resource from the scene; leaving it empty prevents that connection from working.
	/// </summary>
	[Export]
	public Node2D PresentationRoot { get; set; } = null!;

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
	/// Inspector reference used by this component for its impact origin dependency.
	/// Assign the matching node or resource from the scene; leaving it empty prevents that connection from working.
	/// </summary>
	[Export]
	public Marker2D ImpactOrigin { get; set; } = null!;

	/// <summary>
	/// Inspector reference used by this component for its health bar anchor dependency.
	/// Assign the matching node or resource from the scene; leaving it empty prevents that connection from working.
	/// </summary>
	[Export]
	public Marker2D HealthBarAnchor { get; set; } = null!;

	/// <summary>
	/// Controls health bar gap, measured as health points.
	/// For example, changing 18 to 36 doubles the configured health bar gap.
	/// </summary>
	[Export(PropertyHint.Range, "0,32,1")]
	public float HealthBarGap { get; set; } = 18.0f;

	[ExportCategory("Combat Movement")]
	/// <summary>
	/// Controls combat arrival distance, measured as pixels.
	/// For example, changing 1 to 2 doubles the configured combat arrival distance.
	/// </summary>
	[Export(PropertyHint.Range, "0.1,20,0.1")]
	public float CombatArrivalDistance { get; set; } = 1.0f;

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

	public float AttackDamage { get; set; }

	/// <summary>
	/// Runtime level captured from the active region when this monster spawned.
	/// Debug-spawned monsters use their authored definition level.
	/// </summary>
	public int Level { get; private set; } = 1;

	/// <summary>
	/// Regional spawn scaling used by basic attacks, fixed-damage abilities, and
	/// maximum health. Null means this actor uses unscaled authored values.
	/// </summary>
	public MonsterDifficultySnapshot? Difficulty { get; private set; }

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
	public float CombatPresentationScale { get; private set; } = 1.0f;

	/// <summary>
	/// Updates combat presentation scale and applies the new value to the owning system.
	/// Uses the supplied arguments and current node state; any result is applied through side effects, events, or stored fields.
	/// </summary>
	public void SetCombatPresentationScale(float scale)
	{
		CombatPresentationScale =
			Mathf.Max(scale, 0.01f);
	}

	public Vector2 ImpactPosition => ImpactOrigin.GlobalPosition;

	private MonsterState _state = MonsterState.WaitingForTarget;

	public FacingDirection Facing { get; private set; }
	= FacingDirection.Right;

	public HeroActorController? CurrentTarget { get; private set; }

	public bool IsDead => _state == MonsterState.Dead;

	public CombatHealthState Health { get; } = new();

	/// <summary>
	/// Owns temporary combat status effects currently active on this monster.
	/// The state tracks identity and duration only; individual effects decide
	/// their gameplay behavior in later checkpoints.
	/// </summary>
	public CombatStatusEffectState StatusEffects { get; } = new();

	public MonsterThreatState Threat { get; } = new();

	public MeleeEngagementSlotSet MeleeEngagementSlots { get; } =
		new();

	public bool HasTarget => IsValidHeroTarget(CurrentTarget);

	public bool HasForcedTarget =>
		_forcedTargetTimeRemaining > 0.0
		&& IsValidHeroTarget(_forcedTarget);

	public HeroActorController? ForcedTarget =>
		HasForcedTarget
			? _forcedTarget
			: null;

	public float ForcedTargetSecondsRemaining =>
		(float)System.Math.Max(
			0.0,
			_forcedTargetTimeRemaining);

	/// <summary>
	/// Performs the configure operation for Monster Actor Controller.
	/// Uses the supplied arguments and current node state; any result is applied through side effects, events, or stored fields.
	/// </summary>
	public void Configure(
		MonsterDefinition definition,
		IReadOnlyList<AbilityDefinition>? abilities,
		SceneBoundaryService sceneBoundaries,
		MonsterDifficultySnapshot? difficulty = null)
	{
		if (!GodotObject.IsInstanceValid(definition))
		{
			throw new System.ArgumentNullException(
				nameof(definition));
		}

		if (!GodotObject.IsInstanceValid(sceneBoundaries))
		{
			throw new System.ArgumentNullException(nameof(sceneBoundaries));
		}

		Definition = definition;
		_sceneBoundaries = sceneBoundaries;
		Difficulty = difficulty;
		Level = difficulty?.MonsterLevel ?? definition.Level;

		StatusEffects.Clear();
		Threat.Clear();
		SetCurrentTarget(null);
		MeleeEngagementSlots.Clear();
		_forcedTarget = null;
		_forcedTargetTimeRemaining = 0.0;
		ResetForcedMovementRuntime();

		_abilities.Clear();
		_abilityCooldowns.Clear();

		if (abilities is null)
			return;

		foreach (AbilityDefinition ability in abilities)
		{
			if (!GodotObject.IsInstanceValid(ability))
				continue;

			_abilities.Add(ability);
			_abilityCooldowns[ability] = 0.0;
		}
	}

	/// <summary>
	/// Attempts to apply forced target without throwing when the operation cannot be completed.
	/// Uses the supplied arguments and current state and returns the resulting bool to the caller.
	/// </summary>
	public bool TryApplyForcedTarget(
		HeroActorController target,
		float durationSeconds)
	{
		if (IsDead
			|| !IsValidHeroTarget(target)
			|| !float.IsFinite(durationSeconds)
			|| durationSeconds <= 0.0f)
		{
			return false;
		}

		_forcedTarget = target;
		_forcedTargetTimeRemaining = durationSeconds;
		SetCurrentTarget(target);

		_attackCooldownRemaining = 0.0;
		_attackTimeRemaining = 0.0;
		_attackReleaseEmitted = false;
		ClearAbilityCast();
		StopAttackPresentation();

		_state = MonsterState.ApproachingTarget;

		return true;
	}

	/// <summary>
	/// Performs the refresh target validity operation for Monster Actor Controller.
	/// Uses the current node and service state; any result is applied through side effects, events, or stored fields.
	/// </summary>
	public void RefreshTargetValidity()
	{
		if (IsDead)
			return;

		if (_forcedTarget is not null
			&& (!IsValidHeroTarget(_forcedTarget)
				|| _forcedTargetTimeRemaining <= 0.0))
		{
			_forcedTarget = null;
			_forcedTargetTimeRemaining = 0.0;
		}

		if (CurrentTarget is null)
			return;

		if (IsValidHeroTarget(CurrentTarget))
			return;

		SetCurrentTarget(null);

		_attackCooldownRemaining = 0.0;
		_attackTimeRemaining = 0.0;
		_attackReleaseEmitted = false;
		ClearAbilityCast();

		StopAttackPresentation();

		_state =
			MonsterState.WaitingForTarget;

		DebugLog.Print(
			$"{Name} released its invalid hero target.");
	}

	/// <summary>
	/// Performs the enter dead state operation for Monster Actor Controller.
	/// Uses the current node and service state; any result is applied through side effects, events, or stored fields.
	/// </summary>
	public void EnterDeadState()
	{
		if (IsDead)
			return;

		_state = MonsterState.Dead;
		MeleeEngagementSlots.Clear();
		SetCurrentTarget(null);
		_forcedTarget = null;
		_forcedTargetTimeRemaining = 0.0;

		_attackCooldownRemaining = 0.0;
		_attackTimeRemaining = 0.0;
		_attackReleaseEmitted = false;
		ClearAbilityCast();

		StopAttackPresentation();

		DebugLog.Print(
			$"{Name} entered its Dead state.");

		EmitSignal(
			SignalName.Died,
			this);
	}

	/// <summary>
	/// Applies definition to the relevant actor, resource, or presentation state.
	/// Uses the current node and service state; any result is applied through side effects, events, or stored fields.
	/// </summary>
	private void ApplyDefinition()
	{
		float healthMultiplier = Difficulty?.HealthMultiplier ?? 1.0f;
		float damageMultiplier = Difficulty?.DamageMultiplier ?? 1.0f;

		CombatProfile.MaximumHealth =
			Definition.MaximumHealth * healthMultiplier;
		CombatProfile.AttackDamage =
			Definition.AttackDamage * damageMultiplier;
		CombatProfile.AttackRange = Definition.AttackRange;
		CombatProfile.CombatRadius = BodyBounds.GetHorizontalRadiusInParentSpace() * Mathf.Abs(VisualRoot.Scale.X);
		CombatProfile.AttackInterval = Definition.AttackInterval;
		CombatProfile.AttackDuration = Definition.AttackDuration;
		CombatProfile.AttackReleasePoint = Definition.AttackReleasePoint;
		CombatProfile.AttackLungeDistance = Definition.AttackLungeDistance;
		CombatProfile.MoveSpeed = Definition.CombatMoveSpeed;
		CombatProfile.DodgeChancePercent = Definition.DodgeChancePercent;
		CombatProfile.AttackDelivery = Definition.AttackDelivery;
	}

	/// <summary>
	/// Performs the position health bar operation for Monster Actor Controller.
	/// Uses the current node and service state; any result is applied through side effects, events, or stored fields.
	/// </summary>
	private void PositionHealthBar()
	{
		if (!GodotObject.IsInstanceValid(_healthBar)
			|| !GodotObject.IsInstanceValid(PresentationRoot)
			|| !GodotObject.IsInstanceValid(HealthBarAnchor))
		{
			return;
		}

		Vector2 anchorPosition =
			PresentationRoot.ToLocal(
				HealthBarAnchor.GlobalPosition);

		_healthBar.Position =
			anchorPosition
			+ Vector2.Up * HealthBarGap;
	}

	/// <summary>
	/// Runs Godot setup for Monster Actor Controller when the node enters the scene tree.
	/// Uses the current node and service state; any result is applied through side effects, events, or stored fields.
	/// </summary>
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

		if (!GodotObject.IsInstanceValid(BodyBounds))
		{
			GD.PushError(
				"MonsterActorController is missing its " +
				"BodyBounds Inspector reference.");

			SetProcess(false);
			return;
		}

		if (!GodotObject.IsInstanceValid(PresentationRoot))
		{
			GD.PushError(
				"MonsterActorController is missing its " +
				"PresentationRoot Inspector reference.");

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

		if (!GodotObject.IsInstanceValid(HealthBarAnchor))
		{
			GD.PushError(
				"MonsterActorController is missing its " +
				"HealthBarAnchor Inspector reference.");

			SetProcess(false);
			return;
		}

		if (!GodotObject.IsInstanceValid(_sceneBoundaries))
		{
			GD.PushError(
				$"{Name} cannot initialize because no SceneBoundaryService was configured.");
			SetProcess(false);
			return;
		}

		_forcedMovementRandom.Randomize();

		_healthBar = GetNodeOrNull<ActorHealthBarController>("PresentationRoot/ActorHealthBar")!;

		if (!GodotObject.IsInstanceValid(_healthBar))
		{
			GD.PushError(
				$"{Name} requires an ActorHealthBar at " +
				"'PresentationRoot/ActorHealthBar' using " +
				"ActorHealthBarController.cs.");

			SetProcess(false);
			return;
		}

		ApplyDefinition();

		PositionHealthBar();

		Health.Initialize(
			CombatProfile.MaximumHealth);

		_healthBar.Bind(Health);

		_visualRestPosition = VisualRoot.Position;
		InitializeMonsterAnimation();

		string targetPreference =
			Definition.PreferredTargetTags == HeroCombatTag.None
				? "Any"
				: Definition.PreferredTargetTags.ToString();

		DebugLog.Print(
			$"{Name} initialized as " +
			$"{Definition.ContentId} " +
			$"('{Definition.DisplayName}', Level {Level}) with " +
			$"{Health.CurrentHealth}/" +
			$"{Health.MaximumHealth} health. " +
			$"Target preference={targetPreference}; " +
			$"selection={Definition.TargetingStyle}.");
	}

	/// <summary>
	/// Performs the is valid hero target operation for Monster Actor Controller.
	/// Uses the supplied arguments and current state and returns the resulting bool to the caller.
	/// </summary>
	private static bool IsValidHeroTarget(HeroActorController? hero)
	{
		return hero is not null
			&& GodotObject.IsInstanceValid(hero)
			&& hero.IsInsideTree()
			&& hero.Health.IsAlive
			&& !hero.IsIncapacitated;
	}

	/// <summary>
	/// Performs the uses melee engagement slots operation for Monster Actor Controller.
	/// Uses the supplied arguments and current state and returns the resulting bool to the caller.
	/// </summary>
	private bool UsesMeleeEngagementSlots(
		HeroActorController target)
	{
		float scaledAttackRange =
			Mathf.Max(0.0f, CombatProfile.AttackRange)
			* CombatPresentationScale;

		float scaledArrivalDistance =
			CombatArrivalDistance
			* CombatPresentationScale;

		return scaledAttackRange
			<= GetBodyClearanceDistance(target)
			+ scaledArrivalDistance;
	}

	/// <summary>
	/// Retrieves melee slot horizontal distance from the current game state.
	/// Uses the supplied arguments and current state and returns the resulting float to the caller.
	/// </summary>
	private float GetMeleeSlotHorizontalDistance(
		HeroActorController target)
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
		HeroActorController target)
	{
		float scaledMonsterRadius =
			Mathf.Max(0.0f, CombatProfile.CombatRadius)
			* CombatPresentationScale;

		float scaledTargetRadius =
			Mathf.Max(0.0f, target.CombatProfile.CombatRadius)
			* target.CombatPresentationScale;

		return Mathf.Max(
			CombatArrivalDistance * CombatPresentationScale,
			(scaledMonsterRadius + scaledTargetRadius)
				* Mathf.Max(
					MeleeSlotVerticalSpacingMultiplier,
					0.0f));
	}

	/// <summary>
	/// Attempts to get melee engagement position without throwing when the operation cannot be completed.
	/// Uses the supplied arguments and current state and returns the resulting bool to the caller.
	/// </summary>
	private bool TryGetMeleeEngagementPosition(
		HeroActorController target,
		out Vector2 engagementPosition)
	{
		engagementPosition = Vector2.Zero;

		if (!UsesMeleeEngagementSlots(target))
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
	/// Performs the has melee engagement reservation operation for Monster Actor Controller.
	/// Uses the supplied arguments and current state and returns the resulting bool to the caller.
	/// </summary>
	private bool HasMeleeEngagementReservation(
		HeroActorController target)
	{
		return UsesMeleeEngagementSlots(target)
			&& target.MeleeEngagementSlots.TryGetReservation(
				this,
				out _);
	}

	/// <summary>
	/// Performs the is target within melee engagement range operation for Monster Actor Controller.
	/// Uses the supplied arguments and current state and returns the resulting bool to the caller.
	/// </summary>
	private bool IsTargetWithinMeleeEngagementRange(
		HeroActorController target)
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
			CombatArrivalDistance
			* CombatPresentationScale;

		return GlobalPosition.DistanceTo(target.GlobalPosition)
			<= maximumDistance + scaledTolerance;
	}

	/// <summary>
	/// Performs the has reached melee engagement position operation for Monster Actor Controller.
	/// Uses the supplied arguments and current state and returns the resulting bool to the caller.
	/// </summary>
	private bool HasReachedMeleeEngagementPosition(
		HeroActorController target,
		Vector2 engagementPosition)
	{
		if (GlobalPosition.DistanceTo(engagementPosition)
			<= CombatArrivalDistance)
		{
			return true;
		}

		// A slot can extend beyond the top or bottom of the legal ground
		// area. If the ground constraint stops vertical movement, the
		// monster may still engage after reaching the reserved side.
		return Mathf.Abs(
			GlobalPosition.X
			- engagementPosition.X)
			<= CombatArrivalDistance
			&& IsTargetWithinMeleeEngagementRange(target);
	}

	/// <summary>
	/// Performs the release melee engagement slot operation for Monster Actor Controller.
	/// Uses the supplied arguments and current node state; any result is applied through side effects, events, or stored fields.
	/// </summary>
	private void ReleaseMeleeEngagementSlot(
		HeroActorController? target)
	{
		if (target is null
			|| !GodotObject.IsInstanceValid(target))
		{
			return;
		}

		target.MeleeEngagementSlots.Release(this);
	}

	/// <summary>
	/// Updates current target and applies the new value to the owning system.
	/// Uses the supplied arguments and current node state; any result is applied through side effects, events, or stored fields.
	/// </summary>
	private void SetCurrentTarget(
		HeroActorController? target)
	{
		if (CurrentTarget == target)
			return;

		ReleaseMeleeEngagementSlot(CurrentTarget);
		CurrentTarget = target;

		if (IsValidHeroTarget(target))
		{
			TryGetMeleeEngagementPosition(
				target!,
				out _);
		}
	}

	/// <summary>
	/// Retrieves body clearance distance from the current game state.
	/// Uses the supplied arguments and current state and returns the resulting float to the caller.
	/// </summary>
	private float GetBodyClearanceDistance(
		HeroActorController target)
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
	private float GetRequiredAttackDistance(HeroActorController target)
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
	/// Performs the is target within attack range operation for Monster Actor Controller.
	/// Uses the supplied arguments and current state and returns the resulting bool to the caller.
	/// </summary>
	private bool IsTargetWithinAttackRange(HeroActorController target)
	{
		float minimumCenterDistance =
			GetBodyClearanceDistance(target);

		float requiredCenterDistance =
			GetRequiredAttackDistance(target);

		float horizontalDistance = Mathf.Abs(GlobalPosition.X - target.GlobalPosition.X);

		float scaledTolerance =
			CombatArrivalDistance
			* CombatPresentationScale;

		return horizontalDistance
			>= minimumCenterDistance - scaledTolerance
			&& horizontalDistance
			<= requiredCenterDistance + scaledTolerance;
	}

	/// <summary>
	/// Performs the is vertically aligned operation for Monster Actor Controller.
	/// Uses the supplied arguments and current state and returns the resulting bool to the caller.
	/// </summary>
	private bool IsVerticallyAligned(HeroActorController target)
	{
		return Mathf.Abs(
			GlobalPosition.Y
			- target.GlobalPosition.Y)
			<= CombatArrivalDistance;
	}

	/// <summary>
	/// Performs the is aligned for current engagement operation for Monster Actor Controller.
	/// Uses the supplied arguments and current state and returns the resulting bool to the caller.
	/// </summary>
	private bool IsAlignedForCurrentEngagement(
		HeroActorController target)
	{
		return HasMeleeEngagementReservation(target)
			? IsTargetWithinMeleeEngagementRange(target)
			: IsVerticallyAligned(target);
	}

	/// <summary>
	/// Performs the is target within ability range operation for Monster Actor Controller.
	/// Uses the supplied arguments and current state and returns the resulting bool to the caller.
	/// </summary>
	private bool IsTargetWithinAbilityRange(
		HeroActorController target,
		AbilityDefinition ability)
	{
		float horizontalDistance =
			Mathf.Abs(
				GlobalPosition.X
				- target.GlobalPosition.X);

		return horizontalDistance
			<= ability.Range
			+ CombatArrivalDistance;
	}

	/// <summary>
	/// Recalculates ability cooldowns from the latest runtime state.
	/// Uses the supplied arguments and current node state; any result is applied through side effects, events, or stored fields.
	/// </summary>
	private void UpdateAbilityCooldowns(double delta)
	{
		foreach (AbilityDefinition ability in _abilities)
		{
			if (!_abilityCooldowns.TryGetValue(
				ability,
				out double remaining)
				|| remaining <= 0.0)
			{
				continue;
			}

			_abilityCooldowns[ability] =
				System.Math.Max(0.0, remaining - delta);
		}
	}

	/// <summary>
	/// Performs the find ready ability operation for Monster Actor Controller.
	/// Reads the current state and returns the resulting ability definition to the caller.
	/// </summary>
	private AbilityDefinition? FindReadyAbility()
	{
		if (!IsValidHeroTarget(CurrentTarget))
			return null;

		HeroActorController target = CurrentTarget!;

		foreach (AbilityDefinition ability in _abilities)
		{
			if (ability.TargetMode
				!= AbilityTargetMode.CurrentTarget)
			{
				continue;
			}

			if (_abilityCooldowns.TryGetValue(
				ability,
				out double remaining)
				&& remaining > 0.0)
			{
				continue;
			}

			if (!IsTargetWithinAbilityRange(
				target,
				ability))
			{
				continue;
			}

			if (!IsAlignedForCurrentEngagement(target))
				continue;

			return ability;
		}

		return null;
	}

	/// <summary>
	/// Performs the calculate approach position operation for Monster Actor Controller.
	/// Uses the supplied arguments and current state and returns the resulting vector2 to the caller.
	/// </summary>
	private Vector2 CalculateApproachPosition(
	HeroActorController target)
	{
		float requiredCenterDistance =
			GetRequiredAttackDistance(target);

		float horizontalDifference =
			target.GlobalPosition.X
			- GlobalPosition.X;

		float destinationX =
			target.GlobalPosition.X
			- Mathf.Sign(horizontalDifference)
			* requiredCenterDistance;

		return new Vector2(
			destinationX,
			target.GlobalPosition.Y);
	}

	/// <summary>
	/// Recalculates facing toward target from the latest runtime state.
	/// Uses the current node and service state; any result is applied through side effects, events, or stored fields.
	/// </summary>
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

		DebugLog.Print(
			$"{Name} now faces {Facing} toward " +
			$"{CurrentTarget.Name}.");
	}

	/// <summary>
	/// Recalculates combat approach from the latest runtime state.
	/// Uses the supplied arguments and current node state; any result is applied through side effects, events, or stored fields.
	/// </summary>
	private void UpdateCombatApproach(double delta)
	{
		if (!IsValidHeroTarget(CurrentTarget))
		{
			SetCurrentTarget(null);
			_state = MonsterState.WaitingForTarget;
			return;
		}

		HeroActorController target =
			CurrentTarget!;

		if (StatusEffects.PreventsMovement)
			return;

		if (TryGetMeleeEngagementPosition(
			target,
			out Vector2 meleeEngagementPosition))
		{
			if (HasReachedMeleeEngagementPosition(
				target,
				meleeEngagementPosition))
			{
				_attackCooldownRemaining = 0.0;
				_state = MonsterState.WaitingToAttack;
				return;
			}

			float meleeMovementDistance =
				CombatProfile.MoveSpeed * (float)delta;

			Vector2 previousPosition = GlobalPosition;

			GlobalPosition = GlobalPosition.MoveToward(
				meleeEngagementPosition,
				meleeMovementDistance);

			TrackMonsterMovement(previousPosition);

			if (!HasReachedMeleeEngagementPosition(
				target,
				meleeEngagementPosition))
			{
				return;
			}

			_attackCooldownRemaining = 0.0;
			_state = MonsterState.WaitingToAttack;

			DebugLog.Print(
				$"{Name} entered its melee slot for " +
				$"{target.Name}.");

			return;
		}

		if (IsTargetWithinAttackRange(target)
			&& IsVerticallyAligned(target))
		{
			_attackCooldownRemaining = 0.0;
			_state = MonsterState.WaitingToAttack;
			return;
		}

		Vector2 approachPosition =
			CalculateApproachPosition(target);

		float movementDistance =
			CombatProfile.MoveSpeed * (float)delta;

		Vector2 previousApproachPosition = GlobalPosition;

		GlobalPosition = GlobalPosition.MoveToward(
			approachPosition,
			movementDistance);

		TrackMonsterMovement(previousApproachPosition);

		if (!IsTargetWithinAttackRange(target)
			|| !IsVerticallyAligned(target))
		{
			return;
		}

		_attackCooldownRemaining = 0.0;
		_state = MonsterState.WaitingToAttack;

		DebugLog.Print(
			$"{Name} entered attack range for " +
			$"{target.Name}.");
	}

	/// <summary>
	/// Recalculates waiting to attack from the latest runtime state.
	/// Uses the supplied arguments and current node state; any result is applied through side effects, events, or stored fields.
	/// </summary>
	private void UpdateWaitingToAttack(double delta)
	{
		StopAttackPresentation();

		if (!IsValidHeroTarget(CurrentTarget))
		{
			SetCurrentTarget(null);
			_state = MonsterState.WaitingForTarget;
			return;
		}

		HeroActorController target = CurrentTarget!;

		bool hasMeleeReservation =
			HasMeleeEngagementReservation(target);

		bool targetMovedOutOfRange =
			hasMeleeReservation
				? !IsTargetWithinMeleeEngagementRange(target)
				: !IsTargetWithinAttackRange(target)
					|| !IsVerticallyAligned(target);

		if (targetMovedOutOfRange)
		{
			_state = MonsterState.ApproachingTarget;
			return;
		}

		if (!StatusEffects.PreventsAbilities)
		{
			AbilityDefinition? readyAbility =
				FindReadyAbility();

			if (readyAbility is not null)
			{
				BeginAbility(readyAbility);
				return;
			}
		}

		_attackCooldownRemaining =
			System.Math.Max(
				0.0,
				_attackCooldownRemaining - delta);

		if (_attackCooldownRemaining > 0.0
			|| StatusEffects.PreventsBasicAttacks)
		{
			return;
		}

		BeginAttack();
	}

	/// <summary>
	/// Attempts to acquire target without throwing when the operation cannot be completed.
	/// Uses the supplied arguments and current state and returns the resulting bool to the caller.
	/// </summary>
	public bool TryAcquireTarget(HeroActorController target)
	{
		if (IsDead)
			return false;

		if (!IsValidHeroTarget(target))
			return false;

		if (HasForcedTarget
			&& target != _forcedTarget)
		{
			return false;
		}

		if (HasValidTarget)
			return false;

		SetCurrentTarget(target);
		_state = MonsterState.ApproachingTarget;

		DebugLog.Print(
			$"{Name} locked onto {target.Name}.");

		return true;
	}

	/// <summary>
	/// Attempts to switch target without throwing when the operation cannot be completed.
	/// Uses the supplied arguments and current state and returns the resulting bool to the caller.
	/// </summary>
	public bool TrySwitchTarget(HeroActorController target)
	{
		if (IsDead
			|| HasForcedTarget
			|| !IsValidHeroTarget(target)
			|| CurrentTarget == target)
		{
			return false;
		}

		SetCurrentTarget(target);

		_attackCooldownRemaining = 0.0;
		_attackTimeRemaining = 0.0;
		_attackReleaseEmitted = false;
		ClearAbilityCast();
		StopAttackPresentation();

		_state = MonsterState.ApproachingTarget;

		DebugLog.Print(
			$"{Name} switched aggro to {target.Name}.");

		return true;
	}

	/// <summary>
	/// Recalculates forced target from the latest runtime state.
	/// Uses the supplied arguments and current node state; any result is applied through side effects, events, or stored fields.
	/// </summary>
	private void UpdateForcedTarget(double delta)
	{
		if (_forcedTarget is null)
			return;

		if (!IsValidHeroTarget(_forcedTarget))
		{
			EndForcedTarget();

			return;
		}

		_forcedTargetTimeRemaining -= delta;

		if (_forcedTargetTimeRemaining > 0.0)
			return;

		EndForcedTarget();
	}

	/// <summary>
	/// Performs the end forced target operation for Monster Actor Controller.
	/// Uses the current node and service state; any result is applied through side effects, events, or stored fields.
	/// </summary>
	private void EndForcedTarget()
	{
		_forcedTarget = null;
		_forcedTargetTimeRemaining = 0.0;
		SetCurrentTarget(null);

		_attackCooldownRemaining = 0.0;
		_attackTimeRemaining = 0.0;
		_attackReleaseEmitted = false;
		ClearAbilityCast();
		StopAttackPresentation();

		if (!IsDead)
			_state = MonsterState.WaitingForTarget;

		EmitSignal(
			SignalName.ForcedTargetEnded,
			this);
	}

	/// <summary>
	/// Performs the begin attack operation for Monster Actor Controller.
	/// Uses the current node and service state; any result is applied through side effects, events, or stored fields.
	/// </summary>
	private void BeginAttack()
	{
		if (!IsValidHeroTarget(CurrentTarget)
			|| StatusEffects.PreventsBasicAttacks)
		{
			return;
		}

		_state = MonsterState.Attacking;

		_attackTimeRemaining = CombatProfile.AttackDuration;

		_attackReleaseEmitted = false;

		StopAttackPresentation();

		DebugLog.Print(
			$"{Name} began attacking {CurrentTarget!.Name}.");
	}

	/// <summary>
	/// Performs the begin ability operation for Monster Actor Controller.
	/// Uses the supplied arguments and current node state; any result is applied through side effects, events, or stored fields.
	/// </summary>
	private void BeginAbility(AbilityDefinition ability)
	{
		if (!IsValidHeroTarget(CurrentTarget)
			|| StatusEffects.PreventsAbilities)
		{
			return;
		}

		_activeAbility = ability;
		_abilityCastTimeRemaining =
			ability.CastTimeSeconds;
		_state = MonsterState.UsingAbility;

		StopAttackPresentation();

		DebugLog.Print(
			$"{Name} began using ability " +
			$"'{ability.DisplayName}' on " +
			$"{CurrentTarget!.Name}. " +
			$"Cast={ability.CastTimeSeconds:0.##}s.");
	}

	/// <summary>
	/// Recalculates ability from the latest runtime state.
	/// Uses the supplied arguments and current node state; any result is applied through side effects, events, or stored fields.
	/// </summary>
	private void UpdateAbility(double delta)
	{
		if (_activeAbility is null
			|| !GodotObject.IsInstanceValid(_activeAbility))
		{
			ClearAbilityCast();
			_state = MonsterState.WaitingToAttack;
			return;
		}

		AbilityDefinition ability = _activeAbility;

		if (!IsValidHeroTarget(CurrentTarget))
		{
			ClearAbilityCast();
			SetCurrentTarget(null);
			_state = MonsterState.WaitingForTarget;
			return;
		}

		HeroActorController target = CurrentTarget!;

		if (!IsTargetWithinAbilityRange(target, ability)
			|| !IsAlignedForCurrentEngagement(target))
		{
			DebugLog.Print(
				$"{Name} canceled '{ability.DisplayName}' " +
				$"because {target.Name} moved out of range.");

			ClearAbilityCast();
			_state = MonsterState.ApproachingTarget;
			return;
		}

		_abilityCastTimeRemaining -= delta;

		if (_abilityCastTimeRemaining > 0.0)
			return;

		_abilityCooldowns[ability] =
			ability.CooldownSeconds;

		DebugLog.Print(
			$"{Name} released ability " +
			$"'{ability.DisplayName}' on {target.Name}.");

		EmitSignal(
			SignalName.AbilityReleased,
			this,
			target,
			ability);

		if (_state != MonsterState.UsingAbility)
		{
			ClearAbilityCast();
			return;
		}

		EndAbility();
	}

	/// <summary>
	/// Performs the end ability operation for Monster Actor Controller.
	/// Uses the current node and service state; any result is applied through side effects, events, or stored fields.
	/// </summary>
	private void EndAbility()
	{
		ClearAbilityCast();

		_attackCooldownRemaining =
			CombatProfile.AttackInterval;

		if (!IsValidHeroTarget(CurrentTarget))
		{
			SetCurrentTarget(null);
			_state = MonsterState.WaitingForTarget;
			return;
		}

		HeroActorController target = CurrentTarget!;

		bool hasMeleeReservation =
			HasMeleeEngagementReservation(target);

		bool targetStillInRange =
			hasMeleeReservation
				? IsTargetWithinMeleeEngagementRange(target)
				: IsTargetWithinAttackRange(target)
					&& IsVerticallyAligned(target);

		_state =
			targetStillInRange
				? MonsterState.WaitingToAttack
				: MonsterState.ApproachingTarget;
	}

	/// <summary>
	/// Resets ability cast so the system can begin from a clean state.
	/// Uses the current node and service state; any result is applied through side effects, events, or stored fields.
	/// </summary>
	private void ClearAbilityCast()
	{
		_activeAbility = null;
		_abilityCastTimeRemaining = 0.0;
	}

	/// <summary>
	/// Cancels and resets an in-progress combat action when an active control
	/// status explicitly interrupts that action type. Interrupted abilities do
	/// not start their cooldown; interrupted basic attacks do not release damage
	/// if their release point has not already occurred.
	/// </summary>
	private void InterruptControlledActionIfNeeded()
	{
		if (_state == MonsterState.Attacking
			&& StatusEffects.InterruptsBasicAttacks)
		{
			_attackTimeRemaining = 0.0;
			_attackCooldownRemaining = 0.0;
			_attackReleaseEmitted = false;
			StopAttackPresentation();
			ResetStateAfterInterruptedAction();

			DebugLog.Print(
				$"{Name}'s basic attack was interrupted " +
				"and reset by a control status.");

			return;
		}

		if (_state != MonsterState.UsingAbility
			|| !StatusEffects.InterruptsAbilities)
		{
			return;
		}

		string abilityName =
			_activeAbility?.DisplayName
			?? "Unknown Ability";

		ClearAbilityCast();
		_attackCooldownRemaining = 0.0;
		StopAttackPresentation();
		ResetStateAfterInterruptedAction();

		DebugLog.Print(
			$"{Name}'s ability '{abilityName}' was interrupted " +
			"and reset by a control status.");
	}

	private void ResetStateAfterInterruptedAction()
	{
		if (!IsValidHeroTarget(CurrentTarget))
		{
			SetCurrentTarget(null);
			_state = MonsterState.WaitingForTarget;
			return;
		}

		HeroActorController target = CurrentTarget!;

		bool hasMeleeReservation =
			HasMeleeEngagementReservation(target);

		bool targetStillInRange =
			hasMeleeReservation
				? IsTargetWithinMeleeEngagementRange(target)
				: IsTargetWithinAttackRange(target)
					&& IsVerticallyAligned(target);

		_state =
			targetStillInRange
				? MonsterState.WaitingToAttack
				: MonsterState.ApproachingTarget;
	}

	/// <summary>
	/// Recalculates attack from the latest runtime state.
	/// Uses the supplied arguments and current node state; any result is applied through side effects, events, or stored fields.
	/// </summary>
	private void UpdateAttack(double delta)
	{
		if (!IsValidHeroTarget(CurrentTarget))
		{
			EndAttack();
			return;
		}

		_attackTimeRemaining -= delta;

		float duration = Mathf.Max(CombatProfile.AttackDuration, 0.001f);

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

	/// <summary>
	/// Gives an active forced-movement status temporary ownership of this
	/// monster's movement. Threat and CurrentTarget are deliberately preserved
	/// so normal combat can resume when the status expires.
	/// </summary>
	private bool TryUpdateForcedMovement(double delta)
	{
		if (!StatusEffects.TryGetForcedMovementEffect(
			out CombatStatusEffectInstance effect))
		{
			ResetForcedMovementRuntime();
			return false;
		}

		CombatStatusEffectDefinition definition = effect.Definition;

		if (!ReferenceEquals(_activeForcedMovementEffect, effect))
			BeginForcedMovement(effect);

		Vector2 direction = definition.ForcedMovementMode switch
		{
			CombatForcedMovementMode.AwayFromSource =>
				GetAwayFromSourceDirection(effect),

			CombatForcedMovementMode.Panic =>
				GetPanicDirection(effect, delta),

			_ => Vector2.Zero
		};

		if (direction.LengthSquared() <= 0.0001f)
		{
			ResetForcedMovementRuntime();
			return false;
		}

		ReleaseMeleeEngagementSlot(CurrentTarget);

		float movementDistance =
			Mathf.Max(0.0f, CombatProfile.MoveSpeed)
			* Mathf.Max(0.0f, definition.ForcedMovementSpeedMultiplier)
			* (float)System.Math.Max(delta, 0.0);

		Vector2 candidate =
			GlobalPosition + direction * movementDistance;

		if (definition.ForcedMovementMode
			== CombatForcedMovementMode.Panic)
		{
			candidate = ApplyPanicLeash(
				candidate,
				definition.PanicLeashDistance);
		}

		Vector2 contained =
			_sceneBoundaries.ClampToScene(candidate);

		bool hitBoundary =
			!contained.IsEqualApprox(candidate);

		Vector2 previousForcedMovementPosition = GlobalPosition;
		GlobalPosition = contained;
		TrackMonsterMovement(previousForcedMovementPosition);

		if (definition.ForcedMovementMode
			== CombatForcedMovementMode.Panic
			&& hitBoundary)
		{
			_panicDirectionTimeRemaining = 0.0;
		}

		if (Mathf.Abs(direction.X) > FacingDeadZone)
		{
			Facing = direction.X < 0.0f
				? FacingDirection.Left
				: FacingDirection.Right;
		}

		StopAttackPresentation();
		return true;
	}

	private void BeginForcedMovement(
		CombatStatusEffectInstance effect)
	{
		_activeForcedMovementEffect = effect;
		_panicStartPosition = GlobalPosition;
		_panicDirection = Vector2.Zero;
		_panicDirectionTimeRemaining = 0.0;

		if (effect.Definition.ForcedMovementMode
			== CombatForcedMovementMode.Panic)
		{
			ChooseNewPanicDirection(effect.Definition);
		}
	}

	private Vector2 GetAwayFromSourceDirection(
		CombatStatusEffectInstance effect)
	{
		CombatStatusEffectApplicationContext context =
			effect.ApplicationContext;

		Vector2 sourcePosition = context.OriginPosition;

		if (context.SourceActor is Node2D sourceActor
			&& GodotObject.IsInstanceValid(sourceActor)
			&& sourceActor.IsInsideTree())
		{
			sourcePosition = sourceActor.GlobalPosition;
		}

		Vector2 awayDirection =
			GlobalPosition - sourcePosition;

		if (awayDirection.LengthSquared() <= 0.0001f)
		{
			return Facing == FacingDirection.Right
				? Vector2.Left
				: Vector2.Right;
		}

		return awayDirection.Normalized();
	}

	private Vector2 GetPanicDirection(
		CombatStatusEffectInstance effect,
		double delta)
	{
		CombatStatusEffectDefinition definition =
			effect.Definition;

		_panicDirectionTimeRemaining -=
			System.Math.Max(delta, 0.0);

		float leashDistance =
			Mathf.Max(1.0f, definition.PanicLeashDistance);

		Vector2 fromStart =
			GlobalPosition - _panicStartPosition;

		bool atLeash =
			fromStart.LengthSquared()
			>= leashDistance * leashDistance;

		bool movingFartherOut =
			atLeash
			&& fromStart.LengthSquared() > 0.0001f
			&& _panicDirection.Dot(fromStart.Normalized()) > 0.0f;

		if (_panicDirectionTimeRemaining <= 0.0
			|| _panicDirection.LengthSquared() <= 0.0001f
			|| movingFartherOut)
		{
			ChooseNewPanicDirection(
				definition,
				atLeash ? -fromStart : Vector2.Zero);
		}

		return _panicDirection;
	}

	private void ChooseNewPanicDirection(
		CombatStatusEffectDefinition definition,
		Vector2 preferredInwardDirection = default)
	{
		Vector2 direction;

		if (preferredInwardDirection.LengthSquared() > 0.0001f)
		{
			Vector2 inward = preferredInwardDirection.Normalized();
			float jitterAngle =
				_forcedMovementRandom.RandfRange(-0.65f, 0.65f);
			direction = inward.Rotated(jitterAngle);
		}
		else
		{
			float angle =
				_forcedMovementRandom.RandfRange(0.0f, Mathf.Tau);
			direction = Vector2.FromAngle(angle);
		}

		if (direction.LengthSquared() <= 0.0001f)
		{
			direction = Facing == FacingDirection.Right
				? Vector2.Left
				: Vector2.Right;
		}

		_panicDirection = direction.Normalized();

		float minSeconds =
			Mathf.Max(
				0.05f,
				definition.PanicDirectionChangeMinSeconds);

		float maxSeconds =
			Mathf.Max(
				minSeconds,
				definition.PanicDirectionChangeMaxSeconds);

		_panicDirectionTimeRemaining =
			_forcedMovementRandom.RandfRange(
				minSeconds,
				maxSeconds);
	}

	private Vector2 ApplyPanicLeash(
		Vector2 candidatePosition,
		float leashDistance)
	{
		float maxDistance = Mathf.Max(1.0f, leashDistance);

		Vector2 fromStart =
			candidatePosition - _panicStartPosition;

		if (fromStart.LengthSquared()
			<= maxDistance * maxDistance)
		{
			return candidatePosition;
		}

		_panicDirectionTimeRemaining = 0.0;

		return _panicStartPosition
			+ fromStart.Normalized()
			* maxDistance;
	}

	private void ResetForcedMovementRuntime()
	{
		_activeForcedMovementEffect = null;
		_panicStartPosition = Vector2.Zero;
		_panicDirection = Vector2.Zero;
		_panicDirectionTimeRemaining = 0.0;
	}

	/// <summary>
	/// Updates Monster Actor Controller every rendered frame using the supplied frame delta.
	/// Uses the supplied arguments and current node state; any result is applied through side effects, events, or stored fields.
	/// </summary>
	public override void _Process(double delta)
	{
		BeginMonsterAnimationFrame();
		StatusEffects.Update(delta);

		if (!IsDead)
			InterruptControlledActionIfNeeded();

		if (!IsDead)
			UpdateForcedTarget(delta);

		if (!IsDead)
			UpdateAbilityCooldowns(delta);

		if (!IsDead
			&& TryUpdateForcedMovement(delta))
		{
			UpdateMonsterAnimation(delta);
			PositionHealthBar();
			return;
		}

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

			case MonsterState.UsingAbility:
				UpdateAbility(delta);
				break;

			case MonsterState.Dead:
				break;
		}

		UpdateMonsterAnimation(delta);
		PositionHealthBar();
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

	/// <summary>
	/// Performs the end attack operation for Monster Actor Controller.
	/// Uses the current node and service state; any result is applied through side effects, events, or stored fields.
	/// </summary>
	private void EndAttack()
	{
		StopAttackPresentation();

		_attackTimeRemaining = 0.0;
		_attackCooldownRemaining =
			CombatProfile.AttackInterval;

		if (!IsValidHeroTarget(CurrentTarget))
		{
			SetCurrentTarget(null);
			_state = MonsterState.WaitingForTarget;
			return;
		}

		HeroActorController target = CurrentTarget!;

		bool hasMeleeReservation =
			HasMeleeEngagementReservation(target);

		bool targetStillInRange =
			hasMeleeReservation
				? IsTargetWithinMeleeEngagementRange(target)
				: IsTargetWithinAttackRange(target)
					&& IsVerticallyAligned(target);

		_state =
			targetStillInRange
				? MonsterState.WaitingToAttack
				: MonsterState.ApproachingTarget;
	}

	/// <summary>
	/// Performs the stop attack presentation operation for Monster Actor Controller.
	/// Uses the current node and service state; any result is applied through side effects, events, or stored fields.
	/// </summary>
	private void StopAttackPresentation()
	{
		VisualRoot.Position = _visualRestPosition;
	}

	/// <summary>
	/// Cleans up Monster Actor Controller when the node leaves the scene tree.
	/// Uses the current node and service state; any result is applied through side effects, events, or stored fields.
	/// </summary>
	public override void _ExitTree()
	{
		ShutdownMonsterAnimation();
		ResetForcedMovementRuntime();
		SetCurrentTarget(null);
		MeleeEngagementSlots.Clear();
	}
}
