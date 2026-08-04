using Godot;

public partial class HeroActorController : Node2D
{
	[ExportCategory("Formation")]
	[Export]
	public Node2D FormationAnchor { get; set; } = null!;

	public Vector2 FormationPosition =>
		FormationAnchor.GlobalPosition;

	public override void _Ready()
	{
		if (!GodotObject.IsInstanceValid(FormationAnchor))
		{
			GD.PushError(
				"HeroActorController is missing its " +
				"FormationAnchor Inspector reference.");

			return;
		}

		SnapToFormation();

		GD.Print(
			$"HeroActor initialized at formation position " +
			$"{FormationPosition}.");
	}

	public void SnapToFormation()
	{
		GlobalPosition = FormationPosition;
	}
}
