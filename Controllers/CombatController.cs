using Godot;
using System;
using System.Collections.Generic;

public partial class CombatController : Node
{
	private const float DirectDamageThreatMultiplier = 1.15f;
	private const float IndirectDamageThreatMultiplier = 1.0f;

	private enum HeroDamageOrigin
	{
		BasicAttack,
		Ability
	}

	[Signal]
	public delegate void ParticipantsChangedEventHandler(
		int heroCount,
		int monsterCount);

	[ExportCategory("Dependencies")]
	/// <summary>
	/// Inspector reference used by this component for its encounter dependency.
	/// Assign the matching node or resource from the scene; leaving it empty prevents that connection from working.
	/// </summary>
	[Export]
	public EncounterController Encounter { get; set; } = null!;

	/// <summary>
	/// Inspector reference used by this component for its actor layer dependency.
	/// Assign the matching node or resource from the scene; leaving it empty prevents that connection from working.
	/// </summary>
	[Export]
	public Node2D ActorLayer { get; set; } = null!;

	/// <summary>
	/// Inspector reference used by this component for its targeting dependency.
	/// Assign the matching node or resource from the scene; leaving it empty prevents that connection from working.
	/// </summary>
	[Export]
	public TargetingService Targeting { get; set; } = null!;

	/// <summary>
	/// Controls damage resolver, measured as damage points.
	/// For example, selecting a different value changes which damage resolver behavior or content the owning system uses.
	/// </summary>
	[Export]
	public CombatDamageResolver DamageResolver { get; set; } = null!;

	/// <summary>
	/// Resolves whether offensive attacks and abilities connect before damage
	/// or other hit-gated effects are applied. In 21F-A every check resolves
	/// to Hit; later checkpoints add real miss/dodge rules inside this service.
	/// </summary>
	[Export]
	public CombatHitResolver HitResolver { get; set; } = null!;

	/// <summary>
	/// Inspector reference used by this component for its party dependency.
	/// Assign the matching node or resource from the scene; leaving it empty prevents that connection from working.
	/// </summary>
	[Export]
	public PartyController Party { get; set; } = null!;


	[ExportCategory("Combat Content")]
	/// <summary>
	/// Inspector reference used by this component for its projectile scene dependency.
	/// Assign the matching node or resource from the scene; leaving it empty prevents that connection from working.
	/// </summary>
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

	private readonly List<ActiveDamageOverTimeEffect>
		_activeDamageOverTimeEffects = new();

	private sealed class ActiveDamageOverTimeEffect
	{
		public HeroActorController Source { get; }
		public MonsterActorController Target { get; }
		public AbilityDefinition Ability { get; }
		public int TotalTicks { get; }
		public int RemainingTicks { get; private set; }
		public double SecondsUntilNextTick { get; set; }

		/// <summary>
		/// Performs the active damage over time effect operation for Active Damage Over Time Effect.
		/// Uses the supplied arguments and current state and returns the resulting active damage over time effect to the caller.
		/// </summary>
		public ActiveDamageOverTimeEffect(
			HeroActorController source,
			MonsterActorController target,
			AbilityDefinition ability,
			int totalTicks)
		{
			Source = source;
			Target = target;
			Ability = ability;
			TotalTicks = totalTicks;
			Refresh();
		}

		public int AppliedTickCount =>
			TotalTicks - RemainingTicks;

		/// <summary>
		/// Performs the consume tick operation for Active Damage Over Time Effect.
		/// Uses the current node and service state; any result is applied through side effects, events, or stored fields.
		/// </summary>
		public void ConsumeTick()
		{
			RemainingTicks = Math.Max(
				RemainingTicks - 1,
				0);
		}

		/// <summary>
		/// Performs the refresh operation for Active Damage Over Time Effect.
		/// Uses the current node and service state; any result is applied through side effects, events, or stored fields.
		/// </summary>
		public void Refresh()
		{
			RemainingTicks = TotalTicks;
			SecondsUntilNextTick = Math.Max(
				Ability.EffectTickIntervalSeconds,
				0.05f);
		}
	}

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
	/// <summary>
	/// Performs the debug refresh hero participants operation for Active Damage Over Time Effect.
	/// Uses the current node and service state; any result is applied through side effects, events, or stored fields.
	/// </summary>
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

	/// <summary>
	/// Performs the debug incapacitate hero operation for Active Damage Over Time Effect.
	/// Uses the supplied arguments and current state and returns the resulting bool to the caller.
	/// </summary>
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

	/// <summary>
	/// Attempts to use hero ability without throwing when the operation cannot be completed.
	/// Uses the supplied arguments and current state and returns the resulting bool to the caller.
	/// </summary>
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

		if (!hero.CanAffordAbility(ability))
		{
			result =
				$"{hero.Name} needs {ability.ResourceCost:0.##} " +
				$"{hero.Resource.ResourceType} to use " +
				$"{ability.DisplayName}.";

			return false;
		}

