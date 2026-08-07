using System;

[Flags]
public enum HeroCombatTag
{
    None = 0,
    Melee = 1 << 0,
    Ranged = 1 << 1,
    Caster = 1 << 2,
    Healer = 1 << 3,
    Tank = 1 << 4,
    Summoner = 1 << 5,
    Armored = 1 << 6
}
