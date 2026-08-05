using System.Collections.Generic;
using Godot;

public partial class CombatController : Node
{
    [Signal]
    public delegate void ParticipantsChangedEventHandler(
        int heroCount,
        int monsterCount);

    [ExportCategory("Dependencies")]
    [Export]
    public EncounterController Encounter { get; set; } = null!;

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

    public int HeroParticipantCount =>
        _heroParticipants.Count;

    public int MonsterParticipantCount =>
        _monsterParticipants.Count;

    public bool IsCombatActive { get; private set; }

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

    public override void _ExitTree()
    {
        foreach (HeroActorController hero in _heroParticipants)
        {
            if (!GodotObject.IsInstanceValid(hero))
                continue;

            hero.AttackReleased -=
                OnHeroAttackReleased;
        }

        if (GodotObject.IsInstanceValid(Encounter))
        {
            Encounter.ActiveMonsterCountChanged -=
                OnActiveMonsterCountChanged;
        }
    }

    private void BuildHeroParticipants()
    {
        _heroParticipants.Clear();

        foreach (HeroActorController hero in ConfiguredHeroes)
        {
            if (!GodotObject.IsInstanceValid(hero))
                continue;

            if (_heroParticipants.Contains(hero))
                continue;

            _heroParticipants.Add(hero);

            hero.AttackReleased +=
                OnHeroAttackReleased;
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

        bool establishedInitialAggro =
            target.TryEngage(attacker);

        if (!establishedInitialAggro)
            return;

        GD.Print(
            $"Initial monster aggro established: " +
            $"{target.Name} → {attacker.Name}");
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

    private void RefreshMonsterParticipants()
    {
        _monsterParticipants.Clear();

        foreach (
            MonsterActorController monster
            in Encounter.ActiveMonsters)
        {
            if (!GodotObject.IsInstanceValid(monster))
                continue;

            _monsterParticipants.Add(monster);
        }
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
        if (!GodotObject.IsInstanceValid(Encounter))
        {
            GD.PushError(
                "CombatController is missing its " +
                "Encounter Inspector reference.");

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
}