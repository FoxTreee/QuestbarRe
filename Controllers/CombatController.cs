using Godot;
using System;
using System.Collections.Generic;

public partial class CombatController : Node
{
	[Signal]
	public delegate void ParticipantsChangedEventHandler(
		int heroCount,
		int monsterCount);

	[ExportCategory("Dependencies")]
	[Export]
	public EncounterController Encounter { get; set; } = null!;
	
	[Export]
	public Node2D ActorLayer { get; set; } = null!;
	
	[Export]
	public TargetingService Targeting{ get; set; } = null!;


	[ExportCategory("Combat Content")]
	[Export]
	public PackedScene ProjectileScene { get; set; } = null!;

	[ExportCategory("Temporary Hero Roster")]
	[Export]
	public Godot.Collections.Array<HeroActorController>
		ConfiguredHeroes
	{ get; set; } = new();

	private readonly List<HeroActorController>
		_heroParticipants = new();

	private readonly List<MonsterActorController>
		_monsterParticipants = new();

	public IReadOnlyList<HeroActorController> HeroParticipants =>
		_heroParticipants;

	public IReadOnlyList<MonsterActorController> MonsterParticipants =>
		_monsterParticipants;

	public event Action<CombatEvent>? CombatEventOccurred;
	public event Action<TargetChangedEvent>? TargetChanged;

	public int HeroParticipantCount =>
		_heroParticipants.Count;

	public int MonsterParticipantCount =>
		_monsterParticipants.Count;

	public bool IsCombatActive { get; private set; }
	public bool IsInitialized { get; private set; }

	// Reffresh heroes -- DEBUG ONLY
	public void DebugRefreshHeroParticipants()
	{
		UnsubscribeHeroParticipants();
		_heroParticipants.Clear();

		BuildHeroParticipants();

		ApplyCombatState();
		RefreshHeroTargets();
		RefreshMonsterTargets();

		foreach (
			HeroActorController hero
			in _heroParticipants)
		{
			if (!GodotObject.IsInstanceValid(hero))
				continue;

			hero.ResumeCombatAfterDebugReset();
		}

		EmitParticipantsChanged();

		GD.Print(
			$"Debug-respawned heroes into current combat. " +
			$"Active heroes={_heroParticipants.Count}, " +
			$"existing monsters={_monsterParticipants.Count}");
	}

	// Remove Heroes -- DEBUG ONLY
	private void UnsubscribeHeroParticipants()
	{
		foreach (
			HeroActorController hero
			in _heroParticipants)
		{
			if (!GodotObject.IsInstanceValid(hero))
				continue;

			hero.AttackReleased -=
				OnHeroAttackReleased;

			hero.Incapacitated -=
				OnHeroIncapacitated;
		}
	}

	public override void _Ready()
	{
		if (!ValidateReferences())
			return;

		BuildHeroParticipants();

		Encounter.ActiveMonsterCountChanged +=
			OnActiveMonsterCountChanged;

		RefreshMonsterParticipants();
		ApplyCombatState();
		RefreshHeroTargets();

		GD.Print(
			$"Combat participants initialized. " +
			$"Heroes={HeroParticipantCount}, " +
			$"Monsters={MonsterParticipantCount}");
	}
	
	private void BuildHeroParticipants()
	{
		_heroParticipants.Clear();

		foreach (HeroActorController hero in ConfiguredHeroes)
		{
			if (!GodotObject.IsInstanceValid(hero))
				continue;

			if (hero.IsIncapacitated)
				continue;

			_heroParticipants.Add(hero);

			hero.AttackReleased += OnHeroAttackReleased;

			hero.Incapacitated += OnHeroIncapacitated;
		}
	}

	private void OnActiveMonsterCountChanged(
	int activeMonsterCount)
	{
		RefreshMonsterParticipants();
		ApplyCombatState();

		RefreshHeroTargets();
		RefreshMonsterTargets();

		EmitParticipantsChanged();
	}

	private void OnHeroAttackReleased(HeroActorController attacker, MonsterActorController target)
	{
		if (!GodotObject.IsInstanceValid(attacker)
			|| !GodotObject.IsInstanceValid(target))
		{
			return;
		}

		GD.Print(
			$"Combat received attack release: " +
			$"{attacker.Name} → {target.Name}");

		switch (attacker.CombatProfile.AttackDelivery)
		{
			case AttackDeliveryMode.ImmediateImpact:
				ConfirmHeroImpact(attacker, target);
				break;

			case AttackDeliveryMode.Projectile:
				HandlePendingProjectileRelease(
					attacker,
					target);
				break;

			case AttackDeliveryMode.Hitscan:
				ConfirmHeroImpact(attacker, target);
				break;
		}
	}

