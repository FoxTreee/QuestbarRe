using Godot;
using System.Collections.Generic;

[GlobalClass]
public partial class CombatStatusEffectDefinition : Resource
{
    [ExportCategory("Identity")]

    /// <summary>
    /// Stable content identifier for this status effect. Use lowercase
    /// category.namespace.name format such as status.core.freeze.
    /// </summary>
    [Export(PropertyHint.PlaceholderText, "status.core.freeze")]
    public string ContentId { get; set; } =
        string.Empty;

    /// <summary>
    /// Player-facing name used by logs, tooltips, and status UI.
    /// </summary>
    [Export(PropertyHint.PlaceholderText, "Freeze")]
    public string DisplayName { get; set; } =
        "Unnamed Status Effect";

    /// <summary>
    /// Player/designer-facing description of the status effect.
    /// </summary>
    [Export(PropertyHint.MultilineText)]
    public string Description { get; set; } =
        string.Empty;

    [ExportCategory("Presentation")]

    /// <summary>
    /// Compact label shown in the reusable status display. Leave empty to use
    /// the first three characters of DisplayName automatically.
    /// </summary>
    [Export(PropertyHint.PlaceholderText, "FRZ")]
    public string DisplayAbbreviation { get; set; } =
        string.Empty;

    /// <summary>
    /// Presentation tint used by the generic status label. This is visual
    /// metadata only and has no gameplay effect.
    /// </summary>
    [Export]
    public Color DisplayColor { get; set; } = Colors.White;

    [ExportCategory("Control")]

    /// <summary>
    /// When enabled, actors affected by this status cannot move under their
    /// normal combat movement logic while the status remains active.
    /// </summary>
    [Export]
    public bool PreventsMovement { get; set; }

    /// <summary>
    /// When enabled, affected actors cannot begin new basic attacks while the
    /// status remains active.
    /// </summary>
    [Export]
    public bool PreventsBasicAttacks { get; set; }

    /// <summary>
    /// When enabled, affected actors cannot begin new abilities while the
    /// status remains active.
    /// </summary>
    [Export]
    public bool PreventsAbilities { get; set; }

    /// <summary>
    /// When enabled, a basic attack already in progress is canceled and reset
    /// when this status becomes active. The attack must begin again from the
    /// start after control ends.
    /// </summary>
    [Export]
    public bool InterruptsBasicAttacks { get; set; }

    /// <summary>
    /// When enabled, an ability already being cast is canceled and reset when
    /// this status becomes active. An interrupted cast does not start its
    /// cooldown and must begin again from the start after control ends.
    /// </summary>
    [Export]
    public bool InterruptsAbilities { get; set; }

    [ExportCategory("Authoring")]

    /// <summary>
    /// Freeform notes for designers. This does not affect runtime behavior.
    /// </summary>
    [Export(PropertyHint.MultilineText)]
    public string DesignerNotes { get; set; } =
        string.Empty;

    public IReadOnlyList<string> GetValidationErrors()
    {
        List<string> errors = new();

        if (!global::ContentId.IsValid(ContentId))
        {
            errors.Add(
                $"Invalid status-effect Content ID '{ContentId}'. " +
                "Expected lowercase format such as " +
                "'status.core.freeze'.");
        }
        else if (!ContentId.StartsWith(
            "status.",
            System.StringComparison.Ordinal))
        {
            errors.Add(
                $"{ContentId}: status-effect Content IDs must begin " +
                "with 'status.'.");
        }

        if (string.IsNullOrWhiteSpace(DisplayName))
        {
            errors.Add(
                $"{ContentId}: DisplayName is required.");
        }

        if (InterruptsBasicAttacks && !PreventsBasicAttacks)
        {
            errors.Add(
                $"{ContentId}: InterruptsBasicAttacks requires " +
                "PreventsBasicAttacks so the interrupted action cannot " +
                "immediately restart while the status is active.");
        }

        if (InterruptsAbilities && !PreventsAbilities)
        {
            errors.Add(
                $"{ContentId}: InterruptsAbilities requires " +
                "PreventsAbilities so the interrupted cast cannot " +
                "immediately restart while the status is active.");
        }

        return errors;
    }
}
