using Godot;
using System;

public partial class ActorHealthBarController : Node2D
{
	private ProgressBar _healthProgress = null!;
	private CombatHealthState? _health;

	/// <summary>
	/// Runs Godot setup for Actor Health Bar Controller when the node enters the scene tree.
	/// Uses the current node and service state; any result is applied through side effects, events, or stored fields.
	/// </summary>
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

	/// <summary>
	/// Cleans up Actor Health Bar Controller when the node leaves the scene tree.
	/// Uses the current node and service state; any result is applied through side effects, events, or stored fields.
	/// </summary>
	public override void _ExitTree()
	{
		Unbind();
	}

	/// <summary>
	/// Performs the bind operation for Actor Health Bar Controller.
	/// Uses the supplied arguments and current node state; any result is applied through side effects, events, or stored fields.
	/// </summary>
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

	/// <summary>
	/// Performs the unbind operation for Actor Health Bar Controller.
	/// Uses the current node and service state; any result is applied through side effects, events, or stored fields.
	/// </summary>
	private void Unbind()
	{
		if (_health is not null)
		{
			_health.HealthChanged -= OnHealthChanged;
		}

		_health = null;
	}

	/// <summary>
	/// Handles the health changed event and updates the related game state.
	/// Uses the supplied arguments and current node state; any result is applied through side effects, events, or stored fields.
	/// </summary>
	private void OnHealthChanged(
		float currentHealth,
		float maximumHealth)
	{
		Refresh(currentHealth, maximumHealth);
	}

	/// <summary>
	/// Performs the refresh operation for Actor Health Bar Controller.
	/// Uses the supplied arguments and current node state; any result is applied through side effects, events, or stored fields.
	/// </summary>
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
