using Godot;

/// <summary>
/// Keeps Questbar's utility windows in a stable formation around the main
/// desktop window. Backpack, Character, and Map are hosted as locked custom
/// Control panels in one native host while critical modals remain independent.
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
    /// Owns travel and encounter state so global encounter testing remains
    /// available while the native management host has keyboard focus.
    /// </summary>
    [Export]
    public JourneyStateService JourneyState { get; set; } = null!;

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
    /// Region map anchored immediately to the right of the Character window.
    /// </summary>
    [Export]
    public Control MapWindow { get; set; } = null!;

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
    /// Empty horizontal space between Character and Map.
    /// </summary>
    [Export(PropertyHint.Range, "0,64,1")]
    public int MapWindowGap { get; set; } = 8;

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
    /// Additional Inspector-tunable offset applied only to Map after it is
    /// anchored to Character's right edge.
    /// </summary>
    [Export]
    public Vector2I MapWindowOffset { get; set; } = Vector2I.Zero;

    /// <summary>
    /// Additional Inspector-tunable offset for the incapacitation modal.
    /// </summary>
    [Export]
    public Vector2I IncapacitationOffset { get; set; } = Vector2I.Zero;

    /// <summary>
    /// Maximum number of four-slot columns that Backpack may add beside its
    /// authored 4x4 storage grid. Reserved invisibly by the native host so bag
    /// changes never reposition Character.
    /// </summary>
    [Export(PropertyHint.Range, "0,8,1")]
    public int ReservedBackpackExpansionColumns { get; set; } = 4;

    [ExportCategory("Formation Lock")]

    /// <summary>
    /// Keeps managed windows on their assigned anchors if the operating system
    /// or player attempts to move them. Disable only while tuning positions.
    /// </summary>
    [Export]
    public bool LockWindowPositions { get; set; } = true;

    private Vector2 _characterAnchor;
    private Vector2 _backpackAnchor;
    private Vector2 _mapAnchor;
    private Vector2I _formationHostAnchor;
    private Vector2I _incapacitationAnchor;
    private bool _anchorsInitialized;
    private bool _lastCharacterVisible;
    private bool _lastBackpackVisible;
    private bool _lastMapVisible;
    private bool _restoreBackpackOnExpand;
    private bool _restoreMapOnExpand;
    private bool _restoreIncapacitationOnExpand;
    private Rect2I? _lastMouseInputRegion;

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
        WindowHost.ExpandedChanged += OnExpandedChanged;
        CharacterWindow.Resized += ApplyManagementFormation;
        BackpackWindow.Resized += ApplyManagementFormation;
        MapWindow.Resized += ApplyManagementFormation;
        CharacterWindow.VisibilityChanged += ApplyManagementFormation;
        BackpackWindow.VisibilityChanged += ApplyManagementFormation;
        MapWindow.VisibilityChanged += ApplyManagementFormation;
        FormationHost.WindowInput += OnFormationHostWindowInput;
        IncapacitationWindow.WindowInput += OnIncapacitationWindowInput;

        Callable.From(ApplyFormation).CallDeferred();
    }

    /// <summary>
    /// Disconnects every event owned by this controller.
    /// </summary>
    public override void _ExitTree()
    {
        if (GodotObject.IsInstanceValid(WindowHost))
        {
            WindowHost.WindowPlacementApplied -= ApplyFormation;
            WindowHost.ExpandedChanged -= OnExpandedChanged;
        }

        if (GodotObject.IsInstanceValid(CharacterWindow))
        {
            CharacterWindow.Resized -= ApplyManagementFormation;
            CharacterWindow.VisibilityChanged -= ApplyManagementFormation;
        }

        if (GodotObject.IsInstanceValid(BackpackWindow))
        {
            BackpackWindow.Resized -= ApplyManagementFormation;
            BackpackWindow.VisibilityChanged -= ApplyManagementFormation;
        }

        if (GodotObject.IsInstanceValid(MapWindow))
        {
            MapWindow.Resized -= ApplyManagementFormation;
            MapWindow.VisibilityChanged -= ApplyManagementFormation;
        }

        if (GodotObject.IsInstanceValid(FormationHost))
            FormationHost.WindowInput -= OnFormationHostWindowInput;

        if (GodotObject.IsInstanceValid(IncapacitationWindow))
            IncapacitationWindow.WindowInput -= OnIncapacitationWindowInput;

    }

    /// <summary>
    /// Provides temporary keyboard access to the management panels until the
    /// Questbar game-window buttons are authored.
    /// </summary>
    public override void _UnhandledKeyInput(InputEvent @event)
    {
        // DesktopWindowHostController owns Space while the main window has
        // focus. Handling it here too would expand and collapse in one press.
        if (@event is InputEventKey keyEvent
            && keyEvent.Keycode == Key.Space)
        {
            return;
        }

        TryHandleManagementShortcut(@event, GetViewport().GuiGetFocusOwner());
    }

    private void OnFormationHostWindowInput(InputEvent @event)
    {
        if (TryHandleManagementShortcut(
                @event,
                FormationHost.GuiGetFocusOwner()))
        {
            FormationHost.SetInputAsHandled();
        }
    }

    private void OnIncapacitationWindowInput(InputEvent @event)
    {
        if (TryHandleManagementShortcut(
                @event,
                IncapacitationWindow.GuiGetFocusOwner()))
        {
            IncapacitationWindow.SetInputAsHandled();
        }
    }

    private bool TryHandleManagementShortcut(
        InputEvent @event,
        Control? focusOwner)
    {
        if (@event is not InputEventKey keyEvent
            || !keyEvent.Pressed
            || keyEvent.Echo
            || keyEvent.CtrlPressed
            || keyEvent.AltPressed
            || keyEvent.ShiftPressed
            || focusOwner is LineEdit
            || focusOwner is TextEdit)
        {
            return false;
        }

        if (keyEvent.Keycode == Key.Space)
        {
            WindowHost.ToggleExpanded();
            return true;
        }

        if (keyEvent.Keycode == Key.E)
        {
            JourneyState.ToggleTestEncounter();
            return true;
        }

        Control? panel = keyEvent.Keycode switch
        {
            Key.I => BackpackWindow,
            Key.C => CharacterWindow,
            Key.M => MapWindow,
            _ => null
        };

        if (panel is null)
            return false;

        panel.Visible = !panel.Visible;
        ApplyManagementFormation();
        return true;
    }

    /// <summary>
    /// Opens Character, Backpack, and Map together when all are closed. If any
    /// management panel is open, closes the complete group in one action.
    /// </summary>
    public void ToggleManagementGroup()
    {
        bool anyPanelVisible =
            CharacterWindow.Visible
            || BackpackWindow.Visible
            || MapWindow.Visible;

        bool showGroup = !anyPanelVisible;
        CharacterWindow.Visible = showGroup;
        BackpackWindow.Visible = showGroup;
        MapWindow.Visible = showGroup;
        ItemTooltipPanel.Hide();

        ApplyManagementFormation();
    }

    /// <summary>
    /// Gives mouse clicks and Space identical popup behavior. Collapse stores
    /// the open panels; expansion restores them with Character always open.
    /// </summary>
    private void OnExpandedChanged(bool isExpanded)
    {
        if (!isExpanded)
        {
            _restoreBackpackOnExpand = BackpackWindow.Visible;
            _restoreMapOnExpand = MapWindow.Visible;
            _restoreIncapacitationOnExpand = IncapacitationWindow.Visible;

            CharacterWindow.Hide();
            BackpackWindow.Hide();
            MapWindow.Hide();
            ItemTooltipPanel.Hide();
            IncapacitationWindow.Hide();
            ApplyFormation();
            return;
        }

        CharacterWindow.Show();
        BackpackWindow.Visible = _restoreBackpackOnExpand;
        MapWindow.Visible = _restoreMapOnExpand;
        ItemTooltipPanel.Hide();

        if (_restoreIncapacitationOnExpand)
            IncapacitationWindow.Show();
        else
            IncapacitationWindow.Hide();

        ApplyFormation();
    }

    /// <summary>
    /// Restores any managed native window moved away from its formation anchor.
    /// Native title-bar dragging may visually move a window while Windows owns
    /// the drag loop, but releasing it immediately returns it to this position.
    /// </summary>
    public override void _Process(double delta)
    {
        UpdateMousePassthroughRegion();

        if (ReferencesAreUsable()
            && (_lastCharacterVisible != CharacterWindow.Visible
                || _lastBackpackVisible != BackpackWindow.Visible
                || _lastMapVisible != MapWindow.Visible))
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

        if (MapWindow.Position != _mapAnchor)
            MapWindow.Position = _mapAnchor;

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
    /// Right-aligns Character above Questbar, holds Backpack's right edge to
    /// Character's left, and anchors Map to Character's right. Backpack width
    /// changes therefore grow toward the left without moving either neighbor.
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

        Vector2I mapSize = new(
            Mathf.RoundToInt(MapWindow.Size.X),
            Mathf.RoundToInt(MapWindow.Size.Y));

        int reservedBackpackWidth = GetReservedBackpackWidth(
            backpackSize.X);

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

        int reservedBackpackX =
            characterPosition.X
            - WindowGap
            - reservedBackpackWidth;

        Vector2I backpackPosition = ClampToArea(
            new Vector2I(backpackX, characterPosition.Y),
            backpackSize,
            usableArea);

        Vector2I mapPosition = new(
            characterPosition.X
                + characterSize.X
                + MapWindowGap
                + MapWindowOffset.X,
            characterPosition.Y + MapWindowOffset.Y);

        _lastCharacterVisible = CharacterWindow.Visible;
        _lastBackpackVisible = BackpackWindow.Visible;
        _lastMapVisible = MapWindow.Visible;

        bool anyManagementPanelVisible =
            CharacterWindow.Visible
            || BackpackWindow.Visible
            || MapWindow.Visible;

        if (!anyManagementPanelVisible)
        {
            FormationHost.Hide();
            return;
        }

        // Reserve the complete management formation whenever any panel is
        // open. Toggling a panel then changes only its child visibility; the
        // native host and the remaining panel anchors stay stable.
        Rect2I visibleBounds = new Rect2I(
            characterPosition,
            characterSize).Merge(
                new Rect2I(
                    new Vector2I(reservedBackpackX, backpackPosition.Y),
                    new Vector2I(reservedBackpackWidth, backpackSize.Y))).Merge(
                new Rect2I(mapPosition, mapSize));

        _formationHostAnchor = visibleBounds.Position;
        FormationHost.CurrentScreen = questbarWindow.CurrentScreen;
        FormationHost.Position = _formationHostAnchor;
        FormationHost.Size = visibleBounds.Size;

        _characterAnchor = characterPosition - _formationHostAnchor;
        _backpackAnchor = backpackPosition - _formationHostAnchor;
        _mapAnchor = mapPosition - _formationHostAnchor;

        CharacterWindow.Position = _characterAnchor;
        BackpackWindow.Position = _backpackAnchor;
        MapWindow.Position = _mapAnchor;

        _lastMouseInputRegion = null;
        UpdateMousePassthroughRegion();

        if (!FormationHost.Visible)
            FormationHost.Show();

        _anchorsInitialized = true;
    }

    /// <summary>
    /// Restricts native mouse input to visible Questbar panels while allowing
    /// clicks in the host's reserved transparent space to reach desktop apps.
    /// During item dragging the full host remains active so previews and drop
    /// validation can cross freely between Backpack and Character.
    /// </summary>
    private void UpdateMousePassthroughRegion()
    {
        if (!ReferencesAreUsable() || !FormationHost.Visible)
            return;

        Rect2I inputRegion;
        if (FormationHost.GuiIsDragging())
        {
            inputRegion = new Rect2I(Vector2I.Zero, FormationHost.Size);
        }
        else
        {
            bool hasRegion = false;
            inputRegion = new Rect2I();

            IncludeVisibleControl(
                CharacterWindow,
                ref inputRegion,
                ref hasRegion);
            IncludeVisibleControl(
                BackpackWindow,
                ref inputRegion,
                ref hasRegion);
            IncludeVisibleControl(
                MapWindow,
                ref inputRegion,
                ref hasRegion);
            IncludeVisibleControl(
                ItemTooltipPanel,
                ref inputRegion,
                ref hasRegion);

            if (!hasRegion)
                return;
        }

        if (_lastMouseInputRegion.HasValue
            && _lastMouseInputRegion.Value == inputRegion)
        {
            return;
        }

        _lastMouseInputRegion = inputRegion;
        Vector2 topLeft = new(
            inputRegion.Position.X,
            inputRegion.Position.Y);
        Vector2 topRight = new(inputRegion.End.X, inputRegion.Position.Y);
        Vector2 bottomRight = new(inputRegion.End.X, inputRegion.End.Y);
        Vector2 bottomLeft = new(inputRegion.Position.X, inputRegion.End.Y);

        FormationHost.MousePassthroughPolygon = new[]
        {
            topLeft,
            topRight,
            bottomRight,
            bottomLeft
        };
    }

    private static void IncludeVisibleControl(
        Control control,
        ref Rect2I region,
        ref bool hasRegion)
    {
        if (!control.Visible)
            return;

        Rect2I controlRect = new(
            new Vector2I(
                Mathf.RoundToInt(control.Position.X),
                Mathf.RoundToInt(control.Position.Y)),
            new Vector2I(
                Mathf.RoundToInt(control.Size.X),
                Mathf.RoundToInt(control.Size.Y)));

        region = hasRegion ? region.Merge(controlRect) : controlRect;
        hasRegion = true;
    }

    /// <summary>
    /// Calculates Backpack's fully expanded horizontal footprint from its
    /// live generated columns and authored item-slot sizing.
    /// </summary>
    private int GetReservedBackpackWidth(int currentWidth)
    {
        HBoxContainer? expansionColumns =
            BackpackWindow.FindChild(
                "UpperExpansionColumns",
                true,
                false) as HBoxContainer;

        if (!GodotObject.IsInstanceValid(expansionColumns))
            return currentWidth;

        int currentColumnCount = expansionColumns!.GetChildCount();
        int missingColumnCount = Mathf.Max(
            0,
            ReservedBackpackExpansionColumns - currentColumnCount);

        if (missingColumnCount == 0)
            return currentWidth;

        ItemSlotView? sampleSlot = BackpackWindow.FindChild(
            "InventorySlot1",
            true,
            false) as ItemSlotView;

        int columnWidth = GodotObject.IsInstanceValid(sampleSlot)
            ? Mathf.CeilToInt(sampleSlot!.GetCombinedMinimumSize().X)
            : 52;

        int columnSeparation = expansionColumns.GetThemeConstant(
            "separation");

        // When no expansion column exists yet, account for the separation
        // that appears between the authored grid and the first new column.
        int firstColumnSeparation = currentColumnCount == 0
            && expansionColumns.GetParent() is Container upperStorageRow
                ? upperStorageRow.GetThemeConstant("separation")
                : 0;

        return currentWidth
            + firstColumnSeparation
            + missingColumnCount * columnWidth
            + Mathf.Max(0, missingColumnCount - (currentColumnCount == 0 ? 1 : 0))
                * columnSeparation;
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
            && GodotObject.IsInstanceValid(JourneyState)
            && GodotObject.IsInstanceValid(FormationHost)
            && GodotObject.IsInstanceValid(CharacterWindow)
            && GodotObject.IsInstanceValid(BackpackWindow)
            && GodotObject.IsInstanceValid(MapWindow)
            && GodotObject.IsInstanceValid(ItemTooltipPanel)
            && GodotObject.IsInstanceValid(IncapacitationWindow);
    }

    private bool ValidateReferences(bool logErrors = true)
    {
        bool valid = true;
        valid &= Require(WindowHost, nameof(WindowHost), logErrors);
        valid &= Require(JourneyState, nameof(JourneyState), logErrors);
        valid &= Require(FormationHost, nameof(FormationHost), logErrors);
        valid &= Require(CharacterWindow, nameof(CharacterWindow), logErrors);
        valid &= Require(BackpackWindow, nameof(BackpackWindow), logErrors);
        valid &= Require(MapWindow, nameof(MapWindow), logErrors);
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
    /// Reparents the existing Backpack, Character, and Map Controls after
    /// exported scene references resolve. Their internal assignments remain
    /// valid while all panels share one draw and input hierarchy.
    /// </summary>
    private void HostManagementPanels()
    {
        bool characterWasVisible = CharacterWindow.Visible;
        bool backpackWasVisible = BackpackWindow.Visible;
        bool mapWasVisible = MapWindow.Visible;

        CharacterWindow.Hide();
        BackpackWindow.Hide();
        MapWindow.Hide();
        ItemTooltipPanel.Hide();

        CharacterWindow.Reparent(FormationHost, false);
        BackpackWindow.Reparent(FormationHost, false);
        MapWindow.Reparent(FormationHost, false);
        ItemTooltipPanel.Reparent(FormationHost, false);

        CharacterWindow.Position = Vector2.Zero;
        BackpackWindow.Position = Vector2.Zero;
        MapWindow.Position = Vector2.Zero;
        ItemTooltipPanel.Position = Vector2.Zero;

        CharacterWindow.Visible = characterWasVisible;
        BackpackWindow.Visible = backpackWasVisible;
        MapWindow.Visible = mapWasVisible;
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