		if (!hero.HasRequiredComboPoints(ability))
		{
			result =
				$"{hero.Name} needs {ability.ComboPointCost} combo point(s) " +
				$"to use {ability.DisplayName}. Current=" +
				$"{hero.ComboPoints.CurrentPoints}.";

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

		if (ability.EffectType != AbilityEffectType.AreaTaunt
			&& ability.EffectType != AbilityEffectType.DirectHealing
			&& ability.EffectType != AbilityEffectType.DirectDamage
			&& ability.EffectType != AbilityEffectType.ApplyStatusEffect)
		{
			return FailUnsupportedHeroAbility(
				ability,
				out result);
		}

		Node2D? target = ability.EffectType switch
		{
			AbilityEffectType.AreaTaunt =>
				ResolveAbilityTarget(hero, ability),

			AbilityEffectType.DirectHealing =>
				ResolveAutomaticHealingTarget(hero, ability),

			AbilityEffectType.DirectDamage =>
				ResolveAbilityTarget(hero, ability),

			AbilityEffectType.ApplyStatusEffect =>
				ResolveAbilityTarget(hero, ability),

			_ => null
		};

		if (target is null)
		{
			result = ability.EffectType
				== AbilityEffectType.DirectHealing
					? $"No living party member is below " +
						$"{ability.AutoCastHealthThresholdPercent:0.#}% health."
					: $"{ability.DisplayName} could not resolve a valid target.";

			return false;
		}

		// Debug/manual use skips the normal cast animation, but it still uses
		// the exact same authoritative commit operation as automatic abilities.
		if (!hero.TryCommitAbility(ability))
		{
			result =
				$"{hero.Name} could not commit {ability.DisplayName}. " +
				"Resource and cooldown were left unchanged.";

			return false;
		}

		bool abilityApplied = ability.EffectType switch
		{
			AbilityEffectType.AreaTaunt =>
				TryApplyAreaTaunt(
					hero,
					target,
					ability,
					out result),

			AbilityEffectType.DirectHealing
				when target is HeroActorController healingTarget =>
				TryApplyDirectHealing(
					hero,
					healingTarget,
					ability,
					out result),

			AbilityEffectType.DirectDamage
				when target is MonsterActorController damageTarget =>
				TryApplyDirectDamageAbility(
					hero,
					damageTarget,
					ability,
					out result),

			AbilityEffectType.ApplyStatusEffect =>
				TryApplyStatusEffectAbility(
					hero,
					target,
					ability,
					out result),

			_ => false
		};

		if (!abilityApplied)
		{
			result = string.IsNullOrWhiteSpace(result)
				? $"{hero.Name}'s {ability.DisplayName} committed but " +
					"did not apply an effect."
				: result;

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

	/// <summary>
	/// Applies one committed direct-damage hero ability. Ability damage uses
	/// authored calculation data and never awards a basic-attack combo point.
	/// </summary>
	private bool TryApplyDirectDamageAbility(
		HeroActorController caster,
		MonsterActorController target,
		AbilityDefinition ability,
		out string result)
	{
		result = string.Empty;

		if (!GodotObject.IsInstanceValid(ability))
		{
			result = "The direct-damage ability is invalid.";
			return false;
		}

		if (!GodotObject.IsInstanceValid(caster)
			|| !Targeting.IsValidMonsterTarget(target))
		{
			result = $"{ability.DisplayName} has no valid living monster target.";
			return false;
		}

		if (!TryResolveOffensiveHit(
			caster,
			target,
			ability.DodgeRule,
			ability,
			out CombatHitOutcome hitOutcome))
		{
			result =
				$"{caster.Name} used {ability.DisplayName} on {target.Name}, " +
				DescribeFailedHit(hitOutcome) + ".";

			// The ability already committed before release. An avoided result is
			// therefore a successful resolution, not a failed activation.
			return true;
		}

		float requestedDamage = CalculateHeroAbilityDamage(
			caster,
			ability);

		if (!float.IsFinite(requestedDamage)
			|| requestedDamage <= 0.0f)
		{
			result =
				$"{ability.DisplayName} resolved invalid damage " +
				$"({requestedDamage:0.##}).";
			return false;
		}

		DamageResult damage = ApplyHeroDamage(
			caster,
			target,
			requestedDamage,
			HeroDamageOrigin.Ability);

		DebugLog.Print(
			$"{caster.Name} used '{ability.DisplayName}' on {target.Name}. " +
			$"RequestedDamage={requestedDamage:0.##}; " +
			$"AppliedDamage={damage.AppliedDamage:0.##}.",
			DebugLogCategory.Ability);

		result =
			$"{caster.Name} used {ability.DisplayName} on {target.Name} " +
			$"for {damage.AppliedDamage:0.##} damage.";

		return true;
	}

	/// <summary>
	/// Applies a committed status-effect ability to one monster or to every
	/// valid monster inside its authored AOE. Each affected defender resolves
	/// Dodge independently. A successful application refreshes the same status
	/// rather than stacking duplicate runtime instances.
	/// </summary>
	private bool TryApplyStatusEffectAbility(
		HeroActorController caster,
		Node2D targetOrAreaAnchor,
		AbilityDefinition ability,
		out string result)
	{
		result = string.Empty;

		if (!GodotObject.IsInstanceValid(caster)
			|| !GodotObject.IsInstanceValid(targetOrAreaAnchor)
			|| !GodotObject.IsInstanceValid(ability)
			|| !GodotObject.IsInstanceValid(ability.AppliedStatusEffect)
			|| ability.EffectDurationSeconds <= 0.0f)
		{
			result = $"{ability.DisplayName} has invalid status-effect data.";
			return false;
		}

		CombatStatusEffectDefinition status =
			ability.AppliedStatusEffect;

		int eligibleCount = 0;
		int appliedCount = 0;
		int dodgedCount = 0;

		CombatStatusEffectApplicationContext applicationContext = new(
			caster,
			targetOrAreaAnchor.GlobalPosition);

		if (ability.TargetMode == AbilityTargetMode.AreaOfEffect)
		{
			if (ability.AreaTargetGroup != AbilityTargetGroup.Enemies
				&& ability.AreaTargetGroup != AbilityTargetGroup.Everyone)
			{
				result = $"{ability.DisplayName} does not target enemy actors.";
				return false;
			}

			foreach (MonsterActorController monster
				in _monsterParticipants)
			{
				if (!Targeting.IsValidMonsterTarget(monster)
					|| !IsInsideAbilityArea(
						targetOrAreaAnchor,
						monster,
						ability.AreaRadius))
				{
					continue;
				}

				eligibleCount++;

				if (!TryResolveOffensiveHit(
					caster,
					monster,
					ability.DodgeRule,
					ability,
					out _))
				{
					dodgedCount++;
					continue;
				}

				if (monster.StatusEffects.TryApplyOrRefresh(
					status,
					ability.EffectDurationSeconds,
					applicationContext))
				{
					appliedCount++;
				}
			}
		}
		else if (targetOrAreaAnchor is MonsterActorController target
			&& Targeting.IsValidMonsterTarget(target))
		{
			eligibleCount = 1;

			if (!TryResolveOffensiveHit(
				caster,
				target,
				ability.DodgeRule,
				ability,
				out _))
			{
				dodgedCount = 1;
			}
			else if (target.StatusEffects.TryApplyOrRefresh(
				status,
				ability.EffectDurationSeconds,
				applicationContext))
			{
				appliedCount = 1;
			}
		}
		else
		{
			result = $"{ability.DisplayName} could not resolve a valid status target.";
			return false;
		}

		DebugLog.Print(
			$"{caster.Name} used '{ability.DisplayName}'. " +
			$"Status='{status.DisplayName}'; " +
			$"Eligible={eligibleCount}; Applied={appliedCount}; " +
			$"Dodged={dodgedCount}; " +
			$"Duration={ability.EffectDurationSeconds:0.##}s.",
			DebugLogCategory.Ability);

		result =
			$"{caster.Name} used {ability.DisplayName}. " +
			$"Applied {status.DisplayName} to {appliedCount} " +
			$"of {eligibleCount} eligible monster(s); " +
			$"{dodgedCount} dodged.";

		// An AOE that found valid defenders but was entirely dodged still
		// resolved successfully after commit, exactly like a dodged direct hit.
		return eligibleCount > 0;
	}

	private static float CalculateHeroAbilityDamage(
		HeroActorController caster,
		AbilityDefinition ability)
	{
		return ability.DamageCalculationMode switch
		{
			AbilityDamageCalculationMode.Fixed =>
				Mathf.Max(ability.BaseDamage, 0.0f),

			AbilityDamageCalculationMode.BasicAttackMultiplier =>
				Mathf.Max(caster.CombatProfile.AttackDamage, 0.0f)
				* Mathf.Max(ability.BasicAttackDamageMultiplier, 0.0f),

			_ => 0.0f
		};
	}

	/// <summary>
	/// Attempts to apply direct healing without throwing when the operation cannot be completed.
	/// Uses the supplied arguments and current state and returns the resulting bool to the caller.
	/// </summary>
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

	/// <summary>
	/// Attempts to apply area taunt without throwing when the operation cannot be completed.
	/// Uses the supplied arguments and current state and returns the resulting bool to the caller.
	/// </summary>
	private bool TryApplyAreaTaunt(
		HeroActorController caster,
		Node2D areaAnchor,
		AbilityDefinition ability,
		out string result)
	{
		if (ability.TargetMode != AbilityTargetMode.AreaOfEffect
			|| ability.AreaTargetGroup != AbilityTargetGroup.Enemies
			|| (ability.AreaOrigin == AbilityAreaOrigin.Self
				&& areaAnchor != caster))
		{
			result =
				$"{ability.DisplayName} has invalid AOE targeting data.";

			return false;
		}

		float radiusSquared =
			ability.AreaRadius
			* ability.AreaRadius;

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
				areaAnchor.GlobalPosition.DistanceSquaredTo(
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
			$"Radius={ability.AreaRadius:0.##}; " +
			$"Duration={ability.EffectDurationSeconds:0.##}s.");

		result =
			$"{caster.Name} used {ability.DisplayName}. " +
			$"Taunted {affectedCount} monster(s) within " +
			$"{ability.AreaRadius:0.##} units for " +
			$"{ability.EffectDurationSeconds:0.##} seconds.";

		return true;
	}

	/// <summary>
	/// Performs the fail unsupported hero ability operation for Active Damage Over Time Effect.
	/// Uses the supplied arguments and current state and returns the resulting bool to the caller.
	/// </summary>
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
	/// <summary>
	/// Performs the unsubscribe hero participants operation for Active Damage Over Time Effect.
	/// Uses the current node and service state; any result is applied through side effects, events, or stored fields.
	/// </summary>
	private void UnsubscribeHeroParticipants()
	{
		foreach (
			HeroActorController hero
			in _heroParticipants)
		{
			if (!GodotObject.IsInstanceValid(hero))
				continue;

			hero.SetAutomaticAbilityPriorityResolver(null);

			hero.AttackReleased -=
				OnHeroAttackReleased;

			hero.AbilityReleased -=
				OnHeroAbilityReleased;

			hero.Incapacitated -=
				OnHeroIncapacitated;
		}
	}

	/// <summary>
	/// Runs Godot setup for Active Damage Over Time Effect when the node enters the scene tree.
	/// Uses the current node and service state; any result is applied through side effects, events, or stored fields.
	/// </summary>
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

	/// <summary>
	/// Handles the party spawned event and updates the related game state.
	/// Uses the supplied arguments and current node state; any result is applied through side effects, events, or stored fields.
	/// </summary>
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

	/// <summary>
	/// Performs the initialize combat participants operation for Active Damage Over Time Effect.
	/// Uses the current node and service state; any result is applied through side effects, events, or stored fields.
	/// </summary>
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

	/// <summary>
	/// Creates hero participants from the supplied configuration and current dependencies.
	/// Uses the current node and service state; any result is applied through side effects, events, or stored fields.
	/// </summary>
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

			hero.SetAutomaticAbilityPriorityResolver(
				TryBeginPriorityAutomaticHeroAbility);

			hero.AttackReleased += OnHeroAttackReleased;

			hero.AbilityReleased += OnHeroAbilityReleased;

			hero.Incapacitated += OnHeroIncapacitated;
		}
	}

