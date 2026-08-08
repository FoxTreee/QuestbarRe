using Godot;

public partial class WindowSettingsPopupController : Node
{
    [ExportCategory("Dependencies")]
    [Export]
    public DesktopWindowHostController WindowHost { get; set; } = null!;

    [Export]
    public Window SettingsWindow { get; set; } = null!;

    [Export]
    public WindowSettingsPanelController PanelController { get; set; } = null!;

    [ExportCategory("Popup")]
    [Export]
    public Vector2I PopupSize { get; set; } = new(640, 420);

    public override void _Ready()
    {
        if (!ValidateDependencies())
        {
            SetProcessUnhandledKeyInput(false);
            return;
        }

        SettingsWindow.CloseRequested += OnCloseRequested;
        SettingsWindow.WindowInput += OnSettingsWindowInput;
        SettingsWindow.Visible = false;

        PanelController.Initialize(WindowHost.PlacementSettings);
    }

    public override void _ExitTree()
    {
        if (!GodotObject.IsInstanceValid(SettingsWindow))
            return;

        SettingsWindow.CloseRequested -= OnCloseRequested;
        SettingsWindow.WindowInput -= OnSettingsWindowInput;
    }

    public override void _UnhandledKeyInput(InputEvent @event)
    {
        if (!IsSKeyPress(@event))
            return;

        ToggleSettingsWindow();
        GetViewport().SetInputAsHandled();
    }

    public void ToggleSettingsWindow()
    {
        if (SettingsWindow.Visible)
            HideSettingsWindow();
        else
            ShowSettingsWindow();
    }

    public void ShowSettingsWindow()
    {
        SettingsWindow.PopupCentered(PopupSize);
        SettingsWindow.GrabFocus();

        DebugLog.Print("Questbar settings window opened.");
    }

    public void HideSettingsWindow()
    {
        WindowSettingsStorage.Save(
            WindowHost.PlacementSettings);

        SettingsWindow.Hide();

        DebugLog.Print("Questbar settings window hidden.");
    }

    private void OnCloseRequested()
    {
        HideSettingsWindow();
    }

    private void OnSettingsWindowInput(InputEvent @event)
    {
        if (!IsSKeyPress(@event))
            return;

        HideSettingsWindow();
        SettingsWindow.GetViewport().SetInputAsHandled();
    }

    private static bool IsSKeyPress(InputEvent @event)
    {
        return @event is InputEventKey keyEvent
            && keyEvent.Pressed
            && !keyEvent.Echo
            && keyEvent.Keycode == Key.S;
    }

    private bool ValidateDependencies()
    {
        bool valid = true;

        valid &= Require(WindowHost, nameof(WindowHost));
        valid &= Require(SettingsWindow, nameof(SettingsWindow));
        valid &= Require(PanelController, nameof(PanelController));

        return valid;
    }

    private static bool Require(
        GodotObject value,
        string propertyName)
    {
        if (GodotObject.IsInstanceValid(value))
            return true;

        GD.PushError(
            $"WindowSettingsPopupController is missing the " +
            $"Inspector reference '{propertyName}'.");

        return false;
    }
}