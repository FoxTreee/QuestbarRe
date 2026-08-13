public sealed class EquipmentStatTotals
{
    public int Strength { get; private set; }
    public int Agility { get; private set; }
    public int Stamina { get; private set; }
    public int Intellect { get; private set; }
    public int Spirit { get; private set; }

    /// <summary>
    /// Rebuilds aggregate core-stat contributions from equipped runtime items.
    /// These totals are intentionally data-only until stat formulas are designed.
    /// </summary>
    public void Rebuild(
        params IEquipmentStatSource?[] sources)
    {
        Strength = 0;
        Agility = 0;
        Stamina = 0;
        Intellect = 0;
        Spirit = 0;

        foreach (IEquipmentStatSource? source in sources)
        {
            if (source is null)
                continue;

            Strength += source.Strength;
            Agility += source.Agility;
            Stamina += source.Stamina;
            Intellect += source.Intellect;
            Spirit += source.Spirit;
        }
    }

    public override string ToString()
    {
        return
            $"STR={Strength}, " +
            $"AGI={Agility}, " +
            $"STA={Stamina}, " +
            $"INT={Intellect}, " +
            $"SPI={Spirit}";
    }
}