	/// <summary>
	/// Handles the encounter started event and updates the related game state.
	/// Uses the current node and service state; any result is applied through side effects, events, or stored fields.
	/// </summary>
	private void OnEncounterStarted()
	{
		CurrentOutcome = CombatOutcome.None;
		ResetAutomaticTauntState();
		ClearActiveDamageOverTimeEffects();

		DebugLog.Print(
			"Combat outcome reset for new encounter.");

		ApplyCombatState();
	}

	/// <summary>
	/// Handles the encounter completed event and updates the related game state.
	/// Uses the current node and service state; any result is applied through side effects, events, or stored fields.
	/// </summary>
	private void OnEncounterCompleted()
	{
		ResolveCombatOutcome(CombatOutcome.Victory);
	}

	/// <summary>
	/// Handles the active monster count changed event and updates the related game state.
	/// Uses the supplied arguments and current node state; any result is applied through side effects, events, or stored fields.
	/// </summary>
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

	/// <summary>
	/// Handles the hero attack released event and updates the related game state.
	/// Uses the supplied arguments and current node state; any result is applied through side effects, events, or stored fields.
	/// </summary>
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

		if (TryReleaseAutomaticDamageOverTimeAbility(
			attacker,
			target))
		{
			return;
		}

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

	/// <summary>
	/// Handles the hero ability released event and updates the related game state.
	/// Uses the supplied arguments and current node state; any result is applied through side effects, events, or stored fields.
	/// </summary>
	private void OnHeroAbilityReleased(
		HeroActorController caster,
		Node2D target,
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
					target,
					ability,
					out _),

			AbilityEffectType.DirectHealing
				when target is HeroActorController healingTarget =>
				TryApplyDirectHealing(
					caster,
					healingTarget,
					ability,
					out _),

			AbilityEffectType.DirectDamage
				when target is MonsterActorController damageTarget =>
				TryApplyDirectDamageAbility(
					caster,
					damageTarget,
					ability,
					out _),

			AbilityEffectType.ApplyStatusEffect =>
				TryApplyStatusEffectAbility(
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

	/// <summary>
	/// Performs the confirm hero impact operation for Active Damage Over Time Effect.
	/// Uses the supplied arguments and current node state; any result is applied through side effects, events, or stored fields.
	/// </summary>
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

		if (!TryResolveOffensiveHit(
			attacker,
			target,
			CombatDodgeRule.DefenderDodge,
			null,
			out _))
		{
			return;
		}

		ApplyHeroDamage(
			attacker,
			target,
			attacker.CombatProfile.AttackDamage,
			HeroDamageOrigin.BasicAttack);
	}

	/// <summary>
	/// Resolves hero damage through one shared path while preserving its origin.
	/// Only confirmed basic attacks are allowed to generate rogue combo points;
	/// direct-damage abilities use the same damage/threat/death flow without
	/// masquerading as basic attacks.
	/// </summary>
	private DamageResult ApplyHeroDamage(
		HeroActorController attacker,
		MonsterActorController target,
		float requestedDamage,
		HeroDamageOrigin origin)
	{
		DamageResult result = DamageResolver.Resolve(
			new DamageRequest(
				attacker,
				target,
				requestedDamage),
			target.Health);

		if (origin == HeroDamageOrigin.BasicAttack
			&& attacker.TryAddComboPointFromDamage(
				result.AppliedDamage))
		{
			DebugLog.Print(
				$"{attacker.Name} gained a combo point. " +
				$"Combo={attacker.ComboPoints.CurrentPoints}/" +
				$"{HeroComboPointState.MaximumPoints}.",
				DebugLogCategory.Ability);
		}

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

		return result;
	}

	/// <summary>
	/// Performs the broadcast hero damage threat operation for Active Damage Over Time Effect.
	/// Uses the supplied arguments and current node state; any result is applied through side effects, events, or stored fields.
	/// </summary>
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

	/// <summary>
	/// Handles the pending projectile release event and updates the related game state.
	/// Uses the supplied arguments and current node state; any result is applied through side effects, events, or stored fields.
	/// </summary>
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

	/// <summary>
	/// Handles the hero projectile impacted event and updates the related game state.
	/// Uses the supplied arguments and current node state; any result is applied through side effects, events, or stored fields.
	/// </summary>
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

	/// <summary>
	/// Attempts to release automatic damage over time ability without throwing when the operation cannot be completed.
	/// Uses the supplied arguments and current state and returns the resulting bool to the caller.
	/// </summary>
	private bool TryReleaseAutomaticDamageOverTimeAbility(
		HeroActorController attacker,
		MonsterActorController target)
	{
		if (!IsCombatActive
			|| target.IsDead
			|| !target.Health.IsAlive)
		{
			return false;
		}

		foreach (AbilityDefinition ability
			in attacker.Abilities)
		{
			if (!GodotObject.IsInstanceValid(ability)
				|| ability.EffectType
					!= AbilityEffectType.DamageOverTime
				|| ability.TargetMode
					!= AbilityTargetMode.CurrentTarget
				|| !attacker.IsAbilityReady(
					ability.ContentId)
				|| !attacker.CanAffordAbility(ability)
				|| !attacker.TryCommitAbility(ability))
			{
				continue;
			}

			DebugLog.Print(
				$"{attacker.Name} began using ability " +
				$"'{ability.DisplayName}' on {target.Name}. " +
				"Cast=0s.",
				DebugLogCategory.Ability);

			DebugLog.Print(
				$"{attacker.Name} released ability " +
				$"'{ability.DisplayName}' on {target.Name}.",
				DebugLogCategory.Ability);

			switch (attacker.CombatProfile.AttackDelivery)
			{
				case AttackDeliveryMode.Projectile:
					HandlePendingDamageOverTimeProjectileRelease(
						attacker,
						target,
						ability);
					break;

				case AttackDeliveryMode.ImmediateImpact:
				case AttackDeliveryMode.Hitscan:
					TryApplyDamageOverTimeEffect(
						attacker,
						target,
						ability);
					break;
			}

			return true;
		}

		return false;
	}

