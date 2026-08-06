using Godot;

public partial class DebugCommandService : Node
{
    [ExportCategory("Dependencies")]
    [Export]
    public EncounterController Encounter { get; set; } = null!;

    [Export]
    public CombatController Combat { get; set; } = null!;

    [Export]
    public Godot.Collections.Array<HeroActorController>
        Heroes
    { get; set; } = new();

    public override void _Ready()
    {
        SetProcessInput(true);

        GD.Print(
            "DebugCommandService ready. " +
            "Ctrl+Shift+R resets heroes; " +
            "Ctrl+Shift+1 adds one monster; " +
            "Ctrl+Shift+5 adds five monsters.");
    }

    public void ResetHeroes()
    {
        foreach (
            HeroActorController hero
            in Heroes)
        {
            if (!GodotObject.IsInstanceValid(hero))
                continue;

            hero.DebugResetFromIncapacitation();
        }

        Combat.DebugRefreshHeroParticipants();

        GD.Print(
            "Debug command completed: heroes.reset");
    }

    public void AddMonsters(int count)
    {
        Encounter.DebugAddMonsters(count);

        GD.Print(
            $"Debug command completed: " +
            $"monsters.add {count}");
    }

    public void StartEncounter()
    {
        Encounter.JourneyState.BeginEncounter();
    }

    public void EndEncounter()
    {
        Encounter.JourneyState.EndEncounter();
    }
    public override void _UnhandledKeyInput(InputEvent @event)
    {
        if (@event is not InputEventKey keyEvent)
            return;

        if (!keyEvent.Pressed || keyEvent.Echo)
            return;

        if (!keyEvent.CtrlPressed
            || !keyEvent.ShiftPressed)
        {
            return;
        }

        switch (keyEvent.Keycode)
        {
            case Key.R:
                ResetHeroes();
                break;

            case Key.Key1:
                AddMonsters(1);
                break;

            case Key.Key5:
                AddMonsters(5);
                break;

            case Key.X:
                EndEncounter();
                break;

            default:
                return;
        }

        GetViewport().SetInputAsHandled();
    }
}