using Godot;

public partial class WindowSettingsPopupController : Node
{
    [ExportCategory("Dependencies")]
    /// <summary>
    /// Inspector reference used by this component for its window host dependency.
    /// Assign the matching node or resource from the scene; leaving it empty prevents that connection from working.
    /// </summary>
    [Export]
    public DesktopWindowHostController WindowHost { get; set; } = null!;

    /// <summary>
    /// Inspector reference used by this component for its settings window dependency.
    /// Assign the matching node or resource from the scene; leaving it empty prevents that connection from working.
    /// </summary>
    [Export]
    public Window SettingsWindow { get; set; } = null!;

    /// <summary>
    /// Inspector reference used by this component for its panel controller dependency.
    /// Assign the matching node or resource from the scene; leaving it empty prevents that connection from working.
    /// </summary>
    [Export]
    public WindowSettingsPanelController PanelController { get; set; } = null!;

    [ExportCategory("Popup")]
    /// <summary>
    /// Controls popup size.
    /// For example, changing 640 to 1280 doubles this setting's configured contribution to the system.
    /// </summary>
    [Export]
    public Vector2I PopupSize { get; set; } = new(640, 420);

    /// <summary>
    /// Runs Godot setup for Window Settings Popup Controller when the node enters the scene tree.
    /// Uses the current node and service state; any result is applied through side effects, events, or stored fields.
    /// </summary>
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

    /// <summary>
    /// Cleans up Window Settings Popup Controller when the node leaves the scene tree.
    /// Uses the current node and service state; any result is applied through side effects, events, or stored fields.
    /// </summary>
    public override void _ExitTree()
    {
        if (!GodotObject.IsInstanceValid(SettingsWindow))
            return;

        SettingsWindow.CloseRequested -= OnCloseRequested;
        SettingsWindow.WindowInput -= OnSettingsWindowInput;
    }

    /// <summary>
    /// Performs the unhandled key input operation for Window Settings Popup Controller.
    /// Uses the supplied arguments and current node state; any result is applied through side effects, events, or stored fields.
    /// </summary>
    public override void _UnhandledKeyInput(InputEvent @event)
    {
        if (!IsSKeyPress(@event))
            return;

        ToggleSettingsWindow();
        GetViewport().SetInputAsHandled();
    }

    /// <summary>
    /// Performs the toggle settings window operation for Window Settings Popup Controller.
    /// Uses the current node and service state; any result is applied through side effects, events, or stored fields.
    /// </summary>
    public void ToggleSettingsWindow()
    {
        if (SettingsWindow.Visible)
            HideSettingsWindow();
        else
            ShowSettingsWindow();
    }

    /// <summary>
    /// Performs the show settings window operation for Window Settings Popup Controller.
    /// Uses the current node and service state; any result is applied through side effects, events, or stored fields.
    /// </summary>
    public void ShowSettingsWindow()
    {
        SettingsWindow.PopupCentered(PopupSize);
        SettingsWindow.GrabFocus();

        DebugLog.Print("Questbar settings window opened.");
    }

    /// <summary>
    /// Performs the hide settings window operation for Window Settings Popup Controller.
    /// Uses the current node and service state; any result is applied through side effects, events, or stored fields.
    /// </summary>
    public void HideSettingsWindow()
    {
        WindowSettingsStorage.Save(
            WindowHost.PlacementSettings);

        SettingsWindow.Hide();

        DebugLog.Print("Questbar settings window hidden.");
    }

    /// <summary>
    /// Handles the close requested event and updates the related game state.
    /// Uses the current node and service state; any result is applied through side effects, events, or stored fields.
    /// </summary>
    private void OnCloseRequested()
    {
        HideSettingsWindow();
    }

    /// <summary>
    /// Handles the settings window input event and updates the related game state.
    /// Uses the supplied arguments and current node state; any result is applied through side effects, events, or stored fields.
    /// </summary>
    private void OnSettingsWindowInput(InputEvent @event)
    {
        if (!IsSKeyPress(@event))
            return;

        HideSettingsWindow();
        SettingsWindow.GetViewport().SetInputAsHandled();
    }

    /// <summary>
    /// Performs the is s key press operation for Window Settings Popup Controller.
    /// Uses the supplied arguments and current state and returns the resulting bool to the caller.
    /// </summary>
    private static bool IsSKeyPress(InputEvent @event)
    {
        return @event is InputEventKey keyEvent
            && keyEvent.Pressed
            && !keyEvent.Echo
            && keyEvent.Keycode == Key.S;
    }

    /// <summary>
    /// Performs the validate dependencies operation for Window Settings Popup Controller.
    /// Reads the current state and returns the resulting bool to the caller.
    /// </summary>
    private bool ValidateDependencies()
    {
        bool valid = true;

        valid &= Require(WindowHost, nameof(WindowHost));
        valid &= Require(SettingsWindow, nameof(SettingsWindow));
        valid &= Require(PanelController, nameof(PanelController));

        return valid;
    }

    /// <summary>
    /// Performs the require operation for Window Settings Popup Controller.
    /// Uses the supplied arguments and current state and returns the resulting bool to the caller.
    /// </summary>
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