using Godot;

public partial class ProjectileActorController : Node2D
{
	[Signal]
	public delegate void ImpactedEventHandler(
		ProjectileActorController projectile,
		HeroActorController attacker,
		MonsterActorController target);

	[ExportCategory("Movement")]
	[Export(PropertyHint.Range, "1,2000,1")]
	public float TravelSpeed { get; set; } = 500.0f;

	[Export(PropertyHint.Range, "0.1,20,0.1")]
	public float ImpactDistance { get; set; } = 4.0f;

	public HeroActorController? Attacker { get; private set; }

	public MonsterActorController? Target { get; private set; }

	public AbilityDefinition? Ability { get; private set; }

	private bool _isActive;

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

	private bool ValidateRuntimeReferences()
	{
		return Attacker is not null
			&& Target is not null
			&& GodotObject.IsInstanceValid(Attacker)
			&& GodotObject.IsInstanceValid(Target)
			&& Attacker.IsInsideTree()
			&& Target.IsInsideTree();
	}

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

	private void CancelProjectile()
	{
		_isActive = false;

		DebugLog.Print(
			"Projectile cancelled because its attacker " +
			"or target became invalid.");

		QueueFree();
	}
}
