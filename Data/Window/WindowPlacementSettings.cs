using Godot;

[GlobalClass]
public partial class WindowPlacementSettings : Resource
{
    public enum PhysicalScreenAnchor
    {
        Left,
        Right
    }

    [ExportCategory("Monitor")]
    [Export(PropertyHint.Range, "0,8,1")]
    public int SelectedMonitor { get; set; } = 0;

    [ExportCategory("Placement")]
    [Export]
    public PhysicalScreenAnchor ScreenAnchor { get; set; }
        = PhysicalScreenAnchor.Right;

    [Export(PropertyHint.Range, "1,7680,1")]
    public int WindowWidth { get; set; } = 800;

    [Export(PropertyHint.Range, "0,7680,1")]
    public int HorizontalOffset { get; set; } = 365;

    [Export(PropertyHint.Range, "0,4320,1")]
    public int BottomOffset { get; set; } = 0;

    [ExportCategory("Height")]
    [Export(PropertyHint.Range, "1,2160,1")]
    public int CollapsedHeight { get; set; } = 60;

    [Export(PropertyHint.Range, "1,4320,1")]
    public int ExpandedHeight { get; set; } = 180;

    [Export]
    public bool StartExpanded { get; set; } = false;
}