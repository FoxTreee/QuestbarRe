using System.Collections.Generic;
using Godot;

public partial class ActorScalePresentationController : Node
{
    [ExportCategory("Dependencies")]
    [Export]
    public Node2D ActorLayer { get; set; } = null!;

    [Export]
    public DesktopWindowHostController WindowHost
    {
        get;
        set;
    } = null!;

    [ExportCategory("Collapsed Presentation")]
    [Export(PropertyHint.Range, "0.05,1,0.05")]
    public float CollapsedActorScale { get; set; } = 0.4f;

    private readonly Dictionary<Node2D, Vector2>
        _authoredVisualScales = new();

    public override void _Ready()
    {
        if (!ValidateReferences())
            return;

        ActorLayer.YSortEnabled = true;

        ActorLayer.ChildEnteredTree +=
            OnActorEnteredTree;

        ActorLayer.ChildExitingTree +=
            OnActorExitingTree;

        WindowHost.ExpandedChanged +=
            OnExpandedChanged;

        RegisterExistingActors();

        Callable.From(ApplyCurrentScale)
            .CallDeferred();
    }

    public override void _ExitTree()
    {
        if (GodotObject.IsInstanceValid(ActorLayer))
        {
            ActorLayer.ChildEnteredTree -=
                OnActorEnteredTree;

            ActorLayer.ChildExitingTree -=
                OnActorExitingTree;
        }

        if (GodotObject.IsInstanceValid(WindowHost))
        {
            WindowHost.ExpandedChanged -=
                OnExpandedChanged;
        }

        _authoredVisualScales.Clear();
    }

    private void RegisterExistingActors()
    {
        foreach (Node actor in ActorLayer.GetChildren())
            RegisterActor(actor);
    }

    private void OnActorEnteredTree(Node actor)
    {
        Callable.From(
            () => RegisterAndScaleActor(actor))
            .CallDeferred();
    }

    private void OnActorExitingTree(Node actor)
    {
        Node2D? visualRoot = GetVisualRoot(actor);

        if (visualRoot is not null)
            _authoredVisualScales.Remove(visualRoot);
    }

    private void RegisterAndScaleActor(Node actor)
    {
        if (!GodotObject.IsInstanceValid(actor)
            || !actor.IsInsideTree())
        {
            return;
        }

        RegisterActor(actor);
        ApplyScaleToActor(actor);
    }

    private void RegisterActor(Node actor)
    {
        Node2D? visualRoot = GetVisualRoot(actor);

        if (visualRoot is null
            || !GodotObject.IsInstanceValid(visualRoot)
            || _authoredVisualScales.ContainsKey(visualRoot))
        {
            return;
        }

        _authoredVisualScales.Add(
            visualRoot,
            visualRoot.Scale);
    }

    private void OnExpandedChanged(bool isExpanded)
    {
        ApplyCurrentScale();
    }

    private void ApplyCurrentScale()
    {
        foreach (Node actor in ActorLayer.GetChildren())
            ApplyScaleToActor(actor);

        DebugLog.Print(
            $"Actor presentation scale changed. " +
            $"Expanded={WindowHost.IsExpanded}, " +
            $"Scale={GetCurrentPresentationScale():0.###}, " +
            $"Actors={_authoredVisualScales.Count}");
    }

    private void ApplyScaleToActor(Node actor)
    {
        Node2D? visualRoot = GetVisualRoot(actor);

        if (visualRoot is null
            || !_authoredVisualScales.TryGetValue(
                visualRoot,
                out Vector2 authoredScale))
        {
            return;
        }

        float presentationScale =
            GetCurrentPresentationScale();

        visualRoot.Scale =
            authoredScale
            * presentationScale;

        SetCombatPresentationScale(
            actor,
            presentationScale);
    }

    private static void SetCombatPresentationScale(
        Node actor,
        float presentationScale)
    {
        switch (actor)
        {
            case HeroActorController hero:
                hero.SetCombatPresentationScale(
                    presentationScale);
                break;

            case MonsterActorController monster:
                monster.SetCombatPresentationScale(
                    presentationScale);
                break;
        }
    }

    private float GetCurrentPresentationScale()
    {
        if (WindowHost.IsExpanded)
            return 1.0f;

        return Mathf.Clamp(
            CollapsedActorScale,
            0.05f,
            1.0f);
    }

    private static Node2D? GetVisualRoot(Node actor)
    {
        return actor switch
        {
            HeroActorController hero =>
                hero.VisualRoot,

            MonsterActorController monster =>
                monster.PresentationRoot,

            _ => null
        };
    }

    private bool ValidateReferences()
    {
        bool valid = true;

        valid &= Require(
            ActorLayer,
            nameof(ActorLayer));

        valid &= Require(
            WindowHost,
            nameof(WindowHost));

        return valid;
    }

    private static bool Require(
        GodotObject value,
        string propertyName)
    {
        if (GodotObject.IsInstanceValid(value))
            return true;

        GD.PushError(
            $"ActorScalePresentationController is missing " +
            $"'{propertyName}'.");

        return false;
    }
}
