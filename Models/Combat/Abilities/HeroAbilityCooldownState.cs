using System;
using System.Collections.Generic;

public sealed class HeroAbilityCooldownState
{
    private readonly Dictionary<string, double>
        _remainingSecondsByAbilityId =
            new(StringComparer.OrdinalIgnoreCase);

    public void Configure(
        IReadOnlyList<AbilityDefinition> abilities)
    {
        ArgumentNullException.ThrowIfNull(abilities);

        _remainingSecondsByAbilityId.Clear();

        foreach (AbilityDefinition ability in abilities)
        {
            if (ability is null
                || string.IsNullOrWhiteSpace(ability.ContentId))
            {
                continue;
            }

            _remainingSecondsByAbilityId[
                ability.ContentId.Trim()] = 0.0;
        }
    }

    public double GetRemainingSeconds(string abilityContentId)
    {
        if (string.IsNullOrWhiteSpace(abilityContentId))
            return 0.0;

        return _remainingSecondsByAbilityId.TryGetValue(
            abilityContentId.Trim(),
            out double remainingSeconds)
                ? remainingSeconds
                : 0.0;
    }

    public bool IsReady(string abilityContentId)
    {
        return GetRemainingSeconds(abilityContentId) <= 0.0;
    }

    public bool TryStart(AbilityDefinition ability)
    {
        ArgumentNullException.ThrowIfNull(ability);

        if (!IsReady(ability.ContentId))
            return false;

        _remainingSecondsByAbilityId[
            ability.ContentId.Trim()] =
                Math.Max(ability.CooldownSeconds, 0.0f);

        return true;
    }

    public void Update(double delta)
    {
        if (delta <= 0.0)
            return;

        List<string> coolingAbilityIds = new();

        foreach (
            KeyValuePair<string, double> cooldown
            in _remainingSecondsByAbilityId)
        {
            if (cooldown.Value > 0.0)
                coolingAbilityIds.Add(cooldown.Key);
        }

        foreach (string abilityContentId in coolingAbilityIds)
        {
            _remainingSecondsByAbilityId[abilityContentId] =
                Math.Max(
                    0.0,
                    _remainingSecondsByAbilityId[
                        abilityContentId] - delta);
        }
    }

    public void Reset()
    {
        List<string> abilityContentIds =
            new(_remainingSecondsByAbilityId.Keys);

        foreach (string abilityContentId in abilityContentIds)
        {
            _remainingSecondsByAbilityId[abilityContentId] = 0.0;
        }
    }
}
