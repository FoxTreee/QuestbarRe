using Godot;
using System.Collections.Generic;

public partial class TargetingService : Node
{
    public const float MeleeThreatTakeoverMultiplier =
        1.10f;

    public const float DistantThreatTakeoverMultiplier =
        1.30f;

    private readonly RandomNumberGenerator _random =
        new();


    [ExportCategory("Shared Hero Stance Profiles")]

    [Export]
    public HeroCombatStanceProfile PassiveStanceProfile
    { get; set; } = null!;

    [Export]
    public HeroCombatStanceProfile DefensiveStanceProfile
    { get; set; } = null!;

    [Export]
    public HeroCombatStanceProfile AggressiveStanceProfile
    { get; set; } = null!;


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


    public MonsterActorController? SelectPriorityMonster(
        IReadOnlyList<MonsterActorController> candidates)
    {
        return SelectPriorityMonster(
            candidates,
            out _);
    }


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


    public bool TrySelectDefensiveRescueTarget(
        HeroActorController hero,
        IReadOnlyList<MonsterActorController> candidates,
        IReadOnlyList<HeroActorController> partyMembers,
        out HeroTargetDecision decision)
    {
        decision = new HeroTargetDecision
        {
            SelectionRule = "NoDefensiveRescue"
        };

        HeroCombatStanceProfile stanceProfile =
            hero.ActiveStanceProfile;

        if (hero.CombatStance != HeroCombatStance.Defensive
            || !stanceProfile.RescueVulnerableAllies)
        {
            return false;
        }

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

            if (healthPercent
                > stanceProfile.RescueAllyHealthThresholdPercent)
            {
                continue;
            }

            int pressure = CountMonstersTargetingHero(
                partyMember,
                candidates);

            if (pressure
                < stanceProfile.MinimumRescuePressure)
            {
                continue;
            }

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

            if (hero.IsDefensiveRescueActive
                && hero.DefensiveRescueAlly == rescueAlly
                && hero.CurrentTarget == monster)
            {
                currentRescueTarget = monster;
                continue;
            }

            if (availableRescueTarget is null
                || IsBetterDefensiveRescueTarget(
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
            SelectionRule = "DefensiveRescue",
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


    private static bool IsBetterDefensiveRescueTarget(
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


    public bool IsValidMonsterTarget(
        MonsterActorController? monster)
    {
        return monster is not null
            && GodotObject.IsInstanceValid(monster)
            && monster.IsInsideTree()
            && !monster.IsDead;
    }


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
