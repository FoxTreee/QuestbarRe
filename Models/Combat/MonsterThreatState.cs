using System;
using System.Collections.Generic;

public sealed class MonsterThreatState
{
    private readonly Dictionary<HeroActorController, float>
        _threatByHero = new();

    public int EntryCount =>
        _threatByHero.Count;

    public float GetThreat(HeroActorController hero)
    {
        ArgumentNullException.ThrowIfNull(hero);

        return _threatByHero.TryGetValue(
            hero,
            out float threat)
                ? threat
                : 0.0f;
    }

    public void AddThreat(
        HeroActorController hero,
        float amount)
    {
        ArgumentNullException.ThrowIfNull(hero);

        if (!float.IsFinite(amount)
            || amount <= 0.0f)
        {
            return;
        }

        _threatByHero[hero] =
            GetThreat(hero) + amount;
    }

    public bool RemoveThreat(
        HeroActorController hero)
    {
        ArgumentNullException.ThrowIfNull(hero);

        return _threatByHero.Remove(hero);
    }

    public void Clear()
    {
        _threatByHero.Clear();
    }
}