	private void ConfirmHeroImpact(
	HeroActorController attacker,
	MonsterActorController target)
	{
		if (!GodotObject.IsInstanceValid(attacker)
			|| !GodotObject.IsInstanceValid(target))
		{
			return;
		}

		GD.Print(
			$"Hero impact confirmed: " +
			$"{attacker.Name} → {target.Name}");

		DamageResult result = target.Health.ApplyDamage(attacker.CombatProfile.AttackDamage);

			RaiseCombatEvent(
		new CombatEvent
		{
			Type = CombatEventType.DamageApplied,
			Attacker = attacker,
			Target = target,
			Damage = result
		});

		PrintDamageResult(
			attacker.Name,
			target.Name,
			result);

		if (result.WasLethal)
		{
			target.EnterDeadState();

			RaiseCombatEvent(
				new CombatEvent
				{
					Type = CombatEventType.ActorDied,
					Attacker = attacker,
					Target = target,
					Damage = result
				});
		}
	}

	private void HandlePendingProjectileRelease(HeroActorController attacker, MonsterActorController target)
	{
		ProjectileActorController projectile =
			ProjectileScene.Instantiate
				<ProjectileActorController>();

		ActorLayer.AddChild(projectile);

		projectile.Impacted +=
			OnHeroProjectileImpacted;

		projectile.Initialize(
			attacker,
			target,
			attacker.ProjectileOrigin.GlobalPosition);

		GD.Print(
			$"Projectile created: " +
			$"{attacker.Name} → {target.Name}");
	}
	
	private void OnHeroProjectileImpacted(
	ProjectileActorController projectile,
	HeroActorController attacker,
	MonsterActorController target)
	{
		if (GodotObject.IsInstanceValid(projectile))
		{
			projectile.Impacted -=
				OnHeroProjectileImpacted;
		}

		if (GodotObject.IsInstanceValid(attacker)
			&& GodotObject.IsInstanceValid(target))
		{
			ConfirmHeroImpact(
				attacker,
				target);
		}

		if (GodotObject.IsInstanceValid(projectile))
			projectile.QueueFree();
	}

	private void RefreshHeroTargets()
	{
		foreach (HeroActorController hero in _heroParticipants)
		{
			if (!GodotObject.IsInstanceValid(hero))
				continue;

			MonsterActorController? previousTarget =
				hero.CurrentTarget;

			if (MonsterParticipantCount == 0)
			{
				hero.ClearTarget();

				if (previousTarget is not null)
				{
					RaiseTargetChanged(
						hero,
						previousTarget,
						null);
				}

				continue;
			}

			hero.RefreshTarget(_monsterParticipants);

			if (hero.CurrentTarget == previousTarget)
				continue;

			RaiseTargetChanged(
				hero,
				previousTarget,
				hero.CurrentTarget);
		}
	}

	private void RefreshMonsterTargets()
	{
		foreach (
			MonsterActorController monster
			in _monsterParticipants)
		{
			if (!GodotObject.IsInstanceValid(monster)
				|| monster.IsDead)
			{
				continue;
			}

			HeroActorController? previousTarget =
				monster.CurrentTarget;

			monster.RefreshTargetValidity();

			if (monster.HasValidTarget)
				continue;

			HeroActorController? replacementTarget = Targeting.SelectHeroTarget(monster, _heroParticipants);

			if (replacementTarget is null)
			{
				if (previousTarget is not null)
				{
					RaiseTargetChanged(
						monster,
						previousTarget,
						null);
				}

				GD.Print(
					$"{monster.Name} has no living hero target.");

				continue;
			}

			bool targetAccepted =
				monster.TryAcquireTarget(
					replacementTarget);

			if (!targetAccepted)
				continue;

			RaiseTargetChanged(
				monster,
				previousTarget,
				replacementTarget);

			GD.Print(
				$"{monster.Name} selected " +
				$"{replacementTarget.Name} using " +
				$"{monster.Definition.TargetingStyle}.");
		}
	}

	private void RefreshMonsterParticipants()
	{
		foreach (
			MonsterActorController monster
			in _monsterParticipants)
		{
			if (!GodotObject.IsInstanceValid(monster))
				continue;

			monster.AttackReleased -=
				OnMonsterAttackReleased;
		}

		_monsterParticipants.Clear();

		foreach (
			MonsterActorController monster
			in Encounter.ActiveMonsters)
		{
			if (!GodotObject.IsInstanceValid(monster))
				continue;

			if (monster.IsDead)
				continue;

			_monsterParticipants.Add(monster);

			monster.AttackReleased +=
				OnMonsterAttackReleased;
		}
	}

