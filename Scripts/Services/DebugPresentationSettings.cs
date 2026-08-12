using System;

/// <summary>
/// Owns global developer-only presentation flags that are useful while
/// debugging Questbar but are not authoritative gameplay state.
/// </summary>
public static class DebugPresentationSettings
{
    /// <summary>
    /// Raised whenever the global status-effect timer overlay is shown or
    /// hidden so every active actor display updates immediately.
    /// </summary>
    public static event Action<bool> StatusEffectTimersVisibilityChanged =
        delegate { };

    /// <summary>
    /// Controls whether compact status-effect timer labels such as
    /// [FRZ 2.4] are visible above actors. Gameplay status effects continue
    /// running normally when this debug presentation is disabled.
    /// </summary>
    public static bool StatusEffectTimersVisible { get; private set; } = true;

    /// <summary>
    /// Sets the global status-effect timer overlay visibility.
    /// </summary>
    public static void SetStatusEffectTimersVisible(bool visible)
    {
        if (StatusEffectTimersVisible == visible)
            return;

        StatusEffectTimersVisible = visible;
        StatusEffectTimersVisibilityChanged(visible);
    }

    /// <summary>
    /// Toggles the global status-effect timer overlay and returns its new state.
    /// </summary>
    public static bool ToggleStatusEffectTimersVisible()
    {
        SetStatusEffectTimersVisible(!StatusEffectTimersVisible);
        return StatusEffectTimersVisible;
    }
}
