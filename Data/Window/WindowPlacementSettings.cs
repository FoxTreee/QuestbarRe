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
    /// <summary>
    /// Controls selected monitor.
    /// For example, selecting a different value changes which selected monitor behavior or content the owning system uses.
    /// </summary>
    [Export(PropertyHint.Range, "0,8,1")]
    public int SelectedMonitor
    {
        get => _selectedMonitor;
        set => SetValue(ref _selectedMonitor, value);
    }

    [ExportCategory("Placement")]
    /// <summary>
    /// Controls screen anchor.
    /// For example, selecting a different value changes which screen anchor behavior or content the owning system uses.
    /// </summary>
    [Export]
    public PhysicalScreenAnchor ScreenAnchor
    {
        get => _screenAnchor;
        set => SetValue(ref _screenAnchor, value);
    }

    /// <summary>
    /// Controls window width, measured as pixels.
    /// For example, selecting a different value changes which window width behavior or content the owning system uses.
    /// </summary>
    [Export(PropertyHint.Range, "1,7680,1")]
    public int WindowWidth
    {
        get => _windowWidth;
        set => SetValue(ref _windowWidth, value);
    }

    /// <summary>
    /// Controls horizontal offset, measured as pixels.
    /// For example, selecting a different value changes which horizontal offset behavior or content the owning system uses.
    /// </summary>
    [Export(PropertyHint.Range, "0,7680,1")]
    public int HorizontalOffset
    {
        get => _horizontalOffset;
        set => SetValue(ref _horizontalOffset, value);
    }

    /// <summary>
    /// Controls bottom offset, measured as pixels.
    /// For example, selecting a different value changes which bottom offset behavior or content the owning system uses.
    /// </summary>
    [Export(PropertyHint.Range, "0,4320,1")]
    public int BottomOffset
    {
        get => _bottomOffset;
        set => SetValue(ref _bottomOffset, value);
    }

    [ExportCategory("Height")]
    /// <summary>
    /// Controls collapsed height, measured as pixels.
    /// For example, selecting a different value changes which collapsed height behavior or content the owning system uses.
    /// </summary>
    [Export(PropertyHint.Range, "1,2160,1")]
    public int CollapsedHeight
    {
        get => _collapsedHeight;
        set => SetValue(ref _collapsedHeight, value);
    }

    /// <summary>
    /// Controls expanded height, measured as pixels.
    /// For example, selecting a different value changes which expanded height behavior or content the owning system uses.
    /// </summary>
    [Export(PropertyHint.Range, "1,4320,1")]
    public int ExpandedHeight
    {
        get => _expandedHeight;
        set => SetValue(ref _expandedHeight, value);
    }

    /// <summary>
    /// Enables or disables start expanded.
    /// For example, turn this on to enable start expanded, or off to suppress that behavior.
    /// </summary>
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