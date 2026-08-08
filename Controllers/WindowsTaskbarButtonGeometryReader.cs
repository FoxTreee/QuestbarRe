using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using Godot;

public readonly record struct WindowsTaskbarButtonGeometry(
	Rect2I Bounds,
	string Name,
	int AccessibilityRole);

public readonly record struct WindowsTaskbarButtonSnapshot(
	WindowsTaskbarButtonGeometry[] Buttons,
	int ScreenIndex,
	Rect2I TaskbarBounds,
	int ScannedElementCount,
	bool WasTraversalCapped);

public static class WindowsTaskbarButtonGeometryReader
{
	private const string TaskbarWindowClass = "Shell_TrayWnd";
	private const uint ObjectIdClient = 0xFFFFFFFC;
	private const int MaximumTraversalDepth = 16;
	private const int MaximumScannedElements = 512;
	private const int MaximumDiagnosticRoles = 8;
	private const int MaximumDiagnosticSamples = 8;
	private const int MaximumRejectionSamples = 4;

	private const int RoleSystemMenuItem = 0x0C;
	private const int RoleSystemLink = 0x1E;
	private const int RoleSystemListItem = 0x22;
	private const int RoleSystemPushButton = 0x2B;
	private const int RoleSystemCheckButton = 0x2C;
	private const int RoleSystemRadioButton = 0x2D;
	private const int RoleSystemButtonDropDown = 0x38;
	private const int RoleSystemButtonMenu = 0x39;
	private const int RoleSystemButtonDropDownGrid = 0x3A;
	private const int RoleSystemSplitButton = 0x3E;
	private const int RoleSystemOutlineButton = 0x40;

	private const int StateSystemInvisible = 0x00008000;
	private const int StateSystemOffscreen = 0x00010000;

	private static readonly Guid AccessibleInterfaceId =
		new("618736E0-3C3D-11CF-810C-00AA00389B71");

	[ComImport]
	[Guid("618736E0-3C3D-11CF-810C-00AA00389B71")]
	[InterfaceType(ComInterfaceType.InterfaceIsIDispatch)]
	private interface IAccessible
	{
		[DispId(-5001)]
		int accChildCount { get; }

		[DispId(-5003)]
		[return: MarshalAs(UnmanagedType.BStr)]
		string? get_accName(
			[In, MarshalAs(UnmanagedType.Struct)] object childId);

		[DispId(-5006)]
		[return: MarshalAs(UnmanagedType.Struct)]
		object get_accRole(
			[In, MarshalAs(UnmanagedType.Struct)] object childId);

		[DispId(-5007)]
		[return: MarshalAs(UnmanagedType.Struct)]
		object get_accState(
			[In, MarshalAs(UnmanagedType.Struct)] object childId);

		[DispId(-5015)]
		void accLocation(
			out int left,
			out int top,
			out int width,
			out int height,
			[In, MarshalAs(UnmanagedType.Struct)] object childId);
	}

	[DllImport(
		"user32.dll",
		CharSet = CharSet.Unicode,
		ExactSpelling = true)]
	private static extern IntPtr FindWindowW(
		string? className,
		string? windowName);

	[DllImport("oleacc.dll")]
	private static extern int AccessibleObjectFromWindow(
		IntPtr windowHandle,
		uint objectId,
		ref Guid interfaceId,
		[MarshalAs(UnmanagedType.Interface)]
		out IAccessible accessibleObject);

	[DllImport("oleacc.dll")]
	private static extern int AccessibleChildren(
		[MarshalAs(UnmanagedType.Interface)]
		IAccessible container,
		int childStart,
		int childCount,
		[Out] object[] children,
		out int obtainedCount);

