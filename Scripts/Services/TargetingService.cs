using Godot;
using System.Collections.Generic;

public partial class TargetingService : Node
{
    public const float MeleeThreatTakeoverMultiplier =
        1.10f;

    public const float DistantThreatTakeoverMultiplier =
        1.30f;

    public const float CriticalAllyHealthThresholdPercent =
        25.0f;

    private readonly RandomNumberGenerator _random =
        new();


    [ExportCategory("Shared Hero Stance Profiles")]

    /// <summary>
    /// Controls passive stance profile.
    /// For example, selecting a different value changes which passive stance profile behavior or content the owning system uses.
    /// </summary>
    [Export]
    public HeroCombatStanceProfile PassiveStanceProfile
    { get; set; } = null!;

    /// <summary>
    /// Controls defensive stance profile.
    /// For example, selecting a different value changes which defensive stance profile behavior or content the owning system uses.
    /// </summary>
    [Export]
    public HeroCombatStanceProfile DefensiveStanceProfile
    { get; set; } = null!;

    /// <summary>
    /// Controls aggressive stance profile.
    /// For example, selecting a different value changes which aggressive stance profile behavior or content the owning system uses.
    /// </summary>
    [Export]
    public HeroCombatStanceProfile AggressiveStanceProfile
    { get; set; } = null!;


    /// <summary>
    /// Runs Godot setup for Targeting Service when the node enters the scene tree.
    /// Uses the current node and service state; any result is applied through side effects, events, or stored fields.
    /// </summary>
    public override void _Ready()
    {
        _random.Randomize();

        ValidateSharedStanceProfile(
            PassiveStanceProfile,
            "Passive");

        ValidateSharedStanceProfile(
            DefensiveStanceProfile,
            "Defensive");

        ValidateSharedStanceProfile(
            AggressiveStanceProfile,
            "Aggressive");
    }


    /// <summary>
    /// Retrieves stance profile from the current game state.
    /// Uses the supplied arguments and current state and returns the resulting hero combat stance profile to the caller.
    /// </summary>
    public HeroCombatStanceProfile GetStanceProfile(
        HeroCombatStance stance)
    {
        HeroCombatStanceProfile profile = stance switch
        {
            HeroCombatStance.Passive =>
                PassiveStanceProfile,

            HeroCombatStance.Aggressive =>
                AggressiveStanceProfile,

            _ =>
                DefensiveStanceProfile
        };

        if (!GodotObject.IsInstanceValid(profile))
        {
            throw new System.InvalidOperationException(
                $"The shared {stance} stance profile is missing.");
        }

        return profile;
    }


    /// <summary>
    /// Selects one living ally for an ability using the authored selection
    /// style. TargetMode decides that the ability wants an ally; this method
    /// only decides which eligible ally wins.
    /// </summary>
    public HeroActorController? SelectAbilityAllyTarget(
        HeroActorController caster,
        IReadOnlyList<HeroActorController> candidates,
        AbilityTargetSelectionStyle selectionStyle)
    {
        if (!IsValidHeroTarget(caster))
            return null;

        return selectionStyle switch
        {
            AbilityTargetSelectionStyle.LowestHealth =>
                SelectLowestHealthPercentHero(candidates),

            AbilityTargetSelectionStyle.Nearest =>
                SelectNearestAbilityHero(
                    caster.GlobalPosition,
                    candidates),

            AbilityTargetSelectionStyle.Random =>
                SelectRandomLivingHero(candidates),

            _ => null
        };
    }


    /// <summary>
    /// Selects one living monster for an ability using the same authored
    /// selection styles used by ally-targeted abilities. Effect resolution is
    /// deliberately separate from this choice.
    /// </summary>
    public MonsterActorController? SelectAbilityMonsterTarget(
        HeroActorController caster,
        IReadOnlyList<MonsterActorController> candidates,
        AbilityTargetSelectionStyle selectionStyle)
    {
        if (!IsValidHeroTarget(caster))
            return null;

        MonsterActorController? selected = null;

        if (selectionStyle == AbilityTargetSelectionStyle.Random)
        {
            List<MonsterActorController> validMonsters = new();

            foreach (MonsterActorController monster in candidates)
            {
                if (IsValidMonsterTarget(monster))
                    validMonsters.Add(monster);
            }

            if (validMonsters.Count == 0)
                return null;

            return validMonsters[_random.RandiRange(
                0,
                validMonsters.Count - 1)];
        }

        float selectedValue = float.MaxValue;

        foreach (MonsterActorController monster in candidates)
        {
            if (!IsValidMonsterTarget(monster))
                continue;

            float candidateValue = selectionStyle switch
            {
                AbilityTargetSelectionStyle.LowestHealth =>
                    GetHealthPercent(monster.Health),

                AbilityTargetSelectionStyle.Nearest =>
                    caster.GlobalPosition.DistanceSquaredTo(
                        monster.GlobalPosition),

                _ => float.MaxValue
            };

            if (selected is not null
                && candidateValue >= selectedValue)
            {
                continue;
            }

            selected = monster;
            selectedValue = candidateValue;
        }

        return selected;
    }


