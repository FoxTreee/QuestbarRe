using Godot;

public enum MeleeEngagementSlot
{
    UpperLeft,
    LowerLeft,
    UpperRight,
    LowerRight
}

public sealed class MeleeEngagementSlotSet
{
    public const int SlotCount = 4;

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

    public void Clear()
    {
        for (int slotIndex = 0;
            slotIndex < SlotCount;
            slotIndex++)
        {
            _occupants[slotIndex] = null;
        }
    }

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
            slot is MeleeEngagementSlot.UpperLeft
                or MeleeEngagementSlot.LowerLeft
                ? -1.0f
                : 1.0f;

        float verticalDirection =
            slot is MeleeEngagementSlot.UpperLeft
                or MeleeEngagementSlot.UpperRight
                ? -1.0f
                : 1.0f;

        return targetPosition
            + new Vector2(
                horizontalDirection * safeHorizontalDistance,
                verticalDirection * safeVerticalDistance);
    }

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
