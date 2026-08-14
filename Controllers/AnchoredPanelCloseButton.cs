using Godot;

/// <summary>
/// Reusable close button for any custom anchored panel. Assign the complete
/// panel root in the Inspector; pressing this button hides that panel without
/// knowing anything about its formation, content, or owning controller.
/// </summary>
public partial class AnchoredPanelCloseButton : Button
{
    /// <summary>
    /// Complete custom window panel hidden when this button is pressed.
    /// </summary>
    [Export]
    public Control PanelRoot { get; set; } = null!;

    public override void _Ready()
    {
        if (!GodotObject.IsInstanceValid(PanelRoot))
        {
            GD.PushError(
                $"{Name} is missing the Inspector reference 'PanelRoot'.");
        }
    }

    /// <summary>
    /// Hides the assigned panel. The formation controller observes visibility
    /// and hides or resizes its transparent native host automatically.
    /// </summary>
    public override void _Pressed()
    {
        if (GodotObject.IsInstanceValid(PanelRoot))
            PanelRoot.Hide();
    }
}
