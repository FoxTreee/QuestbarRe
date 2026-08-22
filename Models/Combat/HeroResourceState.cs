using Godot;

public sealed class HeroResourceState
{
    private double _regenerationElapsedSeconds;

    public HeroResourceType ResourceType { get; private set; } = HeroResourceType.None;
    public float CurrentAmount { get; private set; }
    public float MaximumAmount { get; private set; }
    public bool HasResource => ResourceType != HeroResourceType.None;
    public bool IsLocked { get; private set; }

    /// <summary>
    /// Initializes runtime resource state from class data. The configured
    /// starting rule determines whether the pool begins full or empty.
    /// </summary>
    public void Configure(HeroResourceDefinition? definition)
    {
        _regenerationElapsedSeconds = 0.0;

        if (!GodotObject.IsInstanceValid(definition)
            || definition!.ResourceType == HeroResourceType.None)
        {
            ResourceType = HeroResourceType.None;
            CurrentAmount = 0.0f;
            MaximumAmount = 0.0f;
            return;
        }

        ResourceType = definition.ResourceType;
        MaximumAmount = Mathf.Max(definition.MaximumAmount, 0.0f);
        CurrentAmount = definition.StartFull ? MaximumAmount : 0.0f;
    }

    /// <summary>
    /// Advances interval-based regeneration and applies every completed tick.
    /// Resource is always clamped to its configured maximum.
    /// </summary>
    public void Update(double delta, HeroResourceDefinition? definition)
    {
        if (IsLocked
            || !HasResource
            || !GodotObject.IsInstanceValid(definition)
            || definition!.RegenerationAmount <= 0.0f
            || definition.RegenerationIntervalSeconds <= 0.0f
            || CurrentAmount >= MaximumAmount)
        {
            return;
        }

        _regenerationElapsedSeconds += System.Math.Max(delta, 0.0);

        while (_regenerationElapsedSeconds >= definition.RegenerationIntervalSeconds)
        {
            _regenerationElapsedSeconds -= definition.RegenerationIntervalSeconds;
            CurrentAmount = Mathf.Min(
                CurrentAmount + definition.RegenerationAmount,
                MaximumAmount);

            if (CurrentAmount >= MaximumAmount)
            {
                _regenerationElapsedSeconds = 0.0;
                break;
            }
        }
    }

    /// <summary>
    /// Attempts to spend resource atomically. It returns false and changes
    /// nothing when the hero lacks a resource pool or cannot afford the cost.
    /// </summary>
    public bool TrySpend(float amount)
    {
        if (IsLocked)
            return HasResource && amount >= 0.0f;

        if (!HasResource || amount < 0.0f || CurrentAmount < amount)
            return false;

        CurrentAmount -= amount;
        return true;
    }

    /// <summary>
    /// Restores resource without exceeding its maximum. This supports future
    /// effects such as potions while preserving the configured cap.
    /// </summary>
    public void Restore(float amount)
    {
        if (IsLocked || !HasResource || amount <= 0.0f)
            return;

        CurrentAmount = Mathf.Min(CurrentAmount + amount, MaximumAmount);
    }

    /// <summary>
    /// Refills the resource pool and restarts its regeneration interval. This
    /// is used when a hero is revived or explicitly reset.
    /// </summary>
    public void RestoreToMaximum()
    {
        CurrentAmount = MaximumAmount;
        _regenerationElapsedSeconds = 0.0;
    }

    /// <summary>
    /// Enables or disables the debug resource lock. Enabling it fills the
    /// active Rage, Energy, or Mana pool and makes ability spending free.
    /// </summary>
    public void SetLocked(bool locked)
    {
        if (locked && HasResource)
            RestoreToMaximum();

        IsLocked = locked;
    }
}
