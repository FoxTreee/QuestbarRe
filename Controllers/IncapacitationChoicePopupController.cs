using Godot;

public partial class IncapacitationChoicePopupController : Node
{
	[ExportCategory("Dependencies")]
	[Export]
	public RegionRunController RegionRun { get; set; } = null!;

	[Export]
	public Window ChoiceWindow { get; set; } = null!;

	[Export]
	public Label HeroNameLabel { get; set; } = null!;

	[Export]
	public Label MessageLabel { get; set; } = null!;

	[Export]
	public Button ReviveButton { get; set; } = null!;

	[Export]
	public Button IncapacitateButton { get; set; } = null!;

	[ExportCategory("Popup")]
	[Export]
	public Vector2I PopupSize { get; set; } = new(520, 260);

	private bool _choicePending;

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

	private void OnRevivePressed()
	{
		ResolveChoice(true);
	}

	private void OnIncapacitatePressed()
	{
		ResolveChoice(false);
	}

	private void ResolveChoice(bool revive)
	{
		if (!_choicePending)
			return;

		_choicePending = false;
		ChoiceWindow.Hide();
		RegionRun.ResolveCurrentIncapacitationChoice(revive);
	}

	private void OnCloseRequested()
	{
		if (_choicePending)
		{
			ChoiceWindow.GrabFocus();
			return;
		}

		ChoiceWindow.Hide();
	}

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
