using Godot;

public partial class PresentationFrameController : Node
{
    private static readonly Vector2I LogicalSize =
        new(800, 192);

    [ExportCategory("Dependencies")]
    [Export]
    public SubViewportContainer PresentationFrame
    {
        get;
        set;
    } = null!;

    [Export]
    public SubViewport RegionViewport
    {
        get;
        set;
    } = null!;

    [Export]
    public Polygon2D Ground
    {
        get;
        set;
    } = null!;

    [Export]
    public DesktopWindowHostController WindowHost
    {
        get;
        set;
    } = null!;

    public override void _Ready()
    {
        if (!ValidateReferences())
            return;

        ConfigureLogicalViewport();

        WindowHost.WindowPlacementApplied +=
            OnWindowPlacementApplied;

        ApplyPresentationFrame();
    }

    public override void _ExitTree()
    {
        if (GodotObject.IsInstanceValid(WindowHost))
        {
            WindowHost.WindowPlacementApplied -=
                OnWindowPlacementApplied;
        }
    }

    private void ConfigureLogicalViewport()
    {
        PresentationFrame.AnchorLeft = 0.0f;
        PresentationFrame.AnchorTop = 0.0f;
        PresentationFrame.AnchorRight = 0.0f;
        PresentationFrame.AnchorBottom = 0.0f;
        PresentationFrame.Stretch = true;

        RegionViewport.Size2DOverride = LogicalSize;
        RegionViewport.Size2DOverrideStretch = true;
    }

    private void OnWindowPlacementApplied()
    {
        ApplyPresentationFrame();
    }

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

        GD.Print(
            $"Presentation frame applied. " +
            $"LogicalSize={LogicalSize}, " +
            $"WindowSize={windowSize}, " +
            $"UniformScale={uniformScale:0.###}, " +
            $"FramePosition={PresentationFrame.Position}, " +
            $"FrameSize={PresentationFrame.Size}");
    }

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
