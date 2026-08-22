using Godot;

public partial class FloatingCombatTextController : Node
{
    private const string CombatTextAnchorName = "CombatTextAnchor";

    [ExportCategory("Dependencies")]
    [Export]
    public CombatController Combat { get; set; } = null!;

    [Export]
    public Node2D TextLayer { get; set; } = null!;

    [ExportCategory("Typography")]
    /// <summary>
    /// Optional custom combat font. Leave empty to use Godot's default font.
    /// A future FontFile can be assigned here without changing combat code.
    /// </summary>
    [Export]
    public Font? CombatFont { get; set; }

    [Export(PropertyHint.Range, "8,64,1")]
    public int FontSize { get; set; } = 20;

    [Export(PropertyHint.Range, "0,10,1")]
    public int OutlineSize { get; set; } = 3;

    [Export]
    public Color OutlineColor { get; set; } =
        new(0.04f, 0.04f, 0.05f, 0.95f);

    [ExportCategory("Colors")]
    [Export]
    public Color DamageColor { get; set; } =
        new(1.0f, 0.16f, 0.16f, 1.0f);

    [Export]
    public Color HealingColor { get; set; } =
        new(0.22f, 1.0f, 0.35f, 1.0f);

    [ExportCategory("Motion")]
    [Export(PropertyHint.Range, "0.1,3,0.05")]
    public float LifetimeSeconds { get; set; } = 0.9f;

    [Export(PropertyHint.Range, "0,120,1")]
    public float FloatDistance { get; set; } = 36.0f;

    [Export(PropertyHint.Range, "0,60,1")]
    public float HorizontalJitter { get; set; } = 10.0f;

    [Export(PropertyHint.Range, "0.05,2,0.05")]
    public float FadeDurationSeconds { get; set; } = 0.35f;

    [ExportCategory("Layout")]
    [Export(PropertyHint.Range, "24,240,1")]
    public float LabelWidth { get; set; } = 100.0f;

    [Export(PropertyHint.Range, "0,160,1")]
    public float FallbackHeightAboveActor { get; set; } = 64.0f;

    private readonly RandomNumberGenerator _random = new();

    public override void _Ready()
    {
        if (!ValidateReferences())
            return;

        _random.Randomize();
        Combat.CombatEventOccurred += OnCombatEventOccurred;
    }

    public override void _ExitTree()
    {
        if (GodotObject.IsInstanceValid(Combat))
            Combat.CombatEventOccurred -= OnCombatEventOccurred;
    }

    private void OnCombatEventOccurred(CombatEvent combatEvent)
    {
        switch (combatEvent.Type)
        {
            case CombatEventType.DamageApplied:
                ShowNumber(
                    combatEvent.Target,
                    combatEvent.Damage.AppliedDamage,
                    DamageColor,
                    false);
                break;

            case CombatEventType.HealingApplied:
                ShowNumber(
                    combatEvent.Target,
                    combatEvent.AppliedHealing,
                    HealingColor,
                    true);
                break;
        }
    }

    private void ShowNumber(
        Node target,
        float amount,
        Color color,
        bool showPositiveSign)
    {
        if (!float.IsFinite(amount)
            || amount <= 0.0f
            || target is not Node2D targetActor
            || !GodotObject.IsInstanceValid(targetActor)
            || !targetActor.IsInsideTree()
            || !GodotObject.IsInstanceValid(TextLayer))
        {
            return;
        }

        int roundedAmount = Mathf.RoundToInt(amount);

        if (roundedAmount <= 0)
            return;

        Label label = CreateLabel(
            showPositiveSign
                ? $"+{roundedAmount}"
                : roundedAmount.ToString(),
            color);

        TextLayer.AddChild(label);

        Vector2 anchorPosition = GetLocalAnchorPosition(targetActor);
        float horizontalOffset = _random.RandfRange(
            -Mathf.Max(HorizontalJitter, 0.0f),
            Mathf.Max(HorizontalJitter, 0.0f));

        Vector2 startPosition = anchorPosition
            + new Vector2(horizontalOffset - LabelWidth * 0.5f, 0.0f);

        label.Position = startPosition;
        label.PivotOffset = label.Size * 0.5f;
        label.Scale = new Vector2(0.8f, 0.8f);

        AnimateLabel(label, startPosition);
    }

    private Label CreateLabel(string text, Color color)
    {
        float height = Mathf.Max(FontSize + OutlineSize * 2 + 8, 24);

        Label label = new()
        {
            Text = text,
            Size = new Vector2(LabelWidth, height),
            CustomMinimumSize = new Vector2(LabelWidth, height),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            ZIndex = 100
        };

        label.AddThemeFontSizeOverride(
            "font_size",
            Mathf.Max(FontSize, 1));
        label.AddThemeConstantOverride(
            "outline_size",
            Mathf.Max(OutlineSize, 0));
        label.AddThemeColorOverride("font_color", color);
        label.AddThemeColorOverride(
            "font_outline_color",
            OutlineColor);

        if (GodotObject.IsInstanceValid(CombatFont))
            label.AddThemeFontOverride("font", CombatFont!);

        return label;
    }

    private Vector2 GetLocalAnchorPosition(Node2D targetActor)
    {
        Node2D? authoredAnchor = targetActor.FindChild(
            CombatTextAnchorName,
            true,
            false) as Node2D;

        Vector2 globalAnchor = GodotObject.IsInstanceValid(authoredAnchor)
            ? authoredAnchor!.GlobalPosition
            : targetActor.GlobalPosition
                + Vector2.Up * Mathf.Max(FallbackHeightAboveActor, 0.0f);

        return TextLayer.ToLocal(globalAnchor);
    }

    private void AnimateLabel(Label label, Vector2 startPosition)
    {
        float lifetime = Mathf.Max(LifetimeSeconds, 0.1f);
        float fadeDuration = Mathf.Clamp(
            FadeDurationSeconds,
            0.05f,
            lifetime);

        Tween tween = CreateTween()
            .SetParallel(true);

        tween.TweenProperty(
                label,
                "position",
                startPosition + Vector2.Up * Mathf.Max(FloatDistance, 0.0f),
                lifetime)
            .SetTrans(Tween.TransitionType.Quad)
            .SetEase(Tween.EaseType.Out);

        tween.TweenProperty(
                label,
                "scale",
                Vector2.One,
                Mathf.Min(0.16f, lifetime))
            .SetTrans(Tween.TransitionType.Back)
            .SetEase(Tween.EaseType.Out);

        tween.TweenProperty(
                label,
                "modulate:a",
                0.0f,
                fadeDuration)
            .SetDelay(lifetime - fadeDuration)
            .SetEase(Tween.EaseType.In);

        tween.Finished +=
            () =>
            {
                if (GodotObject.IsInstanceValid(label))
                    label.QueueFree();
            };
    }

    private bool ValidateReferences()
    {
        bool valid = true;

        if (!GodotObject.IsInstanceValid(Combat))
        {
            GD.PushError(
                "FloatingCombatTextController is missing its " +
                "Combat Inspector reference.");
            valid = false;
        }

        if (!GodotObject.IsInstanceValid(TextLayer))
        {
            GD.PushError(
                "FloatingCombatTextController is missing its " +
                "TextLayer Inspector reference.");
            valid = false;
        }

        return valid;
    }
}
