using System;
using System.Runtime.InteropServices;
using Godot;

public partial class DesktopWindowHostController : Node
{
    private static readonly IntPtr HwndTopmost = new(-1);

    private const uint SwpNoSize = 0x0001;
    private const uint SwpNoMove = 0x0002;
    private const uint SwpNoActivate = 0x0010;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(
        IntPtr windowHandle,
        IntPtr insertAfter,
        int x,
        int y,
        int width,
        int height,
        uint flags);

    [ExportCategory("Settings")]
    [Export]
    public WindowPlacementSettings PlacementSettings { get; set; }
        = new();

    [ExportCategory("Windows")]
    [Export(PropertyHint.Range, "0.1,5.0,0.1")]
    public double TopmostRefreshIntervalSeconds { get; set; } = 0.25;

    private Window _window = null!;
    private bool _isExpanded;
    private double _topmostRefreshElapsed;

    public override void _Ready()
    {
        _window = GetWindow();

        if (PlacementSettings is null)
        {
            PlacementSettings = new WindowPlacementSettings();

            GD.PushWarning(
                "No WindowPlacementSettings resource was assigned. " +
                "Default settings were created at runtime.");
        }

        WindowSettingsStorage.LoadInto(
            PlacementSettings);

        PlacementSettings.Changed +=
            OnPlacementSettingsChanged;

        _isExpanded =
            PlacementSettings.StartExpanded;

        ConfigureNativeWindow();
        ApplyWindowPlacement();
        EnforceNativeTopmost();

        GD.Print(
            $"Questbar window initialized. " +
            $"Monitor={PlacementSettings.SelectedMonitor}, " +
            $"Anchor={PlacementSettings.ScreenAnchor}, " +
            $"Expanded={_isExpanded}, " +
            $"Position={_window.Position}, " +
            $"Size={_window.Size}");
    }

    private void OnPlacementSettingsChanged()
    {
        if (!IsInsideTree())
            return;

        ApplyWindowPlacement();
        EnforceNativeTopmost();

        GD.Print(
            $"Window placement settings changed. " +
            $"Position={_window.Position}, " +
            $"Size={_window.Size}");
    }

    public override void _ExitTree()
    {
        if (PlacementSettings is not null)
            PlacementSettings.Changed -= OnPlacementSettingsChanged;
    }

    public override void _Process(double delta)
    {
        if (!OS.HasFeature("windows"))
            return;

        _topmostRefreshElapsed += delta;

        if (_topmostRefreshElapsed
            < TopmostRefreshIntervalSeconds)
        {
            return;
        }

        _topmostRefreshElapsed = 0.0;
        EnforceNativeTopmost();
    }

    public override void _UnhandledKeyInput(InputEvent @event)
    {
        if (@event is not InputEventKey keyEvent)
            return;

        if (!keyEvent.Pressed || keyEvent.Echo)
            return;

        if (keyEvent.Keycode == Key.Right)
        {
            PlacementSettings.HorizontalOffset -= 10;
            GetViewport().SetInputAsHandled();
            return;
        }

        if (keyEvent.Keycode == Key.Left)
        {
            PlacementSettings.HorizontalOffset = Mathf.Max(
                PlacementSettings.HorizontalOffset + 10,
                0);

            GetViewport().SetInputAsHandled();
            return;
        }

        if (keyEvent.Keycode == Key.Up)
        {
            PlacementSettings.BottomOffset += 10;
            GetViewport().SetInputAsHandled();
            return;
        }

        if (keyEvent.Keycode == Key.Down)
        {
            PlacementSettings.BottomOffset = Mathf.Max(
                PlacementSettings.BottomOffset - 10,
                0);

            GetViewport().SetInputAsHandled();
            return;
        }

        if (keyEvent.Keycode != Key.Space)
            return;

        ToggleExpanded();
        GetViewport().SetInputAsHandled();
    }

    public void ToggleExpanded()
    {
        _isExpanded = !_isExpanded;

        ApplyWindowPlacement();
        EnforceNativeTopmost();

        GD.Print(
            $"Questbar expanded state changed. " +
            $"Expanded={_isExpanded}, " +
            $"Position={_window.Position}, " +
            $"Size={_window.Size}");
    }

