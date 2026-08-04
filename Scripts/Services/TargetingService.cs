using System.Collections.Generic;
using Godot;

public partial class TargetingService : Node
{
    public MonsterActorController? SelectPriorityMonster(
        IReadOnlyList<MonsterActorController> candidates)
    {
        MonsterActorController? selectedTarget = null;

        foreach (MonsterActorController candidate in candidates)
        {
            if (!IsValidMonsterTarget(candidate))
                continue;

            if (selectedTarget is null
                || candidate.GlobalPosition.X
                > selectedTarget.GlobalPosition.X)
            {
                selectedTarget = candidate;
            }
        }

        return selectedTarget;
    }

    public bool IsValidMonsterTarget(
        MonsterActorController? monster)
    {
        return monster is not null
            && GodotObject.IsInstanceValid(monster)
            && monster.IsInsideTree();
    }
}