using Godot;
using System;

public partial class StackSplitPopupController : Node
{
    [ExportCategory("Popup UI")]
    [Export] public Window Popup { get; set; } = null!;
    [Export] public Label PromptLabel { get; set; } = null!;
    [Export] public SpinBox QuantityInput { get; set; } = null!;
    [Export] public Button ConfirmButton { get; set; } = null!;
    [Export] public Button DeleteButton { get; set; } = null!;
    [Export] public Button CancelButton { get; set; } = null!;

    [ExportCategory("Popup Layout")]
    [Export] public Vector2I ActionPopupSize { get; set; } = new(300, 112);
    [Export] public Vector2I SplitPopupSize { get; set; } = new(220, 142);
    [Export] public Vector2I MouseOffset { get; set; } = new(12, 12);

    public event Action<int>? SplitConfirmed;
    public event Action? DeleteConfirmed;

    private bool _showingItemActions;
    private string _displayName = string.Empty;
    private int _stackQuantity;

    public override void _Ready()
    {
        ConfirmButton.Pressed += Confirm;
        DeleteButton.Pressed += Delete;
        CancelButton.Pressed += HidePopup;
        Popup.CloseRequested += HidePopup;
        Popup.FocusExited += HidePopup;
        Popup.Hide();
    }

    public override void _ExitTree()
    {
        if (GodotObject.IsInstanceValid(ConfirmButton)) ConfirmButton.Pressed -= Confirm;
        if (GodotObject.IsInstanceValid(DeleteButton)) DeleteButton.Pressed -= Delete;
        if (GodotObject.IsInstanceValid(CancelButton)) CancelButton.Pressed -= HidePopup;
        if (GodotObject.IsInstanceValid(Popup))
        {
            Popup.CloseRequested -= HidePopup;
            Popup.FocusExited -= HidePopup;
        }
    }

    /// <summary>
    /// Opens the existing quantity picker directly. Shift+click remains a
    /// fast shortcut even after right-click item actions are available.
    /// </summary>
    public void Open(string displayName, int stackQuantity)
    {
        _displayName = displayName;
        _stackQuantity = stackQuantity;
        ShowSplitControls(centerOnScreen: true);
    }

    /// <summary>
    /// Opens a compact item-action window beside the mouse. Split transitions
    /// into the existing quantity picker; delete removes the selected record.
    /// </summary>
    public void OpenActions(
        string displayName,
        int quantity,
        bool isStackable)
    {
        _displayName = displayName;
        _stackQuantity = quantity;
        _showingItemActions = true;

        Popup.Title = "Item Actions";
        PromptLabel.Text = quantity > 1
            ? $"{displayName} x{quantity}"
            : displayName;
        QuantityInput.Hide();
        DeleteButton.Show();
        DeleteButton.Text = isStackable
            ? "Delete Stack"
            : "Delete Item";
        ConfirmButton.Text = "Split Stack";
        ConfirmButton.Disabled = !isStackable || quantity < 2;
        CancelButton.Text = "Cancel";
        Popup.Size = ActionPopupSize;
        Popup.Popup();
        PositionBesideMouse();

        if (ConfirmButton.Disabled)
            DeleteButton.GrabFocus();
        else
            ConfirmButton.GrabFocus();
    }

    private void Confirm()
    {
        if (_showingItemActions)
        {
            ShowSplitControls(centerOnScreen: false);
            return;
        }

        int quantity = Mathf.RoundToInt(QuantityInput.Value);
        HidePopup();
        SplitConfirmed?.Invoke(quantity);
    }

    private void Delete()
    {
        HidePopup();
        DeleteConfirmed?.Invoke();
    }

    private void ShowSplitControls(bool centerOnScreen)
    {
        _showingItemActions = false;
        int maximumSplit = Math.Max(1, _stackQuantity - 1);

        Popup.Title = "Split Stack";
        PromptLabel.Text = $"Split {_displayName} ({_stackQuantity})";
        QuantityInput.MinValue = 1;
        QuantityInput.MaxValue = maximumSplit;
        QuantityInput.Step = 1;
        QuantityInput.Value = Math.Max(1, _stackQuantity / 2);
        QuantityInput.Show();
        DeleteButton.Hide();
        ConfirmButton.Text = "Split";
        ConfirmButton.Disabled = false;
        CancelButton.Text = "Cancel";
        Popup.Size = SplitPopupSize;

        if (centerOnScreen)
            Popup.PopupCentered();

        QuantityInput.GrabFocus();
    }

    private void HidePopup()
    {
        Popup.Hide();
        _showingItemActions = false;
    }

    private void PositionBesideMouse()
    {
        Vector2I mousePosition = DisplayServer.MouseGetPosition();
        Rect2I usableArea = GetMouseScreenUsableArea(mousePosition);
        int maximumX = Math.Max(
            usableArea.Position.X,
            usableArea.End.X - Popup.Size.X);
        int maximumY = Math.Max(
            usableArea.Position.Y,
            usableArea.End.Y - Popup.Size.Y);

        Popup.Position = new Vector2I(
            Math.Clamp(
                mousePosition.X + MouseOffset.X,
                usableArea.Position.X,
                maximumX),
            Math.Clamp(
                mousePosition.Y + MouseOffset.Y,
                usableArea.Position.Y,
                maximumY));
    }

    private static Rect2I GetMouseScreenUsableArea(Vector2I mousePosition)
    {
        int screenCount = DisplayServer.GetScreenCount();

        for (int screen = 0; screen < screenCount; screen++)
        {
            Rect2I area = DisplayServer.ScreenGetUsableRect(screen);

            if (area.HasPoint(mousePosition))
                return area;
        }

        return DisplayServer.ScreenGetUsableRect(0);
    }
}
