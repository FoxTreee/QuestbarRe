using Godot;
using System;

public partial class ActorHealthBarController : Node2D
{
    private ProgressBar _healthProgress = null!;
    private CombatHealthState? _health;

    public override void _Ready()
    {
        _healthProgress =
            GetNodeOrNull<ProgressBar>("HealthProgress")!;

        if (GodotObject.IsInstanceValid(_healthProgress))
            return;

        GD.PushError(
            $"{Name} requires a ProgressBar child named " +
            "'HealthProgress'.");
    }

    public override void _ExitTree()
    {
        Unbind();
    }

    public void Bind(CombatHealthState health)
    {
        ArgumentNullException.ThrowIfNull(health);

        Unbind();

        _health = health;
        _health.HealthChanged += OnHealthChanged;

        Refresh(
            _health.CurrentHealth,
            _health.MaximumHealth);
    }

    private void Unbind()
    {
        if (_health is not null)
        {
            _health.HealthChanged -= OnHealthChanged;
        }

        _health = null;
    }

    private void OnHealthChanged(
        float currentHealth,
        float maximumHealth)
    {
        Refresh(currentHealth, maximumHealth);
    }

    private void Refresh(
        float currentHealth,
        float maximumHealth)
    {
        if (!GodotObject.IsInstanceValid(_healthProgress))
            return;

        _healthProgress.MaxValue =
            Mathf.Max(maximumHealth, 1.0f);

        _healthProgress.Value =
            Mathf.Clamp(
                currentHealth,
                0.0f,
                maximumHealth);
    }
}