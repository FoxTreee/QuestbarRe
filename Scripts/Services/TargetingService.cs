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


    public override void _Ready()
    {
        _random.Randomize();
    }


    public MonsterActorController? SelectPriorityMonster(
        IReadOnlyList<MonsterActorController> candidates)
    {
        MonsterActorController? selectedTarget =
            null;

        foreach (
            MonsterActorController candidate
            in candidates)
        {
            if (!IsValidMonsterTarget(candidate))
                continue;

            if (selectedTarget is null
                || candidate.GlobalPosition.X
                    > selectedTarget.GlobalPosition.X)
            {
                selectedTarget =
                    candidate;
            }
        }

        return selectedTarget;
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
