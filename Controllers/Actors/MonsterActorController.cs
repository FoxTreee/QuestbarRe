using Godot;
using System.Collections.Generic;


public partial class MonsterActorController : Node2D
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

    public bool HasValidTarget => IsValidHeroTarget(CurrentTarget);

    [ExportCategory("Visuals")]
    [Export]
    public Node2D PresentationRoot { get; set; } = null!;

    [Export]
    public Node2D VisualRoot { get; set; } = null!;

    [Export]
    public BodyBounds2D BodyBounds { get; set; } = null!;

    [Export]
    public Marker2D ImpactOrigin { get; set; } = null!;

    [Export]
    public Marker2D HealthBarAnchor { get; set; } = null!;

    [Export(PropertyHint.Range, "0,32,1")]
    public float HealthBarGap { get; set; } = 18.0f;

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
    public float CombatPresentationScale { get; private set; } = 1.0f;

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

    public MonsterThreatState Threat { get; } = new();

    public bool HasTarget => IsValidHeroTarget(CurrentTarget);

    public void Configure(
        MonsterDefinition definition,
        IReadOnlyList<AbilityDefinition>? abilities = null)
    {
        if (!GodotObject.IsInstanceValid(definition))
        {
            throw new System.ArgumentNullException(
                nameof(definition));
        }

        Definition = definition;

        Threat.Clear();

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
        ClearAbilityCast();

        StopAttackPresentation();

        _state =
            MonsterState.WaitingForTarget;

        DebugLog.Print(
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
        ClearAbilityCast();

        StopAttackPresentation();

        DebugLog.Print(
            $"{Name} entered its Dead state.");

        EmitSignal(
            SignalName.Died,
            this);
    }

    private void ApplyDefinition()
    {
        CombatProfile.MaximumHealth = Definition.MaximumHealth;
        CombatProfile.AttackDamage = Definition.AttackDamage;
        CombatProfile.AttackRange = Definition.AttackRange;
        CombatProfile.CombatRadius = BodyBounds.GetHorizontalRadiusInParentSpace() * Mathf.Abs(VisualRoot.Scale.X);
        CombatProfile.AttackInterval = Definition.AttackInterval;
        CombatProfile.AttackDuration = Definition.AttackDuration;
        CombatProfile.AttackReleasePoint = Definition.AttackReleasePoint;
        CombatProfile.AttackLungeDistance = Definition.AttackLungeDistance;
        CombatProfile.MoveSpeed = Definition.CombatMoveSpeed;
        CombatProfile.AttackDelivery = Definition.AttackDelivery;
    }

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

        string targetPreference =
            Definition.PreferredTargetTags == HeroCombatTag.None
                ? "Any"
                : Definition.PreferredTargetTags.ToString();

        DebugLog.Print(
            $"{Name} initialized as " +
            $"{Definition.ContentId} " +
            $"('{Definition.DisplayName}') with " +
            $"{Health.CurrentHealth}/" +
            $"{Health.MaximumHealth} health. " +
            $"Target preference={targetPreference}; " +
            $"selection={Definition.TargetingStyle}.");
    }

    private static bool IsValidHeroTarget(HeroActorController? hero)
    {
        return hero is not null
            && GodotObject.IsInstanceValid(hero)
            && hero.IsInsideTree()
            && hero.Health.IsAlive
            && !hero.IsIncapacitated;
    }

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

    private bool IsVerticallyAligned(HeroActorController target)
    {
        return Mathf.Abs(
            GlobalPosition.Y
            - target.GlobalPosition.Y)
            <= CombatArrivalDistance;
    }

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

            if (!IsVerticallyAligned(target))
                continue;

            return ability;
        }

        return null;
    }

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

    private void UpdateCombatApproach(double delta)
    {
        if (!IsValidHeroTarget(CurrentTarget))
        {
            CurrentTarget = null;
            _state = MonsterState.WaitingForTarget;
            return;
        }

        HeroActorController target =
            CurrentTarget!;

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

        GlobalPosition = GlobalPosition.MoveToward(
            approachPosition,
            movementDistance);

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

    private void UpdateWaitingToAttack(double delta)
    {
        StopAttackPresentation();

        if (!IsValidHeroTarget(CurrentTarget))
        {
            CurrentTarget = null;
            _state = MonsterState.WaitingForTarget;
            return;
        }

        bool targetMovedOutOfRange =
            !IsTargetWithinAttackRange(CurrentTarget!)
            || !IsVerticallyAligned(CurrentTarget!);

        if (targetMovedOutOfRange)
        {
            _state = MonsterState.ApproachingTarget;
            return;
        }

        AbilityDefinition? readyAbility =
            FindReadyAbility();

        if (readyAbility is not null)
        {
            BeginAbility(readyAbility);
            return;
        }

        _attackCooldownRemaining -= delta;

        if (_attackCooldownRemaining > 0.0)
            return;

        BeginAttack();
    }

    public bool TryAcquireTarget(HeroActorController target)
    {
        if (IsDead)
            return false;

        if (!IsValidHeroTarget(target))
            return false;

        if (HasValidTarget)
            return false;

        CurrentTarget = target;
        _state = MonsterState.ApproachingTarget;

        DebugLog.Print(
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

        DebugLog.Print(
            $"{Name} began attacking {CurrentTarget!.Name}.");
    }

    private void BeginAbility(AbilityDefinition ability)
    {
        if (!IsValidHeroTarget(CurrentTarget))
            return;

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
            CurrentTarget = null;
            _state = MonsterState.WaitingForTarget;
            return;
        }

        HeroActorController target = CurrentTarget!;

        if (!IsTargetWithinAbilityRange(target, ability)
            || !IsVerticallyAligned(target))
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

    private void EndAbility()
    {
        ClearAbilityCast();

        _attackCooldownRemaining =
            CombatProfile.AttackInterval;

        if (!IsValidHeroTarget(CurrentTarget))
        {
            CurrentTarget = null;
            _state = MonsterState.WaitingForTarget;
            return;
        }

        bool targetStillInRange =
            IsTargetWithinAttackRange(CurrentTarget!)
            && IsVerticallyAligned(CurrentTarget!);

        _state =
            targetStillInRange
                ? MonsterState.WaitingToAttack
                : MonsterState.ApproachingTarget;
    }

    private void ClearAbilityCast()
    {
        _activeAbility = null;
        _abilityCastTimeRemaining = 0.0;
    }

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

    public override void _Process(double delta)
    {
        UpdateFacingTowardTarget();

        if (!IsDead)
            UpdateAbilityCooldowns(delta);

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

        PositionHealthBar();
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

        bool targetStillInRange =
            IsTargetWithinAttackRange(CurrentTarget!)
            && IsVerticallyAligned(CurrentTarget!);

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
