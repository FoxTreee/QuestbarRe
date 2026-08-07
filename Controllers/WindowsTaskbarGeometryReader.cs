using System;
using System.Runtime.InteropServices;
using Godot;

public enum WindowsTaskbarEdge
{
	Unknown,
	Left,
	Top,
	Right,
	Bottom
}

public readonly record struct WindowsTaskbarGeometry(
	Rect2I Bounds,
	WindowsTaskbarEdge Edge,
	int ScreenIndex,
	Rect2I ScreenBounds);

public static class WindowsTaskbarGeometryReader
{
	private const uint AbmGetTaskbarPosition = 0x00000005;

	[StructLayout(LayoutKind.Sequential)]
	private struct NativeRectangle
	{
		public int Left;
		public int Top;
		public int Right;
		public int Bottom;
	}

	[StructLayout(LayoutKind.Sequential)]
	private struct AppBarData
	{
		public uint Size;
		public IntPtr WindowHandle;
		public uint CallbackMessage;
		public uint Edge;
		public NativeRectangle Rectangle;
		public IntPtr Parameter;
	}

	[DllImport("shell32.dll", ExactSpelling = true)]
	private static extern UIntPtr SHAppBarMessage(
		uint message,
		ref AppBarData data);

	public static bool TryRead(
		out WindowsTaskbarGeometry geometry)
	{
		geometry = default;

		if (!OperatingSystem.IsWindows())
			return false;

		AppBarData data = new()
		{
			Size = (uint)Marshal.SizeOf<AppBarData>()
		};

		UIntPtr result = SHAppBarMessage(
			AbmGetTaskbarPosition,
			ref data);

		int width =
			data.Rectangle.Right
			- data.Rectangle.Left;

		int height =
			data.Rectangle.Bottom
			- data.Rectangle.Top;

		if (result == UIntPtr.Zero
			|| width <= 0
			|| height <= 0)
		{
			return false;
		}

		Rect2I nativeTaskbarBounds = new(
			data.Rectangle.Left,
			data.Rectangle.Top,
			width,
			height);

		// Win32 taskbar coordinates use the Windows primary display as
		// (0, 0). Godot can expose the same desktop with a translated
		// origin, so move the native rectangle into Godot's coordinate
		// space before comparing it with Godot screen rectangles.
		Vector2I nativeToGodotOffset =
			GetNativeToGodotCoordinateOffset();

		Rect2I taskbarBounds = new(
			nativeTaskbarBounds.Position
				+ nativeToGodotOffset,
			nativeTaskbarBounds.Size);

		int screenIndex = FindContainingScreen(
			taskbarBounds,
			out Rect2I screenBounds);

		WindowsTaskbarEdge edge =
			DetectEdge(taskbarBounds, screenBounds);

		geometry = new WindowsTaskbarGeometry(
			taskbarBounds,
			edge,
			screenIndex,
			screenBounds);

		return true;
	}

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

	private static int FindContainingScreen(
		Rect2I taskbarBounds,
		out Rect2I screenBounds)
	{
		int bestScreen = -1;
		long largestIntersectionArea = 0;
		screenBounds = default;

		for (int screen = 0;
			screen < DisplayServer.GetScreenCount();
			screen++)
		{
			Rect2I candidateBounds = new(
				DisplayServer.ScreenGetPosition(screen),
				DisplayServer.ScreenGetSize(screen));

			long intersectionArea = GetIntersectionArea(
				taskbarBounds,
				candidateBounds);

			if (intersectionArea <= largestIntersectionArea)
				continue;

			bestScreen = screen;
			largestIntersectionArea = intersectionArea;
			screenBounds = candidateBounds;
		}

		return bestScreen;
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

	private static WindowsTaskbarEdge DetectEdge(
		Rect2I taskbarBounds,
		Rect2I screenBounds)
	{
		if (screenBounds.Size.X <= 0
			|| screenBounds.Size.Y <= 0)
		{
			return WindowsTaskbarEdge.Unknown;
		}

		if (taskbarBounds.Size.X >= taskbarBounds.Size.Y)
		{
			int distanceFromTop = Math.Abs(
				taskbarBounds.Position.Y
				- screenBounds.Position.Y);

			int distanceFromBottom = Math.Abs(
				taskbarBounds.End.Y
				- screenBounds.End.Y);

			return distanceFromTop <= distanceFromBottom
				? WindowsTaskbarEdge.Top
				: WindowsTaskbarEdge.Bottom;
		}

		int distanceFromLeft = Math.Abs(
			taskbarBounds.Position.X
			- screenBounds.Position.X);

		int distanceFromRight = Math.Abs(
			taskbarBounds.End.X
			- screenBounds.End.X);

		return distanceFromLeft <= distanceFromRight
			? WindowsTaskbarEdge.Left
			: WindowsTaskbarEdge.Right;
	}
}
