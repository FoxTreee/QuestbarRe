using Godot;

public partial class ActorGroundConstraintController : Node
{
    [ExportCategory("Dependencies")]
    /// <summary>
    /// Inspector reference used by this component for its actor layer dependency.
    /// Assign the matching node or resource from the scene; leaving it empty prevents that connection from working.
    /// </summary>
    [Export]
    public Node ActorLayer { get; set; } = null!;

    /// <summary>
    /// Inspector reference used by this component for its ground top boundary dependency.
    /// Assign the matching node or resource from the scene; leaving it empty prevents that connection from working.
    /// </summary>
    [Export]
    public Marker2D GroundTopBoundary { get; set; } = null!;

    /// <summary>
    /// Inspector reference used by this component for its ground bottom boundary dependency.
    /// Assign the matching node or resource from the scene; leaving it empty prevents that connection from working.
    /// </summary>
    [Export]
    public Marker2D GroundBottomBoundary { get; set; } = null!;

    private bool _hasPreviousBounds;
    private float _previousTopY;
    private float _previousBottomY;

    /// <summary>
    /// Runs Godot setup for Actor Ground Constraint Controller when the node enters the scene tree.
    /// Uses the current node and service state; any result is applied through side effects, events, or stored fields.
    /// </summary>
    public override void _Ready()
    {
        if (!ValidateReferences())
        {
            SetProcess(false);
            return;
        }

        // Actor movement uses the default priority of 0. Running later lets
        // this controller enforce the final legal foot position each frame.
        ProcessPriority = 100;

        ActorLayer.ChildEnteredTree +=
            OnActorEnteredTree;

        Callable.From(ApplyGroundConstraints)
            .CallDeferred();
    }

    /// <summary>
    /// Cleans up Actor Ground Constraint Controller when the node leaves the scene tree.
    /// Uses the current node and service state; any result is applied through side effects, events, or stored fields.
    /// </summary>
    public override void _ExitTree()
    {
        if (GodotObject.IsInstanceValid(ActorLayer))
        {
            ActorLayer.ChildEnteredTree -=
                OnActorEnteredTree;
        }
    }

    /// <summary>
    /// Updates Actor Ground Constraint Controller every rendered frame using the supplied frame delta.
    /// Uses the supplied arguments and current node state; any result is applied through side effects, events, or stored fields.
    /// </summary>
    public override void _Process(double delta)
    {
        ApplyGroundConstraints();
    }

    /// <summary>
    /// Handles the actor entered tree event and updates the related game state.
    /// Uses the supplied arguments and current node state; any result is applied through side effects, events, or stored fields.
    /// </summary>
    private void OnActorEnteredTree(Node actor)
    {
        if (!IsGroundedActor(actor))
            return;

        // EncounterController assigns a monster's spawn position immediately
        // after AddChild(), so wait until that assignment has completed.
        Callable.From(ApplyGroundConstraints)
            .CallDeferred();
    }

    /// <summary>
    /// Applies ground constraints to the relevant actor, resource, or presentation state.
    /// Uses the current node and service state; any result is applied through side effects, events, or stored fields.
    /// </summary>
    private void ApplyGroundConstraints()
    {
        float topY = Mathf.Min(
            GroundTopBoundary.GlobalPosition.Y,
            GroundBottomBoundary.GlobalPosition.Y);

        float bottomY = Mathf.Max(
            GroundTopBoundary.GlobalPosition.Y,
            GroundBottomBoundary.GlobalPosition.Y);

        bool boundsMoved =
            _hasPreviousBounds
            && (!Mathf.IsEqualApprox(topY, _previousTopY)
                || !Mathf.IsEqualApprox(
                    bottomY,
                    _previousBottomY));

        foreach (Node child in ActorLayer.GetChildren())
        {
            if (child is not Node2D actor
                || !IsGroundedActor(actor))
            {
                continue;
            }

            Vector2 constrainedPosition =
                actor.GlobalPosition;

            constrainedPosition.Y = boundsMoved
                ? RemapYToMovedGround(
                    constrainedPosition.Y,
                    topY,
                    bottomY)
                : Mathf.Clamp(
                    constrainedPosition.Y,
                    topY,
                    bottomY);

            actor.GlobalPosition =
                constrainedPosition;
        }

        _previousTopY = topY;
        _previousBottomY = bottomY;
        _hasPreviousBounds = true;
    }

    /// <summary>
    /// Performs the remap y to moved ground operation for Actor Ground Constraint Controller.
    /// Uses the supplied arguments and current state and returns the resulting float to the caller.
    /// </summary>
    private float RemapYToMovedGround(
        float currentY,
        float newTopY,
        float newBottomY)
    {
        float previousHeight = Mathf.Max(
            _previousBottomY - _previousTopY,
            0.001f);

        float normalizedDepth = Mathf.Clamp(
            (currentY - _previousTopY)
                / previousHeight,
            0.0f,
            1.0f);

        return Mathf.Lerp(
            newTopY,
            newBottomY,
            normalizedDepth);
    }

    /// <summary>
    /// Performs the is grounded actor operation for Actor Ground Constraint Controller.
    /// Uses the supplied arguments and current state and returns the resulting bool to the caller.
    /// </summary>
    private static bool IsGroundedActor(Node actor)
    {
        return actor is HeroActorController
            or MonsterActorController;
    }

    /// <summary>
    /// Performs the validate references operation for Actor Ground Constraint Controller.
    /// Reads the current state and returns the resulting bool to the caller.
    /// </summary>
    private bool ValidateReferences()
    {
        bool valid = true;

        valid &= Require(
            ActorLayer,
            nameof(ActorLayer));

        valid &= Require(
            GroundTopBoundary,
            nameof(GroundTopBoundary));

        valid &= Require(
            GroundBottomBoundary,
            nameof(GroundBottomBoundary));

        return valid;
    }

    /// <summary>
    /// Performs the require operation for Actor Ground Constraint Controller.
    /// Uses the supplied arguments and current state and returns the resulting bool to the caller.
    /// </summary>
    private static bool Require(
        GodotObject value,
        string propertyName)
    {
        if (GodotObject.IsInstanceValid(value))
            return true;

        GD.PushError(
            $"ActorGroundConstraintController is missing " +
            $"'{propertyName}'.");

        return false;
    }
}
