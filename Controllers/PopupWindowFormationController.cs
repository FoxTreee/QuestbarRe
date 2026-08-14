using Godot;

/// <summary>
/// Keeps Questbar's utility windows in a stable formation around the main
/// desktop window. Character and Backpack are hosted as locked custom Control
/// panels in one native host while critical modals remain independent.
/// </summary>
public partial class PopupWindowFormationController : Node
{
    [ExportCategory("Dependencies")]

    /// <summary>
    /// Owns the native Questbar window position, size, and expanded state.
    /// </summary>
    [Export]
    public DesktopWindowHostController WindowHost { get; set; } = null!;

    /// <summary>
    /// Borderless transparent native host for custom management panels.
    /// </summary>
    [Export]
    public Window FormationHost { get; set; } = null!;

    /// <summary>
    /// Character-management window anchored above the Questbar window.
    /// </summary>
    [Export]
    public Control CharacterWindow { get; set; } = null!;

    /// <summary>
    /// Backpack anchored by its right edge to the Character window.
    /// </summary>
    [Export]
    public Control BackpackWindow { get; set; } = null!;

    /// <summary>
    /// Custom item tooltip panel rendered above both management panels.
    /// </summary>
    [Export]
    public Control ItemTooltipPanel { get; set; } = null!;

    /// <summary>
    /// Critical hero-choice window centered directly above Questbar.
    /// </summary>
    [Export]
    public Window IncapacitationWindow { get; set; } = null!;

    [ExportCategory("Formation Spacing")]

    /// <summary>
    /// Empty horizontal space between Backpack and Character.
    /// </summary>
    [Export(PropertyHint.Range, "0,64,1")]
    public int WindowGap { get; set; } = 8;

    /// <summary>
    /// Empty vertical space between Questbar and windows anchored above it.
    /// </summary>
    [Export(PropertyHint.Range, "0,64,1")]
    public int QuestbarGap { get; set; } = 8;

    /// <summary>
    /// Additional Inspector-tunable offset for the Character/Backpack group.
    /// </summary>
    [Export]
    public Vector2I ManagementOffset { get; set; } = Vector2I.Zero;

    /// <summary>
    /// Additional Inspector-tunable offset for the incapacitation modal.
    /// </summary>
    [Export]
    public Vector2I IncapacitationOffset { get; set; } = Vector2I.Zero;

    [ExportCategory("Formation Lock")]

    /// <summary>
    /// Keeps managed windows on their assigned anchors if the operating system
    /// or player attempts to move them. Disable only while tuning positions.
    /// </summary>
    [Export]
    public bool LockWindowPositions { get; set; } = true;

    private Vector2 _characterAnchor;
    private Vector2 _backpackAnchor;
    private Vector2I _formationHostAnchor;
    private Vector2I _incapacitationAnchor;
    private bool _anchorsInitialized;
    private bool _lastCharacterVisible;
    private bool _lastBackpackVisible;

    /// <summary>
    /// Subscribes to host placement and window-size changes, then applies the
    /// initial formation after all native windows have entered the tree.
    /// </summary>
    public override void _Ready()
    {
        if (!ValidateReferences())
            return;

        ConfigureFormationHost();
        HostManagementPanels();

        WindowHost.WindowPlacementApplied += ApplyFormation;
        CharacterWindow.Resized += ApplyManagementFormation;
        BackpackWindow.Resized += ApplyManagementFormation;

        Callable.From(ApplyFormation).CallDeferred();
    }

    /// <summary>
    /// Disconnects every event owned by this controller.
    /// </summary>
    public override void _ExitTree()
    {
        if (GodotObject.IsInstanceValid(WindowHost))
            WindowHost.WindowPlacementApplied -= ApplyFormation;

        if (GodotObject.IsInstanceValid(CharacterWindow))
            CharacterWindow.Resized -= ApplyManagementFormation;

        if (GodotObject.IsInstanceValid(BackpackWindow))
            BackpackWindow.Resized -= ApplyManagementFormation;

    }

