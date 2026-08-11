using System;
using System.Collections.Generic;

public sealed class MonsterThreatState
{
    private readonly Dictionary<HeroActorController, float>
        _threatByHero = new();

    public int EntryCount =>
        _threatByHero.Count;

    /// <summary>
    /// Retrieves threat from the current game state.
    /// Uses the supplied arguments and current state and returns the resulting float to the caller.
    /// </summary>
    public float GetThreat(HeroActorController hero)
    {
        ArgumentNullException.ThrowIfNull(hero);

        return _threatByHero.TryGetValue(
            hero,
            out float threat)
                ? threat
                : 0.0f;
    }

    /// <summary>
    /// Performs the add threat operation for Monster Threat State.
    /// Uses the supplied arguments and current node state; any result is applied through side effects, events, or stored fields.
    /// </summary>
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

    /// <summary>
    /// Performs the remove threat operation for Monster Threat State.
    /// Uses the supplied arguments and current state and returns the resulting bool to the caller.
    /// </summary>
    public bool RemoveThreat(
        HeroActorController hero)
    {
        ArgumentNullException.ThrowIfNull(hero);

        return _threatByHero.Remove(hero);
    }

    /// <summary>
    /// Resets  so the system can begin from a clean state.
    /// Uses the current node and service state; any result is applied through side effects, events, or stored fields.
    /// </summary>
    public void Clear()
    {
        _threatByHero.Clear();
    }
}
