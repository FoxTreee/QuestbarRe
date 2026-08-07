using Godot;

public partial class PresentationFrameController : Node
{
    [ExportCategory("Dependencies")]
    [Export]
    public SubViewportContainer PresentationFrame
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

        WindowHost.ExpandedChanged +=
            OnExpandedChanged;

        WindowHost.PlacementSettings.Changed +=
            OnPlacementSettingsChanged;

        PrepareFrameLayout();
        ApplyFrame();
    }

    public override void _ExitTree()
    {
        if (GodotObject.IsInstanceValid(WindowHost))
        {
            WindowHost.ExpandedChanged -=
                OnExpandedChanged;

            if (WindowHost.PlacementSettings is not null)
            {
                WindowHost.PlacementSettings.Changed -=
                    OnPlacementSettingsChanged;
            }
        }
    }

    private void OnExpandedChanged(
        bool isExpanded)
    {
        ApplyFrame();
    }

    private void OnPlacementSettingsChanged()
    {
        ApplyFrame();
    }

    private void PrepareFrameLayout()
    {
        PresentationFrame.AnchorLeft = 0.0f;
        PresentationFrame.AnchorTop = 0.0f;
        PresentationFrame.AnchorRight = 0.0f;
        PresentationFrame.AnchorBottom = 0.0f;
    }

    private void ApplyFrame()
    {
        int expandedWidth =
            Mathf.Max(
                WindowHost.PlacementSettings.WindowWidth,
                1);

        int expandedHeight =
            Mathf.Max(
                WindowHost.PlacementSettings.ExpandedHeight,
                1);

        int visibleHeight =
            WindowHost.IsExpanded
                ? expandedHeight
                : Mathf.Max(
                    WindowHost.PlacementSettings.CollapsedHeight,
                    1);

        PresentationFrame.Size =
            new Vector2(
                expandedWidth,
                expandedHeight);

        PresentationFrame.Position =
            new Vector2(
                0.0f,
                visibleHeight - expandedHeight);

        GD.Print(
            $"Presentation frame updated. " +
            $"Expanded={WindowHost.IsExpanded}, " +
            $"FrameSize={PresentationFrame.Size}, " +
            $"FramePosition={PresentationFrame.Position}");
    }

    private bool ValidateReferences()
    {
        bool valid = true;

        valid &= Require(
            PresentationFrame,
            nameof(PresentationFrame));

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