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
	/// <summary>
	/// Controls initial state.
	/// For example, selecting a different value changes which initial state behavior or content the owning system uses.
	/// </summary>
	[Export]
	
	public JourneyState InitialState { get; set; }
		= JourneyState.Traveling;

	public JourneyState CurrentState { get; private set; }
	= JourneyState.Traveling;

	/// <summary>
	/// Runs Godot setup for Journey State Service when the node enters the scene tree.
	/// Uses the current node and service state; any result is applied through side effects, events, or stored fields.
	/// </summary>
	public override void _Ready()
	{
		SetState(InitialState);

		DebugLog.Print(
			$"Journey state initialized: {CurrentState}");
	}

	/// <summary>
	/// Updates state and applies the new value to the owning system.
	/// Uses the supplied arguments and current node state; any result is applied through side effects, events, or stored fields.
	/// </summary>
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

	/// <summary>
	/// Performs the begin travel operation for Journey State Service.
	/// Uses the current node and service state; any result is applied through side effects, events, or stored fields.
	/// </summary>
	public void BeginTravel()
	{
		SetState(JourneyState.Traveling);
	}

	/// <summary>
	/// Performs the begin encounter operation for Journey State Service.
	/// Uses the current node and service state; any result is applied through side effects, events, or stored fields.
	/// </summary>
	public void BeginEncounter()
	{
		SetState(JourneyState.Encounter);
	}

	/// <summary>
	/// Performs the end encounter operation for Journey State Service.
	/// Uses the current node and service state; any result is applied through side effects, events, or stored fields.
	/// </summary>
	public void EndEncounter()
	{
		SetState(JourneyState.Traveling);
	}

	/// <summary>
	/// Performs the toggle test encounter operation for Journey State Service.
	/// Uses the current node and service state; any result is applied through side effects, events, or stored fields.
	/// </summary>
	public void ToggleTestEncounter()
	{
		SetState(
			CurrentState == JourneyState.Traveling
				? JourneyState.Encounter
				: JourneyState.Traveling);
	}
	
	/// <summary>
	/// Performs the unhandled key input operation for Journey State Service.
	/// Uses the supplied arguments and current node state; any result is applied through side effects, events, or stored fields.
	/// </summary>
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
