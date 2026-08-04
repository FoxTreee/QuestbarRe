using Godot;

public partial class WindowSettingsPanelController : Node
{
    [ExportCategory("Width Controls")]
    [Export]
    public HSlider WidthSlider { get; set; } = null!;

    [Export]
    public SpinBox WidthValue { get; set; } = null!;


    private WindowPlacementSettings? _settings;
    private bool _isSynchronizing;
    private bool _controlsAreValid;

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

    private void OnCollapsedHeightChanged(double value)
    {
        if (_isSynchronizing || _settings is null)
            return;

        _settings.CollapsedHeight = Mathf.RoundToInt(value);
    }

    private void OnExpandedHeightChanged(double value)
    {
        if (_isSynchronizing || _settings is null)
            return;

        _settings.ExpandedHeight = Mathf.RoundToInt(value);
    }

    private void OnHorizontalAdjustmentChanged(double value)
    {
        if (_isSynchronizing || _settings is null)
            return;

        _settings.HorizontalOffset = Mathf.RoundToInt(value);
    }

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

    private void DisconnectSettings()
    {
        if (_settings is null)
            return;

        _settings.Changed -= OnSettingsChanged;
        _settings = null;
    }

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

    private void OnWidthChanged(double value)
    {
        if (_isSynchronizing || _settings is null)
            return;

        _settings.WindowWidth = Mathf.RoundToInt(value);
    }

    [ExportCategory("Collapsed Height Controls")]
    [Export]
    public HSlider CollapsedHeightSlider { get; set; } = null!;

    [Export]
    public SpinBox CollapsedHeightValue { get; set; } = null!;

    [ExportCategory("Expanded Height Controls")]
    [Export]
    public HSlider ExpandedHeightSlider { get; set; } = null!;

    [Export]
    public SpinBox ExpandedHeightValue { get; set; } = null!;

    [ExportCategory("Horizontal Adjustment Controls")]
    [Export]
    public HSlider HorizontalAdjustmentSlider { get; set; } = null!;

    [Export]
    public SpinBox HorizontalAdjustmentValue { get; set; } = null!;

    private void OnSettingsChanged()
    {
        RefreshControls();
    }

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