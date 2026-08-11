using Godot;

public partial class BackgroundPresentationController : Node2D
{
	[ExportCategory("Dependencies")]
	[Export]
	public DesktopWindowHostController WindowHost
	{
		get;
		set;
	} = null!;

	[Export]
	public Sprite2D CloudBackground
	{
		get;
		set;
	} = null!;

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

	public override void _ExitTree()
	{
		if (GodotObject.IsInstanceValid(WindowHost))
		{
			WindowHost.WindowPlacementApplied -=
				OnWindowPlacementApplied;
		}
	}

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

	private void OnWindowPlacementApplied()
	{
		ApplyBottomAnchor();
	}

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
