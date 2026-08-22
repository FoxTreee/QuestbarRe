using System;
using Godot;

public partial class ItemSlotView :
    PanelContainer
{
    public event Action<ItemSlotView>? HoverStarted;
    public event Action<ItemSlotView>? HoverEnded;
    /*
     * These keys identify values inside the drag payload.
     */
    public const string DragKeySourcePurpose =
        "source_purpose";

    public const string DragKeySourceContent =
        "source_content";

    public const string DragKeySourceSlotIndex =
        "source_slot_index";

    public const string DragKeyItemId =
        "item_id";

    public const string DragKeyHasStackId =
        "has_stack_id";

    public const string DragKeyStackId =
        "stack_id";

    public const string DragKeyHasUniqueInstanceId =
        "has_unique_instance_id";

    public const string DragKeyUniqueInstanceId =
        "unique_instance_id";

    public enum SlotPurpose
    {
        Storage,
        BagEquipment,
        CharacterEquipment
    }

    public enum SlotContent
    {
        Empty,
        StackableItem,
        UniqueItem
    }

    public event Action<
        ItemSlotView,
        SlotPurpose,
        SlotContent,
        int,
        string,
        long?,
        long?>?
        DropRequested;

    public event Action<
        ItemSlotView,
        SlotPurpose,
        SlotContent,
        int,
        string,
        int,
        long?,
        long?>?
        DeleteDropRequested;

    /*
     * Raised by Shift+Left Click on a stack containing at least
     * two items.
     */
    public event Action<
        ItemSlotView,
        long>?
        SplitRequested;

    /// <summary>
    /// Raised by right-clicking a non-empty slot. The owning controller
    /// resolves the current authoritative item before presenting any action.
    /// </summary>
    public event Action<ItemSlotView>?
        ContextActionsRequested;

    /*
     * The controller can assign a destination-specific validator.
     *
     * Storage slots use the built-in storage rules. Bag equipment
     * slots use a validator that checks ItemDefinition.EquipSlot.
     */
    public Func<
        ItemSlotView,
        SlotPurpose,
        SlotContent,
        int,
        string,
        long?,
        long?,
        bool>?
        DropValidator
    { get; set; }

    // ---------------------------------------------------------
    // Drag configuration
    // ---------------------------------------------------------

    [Export(PropertyHint.Range, "0.15,1.0,0.05")]
    public double DragHoldSeconds { get; set; } =
        0.35;

    [Export(PropertyHint.Range, "1,30,1")]
    public float HoldMovementTolerance { get; set; } =
        8.0f;

    [Export]
    public Vector2 DragPreviewSize { get; set; } =
        new Vector2(
            40.0f,
            40.0f
        );

    [Export]
    public Color ValidDropHighlightColor { get; set; } =
        new Color(
            1.0f,
            0.92f,
            0.45f,
            1.0f
        );

    /// <summary>
    /// Tint applied to the dragged item while it is not above a destination
    /// that can accept it.
    /// </summary>
    [Export]
    public Color InvalidDragTintColor { get; set; } =
        new Color(1.0f, 0.3f, 0.3f, 1.0f);

    /// <summary>
    /// Optional artwork shown while this slot is empty.
    /// Character equipment slots will eventually receive these textures from
    /// a shared visual catalog rather than owning copied artwork.
    /// </summary>
    [Export]
    public Texture2D? EmptySlotTexture { get; set; }

    /// <summary>
    /// Shared catalog used to resolve empty-slot artwork by equipment position.
    /// </summary>
    [Export]
    public ItemSlotVisualCatalog? VisualCatalog { get; set; }

    // ---------------------------------------------------------
    // Startup configuration
    // ---------------------------------------------------------

    /// <summary>
    /// Optional authored startup purpose for scene instances.
    /// Character-window slots can identify themselves before a controller
    /// exists; inventory/bag slots may remain Storage.
    /// </summary>
    [Export]
    public SlotPurpose InitialPurpose { get; set; } =
        SlotPurpose.Storage;

    /// <summary>
    /// Equipment position used when InitialPurpose is CharacterEquipment.
    /// This is ignored for Storage and BagEquipment slots.
    /// </summary>
    [Export]
    public EquipmentSlot InitialCharacterEquipmentSlot { get; set; } =
        EquipmentSlot.Head;

    /// <summary>
    /// Optional authored index for non-character slots.
    /// Character equipment derives its index from EquipmentSlot.
    /// </summary>
    [Export]
    public int InitialSlotIndex { get; set; } =
        -1;

    // ---------------------------------------------------------
    // Slot identity
    // ---------------------------------------------------------

    public SlotPurpose Purpose { get; private set; } =
        SlotPurpose.Storage;

    public SlotContent Content { get; private set; } =
        SlotContent.Empty;

    public int SlotIndex { get; private set; } =
        -1;

    public string ItemId { get; private set; } =
        "";

    public int Quantity { get; private set; }

    public long? StackId { get; private set; }

    public long? UniqueInstanceId { get; private set; }

    public bool IsEmpty =>
        Content == SlotContent.Empty;

    /// <summary>
    /// True when this slot represents one of the hero's authored equipment
    /// positions rather than ordinary storage.
    /// </summary>
    public bool HasCharacterEquipmentSlot { get; private set; }

    public EquipmentSlot CharacterEquipmentSlot { get; private set; }

    /// <summary>
    /// Current item artwork displayed by the slot.
    /// The owning inventory/equipment controller supplies this texture.
    /// </summary>
    public Texture2D? ItemTexture { get; private set; }

    /*
     * Permanent equipment can disable drag initiation while
     * retaining all normal slot presentation behavior.
     */
    public bool DragEnabled { get; set; } =
        true;

    // ---------------------------------------------------------
    // Runtime state
    // ---------------------------------------------------------

    private bool _pressActive;

    private double _pressDurationSeconds;

    private Vector2 _pressStartPosition =
        Vector2.Zero;

    private Color _normalSelfModulate =
        Colors.White;

    private Tween?
        _snapTween;

    private bool _dragStartedByThisSlot;
    private CanvasItem? _dragPreviewIcon;
    private ItemSlotView? _highlightedDropTarget;

    private TextureRect? _emptySlotIcon;
    private TextureRect? _itemIcon;
    private Label? _quantityLabel;
    private static Texture2D? _forbiddenCursorTexture;

    // ---------------------------------------------------------
    // Godot lifecycle
    // ---------------------------------------------------------

    // Reconnects hover input whenever an existing slot is moved into another
    // viewport. The shared popup formation reparents the authored Backpack and
    // Character panels after their slots have already become ready.
    public override void _EnterTree()
    {
        MouseExited += OnMouseExited;
        MouseEntered += OnHoverEntered;
    }

    // Initializes the reusable item-slot visuals, drag state, and input events when this node becomes ready.
    public override void _Ready()
    {
        MouseFilter =
            MouseFilterEnum.Stop;

        InstallForbiddenArrowCursor();

        _emptySlotIcon =
            GetNodeOrNull<TextureRect>(
                "SlotMargin/SlotContents/EmptySlotIcon"
            );

        _itemIcon =
            GetNodeOrNull<TextureRect>(
                "SlotMargin/SlotContents/ItemIcon"
            );

        _quantityLabel =
            GetNodeOrNull<Label>(
                "SlotMargin/SlotContents/QuantityLabel"
            );

        _normalSelfModulate =
            SelfModulate;

        if (InitialPurpose ==
            SlotPurpose.CharacterEquipment)
        {
            ConfigureCharacterEquipmentSlot(
                InitialCharacterEquipmentSlot
            );
        }
        else
        {
            ConfigureSlot(
                InitialPurpose,
                InitialSlotIndex
            );
        }

        RefreshPresentation();

        SetProcess(
            false
        );
    }

    // Detaches event handlers and clears runtime references owned by the reusable item-slot visuals, drag state, and input events as the node leaves the scene tree.
    public override void _ExitTree()
    {
        MouseExited -=
            OnMouseExited;
        MouseEntered -= OnHoverEntered;
        _snapTween?.Kill();
        _snapTween =
            null;
    }

    private void OnHoverEntered()
    {
        HoverStarted?.Invoke(this);
    }

    // Responds to Godot lifecycle notifications that affect the reusable item-slot visuals, drag state, and input events.
    public override void _Notification(
        int what)
    {
        if (what !=
                NotificationDragEnd ||
            !_dragStartedByThisSlot)
        {
            return;
        }
        _dragStartedByThisSlot =
            false;

        ClearTrackedDropTarget();
        _dragPreviewIcon = null;

        Viewport viewport =
            GetViewport();
        if (viewport.GuiIsDragSuccessful())
        {
            return;
        }
        Vector2 mousePosition =
            viewport.GetMousePosition();
        Rect2 visibleRect =
            viewport.GetVisibleRect();
        if (visibleRect.HasPoint(
                mousePosition))
        {
            /*
             * An invalid drop made inside the owning item window
             * simply returns the item to its source slot.
             */
            return;
        }
        DeleteDropRequested?.Invoke(
            this,
            Purpose,
            Content,
            SlotIndex,
            ItemId,
            Quantity,
            StackId,
            UniqueInstanceId
        );
    }

    // Advances the active timed operation each frame and disables idle processing when no work remains.
    public override void _Process(
        double delta)
    {
        if (_dragStartedByThisSlot)
        {
            UpdateDragFeedback();
            return;
        }

        if (!_pressActive)
        {
            SetProcess(
                false
            );
            return;
        }
        if (!Input.IsMouseButtonPressed(
                MouseButton.Left))
        {
            CancelPendingDrag();
            return;
        }
        if (IsEmpty ||
            !DragEnabled)
        {
            CancelPendingDrag();
            return;
        }
        _pressDurationSeconds +=
            delta;
        if (_pressDurationSeconds <
            DragHoldSeconds)
        {
            return;
        }
        BeginDrag();
    }

    // Handles pointer input for the slot, including pending drag detection, cancellation, and drag startup.
    public override void _GuiInput(
        InputEvent inputEvent)
    {
        if (inputEvent is InputEventMouseButton contextClick
            && contextClick.ButtonIndex == MouseButton.Right
            && contextClick.Pressed)
        {
            CancelPendingDrag();

            if (!IsEmpty)
            {
                ContextActionsRequested?.Invoke(this);
                AcceptEvent();
            }

            return;
        }

        if (inputEvent is
            InputEventMouseButton mouseButton &&
            mouseButton.ButtonIndex ==
            MouseButton.Left)
        {
            if (mouseButton.Pressed)
            {
                if (mouseButton.ShiftPressed &&
                    Purpose ==
                        SlotPurpose.Storage &&
                    Content ==
                        SlotContent.StackableItem &&
                    Quantity > 1 &&
                    StackId.HasValue)
                {
                    CancelPendingDrag();
                    SplitRequested?.Invoke(
                        this,
                        StackId.Value
                    );
                    AcceptEvent();
                    return;
                }
                BeginPendingDrag(
                    mouseButton.Position
                );
            }
            else
            {
                CancelPendingDrag();
            }
            return;
        }
        if (inputEvent is
                InputEventMouseMotion mouseMotion &&
            _pressActive)
        {
            float movementDistance =
                mouseMotion.Position.DistanceTo(
                    _pressStartPosition
                );
            if (movementDistance >
                HoldMovementTolerance)
            {
                CancelPendingDrag();
            }
        }
    }

    // Decodes the drag payload and reports whether this slot can accept the proposed item drop.
    public override bool _CanDropData(
        Vector2 atPosition,
        Variant data)
    {
        bool canAcceptDrop =
            TryReadDragData(
                data,
                out SlotPurpose sourcePurpose,
                out SlotContent sourceContent,
                out int sourceSlotIndex,
                out string itemId,
                out long? stackId,
                out long? uniqueInstanceId
            ) &&
            CanAcceptDrop(
                sourcePurpose,
                sourceContent,
                sourceSlotIndex,
                itemId,
                stackId,
                uniqueInstanceId
            );
        SetDropHighlight(
            canAcceptDrop
        );
        return canAcceptDrop;
    }

    // Decodes an accepted drag payload and raises the appropriate drop request without directly mutating inventory state.
    public override void _DropData(
        Vector2 atPosition,
        Variant data)
    {
        SetDropHighlight(
            false
        );
        if (!TryReadDragData(
                data,
                out SlotPurpose sourcePurpose,
                out SlotContent sourceContent,
                out int sourceSlotIndex,
                out string itemId,
                out long? stackId,
                out long? uniqueInstanceId))
        {
            return;
        }
        if (!CanAcceptDrop(
                sourcePurpose,
                sourceContent,
                sourceSlotIndex,
                itemId,
                stackId,
                uniqueInstanceId))
        {
            return;
        }
        DropRequested?.Invoke(
            this,
            sourcePurpose,
            sourceContent,
            sourceSlotIndex,
            itemId,
            stackId,
            uniqueInstanceId
        );
    }

    // ---------------------------------------------------------
    // Slot configuration
    // ---------------------------------------------------------

    // Configures slot for the reusable item-slot visuals, drag state, and input events.
    public void ConfigureSlot(
    SlotPurpose purpose,
    int slotIndex)
    {
        Purpose =
            purpose;
        SlotIndex =
            slotIndex;
        HasCharacterEquipmentSlot =
            false;

        if (VisualCatalog != null)
        {
            EmptySlotTexture =
                VisualCatalog.GetEmptySlotTexture(
                    purpose
                );
        }

        ClearItemIdentity();
    }

    /// <summary>
    /// Configures this reusable slot as a specific hero equipment position.
    /// The EquipmentSlot value is authoritative for destination validation;
    /// the integer slot index remains available for the shared drag payload.
    /// </summary>
    public void ConfigureCharacterEquipmentSlot(
        EquipmentSlot equipmentSlot)
    {
        Purpose =
            SlotPurpose.CharacterEquipment;
        CharacterEquipmentSlot =
            equipmentSlot;
        HasCharacterEquipmentSlot =
            true;
        SlotIndex =
            (int)equipmentSlot;

        if (VisualCatalog != null)
        {
            EmptySlotTexture =
                VisualCatalog.GetEmptySlotTexture(
                    equipmentSlot
                );
        }

        ClearItemIdentity();
    }

    // Sets stackable item identity.
    public void SetStackableItemIdentity(
        string itemId,
        long stackId,
        int quantity)
    {
        CancelPendingDrag();
        Content =
            SlotContent.StackableItem;
        ItemId =
            itemId;
        Quantity =
            Math.Max(
                1,
                quantity
            );
        StackId =
            stackId;
        UniqueInstanceId =
            null;
        RefreshPresentation();
    }

    // Sets unique item identity.
    public void SetUniqueItemIdentity(
        string itemId,
        long uniqueInstanceId)
    {
        CancelPendingDrag();
        Content =
            SlotContent.UniqueItem;
        ItemId =
            itemId;
        Quantity =
            1;
        StackId =
            null;
        UniqueInstanceId =
            uniqueInstanceId;
        RefreshPresentation();
    }

    // Clears item identity.
    public void ClearItemIdentity()
    {
        CancelPendingDrag();
        Content =
            SlotContent.Empty;
        ItemId =
            "";
        Quantity =
            0;
        StackId =
            null;
        UniqueInstanceId =
            null;
        ItemTexture =
            null;
        RefreshPresentation();
    }

    /// <summary>
    /// Assigns the artwork for the item currently represented by this slot.
    /// Item identity and authoritative inventory state remain owned elsewhere.
    /// </summary>
    public void SetItemTexture(
        Texture2D? texture)
    {
        ItemTexture =
            texture;
        RefreshPresentation();
    }

    /// <summary>
    /// Assigns the artwork shown when this slot is empty.
    /// A future shared slot-visual catalog can call this once per slot type.
    /// </summary>
    public void SetEmptySlotTexture(
        Texture2D? texture)
    {
        EmptySlotTexture =
            texture;
        RefreshPresentation();
    }

    /// <summary>
    /// Refreshes empty artwork, equipped item artwork, and stack quantity
    /// without changing authoritative inventory or equipment state.
    /// </summary>
    public void RefreshPresentation()
    {
        if (_emptySlotIcon != null)
        {
            _emptySlotIcon.Texture =
                EmptySlotTexture;
            _emptySlotIcon.Visible =
                IsEmpty &&
                EmptySlotTexture != null;
        }

        if (_itemIcon != null)
        {
            _itemIcon.Texture =
                ItemTexture;
            _itemIcon.Visible =
                !IsEmpty &&
                ItemTexture != null;
        }

        if (_quantityLabel != null)
        {
            bool showQuantity =
                Content ==
                    SlotContent.StackableItem &&
                Quantity > 1;

            _quantityLabel.Visible =
                showQuantity;

            _quantityLabel.Text =
                showQuantity
                    ? Quantity.ToString()
                    : "";
        }
    }

    // ---------------------------------------------------------
    // Drop animation
    // ---------------------------------------------------------

    // Plays snap animation.
    public void PlaySnapAnimation()
    {
        _snapTween?.Kill();
        PivotOffset =
            Size /
            2.0f;
        Scale =
            new Vector2(
                0.82f,
                0.82f
            );
        _snapTween =
            CreateTween();
        _snapTween.SetTrans(
            Tween.TransitionType.Back
        );
        _snapTween.SetEase(
            Tween.EaseType.Out
        );
        _snapTween.TweenProperty(
            this,
            "scale",
            new Vector2(
                1.06f,
                1.06f
            ),
            0.10
        );
        _snapTween.TweenProperty(
            this,
            "scale",
            Vector2.One,
            0.08
        );
    }

    // ---------------------------------------------------------
    // Drag creation
    // ---------------------------------------------------------

    // Begins pending drag.
    private void BeginPendingDrag(
        Vector2 mousePosition)
    {
        if (IsEmpty ||
            !DragEnabled)
        {
            return;
        }
        _pressActive =
            true;
        _pressDurationSeconds =
            0.0;
        _pressStartPosition =
            mousePosition;
        SetProcess(
            true
        );
    }

    // Checks whether cel pending drag is currently valid without applying the change.
    private void CancelPendingDrag()
    {
        _pressActive =
            false;
        _pressDurationSeconds =
            0.0;
        SetProcess(
            false
        );
    }

    // Begins drag.
    private void BeginDrag()
    {
        if (IsEmpty)
        {
            CancelPendingDrag();
            return;
        }
        Godot.Collections.Dictionary dragData =
            CreateDragData();
        Control dragPreview =
            CreateDragPreview();
        CancelPendingDrag();
        _dragStartedByThisSlot =
            true;

        HoverEnded?.Invoke(this);

        ForceDrag(
            dragData,
            dragPreview
        );
        SetProcess(true);
    }

    // Creates drag data for the reusable item-slot visuals, drag state, and input events.
    private Godot.Collections.Dictionary
        CreateDragData()
    {
        Godot.Collections.Dictionary dragData =
            new()
            {
                [
                    DragKeySourcePurpose
                ] = (int)Purpose,
                [
                    DragKeySourceContent
                ] = (int)Content,
                [
                    DragKeySourceSlotIndex
                ] = SlotIndex,
                [
                    DragKeyItemId
                ] = ItemId,
                [
                    DragKeyHasStackId
                ] = StackId.HasValue,
                [
                    DragKeyStackId
                ] = StackId ??
                    0L,
                [
                    DragKeyHasUniqueInstanceId
                ] = UniqueInstanceId.HasValue,
                [
                    DragKeyUniqueInstanceId
                ] = UniqueInstanceId ??
                    0L
            };
        return dragData;
    }

    // Creates drag preview for the reusable item-slot visuals, drag state, and input events.
    private Control CreateDragPreview()
    {
        TextureRect? currentIcon =
            GetNodeOrNull<TextureRect>(
                "SlotMargin/SlotContents/ItemIcon"
            );
        Control previewRoot =
            new()
            {
                MouseFilter =
                    MouseFilterEnum.Ignore,
                ZIndex = 4096,
                ZAsRelative = false
            };
        Texture2D? texture =
            currentIcon?.Texture;
        if (texture == null)
        {
            return previewRoot;
        }
        Vector2 textureSize =
            texture.GetSize();
        Vector2 availableSize =
            DragPreviewSize -
            new Vector2(
                4.0f,
                4.0f
            );
        float horizontalScale =
            availableSize.X /
            Mathf.Max(
                1.0f,
                textureSize.X
            );
        float verticalScale =
            availableSize.Y /
            Mathf.Max(
                1.0f,
                textureSize.Y
            );
        float previewScale =
            Mathf.Min(
                horizontalScale,
                verticalScale
            );
        Sprite2D previewIcon =
            new()
            {
                Texture =
                    texture,
                Centered =
                    true,
                Position =
                    Vector2.Zero,
                Scale =
                    new Vector2(
                        previewScale,
                        previewScale
                    ),
                TextureFilter =
                    currentIcon?.TextureFilter ??
                    CanvasItem.TextureFilterEnum.Nearest
            };
        previewIcon.Modulate = InvalidDragTintColor;
        _dragPreviewIcon = previewIcon;
        previewRoot.AddChild(
            previewIcon
        );
        return previewRoot;
    }

    /// <summary>
    /// Keeps the preview red over invalid space and preserves the highlight on
    /// the one item slot that can currently accept the dragged item.
    /// </summary>
    private void UpdateDragFeedback()
    {
        ItemSlotView? target = FindItemSlot(
            GetViewport().GuiGetHoveredControl());

        bool valid = target is not null && target.CanAcceptDrop(
            Purpose,
            Content,
            SlotIndex,
            ItemId,
            StackId,
            UniqueInstanceId);

        if (!ReferenceEquals(target, _highlightedDropTarget))
            ClearTrackedDropTarget();

        if (valid && target is not null)
        {
            _highlightedDropTarget = target;
            target.SetDropHighlight(true);
        }

        if (GodotObject.IsInstanceValid(_dragPreviewIcon))
            _dragPreviewIcon!.Modulate = valid
                ? Colors.White
                : InvalidDragTintColor;
    }

    private void ClearTrackedDropTarget()
    {
        if (GodotObject.IsInstanceValid(_highlightedDropTarget))
            _highlightedDropTarget!.SetDropHighlight(false);

        _highlightedDropTarget = null;
    }

    private static ItemSlotView? FindItemSlot(Control? control)
    {
        Node? current = control;
        while (current is not null)
        {
            if (current is ItemSlotView slot)
                return slot;

            current = current.GetParent();
        }

        return null;
    }

    /// <summary>
    /// Replaces only Godot's forbidden cursor artwork with an arrow. Godot may
    /// still change cursor state during drag validation, but valid and invalid
    /// states now look consistent while the item tint communicates validity.
    /// </summary>
    private static void InstallForbiddenArrowCursor()
    {
        if (_forbiddenCursorTexture is null)
        {
            string[] pixels =
            {
                "K...............",
                "KW..............",
                "KWW.............",
                "KWWW............",
                "KWWWW...........",
                "KWWWWW..........",
                "KWWWWWW.........",
                "KWWWWWWW........",
                "KWWWWWWWW.......",
                "KWWWWKKKKK......",
                "KWWWK...........",
                "KWWK.K..........",
                "KWK..K..........",
                "KK...K..........",
                "K....K..........",
                ".....K.........."
            };

            Image image = Image.CreateEmpty(
                16,
                pixels.Length,
                false,
                Image.Format.Rgba8);

            for (int y = 0; y < pixels.Length; y++)
            {
                for (int x = 0; x < pixels[y].Length; x++)
                {
                    image.SetPixel(
                        x,
                        y,
                        pixels[y][x] switch
                        {
                            'K' => Colors.Black,
                            'W' => Colors.White,
                            _ => Colors.Transparent
                        });
                }
            }

            _forbiddenCursorTexture = ImageTexture.CreateFromImage(image);
        }

        Input.SetCustomMouseCursor(
            _forbiddenCursorTexture,
            Input.CursorShape.Forbidden,
            Vector2.Zero);
    }

    // ---------------------------------------------------------
    // Drop validation
    // ---------------------------------------------------------

    // Checks whether accept drop is currently valid without applying the change.
    private bool CanAcceptDrop(
        SlotPurpose sourcePurpose,
        SlotContent sourceContent,
        int sourceSlotIndex,
        string itemId,
        long? stackId,
        long? uniqueInstanceId)
    {
        if (sourceSlotIndex < 0 ||
            sourceContent ==
                SlotContent.Empty ||
            string.IsNullOrWhiteSpace(
                itemId))
        {
            return false;
        }
        /*
         * Prevent dropping back onto the exact same logical slot.
         */
        if (sourcePurpose ==
                Purpose &&
            sourceSlotIndex ==
                SlotIndex)
        {
            return false;
        }
        if (sourceContent ==
                SlotContent.StackableItem &&
            !stackId.HasValue)
        {
            return false;
        }
        if (sourceContent ==
                SlotContent.UniqueItem &&
            !uniqueInstanceId.HasValue)
        {
            return false;
        }
        /*
         * Equipment destinations need ItemDefinition-aware rules,
         * so the controller supplies their validator.
         */
        if (DropValidator != null)
        {
            return DropValidator(
                this,
                sourcePurpose,
                sourceContent,
                sourceSlotIndex,
                itemId,
                stackId,
                uniqueInstanceId
            );
        }
        /*
         * Default behavior remains storage-to-storage movement.
         */
        return
            IsEmpty &&
            Purpose ==
                SlotPurpose.Storage &&
            sourcePurpose ==
                SlotPurpose.Storage;
    }

    // Attempts to read drag data; returns whether validation succeeded and writes a diagnostic message to the output parameter.
    private static bool TryReadDragData(
        Variant data,
        out SlotPurpose sourcePurpose,
        out SlotContent sourceContent,
        out int sourceSlotIndex,
        out string itemId,
        out long? stackId,
        out long? uniqueInstanceId)
    {
        sourcePurpose =
            SlotPurpose.Storage;
        sourceContent =
            SlotContent.Empty;
        sourceSlotIndex =
            -1;
        itemId =
            "";
        stackId =
            null;
        uniqueInstanceId =
            null;
        if (data.VariantType !=
            Variant.Type.Dictionary)
        {
            return false;
        }
        Godot.Collections.Dictionary dragData =
            data.AsGodotDictionary();
        if (!dragData.ContainsKey(
                DragKeySourcePurpose) ||
            !dragData.ContainsKey(
                DragKeySourceContent) ||
            !dragData.ContainsKey(
                DragKeySourceSlotIndex) ||
            !dragData.ContainsKey(
                DragKeyItemId) ||
            !dragData.ContainsKey(
                DragKeyHasStackId) ||
            !dragData.ContainsKey(
                DragKeyStackId) ||
            !dragData.ContainsKey(
                DragKeyHasUniqueInstanceId) ||
            !dragData.ContainsKey(
                DragKeyUniqueInstanceId))
        {
            return false;
        }
        int sourcePurposeValue =
            (int)dragData[
                DragKeySourcePurpose
            ];
        int sourceContentValue =
            (int)dragData[
                DragKeySourceContent
            ];
        if (!Enum.IsDefined(
                typeof(SlotPurpose),
                sourcePurposeValue) ||
            !Enum.IsDefined(
                typeof(SlotContent),
                sourceContentValue))
        {
            return false;
        }
        sourcePurpose =
            (SlotPurpose)sourcePurposeValue;
        sourceContent =
            (SlotContent)sourceContentValue;
        sourceSlotIndex =
            (int)dragData[
                DragKeySourceSlotIndex
            ];
        itemId =
            (string)dragData[
                DragKeyItemId
            ];
        bool hasStackId =
            (bool)dragData[
                DragKeyHasStackId
            ];
        if (hasStackId)
        {
            stackId =
                (long)dragData[
                    DragKeyStackId
                ];
        }
        bool hasUniqueInstanceId =
            (bool)dragData[
                DragKeyHasUniqueInstanceId
            ];
        if (hasUniqueInstanceId)
        {
            uniqueInstanceId =
                (long)dragData[
                    DragKeyUniqueInstanceId
                ];
        }
        return true;
    }

    // Sets drop highlight.
    private void SetDropHighlight(
        bool highlighted)
    {
        SelfModulate =
            highlighted
                ? ValidDropHighlightColor
                : _normalSelfModulate;
    }

    // Handles the mouse exited event and updates the related reusable item-slot visuals, drag state, and input events state.
    private void OnMouseExited()
    {
        SetDropHighlight(
            false
        );
        HoverEnded?.Invoke(this);
    }
}