    /// <summary>
    /// Selects the living hero with the lowest current health percentage.
    /// Percent is used instead of raw hit points so tanks and fragile heroes
    /// are compared on the same scale.
    /// </summary>
    private static HeroActorController? SelectLowestHealthPercentHero(
        IReadOnlyList<HeroActorController> candidates)
    {
        HeroActorController? selected = null;
        float selectedPercent = float.MaxValue;

        foreach (HeroActorController hero in candidates)
        {
            if (!IsValidHeroTarget(hero))
                continue;

            float healthPercent = GetHealthPercent(hero.Health);

            if (selected is not null
                && healthPercent >= selectedPercent)
            {
                continue;
            }

            selected = hero;
            selectedPercent = healthPercent;
        }

        return selected;
    }


    /// <summary>
    /// Selects the nearest living hero to an arbitrary gameplay position.
    /// </summary>
    private static HeroActorController? SelectNearestAbilityHero(
        Vector2 origin,
        IReadOnlyList<HeroActorController> candidates)
    {
        HeroActorController? selected = null;
        float selectedDistanceSquared = float.MaxValue;

        foreach (HeroActorController hero in candidates)
        {
            if (!IsValidHeroTarget(hero))
                continue;

            float distanceSquared =
                origin.DistanceSquaredTo(hero.GlobalPosition);

            if (selected is not null
                && distanceSquared >= selectedDistanceSquared)
            {
                continue;
            }

            selected = hero;
            selectedDistanceSquared = distanceSquared;
        }

        return selected;
    }


    private static float GetHealthPercent(CombatHealthState health)
    {
        return health.MaximumHealth > 0.0f
            ? health.CurrentHealth / health.MaximumHealth
            : 0.0f;
    }


    /// <summary>
    /// Performs the select priority monster operation for Targeting Service.
    /// Uses the supplied arguments and current state and returns the resulting monster actor controller to the caller.
    /// </summary>
    public MonsterActorController? SelectPriorityMonster(
        IReadOnlyList<MonsterActorController> candidates)
    {
        return SelectPriorityMonster(
            candidates,
            out _);
    }


    /// <summary>
    /// Performs the select priority monster operation for Targeting Service.
    /// Uses the supplied arguments and current state and returns the resulting monster actor controller to the caller.
    /// </summary>
    public MonsterActorController? SelectPriorityMonster(
        IReadOnlyList<MonsterActorController> candidates,
        out HeroTargetDecision decision)
    {
        MonsterActorController? selectedTarget =
            null;

        int validCandidateCount = 0;

        foreach (
            MonsterActorController candidate
            in candidates)
        {
            if (!IsValidMonsterTarget(candidate))
                continue;

            validCandidateCount++;

            if (selectedTarget is null
                || candidate.GlobalPosition.X
                    > selectedTarget.GlobalPosition.X)
            {
                selectedTarget =
                    candidate;
            }
        }

        decision = new HeroTargetDecision
        {
            SelectedTarget = selectedTarget,
            ValidCandidateCount = validCandidateCount,
            SelectionRule = "LegacyRightmost",
            SelectedRuleValue =
                selectedTarget?.GlobalPosition.X ?? 0.0f
        };

        return selectedTarget;
    }


    /// <summary>
    /// Performs the select priority monster operation for Targeting Service.
    /// Uses the supplied arguments and current state and returns the resulting monster actor controller to the caller.
    /// </summary>
    public MonsterActorController? SelectPriorityMonster(
        HeroActorController hero,
        IReadOnlyList<MonsterActorController> candidates,
        IReadOnlyList<HeroActorController> partyMembers,
        out HeroTargetDecision decision)
    {
        HeroCombatStanceProfile stanceProfile =
            hero.ActiveStanceProfile;

        float highestCurrentHealth =
            GetHighestCurrentMonsterHealth(candidates);

        float highestDangerRating =
            GetHighestMonsterDangerRating(candidates);

        HeroTargetScore? selectedScore = null;
        HeroTargetScore? currentTargetScore = null;
        int validCandidateCount = 0;

        foreach (MonsterActorController candidate
            in candidates)
        {
            if (!IsValidMonsterTarget(candidate))
                continue;

            validCandidateCount++;

            HeroTargetScore score = ScoreMonsterForHero(
                hero,
                candidate,
                partyMembers,
                stanceProfile,
                highestCurrentHealth,
                highestDangerRating);

            if (candidate == hero.CurrentTarget)
                currentTargetScore = score;

            if (selectedScore is null
                || IsBetterHeroTargetScore(
                    hero,
                    score,
                    selectedScore))
            {
                selectedScore = score;
            }
        }

        decision = new HeroTargetDecision
        {
            SelectedTarget = selectedScore?.Target,
            ValidCandidateCount = validCandidateCount,
            SelectionRule = $"{hero.CombatStance}StanceScore",
            SelectedRuleValue = selectedScore?.TotalScore ?? 0.0f,
            SelectedScore = selectedScore,
            CurrentTargetScore = currentTargetScore
        };

        return selectedScore?.Target;
    }


