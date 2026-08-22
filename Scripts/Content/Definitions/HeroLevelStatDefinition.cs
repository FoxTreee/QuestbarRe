using Godot;

[GlobalClass]
public partial class HeroLevelStatDefinition : Resource
{
    [Export(PropertyHint.Range, "1,60,1")]
    public int Level { get; set; } = 1;

    [Export(PropertyHint.Range, "0,1000,1")]
    public int Strength { get; set; }

    [Export(PropertyHint.Range, "0,1000,1")]
    public int Agility { get; set; }

    [Export(PropertyHint.Range, "0,1000,1")]
    public int Stamina { get; set; }

    [Export(PropertyHint.Range, "0,1000,1")]
    public int Intellect { get; set; }

    [Export(PropertyHint.Range, "0,1000,1")]
    public int Spirit { get; set; }

    /// <summary>
    /// Class health at this level before total Stamina contributes health.
    /// </summary>
    [Export(PropertyHint.Range, "1,1000000,1")]
    public float BaseHealth { get; set; } = 100.0f;
}
