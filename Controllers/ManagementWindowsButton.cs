using Godot;

/// <summary>
/// Main-window button that opens or closes the complete management-window
/// group. It remains available in the bottom collapsed portion of Questbar.
/// </summary>
public partial class ManagementWindowsButton : Button
{
    [ExportCategory("Dependencies")]
    /// <summary>
    /// Owns the Character, Backpack, and Map visibility group.
    /// </summary>
    [Export]
    public PopupWindowFormationController Formation { get; set; } = null!;

    public override void _Ready()
    {
        if (!GodotObject.IsInstanceValid(Formation))
        {
            GD.PushError(
                "ManagementWindowsButton is missing its Formation reference.");
            Disabled = true;
            return;
        }

        Pressed += OnPressed;
    }

    public override void _ExitTree()
    {
        if (GodotObject.IsInstanceValid(Formation))
            Pressed -= OnPressed;
    }

    private void OnPressed()
    {
        Formation.ToggleManagementGroup();
    }
}
