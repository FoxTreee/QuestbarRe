using Godot;
using System;
using System.Collections.Generic;

public partial class CombatController : Node
{
	private const float DirectDamageThreatMultiplier = 1.15f;
	private const float IndirectDamageThreatMultiplier = 1.0f;

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
	public TargetingService Targeting { get; set; } = null!;

	[Export]
	public CombatDamageResolver DamageResolver { get; set; } = null!;

	[Export]
	public PartyController Party { get; set; } = null!;


	[ExportCategory("Combat Content")]
	[Export]
	public PackedScene ProjectileScene { get; set; } = null!;

	private readonly List<HeroActorController>
		_heroParticipants = new();

	private readonly List<MonsterActorController>
		_monsterParticipants = new();

	private readonly HashSet<HeroActorController>
		_tauntRecoveryArmed = new();

	private readonly Dictionary<HeroActorController, double>
		_zeroAggroTauntElapsed = new();

	private readonly Dictionary
		<MonsterActorController, HeroActorController>
		_forcedTauntCasters = new();

	public IReadOnlyList<HeroActorController> HeroParticipants =>
		_heroParticipants;

	public IReadOnlyList<MonsterActorController> MonsterParticipants =>
		_monsterParticipants;

	public event Action<CombatEvent>? CombatEventOccurred;
	public event Action<TargetChangedEvent>? TargetChanged;
	public event Action<CombatOutcome>? CombatResolved;

	public int HeroParticipantCount =>
		_heroParticipants.Count;

	public int MonsterParticipantCount =>
		_monsterParticipants.Count;

	public bool IsCombatActive { get; private set; }
	public CombatOutcome CurrentOutcome { get; private set; }
		= CombatOutcome.None;
	public bool IsInitialized { get; private set; }

	// Reffresh heroes -- DEBUG ONLY
	public void DebugRefreshHeroParticipants()
	{
		if (!IsInitialized)
		{
			GD.PushWarning(
				"CombatController cannot refresh heroes " +
				"before the party has spawned.");

			return;
		}

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

		DebugLog.Print(
			$"Debug-respawned heroes into current combat. " +
			$"Active heroes={_heroParticipants.Count}, " +
			$"existing monsters={_monsterParticipants.Count}");
	}

	public bool DebugIncapacitateHero(
		HeroActorController hero)
	{
		if (!GodotObject.IsInstanceValid(hero)
			|| hero.IsIncapacitated
			|| !hero.Health.IsAlive)
		{
			return false;
		}

		DamageResult result = DamageResolver.Resolve(
			new DamageRequest(
				this,
				hero,
				hero.Health.CurrentHealth),
			hero.Health);

		RaiseCombatEvent(
			new CombatEvent
			{
				Type = CombatEventType.DamageApplied,
				Attacker = this,
				Target = hero,
				Damage = result
			});

		if (!result.WasLethal)
			return false;

		hero.EnterIncapacitatedState();

		RaiseCombatEvent(
			new CombatEvent
			{
				Type = CombatEventType.ActorIncapacitated,
				Attacker = this,
				Target = hero,
				Damage = result
			});

		return true;
	}

