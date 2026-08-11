using Godot;

public partial class WindowSettingsPanelController : Node
{
    [ExportCategory("Width Controls")]
    /// <summary>
    /// Controls width slider, measured as pixels.
    /// For example, selecting a different value changes which width slider behavior or content the owning system uses.
    /// </summary>
    [Export]
    public HSlider WidthSlider { get; set; } = null!;

    /// <summary>
    /// Controls width value, measured as pixels.
    /// For example, selecting a different value changes which width value behavior or content the owning system uses.
    /// </summary>
    [Export]
    public SpinBox WidthValue { get; set; } = null!;


    private WindowPlacementSettings? _settings;
    private bool _isSynchronizing;
    private bool _controlsAreValid;

    /// <summary>
    /// Runs Godot setup for Window Settings Panel Controller when the node enters the scene tree.
    /// Uses the current node and service state; any result is applied through side effects, events, or stored fields.
    /// </summary>
    public override void _Ready()
    {
        _controlsAreValid = ValidateControlReferences();

        if (!_controlsAreValid)
            return;

        WidthSlider.ValueChanged += OnWidthChanged;
        WidthValue.ValueChanged += OnWidthChanged;

        CollapsedHeightSlider.ValueChanged += OnCollapsedHeightChanged;
        CollapsedHeightValue.ValueChanged += OnCollapsedHeightChanged;

        ExpandedHeightSlider.ValueChanged += OnExpandedHeightChanged;
        ExpandedHeightValue.ValueChanged += OnExpandedHeightChanged;

        HorizontalAdjustmentSlider.ValueChanged += OnHorizontalAdjustmentChanged;
        HorizontalAdjustmentValue.ValueChanged += OnHorizontalAdjustmentChanged;
    }

    /// <summary>
    /// Cleans up Window Settings Panel Controller when the node leaves the scene tree.
    /// Uses the current node and service state; any result is applied through side effects, events, or stored fields.
    /// </summary>
    public override void _ExitTree()
    {
        if (GodotObject.IsInstanceValid(CollapsedHeightSlider))
        {
            CollapsedHeightSlider.ValueChanged -=
                OnCollapsedHeightChanged;
        }

        if (GodotObject.IsInstanceValid(CollapsedHeightValue))
        {
            CollapsedHeightValue.ValueChanged -=
                OnCollapsedHeightChanged;
        }

        if (GodotObject.IsInstanceValid(ExpandedHeightSlider))
        {
            ExpandedHeightSlider.ValueChanged -=
                OnExpandedHeightChanged;
        }

        if (GodotObject.IsInstanceValid(ExpandedHeightValue))
        {
            ExpandedHeightValue.ValueChanged -=
                OnExpandedHeightChanged;
        }

        if (GodotObject.IsInstanceValid(HorizontalAdjustmentSlider))
        {
            HorizontalAdjustmentSlider.ValueChanged -=
                OnHorizontalAdjustmentChanged;
        }

        if (GodotObject.IsInstanceValid(HorizontalAdjustmentValue))
        {
            HorizontalAdjustmentValue.ValueChanged -=
                OnHorizontalAdjustmentChanged;
        }
    }

    /// <summary>
    /// Handles the collapsed height changed event and updates the related game state.
    /// Uses the supplied arguments and current node state; any result is applied through side effects, events, or stored fields.
    /// </summary>
    private void OnCollapsedHeightChanged(double value)
    {
        if (_isSynchronizing || _settings is null)
            return;

        _settings.CollapsedHeight = Mathf.RoundToInt(value);
    }

    /// <summary>
    /// Handles the expanded height changed event and updates the related game state.
    /// Uses the supplied arguments and current node state; any result is applied through side effects, events, or stored fields.
    /// </summary>
    private void OnExpandedHeightChanged(double value)
    {
        if (_isSynchronizing || _settings is null)
            return;

        _settings.ExpandedHeight = Mathf.RoundToInt(value);
    }

    /// <summary>
    /// Handles the horizontal adjustment changed event and updates the related game state.
    /// Uses the supplied arguments and current node state; any result is applied through side effects, events, or stored fields.
    /// </summary>
    private void OnHorizontalAdjustmentChanged(double value)
    {
        if (_isSynchronizing || _settings is null)
            return;

        _settings.HorizontalOffset = Mathf.RoundToInt(value);
    }

    /// <summary>
    /// Performs the initialize operation for Window Settings Panel Controller.
    /// Uses the supplied arguments and current node state; any result is applied through side effects, events, or stored fields.
    /// </summary>
    public void Initialize(WindowPlacementSettings settings)
    {
        if (!_controlsAreValid)
        {
            GD.PushError(
                "WindowSettingsPanelController cannot initialize " +
                "because its control references are incomplete.");
            return;
        }

        if (settings is null)
        {
            GD.PushError(
                "WindowSettingsPanelController received null settings.");
            return;
        }

        DisconnectSettings();

        _settings = settings;
        _settings.Changed += OnSettingsChanged;

        RefreshControls();
    }

