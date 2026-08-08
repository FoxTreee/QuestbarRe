using Godot;

public partial class RegionPresentationController : Node2D
{
	[ExportCategory("Background Tiles")]
	[Export]
	public Node2D RegionTileA { get; set; } = null!;

	[Export]
	public Node2D RegionTileB { get; set; } = null!;

	[ExportCategory("Travel")]
	[Export(PropertyHint.Range, "0,500,1")]
	public float TravelSpeed { get; set; } = 60.0f;

	[ExportCategory("Logical Stage")]
	[Export(PropertyHint.Range, "1,7680,1")]
	public float TileWidth { get; set; } = 800.0f;
	
	[ExportCategory("Dependencies")]
	[Export]
	public JourneyStateService JourneyState { get; set; } = null!;

	public override void _Ready()
	{
		if (!ValidateReferences())
		{
			SetProcess(false);
			return;
		}

		JourneyState.StateChanged += OnJourneyStateChanged;

		ApplyJourneyState(JourneyState.CurrentState);

		DebugLog.Print(
			$"Region presentation initialized. " +
			$"TravelSpeed={TravelSpeed}, " +
			$"JourneyState={JourneyState.CurrentState}");
	}
	
	public override void _ExitTree()
	{
		if (GodotObject.IsInstanceValid(JourneyState))
		{
			JourneyState.StateChanged -= OnJourneyStateChanged;
		}
	}
	
	public override void _Process(double delta)
	{
		if (!_isTravelPresentationActive)
			return;

		float movement = TravelSpeed * (float)delta;
		RegionTileA.Position += Vector2.Right * movement;
		RegionTileB.Position += Vector2.Right * movement;

		WrapTileIfNeeded(RegionTileA);
		WrapTileIfNeeded(RegionTileB);
	}
	
	private void OnJourneyStateChanged(JourneyStateService.JourneyState previousState, JourneyStateService.JourneyState currentState)
	{
		ApplyJourneyState(currentState);
	}
	
	private void ApplyJourneyState(JourneyStateService.JourneyState state)
	{
		_isTravelPresentationActive = state == JourneyStateService.JourneyState.Traveling;
	}

	private void WrapTileIfNeeded(Node2D tile)
	{
		if (tile.Position.X < TileWidth)
			return;

		tile.Position -=
			new Vector2(TileWidth * 2.0f, 0.0f);
	}
	
	private bool ValidateReferences()
	{
		bool valid = true;

		valid &= Require(JourneyState, nameof(JourneyState));
		valid &= Require(RegionTileA, nameof(RegionTileA));
		valid &= Require(RegionTileB, nameof(RegionTileB));

		return valid;
	}

	private static bool Require(
		GodotObject value,
		string propertyName)
	{
		if (GodotObject.IsInstanceValid(value))
			return true;

		GD.PushError(
			$"RegionPresentationController is missing the " +
			$"Inspector reference '{propertyName}'.");

		return false;
	}
	
	private bool _isTravelPresentationActive;
}
