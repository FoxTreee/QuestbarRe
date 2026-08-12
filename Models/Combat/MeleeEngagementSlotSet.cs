using Godot;

public enum MeleeEngagementSlot
{
    UpperLeft,
    MiddleLeft,
    LowerLeft,
    UpperRight,
    MiddleRight,
    LowerRight
}

public sealed class MeleeEngagementSlotSet
{
    public const int SlotCount = 6;

    private readonly Node2D?[] _occupants =
        new Node2D?[SlotCount];

    public int ReservedCount
    {
        get
        {
            RemoveInvalidReservations();

            int reservedCount = 0;

            foreach (Node2D? occupant in _occupants)
            {
                if (occupant is not null)
                    reservedCount++;
            }

            return reservedCount;
        }
    }

    /// <summary>
    /// Attempts to reserve the closest currently-open melee engagement slot.
    /// Existing reservations are preserved until released or invalidated.
    /// </summary>
    public bool TryReserveClosest(
        Node2D attacker,
        Vector2 targetPosition,
        float horizontalDistance,
        float verticalDistance,
        out MeleeEngagementSlot slot)
    {
        slot = default;

        if (!IsValidOccupant(attacker))
            return false;

        RemoveInvalidReservations();

        if (TryGetReservation(attacker, out slot))
            return true;

        int closestSlotIndex = -1;
        float closestDistanceSquared = float.MaxValue;

        for (int slotIndex = 0;
            slotIndex < SlotCount;
            slotIndex++)
        {
            if (_occupants[slotIndex] is not null)
                continue;

            MeleeEngagementSlot candidateSlot =
                (MeleeEngagementSlot)slotIndex;

            Vector2 candidatePosition =
                GetWorldPosition(
                    candidateSlot,
                    targetPosition,
                    horizontalDistance,
                    verticalDistance);

            float distanceSquared =
                attacker.GlobalPosition.DistanceSquaredTo(
                    candidatePosition);

            if (distanceSquared >= closestDistanceSquared)
                continue;

            closestSlotIndex = slotIndex;
            closestDistanceSquared = distanceSquared;
        }

        if (closestSlotIndex < 0)
            return false;

        _occupants[closestSlotIndex] = attacker;
        slot = (MeleeEngagementSlot)closestSlotIndex;

        return true;
    }

    /// <summary>
    /// Returns the attacker's existing reservation, if one is still valid.
    /// </summary>
    public bool TryGetReservation(
        Node2D attacker,
        out MeleeEngagementSlot slot)
    {
        slot = default;

        if (!IsValidOccupant(attacker))
            return false;

        RemoveInvalidReservations();

        for (int slotIndex = 0;
            slotIndex < SlotCount;
            slotIndex++)
        {
            if (_occupants[slotIndex] != attacker)
                continue;

            slot = (MeleeEngagementSlot)slotIndex;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Releases the attacker's reserved melee engagement slot.
    /// </summary>
    public bool Release(Node2D attacker)
    {
        if (!GodotObject.IsInstanceValid(attacker))
            return false;

        for (int slotIndex = 0;
            slotIndex < SlotCount;
            slotIndex++)
        {
            if (_occupants[slotIndex] != attacker)
                continue;

            _occupants[slotIndex] = null;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Clears every reservation from this slot set.
    /// </summary>
    public void Clear()
    {
        for (int slotIndex = 0;
            slotIndex < SlotCount;
            slotIndex++)
        {
            _occupants[slotIndex] = null;
        }
    }

    /// <summary>
    /// Converts a six-position melee engagement slot into its world position.
    /// Each side of the target has upper, middle, and lower positions.
    /// </summary>
    public static Vector2 GetWorldPosition(
        MeleeEngagementSlot slot,
        Vector2 targetPosition,
        float horizontalDistance,
        float verticalDistance)
    {
        float safeHorizontalDistance =
            Mathf.Max(0.0f, horizontalDistance);

        float safeVerticalDistance =
            Mathf.Max(0.0f, verticalDistance);

        float horizontalDirection =
            slot switch
            {
                MeleeEngagementSlot.UpperLeft
                    or MeleeEngagementSlot.MiddleLeft
                    or MeleeEngagementSlot.LowerLeft
                    => -1.0f,

                MeleeEngagementSlot.UpperRight
                    or MeleeEngagementSlot.MiddleRight
                    or MeleeEngagementSlot.LowerRight
                    => 1.0f,

                _ => 0.0f
            };

        float verticalDirection =
            slot switch
            {
                MeleeEngagementSlot.UpperLeft
                    or MeleeEngagementSlot.UpperRight
                    => -1.0f,

                MeleeEngagementSlot.MiddleLeft
                    or MeleeEngagementSlot.MiddleRight
                    => 0.0f,

                MeleeEngagementSlot.LowerLeft
                    or MeleeEngagementSlot.LowerRight
                    => 1.0f,

                _ => 0.0f
            };

        return targetPosition
            + new Vector2(
                horizontalDirection * safeHorizontalDistance,
                verticalDirection * safeVerticalDistance);
    }

    /// <summary>
    /// Removes reservations whose occupants no longer exist in the scene tree.
    /// </summary>
    private void RemoveInvalidReservations()
    {
        for (int slotIndex = 0;
            slotIndex < SlotCount;
            slotIndex++)
        {
            if (IsValidOccupant(_occupants[slotIndex]))
                continue;

            _occupants[slotIndex] = null;
        }
    }

    private static bool IsValidOccupant(Node2D? occupant)
    {
        return occupant is not null
            && GodotObject.IsInstanceValid(occupant)
            && occupant.IsInsideTree()
            && !occupant.IsQueuedForDeletion();
    }
}
