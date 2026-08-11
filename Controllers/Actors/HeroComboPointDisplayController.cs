using Godot;

public partial class HeroComboPointDisplayController : Node2D
{
	private static readonly Color FilledColor = new("f2cf3a");
	private static readonly Color EmptyColor = new("32343a");
	private readonly ColorRect[] _points = new ColorRect[5];
	private HeroComboPointState? _state;

	/// <summary>
	/// Finds the five required square controls and starts the display hidden.
	/// The owning hero binds its combo state after both nodes are ready.
	/// </summary>
	public override void _Ready()
	{
		for (int index = 0; index < _points.Length; index++)
		{
			_points[index] = GetNodeOrNull<ColorRect>(
				$"Point{index + 1}")!;

			if (!GodotObject.IsInstanceValid(_points[index]))
			{
				GD.PushError(
					$"{Name} requires ColorRect children named " +
					"Point1 through Point5.");
			}
		}

		Visible = false;
	}

	/// <summary>
	/// Connects the five-square presentation to a hero's persistent runtime
	/// combo state. Non-rogues stay hidden through the enabled flag.
	/// </summary>
	public void Bind(HeroComboPointState state, bool enabled)
	{
		if (_state is not null)
			_state.Changed -= Refresh;

		_state = state;
		Visible = enabled;

		if (_state is not null)
			_state.Changed += Refresh;

		Refresh();
	}

	/// <summary>
	/// Disconnects the runtime event when the display leaves the scene tree so
	/// a freed hero cannot retain a presentation callback.
	/// </summary>
	public override void _ExitTree()
	{
		if (_state is not null)
			_state.Changed -= Refresh;
	}

	/// <summary>
	/// Colors one square for each accumulated point. Empty squares remain dark
	/// so the player can always see Rook's five-point capacity.
	/// </summary>
	private void Refresh()
	{
		if (!Visible || _state is null)
			return;

		for (int index = 0; index < _points.Length; index++)
		{
			if (GodotObject.IsInstanceValid(_points[index]))
			{
				_points[index].Color =
					index < _state.CurrentPoints
						? FilledColor
						: EmptyColor;
			}
		}
	}
}