	public bool TryUseHeroAbility(
		HeroActorController hero,
		string abilityContentId,
		out string result)
	{
		result = string.Empty;

		if (!GodotObject.IsInstanceValid(hero))
		{
			result = "The requested hero is invalid.";
			return false;
		}

		if (!_heroParticipants.Contains(hero)
			|| hero.IsIncapacitated
			|| !hero.Health.IsAlive
			|| hero.IsUsingAbility)
		{
			result =
				$"{hero.Name} cannot use an ability right now.";

			return false;
		}

		if (!hero.TryGetAbility(
			abilityContentId,
			out AbilityDefinition ability))
		{
			result =
				$"{hero.Name} does not have ability " +
				$"'{abilityContentId}'.";

			return false;
		}

		double cooldownRemaining =
			hero.GetAbilityCooldownRemaining(
				ability.ContentId);

		if (cooldownRemaining > 0.0)
		{
			result =
				$"{hero.Name}'s {ability.DisplayName} is on " +
				$"cooldown for {cooldownRemaining:0.0} more " +
				"seconds.";

			return false;
		}

		if (ability.EffectType != AbilityEffectType.DirectHealing
			&& !IsCombatActive)
		{
			result =
				$"{hero.Name} cannot use {ability.DisplayName} " +
				"outside active combat.";

			return false;
		}

		bool abilityApplied = ability.EffectType switch
		{
			AbilityEffectType.AreaTaunt =>
				TryApplyAreaTaunt(
					hero,
					ability,
					out result),

			AbilityEffectType.DirectHealing =>
				TryApplyAutomaticDirectHealing(
					hero,
					ability,
					out result),

			_ => FailUnsupportedHeroAbility(
				ability,
				out result)
		};

		if (!abilityApplied)
			return false;

		if (!hero.TryStartAbilityCooldown(ability))
		{
			result =
				$"{hero.Name}'s {ability.DisplayName} could not " +
				"start its cooldown.";

			return false;
		}

		if (ability.CooldownSeconds > 0.0f)
		{
			result +=
				$" Cooldown started: " +
				$"{ability.CooldownSeconds:0.##} seconds.";
		}

		return true;
	}

	private bool TryApplyAutomaticDirectHealing(
		HeroActorController caster,
		AbilityDefinition ability,
		out string result)
	{
		HeroActorController? target =
			FindLowestHealthAllyBelowThreshold(ability);

		if (target is null)
		{
			result =
				$"No living party member is below " +
				$"{ability.AutoCastHealthThresholdPercent:0.#}% health.";

			return false;
		}

		return TryApplyDirectHealing(
			caster,
			target,
			ability,
			out result);
	}

	private bool TryApplyDirectHealing(
		HeroActorController caster,
		HeroActorController target,
		AbilityDefinition ability,
		out string result)
	{
		if (!GodotObject.IsInstanceValid(target)
			|| target.IsIncapacitated
			|| !target.Health.IsAlive)
		{
			result = "The selected healing target is not alive.";
			return false;
		}

		// Future Spirit scaling belongs on this spell-healing amount.
		// Passive traveling recovery never passes through this path.
		float requestedHealing = ability.BaseHealing;
		float appliedHealing =
			target.Health.ApplySpellHealing(requestedHealing);

		DebugLog.Print(
			$"{caster.Name} used '{ability.DisplayName}' on " +
			$"{target.Name}. SpellHealing={appliedHealing:0.##}; " +
			$"Health={target.Health.CurrentHealth:0.##}/" +
			$"{target.Health.MaximumHealth:0.##}.");

		result =
			$"{caster.Name} healed {target.Name} for " +
			$"{appliedHealing:0.##}.";

		return true;
	}

	private bool TryApplyAreaTaunt(
		HeroActorController caster,
		AbilityDefinition ability,
		out string result)
	{
		float radiusSquared =
			ability.EffectRadius
			* ability.EffectRadius;

		int affectedCount = 0;

		foreach (
			MonsterActorController monster
			in _monsterParticipants)
		{
			if (!GodotObject.IsInstanceValid(monster)
				|| monster.IsDead)
			{
				continue;
			}

			float distanceSquared =
				caster.GlobalPosition.DistanceSquaredTo(
					monster.GlobalPosition);

			if (distanceSquared > radiusSquared)
				continue;

			HeroActorController? previousTarget =
				monster.CurrentTarget;

			if (!monster.TryApplyForcedTarget(
				caster,
				ability.EffectDurationSeconds))
			{
				continue;
			}

			affectedCount++;
			_forcedTauntCasters[monster] = caster;

			if (previousTarget != monster.CurrentTarget)
			{
				RaiseTargetChanged(
					monster,
					previousTarget,
					monster.CurrentTarget);
			}
		}

		DebugLog.Print(
			$"{caster.Name} used '{ability.DisplayName}'. " +
			$"Affected monsters={affectedCount}; " +
			$"Radius={ability.EffectRadius:0.##}; " +
			$"Duration={ability.EffectDurationSeconds:0.##}s.");

		result =
			$"{caster.Name} used {ability.DisplayName}. " +
			$"Taunted {affectedCount} monster(s) within " +
			$"{ability.EffectRadius:0.##} units for " +
			$"{ability.EffectDurationSeconds:0.##} seconds.";

		return true;
	}