	/// <summary>
	/// Handles the pending damage over time projectile release event and updates the related game state.
	/// Uses the supplied arguments and current node state; any result is applied through side effects, events, or stored fields.
	/// </summary>
	private void HandlePendingDamageOverTimeProjectileRelease(
		HeroActorController attacker,
		MonsterActorController target,
		AbilityDefinition ability)
	{
		ProjectileActorController projectile =
			ProjectileScene.Instantiate
				<ProjectileActorController>();

		ActorLayer.AddChild(projectile);

		projectile.Impacted +=
			OnDamageOverTimeProjectileImpacted;

		projectile.Initialize(
			attacker,
			target,
			attacker.ProjectileOrigin.GlobalPosition,
			ability);

		DebugLog.Print(
			$"Ability projectile created: " +
			$"{attacker.Name} → {target.Name} " +
			$"using '{ability.DisplayName}'.",
			DebugLogCategory.Ability);
	}

	/// <summary>
	/// Handles the damage over time projectile impacted event and updates the related game state.
	/// Uses the supplied arguments and current node state; any result is applied through side effects, events, or stored fields.
	/// </summary>
	private void OnDamageOverTimeProjectileImpacted(
		ProjectileActorController projectile,
		HeroActorController attacker,
		MonsterActorController target)
	{
		if (GodotObject.IsInstanceValid(projectile))
		{
			projectile.Impacted -=
				OnDamageOverTimeProjectileImpacted;
		}

		AbilityDefinition? ability =
			GodotObject.IsInstanceValid(projectile)
				? projectile.Ability
				: null;

		if (GodotObject.IsInstanceValid(attacker)
			&& GodotObject.IsInstanceValid(target)
			&& GodotObject.IsInstanceValid(ability)
			&& !target.IsDead
			&& target.Health.IsAlive)
		{
			TryApplyDamageOverTimeEffect(
				attacker,
				target,
				ability!);
		}

		if (GodotObject.IsInstanceValid(projectile))
			projectile.QueueFree();
	}

	/// <summary>
	/// Attempts to apply damage over time effect without throwing when the operation cannot be completed.
	/// Uses the supplied arguments and current node state; any result is applied through side effects, events, or stored fields.
	/// </summary>
	private void TryApplyDamageOverTimeEffect(
		HeroActorController source,
		MonsterActorController target,
		AbilityDefinition ability)
	{
		if (!TryResolveOffensiveHit(
			source,
			target,
			ability.DodgeRule,
			ability,
			out _))
		{
			// Only the initial application checks accuracy. Existing DOT ticks do
			// not reroll hit/miss/dodge once the effect is successfully applied.
			return;
		}

		int totalTicks = Math.Max(
			(int)Math.Floor(
				ability.EffectDurationSeconds
				/ Math.Max(
					ability.EffectTickIntervalSeconds,
					0.05f)
				+ 0.0001f),
			1);

		foreach (ActiveDamageOverTimeEffect activeEffect
			in _activeDamageOverTimeEffects)
		{
			if (activeEffect.Source != source
				|| activeEffect.Target != target
				|| !activeEffect.Ability.ContentId.Equals(
					ability.ContentId,
					StringComparison.OrdinalIgnoreCase))
			{
				continue;
			}

			activeEffect.Refresh();

			DebugLog.Print(
				$"{source.Name} refreshed " +
				$"'{ability.DisplayName}' on {target.Name}. " +
				$"Damage={ability.BaseDamage:0.##} every " +
				$"{ability.EffectTickIntervalSeconds:0.##}s; " +
				$"Ticks={totalTicks}.",
				DebugLogCategory.Ability);

			return;
		}

		_activeDamageOverTimeEffects.Add(
			new ActiveDamageOverTimeEffect(
				source,
				target,
				ability,
				totalTicks));

		DebugLog.Print(
			$"{source.Name} applied '{ability.DisplayName}' " +
			$"to {target.Name}. " +
			$"Damage={ability.BaseDamage:0.##} every " +
			$"{ability.EffectTickIntervalSeconds:0.##}s; " +
			$"Ticks={totalTicks}; " +
			$"Duration={ability.EffectDurationSeconds:0.##}s.",
			DebugLogCategory.Ability);
	}

	/// <summary>
	/// Performs the refresh hero targets operation for Active Damage Over Time Effect.
	/// Uses the current node and service state; any result is applied through side effects, events, or stored fields.
	/// </summary>
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

	/// <summary>
	/// Performs the refresh monster targets operation for Active Damage Over Time Effect.
	/// Uses the current node and service state; any result is applied through side effects, events, or stored fields.
	/// </summary>
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

	/// <summary>
	/// Attempts to switch monster threat target without throwing when the operation cannot be completed.
	/// Uses the supplied arguments and current node state; any result is applied through side effects, events, or stored fields.
	/// </summary>
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

	/// <summary>
	/// Updates Active Damage Over Time Effect every rendered frame using the supplied frame delta.
	/// Uses the supplied arguments and current node state; any result is applied through side effects, events, or stored fields.
	/// </summary>
	public override void _Process(double delta)
	{
		if (!IsInitialized)
			return;

		if (IsCombatActive)
		{
			UpdateActiveDamageOverTimeEffects(delta);
		}
		else
		{
			ClearActiveDamageOverTimeEffects();
		}

		UpdateAutomaticHeroAbilities(delta);

		if (!IsCombatActive)
			return;

		RefreshHeroTargets();
		RefreshMonsterTargets();
	}

	/// <summary>
	/// Recalculates active damage over time effects from the latest runtime state.
	/// Uses the supplied arguments and current node state; any result is applied through side effects, events, or stored fields.
	/// </summary>
	private void UpdateActiveDamageOverTimeEffects(
		double delta)
	{
		double elapsedSeconds = Math.Max(delta, 0.0);

		for (int index =
			_activeDamageOverTimeEffects.Count - 1;
			index >= 0;
			index--)
		{
			ActiveDamageOverTimeEffect activeEffect =
				_activeDamageOverTimeEffects[index];

			if (!IsValidDamageOverTimeEffect(activeEffect))
			{
				_activeDamageOverTimeEffects.RemoveAt(index);
				continue;
			}

			activeEffect.SecondsUntilNextTick -=
				elapsedSeconds;

			while (activeEffect.RemainingTicks > 0
				&& activeEffect.SecondsUntilNextTick <= 0.0)
			{
				bool targetSurvived =
					ApplyDamageOverTimeTick(activeEffect);

				activeEffect.ConsumeTick();

				if (!targetSurvived)
					break;

				activeEffect.SecondsUntilNextTick +=
					Math.Max(
						activeEffect.Ability
							.EffectTickIntervalSeconds,
						0.05f);
			}

			if (activeEffect.RemainingTicks > 0
				&& IsValidDamageOverTimeEffect(activeEffect))
			{
				continue;
			}

			if (activeEffect.RemainingTicks == 0
				&& GodotObject.IsInstanceValid(
					activeEffect.Target)
				&& !activeEffect.Target.IsDead)
			{
				DebugLog.Print(
					$"'{activeEffect.Ability.DisplayName}' " +
					$"expired on {activeEffect.Target.Name} " +
					$"after {activeEffect.TotalTicks} ticks.",
					DebugLogCategory.Ability);
			}

			_activeDamageOverTimeEffects.RemoveAt(index);
		}
	}

