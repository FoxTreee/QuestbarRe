using Godot;
using System.Collections.Generic;

public partial class TargetingService : Node
{
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
                HandleUnsupportedThreatTargeting(
                    monster),

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
        HandleUnsupportedThreatTargeting(
            MonsterActorController monster)
    {
        GD.PushWarning(
            $"{monster.Name} uses HighestThreatHero, " +
            "but monster threat targeting has not " +
            "been implemented.");

        return null;
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