using Godot;

public static class CombatSpacing
{
    public const float MinimumBodyGap = 3.0f;

    public static float GetBodyClearanceDistance(
        float attackerRadius,
        float targetRadius,
        float attackerLungeDistance,
        float targetLungeDistance,
        float attackerPresentationScale,
        float targetPresentationScale)
    {
        float safeAttackerScale =
            Mathf.Max(attackerPresentationScale, 0.01f);

        float safeTargetScale =
            Mathf.Max(targetPresentationScale, 0.01f);

        float gapScale =
            Mathf.Min(
                safeAttackerScale,
                safeTargetScale);

        return
            Mathf.Max(0.0f, attackerRadius)
                * safeAttackerScale
            + Mathf.Max(0.0f, targetRadius)
                * safeTargetScale
            + Mathf.Max(0.0f, attackerLungeDistance)
                * safeAttackerScale
            + Mathf.Max(0.0f, targetLungeDistance)
                * safeTargetScale
            + MinimumBodyGap * gapScale;
    }

    public static float GetRequiredCenterDistance(
        float attackRange,
        float attackerRadius,
        float targetRadius,
        float attackerLungeDistance,
        float targetLungeDistance,
        float attackerPresentationScale,
        float targetPresentationScale)
    {
        float bodyClearanceDistance =
            GetBodyClearanceDistance(
                attackerRadius,
                targetRadius,
                attackerLungeDistance,
                targetLungeDistance,
                attackerPresentationScale,
                targetPresentationScale);

        float scaledAttackRange =
            Mathf.Max(0.0f, attackRange)
            * Mathf.Max(
                attackerPresentationScale,
                0.01f);

        return Mathf.Max(
            scaledAttackRange,
            bodyClearanceDistance);
    }
}