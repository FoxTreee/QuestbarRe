using Godot;

[GlobalClass]
public partial class WindowPlacementSettings : Resource
{
    public enum PhysicalScreenAnchor
    {
        Left,
        Right
    }

    private int _selectedMonitor;
    private PhysicalScreenAnchor _screenAnchor =
        PhysicalScreenAnchor.Right;
    private int _windowWidth = 800;
    private int _horizontalOffset;
    private int _bottomOffset;
    private int _collapsedHeight = 48;
    private int _expandedHeight = 144;
    private bool _startExpanded;

    [ExportCategory("Monitor")]
    [Export(PropertyHint.Range, "0,8,1")]
    public int SelectedMonitor
    {
        get => _selectedMonitor;
        set => SetValue(ref _selectedMonitor, value);
    }

    [ExportCategory("Placement")]
    [Export]
    public PhysicalScreenAnchor ScreenAnchor
    {
        get => _screenAnchor;
        set => SetValue(ref _screenAnchor, value);
    }

    [Export(PropertyHint.Range, "1,7680,1")]
    public int WindowWidth
    {
        get => _windowWidth;
        set => SetValue(ref _windowWidth, value);
    }

    [Export(PropertyHint.Range, "0,7680,1")]
    public int HorizontalOffset
    {
        get => _horizontalOffset;
        set => SetValue(ref _horizontalOffset, value);
    }

    [Export(PropertyHint.Range, "0,4320,1")]
    public int BottomOffset
    {
        get => _bottomOffset;
        set => SetValue(ref _bottomOffset, value);
    }

    [ExportCategory("Height")]
    [Export(PropertyHint.Range, "1,2160,1")]
    public int CollapsedHeight
    {
        get => _collapsedHeight;
        set => SetValue(ref _collapsedHeight, value);
    }

    [Export(PropertyHint.Range, "1,4320,1")]
    public int ExpandedHeight
    {
        get => _expandedHeight;
        set => SetValue(ref _expandedHeight, value);
    }

    [Export]
    public bool StartExpanded
    {
        get => _startExpanded;
        set => SetValue(ref _startExpanded, value);
    }

    private void SetValue<T>(ref T field, T value)
    {
        if (Equals(field, value))
            return;

        field = value;
        EmitChanged();
    }
}