using Godot;
using System;

public partial class HeroAbilityCooldownIndicatorController : Node2D
{
    private ProgressBar _cooldownProgress = null!;
    private HeroActorController? _hero;
    private AbilityDefinition? _ability;

    /// <summary>
    /// Runs Godot setup for Hero Ability Cooldown Indicator Controller when the node enters the scene tree.
    /// Uses the current node and service state; any result is applied through side effects, events, or stored fields.
    /// </summary>
    public override void _Ready()
    {
        _cooldownProgress =
            GetNodeOrNull<ProgressBar>("CooldownProgress")!;

        if (!GodotObject.IsInstanceValid(_cooldownProgress))
        {
            GD.PushError(
                $"{Name} requires a ProgressBar child named " +
                "'CooldownProgress'.");

            Visible = false;
            SetProcess(false);
            return;
        }

        _cooldownProgress.MinValue = 0.0;
        _cooldownProgress.MaxValue = 1.0;
        _cooldownProgress.Value = 1.0;
        _cooldownProgress.ShowPercentage = false;

        Visible = false;
        SetProcess(false);
    }

    /// <summary>
    /// Cleans up Hero Ability Cooldown Indicator Controller when the node leaves the scene tree.
    /// Uses the current node and service state; any result is applied through side effects, events, or stored fields.
    /// </summary>
    public override void _ExitTree()
    {
        Unbind();
    }

    /// <summary>
    /// Updates Hero Ability Cooldown Indicator Controller every rendered frame using the supplied frame delta.
    /// Uses the supplied arguments and current node state; any result is applied through side effects, events, or stored fields.
    /// </summary>
    public override void _Process(double delta)
    {
        Refresh();
    }

    /// <summary>
    /// Performs the bind operation for Hero Ability Cooldown Indicator Controller.
    /// Uses the supplied arguments and current node state; any result is applied through side effects, events, or stored fields.
    /// </summary>
    public void Bind(HeroActorController hero)
    {
        if (!GodotObject.IsInstanceValid(hero))
        {
            throw new ArgumentNullException(nameof(hero));
        }

        Unbind();

        _hero = hero;
        _ability =
            hero.Abilities.Count > 0
                ? hero.Abilities[0]
                : null;

        Visible = _ability is not null;
        SetProcess(Visible);
        Refresh();
    }

    /// <summary>
    /// Performs the unbind operation for Hero Ability Cooldown Indicator Controller.
    /// Uses the current node and service state; any result is applied through side effects, events, or stored fields.
    /// </summary>
    private void Unbind()
    {
        _hero = null;
        _ability = null;
        Visible = false;
        SetProcess(false);
    }

    /// <summary>
    /// Performs the refresh operation for Hero Ability Cooldown Indicator Controller.
    /// Uses the current node and service state; any result is applied through side effects, events, or stored fields.
    /// </summary>
    private void Refresh()
    {
        if (_hero is null
            || !GodotObject.IsInstanceValid(_hero)
            || _ability is null
            || !GodotObject.IsInstanceValid(_cooldownProgress))
        {
            return;
        }

        double cooldownSeconds =
            Math.Max(_ability.CooldownSeconds, 0.0f);

        double remainingSeconds =
            _hero.GetAbilityCooldownRemaining(
                _ability.ContentId);

        _cooldownProgress.Value =
            cooldownSeconds <= 0.0
                ? 1.0
                : Math.Clamp(
                    1.0
                    - remainingSeconds / cooldownSeconds,
                    0.0,
                    1.0);
    }
}
