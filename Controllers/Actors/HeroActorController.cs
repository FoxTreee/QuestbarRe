using Godot;

public partial class HeroActorController : Node2D
{
	private Vector2 _visualRestPosition;
	private double _animationTime;
	private bool _isTraveling;
	
	[ExportCategory("Formation")]
	[Export]
	public Node2D FormationAnchor { get; set; } = null!;

	[Export]
	public Vector2 FormationOffset { get; set; } = Vector2.Zero;
	
	[ExportCategory("Dependencies")]
	[Export]
	public JourneyStateService JourneyState { get; set; } = null!;

	[ExportCategory("Visuals")]
	[Export]
	public Node2D VisualRoot { get; set; } = null!;

	[ExportCategory("Travel Animation")]
	[Export(PropertyHint.Range, "0,20,0.5")]
	public float BobHeight { get; set; } = 4.0f;

	[Export(PropertyHint.Range, "0,20,0.1")]
	public float BobSpeed { get; set; } = 7.0f;

	[Export(PropertyHint.Range, "0,6.28,0.01")]
	public float BobPhaseOffset { get; set; } = 0.0f;

	public Vector2 FormationPosition => FormationAnchor.GlobalPosition + FormationOffset;

	public override void _Ready()
{
	if (!ValidateReferences())
	{
		SetProcess(false);
		return;
	}

	_visualRestPosition = VisualRoot.Position;

	JourneyState.StateChanged += OnJourneyStateChanged;

	ApplyJourneyState(JourneyState.CurrentState);
	SnapToFormation();

	GD.Print(
		$"HeroActor initialized at formation position " +
		$"{FormationPosition}.");
}

	public override void _ExitTree()
	{
		if (GodotObject.IsInstanceValid(JourneyState))
		{
			JourneyState.StateChanged -=
				OnJourneyStateChanged;
		}
	}
	
	public override void _Process(double delta)
	{
		if (!_isTraveling)
			return;

		_animationTime += delta;

		float bobOffset =
			Mathf.Abs(Mathf.Sin((float)(_animationTime * 
			BobSpeed) + BobPhaseOffset)) * BobHeight;

		VisualRoot.Position = _visualRestPosition + Vector2.Up * bobOffset;
	}
	
	private void OnJourneyStateChanged(
		JourneyStateService.JourneyState previousState,
		JourneyStateService.JourneyState currentState)
	{
		ApplyJourneyState(currentState);
	}
	
	private void ApplyJourneyState(
		JourneyStateService.JourneyState state)
	{
		_isTraveling = state == JourneyStateService.JourneyState.Traveling;

		if (_isTraveling)
			return;

		_animationTime = 0.0;
		VisualRoot.Position = _visualRestPosition;
	}
	
	private bool ValidateReferences()
	{
		bool valid = true;

		valid &= Require(FormationAnchor, nameof(FormationAnchor));

		valid &= Require(JourneyState, nameof(JourneyState));

		valid &= Require(VisualRoot, nameof(VisualRoot));

		return valid;
	}
	
	private static bool Require(GodotObject value, string propertyName)
{
	if (GodotObject.IsInstanceValid(value))
		return true;

	GD.PushError(
		$"HeroActorController is missing the " +
		$"Inspector reference '{propertyName}'.");

	return false;
}
	
	public void SnapToFormation()
	{
		GlobalPosition = FormationPosition;
	}
}
