using Godot;

public partial class MonsterActorController : Node2D
{
    [ExportCategory("Entrance")]
    [Export(PropertyHint.Range, "0,500,1")]
    public float EntrySpeed { get; set; } = 100.0f;

    [Export(PropertyHint.Range, "0.1,20,0.1")]
    public float ArrivalDistance { get; set; } = 1.0f;

    public Vector2 EntryDestination { get; private set; }

    public bool IsEntering { get; private set; }

    public void InitializeEntrance(
        Vector2 spawnPosition,
        Vector2 entryDestination)
    {
        GlobalPosition = spawnPosition;
        EntryDestination = entryDestination;
        IsEntering = true;

        GD.Print(
            $"Monster entrance initialized. " +
            $"Spawn={spawnPosition}, " +
            $"Destination={entryDestination}");
    }

    public override void _Process(double delta)
    {
        if (!IsEntering)
            return;

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
        IsEntering = false;

        GD.Print(
            $"Monster reached encounter position " +
            $"{EntryDestination}.");
    }
}