    /// <summary>
    /// Attempts to select party support target without throwing when the operation cannot be completed.
    /// Uses the supplied arguments and current state and returns the resulting bool to the caller.
    /// </summary>
    public bool TrySelectPartySupportTarget(
        HeroActorController hero,
        IReadOnlyList<MonsterActorController> candidates,
        IReadOnlyList<HeroActorController> partyMembers,
        out HeroTargetDecision decision)
    {
        decision = new HeroTargetDecision
        {
            SelectionRule = "NoPartySupport"
        };

        HeroCombatStanceProfile stanceProfile =
            hero.ActiveStanceProfile;

        if (stanceProfile.RescueCriticalAllies
            && TrySelectRescueTarget(
                hero,
                candidates,
                partyMembers,
                CriticalAllyHealthThresholdPercent,
                stanceProfile.MinimumRescuePressure,
                "CriticalAllyRescue",
                out decision))
        {
            return true;
        }

        if (hero.CombatStance == HeroCombatStance.Defensive
            && stanceProfile.RescueVulnerableAllies
            && TrySelectRescueTarget(
                hero,
                candidates,
                partyMembers,
                stanceProfile.RescueAllyHealthThresholdPercent,
                stanceProfile.MinimumRescuePressure,
                "VulnerableAllyRescue",
                out decision))
        {
            return true;
        }

        return false;
    }


    /// <summary>
    /// Selects a monster threatening the most urgent ally inside the supplied
    /// health threshold. The caller decides whether this is a critical rescue
    /// or a stance-specific vulnerable rescue.
    /// </summary>
    private bool TrySelectRescueTarget(
        HeroActorController hero,
        IReadOnlyList<MonsterActorController> candidates,
        IReadOnlyList<HeroActorController> partyMembers,
        float healthThresholdPercent,
        int minimumPressure,
        string selectionRule,
        out HeroTargetDecision decision)
    {
        decision = new HeroTargetDecision
        {
            SelectionRule = "NoPartySupport"
        };

        HeroActorController? rescueAlly = null;
        float rescueAllyHealthPercent = 0.0f;
        int rescuePressure = 0;

        foreach (HeroActorController partyMember
            in partyMembers)
        {
            if (partyMember == hero
                || !IsValidHeroTarget(partyMember)
                || partyMember.Health.MaximumHealth <= 0.0f)
            {
                continue;
            }

            float healthPercent =
                partyMember.Health.CurrentHealth
                / partyMember.Health.MaximumHealth
                * 100.0f;

            if (healthPercent > healthThresholdPercent)
                continue;

            int pressure = CountMonstersTargetingHero(
                partyMember,
                candidates);

            if (pressure < minimumPressure)
                continue;

            bool isMoreVulnerable = rescueAlly is null
                || healthPercent < rescueAllyHealthPercent;

            bool tiesHealthWithMorePressure =
                rescueAlly is not null
                && Mathf.IsEqualApprox(
                    healthPercent,
                    rescueAllyHealthPercent)
                && pressure > rescuePressure;

            if (!isMoreVulnerable
                && !tiesHealthWithMorePressure)
            {
                continue;
            }

            rescueAlly = partyMember;
            rescueAllyHealthPercent = healthPercent;
            rescuePressure = pressure;
        }

        if (rescueAlly is null)
            return false;

        MonsterActorController? currentRescueTarget =
            null;

        MonsterActorController? availableRescueTarget =
            null;

        foreach (MonsterActorController monster
            in candidates)
        {
            if (!IsValidMonsterTarget(monster)
                || monster.CurrentTarget != rescueAlly)
            {
                continue;
            }

            int preferredHeroAttackerCount =
                System.Math.Max(
                    monster.Definition
                        .PreferredHeroAttackerCount,
                    1);

            int otherHeroAttackerCount =
                CountOtherHeroAttackers(
                    hero,
                    monster,
                    partyMembers);

            if (otherHeroAttackerCount
                >= preferredHeroAttackerCount)
            {
                continue;
            }

            if (hero.IsPartySupportActive
                && hero.PartySupportAlly == rescueAlly
                && hero.CurrentTarget == monster)
            {
                currentRescueTarget = monster;
                continue;
            }

            if (availableRescueTarget is null
                || IsBetterPartySupportTarget(
                    monster,
                    availableRescueTarget))
            {
                availableRescueTarget = monster;
            }
        }

        MonsterActorController? rescueTarget =
            currentRescueTarget;

        if (rescueTarget is null)
        {
            rescueTarget = availableRescueTarget;
        }
        else if (availableRescueTarget is not null
            && IsGenuinelyMoreDangerousRescueTarget(
                availableRescueTarget,
                rescueTarget))
        {
            rescueTarget = availableRescueTarget;
        }

        if (rescueTarget is null)
            return false;

        int selectedOtherHeroAttackerCount =
            CountOtherHeroAttackers(
                hero,
                rescueTarget,
                partyMembers);

        int selectedPreferredHeroAttackerCount =
            System.Math.Max(
                rescueTarget.Definition
                    .PreferredHeroAttackerCount,
                1);

        decision = new HeroTargetDecision
        {
            SelectedTarget = rescueTarget,
            ValidCandidateCount = rescuePressure,
            SelectionRule = selectionRule,
            SelectedRuleValue =
                rescueTarget.Definition.DangerRating,
            RescueAlly = rescueAlly,
            RescueAllyHealthPercent =
                rescueAllyHealthPercent,
            RescuePressure = rescuePressure,
            OtherHeroAttackerCount =
                selectedOtherHeroAttackerCount,
            PreferredHeroAttackerCount =
                selectedPreferredHeroAttackerCount
        };

        return true;
    }


