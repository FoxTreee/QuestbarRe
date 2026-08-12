public sealed class HeroCombatProfile
{
    public HeroCombatStance CombatStance { get; set; }

    public float MaximumHealth { get; set; }

    public float AttackRange { get; set; }

    public float CombatRadius { get; set; }

    public float AttackInterval { get; set; }

    public float AttackDuration { get; set; }

    public float AttackLungeDistance { get; set; }

    public float MoveSpeed { get; set; }

    public float AttackDamage { get; set; }

    /// <summary>
    /// Final resolved chance for this hero to dodge an incoming dodgeable
    /// offensive action. The current foundation starts at 0%; future hero
    /// stats, Agility, gear, and effects will resolve into this value.
    /// </summary>
    public float DodgeChancePercent { get; set; }

    public AttackDeliveryMode AttackDelivery { get; set; }
}
