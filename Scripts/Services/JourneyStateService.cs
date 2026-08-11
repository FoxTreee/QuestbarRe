using Godot;

public partial class JourneyStateService : Node
{
	public enum JourneyState
	{
		Traveling,
		Encounter,
		AwaitingIncapacitationChoice,
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
	= JourneyState.Traveling;

	public override void _Ready()
	{
		SetState(InitialState);

		DebugLog.Print(
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

		DebugLog.Print(
			$"Journey state changed: " +
			$"{previousState} → {CurrentState}");
	}

	public void BeginTravel()
	{
		SetState(JourneyState.Traveling);
	}

	public void BeginEncounter()
	{
		SetState(JourneyState.Encounter);
	}

	public void EndEncounter()
	{
		SetState(JourneyState.Traveling);
	}

	public void ToggleTestEncounter()
	{
		SetState(
			CurrentState == JourneyState.Traveling
				? JourneyState.Encounter
				: JourneyState.Traveling);
	}
	
	public override void _UnhandledKeyInput(InputEvent @event)
{
	if (@event is not InputEventKey keyEvent)
		return;

	if (!keyEvent.Pressed || keyEvent.Echo)
		return;

	if (keyEvent.Keycode != Key.E)
		return;

	ToggleTestEncounter();
	GetViewport().SetInputAsHandled();
}
}