	public static bool TryRead(
		WindowsTaskbarGeometry taskbarGeometry,
		WindowsNotificationAreaGeometry? notificationAreaGeometry,
		out WindowsTaskbarButtonSnapshot snapshot,
		out string failureReason)
	{
		snapshot = default;
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

		Guid accessibleInterfaceId = AccessibleInterfaceId;
		int result;
		IAccessible taskbarAccessible;

		try
		{
			result = AccessibleObjectFromWindow(
				taskbarWindow,
				ObjectIdClient,
				ref accessibleInterfaceId,
				out taskbarAccessible);
		}
		catch (Exception exception)
			when (exception is COMException
				|| exception is InvalidCastException
				|| exception is MarshalDirectiveException)
		{
			failureReason =
				$"AccessibleObjectFromWindow could not create the " +
				$"taskbar accessibility object. " +
				$"{exception.GetType().Name}: {exception.Message}";
			return false;
		}

		if (result < 0 || taskbarAccessible is null)
		{
			failureReason =
				$"AccessibleObjectFromWindow failed for " +
				$"{TaskbarWindowClass}. HRESULT=0x{result:X8}.";
			return false;
		}

		TraversalContext context = new(
			taskbarGeometry,
			notificationAreaGeometry);

		try
		{
			TraverseAccessibleTree(
				taskbarAccessible,
				0,
				context);
		}
		catch (Exception exception)
			when (exception is COMException
				|| exception is InvalidCastException
				|| exception is NotImplementedException
				|| exception is MarshalDirectiveException)
		{
			failureReason =
				$"The Windows accessibility tree could not be read. " +
				$"{exception.GetType().Name}: {exception.Message}";
			return false;
		}
		finally
		{
			ReleaseComObject(taskbarAccessible);
		}

		WindowsTaskbarButtonGeometry[] buttons =
			context.GetOrderedButtons();

		if (buttons.Length == 0)
		{
			failureReason =
				$"Windows exposed {context.ScannedElementCount} " +
				$"taskbar accessibility element(s), but none passed " +
				$"the taskbar-button filter. " +
				$"{context.GetFilterDiagnostic()}";
			return false;
		}

		snapshot = new WindowsTaskbarButtonSnapshot(
			buttons,
			taskbarGeometry.ScreenIndex,
			taskbarGeometry.Bounds,
			context.ScannedElementCount,
			context.WasTraversalCapped);

		return true;
	}

	private static void TraverseAccessibleTree(
		IAccessible accessible,
		int depth,
		TraversalContext context)
	{
		if (depth > MaximumTraversalDepth
			|| context.ScannedElementCount
				>= MaximumScannedElements)
		{
			context.WasTraversalCapped = true;
			return;
		}

		context.Inspect(accessible, 0);

		int childCount;

		try
		{
			childCount = accessible.accChildCount;
		}
		catch (COMException)
		{
			return;
		}

		if (childCount <= 0)
			return;

		int remainingCapacity =
			MaximumScannedElements
			- context.ScannedElementCount;

		int requestedCount = Math.Min(
			childCount,
			remainingCapacity);

		if (requestedCount < childCount)
			context.WasTraversalCapped = true;

		if (requestedCount <= 0)
			return;

		object[] children = new object[requestedCount];
		int result = AccessibleChildren(
			accessible,
			0,
			requestedCount,
			children,
			out int obtainedCount);

		if (result < 0)
			return;

		int safeObtainedCount = Math.Min(
			obtainedCount,
			children.Length);

		for (int index = 0;
			index < safeObtainedCount;
			index++)
		{
			if (context.ScannedElementCount
				>= MaximumScannedElements)
			{
				context.WasTraversalCapped = true;
				return;
			}

			object? child = children[index];

			if (TryGetAccessibleObject(
				child,
				out IAccessible childAccessible))
			{
				try
				{
					TraverseAccessibleTree(
						childAccessible,
						depth + 1,
						context);
				}
				finally
				{
					ReleaseComObject(childAccessible);
				}

				continue;
			}

			if (TryConvertToInt32(child, out int childId))
				context.Inspect(accessible, childId);
		}
	}

	private static bool TryGetAccessibleObject(
		object? value,
		out IAccessible accessible)
	{
		accessible = null!;

		if (value is null)
			return false;

		if (value is IAccessible typedAccessible)
		{
			accessible = typedAccessible;
			return true;
		}

		if (!Marshal.IsComObject(value))
			return false;

		try
		{
			accessible = (IAccessible)value;
			return true;
		}
		catch (InvalidCastException)
		{
			return false;
		}
	}

	private static bool TryConvertToInt32(
		object? value,
		out int convertedValue)
	{
		convertedValue = 0;

		if (value is null)
			return false;

		try
		{
			convertedValue = Convert.ToInt32(value);
			return true;
		}
		catch (Exception exception)
			when (exception is FormatException
				|| exception is InvalidCastException
				|| exception is OverflowException)
		{
			return false;
		}
	}

