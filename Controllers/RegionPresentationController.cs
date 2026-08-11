using Godot;

public partial class RegionPresentationController : Node2D
{
	[ExportCategory("Background Tiles")]
	/// <summary>
	/// Inspector reference used by this component for its region tile a dependency.
	/// Assign the matching node or resource from the scene; leaving it empty prevents that connection from working.
	/// </summary>
	[Export]
	public Node2D RegionTileA { get; set; } = null!;

	/// <summary>
	/// Inspector reference used by this component for its region tile b dependency.
	/// Assign the matching node or resource from the scene; leaving it empty prevents that connection from working.
	/// </summary>
	[Export]
	public Node2D RegionTileB { get; set; } = null!;

	[ExportCategory("Region Visuals")]
	/// <summary>
	/// Inspector reference used by this component for its background presentation dependency.
	/// Assign the matching node or resource from the scene; leaving it empty prevents that connection from working.
	/// </summary>
	[Export]
	public BackgroundPresentationController BackgroundPresentation
	{
		get;
		set;
	} = null!;

	/// <summary>
	/// Inspector reference used by this component for its traveling ground dependency.
	/// Assign the matching node or resource from the scene; leaving it empty prevents that connection from working.
	/// </summary>
	[Export]
	public Parallax2D TravelingGround { get; set; } = null!;

	/// <summary>
	/// Inspector reference used by this component for its ground sprite dependency.
	/// Assign the matching node or resource from the scene; leaving it empty prevents that connection from working.
	/// </summary>
	[Export]
	public Sprite2D GroundSprite { get; set; } = null!;

	[ExportCategory("Travel")]
	/// <summary>
	/// Controls travel speed, measured as pixels per second.
	/// For example, changing 60 to 120 makes the affected movement or animation run about twice as fast.
	/// </summary>
	[Export(PropertyHint.Range, "0,500,1")]
	public float TravelSpeed { get; set; } = 60.0f;

	/// <summary>
	/// Controls ground travel speed, measured as pixels per second.
	/// For example, changing 90 to 180 makes the affected movement or animation run about twice as fast.
	/// </summary>
	[Export(PropertyHint.Range, "0,500,1")]
	public float GroundTravelSpeed { get; set; } = 90.0f;

	[ExportCategory("Logical Stage")]
	/// <summary>
	/// Controls tile width, measured as pixels.
	/// For example, changing 800 to 1600 doubles the configured tile width.
	/// </summary>
	[Export(PropertyHint.Range, "1,7680,1")]
	public float TileWidth { get; set; } = 800.0f;

	[ExportCategory("Dependencies")]
	/// <summary>
	/// Inspector reference used by this component for its journey state dependency.
	/// Assign the matching node or resource from the scene; leaving it empty prevents that connection from working.
	/// </summary>
	[Export]
	public JourneyStateService JourneyState { get; set; } = null!;

	/// <summary>
	/// Runs Godot setup for Region Presentation Controller when the node enters the scene tree.
	/// Uses the current node and service state; any result is applied through side effects, events, or stored fields.
	/// </summary>
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
			$"GroundTravelSpeed={GroundTravelSpeed}, " +
			$"JourneyState={JourneyState.CurrentState}");
	}

	/// <summary>
	/// Cleans up Region Presentation Controller when the node leaves the scene tree.
	/// Uses the current node and service state; any result is applied through side effects, events, or stored fields.
	/// </summary>
	public override void _ExitTree()
	{
		if (GodotObject.IsInstanceValid(JourneyState))
			JourneyState.StateChanged -= OnJourneyStateChanged;
	}

	/// <summary>
	/// Updates Region Presentation Controller every rendered frame using the supplied frame delta.
	/// Uses the supplied arguments and current node state; any result is applied through side effects, events, or stored fields.
	/// </summary>
	public override void _Process(double delta)
	{
		if (!_isTravelPresentationActive)
			return;

		float backgroundMovement = TravelSpeed * (float)delta;
		RegionTileA.Position += Vector2.Right * backgroundMovement;
		RegionTileB.Position += Vector2.Right * backgroundMovement;

		WrapTileIfNeeded(RegionTileA);
		WrapTileIfNeeded(RegionTileB);

		TravelingGround.ScrollOffset +=
			Vector2.Right * GroundTravelSpeed * (float)delta;
	}

	/// <summary>
	/// Applies region to the relevant actor, resource, or presentation state.
	/// Uses the supplied arguments and current node state; any result is applied through side effects, events, or stored fields.
	/// </summary>
	public void ApplyRegion(RegionDefinition region)
	{
		if (!GodotObject.IsInstanceValid(region))
		{
			GD.PushError(
				"RegionPresentationController cannot apply a null region.");
			return;
		}

		if (!GodotObject.IsInstanceValid(region.BackgroundTexture)
			|| !GodotObject.IsInstanceValid(region.GroundTexture))
		{
			GD.PushError(
				$"Region '{region.ContentId}' requires both presentation " +
				"textures before it can be displayed.");
			return;
		}

		BackgroundPresentation.ApplyTexture(region.BackgroundTexture);
		GroundSprite.Texture = region.GroundTexture;

		float renderedGroundWidth =
			region.GroundTexture.GetWidth()
			* Mathf.Abs(GroundSprite.Scale.X);

		TravelingGround.RepeatSize = new Vector2(
			renderedGroundWidth,
			TravelingGround.RepeatSize.Y);

		DebugLog.Print(
			$"Region visuals applied: {region.DisplayName} " +
			$"({region.ContentId}), " +
			$"GroundRepeatWidth={renderedGroundWidth:0.###}.");
	}

	/// <summary>
	/// Handles the journey state changed event and updates the related game state.
	/// Uses the supplied arguments and current node state; any result is applied through side effects, events, or stored fields.
	/// </summary>
	private void OnJourneyStateChanged(
		JourneyStateService.JourneyState previousState,
		JourneyStateService.JourneyState currentState)
	{
		ApplyJourneyState(currentState);
	}

	/// <summary>
	/// Applies journey state to the relevant actor, resource, or presentation state.
	/// Uses the supplied arguments and current node state; any result is applied through side effects, events, or stored fields.
	/// </summary>
	private void ApplyJourneyState(
		JourneyStateService.JourneyState state)
	{
		_isTravelPresentationActive =
			state == JourneyStateService.JourneyState.Traveling;
	}

	/// <summary>
	/// Performs the wrap tile if needed operation for Region Presentation Controller.
	/// Uses the supplied arguments and current node state; any result is applied through side effects, events, or stored fields.
	/// </summary>
	private void WrapTileIfNeeded(Node2D tile)
	{
		if (tile.Position.X < TileWidth)
			return;

		tile.Position -= new Vector2(TileWidth * 2.0f, 0.0f);
	}

	/// <summary>
	/// Performs the validate references operation for Region Presentation Controller.
	/// Reads the current state and returns the resulting bool to the caller.
	/// </summary>
	private bool ValidateReferences()
	{
		bool valid = true;
		valid &= Require(JourneyState, nameof(JourneyState));
		valid &= Require(RegionTileA, nameof(RegionTileA));
		valid &= Require(RegionTileB, nameof(RegionTileB));
		valid &= Require(
			BackgroundPresentation,
			nameof(BackgroundPresentation));
		valid &= Require(TravelingGround, nameof(TravelingGround));
		valid &= Require(GroundSprite, nameof(GroundSprite));
		return valid;
	}

	/// <summary>
	/// Performs the require operation for Region Presentation Controller.
	/// Uses the supplied arguments and current state and returns the resulting bool to the caller.
	/// </summary>
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
