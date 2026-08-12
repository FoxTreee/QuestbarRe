using Godot;
using System;

public partial class CombatDamageResolver : Node
{
    /// <summary>
    /// Runs Godot setup for Combat Damage Resolver when the node enters the scene tree.
    /// Uses the current node and service state; any result is applied through side effects, events, or stored fields.
    /// </summary>
    public override void _Ready()
    {
        DebugLog.Print(
            "CombatDamageResolver initialized.");
    }

    /// <summary>
    /// Performs the resolve operation for Combat Damage Resolver.
    /// Uses the supplied arguments and current state and returns the resulting damage result to the caller.
    /// </summary>
    public DamageResult Resolve(
        DamageRequest request,
        CombatHealthState targetHealth)
    {
        ArgumentNullException.ThrowIfNull(
            targetHealth);

        float roundedDamage =
            MathF.Round(
                MathF.Max(request.RequestedDamage, 0.0f),
                MidpointRounding.AwayFromZero);

        return targetHealth.ApplyDamage(
            roundedDamage);
    }
}
