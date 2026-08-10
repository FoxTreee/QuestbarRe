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

        if (!IsCombatActive
            || !_heroParticipants.Contains(hero)
            || hero.IsIncapacitated
            || !hero.Health.IsAlive)
        {
            result =
                $"{hero.Name} cannot use an ability outside " +
                "active combat.";

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

        bool abilityApplied = ability.EffectType switch
        {
            AbilityEffectType.AreaTaunt =>
                TryApplyAreaTaunt(
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

            hero.Incapacitated += OnHeroIncapacitated;
        }
    }

    private void OnEncounterStarted()
    {
        CurrentOutcome = CombatOutcome.None;

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
        if (!IsInitialized
            || !IsCombatActive)
        {
            return;
        }

        RefreshMonsterTargets();
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

        RefreshMonsterTargets();
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