	/// <summary>
	/// Applies damage over time tick to the relevant actor, resource, or presentation state.
	/// Uses the supplied arguments and current state and returns the resulting bool to the caller.
	/// </summary>
	private bool ApplyDamageOverTimeTick(
		ActiveDamageOverTimeEffect activeEffect)
	{
		HeroActorController source = activeEffect.Source;
		MonsterActorController target = activeEffect.Target;
		AbilityDefinition ability = activeEffect.Ability;

		DamageResult result = DamageResolver.Resolve(
			new DamageRequest(
				source,
				target,
				ability.BaseDamage),
			target.Health);

		if (!source.IsIncapacitated
			&& source.Health.IsAlive)
		{
			BroadcastHeroDamageThreat(
				source,
				target,
				result.AppliedDamage);
		}

		RaiseCombatEvent(
			new CombatEvent
			{
				Type = CombatEventType.DamageApplied,
				Attacker = source,
				Target = target,
				Damage = result
			});

		DebugLog.Print(
			$"{source.Name}'s '{ability.DisplayName}' dealt " +
			$"{result.AppliedDamage:0.##} poison damage to " +
			$"{target.Name}. " +
			$"Tick={activeEffect.AppliedTickCount + 1}/" +
			$"{activeEffect.TotalTicks}; " +
			$"Remaining health={result.RemainingHealth:0.##}.",
			DebugLogCategory.Ability);

		if (!result.WasLethal)
			return true;

		target.EnterDeadState();

		RaiseCombatEvent(
			new CombatEvent
			{
				Type = CombatEventType.ActorDied,
				Attacker = source,
				Target = target,
				Damage = result
			});

		return false;
	}

	/// <summary>
	/// Performs the is valid damage over time effect operation for Active Damage Over Time Effect.
	/// Uses the supplied arguments and current state and returns the resulting bool to the caller.
	/// </summary>
	private static bool IsValidDamageOverTimeEffect(
		ActiveDamageOverTimeEffect activeEffect)
	{
		return GodotObject.IsInstanceValid(activeEffect.Source)
			&& GodotObject.IsInstanceValid(activeEffect.Target)
			&& GodotObject.IsInstanceValid(activeEffect.Ability)
			&& activeEffect.Target.IsInsideTree()
			&& !activeEffect.Target.IsDead
			&& activeEffect.Target.Health.IsAlive;
	}

	/// <summary>
	/// Resets active damage over time effects so the system can begin from a clean state.
	/// Uses the current node and service state; any result is applied through side effects, events, or stored fields.
	/// </summary>
	private void ClearActiveDamageOverTimeEffects()
	{
		if (_activeDamageOverTimeEffects.Count == 0)
			return;

		_activeDamageOverTimeEffects.Clear();
	}

	/// <summary>
	/// Recalculates automatic hero abilities from the latest runtime state.
	/// Uses the supplied arguments and current node state; any result is applied through side effects, events, or stored fields.
	/// </summary>
	private void UpdateAutomaticHeroAbilities(double delta)
	{
		foreach (HeroActorController hero
			in _heroParticipants)
		{
			TryBeginAutomaticHeroAbility(
				hero,
				delta);
		}
	}

	/// <summary>
	/// Gives automatic abilities one final priority check at the exact moment a
	/// hero would otherwise begin a new basic attack. Passing zero delta avoids
	/// advancing time-based triggers twice in the same frame; those timers are
	/// advanced by UpdateAutomaticHeroAbilities.
	/// </summary>
	private bool TryBeginPriorityAutomaticHeroAbility(
		HeroActorController hero)
	{
		return TryBeginAutomaticHeroAbility(
			hero,
			0.0);
	}

	/// <summary>
	/// Resolves and begins the first automatic ability whose normal readiness,
	/// targeting, and effect-specific use rules are all satisfied. Ability list
	/// order remains the tie-breaker between multiple simultaneously valid
	/// abilities. A basic attack already in progress is never interrupted;
	/// HeroActorController rejects that transition and the ability is retried
	/// before the next basic attack can begin.
	/// </summary>
	private bool TryBeginAutomaticHeroAbility(
		HeroActorController hero,
		double delta)
	{
		if (!GodotObject.IsInstanceValid(hero)
			|| hero.IsIncapacitated
			|| !hero.Health.IsAlive
			|| hero.IsUsingAbility)
		{
			return false;
		}

		if (TryBeginPartySupportAbility(hero))
			return true;

		foreach (AbilityDefinition ability
			in hero.Abilities)
		{
			if (!GodotObject.IsInstanceValid(ability))
				continue;

			bool abilityReady =
				hero.IsAbilityReady(ability.ContentId)
				&& hero.CanAffordAbility(ability)
				&& hero.HasRequiredComboPoints(ability);

			Node2D? abilityTarget = null;

			switch (ability.EffectType)
			{
				case AbilityEffectType.DirectHealing
					when abilityReady:
					abilityTarget =
						ResolveAutomaticHealingTarget(
							hero,
							ability);
					break;

				case AbilityEffectType.DirectDamage
					when abilityReady:
					abilityTarget =
						ResolveAbilityTarget(
							hero,
							ability);
					break;

				case AbilityEffectType.AreaTaunt:
					bool shouldTaunt =
						ShouldUseAutomaticTaunt(
							hero,
							ability,
							delta);

					if (abilityReady && shouldTaunt)
					{
						abilityTarget =
							ResolveAbilityTarget(
								hero,
								ability);
					}

					break;
			}

			if (abilityTarget is null)
				continue;

			if (!hero.TryBeginAbility(
				ability,
				abilityTarget))
			{
				continue;
			}

			if (ability.EffectType
				== AbilityEffectType.AreaTaunt)
			{
				ConsumeAutomaticTauntTrigger(hero);
			}

			return true;
		}

		return false;
	}

	/// <summary>
	/// Gives authored party-support abilities first choice while this hero is
	/// actively rescuing a pressured ally. The support role only changes which
	/// target/context makes the ability desirable; normal ownership, cooldown,
	/// resource, combo-point, range, and cast rules still apply.
	/// </summary>
	private bool TryBeginPartySupportAbility(
		HeroActorController hero)
	{
		if (!hero.IsPartySupportActive
			|| hero.PartySupportAlly is not HeroActorController rescueAlly
			|| !TargetingService.IsValidHeroTarget(rescueAlly))
		{
			return false;
		}

		AbilityDefinition? selectedAbility = null;
		Node2D? selectedTarget = null;
		float selectedScore = float.MinValue;
		string selectedSituation = string.Empty;

		foreach (AbilityDefinition ability in hero.Abilities)
		{
			if (!GodotObject.IsInstanceValid(ability)
				|| ability.SupportRole == AbilitySupportRole.None
				|| !hero.IsAbilityReady(ability.ContentId)
				|| !hero.CanAffordAbility(ability)
				|| !hero.HasRequiredComboPoints(ability))
			{
				continue;
			}

			Node2D? supportTarget =
				ResolvePartySupportAbilityTarget(
					hero,
					rescueAlly,
					ability);

			if (supportTarget is null)
				continue;

			float supportScore =
				CalculatePartySupportAbilityScore(
					rescueAlly,
					ability,
					supportTarget,
					out string situationSummary);

			// Highest final situational score wins. Equal scores deliberately keep
			// the first ability in authored list order as a stable tie-breaker.
			if (selectedAbility is not null
				&& supportScore <= selectedScore)
			{
				continue;
			}

			selectedAbility = ability;
			selectedTarget = supportTarget;
			selectedScore = supportScore;
			selectedSituation = situationSummary;
		}

		if (selectedAbility is null
			|| selectedTarget is null
			|| !hero.TryBeginAbility(
				selectedAbility,
				selectedTarget))
		{
			return false;
		}

		if (selectedAbility.EffectType
			== AbilityEffectType.AreaTaunt)
		{
			ConsumeAutomaticTauntTrigger(hero);
		}

		DebugLog.Print(
			$"{hero.Name} prioritized support ability " +
				$"'{selectedAbility.DisplayName}' for {rescueAlly.Name}. " +
				$"SupportRole={selectedAbility.SupportRole}; " +
				$"Base={selectedAbility.SupportPriority:0.##}; " +
				$"{selectedSituation}; Final={selectedScore:0.##}.",
			DebugLogCategory.Ability);

		return true;
	}

