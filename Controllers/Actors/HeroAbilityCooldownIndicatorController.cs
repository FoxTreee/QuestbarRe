using Godot;
using System;

public partial class HeroAbilityCooldownIndicatorController : Node2D
{
    private ProgressBar _cooldownProgress = null!;
    private HeroActorController? _hero;
    private AbilityDefinition? _ability;

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

    public override void _ExitTree()
    {
        Unbind();
    }

    public override void _Process(double delta)
    {
        Refresh();
    }

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

    private void Unbind()
    {
        _hero = null;
        _ability = null;
        Visible = false;
        SetProcess(false);
    }

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