    /// <summary>
    /// Restores any managed native window moved away from its formation anchor.
    /// Native title-bar dragging may visually move a window while Windows owns
    /// the drag loop, but releasing it immediately returns it to this position.
    /// </summary>
    public override void _Process(double delta)
    {
        if (ReferencesAreUsable()
            && (_lastCharacterVisible != CharacterWindow.Visible
                || _lastBackpackVisible != BackpackWindow.Visible))
        {
            ApplyManagementFormation();
        }

        if (!LockWindowPositions
            || !_anchorsInitialized
            || !ReferencesAreUsable())
        {
            return;
        }

        if (FormationHost.Position != _formationHostAnchor)
            FormationHost.Position = _formationHostAnchor;

        if (CharacterWindow.Position != _characterAnchor)
            CharacterWindow.Position = _characterAnchor;

        if (BackpackWindow.Position != _backpackAnchor)
            BackpackWindow.Position = _backpackAnchor;

        if (IncapacitationWindow.Position != _incapacitationAnchor)
            IncapacitationWindow.Position = _incapacitationAnchor;
    }

    /// <summary>
    /// Repositions every managed window from the main Questbar's current
    /// screen rectangle. Hidden windows are positioned too, so they open in
    /// the correct place without a one-frame jump.
    /// </summary>
    public void ApplyFormation()
    {
        if (!ValidateReferences(logErrors: false))
            return;

        ApplyManagementFormation();
        AnchorIncapacitationWindow();
    }

    /// <summary>
    /// Right-aligns Character above Questbar, then holds Backpack's right edge
    /// beside Character. Backpack width changes therefore grow toward the left.
    /// </summary>
    public void ApplyManagementFormation()
    {
        if (!ReferencesAreUsable())
            return;

        Window questbarWindow = WindowHost.GetWindow();
        Rect2I usableArea = GetHostUsableArea(questbarWindow);

        Vector2I characterSize = new(
            Mathf.RoundToInt(CharacterWindow.Size.X),
            Mathf.RoundToInt(CharacterWindow.Size.Y));

        Vector2I backpackSize = new(
            Mathf.RoundToInt(BackpackWindow.Size.X),
            Mathf.RoundToInt(BackpackWindow.Size.Y));

        int characterX =
            questbarWindow.Position.X
            + questbarWindow.Size.X
            - characterSize.X
            + ManagementOffset.X;

        int characterY =
            questbarWindow.Position.Y
            - characterSize.Y
            - QuestbarGap
            + ManagementOffset.Y;

        Vector2I characterPosition = ClampToArea(
            new Vector2I(characterX, characterY),
            characterSize,
            usableArea);

        int backpackX =
            characterPosition.X
            - WindowGap
            - backpackSize.X;

        Vector2I backpackPosition = ClampToArea(
            new Vector2I(backpackX, characterPosition.Y),
            backpackSize,
            usableArea);

        _lastCharacterVisible = CharacterWindow.Visible;
        _lastBackpackVisible = BackpackWindow.Visible;

        bool characterParticipates = CharacterWindow.Visible;
        bool backpackParticipates = BackpackWindow.Visible;

        if (!characterParticipates && !backpackParticipates)
        {
            FormationHost.Hide();
            return;
        }

        Rect2I visibleBounds = characterParticipates
            ? new Rect2I(characterPosition, characterSize)
            : new Rect2I(backpackPosition, backpackSize);

        if (characterParticipates && backpackParticipates)
        {
            visibleBounds = visibleBounds.Merge(
                new Rect2I(backpackPosition, backpackSize));
        }

        _formationHostAnchor = visibleBounds.Position;
        FormationHost.CurrentScreen = questbarWindow.CurrentScreen;
        FormationHost.Position = _formationHostAnchor;
        FormationHost.Size = visibleBounds.Size;

        _characterAnchor = characterPosition - _formationHostAnchor;
        _backpackAnchor = backpackPosition - _formationHostAnchor;

        CharacterWindow.Position = _characterAnchor;
        BackpackWindow.Position = _backpackAnchor;

        if (!FormationHost.Visible)
            FormationHost.Show();

        _anchorsInitialized = true;
    }

    /// <summary>
    /// Centers the critical hero-choice window directly above Questbar.
    /// Call this after Popup() because PopupCentered() replaces explicit
    /// positioning with screen-centered placement.
    /// </summary>
    public void AnchorIncapacitationWindow()
    {
        if (!ReferencesAreUsable())
            return;

        Window questbarWindow = WindowHost.GetWindow();
        Rect2I usableArea = GetHostUsableArea(questbarWindow);

        int popupX =
            questbarWindow.Position.X
            + ((questbarWindow.Size.X - IncapacitationWindow.Size.X) / 2)
            + IncapacitationOffset.X;

        int popupY =
            questbarWindow.Position.Y
            - IncapacitationWindow.Size.Y
            - QuestbarGap
            + IncapacitationOffset.Y;

        _incapacitationAnchor = ClampToArea(
            new Vector2I(popupX, popupY),
            IncapacitationWindow.Size,
            usableArea);

        IncapacitationWindow.CurrentScreen = questbarWindow.CurrentScreen;
        IncapacitationWindow.Position = _incapacitationAnchor;
    }

