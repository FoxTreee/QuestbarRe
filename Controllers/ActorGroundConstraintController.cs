using Godot;

public partial class ActorGroundConstraintController : Node
{
    [ExportCategory("Dependencies")]
    [Export]
    public Node ActorLayer { get; set; } = null!;

    [Export]
    public Marker2D GroundTopBoundary { get; set; } = null!;

    [Export]
    public Marker2D GroundBottomBoundary { get; set; } = null!;

    private bool _hasPreviousBounds;
    private float _previousTopY;
    private float _previousBottomY;

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

    public override void _ExitTree()
    {
        if (GodotObject.IsInstanceValid(ActorLayer))
        {
            ActorLayer.ChildEnteredTree -=
                OnActorEnteredTree;
        }
    }

    public override void _Process(double delta)
    {
        ApplyGroundConstraints();
    }

    private void OnActorEnteredTree(Node actor)
    {
        if (!IsGroundedActor(actor))
            return;

        // EncounterController assigns a monster's spawn position immediately
        // after AddChild(), so wait until that assignment has completed.
        Callable.From(ApplyGroundConstraints)
            .CallDeferred();
    }

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

    private static bool IsGroundedActor(Node actor)
    {
        return actor is HeroActorController
            or MonsterActorController;
    }

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
