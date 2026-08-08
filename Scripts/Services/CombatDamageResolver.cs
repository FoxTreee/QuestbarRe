using Godot;
using System;

public partial class CombatDamageResolver : Node
{
    public override void _Ready()
    {
        DebugLog.Print(
            "CombatDamageResolver initialized.");
    }

    public DamageResult Resolve(
        DamageRequest request,
        CombatHealthState targetHealth)
    {
        ArgumentNullException.ThrowIfNull(
            targetHealth);

        return targetHealth.ApplyDamage(
            request.RequestedDamage);
    }
}
