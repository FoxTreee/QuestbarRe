using Godot;

public partial class MonsterActorController : Node2D
{
    private enum MonsterState
    {
        Entering,
        WaitingForTarget,
        ApproachingTarget,
        WaitingToAttack,
        Dead
    }

    [ExportCategory("Entrance")]
    [Export(PropertyHint.Range, "0,500,1")]
    public float EntrySpeed { get; set; } = 100.0f;

    [Export(PropertyHint.Range, "0.1,20,0.1")]
    public float ArrivalDistance { get; set; } = 1.0f;

    public Vector2 EntryDestination { get; private set; }

    private MonsterState _state = MonsterState.WaitingForTarget;

    public bool IsEntering => _state == MonsterState.Entering;

    public HeroActorController? CurrentTarget { get; private set; }

    public bool HasTarget =>
        IsValidHeroTarget(CurrentTarget);

    public void InitializeEntrance( Vector2 spawnPosition, Vector2 entryDestination)
    {
        GlobalPosition = spawnPosition;
        EntryDestination = entryDestination;

        CurrentTarget = null;
        _state = MonsterState.Entering;

        GD.Print(
            $"Monster entrance initialized. " +
            $"Spawn={spawnPosition}, " +
            $"Destination={entryDestination}");
    }

    private void UpdateEntrance(double delta)
    {
        float movementDistance =
            EntrySpeed * (float)delta;

        GlobalPosition = GlobalPosition.MoveToward(
            EntryDestination,
            movementDistance);

        if (GlobalPosition.DistanceTo(EntryDestination)
            > ArrivalDistance)
        {
            return;
        }

        GlobalPosition = EntryDestination;
        _state = MonsterState.WaitingForTarget;

        GD.Print(
            $"Monster reached encounter position " +
            $"{EntryDestination}.");
    }

    private static bool IsValidHeroTarget(HeroActorController? hero)
    {
        return hero is not null
            && GodotObject.IsInstanceValid(hero)
            && hero.IsInsideTree();
    }

    public bool TryEngage( HeroActorController attacker)
    {
        if (!IsValidHeroTarget(attacker))
            return false;

        if (HasTarget)
            return false;

        CurrentTarget = attacker;
        _state = MonsterState.ApproachingTarget;

        GD.Print(
            $"{Name} engaged {attacker.Name} " +
            $"and interrupted its entrance.");

        return true;
    }

    public override void _Process(double delta)
    {
        switch (_state)
        {
            case MonsterState.Entering:
                UpdateEntrance(delta);
                break;

            case MonsterState.WaitingForTarget:
                break;

            case MonsterState.ApproachingTarget:
                break;

            case MonsterState.WaitingToAttack:
                break;

            case MonsterState.Dead:
                break;
        }
    }
}
