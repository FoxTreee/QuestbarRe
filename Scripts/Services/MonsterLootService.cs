using Godot;

public partial class MonsterLootService : Node
{
    [ExportCategory("Dependencies")]

    /// <summary>
    /// Routes successful item and currency rolls into the existing Backpack,
    /// identity, uniqueness, UI refresh, and persistence pipelines.
    /// </summary>
    [Export]
    public ItemAcquisitionService ItemAcquisition { get; set; } = null!;

    private readonly RandomNumberGenerator _random = new();

    public override void _Ready()
    {
        _random.Randomize();

        if (!GodotObject.IsInstanceValid(ItemAcquisition))
        {
            GD.PushError(
                "MonsterLootService is missing its ItemAcquisition Inspector reference.");
        }
    }

    /// <summary>
    /// Rolls every configured item independently and then rolls the monster's
    /// optional coin drop. EncounterController calls this once per accepted death.
    /// </summary>
    public void AwardMonsterDefeat(MonsterActorController monster)
    {
        if (!GodotObject.IsInstanceValid(monster)
            || !GodotObject.IsInstanceValid(monster.Definition)
            || !GodotObject.IsInstanceValid(ItemAcquisition))
        {
            return;
        }

        MonsterDefinition definition = monster.Definition;

        foreach (MonsterLootEntry entry in definition.LootTable)
        {
            if (!GodotObject.IsInstanceValid(entry)
                || !RollSucceeds(entry.DropChancePercent))
            {
                continue;
            }

            int quantity = _random.RandiRange(
                entry.MinimumQuantity,
                entry.MaximumQuantity);

            if (ItemAcquisition.TryAcquire(
                entry.ItemContentId,
                quantity,
                out string result))
            {
                DebugLog.Print(
                    $"Loot: {monster.DisplayName} dropped {quantity} x " +
                    $"{entry.ItemContentId}. {result}");
            }
            else
            {
                DebugLog.Print(
                    $"Loot could not be collected from {monster.DisplayName}: " +
                    result);
            }
        }

        AwardCurrency(definition, monster.DisplayName);
    }

    /// <summary>
    /// Rolls authored currency in copper; the Backpack wallet automatically
    /// presents the normalized total as gold, silver, and copper.
    /// </summary>
    private void AwardCurrency(
        MonsterDefinition definition,
        string monsterDisplayName)
    {
        if (definition.MaximumCopperDrop <= 0
            || !RollSucceeds(definition.CurrencyDropChancePercent))
        {
            return;
        }

        int copper = _random.RandiRange(
            definition.MinimumCopperDrop,
            definition.MaximumCopperDrop);

        if (ItemAcquisition.TryAcquireCopper(copper, out string result))
        {
            DebugLog.Print(
                $"Loot: {monsterDisplayName} dropped {copper} copper. {result}");
        }
        else
        {
            DebugLog.Print(
                $"Currency loot could not be collected from " +
                $"{monsterDisplayName}: {result}");
        }
    }

    private bool RollSucceeds(float chancePercent)
    {
        if (chancePercent <= 0.0f)
            return false;

        if (chancePercent >= 100.0f)
            return true;

        return _random.RandfRange(0.0f, 100.0f) < chancePercent;
    }
}