	/// <summary>
	/// Adds situational context to an ability's authored support priority. Peel
	/// abilities gain value for each threatening monster they can actually affect.
	/// RecoverAlly abilities gain value as the missing-health coverage they can
	/// reach increases. The authored weights keep these behaviors tunable without
	/// introducing class-specific decision branches.
	/// </summary>
	private float CalculatePartySupportAbilityScore(
		HeroActorController rescueAlly,
		AbilityDefinition ability,
		Node2D supportTarget,
		out string situationSummary)
	{
		float score = ability.SupportPriority;

		switch (ability.SupportRole)
		{
			case AbilitySupportRole.Peel:
			{
				int affectedThreats =
					CountAffectedRescueThreats(
						rescueAlly,
						ability,
						supportTarget);

				float situationalBonus =
					affectedThreats
					* ability.SupportPriorityPerThreatAffected;

				situationSummary =
					$"ThreatsAffected={affectedThreats}; " +
					$"SituationBonus={situationalBonus:0.##}";

				return score + situationalBonus;
			}

			case AbilitySupportRole.RecoverAlly:
			{
				float missingHealthCoverage =
					CalculateRecoverAllyMissingHealthCoverage(
						rescueAlly,
						ability,
						supportTarget);

				float situationalBonus =
					missingHealthCoverage
					* ability.SupportPriorityPerMissingHealthPercent;

				situationSummary =
					$"MissingHealthCoverage={missingHealthCoverage:0.##}%; " +
					$"SituationBonus={situationalBonus:0.##}";

				return score + situationalBonus;
			}

			default:
				situationSummary = "SituationBonus=0";
				return score;
		}
	}

	/// <summary>
	/// Counts only monsters currently attacking the rescued ally that the chosen
	/// Peel ability would actually affect from its resolved target or AOE anchor.
	/// </summary>
	private int CountAffectedRescueThreats(
		HeroActorController rescueAlly,
		AbilityDefinition ability,
		Node2D supportTarget)
	{
		int affectedThreats = 0;

		foreach (MonsterActorController monster
			in _monsterParticipants)
		{
			if (!Targeting.IsValidMonsterTarget(monster)
				|| monster.CurrentTarget != rescueAlly)
			{
				continue;
			}

			bool isAffected = ability.TargetMode switch
			{
				AbilityTargetMode.CurrentTarget
					or AbilityTargetMode.Monster =>
					supportTarget == monster,

				AbilityTargetMode.AreaOfEffect =>
					IsInsideAbilityArea(
						supportTarget,
						monster,
						ability.AreaRadius),

				_ => false
			};

			if (isAffected)
				affectedThreats++;
		}

		return affectedThreats;
	}

	/// <summary>
	/// Returns the missing-health percentage covered by a recovery ability. A
	/// single-target ability considers the rescued ally. An AOE ability sums all
	/// living party members inside the resolved area so group recovery naturally
	/// becomes more valuable when several nearby allies are injured.
	/// </summary>
	private float CalculateRecoverAllyMissingHealthCoverage(
		HeroActorController rescueAlly,
		AbilityDefinition ability,
		Node2D supportTarget)
	{
		if (ability.TargetMode != AbilityTargetMode.AreaOfEffect)
			return GetMissingHealthPercent(rescueAlly);

		float totalMissingHealthPercent = 0.0f;

		foreach (HeroActorController partyMember
			in _heroParticipants)
		{
			if (!TargetingService.IsValidHeroTarget(partyMember)
				|| !IsInsideAbilityArea(
					supportTarget,
					partyMember,
					ability.AreaRadius))
			{
				continue;
			}

			totalMissingHealthPercent +=
				GetMissingHealthPercent(partyMember);
		}

		return totalMissingHealthPercent;
	}

	private static float GetMissingHealthPercent(
		HeroActorController hero)
	{
		if (hero.Health.MaximumHealth <= 0.0f)
			return 0.0f;

		float healthPercent =
			hero.Health.CurrentHealth
			/ hero.Health.MaximumHealth
			* 100.0f;

		return Mathf.Clamp(
			100.0f - healthPercent,
			0.0f,
			100.0f);
	}

	/// <summary>
	/// Resolves a support ability against the ally already selected by the party
	/// support system. RecoverAlly abilities act on that ally; Peel abilities act
	/// on monsters currently targeting that ally.
	/// </summary>
	private Node2D? ResolvePartySupportAbilityTarget(
		HeroActorController caster,
		HeroActorController rescueAlly,
		AbilityDefinition ability)
	{
		return ability.SupportRole switch
		{
			AbilitySupportRole.RecoverAlly =>
				ResolveRecoverAllySupportTarget(
					caster,
					rescueAlly,
					ability),

			AbilitySupportRole.Peel =>
				ResolvePeelSupportTarget(
					caster,
					rescueAlly,
					ability),

			_ => null
		};
	}

	private Node2D? ResolveRecoverAllySupportTarget(
		HeroActorController caster,
		HeroActorController rescueAlly,
		AbilityDefinition ability)
	{
		// Preserve the ability's normal healing threshold. Party support changes
		// priority and target focus; it does not make a heal ignore its authored
		// automatic-use condition.
		if (ability.EffectType == AbilityEffectType.DirectHealing
			&& !IsBelowAutomaticHealthThreshold(
				rescueAlly,
				ability))
		{
			return null;
		}

		if (ability.TargetMode == AbilityTargetMode.Ally)
		{
			return caster.IsWithinAbilityRange(
				ability,
				rescueAlly)
					? rescueAlly
					: null;
		}

		if (ability.TargetMode != AbilityTargetMode.AreaOfEffect
			|| (ability.AreaTargetGroup != AbilityTargetGroup.Allies
				&& ability.AreaTargetGroup != AbilityTargetGroup.Everyone))
		{
			return null;
		}

		if (ability.AreaOrigin == AbilityAreaOrigin.Self)
		{
			return IsInsideAbilityArea(
				caster,
				rescueAlly,
				ability.AreaRadius)
					? caster
					: null;
		}

		return caster.IsWithinAbilityRange(
			ability,
			rescueAlly)
				? rescueAlly
				: null;
	}

	private Node2D? ResolvePeelSupportTarget(
		HeroActorController caster,
		HeroActorController rescueAlly,
		AbilityDefinition ability)
	{
		List<MonsterActorController> threats = new();

		foreach (MonsterActorController monster
			in _monsterParticipants)
		{
			if (Targeting.IsValidMonsterTarget(monster)
				&& monster.CurrentTarget == rescueAlly)
			{
				threats.Add(monster);
			}
		}

		if (threats.Count == 0)
			return null;

		if (ability.TargetMode == AbilityTargetMode.CurrentTarget)
		{
			MonsterActorController? currentTarget =
				caster.CurrentTarget;

			return currentTarget is not null
				&& threats.Contains(currentTarget)
				&& caster.IsWithinAbilityRange(
					ability,
					currentTarget)
					? currentTarget
					: null;
		}

		if (ability.TargetMode == AbilityTargetMode.Monster)
		{
			return ResolveAbilityTarget(
				caster,
				ability,
				monsterCandidates: threats);
		}

		if (ability.TargetMode != AbilityTargetMode.AreaOfEffect
			|| (ability.AreaTargetGroup != AbilityTargetGroup.Enemies
				&& ability.AreaTargetGroup != AbilityTargetGroup.Everyone))
		{
			return null;
		}

		if (ability.AreaOrigin == AbilityAreaOrigin.Self)
		{
			foreach (MonsterActorController threat in threats)
			{
				if (IsInsideAbilityArea(
					caster,
					threat,
					ability.AreaRadius))
				{
					return caster;
				}
			}

			return null;
		}

		return SelectBestTargetCenteredPeelAnchor(
			caster,
			ability,
			threats);
	}