	private static bool FailUnsupportedHeroAbility(
		AbilityDefinition ability,
		out string result)
	{
		result =
			$"Hero execution is not implemented for effect " +
			$"type '{ability.EffectType}' on " +
			$"'{ability.ContentId}'.";

		return false;
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

			hero.AbilityReleased -=
				OnHeroAbilityReleased;

			hero.Incapacitated -=
				OnHeroIncapacitated;
		}
	}

	public override void _Ready()
	{
		if (!ValidateReferences())
			return;

		Party.PartySpawned += OnPartySpawned;

		if (Party.SpawnedHeroCount > 0)
		{
			InitializeCombatParticipants();
			return;
		}

		DebugLog.Print(
			"CombatController waiting for PartyController roster.");
	}

	private void OnPartySpawned(int heroCount)
	{
		if (IsInitialized)
			return;

		if (heroCount == 0)
		{
			GD.PushError(
				"CombatController cannot initialize because " +
				"PartyController spawned no heroes.");

			return;
		}

		InitializeCombatParticipants();
	}

	private void InitializeCombatParticipants()
	{
		BuildHeroParticipants();

		if (HeroParticipantCount == 0)
		{
			GD.PushError(
				"CombatController found no active heroes in " +
				"the PartyController roster.");

			return;
		}

		Encounter.ActiveMonsterCountChanged +=
			OnActiveMonsterCountChanged;

		Encounter.EncounterStarted +=
			OnEncounterStarted;

		Encounter.EncounterCompleted +=
			OnEncounterCompleted;

		RefreshMonsterParticipants();
		ApplyCombatState();
		RefreshHeroTargets();
		RefreshMonsterTargets();
		IsInitialized = true;

		DebugLog.Print(
			$"Combat participants initialized. " +
			$"Heroes={HeroParticipantCount}, " +
			$"Monsters={MonsterParticipantCount}");
	}

	private void BuildHeroParticipants()
	{
		_heroParticipants.Clear();

		foreach (HeroActorController hero in Party.SpawnedHeroes)
		{
			if (!GodotObject.IsInstanceValid(hero))
				continue;

			if (hero.IsIncapacitated)
				continue;

			_heroParticipants.Add(hero);

			hero.AttackReleased += OnHeroAttackReleased;

			hero.AbilityReleased += OnHeroAbilityReleased;

			hero.Incapacitated += OnHeroIncapacitated;
		}
	}

	private void OnEncounterStarted()
	{
		CurrentOutcome = CombatOutcome.None;
		ResetAutomaticTauntState();

		DebugLog.Print(
			"Combat outcome reset for new encounter.");

		ApplyCombatState();
	}

	private void OnEncounterCompleted()
	{
		ResolveCombatOutcome(CombatOutcome.Victory);
	}

	private void OnActiveMonsterCountChanged(
	int activeMonsterCount)
	{
		RefreshMonsterParticipants();

		if (activeMonsterCount == 0
			&& CurrentOutcome == CombatOutcome.None
			&& Encounter.JourneyState.CurrentState
				== JourneyStateService.JourneyState.Encounter)
		{
			ResolveCombatOutcome(
				CombatOutcome.Victory);
		}

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

		DebugLog.Print(
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

	private void OnHeroAbilityReleased(
		HeroActorController caster,
		HeroActorController target,
		AbilityDefinition ability)
	{
		if (!GodotObject.IsInstanceValid(caster)
			|| !GodotObject.IsInstanceValid(target)
			|| !GodotObject.IsInstanceValid(ability))
		{
			return;
		}

		bool applied = ability.EffectType switch
		{
			AbilityEffectType.AreaTaunt =>
				TryApplyAreaTaunt(
					caster,
					ability,
					out _),

			AbilityEffectType.DirectHealing =>
				TryApplyDirectHealing(
					caster,
					target,
					ability,
					out _),

			_ => false
		};

		if (!applied)
		{
			DebugLog.Print(
				$"{caster.Name}'s '{ability.DisplayName}' " +
				"released without applying an effect.");
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

		DebugLog.Print(
			$"Hero impact confirmed: " +
			$"{attacker.Name} → {target.Name}");

		DamageResult result = DamageResolver.Resolve(
			new DamageRequest(
				attacker,
				target,
				attacker.CombatProfile.AttackDamage),
			target.Health);

		BroadcastHeroDamageThreat(
			attacker,
			target,
			result.AppliedDamage);

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

	private void BroadcastHeroDamageThreat(
		HeroActorController attacker,
		MonsterActorController directlyDamagedMonster,
		float appliedDamage)
	{
		if (!float.IsFinite(appliedDamage)
			|| appliedDamage <= 0.0f)
		{
			return;
		}

		foreach (
			MonsterActorController monster
			in _monsterParticipants)
		{
			if (!GodotObject.IsInstanceValid(monster)
				|| monster.IsDead)
			{
				continue;
			}

			float threatMultiplier =
				monster == directlyDamagedMonster
					? DirectDamageThreatMultiplier
					: IndirectDamageThreatMultiplier;

			monster.Threat.AddThreat(
				attacker,
				appliedDamage * threatMultiplier);
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

		DebugLog.Print(
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

			if (monster.HasForcedTarget)
				continue;

			if (monster.HasValidTarget)
			{
				TrySwitchMonsterThreatTarget(
					monster,
					previousTarget!);

				continue;
			}

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

				DebugLog.Print(
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

			DebugLog.Print(
				$"{monster.Name} selected " +
				$"{replacementTarget.Name} using " +
				$"{monster.Definition.TargetingStyle}.");
		}
	}

	private void TrySwitchMonsterThreatTarget(
		MonsterActorController monster,
		HeroActorController currentTarget)
	{
		HeroActorController? challenger =
			Targeting.SelectThreatTakeoverTarget(
				monster,
				_heroParticipants);

		if (challenger is null)
			return;

		float currentThreat =
			monster.Threat.GetThreat(currentTarget);

		float challengerThreat =
			monster.Threat.GetThreat(challenger);

		bool challengerIsInMeleeRange =
			TargetingService.IsWithinMonsterMeleeRange(
				monster,
				challenger);

		float takeoverMultiplier =
			challengerIsInMeleeRange
				? TargetingService.MeleeThreatTakeoverMultiplier
				: TargetingService.DistantThreatTakeoverMultiplier;

		if (!monster.TrySwitchTarget(challenger))
			return;

		ArmTauntRecoveryAfterAggroLoss(
			currentTarget,
			monster,
			challenger);

		RaiseTargetChanged(
			monster,
			currentTarget,
			challenger);

		DebugLog.Print(
			$"{monster.Name} changed threat target: " +
			$"{currentTarget.Name}=" +
			$"{currentThreat:0.##} → " +
			$"{challenger.Name}=" +
			$"{challengerThreat:0.##}; " +
			$"required={takeoverMultiplier * 100.0f:0}% " +
			$"({(challengerIsInMeleeRange ? "melee range" : "outside melee range")}).");
	}

	public override void _Process(double delta)
	{
		if (!IsInitialized)
			return;

		UpdateAutomaticHeroAbilities(delta);

		if (!IsCombatActive)
			return;

		RefreshMonsterTargets();
	}

	private void UpdateAutomaticHeroAbilities(double delta)
	{
		foreach (HeroActorController hero
			in _heroParticipants)
		{
			if (!GodotObject.IsInstanceValid(hero)
				|| hero.IsIncapacitated
				|| !hero.Health.IsAlive
				|| hero.IsUsingAbility)
			{
				continue;
			}

			foreach (AbilityDefinition ability
				in hero.Abilities)
			{
				if (!GodotObject.IsInstanceValid(ability))
				{
					continue;
				}

				bool abilityReady =
					hero.IsAbilityReady(ability.ContentId);

				HeroActorController? abilityTarget = null;

				switch (ability.EffectType)
				{
					case AbilityEffectType.DirectHealing
						when abilityReady:
						abilityTarget =
							FindLowestHealthAllyBelowThreshold(
								ability);
						break;

					case AbilityEffectType.AreaTaunt:
						bool shouldTaunt =
							ShouldUseAutomaticTaunt(
								hero,
								ability,
								delta);

						if (abilityReady && shouldTaunt)
							abilityTarget = hero;

						break;
				}

				if (abilityTarget is null)
					continue;

				if (hero.TryBeginAbility(
					ability,
					abilityTarget))
				{
					if (ability.EffectType
						== AbilityEffectType.AreaTaunt)
					{
						ConsumeAutomaticTauntTrigger(hero);
					}

					break;
				}
			}
		}
	}

	private bool ShouldUseAutomaticTaunt(
		HeroActorController caster,
		AbilityDefinition ability,
		double delta)
	{
		if (!IsCombatActive)
		{
			_zeroAggroTauntElapsed.Remove(caster);
			return false;
		}

		bool hasMonsterInRadius =
			HasMonsterWithinTauntRadius(
				caster,
				ability);

		bool holdsMonsterAggro =
			IsHoldingAnyMonsterAggro(caster);

		if (holdsMonsterAggro || !hasMonsterInRadius)
		{
			_zeroAggroTauntElapsed.Remove(caster);
		}
		else
		{
			_zeroAggroTauntElapsed.TryGetValue(
				caster,
				out double elapsed);

			_zeroAggroTauntElapsed[caster] =
				elapsed + System.Math.Max(delta, 0.0);
		}

		double zeroAggroElapsed =
			_zeroAggroTauntElapsed.TryGetValue(
				caster,
				out double elapsedSeconds)
					? elapsedSeconds
					: 0.0;

		double requiredDelay =
			System.Math.Max(
				ability.AutoCastDelaySeconds,
				0.0f);

		bool zeroAggroFallbackReady =
			!holdsMonsterAggro
			&& hasMonsterInRadius
			&& zeroAggroElapsed >= requiredDelay;

		return hasMonsterInRadius
			&& (_tauntRecoveryArmed.Contains(caster)
				|| zeroAggroFallbackReady);
	}

	private bool IsHoldingAnyMonsterAggro(
		HeroActorController hero)
	{
		foreach (MonsterActorController monster
			in _monsterParticipants)
		{
			if (GodotObject.IsInstanceValid(monster)
				&& !monster.IsDead
				&& monster.CurrentTarget == hero)
			{
				return true;
			}
		}

		return false;
	}

	private void ArmTauntRecoveryAfterAggroLoss(
		HeroActorController previousTarget,
		MonsterActorController monster,
		HeroActorController currentTarget)
	{
		if (!GodotObject.IsInstanceValid(previousTarget)
			|| previousTarget.IsIncapacitated
			|| !previousTarget.Health.IsAlive
			|| !HasAreaTauntAbility(previousTarget)
			|| currentTarget == previousTarget)
		{
			return;
		}

		if (!_tauntRecoveryArmed.Add(previousTarget))
			return;

		DebugLog.Print(
			$"{previousTarget.Name} armed automatic Taunt after " +
			$"losing aggro on {monster.Name} to " +
			$"{currentTarget.Name}.");
	}

	private static bool HasAreaTauntAbility(
		HeroActorController hero)
	{
		foreach (AbilityDefinition ability in hero.Abilities)
		{
			if (GodotObject.IsInstanceValid(ability)
				&& ability.EffectType
					== AbilityEffectType.AreaTaunt)
			{
				return true;
			}
		}

		return false;
	}

	private void ConsumeAutomaticTauntTrigger(
		HeroActorController caster)
	{
		_tauntRecoveryArmed.Remove(caster);
		_zeroAggroTauntElapsed.Remove(caster);
	}

	private void ResetAutomaticTauntState()
	{
		_tauntRecoveryArmed.Clear();
		_zeroAggroTauntElapsed.Clear();
		_forcedTauntCasters.Clear();
	}

	private HeroActorController?
		FindLowestHealthAllyBelowThreshold(
			AbilityDefinition ability)
	{
		float threshold = Mathf.Clamp(
			ability.AutoCastHealthThresholdPercent
				/ 100.0f,
			0.0f,
			1.0f);

		HeroActorController? lowestHealthAlly = null;
		float lowestHealthPercent = float.MaxValue;

		foreach (HeroActorController candidate
			in Party.SpawnedHeroes)
		{
			if (!GodotObject.IsInstanceValid(candidate)
				|| candidate.IsIncapacitated
				|| !candidate.Health.IsAlive)
			{
				continue;
			}

			float healthPercent =
				candidate.Health.CurrentHealth
				/ candidate.Health.MaximumHealth;

			if (healthPercent >= threshold
				|| healthPercent >= lowestHealthPercent)
			{
				continue;
			}

			lowestHealthAlly = candidate;
			lowestHealthPercent = healthPercent;
		}

		return lowestHealthAlly;
	}

	private bool HasMonsterWithinTauntRadius(
		HeroActorController caster,
		AbilityDefinition ability)
	{
		float radius = Mathf.Max(
			ability.EffectRadius,
			0.0f);

		float radiusSquared = radius * radius;

		foreach (MonsterActorController monster
			in _monsterParticipants)
		{
			if (!GodotObject.IsInstanceValid(monster)
				|| monster.IsDead)
			{
				continue;
			}

			if (caster.GlobalPosition.DistanceSquaredTo(
				monster.GlobalPosition) <= radiusSquared)
			{
				return true;
			}
		}

		return false;
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

			monster.AbilityReleased -=
				OnMonsterAbilityReleased;

			monster.ForcedTargetEnded -=
				OnMonsterForcedTargetEnded;
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

			monster.AbilityReleased +=
				OnMonsterAbilityReleased;

			monster.ForcedTargetEnded +=
				OnMonsterForcedTargetEnded;
		}
	}

	private void OnMonsterForcedTargetEnded(
		MonsterActorController monster)
	{
		if (!GodotObject.IsInstanceValid(monster)
			|| monster.IsDead)
		{
			return;
		}

		_forcedTauntCasters.TryGetValue(
			monster,
			out HeroActorController? previousForcedTarget);

		_forcedTauntCasters.Remove(monster);

		RefreshMonsterTargets();

		if (previousForcedTarget is not null
			&& GodotObject.IsInstanceValid(previousForcedTarget)
			&& monster.CurrentTarget is HeroActorController currentTarget
			&& currentTarget != previousForcedTarget)
		{
			ArmTauntRecoveryAfterAggroLoss(
				previousForcedTarget,
				monster,
				currentTarget);
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

		DebugLog.Print(
			$"Combat received monster attack release: " +
			$"{attacker.Name} → {target.Name}");

		ConfirmMonsterImpact(
			attacker,
			target);
	}

	private void OnMonsterAbilityReleased(
		MonsterActorController attacker,
		HeroActorController target,
		AbilityDefinition ability)
	{
		if (!GodotObject.IsInstanceValid(attacker)
			|| !GodotObject.IsInstanceValid(target)
			|| !GodotObject.IsInstanceValid(ability))
		{
			return;
		}

		DebugLog.Print(
			$"Combat received ability release: " +
			$"{attacker.Name} → {target.Name} " +
			$"using '{ability.DisplayName}'.");

		ConfirmMonsterAbilityImpact(
			attacker,
			target,
			ability);
	}

	private void ConfirmMonsterAbilityImpact(
		MonsterActorController attacker,
		HeroActorController target,
		AbilityDefinition ability)
	{
		if (!GodotObject.IsInstanceValid(attacker)
			|| !GodotObject.IsInstanceValid(target)
			|| !GodotObject.IsInstanceValid(ability))
		{
			return;
		}

		DebugLog.Print(
			$"Ability impact confirmed: " +
			$"{attacker.Name} → {target.Name} " +
			$"with '{ability.DisplayName}'.");

		DamageResult result = DamageResolver.Resolve(
			new DamageRequest(
				attacker,
				target,
				ability.BaseDamage),
			target.Health);

		PrintDamageResult(
			attacker.Name,
			target.Name,
			result);

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

	private void ConfirmMonsterImpact(
	MonsterActorController attacker,
	HeroActorController target)
	{
		if (!GodotObject.IsInstanceValid(attacker)
			|| !GodotObject.IsInstanceValid(target))
		{
			return;
		}

		DebugLog.Print(
			$"Monster impact confirmed: " +
			$"{attacker.Name} → {target.Name}");

		DamageResult result = DamageResolver.Resolve(
			new DamageRequest(
				attacker,
				target,
				attacker.CombatProfile.AttackDamage),
			target.Health);

		PrintDamageResult(attacker.Name, target.Name, result);

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
		DebugLog.Print(
			$"{attackerName} dealt " +
			$"{result.AppliedDamage} damage to " +
			$"{targetName}. " +
			$"Remaining health=" +
			$"{result.RemainingHealth}.");

		if (!result.WasLethal)
			return;

		DebugLog.Print(
			$"{targetName} received lethal damage.");
	}

	private void OnHeroIncapacitated(
	HeroActorController hero)
	{
		if (!GodotObject.IsInstanceValid(hero))
			return;

		DebugLog.Print(
			$"Combat handling incapacitation for {hero.Name}.");

		bool wasRemoved =
			_heroParticipants.Remove(hero);

		if (!wasRemoved)
			return;

		hero.AttackReleased -= OnHeroAttackReleased;

		hero.AbilityReleased -= OnHeroAbilityReleased;

		hero.Incapacitated -= OnHeroIncapacitated;

		DebugLog.Print(
			$"{hero.Name} removed from active combat. " +
			$"Active heroes={_heroParticipants.Count}");

		RefreshMonsterTargets();

		if (HeroParticipantCount == 0
			&& MonsterParticipantCount > 0)
		{
			ResolveCombatOutcome(
				CombatOutcome.Defeat);

			Encounter.EndEncounterAsDefeat();
		}

		ApplyCombatState();
		EmitParticipantsChanged();
	}

	private void ResolveCombatOutcome(
	CombatOutcome outcome)
	{
		if (outcome == CombatOutcome.None
			|| CurrentOutcome != CombatOutcome.None)
		{
			return;
		}

		CurrentOutcome = outcome;
		ResetAutomaticTauntState();

		DebugLog.Print(
			$"Combat resolved: {CurrentOutcome}. " +
			$"Heroes={HeroParticipantCount}, " +
			$"Monsters={MonsterParticipantCount}");

		ApplyCombatState();
		CombatResolved?.Invoke(CurrentOutcome);
	}

	private void ApplyCombatState()
	{
		bool shouldCombatBeActive =
			CurrentOutcome == CombatOutcome.None
			&& HeroParticipantCount > 0
			&& MonsterParticipantCount > 0;

		if (IsCombatActive == shouldCombatBeActive)
			return;

		IsCombatActive = shouldCombatBeActive;

		DebugLog.Print(
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
		if (!GodotObject.IsInstanceValid(Encounter))
		{
			GD.PushError(
				"CombatController is missing its " +
				"Encounter Inspector reference.");

			return false;
		}
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
		if (!GodotObject.IsInstanceValid(DamageResolver))
		{
			GD.PushError(
				"CombatController is missing its " +
				"DamageResolver Inspector reference.");

			return false;
		}
		if (!GodotObject.IsInstanceValid(ProjectileScene))
		{
			GD.PushError(
				"CombatController is missing its " +
				"ProjectileScene Inspector reference.");
			return false;
		}
		if (!GodotObject.IsInstanceValid(Party))
		{
			GD.PushError(
				"CombatController is missing its " +
				"Party Inspector reference.");

			return false;
		}

		return true;
	}

	public override void _ExitTree()
	{
		UnsubscribeHeroParticipants();

		if (GodotObject.IsInstanceValid(Party))
		{
			Party.PartySpawned -= OnPartySpawned;
		}

		foreach (
			MonsterActorController monster
			in _monsterParticipants)
		{
			if (!GodotObject.IsInstanceValid(monster))
				continue;

			monster.AttackReleased -=
				OnMonsterAttackReleased;

			monster.AbilityReleased -=
				OnMonsterAbilityReleased;

			monster.ForcedTargetEnded -=
				OnMonsterForcedTargetEnded;
		}

		if (GodotObject.IsInstanceValid(Encounter))
		{
			Encounter.ActiveMonsterCountChanged -=
				OnActiveMonsterCountChanged;

			Encounter.EncounterStarted -=
				OnEncounterStarted;

			Encounter.EncounterCompleted -=
				OnEncounterCompleted;
		}
	}
}
