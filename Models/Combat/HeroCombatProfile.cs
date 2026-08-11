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

    public AttackDeliveryMode AttackDelivery { get; set; }
}
