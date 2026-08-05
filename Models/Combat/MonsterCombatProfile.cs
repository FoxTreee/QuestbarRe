public sealed class MonsterCombatProfile
{
    public float MaximumHealth { get; set; }

    public float AttackRange { get; set; }

    public float AttackInterval { get; set; }

    public float MoveSpeed { get; set; }

    public float AttackDamage { get; set; }

    public AttackDeliveryMode AttackDelivery { get; set; }
}