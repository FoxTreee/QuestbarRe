using Godot;

public partial class BackgroundPresentationController : Node2D
{
	[ExportCategory("Dependencies")]
	/// <summary>
	/// Inspector reference used by this component for its window host dependency.
	/// Assign the matching node or resource from the scene; leaving it empty prevents that connection from working.
	/// </summary>
	[Export]
	public DesktopWindowHostController WindowHost
	{
		get;
		set;
	} = null!;

	/// <summary>
	/// Inspector reference used by this component for its cloud background dependency.
	/// Assign the matching node or resource from the scene; leaving it empty prevents that connection from working.
	/// </summary>
	[Export]
	public Sprite2D CloudBackground
	{
		get;
		set;
	} = null!;

	/// <summary>
	/// Runs Godot setup for Background Presentation Controller when the node enters the scene tree.
	/// Uses the current node and service state; any result is applied through side effects, events, or stored fields.
	/// </summary>
	public override void _Ready()
	{
		if (!GodotObject.IsInstanceValid(WindowHost))
		{
			GD.PushError(
				"BackgroundPresentationController is missing WindowHost.");
			return;
		}

		WindowHost.WindowPlacementApplied +=
			OnWindowPlacementApplied;

		ApplyBottomAnchor();
	}

	/// <summary>
	/// Cleans up Background Presentation Controller when the node leaves the scene tree.
	/// Uses the current node and service state; any result is applied through side effects, events, or stored fields.
	/// </summary>
	public override void _ExitTree()
	{
		if (GodotObject.IsInstanceValid(WindowHost))
		{
			WindowHost.WindowPlacementApplied -=
				OnWindowPlacementApplied;
		}
	}

	/// <summary>
	/// Applies texture to the relevant actor, resource, or presentation state.
	/// Uses the supplied arguments and current node state; any result is applied through side effects, events, or stored fields.
	/// </summary>
	public void ApplyTexture(Texture2D texture)
	{
		if (!GodotObject.IsInstanceValid(texture))
		{
			GD.PushError(
				"BackgroundPresentationController cannot apply a null texture.");
			return;
		}

		CloudBackground.Texture = texture;
		ApplyBottomAnchor();
	}

	/// <summary>
	/// Handles the window placement applied event and updates the related game state.
	/// Uses the current node and service state; any result is applied through side effects, events, or stored fields.
	/// </summary>
	private void OnWindowPlacementApplied()
	{
		ApplyBottomAnchor();
	}

	/// <summary>
	/// Applies bottom anchor to the relevant actor, resource, or presentation state.
	/// Uses the current node and service state; any result is applied through side effects, events, or stored fields.
	/// </summary>
	private void ApplyBottomAnchor()
	{
		int currentHeight = GetWindow().Size.Y;
		int expandedHeight =
			WindowHost.PlacementSettings.ExpandedHeight;

		Position = new Vector2(
			0.0f,
			currentHeight - expandedHeight);

		float cloudHeight =
			CloudBackground.Texture.GetHeight()
			* CloudBackground.Scale.Y;

		CloudBackground.Position = new Vector2(
			CloudBackground.Position.X,
			expandedHeight - cloudHeight);

		DebugLog.Print(
			$"Background anchored. " +
			$"WindowHeight={currentHeight}, " +
			$"BackgroundY={Position.Y}");
	}
}
