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

	[Export]
	public bool TravelEnabled { get; set; } = true;

	[ExportCategory("Logical Stage")]
	[Export(PropertyHint.Range, "1,7680,1")]
	public float TileWidth { get; set; } = 800.0f;

	public override void _Ready()
	{
		if (!ValidateReferences())
		{
			SetProcess(false);
			return;
		}

		GD.Print(
			$"Region presentation initialized. " +
			$"TravelEnabled={TravelEnabled}, " +
			$"TravelSpeed={TravelSpeed}");
	}

	public override void _Process(double delta)
	{
		if (!TravelEnabled)
			return;

		float movement =
			TravelSpeed * (float)delta;

		RegionTileA.Position +=
			Vector2.Right * movement;

		RegionTileB.Position +=
			Vector2.Right * movement;

		WrapTileIfNeeded(RegionTileA);
		WrapTileIfNeeded(RegionTileB);
	}

	public void StartTravel()
	{
		TravelEnabled = true;
	}

	public void StopTravel()
	{
		TravelEnabled = false;
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

		valid &= Require(
			RegionTileA,
			nameof(RegionTileA));

		valid &= Require(
			RegionTileB,
			nameof(RegionTileB));

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
}
