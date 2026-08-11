using Godot;

public partial class WorldScaleController : Node
{
    [ExportCategory("Dependencies")]

    /// <summary>
    /// Inspector reference used by this component for its scalable world dependency.
    /// Assign the matching node or resource from the scene; leaving it empty prevents that connection from working.
    /// </summary>
    [Export]
    public Node2D ScalableWorld { get; set; } = null!;

    /// <summary>
    /// Inspector reference used by this component for its background layer dependency.
    /// Assign the matching node or resource from the scene; leaving it empty prevents that connection from working.
    /// </summary>
    [Export]
    public Node2D BackgroundLayer { get; set; } = null!;

    /// <summary>
    /// Inspector reference used by this component for its window host dependency.
    /// Assign the matching node or resource from the scene; leaving it empty prevents that connection from working.
    /// </summary>
    [Export]
    public DesktopWindowHostController WindowHost
    {
        get;
        set;
    } = null!;


    private Vector2 _expandedWorldPosition;


    /// <summary>
    /// Runs Godot setup for World Scale Controller when the node enters the scene tree.
    /// Uses the current node and service state; any result is applied through side effects, events, or stored fields.
    /// </summary>
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


    /// <summary>
    /// Cleans up World Scale Controller when the node leaves the scene tree.
    /// Uses the current node and service state; any result is applied through side effects, events, or stored fields.
    /// </summary>
    public override void _ExitTree()
    {
        if (GodotObject.IsInstanceValid(WindowHost))
        {
            WindowHost.ExpandedChanged -=
                OnExpandedChanged;
        }
    }


    /// <summary>
    /// Handles the expanded changed event and updates the related game state.
    /// Uses the supplied arguments and current node state; any result is applied through side effects, events, or stored fields.
    /// </summary>
    private void OnExpandedChanged(
        bool isExpanded)
    {
        ApplyPresentationState(
            isExpanded);
    }


    /// <summary>
    /// Applies presentation state to the relevant actor, resource, or presentation state.
    /// Uses the supplied arguments and current node state; any result is applied through side effects, events, or stored fields.
    /// </summary>
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


    /// <summary>
    /// Retrieves collapsed scale from the current game state.
    /// Reads the current state and returns the resulting float to the caller.
    /// </summary>
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


    /// <summary>
    /// Performs the validate references operation for World Scale Controller.
    /// Reads the current state and returns the resulting bool to the caller.
    /// </summary>
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


    /// <summary>
    /// Performs the require operation for World Scale Controller.
    /// Uses the supplied arguments and current state and returns the resulting bool to the caller.
    /// </summary>
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