    /// <summary>
    /// Performs the score monster for hero operation for Targeting Service.
    /// Uses the supplied arguments and current state and returns the resulting hero target score to the caller.
    /// </summary>
    private HeroTargetScore ScoreMonsterForHero(
        HeroActorController hero,
        MonsterActorController monster,
        IReadOnlyList<HeroActorController> partyMembers,
        HeroCombatStanceProfile stanceProfile,
        float highestCurrentHealth,
        float highestDangerRating)
    {
        float healthFactor = highestCurrentHealth > 0.0f
            ? monster.Health.CurrentHealth / highestCurrentHealth
            : 0.0f;

        healthFactor = Mathf.Clamp(
            healthFactor,
            0.0f,
            1.0f);

        float dangerFactor = highestDangerRating > 0.0f
            ? monster.Definition.DangerRating / highestDangerRating
            : 0.0f;

        dangerFactor = Mathf.Clamp(
            dangerFactor,
            0.0f,
            1.0f);

        int otherHeroAttackerCount =
            CountOtherHeroAttackers(
                hero,
                monster,
                partyMembers);

        int preferredHeroAttackerCount =
            System.Math.Max(
                monster.Definition.PreferredHeroAttackerCount,
                1);

        int saturationLevel = System.Math.Max(
            otherHeroAttackerCount
                - preferredHeroAttackerCount
                + 1,
            0);

        float lowestHealthScore =
            (1.0f - healthFactor)
            * stanceProfile.LowestCurrentHealthWeight;

        float highestHealthScore =
            healthFactor
            * stanceProfile.HighestCurrentHealthWeight;

        float dangerScore =
            dangerFactor
            * stanceProfile.MonsterDangerWeight;

        float coverageScore = otherHeroAttackerCount
            < preferredHeroAttackerCount
            ? stanceProfile.UntargetedCoverageBonus
            : 0.0f;

        float healthyAllySupportScore =
            ShouldSupportHealthyAlly(
                hero,
                monster,
                stanceProfile)
                ? stanceProfile.HealthyAllySupportBonus
                : 0.0f;

        float saturationPenalty =
            saturationLevel
            * stanceProfile.SaturationPenaltyPerHero;

        float aggroScore = GetAggroScore(
            hero,
            monster,
            stanceProfile);

        float currentTargetScore = monster == hero.CurrentTarget
            ? stanceProfile.CurrentTargetBonus
            : 0.0f;

        float totalScore =
            lowestHealthScore
            + highestHealthScore
            + dangerScore
            + coverageScore
            + healthyAllySupportScore
            - saturationPenalty
            + aggroScore
            + currentTargetScore;

        return new HeroTargetScore
        {
            Target = monster,
            LowestHealthScore = lowestHealthScore,
            HighestHealthScore = highestHealthScore,
            DangerScore = dangerScore,
            CoverageScore = coverageScore,
            HealthyAllySupportScore = healthyAllySupportScore,
            SaturationPenalty = saturationPenalty,
            AggroScore = aggroScore,
            CurrentTargetScore = currentTargetScore,
            TotalScore = totalScore,
            OtherHeroAttackerCount = otherHeroAttackerCount,
            PreferredHeroAttackerCount =
                preferredHeroAttackerCount
        };
    }


