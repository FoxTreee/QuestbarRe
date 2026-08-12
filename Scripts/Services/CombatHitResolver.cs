using Godot;
using System;

public partial class CombatHitResolver : Node
{
    private readonly RandomNumberGenerator _random = new();

    public override void _Ready()
    {
        _random.Randomize();

        DebugLog.Print(
            "CombatHitResolver initialized. Defender-owned Dodge is active.");
    }

    /// <summary>
    /// Resolves whether one offensive action connects. Dodge chance belongs to
    /// the defender: heroes expose a resolved combat-profile value that future
    /// Agility/gear/effects can build, while monsters receive a simple authored
    /// value from MonsterDefinition. The resolver owns only the roll.
    /// </summary>
    public CombatHitOutcome Resolve(
        CombatHitRequest request)
    {
        ArgumentNullException.ThrowIfNull(request.Source);
        ArgumentNullException.ThrowIfNull(request.Target);

        if (!Enum.IsDefined(
            typeof(CombatDodgeRule),
            request.DodgeRule))
        {
            throw new ArgumentOutOfRangeException(
                nameof(request),
                request.DodgeRule,
                "Combat dodge rule is invalid.");
        }

        if (request.DodgeRule == CombatDodgeRule.CannotBeDodged)
        {
            return CombatHitOutcome.Hit;
        }

        float dodgeChancePercent = Mathf.Clamp(
            ResolveDefenderDodgeChancePercent(request.Target),
            0.0f,
            100.0f);

        if (dodgeChancePercent <= 0.0f)
        {
            return CombatHitOutcome.Hit;
        }

        if (dodgeChancePercent >= 100.0f)
        {
            return CombatHitOutcome.Dodge;
        }

        float rollPercent = _random.RandfRange(0.0f, 100.0f);

        return rollPercent < dodgeChancePercent
            ? CombatHitOutcome.Dodge
            : CombatHitOutcome.Hit;
    }

    private static float ResolveDefenderDodgeChancePercent(
        Node target)
    {
        return target switch
        {
            HeroActorController hero
                when GodotObject.IsInstanceValid(hero)
                => hero.CombatProfile.DodgeChancePercent,

            MonsterActorController monster
                when GodotObject.IsInstanceValid(monster)
                => monster.CombatProfile.DodgeChancePercent,

            _ => throw new InvalidOperationException(
                $"Defender Dodge was requested for unsupported target " +
                $"type '{target.GetType().Name}'.")
        };
    }
}
