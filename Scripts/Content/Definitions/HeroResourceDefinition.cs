using Godot;
using System.Collections.Generic;

[GlobalClass]
public partial class HeroResourceDefinition : Resource
{
    [ExportCategory("Identity")]

    /// <summary>
    /// Selects the resource represented by this configuration. None disables
    /// resource tracking for heroes whose class uses this definition.
    /// </summary>
    [Export]
    public HeroResourceType ResourceType { get; set; } = HeroResourceType.None;

    [ExportCategory("Capacity")]

    /// <summary>
    /// Maximum resource available to the hero. Energy and Rage normally use
    /// 100; Mana can use a class-specific value when Mana scaling is added.
    /// </summary>
    [Export(PropertyHint.Range, "0,100000,1")]
    public float MaximumAmount { get; set; } = 100.0f;

    /// <summary>
    /// When enabled, newly created and revived heroes begin with a full
    /// resource pool. For example, a 100-point Energy pool begins at 100.
    /// </summary>
    [Export]
    public bool StartFull { get; set; } = true;

    [ExportCategory("Regeneration")]

    /// <summary>
    /// Resource restored at the end of each regeneration interval. Setting
    /// this to 10 restores ten points per completed interval.
    /// </summary>
    [Export(PropertyHint.Range, "0,100000,0.1")]
    public float RegenerationAmount { get; set; } = 0.0f;

    /// <summary>
    /// Seconds between regeneration ticks. For example, an amount of 10 and
    /// interval of 2 restores ten resource every two seconds.
    /// </summary>
    [Export(PropertyHint.Range, "0.01,3600,0.01")]
    public float RegenerationIntervalSeconds { get; set; } = 2.0f;

    /// <summary>
    /// Returns every invalid setting so content registries can report all
    /// resource configuration problems in one validation pass.
    /// </summary>
    public IReadOnlyList<string> GetValidationErrors()
    {
        List<string> errors = new();

        if (ResourceType == HeroResourceType.None)
            return errors;

        if (MaximumAmount <= 0.0f)
            errors.Add("MaximumAmount must be greater than zero.");

        if (RegenerationAmount < 0.0f)
            errors.Add("RegenerationAmount cannot be negative.");

        if (RegenerationAmount > 0.0f
            && RegenerationIntervalSeconds <= 0.0f)
        {
            errors.Add(
                "RegenerationIntervalSeconds must be greater than zero " +
                "when regeneration is enabled.");
        }

        return errors;
    }
}