    /// <summary>
    /// Performs the count monsters targeting hero operation for Targeting Service.
    /// Uses the supplied arguments and current state and returns the resulting int to the caller.
    /// </summary>
    private int CountMonstersTargetingHero(
        HeroActorController hero,
        IReadOnlyList<MonsterActorController> candidates)
    {
        int count = 0;

        foreach (MonsterActorController monster
            in candidates)
        {
            if (IsValidMonsterTarget(monster)
                && monster.CurrentTarget == hero)
            {
                count++;
            }
        }

        return count;
    }


    /// <summary>
    /// Performs the is better party support target operation for Targeting Service.
    /// Uses the supplied arguments and current state and returns the resulting bool to the caller.
    /// </summary>
    private static bool IsBetterPartySupportTarget(
        MonsterActorController candidate,
        MonsterActorController selected)
    {
        float candidateDanger =
            candidate.Definition.DangerRating;

        float selectedDanger =
            selected.Definition.DangerRating;

        if (candidateDanger > selectedDanger
            && !Mathf.IsEqualApprox(
                candidateDanger,
                selectedDanger))
        {
            return true;
        }

        if (!Mathf.IsEqualApprox(
            candidateDanger,
            selectedDanger))
        {
            return false;
        }

        if (candidate.Health.CurrentHealth
            > selected.Health.CurrentHealth
            && !Mathf.IsEqualApprox(
                candidate.Health.CurrentHealth,
                selected.Health.CurrentHealth))
        {
            return true;
        }

        if (!Mathf.IsEqualApprox(
            candidate.Health.CurrentHealth,
            selected.Health.CurrentHealth))
        {
            return false;
        }

        return candidate.GlobalPosition.X
            > selected.GlobalPosition.X;
    }


    /// <summary>
    /// Performs the is genuinely more dangerous rescue target operation for Targeting Service.
    /// Uses the supplied arguments and current state and returns the resulting bool to the caller.
    /// </summary>
    private static bool
        IsGenuinelyMoreDangerousRescueTarget(
            MonsterActorController candidate,
            MonsterActorController currentTarget)
    {
        float candidateDanger =
            candidate.Definition.DangerRating;

        float currentDanger =
            currentTarget.Definition.DangerRating;

        return candidateDanger > currentDanger
            && !Mathf.IsEqualApprox(
                candidateDanger,
                currentDanger);
    }


    /// <summary>
    /// Performs the validate shared stance profile operation for Targeting Service.
    /// Uses the supplied arguments and current node state; any result is applied through side effects, events, or stored fields.
    /// </summary>
    private static void ValidateSharedStanceProfile(
        HeroCombatStanceProfile profile,
        string profileName)
    {
        if (!GodotObject.IsInstanceValid(profile))
        {
            GD.PushError(
                $"Shared {profileName} combat stance profile " +
                "is missing.");

            return;
        }

        foreach (string error
            in profile.GetValidationErrors(profileName))
        {
            GD.PushError(error);
        }
    }


    /// <summary>
    /// Performs the is better hero target score operation for Targeting Service.
    /// Uses the supplied arguments and current state and returns the resulting bool to the caller.
    /// </summary>
    private static bool IsBetterHeroTargetScore(
        HeroActorController hero,
        HeroTargetScore candidate,
        HeroTargetScore selected)
    {
        if (candidate.TotalScore > selected.TotalScore
            && !Mathf.IsEqualApprox(
                candidate.TotalScore,
                selected.TotalScore))
        {
            return true;
        }

        if (!Mathf.IsEqualApprox(
            candidate.TotalScore,
            selected.TotalScore))
        {
            return false;
        }

        if (candidate.Target == hero.CurrentTarget
            && selected.Target != hero.CurrentTarget)
        {
            return true;
        }

        if (selected.Target == hero.CurrentTarget)
            return false;

        return candidate.Target.GlobalPosition.X
            > selected.Target.GlobalPosition.X;
    }