	private static bool IsInteractiveRole(int role)
	{
		return role == RoleSystemMenuItem
			|| role == RoleSystemLink
			|| role == RoleSystemListItem
			|| role == RoleSystemPushButton
			|| role == RoleSystemCheckButton
			|| role == RoleSystemRadioButton
			|| role == RoleSystemButtonDropDown
			|| role == RoleSystemButtonMenu
			|| role == RoleSystemButtonDropDownGrid
			|| role == RoleSystemSplitButton
			|| role == RoleSystemOutlineButton;
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

	private static void ReleaseComObject(object? value)
	{
		if (value is null || !Marshal.IsComObject(value))
			return;

		try
		{
			Marshal.ReleaseComObject(value);
		}
		catch (InvalidComObjectException)
		{
			// The runtime has already released this accessibility object.
		}
	}

	private sealed class TraversalContext
	{
		private readonly WindowsTaskbarGeometry _taskbarGeometry;
		private readonly WindowsNotificationAreaGeometry?
			_notificationAreaGeometry;
		private readonly Dictionary<Rect2I, WindowsTaskbarButtonGeometry>
			_buttonsByBounds = new();
		private readonly Dictionary<int, int>
			_visibleRoleCounts = new();
		private readonly List<string> _visibleSamples = new();
		private readonly List<string> _unreadRoleSamples = new();
		private readonly List<string> _invalidBoundsSamples = new();
		private readonly List<string> _outsideTaskbarSamples = new();
		private readonly Vector2I _nativeToGodotOffset =
			GetNativeToGodotCoordinateOffset();

		public TraversalContext(
			WindowsTaskbarGeometry taskbarGeometry,
			WindowsNotificationAreaGeometry? notificationAreaGeometry)
		{
			_taskbarGeometry = taskbarGeometry;
			_notificationAreaGeometry = notificationAreaGeometry;
		}

		public int ScannedElementCount { get; private set; }
		public bool WasTraversalCapped { get; set; }
		private int RoleUnreadCount { get; set; }
		private int HiddenOrOffscreenCount { get; set; }
		private int InvalidBoundsCount { get; set; }
		private int OutsideTaskbarCount { get; set; }
		private int NotificationAreaCount { get; set; }
		private int NonInteractiveRoleCount { get; set; }

		public void Inspect(
			IAccessible accessible,
			int childId)
		{
			if (ScannedElementCount >= MaximumScannedElements)
			{
				WasTraversalCapped = true;
				return;
			}

			ScannedElementCount++;

			if (!TryReadRole(
				accessible,
				childId,
				out int role))
			{
				RoleUnreadCount++;
				return;
			}

			int state = 0;
			TryReadIntProperty(
				() => accessible.get_accState(childId),
				out state);

			if ((state & StateSystemInvisible) != 0
				|| (state & StateSystemOffscreen) != 0)
			{
				HiddenOrOffscreenCount++;
				return;
			}

			if (!TryReadBounds(
				accessible,
				childId,
				out Rect2I bounds,
				out string boundsFailure))
			{
				InvalidBoundsCount++;

				if (_invalidBoundsSamples.Count
					< MaximumRejectionSamples)
				{
					_invalidBoundsSamples.Add(boundsFailure);
				}

				return;
			}

			if (GetIntersectionArea(
				bounds,
				_taskbarGeometry.Bounds) <= 0)
			{
				OutsideTaskbarCount++;

				if (_outsideTaskbarSamples.Count
					< MaximumRejectionSamples)
				{
					string outsideName = TryReadName(
						accessible,
						childId);

					_outsideTaskbarSamples.Add(
						$"Name=\"{SanitizeForDiagnostic(outsideName)}\", " +
						$"Role={GetAccessibilityRoleName(role)}" +
						$"(0x{role:X2}), " +
						$"Rectangle=(X={bounds.Position.X}, " +
						$"Y={bounds.Position.Y}, W={bounds.Size.X}, " +
						$"H={bounds.Size.Y})");
				}

				return;
			}

			if (_notificationAreaGeometry.HasValue
				&& IsContainedWithin(
					bounds,
					_notificationAreaGeometry.Value.Bounds))
			{
				NotificationAreaCount++;
				return;
			}

			string name = TryReadName(
				accessible,
				childId);

			RecordVisibleTaskbarElement(
				role,
				state,
				bounds,
				name);

			if (!IsInteractiveRole(role))
			{
				NonInteractiveRoleCount++;
				return;
			}

			WindowsTaskbarButtonGeometry candidate = new(
				bounds,
				name,
				role);

			if (!_buttonsByBounds.TryGetValue(
				bounds,
				out WindowsTaskbarButtonGeometry existing)
				|| (string.IsNullOrWhiteSpace(existing.Name)
					&& !string.IsNullOrWhiteSpace(name)))
			{
				_buttonsByBounds[bounds] = candidate;
			}
		}

		public string GetFilterDiagnostic()
		{
			return
				$"FilterResults=(RoleUnread={RoleUnreadCount}, " +
				$"HiddenOrOffscreen={HiddenOrOffscreenCount}, " +
				$"InvalidBounds={InvalidBoundsCount}, " +
				$"OutsideTaskbar={OutsideTaskbarCount}, " +
				$"NotificationArea={NotificationAreaCount}, " +
				$"NonInteractiveRole={NonInteractiveRoleCount}, " +
				$"Accepted={_buttonsByBounds.Count}). " +
				$"VisibleTaskbarRoles={GetVisibleRoleSummary()}. " +
				$"VisibleSamples={GetSampleSummary(_visibleSamples)}. " +
				$"RoleUnreadSamples={GetSampleSummary(_unreadRoleSamples)}. " +
				$"InvalidBoundsSamples=" +
				$"{GetSampleSummary(_invalidBoundsSamples)}. " +
				$"OutsideTaskbarSamples=" +
				$"{GetSampleSummary(_outsideTaskbarSamples)}.";
		}

		private void RecordVisibleTaskbarElement(
			int role,
			int state,
			Rect2I bounds,
			string name)
		{
			_visibleRoleCounts.TryGetValue(
				role,
				out int currentCount);

			_visibleRoleCounts[role] = currentCount + 1;

			if (_visibleSamples.Count >= MaximumDiagnosticSamples)
				return;

			_visibleSamples.Add(
				$"Name=\"{SanitizeForDiagnostic(name)}\", " +
				$"Role={GetAccessibilityRoleName(role)}" +
				$"(0x{role:X2}), State=0x{state:X8}, " +
				$"Rectangle=(X={bounds.Position.X}, " +
				$"Y={bounds.Position.Y}, W={bounds.Size.X}, " +
				$"H={bounds.Size.Y})");
		}

		private string GetVisibleRoleSummary()
		{
			if (_visibleRoleCounts.Count == 0)
				return "[]";

			List<KeyValuePair<int, int>> orderedRoles =
				new(_visibleRoleCounts);

			orderedRoles.Sort(
				(first, second) =>
				{
					int countComparison = second.Value.CompareTo(
						first.Value);

					return countComparison != 0
						? countComparison
						: first.Key.CompareTo(second.Key);
				});

			int includedCount = Math.Min(
				orderedRoles.Count,
				MaximumDiagnosticRoles);

			StringBuilder summary = new("[");

			for (int index = 0; index < includedCount; index++)
			{
				if (index > 0)
					summary.Append(", ");

				KeyValuePair<int, int> role = orderedRoles[index];

				summary.Append(
					$"{GetAccessibilityRoleName(role.Key)}" +
					$"(0x{role.Key:X2})={role.Value}");
			}

			if (orderedRoles.Count > includedCount)
			{
				summary.Append(
					$", +{orderedRoles.Count - includedCount} more");
			}

			summary.Append(']');
			return summary.ToString();
		}

		private static string GetSampleSummary(
			List<string> samples)
		{
			return samples.Count == 0
				? "[]"
				: $"[{string.Join("; ", samples)}]";
		}

		private static string SanitizeForDiagnostic(string value)
		{
			if (string.IsNullOrWhiteSpace(value))
				return "<unnamed>";

			return value
				.Replace("\\", "\\\\", StringComparison.Ordinal)
				.Replace("\"", "\\\"", StringComparison.Ordinal)
				.Replace("\r", " ", StringComparison.Ordinal)
				.Replace("\n", " ", StringComparison.Ordinal)
				.Replace(";", ",", StringComparison.Ordinal);
		}

		private static string GetAccessibilityRoleName(int role)
		{
			return role switch
			{
				0x01 => "TitleBar",
				0x02 => "MenuBar",
				0x09 => "Window",
				0x0A => "Client",
				0x0B => "MenuPopup",
				0x0C => "MenuItem",
				0x0E => "Application",
				0x10 => "Pane",
				0x14 => "Grouping",
				0x15 => "Separator",
				0x16 => "Toolbar",
				0x17 => "StatusBar",
				0x1E => "Link",
				0x21 => "List",
				0x22 => "ListItem",
				0x23 => "Outline",
				0x24 => "OutlineItem",
				0x25 => "PageTab",
				0x28 => "Graphic",
				0x29 => "StaticText",
				0x2A => "Text",
				0x2B => "PushButton",
				0x2C => "CheckButton",
				0x2D => "RadioButton",
				0x38 => "ButtonDropDown",
				0x39 => "ButtonMenu",
				0x3A => "ButtonDropDownGrid",
				0x3E => "SplitButton",
				0x40 => "OutlineButton",
				_ => "Unknown"
			};
		}

		private bool TryReadRole(
			IAccessible accessible,
			int childId,
			out int role)
		{
			role = 0;
			object? rawRole = null;

			try
			{
				rawRole = accessible.get_accRole(childId);

				if (TryConvertToInt32(rawRole, out role))
					return true;
			}
			catch (COMException exception)
			{
				rawRole =
					$"COMException HRESULT=0x{exception.HResult:X8}";
			}

			if (_unreadRoleSamples.Count < MaximumRejectionSamples)
			{
				string typeName = rawRole?.GetType().Name ?? "null";
				string value = SanitizeForDiagnostic(
					rawRole?.ToString() ?? "<null>");

				_unreadRoleSamples.Add(
					$"Type={typeName}, Value=\"{value}\"");
			}

			return false;
		}

		public WindowsTaskbarButtonGeometry[] GetOrderedButtons()
		{
			WindowsTaskbarButtonGeometry[] buttons =
				new WindowsTaskbarButtonGeometry[
					_buttonsByBounds.Count];

			_buttonsByBounds.Values.CopyTo(buttons, 0);

			Array.Sort(
				buttons,
				(first, second) =>
				{
					int xComparison = first.Bounds.Position.X.CompareTo(
						second.Bounds.Position.X);

					return xComparison != 0
						? xComparison
						: first.Bounds.Position.Y.CompareTo(
							second.Bounds.Position.Y);
				});

			return buttons;
		}

		private bool TryReadBounds(
			IAccessible accessible,
			int childId,
			out Rect2I bounds,
			out string failureReason)
		{
			bounds = default;
			failureReason = string.Empty;

			try
			{
				accessible.accLocation(
					out int left,
					out int top,
					out int width,
					out int height,
					childId);

				if (width <= 0 || height <= 0)
				{
					failureReason =
						$"NonPositiveRectangle=(X={left}, Y={top}, " +
						$"W={width}, H={height})";
					return false;
				}

				bounds = new Rect2I(
					new Vector2I(left, top)
						+ _nativeToGodotOffset,
					new Vector2I(width, height));

				return true;
			}
			catch (COMException exception)
			{
				failureReason =
					$"COMException HRESULT=0x{exception.HResult:X8}";
				return false;
			}
		}

		private static bool TryReadIntProperty(
			Func<object> readProperty,
			out int value)
		{
			value = 0;

			try
			{
				return TryConvertToInt32(
					readProperty(),
					out value);
			}
			catch (COMException)
			{
				return false;
			}
		}

		private static string TryReadName(
			IAccessible accessible,
			int childId)
		{
			try
			{
				return accessible.get_accName(childId)
					?? string.Empty;
			}
			catch (COMException)
			{
				return string.Empty;
			}
		}

		private static bool IsContainedWithin(
			Rect2I candidate,
			Rect2I container)
		{
			return candidate.Position.X >= container.Position.X
				&& candidate.Position.Y >= container.Position.Y
				&& candidate.End.X <= container.End.X
				&& candidate.End.Y <= container.End.Y;
		}
	}
}
