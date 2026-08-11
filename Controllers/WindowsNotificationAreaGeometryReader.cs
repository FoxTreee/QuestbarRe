using System;
using System.Runtime.InteropServices;
using System.Text;
using Godot;

public readonly record struct WindowsNotificationAreaGeometry(
	Rect2I Bounds,
	int ScreenIndex,
	Rect2I TaskbarBounds,
	string NativeWindowClass);

public static class WindowsNotificationAreaGeometryReader
{
	private const string TaskbarWindowClass = "Shell_TrayWnd";
	private const string NotificationAreaWindowClass = "TrayNotifyWnd";
	private const int MaximumClassNameLength = 256;

	private delegate bool EnumChildWindowCallback(
		IntPtr windowHandle,
		IntPtr parameter);

	[StructLayout(LayoutKind.Sequential)]
	private struct NativeRectangle
	{
		public int Left;
		public int Top;
		public int Right;
		public int Bottom;
	}

	[DllImport(
		"user32.dll",
		CharSet = CharSet.Unicode,
		ExactSpelling = true)]
	/// <summary>
	/// Performs the find window w operation for Windows Notification Area Geometry Reader.
	/// Uses the supplied arguments and current state and returns the resulting int ptr to the caller.
	/// </summary>
	private static extern IntPtr FindWindowW(
		string? className,
		string? windowName);

	[DllImport("user32.dll")]
	[return: MarshalAs(UnmanagedType.Bool)]
	/// <summary>
	/// Performs the enum child windows operation for Windows Notification Area Geometry Reader.
	/// Uses the supplied arguments and current state and returns the resulting bool to the caller.
	/// </summary>
	private static extern bool EnumChildWindows(
		IntPtr parentWindowHandle,
		EnumChildWindowCallback callback,
		IntPtr parameter);

	[DllImport(
		"user32.dll",
		CharSet = CharSet.Unicode,
		ExactSpelling = true,
		SetLastError = true)]
	/// <summary>
	/// Retrieves class name w from the current game state.
	/// Uses the supplied arguments and current state and returns the resulting int to the caller.
	/// </summary>
	private static extern int GetClassNameW(
		IntPtr windowHandle,
		StringBuilder className,
		int maximumCharacterCount);

	[DllImport("user32.dll", SetLastError = true)]
	[return: MarshalAs(UnmanagedType.Bool)]
	/// <summary>
	/// Retrieves window rect from the current game state.
	/// Uses the supplied arguments and current state and returns the resulting bool to the caller.
	/// </summary>
	private static extern bool GetWindowRect(
		IntPtr windowHandle,
		out NativeRectangle rectangle);

	/// <summary>
	/// Attempts to read without throwing when the operation cannot be completed.
	/// Uses the supplied arguments and current state and returns the resulting bool to the caller.
	/// </summary>
	public static bool TryRead(
		WindowsTaskbarGeometry taskbarGeometry,
		out WindowsNotificationAreaGeometry geometry,
		out string failureReason)
	{
		geometry = default;
		failureReason = string.Empty;

		if (!OperatingSystem.IsWindows())
		{
			failureReason = "The current platform is not Windows.";
			return false;
		}

		IntPtr taskbarWindow = FindWindowW(
			TaskbarWindowClass,
			null);

		if (taskbarWindow == IntPtr.Zero)
		{
			failureReason =
				$"Windows did not expose a {TaskbarWindowClass} window.";
			return false;
		}

		IntPtr notificationAreaWindow =
			FindDescendantWindowByClass(
				taskbarWindow,
				NotificationAreaWindowClass);

		if (notificationAreaWindow == IntPtr.Zero)
		{
			failureReason =
				$"Windows did not expose a " +
				$"{NotificationAreaWindowClass} descendant beneath " +
				$"{TaskbarWindowClass}.";
			return false;
		}

		if (!GetWindowRect(
			notificationAreaWindow,
			out NativeRectangle nativeRectangle))
		{
			failureReason =
				$"GetWindowRect failed for " +
				$"{NotificationAreaWindowClass}. " +
				$"Windows error code: {Marshal.GetLastWin32Error()}.";
			return false;
		}

		int width = nativeRectangle.Right - nativeRectangle.Left;
		int height = nativeRectangle.Bottom - nativeRectangle.Top;

		if (width <= 0 || height <= 0)
		{
			failureReason =
				$"{NotificationAreaWindowClass} returned an invalid " +
				$"rectangle: X={nativeRectangle.Left}, " +
				$"Y={nativeRectangle.Top}, W={width}, H={height}.";
			return false;
		}

		Vector2I nativeToGodotOffset =
			GetNativeToGodotCoordinateOffset();

		Rect2I notificationAreaBounds = new(
			new Vector2I(
				nativeRectangle.Left,
				nativeRectangle.Top)
				+ nativeToGodotOffset,
			new Vector2I(width, height));

		if (GetIntersectionArea(
			notificationAreaBounds,
			taskbarGeometry.Bounds) <= 0)
		{
			failureReason =
				$"{NotificationAreaWindowClass} bounds " +
				$"{notificationAreaBounds} do not overlap the detected " +
				$"taskbar bounds {taskbarGeometry.Bounds}.";
			return false;
		}

		geometry = new WindowsNotificationAreaGeometry(
			notificationAreaBounds,
			taskbarGeometry.ScreenIndex,
			taskbarGeometry.Bounds,
			NotificationAreaWindowClass);

		return true;
	}

	/// <summary>
	/// Performs the find descendant window by class operation for Windows Notification Area Geometry Reader.
	/// Uses the supplied arguments and current state and returns the resulting int ptr to the caller.
	/// </summary>
	private static IntPtr FindDescendantWindowByClass(
		IntPtr parentWindow,
		string desiredClassName)
	{
		IntPtr matchingWindow = IntPtr.Zero;

		EnumChildWindowCallback callback =
			(windowHandle, _) =>
			{
				StringBuilder className = new(
					MaximumClassNameLength);

				int copiedCharacterCount = GetClassNameW(
					windowHandle,
					className,
					className.Capacity);

				if (copiedCharacterCount <= 0
					|| !string.Equals(
						className.ToString(),
						desiredClassName,
						StringComparison.Ordinal))
				{
					return true;
				}

				matchingWindow = windowHandle;
				return false;
			};

		EnumChildWindows(
			parentWindow,
			callback,
			IntPtr.Zero);

		return matchingWindow;
	}

	/// <summary>
	/// Retrieves native to godot coordinate offset from the current game state.
	/// Reads the current state and returns the resulting vector2 i to the caller.
	/// </summary>
	private static Vector2I
		GetNativeToGodotCoordinateOffset()
	{
		int primaryScreen = DisplayServer.GetPrimaryScreen();

		if (primaryScreen < 0
			|| primaryScreen >= DisplayServer.GetScreenCount())
		{
			return Vector2I.Zero;
		}

		return DisplayServer.ScreenGetPosition(primaryScreen);
	}

	/// <summary>
	/// Retrieves intersection area from the current game state.
	/// Uses the supplied arguments and current state and returns the resulting long to the caller.
	/// </summary>
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
}