    /// <summary>
    /// Performs the count other hero attackers operation for Targeting Service.
    /// Uses the supplied arguments and current state and returns the resulting int to the caller.
    /// </summary>
    private int CountOtherHeroAttackers(
        HeroActorController hero,
        MonsterActorController monster,
        IReadOnlyList<HeroActorController> partyMembers)
    {
        int attackerCount = 0;

        foreach (HeroActorController partyMember
            in partyMembers)
        {
            if (partyMember == hero
                || !IsValidHeroTarget(partyMember)
                || partyMember.CurrentTarget != monster)
            {
                continue;
            }

            attackerCount++;
        }

        return attackerCount;
    }


    /// <summary>
    /// Performs the should support healthy ally operation for Targeting Service.
    /// Uses the supplied arguments and current state and returns the resulting bool to the caller.
    /// </summary>
    private static bool ShouldSupportHealthyAlly(
        HeroActorController hero,
        MonsterActorController monster,
        HeroCombatStanceProfile stanceProfile)
    {
        HeroActorController? target = monster.CurrentTarget;

        if (target == hero
            || !IsValidHeroTarget(target)
            || target!.Health.MaximumHealth <= 0.0f)
        {
            return false;
        }

        float healthPercent =
            target.Health.CurrentHealth
            / target.Health.MaximumHealth
            * 100.0f;

        return healthPercent
            >= stanceProfile.HealthyAllyMinimumHealthPercent;
    }


    /// <summary>
    /// Retrieves aggro score from the current game state.
    /// Uses the supplied arguments and current state and returns the resulting float to the caller.
    /// </summary>
    private static float GetAggroScore(
        HeroActorController hero,
        MonsterActorController monster,
        HeroCombatStanceProfile stanceProfile)
    {
        HeroActorController? target = monster.CurrentTarget;

        if (!IsValidHeroTarget(target))
            return 0.0f;

        if (target == hero)
            return -stanceProfile.AvoidAggroPenalty;

        return stanceProfile.SeekAggroBonus;
    }


    /// <summary>
    /// Retrieves highest current monster health from the current game state.
    /// Uses the supplied arguments and current state and returns the resulting float to the caller.
    /// </summary>
    private float GetHighestCurrentMonsterHealth(
        IReadOnlyList<MonsterActorController> candidates)
    {
        float highestCurrentHealth = 0.0f;

        foreach (MonsterActorController candidate
            in candidates)
        {
            if (!IsValidMonsterTarget(candidate))
                continue;

            highestCurrentHealth = Mathf.Max(
                highestCurrentHealth,
                candidate.Health.CurrentHealth);
        }

        return highestCurrentHealth;
    }


    /// <summary>
    /// Retrieves highest monster danger rating from the current game state.
    /// Uses the supplied arguments and current state and returns the resulting float to the caller.
    /// </summary>
    private float GetHighestMonsterDangerRating(
        IReadOnlyList<MonsterActorController> candidates)
    {
        float highestDangerRating = 0.0f;

        foreach (MonsterActorController candidate
            in candidates)
        {
            if (!IsValidMonsterTarget(candidate))
                continue;

            highestDangerRating = Mathf.Max(
                highestDangerRating,
                candidate.Definition.DangerRating);
        }

        return highestDangerRating;
    }


    /// <summary>
    /// Performs the select hero target operation for Targeting Service.
    /// Uses the supplied arguments and current state and returns the resulting hero actor controller to the caller.
    /// </summary>
    public HeroActorController? SelectHeroTarget(
        MonsterActorController monster,
        IReadOnlyList<HeroActorController> candidates)
    {
        if (!GodotObject.IsInstanceValid(monster)
            || monster.IsDead)
        {
            return null;
        }

        IReadOnlyList<HeroActorController> candidatePool =
            GetPreferredHeroCandidates(
                monster,
                candidates);

        return monster.Definition.TargetingStyle switch
        {
            MonsterTargetingStyle.NearestHero =>
                SelectNearestHero(
                    monster,
                    candidatePool),

            MonsterTargetingStyle.LowestHealthHero =>
                SelectLowestHealthHero(
                    candidatePool),

            MonsterTargetingStyle.RandomLivingHero =>
                SelectRandomLivingHero(
                    candidatePool),

            MonsterTargetingStyle.HighestThreatHero =>
                SelectHighestThreatHero(
                    monster,
                    candidatePool),

            _ =>
                null
        };
    }


