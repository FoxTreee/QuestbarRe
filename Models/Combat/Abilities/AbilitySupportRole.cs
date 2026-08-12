/// <summary>
/// Describes how an ability can contribute when the hero is actively helping
/// a pressured party member. This is support intent, not effect type: Taunt,
/// Frost Nova, and Fear can all be Peel abilities even though their effects
/// are different.
/// </summary>
public enum AbilitySupportRole
{
    None,
    RecoverAlly,
    Peel
}