	/// <summary>
	/// Chooses the target-centered AOE anchor that catches the greatest number
	/// of monsters currently pressuring the rescued ally. Authored target
	/// selection style remains the deterministic tie-breaker between equally
	/// strong anchors. This keeps ranged AOE Peel abilities focused on the
	/// actual swarm rather than on arbitrary ability-list or monster order.
	/// </summary>
	private MonsterActorController? SelectBestTargetCenteredPeelAnchor(
		HeroActorController caster,
		AbilityDefinition ability,
		IReadOnlyList<MonsterActorController> threats)
	{
		List<MonsterActorController> bestAnchors = new();
		int bestAffectedCount = 0;

		foreach (MonsterActorController candidate in threats)
		{
			if (!Targeting.IsValidMonsterTarget(candidate)
				|| !caster.IsWithinAbilityRange(ability, candidate))
			{
				continue;
			}

			int affectedCount = 0;

			foreach (MonsterActorController threat in threats)
			{
				if (Targeting.IsValidMonsterTarget(threat)
					&& IsInsideAbilityArea(
						candidate,
						threat,
						ability.AreaRadius))
				{
					affectedCount++;
				}
			}

			if (affectedCount < bestAffectedCount)
				continue;

			if (affectedCount > bestAffectedCount)
			{
				bestAffectedCount = affectedCount;
				bestAnchors.Clear();
			}

			bestAnchors.Add(candidate);
		}

		if (bestAnchors.Count == 0)
			return null;

		return Targeting.SelectAbilityMonsterTarget(
			caster,
			bestAnchors,
			ability.TargetSelectionStyle);
	}

	private static bool IsInsideAbilityArea(
		Node2D areaAnchor,
		Node2D target,
		float radius)
	{
		float clampedRadius = Mathf.Max(radius, 0.0f);

		return areaAnchor.GlobalPosition.DistanceSquaredTo(
			target.GlobalPosition)
			<= clampedRadius * clampedRadius;
	}

	/// <summary>
	/// Performs the should use automatic taunt operation for Active Damage Over Time Effect.
	/// Uses the supplied arguments and current state and returns the resulting bool to the caller.
	/// </summary>
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

	/// <summary>
	/// Performs the is holding any monster aggro operation for Active Damage Over Time Effect.
	/// Uses the supplied arguments and current state and returns the resulting bool to the caller.
	/// </summary>
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

	/// <summary>
	/// Performs the arm taunt recovery after aggro loss operation for Active Damage Over Time Effect.
	/// Uses the supplied arguments and current node state; any result is applied through side effects, events, or stored fields.
	/// </summary>
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

	/// <summary>
	/// Performs the has area taunt ability operation for Active Damage Over Time Effect.
	/// Uses the supplied arguments and current state and returns the resulting bool to the caller.
	/// </summary>
	private static bool HasAreaTauntAbility(
		HeroActorController hero)
	{
		foreach (AbilityDefinition ability in hero.Abilities)
		{
			if (GodotObject.IsInstanceValid(ability)
				&& ability.EffectType
					== AbilityEffectType.AreaTaunt
				&& ability.TargetMode
					== AbilityTargetMode.AreaOfEffect)
			{
				return true;
			}
		}

		return false;
	}

	/// <summary>
	/// Performs the consume automatic taunt trigger operation for Active Damage Over Time Effect.
	/// Uses the supplied arguments and current node state; any result is applied through side effects, events, or stored fields.
	/// </summary>
	private void ConsumeAutomaticTauntTrigger(
		HeroActorController caster)
	{
		_tauntRecoveryArmed.Remove(caster);
		_zeroAggroTauntElapsed.Remove(caster);
	}

	/// <summary>
	/// Resets automatic taunt state so the system can begin from a clean state.
	/// Uses the current node and service state; any result is applied through side effects, events, or stored fields.
	/// </summary>
	private void ResetAutomaticTauntState()
	{
		_tauntRecoveryArmed.Clear();
		_zeroAggroTauntElapsed.Clear();
		_forcedTauntCasters.Clear();
	}

	/// <summary>
	/// Resolves the target for an automatically used healing ability. The
	/// ability says what kind of target it wants; TargetingService decides
	/// which eligible ally wins when selection is required.
	/// </summary>
	private HeroActorController? ResolveAutomaticHealingTarget(
		HeroActorController caster,
		AbilityDefinition ability)
	{
		if (ability.TargetMode == AbilityTargetMode.Self)
		{
			return IsBelowAutomaticHealthThreshold(
				caster,
				ability)
				? ResolveAbilityTarget(caster, ability)
					as HeroActorController
				: null;
		}

		if (ability.TargetMode != AbilityTargetMode.Ally)
			return null;

		List<HeroActorController> eligibleAllies = new();

		foreach (HeroActorController candidate
			in Party.SpawnedHeroes)
		{
			if (!TargetingService.IsValidHeroTarget(candidate)
				|| !IsBelowAutomaticHealthThreshold(
					candidate,
					ability))
			{
				continue;
			}

			eligibleAllies.Add(candidate);
		}

		return ResolveAbilityTarget(
			caster,
			ability,
			eligibleAllies)
			as HeroActorController;
	}

	/// <summary>
	/// Resolves the authored target independently from the ability effect. This
	/// is the shared path for self, ally, monster, current-target, and AOE
	/// abilities. Callers may provide a pre-filtered candidate set when AI
	/// rules such as a healing threshold define eligibility.
	/// </summary>
	private Node2D? ResolveAbilityTarget(
		HeroActorController caster,
		AbilityDefinition ability,
		IReadOnlyList<HeroActorController>? allyCandidates = null,
		IReadOnlyList<MonsterActorController>? monsterCandidates = null)
	{
		allyCandidates ??= Party.SpawnedHeroes;
		monsterCandidates ??= _monsterParticipants;

		return ability.TargetMode switch
		{
			AbilityTargetMode.CurrentTarget =>
				Targeting.IsValidMonsterTarget(caster.CurrentTarget)
				&& caster.IsWithinAbilityRange(
					ability,
					caster.CurrentTarget!)
					? caster.CurrentTarget
					: null,

			AbilityTargetMode.Self => caster,

			AbilityTargetMode.Ally =>
				SelectAbilityAllyTargetInRange(
					caster,
					ability,
					allyCandidates),

			AbilityTargetMode.Monster =>
				SelectAbilityMonsterTargetInRange(
					caster,
					ability,
					monsterCandidates),

			AbilityTargetMode.AreaOfEffect =>
				ResolveAreaAnchor(
					caster,
					ability,
					allyCandidates,
					monsterCandidates),

			_ => null
		};
	}