    /// <summary>
    /// Retrieves preferred hero candidates from the current game state.
    /// Uses the supplied arguments and current state and returns the resulting i read only list hero actor controller to the caller.
    /// </summary>
    private static IReadOnlyList<HeroActorController>
        GetPreferredHeroCandidates(
            MonsterActorController monster,
            IReadOnlyList<HeroActorController> candidates)
    {
        HeroCombatTag preferredTags =
            monster.Definition.PreferredTargetTags;

        if (preferredTags == HeroCombatTag.None)
            return candidates;

        List<HeroActorController> preferredHeroes =
            new();

        foreach (
            HeroActorController hero
            in candidates)
        {
            if (!IsValidHeroTarget(hero))
                continue;

            if (hero.HasCombatTag(preferredTags))
            {
                preferredHeroes.Add(hero);
            }
        }

        return preferredHeroes.Count > 0
            ? preferredHeroes
            : candidates;
    }


    /// <summary>
    /// Performs the select nearest hero operation for Targeting Service.
    /// Uses the supplied arguments and current state and returns the resulting hero actor controller to the caller.
    /// </summary>
    private static HeroActorController?
        SelectNearestHero(
            MonsterActorController monster,
            IReadOnlyList<HeroActorController> candidates)
    {
        HeroActorController? selectedHero =
            null;

        float shortestDistanceSquared =
            float.MaxValue;

        foreach (
            HeroActorController hero
            in candidates)
        {
            if (!IsValidHeroTarget(hero))
                continue;

            float distanceSquared =
                monster.GlobalPosition.DistanceSquaredTo(
                    hero.GlobalPosition);

            if (selectedHero is not null
                && distanceSquared
                    >= shortestDistanceSquared)
            {
                continue;
            }

            selectedHero =
                hero;

            shortestDistanceSquared =
                distanceSquared;
        }

        return selectedHero;
    }


    /// <summary>
    /// Performs the select lowest health hero operation for Targeting Service.
    /// Uses the supplied arguments and current state and returns the resulting hero actor controller to the caller.
    /// </summary>
    private static HeroActorController?
        SelectLowestHealthHero(
            IReadOnlyList<HeroActorController> candidates)
    {
        HeroActorController? selectedHero =
            null;

        foreach (
            HeroActorController hero
            in candidates)
        {
            if (!IsValidHeroTarget(hero))
                continue;

            if (selectedHero is null
                || hero.Health.CurrentHealth
                    < selectedHero.Health.CurrentHealth)
            {
                selectedHero =
                    hero;
            }
        }

        return selectedHero;
    }


    /// <summary>
    /// Performs the select random living hero operation for Targeting Service.
    /// Uses the supplied arguments and current state and returns the resulting hero actor controller to the caller.
    /// </summary>
    private HeroActorController?
        SelectRandomLivingHero(
            IReadOnlyList<HeroActorController> candidates)
    {
        List<HeroActorController> validHeroes =
            new();

        foreach (
            HeroActorController hero
            in candidates)
        {
            if (IsValidHeroTarget(hero))
            {
                validHeroes.Add(hero);
            }
        }

        if (validHeroes.Count == 0)
            return null;

        int selectedIndex =
            _random.RandiRange(
                0,
                validHeroes.Count - 1);

        return validHeroes[selectedIndex];
    }


    /// <summary>
    /// Performs the select highest threat hero operation for Targeting Service.
    /// Uses the supplied arguments and current state and returns the resulting hero actor controller to the caller.
    /// </summary>
    private static HeroActorController?
        SelectHighestThreatHero(
            MonsterActorController monster,
            IReadOnlyList<HeroActorController> candidates)
    {
        HeroActorController? selectedHero =
            null;

        float highestThreat =
            float.MinValue;

        float shortestDistanceSquared =
            float.MaxValue;

        foreach (
            HeroActorController hero
            in candidates)
        {
            if (!IsValidHeroTarget(hero))
                continue;

            float threat =
                monster.Threat.GetThreat(hero);

            float distanceSquared =
                monster.GlobalPosition.DistanceSquaredTo(
                    hero.GlobalPosition);

            bool hasHigherThreat =
                threat > highestThreat;

            bool tiesThreatAndIsNearer =
                threat == highestThreat
                && distanceSquared < shortestDistanceSquared;

            if (!hasHigherThreat
                && !tiesThreatAndIsNearer)
            {
                continue;
            }

            selectedHero =
                hero;

            highestThreat =
                threat;

            shortestDistanceSquared =
                distanceSquared;
        }

        return selectedHero;
    }