    public void ApplyWindowPlacement()
    {
        int screenCount = DisplayServer.GetScreenCount();

        if (screenCount <= 0)
        {
            GD.PushError(
                "Questbar could not detect any available monitors.");
            return;
        }

        int validMonitor = Mathf.Clamp(
            PlacementSettings.SelectedMonitor,
            0,
            screenCount - 1);

        if (validMonitor
            != PlacementSettings.SelectedMonitor)
        {
            GD.PushWarning(
                $"Selected monitor " +
                $"{PlacementSettings.SelectedMonitor} is invalid. " +
                $"Using monitor {validMonitor} instead.");
        }

        _window.CurrentScreen = validMonitor;

        Vector2I screenPosition =
            DisplayServer.ScreenGetPosition(validMonitor);

        Vector2I screenSize =
            DisplayServer.ScreenGetSize(validMonitor);

        Rect2I screenArea =
            new(screenPosition, screenSize);

        int requestedHeight = GetRequestedHeight();

        int clampedWidth = Mathf.Clamp(
            PlacementSettings.WindowWidth,
            1,
            screenArea.Size.X);

        int clampedHeight = Mathf.Clamp(
            requestedHeight,
            1,
            screenArea.Size.Y);

        Vector2I finalSize =
            new(clampedWidth, clampedHeight);

        int screenLeft = screenArea.Position.X;
        int screenTop = screenArea.Position.Y;

        int screenRight =
            screenArea.Position.X
            + screenArea.Size.X;

        int screenBottom =
            screenArea.Position.Y
            + screenArea.Size.Y;

        int requestedX =
            PlacementSettings.ScreenAnchor switch
            {
                WindowPlacementSettings
                    .PhysicalScreenAnchor.Left =>
                    screenLeft
                    + PlacementSettings.HorizontalOffset,

                WindowPlacementSettings
                    .PhysicalScreenAnchor.Right =>
                    screenRight
                    - finalSize.X
                    - PlacementSettings.HorizontalOffset,

                _ =>
                    screenRight
                    - finalSize.X
                    - PlacementSettings.HorizontalOffset
            };

        int requestedY =
            screenBottom
            - finalSize.Y
            - PlacementSettings.BottomOffset;

        int minimumX = screenLeft;
        int maximumX = screenRight - finalSize.X;

        int minimumY = screenTop;
        int maximumY = screenBottom - finalSize.Y;

        int clampedX = Mathf.Clamp(
            requestedX,
            minimumX,
            maximumX);

        int clampedY = Mathf.Clamp(
            requestedY,
            minimumY,
            maximumY);

        _window.Size = finalSize;
        _window.Position =
            new Vector2I(clampedX, clampedY);
    }

    private void ConfigureNativeWindow()
    {
        _window.Mode = Window.ModeEnum.Windowed;
        _window.Borderless = true;
        _window.AlwaysOnTop = true;
        _window.Unresizable = true;
    }

    private int GetRequestedHeight()
    {
        int collapsedHeight = Mathf.Max(
            PlacementSettings.CollapsedHeight,
            1);

        int expandedHeight = Mathf.Max(
            PlacementSettings.ExpandedHeight,
            collapsedHeight);

        return _isExpanded
            ? expandedHeight
            : collapsedHeight;
    }

    private void EnforceNativeTopmost()
    {
        if (!OS.HasFeature("windows"))
            return;

        long nativeHandle =
            DisplayServer.WindowGetNativeHandle(
                DisplayServer.HandleType.WindowHandle);

        if (nativeHandle == 0)
        {
            GD.PushWarning(
                "Questbar could not retrieve its native Windows handle.");
            return;
        }

        bool succeeded = SetWindowPos(
            new IntPtr(nativeHandle),
            HwndTopmost,
            0,
            0,
            0,
            0,
            SwpNoMove
            | SwpNoSize
            | SwpNoActivate);

        if (succeeded)
            return;

        int errorCode = Marshal.GetLastWin32Error();

        GD.PushWarning(
            $"Native topmost enforcement failed. " +
            $"Windows error code: {errorCode}");
    }
}