    private static Rect2I GetHostUsableArea(Window questbarWindow)
    {
        int screenCount = DisplayServer.GetScreenCount();
        int screen = Mathf.Clamp(
            questbarWindow.CurrentScreen,
            0,
            Mathf.Max(0, screenCount - 1));

        return DisplayServer.ScreenGetUsableRect(screen);
    }

    private static Vector2I ClampToArea(
        Vector2I requestedPosition,
        Vector2I windowSize,
        Rect2I area)
    {
        int maximumX = Mathf.Max(
            area.Position.X,
            area.End.X - windowSize.X);

        int maximumY = Mathf.Max(
            area.Position.Y,
            area.End.Y - windowSize.Y);

        return new Vector2I(
            Mathf.Clamp(requestedPosition.X, area.Position.X, maximumX),
            Mathf.Clamp(requestedPosition.Y, area.Position.Y, maximumY));
    }

    private bool ReferencesAreUsable()
    {
        return GodotObject.IsInstanceValid(WindowHost)
            && GodotObject.IsInstanceValid(FormationHost)
            && GodotObject.IsInstanceValid(CharacterWindow)
            && GodotObject.IsInstanceValid(BackpackWindow)
            && GodotObject.IsInstanceValid(ItemTooltipPanel)
            && GodotObject.IsInstanceValid(IncapacitationWindow);
    }

    private bool ValidateReferences(bool logErrors = true)
    {
        bool valid = true;
        valid &= Require(WindowHost, nameof(WindowHost), logErrors);
        valid &= Require(FormationHost, nameof(FormationHost), logErrors);
        valid &= Require(CharacterWindow, nameof(CharacterWindow), logErrors);
        valid &= Require(BackpackWindow, nameof(BackpackWindow), logErrors);
        valid &= Require(ItemTooltipPanel, nameof(ItemTooltipPanel), logErrors);
        valid &= Require(
            IncapacitationWindow,
            nameof(IncapacitationWindow),
            logErrors);
        return valid;
    }

    /// <summary>
    /// Makes the Character host transparent and frame-free. Only the custom
    /// Character panel and its authored title bar remain visible.
    /// </summary>
    private void ConfigureFormationHost()
    {
        FormationHost.Hide();
        FormationHost.ForceNative = true;
        FormationHost.Borderless = true;
        FormationHost.Transparent = true;
        FormationHost.TransparentBg = true;
        FormationHost.Unresizable = true;
        FormationHost.MinimizeDisabled = true;
        FormationHost.MaximizeDisabled = true;
        // The tooltip window already owns AlwaysOnTop. Keeping this host as a
        // normal transient lets tooltips reliably rise above Character.
        FormationHost.AlwaysOnTop = false;
        FormationHost.Transient = true;
    }

    /// <summary>
    /// Reparents the existing Character and Backpack Controls after exported
    /// scene references resolve. Their internal controller assignments remain
    /// valid while both panels begin sharing one draw and drag hierarchy.
    /// </summary>
    private void HostManagementPanels()
    {
        bool characterWasVisible = CharacterWindow.Visible;
        bool backpackWasVisible = BackpackWindow.Visible;

        CharacterWindow.Hide();
        BackpackWindow.Hide();
        ItemTooltipPanel.Hide();

        CharacterWindow.Reparent(FormationHost, false);
        BackpackWindow.Reparent(FormationHost, false);
        ItemTooltipPanel.Reparent(FormationHost, false);

        CharacterWindow.Position = Vector2.Zero;
        BackpackWindow.Position = Vector2.Zero;
        ItemTooltipPanel.Position = Vector2.Zero;

        CharacterWindow.Visible = characterWasVisible;
        BackpackWindow.Visible = backpackWasVisible;
    }

    private static bool Require(
        GodotObject value,
        string propertyName,
        bool logErrors)
    {
        if (GodotObject.IsInstanceValid(value))
            return true;

        if (logErrors)
        {
            GD.PushError(
                $"PopupWindowFormationController is missing the " +
                $"Inspector reference '{propertyName}'.");
        }

        return false;
    }
}
