using System.Collections.Generic;
using Godot;

public partial class ActorScalePresentationController : Node
{
    [ExportCategory("Dependencies")]
    /// <summary>
    /// Inspector reference used by this component for its actor layer dependency.
    /// Assign the matching node or resource from the scene; leaving it empty prevents that connection from working.
    /// </summary>
    [Export]
    public Node2D ActorLayer { get; set; } = null!;

    /// <summary>
    /// Inspector reference used by this component for its window host dependency.
    /// Assign the matching node or resource from the scene; leaving it empty prevents that connection from working.
    /// </summary>
    [Export]
    public DesktopWindowHostController WindowHost
    {
        get;
        set;
    } = null!;

    [ExportCategory("Collapsed Presentation")]
    /// <summary>
    /// Controls collapsed actor scale, measured as a ratio or multiplier.
    /// For example, changing 0.4 to 0.8 doubles this setting's configured contribution to the system.
    /// </summary>
    [Export(PropertyHint.Range, "0.05,1,0.05")]
    public float CollapsedActorScale { get; set; } = 0.4f;

    private readonly Dictionary<Node2D, Vector2>
        _authoredVisualScales = new();

    private float _appliedPresentationScale = 1.0f;
    private bool _hasAppliedPresentationScale;

    /// <summary>
    /// Runs Godot setup for Actor Scale Presentation Controller when the node enters the scene tree.
    /// Uses the current node and service state; any result is applied through side effects, events, or stored fields.
    /// </summary>
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

    /// <summary>
    /// Cleans up Actor Scale Presentation Controller when the node leaves the scene tree.
    /// Uses the current node and service state; any result is applied through side effects, events, or stored fields.
    /// </summary>
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

    /// <summary>
    /// Performs the register existing actors operation for Actor Scale Presentation Controller.
    /// Uses the current node and service state; any result is applied through side effects, events, or stored fields.
    /// </summary>
    private void RegisterExistingActors()
    {
        foreach (Node actor in ActorLayer.GetChildren())
            RegisterActor(actor);
    }

    /// <summary>
    /// Handles the actor entered tree event and updates the related game state.
    /// Uses the supplied arguments and current node state; any result is applied through side effects, events, or stored fields.
    /// </summary>
    private void OnActorEnteredTree(Node actor)
    {
        Callable.From(
            () => RegisterAndScaleActor(actor))
            .CallDeferred();
    }

    /// <summary>
    /// Handles the actor exiting tree event and updates the related game state.
    /// Uses the supplied arguments and current node state; any result is applied through side effects, events, or stored fields.
    /// </summary>
    private void OnActorExitingTree(Node actor)
    {
        Node2D? visualRoot = GetVisualRoot(actor);

        if (visualRoot is not null)
            _authoredVisualScales.Remove(visualRoot);
    }

    /// <summary>
    /// Performs the register and scale actor operation for Actor Scale Presentation Controller.
    /// Uses the supplied arguments and current node state; any result is applied through side effects, events, or stored fields.
    /// </summary>
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

    /// <summary>
    /// Performs the register actor operation for Actor Scale Presentation Controller.
    /// Uses the supplied arguments and current node state; any result is applied through side effects, events, or stored fields.
    /// </summary>
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

    /// <summary>
    /// Handles the expanded changed event and updates the related game state.
    /// Uses the supplied arguments and current node state; any result is applied through side effects, events, or stored fields.
    /// </summary>
    private void OnExpandedChanged(bool isExpanded)
    {
        ApplyCurrentScale(
            remapEngagedFormations: true);
    }

    /// <summary>
    /// Applies current scale to the relevant actor, resource, or presentation state.
    /// Uses the current node and service state; any result is applied through side effects, events, or stored fields.
    /// </summary>
    private void ApplyCurrentScale()
    {
        ApplyCurrentScale(
            remapEngagedFormations: false);
    }

    /// <summary>
    /// Applies current scale to the relevant actor, resource, or presentation state.
    /// Uses the supplied arguments and current node state; any result is applied through side effects, events, or stored fields.
    /// </summary>
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

    /// <summary>
    /// Applies scale to actor to the relevant actor, resource, or presentation state.
    /// Uses the supplied arguments and current node state; any result is applied through side effects, events, or stored fields.
    /// </summary>
    private void ApplyScaleToActor(Node actor)
    {
        ApplyScaleToActor(
            actor,
            GetCurrentPresentationScale());
    }

    /// <summary>
    /// Applies scale to actor to the relevant actor, resource, or presentation state.
    /// Uses the supplied arguments and current node state; any result is applied through side effects, events, or stored fields.
    /// </summary>
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

    /// <summary>
    /// Performs the remap engaged formations operation for Actor Scale Presentation Controller.
    /// Uses the supplied arguments and current state and returns the resulting int to the caller.
    /// </summary>
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

    /// <summary>
    /// Retrieves visible battlefield rect from the current game state.
    /// Reads the current state and returns the resulting rect2 to the caller.
    /// </summary>
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

    /// <summary>
    /// Performs the would fit horizontally operation for Actor Scale Presentation Controller.
    /// Uses the supplied arguments and current state and returns the resulting bool to the caller.
    /// </summary>
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

    /// <summary>
    /// Performs the add engagement operation for Actor Scale Presentation Controller.
    /// Uses the supplied arguments and current node state; any result is applied through side effects, events, or stored fields.
    /// </summary>
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

    /// <summary>
    /// Performs the collect engagement group operation for Actor Scale Presentation Controller.
    /// Uses the supplied arguments and current state and returns the resulting list node2 d to the caller.
    /// </summary>
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

    /// <summary>
    /// Performs the calculate group center operation for Actor Scale Presentation Controller.
    /// Uses the supplied arguments and current state and returns the resulting vector2 to the caller.
    /// </summary>
    private static Vector2 CalculateGroupCenter(
        IReadOnlyList<Node2D> group)
    {
        Vector2 positionTotal = Vector2.Zero;

        foreach (Node2D actor in group)
            positionTotal += actor.GlobalPosition;

        return positionTotal
            / Mathf.Max(group.Count, 1);
    }

    /// <summary>
    /// Performs the is active combat actor operation for Actor Scale Presentation Controller.
    /// Uses the supplied arguments and current state and returns the resulting bool to the caller.
    /// </summary>
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

    /// <summary>
    /// Updates combat presentation scale and applies the new value to the owning system.
    /// Uses the supplied arguments and current node state; any result is applied through side effects, events, or stored fields.
    /// </summary>
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

    /// <summary>
    /// Retrieves current presentation scale from the current game state.
    /// Reads the current state and returns the resulting float to the caller.
    /// </summary>
    private float GetCurrentPresentationScale()
    {
        if (WindowHost.IsExpanded)
            return 1.0f;

        return Mathf.Clamp(
            CollapsedActorScale,
            0.05f,
            1.0f);
    }

    /// <summary>
    /// Retrieves visual root from the current game state.
    /// Uses the supplied arguments and current state and returns the resulting node2 d to the caller.
    /// </summary>
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

    /// <summary>
    /// Performs the validate references operation for Actor Scale Presentation Controller.
    /// Reads the current state and returns the resulting bool to the caller.
    /// </summary>
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

    /// <summary>
    /// Performs the require operation for Actor Scale Presentation Controller.
    /// Uses the supplied arguments and current state and returns the resulting bool to the caller.
    /// </summary>
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
