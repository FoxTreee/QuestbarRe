using Godot;
using System.Collections.Generic;

public partial class CombatStatusEffectDisplayController : Node2D
{
    private readonly Dictionary<string, Label> _labels = new();
    private CombatStatusEffectState? _statusEffects;

    [ExportCategory("Dependencies")]

    /// <summary>
    /// Actor that owns the runtime status state shown by this display. Assign
    /// the owning HeroActorController or MonsterActorController node.
    /// </summary>
    [Export]
    public Node StatusOwner { get; set; } = null!;

    /// <summary>
    /// Container that receives one compact label per active status effect.
    /// </summary>
    [Export]
    public HBoxContainer StatusContainer { get; set; } = null!;

    public override void _Ready()
    {
        Visible = false;
        DebugPresentationSettings.StatusEffectTimersVisibilityChanged +=
            OnStatusEffectTimersVisibilityChanged;

        if (!GodotObject.IsInstanceValid(StatusContainer))
        {
            GD.PushError(
                $"{Name} requires its StatusContainer Inspector reference.");
            return;
        }

        if (StatusOwner is not ICombatStatusEffectOwner owner)
        {
            GD.PushError(
                $"{Name} requires StatusOwner to implement " +
                $"{nameof(ICombatStatusEffectOwner)}.");
            return;
        }

        Bind(owner.StatusEffects);
    }

    public override void _ExitTree()
    {
        DebugPresentationSettings.StatusEffectTimersVisibilityChanged -=
            OnStatusEffectTimersVisibilityChanged;
        Unbind();
    }

    public override void _Process(double delta)
    {
        if (!DebugPresentationSettings.StatusEffectTimersVisible
            || _statusEffects is null
            || _labels.Count == 0)
        {
            return;
        }

        foreach (CombatStatusEffectInstance effect
            in _statusEffects.ActiveEffects)
        {
            if (_labels.TryGetValue(
                Normalize(effect.ContentId),
                out Label? label))
            {
                RefreshLabel(label, effect);
            }
        }
    }

    private void Bind(CombatStatusEffectState statusEffects)
    {
        Unbind();

        _statusEffects = statusEffects;
        _statusEffects.EffectApplied += OnEffectApplied;
        _statusEffects.EffectRefreshed += OnEffectRefreshed;
        _statusEffects.EffectExpired += OnEffectExpired;
        _statusEffects.EffectRemoved += OnEffectRemoved;
        _statusEffects.EffectsCleared += OnEffectsCleared;

        foreach (CombatStatusEffectInstance effect
            in _statusEffects.ActiveEffects)
        {
            AddOrRefresh(effect);
        }

        RefreshVisibility();
    }

    private void Unbind()
    {
        if (_statusEffects is not null)
        {
            _statusEffects.EffectApplied -= OnEffectApplied;
            _statusEffects.EffectRefreshed -= OnEffectRefreshed;
            _statusEffects.EffectExpired -= OnEffectExpired;
            _statusEffects.EffectRemoved -= OnEffectRemoved;
            _statusEffects.EffectsCleared -= OnEffectsCleared;
        }

        _statusEffects = null;
        ClearLabels();
    }

    private void OnStatusEffectTimersVisibilityChanged(bool visible)
    {
        RefreshVisibility();
    }

    private void OnEffectApplied(CombatStatusEffectInstance effect)
    {
        AddOrRefresh(effect);
    }

    private void OnEffectRefreshed(CombatStatusEffectInstance effect)
    {
        AddOrRefresh(effect);
    }

    private void OnEffectExpired(string contentId)
    {
        RemoveLabel(contentId);
    }

    private void OnEffectRemoved(string contentId)
    {
        RemoveLabel(contentId);
    }

    private void OnEffectsCleared()
    {
        ClearLabels();
    }

    private void AddOrRefresh(CombatStatusEffectInstance effect)
    {
        if (!GodotObject.IsInstanceValid(StatusContainer))
            return;

        string key = Normalize(effect.ContentId);

        if (!_labels.TryGetValue(key, out Label? label))
        {
            label = CreateLabel();
            _labels.Add(key, label);
            StatusContainer.AddChild(label);
        }

        RefreshLabel(label, effect);
        RefreshVisibility();
    }

    private static Label CreateLabel()
    {
        Label label = new()
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            CustomMinimumSize = new Vector2(50.0f, 14.0f)
        };

        label.AddThemeFontSizeOverride("font_size", 9);
        label.AddThemeConstantOverride("outline_size", 2);
        label.AddThemeColorOverride("font_outline_color", Colors.Black);

        return label;
    }

    private static void RefreshLabel(
        Label label,
        CombatStatusEffectInstance effect)
    {
        CombatStatusEffectDefinition definition = effect.Definition;
        string abbreviation = GetAbbreviation(definition);

        label.Text =
            $"[{abbreviation} {effect.RemainingSeconds:0.0}]";

        label.TooltipText = string.IsNullOrWhiteSpace(definition.Description)
            ? definition.DisplayName
            : $"{definition.DisplayName}: {definition.Description}";

        label.AddThemeColorOverride(
            "font_color",
            definition.DisplayColor);
    }

    private void RemoveLabel(string contentId)
    {
        string key = Normalize(contentId);

        if (!_labels.Remove(key, out Label? label))
            return;

        if (GodotObject.IsInstanceValid(label))
            label.QueueFree();

        RefreshVisibility();
    }

    private void ClearLabels()
    {
        foreach (Label label in _labels.Values)
        {
            if (GodotObject.IsInstanceValid(label))
                label.QueueFree();
        }

        _labels.Clear();
        RefreshVisibility();
    }

    private void RefreshVisibility()
    {
        Visible = DebugPresentationSettings.StatusEffectTimersVisible
            && _labels.Count > 0;
    }

    private static string GetAbbreviation(
        CombatStatusEffectDefinition definition)
    {
        string authored = definition.DisplayAbbreviation?.Trim()
            ?? string.Empty;

        if (!string.IsNullOrWhiteSpace(authored))
            return authored.ToUpperInvariant();

        string name = definition.DisplayName?.Trim()
            ?? string.Empty;

        if (name.Length == 0)
            return "???";

        return name[..System.Math.Min(3, name.Length)]
            .ToUpperInvariant();
    }

    private static string Normalize(string contentId)
    {
        return contentId
            .Trim()
            .ToLowerInvariant();
    }
}
