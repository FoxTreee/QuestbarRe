using Godot;

public partial class IncapacitationChoicePopupController : Node
{
	[ExportCategory("Dependencies")]
	/// <summary>
	/// Inspector reference used by this component for its region run dependency.
	/// Assign the matching node or resource from the scene; leaving it empty prevents that connection from working.
	/// </summary>
	[Export]
	public RegionRunController RegionRun { get; set; } = null!;

	/// <summary>
	/// Inspector reference used by this component for its choice window dependency.
	/// Assign the matching node or resource from the scene; leaving it empty prevents that connection from working.
	/// </summary>
	[Export]
	public Window ChoiceWindow { get; set; } = null!;

	/// <summary>
	/// Inspector reference used by this component for its hero name label dependency.
	/// Assign the matching node or resource from the scene; leaving it empty prevents that connection from working.
	/// </summary>
	[Export]
	public Label HeroNameLabel { get; set; } = null!;

	/// <summary>
	/// Inspector reference used by this component for its message label dependency.
	/// Assign the matching node or resource from the scene; leaving it empty prevents that connection from working.
	/// </summary>
	[Export]
	public Label MessageLabel { get; set; } = null!;

	/// <summary>
	/// Inspector reference used by this component for its revive button dependency.
	/// Assign the matching node or resource from the scene; leaving it empty prevents that connection from working.
	/// </summary>
	[Export]
	public Button ReviveButton { get; set; } = null!;

	/// <summary>
	/// Inspector reference used by this component for its incapacitate button dependency.
	/// Assign the matching node or resource from the scene; leaving it empty prevents that connection from working.
	/// </summary>
	[Export]
	public Button IncapacitateButton { get; set; } = null!;

	[ExportCategory("Popup")]
	/// <summary>
	/// Controls popup size.
	/// For example, changing 520 to 1040 doubles this setting's configured contribution to the system.
	/// </summary>
	[Export]
	public Vector2I PopupSize { get; set; } = new(520, 260);

	private bool _choicePending;

	/// <summary>
	/// Runs Godot setup for Incapacitation Choice Popup Controller when the node enters the scene tree.
	/// Uses the current node and service state; any result is applied through side effects, events, or stored fields.
	/// </summary>
	public override void _Ready()
	{
		if (!ValidateReferences())
			return;

		ChoiceWindow.Visible = false;
		ChoiceWindow.CloseRequested += OnCloseRequested;
		ReviveButton.Pressed += OnRevivePressed;
		IncapacitateButton.Pressed += OnIncapacitatePressed;
		RegionRun.IncapacitationChoiceRequested += OnChoiceRequested;
	}

	/// <summary>
	/// Cleans up Incapacitation Choice Popup Controller when the node leaves the scene tree.
	/// Uses the current node and service state; any result is applied through side effects, events, or stored fields.
	/// </summary>
	public override void _ExitTree()
	{
		if (GodotObject.IsInstanceValid(ChoiceWindow))
			ChoiceWindow.CloseRequested -= OnCloseRequested;

		if (GodotObject.IsInstanceValid(ReviveButton))
			ReviveButton.Pressed -= OnRevivePressed;

		if (GodotObject.IsInstanceValid(IncapacitateButton))
			IncapacitateButton.Pressed -= OnIncapacitatePressed;

		if (GodotObject.IsInstanceValid(RegionRun))
			RegionRun.IncapacitationChoiceRequested -= OnChoiceRequested;
	}

	/// <summary>
	/// Handles the choice requested event and updates the related game state.
	/// Uses the supplied arguments and current node state; any result is applied through side effects, events, or stored fields.
	/// </summary>
	private void OnChoiceRequested(
		HeroActorController hero,
		bool reviveAvailable)
	{
		_choicePending = true;
		HeroNameLabel.Text = hero.Name.ToString();
		MessageLabel.Text =
			"This hero was incapacitated. Revive them now, or remove " +
			"them from the active party for the remainder of this run?";

		ReviveButton.Disabled = !reviveAvailable;
		ReviveButton.TooltipText = reviveAvailable
			? "Restore this hero and return them to the active party."
			: "Requires a priest, revive potion, or another revive source.";

		ChoiceWindow.PopupCentered(PopupSize);
		ChoiceWindow.GrabFocus();
	}

	/// <summary>
	/// Handles the revive pressed event and updates the related game state.
	/// Uses the current node and service state; any result is applied through side effects, events, or stored fields.
	/// </summary>
	private void OnRevivePressed()
	{
		ResolveChoice(true);
	}

	/// <summary>
	/// Handles the incapacitate pressed event and updates the related game state.
	/// Uses the current node and service state; any result is applied through side effects, events, or stored fields.
	/// </summary>
	private void OnIncapacitatePressed()
	{
		ResolveChoice(false);
	}

	/// <summary>
	/// Performs the resolve choice operation for Incapacitation Choice Popup Controller.
	/// Uses the supplied arguments and current node state; any result is applied through side effects, events, or stored fields.
	/// </summary>
	private void ResolveChoice(bool revive)
	{
		if (!_choicePending)
			return;

		_choicePending = false;
		ChoiceWindow.Hide();
		RegionRun.ResolveCurrentIncapacitationChoice(revive);
	}

	/// <summary>
	/// Handles the close requested event and updates the related game state.
	/// Uses the current node and service state; any result is applied through side effects, events, or stored fields.
	/// </summary>
	private void OnCloseRequested()
	{
		if (_choicePending)
		{
			ChoiceWindow.GrabFocus();
			return;
		}

		ChoiceWindow.Hide();
	}

	/// <summary>
	/// Performs the validate references operation for Incapacitation Choice Popup Controller.
	/// Reads the current state and returns the resulting bool to the caller.
	/// </summary>
	private bool ValidateReferences()
	{
		bool valid = true;
		valid &= Require(RegionRun, nameof(RegionRun));
		valid &= Require(ChoiceWindow, nameof(ChoiceWindow));
		valid &= Require(HeroNameLabel, nameof(HeroNameLabel));
		valid &= Require(MessageLabel, nameof(MessageLabel));
		valid &= Require(ReviveButton, nameof(ReviveButton));
		valid &= Require(IncapacitateButton, nameof(IncapacitateButton));
		return valid;
	}

	/// <summary>
	/// Performs the require operation for Incapacitation Choice Popup Controller.
	/// Uses the supplied arguments and current state and returns the resulting bool to the caller.
	/// </summary>
	private static bool Require(GodotObject value, string propertyName)
	{
		if (GodotObject.IsInstanceValid(value))
			return true;

		GD.PushError(
			$"IncapacitationChoicePopupController is missing the " +
			$"Inspector reference '{propertyName}'.");
		return false;
	}
}
