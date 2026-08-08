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

	[Export(PropertyHint.Range, "0.25,10.0,0.25")]
	public double TaskbarRefreshIntervalSeconds { get; set; } = 1.0;

	[Export(PropertyHint.Range, "0.5,10.0,0.5")]
	public double TaskbarButtonRefreshIntervalSeconds { get; set; } = 2.0;

	private Window _window = null!;
	private bool _isExpanded;
	private double _topmostRefreshElapsed;
	private double _taskbarRefreshElapsed;
	private double _taskbarButtonRefreshElapsed;
	private bool _taskbarReadFailureReported;
	private bool _notificationAreaReadFailureReported;
	private bool _taskbarButtonReadFailureReported;
	public bool IsExpanded => _isExpanded;
	public WindowsTaskbarGeometry? CurrentTaskbarGeometry { get; private set; }
	public WindowsNotificationAreaGeometry? CurrentNotificationAreaGeometry
	{
		get;
		private set;
	}
	public WindowsTaskbarButtonSnapshot? CurrentTaskbarButtonSnapshot
	{
		get;
		private set;
	}
	public event Action<bool>? ExpandedChanged;
	public event Action? WindowPlacementApplied;
	public event Action<WindowsTaskbarGeometry?>? TaskbarGeometryChanged;
	public event Action<WindowsNotificationAreaGeometry?>?
		NotificationAreaGeometryChanged;
	public event Action<WindowsTaskbarButtonSnapshot?>?
		TaskbarButtonSnapshotChanged;

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
		RefreshTaskbarGeometry(forceLog: true);

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
		_taskbarRefreshElapsed += delta;
		_taskbarButtonRefreshElapsed += delta;

		if (_topmostRefreshElapsed
			>= TopmostRefreshIntervalSeconds)
		{
			_topmostRefreshElapsed = 0.0;
			EnforceNativeTopmost();
		}

		if (_taskbarRefreshElapsed
			>= TaskbarRefreshIntervalSeconds)
		{
			_taskbarRefreshElapsed = 0.0;
			RefreshTaskbarGeometry();
		}

		if (_taskbarButtonRefreshElapsed
			>= TaskbarButtonRefreshIntervalSeconds)
		{
			_taskbarButtonRefreshElapsed = 0.0;

			if (CurrentTaskbarGeometry.HasValue)
			{
				RefreshTaskbarButtonGeometry(
					CurrentTaskbarGeometry.Value);
			}
		}
	}

	public bool RefreshTaskbarGeometry(
		bool forceLog = false)
	{
		if (!OS.HasFeature("windows"))
			return false;

		if (!WindowsTaskbarGeometryReader.TryRead(
			out WindowsTaskbarGeometry geometry))
		{
			bool hadGeometry = CurrentTaskbarGeometry.HasValue;
			CurrentTaskbarGeometry = null;
			bool hadNotificationAreaGeometry =
				ClearNotificationAreaGeometry();
			ClearTaskbarButtonSnapshot();

			if (hadGeometry)
				TaskbarGeometryChanged?.Invoke(null);

			if (hadNotificationAreaGeometry)
				ApplyWindowPlacement();

			if (!_taskbarReadFailureReported)
			{
				_taskbarReadFailureReported = true;

				GD.PushWarning(
					"Questbar could not read the Windows " +
					"system taskbar rectangle. Current fixed " +
					"placement remains active.");
			}

			return false;
		}

		_taskbarReadFailureReported = false;

		bool changed =
			!CurrentTaskbarGeometry.HasValue
			|| CurrentTaskbarGeometry.Value != geometry;

		CurrentTaskbarGeometry = geometry;

		if (changed)
			TaskbarGeometryChanged?.Invoke(geometry);

		if (forceLog || changed)
			PrintTaskbarDiagnostic(geometry);

		RefreshNotificationAreaGeometry(
			geometry,
			forceLog);

		if (forceLog)
		{
			_taskbarButtonRefreshElapsed = 0.0;
			RefreshTaskbarButtonGeometry(
				geometry,
				forceLog: true);
		}

		return true;
	}

	private bool RefreshNotificationAreaGeometry(
		WindowsTaskbarGeometry taskbarGeometry,
		bool forceLog)
	{
		if (!WindowsNotificationAreaGeometryReader.TryRead(
			taskbarGeometry,
			out WindowsNotificationAreaGeometry geometry,
			out string failureReason))
		{
			bool hadGeometry =
				ClearNotificationAreaGeometry();

			if (hadGeometry)
				ApplyWindowPlacement();

			if (!_notificationAreaReadFailureReported)
			{
				_notificationAreaReadFailureReported = true;

				GD.PushWarning(
					$"Questbar could not read the Windows " +
					$"notification area rectangle. {failureReason} " +
					$"Current fixed placement remains active.");
			}

			return false;
		}

		_notificationAreaReadFailureReported = false;

		bool changed =
			!CurrentNotificationAreaGeometry.HasValue
			|| CurrentNotificationAreaGeometry.Value != geometry;

		CurrentNotificationAreaGeometry = geometry;

		if (changed)
			NotificationAreaGeometryChanged?.Invoke(geometry);

		if (forceLog || changed)
			PrintNotificationAreaDiagnostic(geometry);

		if (changed
			&& CanUseNotificationAreaPlacement(
				geometry.ScreenIndex))
		{
			ApplyWindowPlacement();
		}

		return true;
	}

	private bool ClearNotificationAreaGeometry()
	{
		if (!CurrentNotificationAreaGeometry.HasValue)
			return false;

		CurrentNotificationAreaGeometry = null;
		NotificationAreaGeometryChanged?.Invoke(null);
		return true;
	}

	private bool RefreshTaskbarButtonGeometry(
		WindowsTaskbarGeometry taskbarGeometry,
		bool forceLog = false)
	{
		if (!WindowsTaskbarButtonGeometryReader.TryRead(
			taskbarGeometry,
			CurrentNotificationAreaGeometry,
			out WindowsTaskbarButtonSnapshot snapshot,
			out string failureReason))
		{
			ClearTaskbarButtonSnapshot();

			if (!_taskbarButtonReadFailureReported)
			{
				_taskbarButtonReadFailureReported = true;

				GD.PushWarning(
					$"Questbar could not read Windows taskbar " +
					$"button rectangles. {failureReason} " +
					$"Notification-area placement remains active; " +
					$"button collision handling is unavailable.");
			}

			return false;
		}

		_taskbarButtonReadFailureReported = false;

		bool changed =
			!CurrentTaskbarButtonSnapshot.HasValue
			|| !AreTaskbarButtonSnapshotsEquivalent(
				CurrentTaskbarButtonSnapshot.Value,
				snapshot);

		CurrentTaskbarButtonSnapshot = snapshot;

		if (changed)
			TaskbarButtonSnapshotChanged?.Invoke(snapshot);

		if (forceLog || changed)
			PrintTaskbarButtonDiagnostic(snapshot);

		return true;
	}

	private bool ClearTaskbarButtonSnapshot()
	{
		if (!CurrentTaskbarButtonSnapshot.HasValue)
			return false;

		CurrentTaskbarButtonSnapshot = null;
		TaskbarButtonSnapshotChanged?.Invoke(null);
		return true;
	}

	private static bool AreTaskbarButtonSnapshotsEquivalent(
		WindowsTaskbarButtonSnapshot first,
		WindowsTaskbarButtonSnapshot second)
	{
		if (first.ScreenIndex != second.ScreenIndex
			|| first.TaskbarBounds != second.TaskbarBounds
			|| first.WasTraversalCapped != second.WasTraversalCapped
			|| first.Buttons.Length != second.Buttons.Length)
		{
			return false;
		}

		for (int index = 0;
			index < first.Buttons.Length;
			index++)
		{
			if (first.Buttons[index] != second.Buttons[index])
				return false;
		}

		return true;
	}

	private void PrintTaskbarButtonDiagnostic(
		WindowsTaskbarButtonSnapshot snapshot)
	{
		bool hasCandidate = TryGetTaskbarQuestbarCandidate(
			snapshot,
			out Rect2I candidateBounds);

		int collisionCount = 0;

		for (int index = 0;
			index < snapshot.Buttons.Length;
			index++)
		{
			if (hasCandidate
				&& GetIntersectionArea(
					candidateBounds,
					snapshot.Buttons[index].Bounds) > 0)
			{
				collisionCount++;
			}
		}

		string candidateDescription = hasCandidate
			? FormatRectangle(candidateBounds)
			: "Unavailable";

		GD.Print(
			$"Windows taskbar buttons detected. " +
			$"Screen={snapshot.ScreenIndex}, " +
			$"ButtonCount={snapshot.Buttons.Length}, " +
			$"ScannedElements={snapshot.ScannedElementCount}, " +
			$"TraversalCapped={snapshot.WasTraversalCapped}, " +
			$"QuestbarCandidate={candidateDescription}, " +
			$"Collision={(hasCandidate && collisionCount > 0)}, " +
			$"CollisionCount={collisionCount}. " +
			$"Diagnostic only; button collisions do not change " +
			$"placement yet.");

		for (int index = 0;
			index < snapshot.Buttons.Length;
			index++)
		{
			WindowsTaskbarButtonGeometry button =
				snapshot.Buttons[index];

			bool collides = hasCandidate
				&& GetIntersectionArea(
					candidateBounds,
					button.Bounds) > 0;

			string roleName = GetAccessibilityRoleName(
				button.AccessibilityRole);

			GD.Print(
				$"Taskbar button detected. " +
				$"Name=\"{SanitizeForLog(button.Name)}\", " +
				$"Role={roleName}, " +
				$"Rectangle={FormatRectangle(button.Bounds)}, " +
				$"CollidesWithQuestbar={collides}");
		}
	}

	private bool TryGetTaskbarQuestbarCandidate(
		WindowsTaskbarButtonSnapshot snapshot,
		out Rect2I candidateBounds)
	{
		candidateBounds = default;

		if (!CanUseNotificationAreaPlacement(
			snapshot.ScreenIndex))
		{
			return false;
		}

		int windowWidth = Mathf.Clamp(
			PlacementSettings.WindowWidth,
			1,
			CurrentTaskbarGeometry!.Value.ScreenBounds.Size.X);

		int candidateX =
			CurrentNotificationAreaGeometry!.Value.Bounds.Position.X
			- windowWidth;

		candidateBounds = new Rect2I(
			candidateX,
			snapshot.TaskbarBounds.Position.Y,
			windowWidth,
			snapshot.TaskbarBounds.Size.Y);

		return true;
	}

	private static string FormatRectangle(Rect2I rectangle)
	{
		return
			$"(X={rectangle.Position.X}, " +
			$"Y={rectangle.Position.Y}, " +
			$"W={rectangle.Size.X}, " +
			$"H={rectangle.Size.Y})";
	}

	private static string SanitizeForLog(string value)
	{
		if (string.IsNullOrWhiteSpace(value))
			return "<unnamed>";

		return value
			.Replace("\\", "\\\\", StringComparison.Ordinal)
			.Replace("\"", "\\\"", StringComparison.Ordinal)
			.Replace("\r", " ", StringComparison.Ordinal)
			.Replace("\n", " ", StringComparison.Ordinal);
	}

	private static string GetAccessibilityRoleName(int role)
	{
		return role switch
		{
			0x0C => "MenuItem",
			0x1E => "Link",
			0x22 => "ListItem",
			0x2B => "PushButton",
			0x2C => "CheckButton",
			0x2D => "RadioButton",
			0x38 => "ButtonDropDown",
			0x39 => "ButtonMenu",
			0x3A => "ButtonDropDownGrid",
			0x3E => "SplitButton",
			0x40 => "OutlineButton",
			_ => $"Unknown({role})"
		};
	}

	private static long GetIntersectionArea(
		Rect2I first,
		Rect2I second)
	{
		int left = Math.Max(
			first.Position.X,
			second.Position.X);

		int top = Math.Max(
			first.Position.Y,
			second.Position.Y);

		int right = Math.Min(
			first.End.X,
			second.End.X);

		int bottom = Math.Min(
			first.End.Y,
			second.End.Y);

		long width = Math.Max(right - left, 0);
		long height = Math.Max(bottom - top, 0);

		return width * height;
	}

	private static void PrintTaskbarDiagnostic(
		WindowsTaskbarGeometry geometry)
	{
		string screenScale =
			geometry.ScreenIndex >= 0
				? DisplayServer
					.ScreenGetScale(geometry.ScreenIndex)
					.ToString("0.###")
				: "Unmatched";

		GD.Print(
			$"Windows taskbar detected. " +
			$"Screen={geometry.ScreenIndex}, " +
			$"Edge={geometry.Edge}, " +
			$"Position={geometry.Bounds.Position}, " +
			$"Size={geometry.Bounds.Size}, " +
			$"Rectangle=(X={geometry.Bounds.Position.X}, " +
			$"Y={geometry.Bounds.Position.Y}, " +
			$"W={geometry.Bounds.Size.X}, " +
			$"H={geometry.Bounds.Size.Y}), " +
			$"ScreenBounds={geometry.ScreenBounds}, " +
			$"ScreenScale={screenScale}. " +
			$"Legacy placement remains available as the fallback.");
	}

	private static void PrintNotificationAreaDiagnostic(
		WindowsNotificationAreaGeometry geometry)
	{
		GD.Print(
			$"Windows notification area detected. " +
			$"Screen={geometry.ScreenIndex}, " +
			$"Class={geometry.NativeWindowClass}, " +
			$"Position={geometry.Bounds.Position}, " +
			$"Size={geometry.Bounds.Size}, " +
			$"Rectangle=(X={geometry.Bounds.Position.X}, " +
			$"Y={geometry.Bounds.Position.Y}, " +
			$"W={geometry.Bounds.Size.X}, " +
			$"H={geometry.Bounds.Size.Y}), " +
			$"TaskbarBounds={geometry.TaskbarBounds}. " +
			$"Available for adaptive horizontal placement.");
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

		ExpandedChanged?.Invoke(_isExpanded);

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

		bool usesNotificationArea =
			TryGetNotificationAreaPlacementX(
				validMonitor,
				finalSize.X,
				out int notificationAreaX);

		int requestedX = usesNotificationArea
			? notificationAreaX
			: PlacementSettings.ScreenAnchor switch
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

		GD.Print(
			$"Window placement diagnostic: " +
			$"Monitor={validMonitor}, " +
			$"ScreenPosition={screenPosition}, " +
			$"ScreenSize={screenSize}, " +
			$"ScreenScale={DisplayServer.ScreenGetScale(validMonitor):0.###}, " +
			$"ConfiguredWidth={PlacementSettings.WindowWidth}, " +
			$"CollapsedHeight={PlacementSettings.CollapsedHeight}, " +
			$"ExpandedHeight={PlacementSettings.ExpandedHeight}, " +
			$"RequestedHeight={requestedHeight}, " +
			$"FinalSize={finalSize}, " +
			$"HorizontalPlacement=" +
			$"{(usesNotificationArea ? "NotificationArea" : "FixedOffset")}, " +
			$"RequestedPosition=({requestedX}, {requestedY})");

		_window.Size = finalSize;
		_window.Position =
			new Vector2I(clampedX, clampedY);

		WindowPlacementApplied?.Invoke();

		GD.Print(
			$"Native window applied. " +
			$"ActualPosition={_window.Position}, " +
			$"ActualSize={_window.Size}");
	}

	private bool TryGetNotificationAreaPlacementX(
		int monitor,
		int windowWidth,
		out int requestedX)
	{
		requestedX = 0;

		if (!CanUseNotificationAreaPlacement(monitor))
			return false;

		requestedX =
			CurrentNotificationAreaGeometry!.Value.Bounds.Position.X
			- windowWidth;

		return true;
	}

	private bool CanUseNotificationAreaPlacement(
		int monitor)
	{
		if (PlacementSettings.ScreenAnchor
			!= WindowPlacementSettings.PhysicalScreenAnchor.Right)
		{
			return false;
		}

		if (!CurrentTaskbarGeometry.HasValue
			|| !CurrentNotificationAreaGeometry.HasValue)
		{
			return false;
		}

		WindowsTaskbarGeometry taskbar =
			CurrentTaskbarGeometry.Value;

		WindowsNotificationAreaGeometry notificationArea =
			CurrentNotificationAreaGeometry.Value;

		bool horizontalTaskbar =
			taskbar.Edge == WindowsTaskbarEdge.Bottom
			|| taskbar.Edge == WindowsTaskbarEdge.Top;

		return horizontalTaskbar
			&& taskbar.ScreenIndex == monitor
			&& notificationArea.ScreenIndex == monitor;
	}

	private void ConfigureNativeWindow()
	{
		_window.Mode = Window.ModeEnum.Windowed;
		_window.Borderless = true;
		_window.AlwaysOnTop = true;
		_window.Unresizable = true;
		_window.Transparent = true;

		GetViewport().TransparentBg = true;
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
