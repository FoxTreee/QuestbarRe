using Godot;
using System;

public partial class StackSplitPopupController : Node
{
    [ExportCategory("Popup UI")]
    [Export] public Window Popup { get; set; } = null!;
    [Export] public Label PromptLabel { get; set; } = null!;
    [Export] public SpinBox QuantityInput { get; set; } = null!;
    [Export] public Button ConfirmButton { get; set; } = null!;
    [Export] public Button CancelButton { get; set; } = null!;

    public event Action<int>? SplitConfirmed;

    public override void _Ready()
    {
        ConfirmButton.Pressed += Confirm;
        CancelButton.Pressed += Popup.Hide;
        Popup.CloseRequested += Popup.Hide;
        Popup.Hide();
    }

    public override void _ExitTree()
    {
        if (GodotObject.IsInstanceValid(ConfirmButton)) ConfirmButton.Pressed -= Confirm;
        if (GodotObject.IsInstanceValid(CancelButton)) CancelButton.Pressed -= Popup.Hide;
        if (GodotObject.IsInstanceValid(Popup)) Popup.CloseRequested -= Popup.Hide;
    }

    public void Open(string displayName, int stackQuantity)
    {
        int maximumSplit = Math.Max(1, stackQuantity - 1);
        PromptLabel.Text = $"Split {displayName} ({stackQuantity})";
        QuantityInput.MinValue = 1;
        QuantityInput.MaxValue = maximumSplit;
        QuantityInput.Step = 1;
        QuantityInput.Value = Math.Max(1, stackQuantity / 2);
        Popup.PopupCentered();
        QuantityInput.GrabFocus();
    }

    private void Confirm()
    {
        int quantity = Mathf.RoundToInt(QuantityInput.Value);
        Popup.Hide();
        SplitConfirmed?.Invoke(quantity);
    }
}
