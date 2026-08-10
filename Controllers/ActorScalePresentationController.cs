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

    private float _appliedPresentationScale = 1.0f;
    private bool _hasAppliedPresentationScale;

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
        ApplyCurrentScale(
            remapEngagedFormations: true);
    }

    private void ApplyCurrentScale()
    {
        ApplyCurrentScale(
            remapEngagedFormations: false);
    }

    private void ApplyCurrentScale(
        bool remapEngagedFormations)
    {
        float presentationScale =
            GetCurrentPresentationScale();

        int remappedGroups = 0;
        int remappedActors = 0;
        int skippedGroups = 0;

        if (remapEngagedFormations
            && _hasAppliedPresentationScale
            && !Mathf.IsEqualApprox(
                presentationScale,
                _appliedPresentationScale))
        {
            float scaleRatio =
                presentationScale
                / _appliedPresentationScale;

            remappedGroups = RemapEngagedFormations(
                scaleRatio,
                out remappedActors,
                out skippedGroups);
        }

        foreach (Node actor in ActorLayer.GetChildren())
        {
            ApplyScaleToActor(
                actor,
                presentationScale);
        }

        _appliedPresentationScale =
            presentationScale;

        _hasAppliedPresentationScale = true;

        DebugLog.Print(
            $"Actor presentation scale changed. " +
            $"Expanded={WindowHost.IsExpanded}, " +
            $"Scale={presentationScale:0.###}, " +
            $"Actors={_authoredVisualScales.Count}, " +
            $"RemappedGroups={remappedGroups}, " +
            $"RemappedActors={remappedActors}, " +
            $"SkippedGroups={skippedGroups}");
    }

    private void ApplyScaleToActor(Node actor)
    {
        ApplyScaleToActor(
            actor,
            GetCurrentPresentationScale());
    }

    private void ApplyScaleToActor(
        Node actor,
        float presentationScale)
    {
        Node2D? visualRoot = GetVisualRoot(actor);

        if (visualRoot is null
            || !_authoredVisualScales.TryGetValue(
                visualRoot,
                out Vector2 authoredScale))
        {
            return;
        }

        visualRoot.Scale =
            authoredScale
            * presentationScale;

        SetCombatPresentationScale(
            actor,
            presentationScale);
    }

    private int RemapEngagedFormations(
        float scaleRatio,
        out int remappedActorCount,
        out int skippedGroupCount)
    {
        remappedActorCount = 0;
        skippedGroupCount = 0;

        if (!float.IsFinite(scaleRatio)
            || scaleRatio <= 0.0f
            || Mathf.IsEqualApprox(scaleRatio, 1.0f))
        {
            return 0;
        }

        Dictionary<Node2D, List<Node2D>> engagements =
            BuildEngagementGraph();

        HashSet<Node2D> visited = new();
        int remappedGroupCount = 0;
        Rect2 visibleBattlefield =
            GetVisibleBattlefieldRect();

        foreach (Node2D actor in engagements.Keys)
        {
            if (visited.Contains(actor)
                || engagements[actor].Count == 0)
            {
                continue;
            }

            List<Node2D> group =
                CollectEngagementGroup(
                    actor,
                    engagements,
                    visited);

            if (group.Count < 2)
                continue;

            Vector2 groupCenter =
                CalculateGroupCenter(group);

            if (!WouldFitHorizontally(
                group,
                groupCenter,
                scaleRatio,
                visibleBattlefield))
            {
                skippedGroupCount++;
                continue;
            }

            foreach (Node2D groupActor in group)
            {
                Vector2 offset =
                    groupActor.GlobalPosition
                    - groupCenter;

                groupActor.GlobalPosition =
                    groupCenter
                    + offset * scaleRatio;
            }

            remappedGroupCount++;
            remappedActorCount += group.Count;
        }

        return remappedGroupCount;
    }

    private Rect2 GetVisibleBattlefieldRect()
    {
        Viewport viewport = ActorLayer.GetViewport();
        Rect2 visibleRect = viewport.GetVisibleRect();

        if (viewport is SubViewport subViewport
            && subViewport.Size2DOverride.X > 0
            && subViewport.Size2DOverride.Y > 0)
        {
            visibleRect = new Rect2(
                Vector2.Zero,
                new Vector2(
                    subViewport.Size2DOverride.X,
                    subViewport.Size2DOverride.Y));
        }

        return visibleRect;
    }

    private static bool WouldFitHorizontally(
        IReadOnlyList<Node2D> group,
        Vector2 groupCenter,
        float scaleRatio,
        Rect2 visibleBattlefield)
    {
        float minimumX = visibleBattlefield.Position.X;
        float maximumX = visibleBattlefield.End.X;

        if (!float.IsFinite(minimumX)
            || !float.IsFinite(maximumX)
            || maximumX <= minimumX)
        {
            return false;
        }

        foreach (Node2D actor in group)
        {
            float proposedX =
                groupCenter.X
                + (actor.GlobalPosition.X - groupCenter.X)
                * scaleRatio;

            if (!float.IsFinite(proposedX)
                || proposedX < minimumX
                || proposedX > maximumX)
            {
                return false;
            }
        }

        return true;
    }

    private Dictionary<Node2D, List<Node2D>>
        BuildEngagementGraph()
    {
        Dictionary<Node2D, List<Node2D>> engagements =
            new();

        foreach (Node child in ActorLayer.GetChildren())
        {
            if (!IsActiveCombatActor(child))
                continue;

            engagements[(Node2D)child] = new List<Node2D>();
        }

        foreach (Node2D actor in engagements.Keys)
        {
            Node2D? target = actor switch
            {
                HeroActorController hero =>
                    hero.CurrentTarget,

                MonsterActorController monster =>
                    monster.CurrentTarget,

                _ => null
            };

            if (target is null
                || !engagements.ContainsKey(target))
            {
                continue;
            }

            AddEngagement(
                actor,
                target,
                engagements);
        }

        return engagements;
    }

    private static void AddEngagement(
        Node2D actor,
        Node2D target,
        Dictionary<Node2D, List<Node2D>> engagements)
    {
        if (!engagements[actor].Contains(target))
            engagements[actor].Add(target);

        if (!engagements[target].Contains(actor))
            engagements[target].Add(actor);
    }

    private static List<Node2D> CollectEngagementGroup(
        Node2D firstActor,
        Dictionary<Node2D, List<Node2D>> engagements,
        HashSet<Node2D> visited)
    {
        Queue<Node2D> pending = new();
        List<Node2D> group = new();

        pending.Enqueue(firstActor);
        visited.Add(firstActor);

        while (pending.Count > 0)
        {
            Node2D actor = pending.Dequeue();
            group.Add(actor);

            foreach (Node2D connectedActor in engagements[actor])
            {
                if (!visited.Add(connectedActor))
                    continue;

                pending.Enqueue(connectedActor);
            }
        }

        return group;
    }

    private static Vector2 CalculateGroupCenter(
        IReadOnlyList<Node2D> group)
    {
        Vector2 positionTotal = Vector2.Zero;

        foreach (Node2D actor in group)
            positionTotal += actor.GlobalPosition;

        return positionTotal
            / Mathf.Max(group.Count, 1);
    }

    private static bool IsActiveCombatActor(Node actor)
    {
        return actor switch
        {
            HeroActorController hero =>
                hero.IsInsideTree()
                && hero.Health.IsAlive
                && !hero.IsIncapacitated,

            MonsterActorController monster =>
                monster.IsInsideTree()
                && monster.Health.IsAlive
                && !monster.IsDead,

            _ => false
        };
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
