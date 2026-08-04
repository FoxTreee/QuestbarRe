using Godot;

public partial class JourneyStateService : Node
{
	public enum JourneyState
	{
		Idle,
		Traveling
	}

	[Signal]
	public delegate void StateChangedEventHandler(
		JourneyState previousState,
		JourneyState currentState);

	[ExportCategory("Startup")]
	[Export]
	
	public JourneyState InitialState { get; set; }
		= JourneyState.Traveling;

	public JourneyState CurrentState { get; private set; }
		= JourneyState.Idle;

	public override void _Ready()
	{
		SetState(InitialState);

		GD.Print(
			$"Journey state initialized: {CurrentState}");
	}

	public void SetState(JourneyState newState)
	{
		if (CurrentState == newState)
			return;

		JourneyState previousState = CurrentState;
		CurrentState = newState;

		EmitSignal(
			SignalName.StateChanged,
			(int)previousState,
			(int)CurrentState);

		GD.Print(
			$"Journey state changed: " +
			$"{previousState} → {CurrentState}");
	}

	public void BeginTravel()
	{
		SetState(JourneyState.Traveling);
	}

	public void StopTravel()
	{
		SetState(JourneyState.Idle);
	}

	public void ToggleTravel()
	{
		SetState(
			CurrentState == JourneyState.Traveling
				? JourneyState.Idle
				: JourneyState.Traveling);
	}
	
	public override void _UnhandledKeyInput(InputEvent @event)
	{
		if (@event is not InputEventKey keyEvent)
			return;

		if (!keyEvent.Pressed || keyEvent.Echo)
			return;

		if (keyEvent.Keycode != Key.J)
			return;

		ToggleTravel();
		GetViewport().SetInputAsHandled();
	}
}
