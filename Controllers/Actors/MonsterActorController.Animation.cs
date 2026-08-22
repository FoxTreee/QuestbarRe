using Godot;

public partial class MonsterActorController
{
    private static readonly StringName IdleAnimationName = new("idle");
    private static readonly StringName WalkAnimationName = new("walk");
    private static readonly StringName AttackAnimationName = new("attack");
    private static readonly StringName HurtAnimationName = new("hurt");

    [ExportCategory("Monster Animation")]

    /// <summary>
    /// Optional animated monster artwork. Assign an AnimatedSprite2D under
    /// PresentationRoot/VisualRoot with idle, walk, attack, and hurt clips.
    /// Existing placeholder monsters continue working when this is empty.
    /// </summary>
    [Export]
    public AnimatedSprite2D? MonsterSprite { get; set; }

    /// <summary>
    /// Enable this when the imported source frames naturally face right.
    /// Leave it disabled for artwork authored facing left.
    /// </summary>
    [Export]
    public bool SpriteFramesFaceRight { get; set; }

    private bool _movedThisFrame;
    private float _lastObservedHealth;
    private double _hurtAnimationRemaining;


    /// <summary>
    /// Binds damage reactions and starts the animation matching the monster's
    /// initial gameplay state.
    /// </summary>
    private void InitializeMonsterAnimation()
    {
        _lastObservedHealth = Health.CurrentHealth;
        Health.HealthChanged += OnMonsterHealthChanged;

        UpdateMonsterSpriteFacing();
        SyncMonsterAnimationToActivity(forceRestart: true);
    }


    private void ShutdownMonsterAnimation()
    {
        Health.HealthChanged -= OnMonsterHealthChanged;
    }


    private void BeginMonsterAnimationFrame()
    {
        _movedThisFrame = false;
    }


    /// <summary>
    /// Records real actor movement so walk plays only while position actually
    /// changes, including panic and other forced movement effects.
    /// </summary>
    private void TrackMonsterMovement(Vector2 previousPosition)
    {
        _movedThisFrame |=
            !GlobalPosition.IsEqualApprox(previousPosition);
    }


    /// <summary>
    /// Begins hurt only after the authoritative health model reports an actual
    /// decrease. Healing and maximum-health refreshes do not trigger it.
    /// </summary>
    private void OnMonsterHealthChanged(
        float currentHealth,
        float maximumHealth)
    {
        bool tookDamage =
            currentHealth < _lastObservedHealth;

        _lastObservedHealth = currentHealth;

        if (!tookDamage
            || IsDead
            || !HasMonsterAnimation(HurtAnimationName))
        {
            return;
        }

        _hurtAnimationRemaining =
            GetMonsterAnimationDuration(HurtAnimationName);

        PlayMonsterAnimation(
            HurtAnimationName,
            forceRestart: true);
    }


    /// <summary>
    /// Preserves a damage reaction until its authored clip completes, then
    /// returns to the animation selected from current combat activity.
    /// </summary>
    private void UpdateMonsterAnimation(double delta)
    {
        UpdateMonsterSpriteFacing();

        if (_hurtAnimationRemaining > 0.0)
        {
            _hurtAnimationRemaining =
                System.Math.Max(
                    0.0,
                    _hurtAnimationRemaining - delta);

            if (_hurtAnimationRemaining > 0.0)
                return;
        }

        SyncMonsterAnimationToActivity();
    }


    /// <summary>
    /// Selects animation from real activity: movement wins over passive states,
    /// attacks and ability casts use attack, and everything else uses idle.
    /// </summary>
    private void SyncMonsterAnimationToActivity(
        bool forceRestart = false)
    {
        if (!GodotObject.IsInstanceValid(MonsterSprite)
            || MonsterSprite!.SpriteFrames is null)
        {
            return;
        }

        StringName desiredAnimation;
        float speedScale = 1.0f;

        if (_movedThisFrame)
        {
            desiredAnimation = WalkAnimationName;
        }
        else if (_state == MonsterState.Attacking)
        {
            desiredAnimation = AttackAnimationName;
            speedScale = CalculateActionAnimationSpeedScale(
                CombatProfile.AttackDuration);
        }
        else if (_state == MonsterState.UsingAbility)
        {
            desiredAnimation = AttackAnimationName;
            speedScale = CalculateActionAnimationSpeedScale(
                _activeAbility?.CastTimeSeconds
                    ?? CombatProfile.AttackDuration);
        }
        else
        {
            desiredAnimation = IdleAnimationName;
        }

        PlayMonsterAnimation(
            desiredAnimation,
            forceRestart,
            speedScale);
    }


    private bool HasMonsterAnimation(StringName animationName)
    {
        return GodotObject.IsInstanceValid(MonsterSprite)
            && MonsterSprite!.SpriteFrames is not null
            && MonsterSprite.SpriteFrames.HasAnimation(animationName)
            && MonsterSprite.SpriteFrames.GetFrameCount(animationName) > 0;
    }


    /// <summary>
    /// Plays only when the requested clip changes, unless a real event such as
    /// taking damage explicitly requests a restart.
    /// </summary>
    private void PlayMonsterAnimation(
        StringName animationName,
        bool forceRestart = false,
        float speedScale = 1.0f)
    {
        if (!HasMonsterAnimation(animationName))
            return;

        MonsterSprite!.SpeedScale =
            Mathf.Max(speedScale, 0.01f);

        bool animationChanged =
            MonsterSprite.Animation != animationName;

        if (forceRestart)
        {
            MonsterSprite.Stop();
            MonsterSprite.Play(animationName);
            MonsterSprite.SetFrameAndProgress(0, 0.0f);
            return;
        }

        if (animationChanged)
        {
            MonsterSprite.Play(animationName);
            return;
        }

        bool loopingAnimation =
            animationName == IdleAnimationName
            || animationName == WalkAnimationName;

        if (loopingAnimation
            && !MonsterSprite.IsPlaying())
        {
            MonsterSprite.Play(animationName);
        }
    }


    /// <summary>
    /// Matches the attack clip to the authoritative basic-attack duration or
    /// ability cast time. AttackReleasePoint therefore stays on the same
    /// normalized place in the visible animation.
    /// </summary>
    private float CalculateActionAnimationSpeedScale(
        float actionDuration)
    {
        float clipDuration =
            GetMonsterAnimationDuration(AttackAnimationName);

        return Mathf.Clamp(
            clipDuration
                / Mathf.Max(actionDuration, 0.01f),
            0.05f,
            20.0f);
    }


    private float GetMonsterAnimationDuration(
        StringName animationName)
    {
        if (!HasMonsterAnimation(animationName))
            return 0.01f;

        SpriteFrames frames = MonsterSprite!.SpriteFrames;
        int frameCount = frames.GetFrameCount(animationName);
        float framesPerSecond =
            (float)frames.GetAnimationSpeed(animationName);
        float relativeDuration = 0.0f;

        for (int frameIndex = 0;
            frameIndex < frameCount;
            frameIndex++)
        {
            relativeDuration +=
                frames.GetFrameDuration(
                    animationName,
                    frameIndex);
        }

        return relativeDuration
            / Mathf.Max(framesPerSecond, 0.01f);
    }


    /// <summary>
    /// Mirrors the source artwork only when its authored facing differs from
    /// the monster's real movement or target-facing direction.
    /// </summary>
    private void UpdateMonsterSpriteFacing()
    {
        if (!GodotObject.IsInstanceValid(MonsterSprite))
            return;

        MonsterSprite!.FlipH = SpriteFramesFaceRight
            ? Facing == FacingDirection.Left
            : Facing == FacingDirection.Right;
    }
}