    /// <summary>
    /// Performs the disconnect settings operation for Window Settings Panel Controller.
    /// Uses the current node and service state; any result is applied through side effects, events, or stored fields.
    /// </summary>
    private void DisconnectSettings()
    {
        if (_settings is null)
            return;

        _settings.Changed -= OnSettingsChanged;
        _settings = null;
    }

    /// <summary>
    /// Performs the validate control references operation for Window Settings Panel Controller.
    /// Reads the current state and returns the resulting bool to the caller.
    /// </summary>
    private bool ValidateControlReferences()
    {
        bool valid = true;

        valid &= Require(WidthSlider, nameof(WidthSlider));
        valid &= Require(WidthValue, nameof(WidthValue));
        valid &= Require(CollapsedHeightSlider, nameof(CollapsedHeightSlider));
        valid &= Require(CollapsedHeightValue, nameof(CollapsedHeightValue));
        valid &= Require(ExpandedHeightSlider, nameof(ExpandedHeightSlider));
        valid &= Require(ExpandedHeightValue, nameof(ExpandedHeightValue));
        valid &= Require(HorizontalAdjustmentSlider, nameof(HorizontalAdjustmentSlider));
        valid &= Require(HorizontalAdjustmentValue, nameof(HorizontalAdjustmentValue));

        return valid;
    }

    /// <summary>
    /// Performs the require operation for Window Settings Panel Controller.
    /// Uses the supplied arguments and current state and returns the resulting bool to the caller.
    /// </summary>
    private static bool Require(
        GodotObject value,
        string propertyName)
    {
        if (GodotObject.IsInstanceValid(value))
            return true;

        GD.PushError(
            $"WindowSettingsPanelController is missing the " +
            $"Inspector reference '{propertyName}'.");

        return false;
    }

    /// <summary>
    /// Handles the width changed event and updates the related game state.
    /// Uses the supplied arguments and current node state; any result is applied through side effects, events, or stored fields.
    /// </summary>
    private void OnWidthChanged(double value)
    {
        if (_isSynchronizing || _settings is null)
            return;

        _settings.WindowWidth = Mathf.RoundToInt(value);
    }

    [ExportCategory("Collapsed Height Controls")]
    /// <summary>
    /// Controls collapsed height slider, measured as pixels.
    /// For example, selecting a different value changes which collapsed height slider behavior or content the owning system uses.
    /// </summary>
    [Export]
    public HSlider CollapsedHeightSlider { get; set; } = null!;

    /// <summary>
    /// Controls collapsed height value, measured as pixels.
    /// For example, selecting a different value changes which collapsed height value behavior or content the owning system uses.
    /// </summary>
    [Export]
    public SpinBox CollapsedHeightValue { get; set; } = null!;

    [ExportCategory("Expanded Height Controls")]
    /// <summary>
    /// Controls expanded height slider, measured as pixels.
    /// For example, selecting a different value changes which expanded height slider behavior or content the owning system uses.
    /// </summary>
    [Export]
    public HSlider ExpandedHeightSlider { get; set; } = null!;

    /// <summary>
    /// Controls expanded height value, measured as pixels.
    /// For example, selecting a different value changes which expanded height value behavior or content the owning system uses.
    /// </summary>
    [Export]
    public SpinBox ExpandedHeightValue { get; set; } = null!;

    [ExportCategory("Horizontal Adjustment Controls")]
    /// <summary>
    /// Controls horizontal adjustment slider.
    /// For example, selecting a different value changes which horizontal adjustment slider behavior or content the owning system uses.
    /// </summary>
    [Export]
    public HSlider HorizontalAdjustmentSlider { get; set; } = null!;

    /// <summary>
    /// Controls horizontal adjustment value.
    /// For example, selecting a different value changes which horizontal adjustment value behavior or content the owning system uses.
    /// </summary>
    [Export]
    public SpinBox HorizontalAdjustmentValue { get; set; } = null!;

    /// <summary>
    /// Handles the settings changed event and updates the related game state.
    /// Uses the current node and service state; any result is applied through side effects, events, or stored fields.
    /// </summary>
    private void OnSettingsChanged()
    {
        RefreshControls();
    }

    /// <summary>
    /// Performs the refresh controls operation for Window Settings Panel Controller.
    /// Uses the current node and service state; any result is applied through side effects, events, or stored fields.
    /// </summary>
    private void RefreshControls()
    {
        if (_settings is null)
            return;

        _isSynchronizing = true;

        WidthSlider.Value = _settings.WindowWidth;
        WidthValue.Value = _settings.WindowWidth;
        CollapsedHeightSlider.Value = _settings.CollapsedHeight;
        CollapsedHeightValue.Value = _settings.CollapsedHeight;
        ExpandedHeightSlider.Value = _settings.ExpandedHeight;
        ExpandedHeightValue.Value = _settings.ExpandedHeight;
        HorizontalAdjustmentSlider.Value = _settings.HorizontalOffset;
        HorizontalAdjustmentValue.Value = _settings.HorizontalOffset;

        _isSynchronizing = false;
    }
}