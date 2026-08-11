using Godot;

public partial class ProjectileActorController : Node2D
{
	[Signal]
	public delegate void ImpactedEventHandler(
		ProjectileActorController projectile,
		HeroActorController attacker,
		MonsterActorController target);

	[ExportCategory("Movement")]
	/// <summary>
	/// Controls travel speed, measured as pixels per second.
	/// For example, changing 500 to 1000 makes the affected movement or animation run about twice as fast.
	/// </summary>
	[Export(PropertyHint.Range, "1,2000,1")]
	public float TravelSpeed { get; set; } = 500.0f;

	/// <summary>
	/// Controls impact distance, measured as pixels.
	/// For example, changing 4 to 8 doubles the configured impact distance.
	/// </summary>
	[Export(PropertyHint.Range, "0.1,20,0.1")]
	public float ImpactDistance { get; set; } = 4.0f;

	public HeroActorController? Attacker { get; private set; }

	public MonsterActorController? Target { get; private set; }

	public AbilityDefinition? Ability { get; private set; }

	private bool _isActive;

	/// <summary>
	/// Performs the initialize operation for Projectile Actor Controller.
	/// Uses the supplied arguments and current node state; any result is applied through side effects, events, or stored fields.
	/// </summary>
	public void Initialize(
		HeroActorController attacker,
		MonsterActorController target,
		Vector2 spawnPosition,
		AbilityDefinition? ability = null)
	{
		Attacker = attacker;
		Target = target;
		Ability = ability;
		GlobalPosition = spawnPosition;
		_isActive = true;

		DebugLog.Print(
			$"Projectile initialized: " +
			$"{attacker.Name} → {target.Name}");
	}

	/// <summary>
	/// Updates Projectile Actor Controller every rendered frame using the supplied frame delta.
	/// Uses the supplied arguments and current node state; any result is applied through side effects, events, or stored fields.
	/// </summary>
	public override void _Process(double delta)
	{
		if (!_isActive)
			return;

		if (!ValidateRuntimeReferences())
		{
			CancelProjectile();
			return;
		}

		Vector2 targetPosition = Target!.ImpactPosition;

		float movementDistance =
			TravelSpeed * (float)delta;

		GlobalPosition = GlobalPosition.MoveToward(
			targetPosition,
			movementDistance);

		if (GlobalPosition.DistanceTo(targetPosition)
			> ImpactDistance)
		{
			return;
		}

		ConfirmArrival();
	}

	/// <summary>
	/// Performs the validate runtime references operation for Projectile Actor Controller.
	/// Reads the current state and returns the resulting bool to the caller.
	/// </summary>
	private bool ValidateRuntimeReferences()
	{
		return Attacker is not null
			&& Target is not null
			&& GodotObject.IsInstanceValid(Attacker)
			&& GodotObject.IsInstanceValid(Target)
			&& Attacker.IsInsideTree()
			&& Target.IsInsideTree();
	}

	/// <summary>
	/// Performs the confirm arrival operation for Projectile Actor Controller.
	/// Uses the current node and service state; any result is applied through side effects, events, or stored fields.
	/// </summary>
	private void ConfirmArrival()
	{
		if (!_isActive)
			return;

		_isActive = false;

		DebugLog.Print(
			$"Projectile reached {Target!.Name}.");

		EmitSignal(
			SignalName.Impacted,
			this,
			Attacker!,
			Target!);
	}

	/// <summary>
	/// Performs the cancel projectile operation for Projectile Actor Controller.
	/// Uses the current node and service state; any result is applied through side effects, events, or stored fields.
	/// </summary>
	private void CancelProjectile()
	{
		_isActive = false;

		DebugLog.Print(
			"Projectile cancelled because its attacker " +
			"or target became invalid.");

		QueueFree();
	}
}