	private Node2D? ResolveAreaAnchor(
		HeroActorController caster,
		AbilityDefinition ability,
		IReadOnlyList<HeroActorController> allyCandidates,
		IReadOnlyList<MonsterActorController> monsterCandidates)
	{
		if (ability.AreaOrigin == AbilityAreaOrigin.Self)
			return caster;

		return ability.AreaTargetGroup switch
		{
			AbilityTargetGroup.Allies =>
				SelectAbilityAllyTargetInRange(
					caster,
					ability,
					allyCandidates),

			AbilityTargetGroup.Enemies =>
				SelectAbilityMonsterTargetInRange(
					caster,
					ability,
					monsterCandidates),

			_ => null
		};
	}

	private HeroActorController? SelectAbilityAllyTargetInRange(
		HeroActorController caster,
		AbilityDefinition ability,
		IReadOnlyList<HeroActorController> candidates)
	{
		List<HeroActorController> inRange = new();

		foreach (HeroActorController hero in candidates)
		{
			if (TargetingService.IsValidHeroTarget(hero)
				&& caster.IsWithinAbilityRange(ability, hero))
			{
				inRange.Add(hero);
			}
		}

		return Targeting.SelectAbilityAllyTarget(
			caster,
			inRange,
			ability.TargetSelectionStyle);
	}

	private MonsterActorController? SelectAbilityMonsterTargetInRange(
		HeroActorController caster,
		AbilityDefinition ability,
		IReadOnlyList<MonsterActorController> candidates)
	{
		List<MonsterActorController> inRange = new();

		foreach (MonsterActorController monster in candidates)
		{
			if (Targeting.IsValidMonsterTarget(monster)
				&& caster.IsWithinAbilityRange(ability, monster))
			{
				inRange.Add(monster);
			}
		}

		return Targeting.SelectAbilityMonsterTarget(
			caster,
			inRange,
			ability.TargetSelectionStyle);
	}


	private static bool IsBelowAutomaticHealthThreshold(
		HeroActorController hero,
		AbilityDefinition ability)
	{
		if (!TargetingService.IsValidHeroTarget(hero)
			|| hero.Health.MaximumHealth <= 0.0f)
		{
			return false;
		}

		float threshold = Mathf.Clamp(
			ability.AutoCastHealthThresholdPercent / 100.0f,
			0.0f,
			1.0f);

		float healthPercent =
			hero.Health.CurrentHealth
			/ hero.Health.MaximumHealth;

		return healthPercent < threshold;
	}

	/// <summary>
	/// Performs the has monster within taunt radius operation for Active Damage Over Time Effect.
	/// Uses the supplied arguments and current state and returns the resulting bool to the caller.
	/// </summary>
	private bool HasMonsterWithinTauntRadius(
		HeroActorController caster,
		AbilityDefinition ability)
	{
		float radius = Mathf.Max(
			ability.AreaRadius,
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

	/// <summary>
	/// Performs the refresh monster participants operation for Active Damage Over Time Effect.
	/// Uses the current node and service state; any result is applied through side effects, events, or stored fields.
	/// </summary>
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

	/// <summary>
	/// Handles the monster forced target ended event and updates the related game state.
	/// Uses the supplied arguments and current node state; any result is applied through side effects, events, or stored fields.
	/// </summary>
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

	/// <summary>
	/// Handles the monster attack released event and updates the related game state.
	/// Uses the supplied arguments and current node state; any result is applied through side effects, events, or stored fields.
	/// </summary>
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

	/// <summary>
	/// Handles the monster ability released event and updates the related game state.
	/// Uses the supplied arguments and current node state; any result is applied through side effects, events, or stored fields.
	/// </summary>
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

	/// <summary>
	/// Performs the confirm monster ability impact operation for Active Damage Over Time Effect.
	/// Uses the supplied arguments and current node state; any result is applied through side effects, events, or stored fields.
	/// </summary>
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

		if (!TryResolveOffensiveHit(
			attacker,
			target,
			ability.DodgeRule,
			ability,
			out _))
		{
			return;
		}

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

	/// <summary>
	/// Performs the confirm monster impact operation for Active Damage Over Time Effect.
	/// Uses the supplied arguments and current node state; any result is applied through side effects, events, or stored fields.
	/// </summary>
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

		if (!TryResolveOffensiveHit(
			attacker,
			target,
			CombatDodgeRule.DefenderDodge,
			null,
			out _))
		{
			return;
		}

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

	/// <summary>
	/// Resolves one offensive Dodge check through the shared hit service. Basic
	/// attacks always use defender Dodge. Abilities supply their authored Dodge
	/// rule so effects such as Taunt can explicitly bypass Dodge.
	/// </summary>
	private bool TryResolveOffensiveHit(
		Node source,
		Node target,
		CombatDodgeRule dodgeRule,
		AbilityDefinition? ability,
		out CombatHitOutcome outcome)
	{
		outcome = HitResolver.Resolve(
			new CombatHitRequest(
				source,
				target,
				dodgeRule,
				ability));

		if (outcome == CombatHitOutcome.Hit)
		{
			return true;
		}

		string actionName =
			GodotObject.IsInstanceValid(ability)
				? $"'{ability!.DisplayName}'"
				: "basic attack";

		DebugLog.Print(
			$"{source.Name}'s {actionName} against {target.Name} " +
			DescribeFailedHit(outcome) + ".",
			DebugLogCategory.Damage);

		return false;
	}

	private static string DescribeFailedHit(
		CombatHitOutcome outcome)
	{
		return outcome switch
		{
			CombatHitOutcome.Dodge => "was dodged",
			_ => "did not connect"
		};
	}

	/// <summary>
	/// Performs the raise combat event operation for Active Damage Over Time Effect.
	/// Uses the supplied arguments and current node state; any result is applied through side effects, events, or stored fields.
	/// </summary>
	private void RaiseCombatEvent(
	CombatEvent combatEvent)
	{
		CombatEventOccurred?.Invoke(combatEvent);
	}

	/// <summary>
	/// Performs the raise target changed operation for Active Damage Over Time Effect.
	/// Uses the supplied arguments and current node state; any result is applied through side effects, events, or stored fields.
	/// </summary>
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

	/// <summary>
	/// Performs the print damage result operation for Active Damage Over Time Effect.
	/// Uses the supplied arguments and current node state; any result is applied through side effects, events, or stored fields.
	/// </summary>
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

	/// <summary>
	/// Handles the hero incapacitated event and updates the related game state.
	/// Uses the supplied arguments and current node state; any result is applied through side effects, events, or stored fields.
	/// </summary>
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

		hero.SetAutomaticAbilityPriorityResolver(null);

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

	/// <summary>
	/// Performs the resolve combat outcome operation for Active Damage Over Time Effect.
	/// Uses the supplied arguments and current node state; any result is applied through side effects, events, or stored fields.
	/// </summary>
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

	/// <summary>
	/// Applies combat state to the relevant actor, resource, or presentation state.
	/// Uses the current node and service state; any result is applied through side effects, events, or stored fields.
	/// </summary>
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

	/// <summary>
	/// Performs the emit participants changed operation for Active Damage Over Time Effect.
	/// Uses the current node and service state; any result is applied through side effects, events, or stored fields.
	/// </summary>
	private void EmitParticipantsChanged()
	{
		EmitSignal(
			SignalName.ParticipantsChanged,
			HeroParticipantCount,
			MonsterParticipantCount);
	}

	/// <summary>
	/// Performs the validate references operation for Active Damage Over Time Effect.
	/// Reads the current state and returns the resulting bool to the caller.
	/// </summary>
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
		if (!GodotObject.IsInstanceValid(HitResolver))
		{
			GD.PushError(
				"CombatController is missing its " +
				"HitResolver Inspector reference.");

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

	/// <summary>
	/// Cleans up Active Damage Over Time Effect when the node leaves the scene tree.
	/// Uses the current node and service state; any result is applied through side effects, events, or stored fields.
	/// </summary>
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
