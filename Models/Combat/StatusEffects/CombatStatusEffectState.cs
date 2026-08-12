using Godot;
using System;
using System.Collections.Generic;

public sealed class CombatStatusEffectState
{
    private readonly Dictionary<string, CombatStatusEffectInstance>
        _activeEffects = new();

    public event Action<CombatStatusEffectInstance>? EffectApplied;
    public event Action<CombatStatusEffectInstance>? EffectRefreshed;
    public event Action<string>? EffectExpired;
    public event Action<string>? EffectRemoved;
    public event Action? EffectsCleared;

    public int Count => _activeEffects.Count;

    /// <summary>
    /// Read-only enumeration used by presentation or debug consumers that bind
    /// after statuses are already active. Callers cannot mutate runtime state.
    /// </summary>
    public IEnumerable<CombatStatusEffectInstance> ActiveEffects =>
        _activeEffects.Values;

    public bool PreventsMovement =>
        AnyActiveEffect(static definition => definition.PreventsMovement);

    public bool PreventsBasicAttacks =>
        AnyActiveEffect(static definition => definition.PreventsBasicAttacks);

    public bool PreventsAbilities =>
        AnyActiveEffect(static definition => definition.PreventsAbilities);

    public bool InterruptsBasicAttacks =>
        AnyActiveEffect(static definition => definition.InterruptsBasicAttacks);

    public bool InterruptsAbilities =>
        AnyActiveEffect(static definition => definition.InterruptsAbilities);

    public bool Has(string contentId)
    {
        return TryNormalize(contentId, out string normalizedId)
            && _activeEffects.ContainsKey(normalizedId);
    }

    public bool TryGet(
        string contentId,
        out CombatStatusEffectInstance effect)
    {
        effect = null!;

        return TryNormalize(contentId, out string normalizedId)
            && _activeEffects.TryGetValue(
                normalizedId,
                out effect!);
    }

    /// <summary>
    /// Adds a new timed status or refreshes the same status to at least the
    /// supplied duration. Different status IDs remain independent. Control
    /// behavior is resolved from the active definitions rather than from
    /// hard-coded status IDs.
    /// </summary>
    public bool TryApplyOrRefresh(
        CombatStatusEffectDefinition definition,
        float durationSeconds)
    {
        if (!GodotObject.IsInstanceValid(definition)
            || durationSeconds <= 0.0f)
        {
            return false;
        }

        IReadOnlyList<string> errors =
            definition.GetValidationErrors();

        if (errors.Count > 0)
            return false;

        string normalizedId =
            Normalize(definition.ContentId);

        if (_activeEffects.TryGetValue(
            normalizedId,
            out CombatStatusEffectInstance? existing))
        {
            existing.Refresh(durationSeconds);
            EffectRefreshed?.Invoke(existing);
            return true;
        }

        CombatStatusEffectInstance instance = new(
            definition,
            durationSeconds);

        _activeEffects.Add(
            normalizedId,
            instance);

        EffectApplied?.Invoke(instance);
        return true;
    }

    public bool Remove(string contentId)
    {
        if (!TryNormalize(contentId, out string normalizedId)
            || !_activeEffects.Remove(normalizedId))
        {
            return false;
        }

        EffectRemoved?.Invoke(normalizedId);
        return true;
    }

    public void Update(double delta)
    {
        if (delta <= 0.0 || _activeEffects.Count == 0)
            return;

        List<string>? expiredIds = null;

        foreach (KeyValuePair<string, CombatStatusEffectInstance> pair
            in _activeEffects)
        {
            pair.Value.Update(delta);

            if (!pair.Value.IsExpired)
                continue;

            expiredIds ??= new List<string>();
            expiredIds.Add(pair.Key);
        }

        if (expiredIds is null)
            return;

        foreach (string expiredId in expiredIds)
        {
            _activeEffects.Remove(expiredId);
            EffectExpired?.Invoke(expiredId);
        }
    }

    public void Clear()
    {
        if (_activeEffects.Count == 0)
            return;

        _activeEffects.Clear();
        EffectsCleared?.Invoke();
    }

    private bool AnyActiveEffect(
        Func<CombatStatusEffectDefinition, bool> predicate)
    {
        foreach (CombatStatusEffectInstance effect
            in _activeEffects.Values)
        {
            if (!effect.IsExpired
                && predicate(effect.Definition))
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryNormalize(
        string contentId,
        out string normalizedId)
    {
        normalizedId = string.Empty;

        if (string.IsNullOrWhiteSpace(contentId))
            return false;

        normalizedId = Normalize(contentId);
        return true;
    }

    private static string Normalize(string contentId)
    {
        return contentId
            .Trim()
            .ToLowerInvariant();
    }
}
