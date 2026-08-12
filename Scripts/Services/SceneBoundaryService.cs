using Godot;

public partial class SceneBoundaryService : Node
{
    [ExportCategory("Horizontal Boundaries")]
    [Export] public Marker2D SceneLeftBoundary { get; set; } = null!;
    [Export] public Marker2D SceneRightBoundary { get; set; } = null!;

    [ExportCategory("Ground Boundaries")]
    [Export] public Marker2D GroundTopBoundary { get; set; } = null!;
    [Export] public Marker2D GroundBottomBoundary { get; set; } = null!;

    public float LeftX => Mathf.Min(SceneLeftBoundary.GlobalPosition.X, SceneRightBoundary.GlobalPosition.X);
    public float RightX => Mathf.Max(SceneLeftBoundary.GlobalPosition.X, SceneRightBoundary.GlobalPosition.X);
    public float TopY => Mathf.Min(GroundTopBoundary.GlobalPosition.Y, GroundBottomBoundary.GlobalPosition.Y);
    public float BottomY => Mathf.Max(GroundTopBoundary.GlobalPosition.Y, GroundBottomBoundary.GlobalPosition.Y);

    public override void _Ready()
    {
        if (!ValidateReferences())
            SetProcess(false);
    }

    public Vector2 ClampToScene(Vector2 worldPosition)
    {
        if (!ValidateReferences(false))
            return worldPosition;

        return new Vector2(
            Mathf.Clamp(worldPosition.X, LeftX, RightX),
            Mathf.Clamp(worldPosition.Y, TopY, BottomY));
    }

    public bool Contains(Vector2 worldPosition)
    {
        if (!ValidateReferences(false))
            return true;

        return worldPosition.X >= LeftX
            && worldPosition.X <= RightX
            && worldPosition.Y >= TopY
            && worldPosition.Y <= BottomY;
    }

    private bool ValidateReferences(bool logErrors = true)
    {
        bool valid = true;
        valid &= Require(SceneLeftBoundary, nameof(SceneLeftBoundary), logErrors);
        valid &= Require(SceneRightBoundary, nameof(SceneRightBoundary), logErrors);
        valid &= Require(GroundTopBoundary, nameof(GroundTopBoundary), logErrors);
        valid &= Require(GroundBottomBoundary, nameof(GroundBottomBoundary), logErrors);
        return valid;
    }

    private static bool Require(GodotObject value, string propertyName, bool logErrors)
    {
        if (GodotObject.IsInstanceValid(value))
            return true;

        if (logErrors)
            GD.PushError($"SceneBoundaryService is missing '{propertyName}'.");

        return false;
    }
}
