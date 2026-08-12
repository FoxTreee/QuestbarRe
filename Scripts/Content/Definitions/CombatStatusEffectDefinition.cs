using Godot;
using System.Collections.Generic;

[GlobalClass]
public partial class CombatStatusEffectDefinition : Resource
{
    [ExportCategory("Identity")]

    [Export(PropertyHint.PlaceholderText, "status.core.freeze")]
    public string ContentId { get; set; } =
        string.Empty;

    [Export(PropertyHint.PlaceholderText, "Freeze")]
    public string DisplayName { get; set; } =
        "Unnamed Status Effect";

    [Export(PropertyHint.MultilineText)]
    public string Description { get; set; } =
        string.Empty;

    [ExportCategory("Presentation")]

    [Export(PropertyHint.PlaceholderText, "FRZ")]
    public string DisplayAbbreviation { get; set; } =
        string.Empty;

    [Export]
    public Color DisplayColor { get; set; } = Colors.White;

    [ExportCategory("Control")]

    [Export]
    public bool PreventsMovement { get; set; }

    [Export]
    public bool PreventsBasicAttacks { get; set; }

    [Export]
    public bool PreventsAbilities { get; set; }

    [Export]
    public bool InterruptsBasicAttacks { get; set; }

    [Export]
    public bool InterruptsAbilities { get; set; }

    [ExportCategory("Forced Movement")]

    [Export]
    public CombatForcedMovementMode ForcedMovementMode { get; set; } =
        CombatForcedMovementMode.None;

    [Export(PropertyHint.Range, "0.1,5,0.05")]
    public float ForcedMovementSpeedMultiplier { get; set; } =
        1.0f;

    [Export(PropertyHint.Range, "0.05,5,0.05")]
    public float PanicDirectionChangeMinSeconds { get; set; } = 0.35f;

    [Export(PropertyHint.Range, "0.05,5,0.05")]
    public float PanicDirectionChangeMaxSeconds { get; set; } = 0.80f;

    [Export(PropertyHint.Range, "1,400,1")]
    public float PanicLeashDistance { get; set; } = 90.0f;

    [ExportCategory("Authoring")]

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

        if (!System.Enum.IsDefined(
            typeof(CombatForcedMovementMode),
            ForcedMovementMode))
        {
            errors.Add(
                $"{ContentId}: ForcedMovementMode is invalid.");
        }

        if (!float.IsFinite(ForcedMovementSpeedMultiplier)
            || ForcedMovementSpeedMultiplier <= 0.0f)
        {
            errors.Add(
                $"{ContentId}: ForcedMovementSpeedMultiplier must be " +
                "finite and greater than zero.");
        }

        if (!float.IsFinite(PanicDirectionChangeMinSeconds)
            || PanicDirectionChangeMinSeconds <= 0.0f)
        {
            errors.Add(
                $"{ContentId}: PanicDirectionChangeMinSeconds must be finite and greater than zero.");
        }

        if (!float.IsFinite(PanicDirectionChangeMaxSeconds)
            || PanicDirectionChangeMaxSeconds < PanicDirectionChangeMinSeconds)
        {
            errors.Add(
                $"{ContentId}: PanicDirectionChangeMaxSeconds must be finite and at least the minimum.");
        }

        if (!float.IsFinite(PanicLeashDistance)
            || PanicLeashDistance <= 0.0f)
        {
            errors.Add(
                $"{ContentId}: PanicLeashDistance must be finite and greater than zero.");
        }

        return errors;
    }
}
