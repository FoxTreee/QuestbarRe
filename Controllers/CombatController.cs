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

    public int HeroParticipantCount =>
		_heroParticipants.Count;

	public int MonsterParticipantCount =>
		_monsterParticipants.Count;

	public bool IsCombatActive { get; private set; }
    public bool IsInitialized { get; private set; }

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

	private void OnActiveMonsterCountChanged(int activeMonsterCount)
	{
		RefreshMonsterParticipants();
		ApplyCombatState();
		RefreshHeroTargets();
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

        bool establishedInitialAggro =
            target.TryEngage(attacker);

        if (establishedInitialAggro)
        {
            GD.Print(
                $"Initial monster aggro established: " +
                $"{target.Name} → {attacker.Name}");
        }

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
                    Type = CombatEventType.DamageApplied,
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

    private HeroActorController?
    SelectLowestHealthHero()
    {
        HeroActorController? selectedHero = null;

        foreach (
            HeroActorController hero
            in _heroParticipants)
        {
            if (!GodotObject.IsInstanceValid(hero))
                continue;

            if (!hero.IsInsideTree()
                || hero.IsIncapacitated
                || !hero.Health.IsAlive)
            {
                continue;
            }

            if (selectedHero is null
                || hero.Health.CurrentHealth
                    < selectedHero.Health.CurrentHealth)
            {
                selectedHero = hero;
            }
        }

        return selectedHero;
    }

    private void RefreshHeroTargets()
	{
		foreach (HeroActorController hero in _heroParticipants)
		{
			if (!GodotObject.IsInstanceValid(hero))
				continue;

			if (MonsterParticipantCount == 0)
			{
				hero.ClearTarget();
				continue;
			}

			hero.RefreshTarget(_monsterParticipants);
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

            monster.RefreshTargetValidity();

            if (monster.HasValidTarget)
                continue;

            HeroActorController? replacementTarget =
                SelectLowestHealthHero();

            if (replacementTarget is null)
            {
                GD.Print(
                    $"{monster.Name} has no living hero target.");

                continue;
            }

            bool targetAccepted = monster.TryEngage(replacementTarget);

            if (!targetAccepted)
                continue;

            GD.Print(
                $"{monster.Name} automatically retargeted " +
                $"{replacementTarget.Name} with " +
                $"{replacementTarget.Health.CurrentHealth} health.");
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
		foreach (HeroActorController hero in _heroParticipants)
		{
			if (!GodotObject.IsInstanceValid(hero))
				continue;

			hero.AttackReleased -=
				OnHeroAttackReleased;
		}
		
		foreach (MonsterActorController monster in _monsterParticipants)
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
