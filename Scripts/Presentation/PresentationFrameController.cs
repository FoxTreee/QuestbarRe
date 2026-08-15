using Godot;

public partial class PresentationFrameController : Node
{
    private static readonly Vector2I LogicalSize =
        new(800, 192);

    [ExportCategory("Dependencies")]
    /// <summary>
    /// Controls presentation frame.
    /// For example, selecting a different value changes which presentation frame behavior or content the owning system uses.
    /// </summary>
    [Export]
    public SubViewportContainer PresentationFrame
    {
        get;
        set;
    } = null!;

    /// <summary>
    /// Controls region viewport.
    /// For example, selecting a different value changes which region viewport behavior or content the owning system uses.
    /// </summary>
    [Export]
    public SubViewport RegionViewport
    {
        get;
        set;
    } = null!;

    /// <summary>
    /// Controls ground.
    /// For example, selecting a different value changes which ground behavior or content the owning system uses.
    /// </summary>
    [Export]
    public Polygon2D Ground
    {
        get;
        set;
    } = null!;

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

    /// <summary>
    /// Runs Godot setup for Presentation Frame Controller when the node enters the scene tree.
    /// Uses the current node and service state; any result is applied through side effects, events, or stored fields.
    /// </summary>
    public override void _Ready()
    {
        if (!ValidateReferences())
            return;

        ConfigureLogicalViewport();

        WindowHost.WindowPlacementApplied +=
            OnWindowPlacementApplied;

        ApplyPresentationFrame();
    }

    /// <summary>
    /// Cleans up Presentation Frame Controller when the node leaves the scene tree.
    /// Uses the current node and service state; any result is applied through side effects, events, or stored fields.
    /// </summary>
    public override void _ExitTree()
    {
        if (GodotObject.IsInstanceValid(WindowHost))
        {
            WindowHost.WindowPlacementApplied -=
                OnWindowPlacementApplied;
        }
    }

    /// <summary>
    /// Performs the configure logical viewport operation for Presentation Frame Controller.
    /// Uses the current node and service state; any result is applied through side effects, events, or stored fields.
    /// </summary>
    private void ConfigureLogicalViewport()
    {
        PresentationFrame.AnchorLeft = 0.0f;
        PresentationFrame.AnchorTop = 0.0f;
        PresentationFrame.AnchorRight = 0.0f;
        PresentationFrame.AnchorBottom = 0.0f;
        PresentationFrame.Stretch = true;

        // The container already gives the SubViewport its rendered size.
        // Keeping a second logical-size override here created another hidden
        // scaling stage that the editor could not represent honestly.
        RegionViewport.Size2DOverride = Vector2I.Zero;
        RegionViewport.Size2DOverrideStretch = false;
    }

    /// <summary>
    /// Handles the window placement applied event and updates the related game state.
    /// Uses the current node and service state; any result is applied through side effects, events, or stored fields.
    /// </summary>
    private void OnWindowPlacementApplied()
    {
        ApplyPresentationFrame();
    }

    /// <summary>
    /// Applies presentation frame to the relevant actor, resource, or presentation state.
    /// Uses the current node and service state; any result is applied through side effects, events, or stored fields.
    /// </summary>
    private void ApplyPresentationFrame()
    {
        Vector2I windowSize = GetWindow().Size;

        float uniformScale = Mathf.Max(
            windowSize.X / (float)LogicalSize.X,
            0.01f);

        float frameHeight =
            LogicalSize.Y * uniformScale;

        PresentationFrame.Size = new Vector2(
            windowSize.X,
            frameHeight);

        PresentationFrame.Position = new Vector2(
            0.0f,
            windowSize.Y - frameHeight);

        ApplyGroundHeight(uniformScale);

        DebugLog.Print(
            $"Presentation frame applied. " +
            $"LogicalSize={LogicalSize}, " +
            $"WindowSize={windowSize}, " +
            $"UniformScale={uniformScale:0.###}, " +
            $"FramePosition={PresentationFrame.Position}, " +
            $"FrameSize={PresentationFrame.Size}");
    }

    /// <summary>
    /// Applies ground height to the relevant actor, resource, or presentation state.
    /// Uses the supplied arguments and current node state; any result is applied through side effects, events, or stored fields.
    /// </summary>
    private void ApplyGroundHeight(float uniformScale)
    {
        float collapsedHeight = Mathf.Clamp(
            WindowHost.PlacementSettings.CollapsedHeight,
            1,
            GetWindow().Size.Y);

        float logicalGroundHeight = Mathf.Clamp(
            collapsedHeight / uniformScale,
            1.0f,
            LogicalSize.Y);

        float groundTop =
            LogicalSize.Y - logicalGroundHeight;

        Ground.Position = Vector2.Zero;
        Ground.Scale = Vector2.One;
        Ground.Polygon = new Vector2[]
        {
            new(0.0f, groundTop),
            new(LogicalSize.X, groundTop),
            new(LogicalSize.X, LogicalSize.Y),
            new(0.0f, LogicalSize.Y)
        };
    }

    /// <summary>
    /// Performs the validate references operation for Presentation Frame Controller.
    /// Reads the current state and returns the resulting bool to the caller.
    /// </summary>
    private bool ValidateReferences()
    {
        bool valid = true;

        valid &= Require(
            PresentationFrame,
            nameof(PresentationFrame));

        valid &= Require(
            RegionViewport,
            nameof(RegionViewport));

        valid &= Require(
            Ground,
            nameof(Ground));

        valid &= Require(
            WindowHost,
            nameof(WindowHost));

        return valid;
    }

    /// <summary>
    /// Performs the require operation for Presentation Frame Controller.
    /// Uses the supplied arguments and current state and returns the resulting bool to the caller.
    /// </summary>
    private static bool Require(
        GodotObject value,
        string propertyName)
    {
        if (GodotObject.IsInstanceValid(value))
            return true;

        GD.PushError(
            $"PresentationFrameController is missing " +
            $"'{propertyName}'.");

        return false;
    }
}