    /// <summary>
    /// Performs the select threat takeover target operation for Targeting Service.
    /// Uses the supplied arguments and current state and returns the resulting hero actor controller to the caller.
    /// </summary>
    public HeroActorController?
        SelectThreatTakeoverTarget(
            MonsterActorController monster,
            IReadOnlyList<HeroActorController> candidates)
    {
        if (!GodotObject.IsInstanceValid(monster)
            || monster.IsDead
            || monster.HasForcedTarget
            || !IsValidHeroTarget(monster.CurrentTarget))
        {
            return null;
        }

        HeroActorController currentTarget =
            monster.CurrentTarget!;

        float currentThreat =
            monster.Threat.GetThreat(currentTarget);

        HeroActorController? selectedChallenger =
            null;

        float selectedThreat =
            float.MinValue;

        float selectedDistanceSquared =
            float.MaxValue;

        HeroCombatTag preferredTags =
            monster.Definition.PreferredTargetTags;

        bool hasLivingPreferredCandidate =
            HasLivingPreferredCandidate(
                preferredTags,
                candidates);

        foreach (
            HeroActorController challenger
            in candidates)
        {
            if (!IsValidHeroTarget(challenger)
                || challenger == currentTarget)
            {
                continue;
            }

            if (hasLivingPreferredCandidate
                && !challenger.HasCombatTag(preferredTags))
            {
                continue;
            }

            float challengerThreat =
                monster.Threat.GetThreat(challenger);

            if (challengerThreat <= currentThreat)
                continue;

            float takeoverMultiplier =
                IsWithinMonsterMeleeRange(
                    monster,
                    challenger)
                    ? MeleeThreatTakeoverMultiplier
                    : DistantThreatTakeoverMultiplier;

            float requiredThreat =
                currentThreat * takeoverMultiplier;

            if (challengerThreat < requiredThreat
                && !Mathf.IsEqualApprox(
                    challengerThreat,
                    requiredThreat))
            {
                continue;
            }

            float distanceSquared =
                monster.GlobalPosition.DistanceSquaredTo(
                    challenger.GlobalPosition);

            bool hasHigherThreat =
                challengerThreat > selectedThreat;

            bool tiesThreatAndIsNearer =
                challengerThreat == selectedThreat
                && distanceSquared < selectedDistanceSquared;

            if (!hasHigherThreat
                && !tiesThreatAndIsNearer)
            {
                continue;
            }

            selectedChallenger = challenger;
            selectedThreat = challengerThreat;
            selectedDistanceSquared = distanceSquared;
        }

        return selectedChallenger;
    }


    /// <summary>
    /// Performs the has living preferred candidate operation for Targeting Service.
    /// Uses the supplied arguments and current state and returns the resulting bool to the caller.
    /// </summary>
    private static bool HasLivingPreferredCandidate(
        HeroCombatTag preferredTags,
        IReadOnlyList<HeroActorController> candidates)
    {
        if (preferredTags == HeroCombatTag.None)
            return false;

        foreach (
            HeroActorController hero
            in candidates)
        {
            if (IsValidHeroTarget(hero)
                && hero.HasCombatTag(preferredTags))
            {
                return true;
            }
        }

        return false;
    }


    /// <summary>
    /// Performs the is within monster melee range operation for Targeting Service.
    /// Uses the supplied arguments and current state and returns the resulting bool to the caller.
    /// </summary>
    public static bool IsWithinMonsterMeleeRange(
        MonsterActorController monster,
        HeroActorController hero)
    {
        if (!GodotObject.IsInstanceValid(monster)
            || !IsValidHeroTarget(hero))
        {
            return false;
        }

        float meleeCenterDistance =
            CombatSpacing.GetRequiredCenterDistance(
                monster.CombatProfile.AttackRange,
                monster.CombatProfile.CombatRadius,
                hero.CombatProfile.CombatRadius,
                monster.CombatProfile.AttackLungeDistance,
                hero.CombatProfile.AttackLungeDistance,
                monster.CombatPresentationScale,
                hero.CombatPresentationScale);

        float distanceSquared =
            monster.GlobalPosition.DistanceSquaredTo(
                hero.GlobalPosition);

        return distanceSquared
            <= meleeCenterDistance * meleeCenterDistance;
    }


    /// <summary>
    /// Performs the is valid monster target operation for Targeting Service.
    /// Uses the supplied arguments and current state and returns the resulting bool to the caller.
    /// </summary>
    public bool IsValidMonsterTarget(
        MonsterActorController? monster)
    {
        return monster is not null
            && GodotObject.IsInstanceValid(monster)
            && monster.IsInsideTree()
            && !monster.IsDead;
    }


    /// <summary>
    /// Performs the is valid hero target operation for Targeting Service.
    /// Uses the supplied arguments and current state and returns the resulting bool to the caller.
    /// </summary>
    public static bool IsValidHeroTarget(
        HeroActorController? hero)
    {
        return hero is not null
            && GodotObject.IsInstanceValid(hero)
            && hero.IsInsideTree()
            && hero.Health.IsAlive
            && !hero.IsIncapacitated;
    }
}
