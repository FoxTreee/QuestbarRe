using Godot;

public partial class WorldScaleController : Node
{
    [ExportCategory("Dependencies")]

    [Export]
    public Node2D ScalableWorld { get; set; } = null!;

    [Export]
    public Node2D BackgroundLayer { get; set; } = null!;

    [Export]
    public DesktopWindowHostController WindowHost
    {
        get;
        set;
    } = null!;


    private Vector2 _expandedWorldPosition;


    public override void _Ready()
    {
        if (!ValidateReferences())
            return;

        _expandedWorldPosition =
            ScalableWorld.Position;

        WindowHost.ExpandedChanged +=
            OnExpandedChanged;

        ApplyPresentationState(
            WindowHost.IsExpanded);
    }


    public override void _ExitTree()
    {
        if (GodotObject.IsInstanceValid(WindowHost))
        {
            WindowHost.ExpandedChanged -=
                OnExpandedChanged;
        }
    }


    private void OnExpandedChanged(
        bool isExpanded)
    {
        ApplyPresentationState(
            isExpanded);
    }


    private void ApplyPresentationState(
    bool isExpanded)
    {
        float scale =
            isExpanded
                ? 1.0f
                : GetCollapsedScale();

        ScalableWorld.Scale =
            new Vector2(
                scale,
                scale);

        ScalableWorld.Position =
         new Vector2(
        0.0f,
        192.0f * (1.0f - scale));

        DebugLog.Print(
            $"World scale changed. " +
            $"Expanded={isExpanded}, " +
            $"Scale={scale:0.###}, " +
            $"Position={ScalableWorld.Position}");
    }


    private float GetCollapsedScale()
    {
        float expandedHeight =
            Mathf.Max(
                WindowHost.PlacementSettings
                    .ExpandedHeight,
                1);

        float collapsedHeight =
            Mathf.Max(
                WindowHost.PlacementSettings
                    .CollapsedHeight,
                1);

        return Mathf.Clamp(
            collapsedHeight
                / expandedHeight,
            0.01f,
            1.0f);
    }


    private bool ValidateReferences()
    {
        bool valid = true;

        valid &= Require(
            ScalableWorld,
            nameof(ScalableWorld));

        valid &= Require(
            BackgroundLayer,
            nameof(BackgroundLayer));

        valid &= Require(
            WindowHost,
            nameof(WindowHost));

        return valid;
    }


    private static bool Require(
        GodotObject value,
        string propertyName)
    {
        if (GodotObject.IsInstanceValid(value))
            return true;

        GD.PushError(
            $"WorldScaleController is missing " +
            $"'{propertyName}'.");

        return false;
    }
}