	private void OnMonsterAttackReleased(
	MonsterActorController attacker,
	HeroActorController target)
	{
		if (!GodotObject.IsInstanceValid(attacker)
			|| !GodotObject.IsInstanceValid(target))
		{
			return;
		}

		GD.Print(
			$"Combat received monster attack release: " +
			$"{attacker.Name} → {target.Name}");

		ConfirmMonsterImpact(
			attacker,
			target);
	}

	private void ConfirmMonsterImpact(
	MonsterActorController attacker,
	HeroActorController target)
	{
		if (!GodotObject.IsInstanceValid(attacker)
			|| !GodotObject.IsInstanceValid(target))
		{
			return;
		}

		GD.Print(
			$"Monster impact confirmed: " +
			$"{attacker.Name} → {target.Name}");

		DamageResult result =
			target.Health.ApplyDamage(attacker.CombatProfile.AttackDamage);

		PrintDamageResult(attacker.Name,  target.Name, result);

		RaiseCombatEvent(
	new CombatEvent
	{
		Type = CombatEventType.DamageApplied,
		Attacker = attacker,
		Target = target,
		Damage = result
	});

		if (!result.WasLethal)
			return;

		target.EnterIncapacitatedState();

		RaiseCombatEvent(
			new CombatEvent
			{
				Type = CombatEventType.ActorIncapacitated,
				Attacker = attacker,
				Target = target,
				Damage = result
			});
	}

	private void RaiseCombatEvent(
	CombatEvent combatEvent)
	{
		CombatEventOccurred?.Invoke(combatEvent);
	}

	private void RaiseTargetChanged(
	Node actor,
	Node? previousTarget,
	Node? currentTarget)
	{
		TargetChanged?.Invoke(
			new TargetChangedEvent
			{
				Actor = actor,
				PreviousTarget = previousTarget,
				CurrentTarget = currentTarget
			});
	}

	private static void PrintDamageResult(
	StringName attackerName,
	StringName targetName,
	DamageResult result)
		{
			GD.Print(
				$"{attackerName} dealt " +
				$"{result.AppliedDamage} damage to " +
				$"{targetName}. " +
				$"Remaining health=" +
				$"{result.RemainingHealth}.");

			if (!result.WasLethal)
				return;

			GD.Print(
				$"{targetName} received lethal damage.");
		}

	private void OnHeroIncapacitated(
	HeroActorController hero)
	{
		if (!GodotObject.IsInstanceValid(hero))
			return;

		GD.Print(
			$"Combat handling incapacitation for {hero.Name}.");

		bool wasRemoved =
			_heroParticipants.Remove(hero);

		if (!wasRemoved)
			return;

		hero.AttackReleased -= OnHeroAttackReleased;

		hero.Incapacitated -= OnHeroIncapacitated;

		GD.Print(
			$"{hero.Name} removed from active combat. " +
			$"Active heroes={_heroParticipants.Count}");

		RefreshMonsterTargets();
		ApplyCombatState();
		EmitParticipantsChanged();
	}

	private void ApplyCombatState()
	{
		bool shouldCombatBeActive =
			MonsterParticipantCount > 0;

		if (IsCombatActive == shouldCombatBeActive)
			return;

		IsCombatActive = shouldCombatBeActive;

		GD.Print(
			IsCombatActive
				? $"Combat activated. " +
				  $"Heroes={HeroParticipantCount}, " +
				  $"Monsters={MonsterParticipantCount}"
				: "Combat ended.");
	}

	private void EmitParticipantsChanged()
	{
		EmitSignal(
			SignalName.ParticipantsChanged,
			HeroParticipantCount,
			MonsterParticipantCount);
	}

	private bool ValidateReferences()
	{
		if (!GodotObject.IsInstanceValid(ActorLayer))
		{
		GD.PushError(
			"CombatController is missing its " +
			"ActorLayer Inspector reference.");
		return false;
		}
		if (!GodotObject.IsInstanceValid(Targeting))
		{
			GD.PushError(
				"CombatController is missing its " +
				"Targeting Inspector reference.");

			return false;
		}
		if (!GodotObject.IsInstanceValid(ProjectileScene))
		{
		GD.PushError(
			"CombatController is missing its " +
			"ProjectileScene Inspector reference.");
		return false;
		}
		if (ConfiguredHeroes.Count == 0)
		{
			GD.PushError(
				"CombatController has no configured heroes.");

			return false;
		}

		return true;
	}

	public override void _ExitTree()
	{
		UnsubscribeHeroParticipants();

		foreach (
			MonsterActorController monster
			in _monsterParticipants)
		{
			if (!GodotObject.IsInstanceValid(monster))
				continue;

			monster.AttackReleased -=
				OnMonsterAttackReleased;
		}

		if (GodotObject.IsInstanceValid(Encounter))
		{
			Encounter.ActiveMonsterCountChanged -=
				OnActiveMonsterCountChanged;
		}
	}
}
