using Godot;
using System;
using System.Collections.Generic;
using System.Text.Json;

public partial class InventoryPersistenceService : Node
{
    private const int CurrentSaveVersion = 3;
    private const string SavePath = "user://inventory_v3.json";
    private const string VersionTwoSavePath = "user://inventory_v2.json";
    private const string VersionOneSavePath = "user://inventory_v1.json";
    private const double AutoSaveInterval = 0.75;

    [ExportCategory("Dependencies")]
    [Export] public BackpackWindowController Backpack { get; set; } = null!;
    [Export] public ItemContentRegistry Registry { get; set; } = null!;
    [Export] public PartyController Party { get; set; } = null!;
    [Export] public CharacterWindowEquipmentPanelController EquipmentPanel { get; set; } = null!;
    [Export] public ItemAcquisitionService ItemAcquisition { get; set; } = null!;

    private bool _startupComplete;
    private bool _suppressAutoSave;
    private double _autoSaveElapsed;
    private string _lastSavedSnapshot = string.Empty;
    private PreparedSnapshot? _preparingSnapshot;

    public override void _Ready()
    {
        Party.PartySpawned += OnPartySpawned;
        SetProcess(true);
        if (Party.SpawnedHeroCount > 0) Callable.From(CompleteStartup).CallDeferred();
    }

    public override void _ExitTree()
    {
        if (GodotObject.IsInstanceValid(Party)) Party.PartySpawned -= OnPartySpawned;
        if (_startupComplete && !_suppressAutoSave) Save();
    }

    public override void _Process(double delta)
    {
        if (!_startupComplete || _suppressAutoSave) return;
        _autoSaveElapsed += delta;
        if (_autoSaveElapsed < AutoSaveInterval) return;
        _autoSaveElapsed = 0;
        string snapshot = Serialize(BuildSnapshot());
        if (snapshot == _lastSavedSnapshot) return;
        WriteSnapshot(snapshot, out _);
    }

    private void OnPartySpawned(int heroCount) => CompleteStartup();

    private void CompleteStartup()
    {
        if (_startupComplete) return;
        _suppressAutoSave = true;

        string result;
        if (FileAccess.FileExists(SavePath))
            result = Load();
        else if (FileAccess.FileExists(VersionTwoSavePath))
            result = MigrateVersionTwoSave();
        else if (FileAccess.FileExists(VersionOneSavePath))
            result = MigrateVersionOneSave();
        else
            result = "No inventory save found; using new-game inventory.";

        _suppressAutoSave = false;
        _startupComplete = true;
        _lastSavedSnapshot = Serialize(BuildSnapshot());
        DebugLog.Print(result);
    }

    public string Save()
    {
        if (Party.SpawnedHeroCount == 0)
            return "Inventory save deferred until the party has spawned.";
        string snapshot = Serialize(BuildSnapshot());
        return WriteSnapshot(snapshot, out string result) ? result : result;
    }

    public string Load()
    {
        if (!FileAccess.FileExists(SavePath)) return $"No inventory save exists at {SavePath}.";
        if (!TryRead(SavePath, out InventorySaveData? data, out string error)) return error;
        if (data!.Version != CurrentSaveVersion)
            return $"Unsupported inventory save version {data.Version}.";
        if (!TryPrepare(data, out PreparedSnapshot? prepared, out error)) return error;

        _suppressAutoSave = true;
        Commit(prepared!);
        _suppressAutoSave = false;
        _lastSavedSnapshot = Serialize(BuildSnapshot());
        return $"Inventory, equipment, and hero progression loaded from {SavePath}.";
    }

    private InventorySaveData BuildSnapshot()
    {
        InventorySaveData data = new()
        {
            Version = CurrentSaveVersion,
            TotalCopper = Backpack.Currency.TotalCopper
        };
        foreach (BackpackInventoryLocation location in Backpack.Inventory.BagEquipmentLocations)
            data.Bags.Add(ToData(location.Item));
        foreach (BackpackInventoryLocation location in Backpack.Inventory.StorageLocations)
            data.Storage.Add(ToData(location.Item));

        for (int partySlot = 0; partySlot < PartyController.MaximumPartySize; partySlot++)
        {
            HeroActorController? hero = Party.GetHeroAtSlot(partySlot);
            if (!GodotObject.IsInstanceValid(hero)) continue;
            SavedHero savedHero = new()
            {
                PartySlotIndex = partySlot,
                HeroContentId = hero!.Definition.ContentId,
                Level = hero.Progression.Level,
                Experience = hero.Progression.Experience
            };
            foreach (EquipmentSlot slot in Enum.GetValues<EquipmentSlot>())
            {
                IResolvedEquipmentProfile? profile = hero.Equipment.GetItem(slot);
                BackpackItemState? item = profile is null ? null :
                    EquipmentPanel.GetEquipmentInstance(hero, slot, profile);
                savedHero.Equipment.Add(new SavedEquipmentSlot
                {
                    Slot = slot.ToString(),
                    Item = ToData(item)
                });
            }
            data.Heroes.Add(savedHero);
        }
        return data;
    }

    private bool TryPrepare(InventorySaveData data, out PreparedSnapshot? prepared, out string error)
    {
        prepared = new(); error = string.Empty;
        _preparingSnapshot = prepared;
        HashSet<long> identities = new();
        if (!TryResolve(data.Bags, prepared.Bags, identities, out error) ||
            !TryResolve(data.Storage, prepared.Storage, identities, out error)) return false;

        foreach (SavedHero savedHero in data.Heroes)
        {
            HeroActorController? hero = Party.GetHeroAtSlot(savedHero.PartySlotIndex);
            if (!GodotObject.IsInstanceValid(hero) ||
                !hero!.Definition.ContentId.Equals(savedHero.HeroContentId, StringComparison.OrdinalIgnoreCase))
            { error = $"Saved party slot {savedHero.PartySlotIndex + 1} does not match the current hero."; return false; }

            if (!TryValidateSavedProgression(savedHero, out error))
                return false;

            if (savedHero.Equipment.Count != Enum.GetValues<EquipmentSlot>().Length)
            { error = $"Saved hero '{savedHero.HeroContentId}' does not contain every equipment slot."; return false; }

            Dictionary<EquipmentSlot, BackpackItemState?> equipment = new();
            HeroEquipmentLoadout validator = new();
            foreach (SavedEquipmentSlot savedSlot in savedHero.Equipment)
            {
                if (!Enum.TryParse(savedSlot.Slot, out EquipmentSlot slot) || equipment.ContainsKey(slot))
                { error = $"Invalid or duplicate saved equipment slot '{savedSlot.Slot}'."; return false; }
                BackpackItemState? item = null;
                if (savedSlot.Item is not null)
                {
                    List<BackpackItemState?> one = new();
                    if (!TryResolve(new List<SavedItem?> { savedSlot.Item }, one, identities, out error)) return false;
                    item = one[0];
                    if (item?.EquipmentProfile is null)
                    { error = $"Saved hero slot {slot} contains non-equipment."; return false; }
                    if (!HeroEquipmentEligibility.CanEquip(
                        hero.Definition.ClassDefinition,
                        savedHero.Level,
                        item.EquipmentProfile, slot, out error) ||
                        !validator.TryEquipResolved(item.EquipmentProfile, slot, out error)) return false;
                }
                equipment.Add(slot, item);
            }
            prepared.Heroes.Add((
                hero,
                savedHero.Level,
                savedHero.Experience,
                equipment));
        }

        if (!ValidateCapacity(prepared.Bags, prepared.Storage, out error)) return false;
        if (data.TotalCopper < 0) { error = "Saved currency cannot be negative."; return false; }
        prepared.TotalCopper = data.TotalCopper;
        _preparingSnapshot = null;
        return true;
    }

    /// <summary>
    /// Rejects invalid progression before any inventory, equipment, currency,
    /// or hero state is committed from the save.
    /// </summary>
    private static bool TryValidateSavedProgression(
        SavedHero savedHero,
        out string error)
    {
        if (savedHero.Level < 1
            || savedHero.Level > HeroProgressionState.MaximumLevel)
        {
            error =
                $"Saved hero '{savedHero.HeroContentId}' has invalid " +
                $"Level {savedHero.Level}.";
            return false;
        }

        if (!double.IsFinite(savedHero.Experience)
            || savedHero.Experience < 0.0)
        {
            error =
                $"Saved hero '{savedHero.HeroContentId}' has invalid XP " +
                $"{savedHero.Experience}.";
            return false;
        }

        if (savedHero.Level >= HeroProgressionState.MaximumLevel
            && savedHero.Experience > 0.0)
        {
            error =
                $"Saved Level {HeroProgressionState.MaximumLevel} hero " +
                $"'{savedHero.HeroContentId}' must have zero carried XP.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    private void Commit(PreparedSnapshot prepared)
    {
        Backpack.Inventory.TryRestore(prepared.Bags, prepared.Storage, out _);
        Backpack.Currency.TryRestore(prepared.TotalCopper, out _);
        foreach (var heroEntry in prepared.Heroes)
        {
            HeroActorController hero = heroEntry.Hero;
            hero.Equipment.ClearAll();
            hero.Progression.Configure(
                heroEntry.Level,
                heroEntry.Experience);
            foreach (var pair in heroEntry.Equipment)
            {
                if (pair.Value?.EquipmentProfile is not null)
                    hero.Equipment.TryEquipResolved(pair.Value.EquipmentProfile, pair.Key, out _);
                EquipmentPanel.SetRestoredEquipmentInstance(hero, pair.Key, pair.Value);
            }
            hero.NotifyRuntimeEquipmentChanged();
        }
        Backpack.RebuildAfterRestore();
        EquipmentPanel.RefreshAfterRestore();
        ItemAcquisition.AdvanceIdentityCounters(
            prepared.MaximumInstanceId,
            prepared.MaximumStackId);
        Backpack.Inventory.AdvanceSplitStackIdentity(prepared.MaximumStackId);
    }

    private bool TryResolve(List<SavedItem?> saved, List<BackpackItemState?> target,
        HashSet<long> identities, out string error)
    {
        error = string.Empty;
        foreach (SavedItem? entry in saved)
        {
            if (entry is null) { target.Add(null); continue; }
            if (!Registry.TryGet(entry.ItemId, out ItemDefinition definition))
            { error = $"Save references unknown item '{entry.ItemId}'."; return false; }
            if (entry.StackId.HasValue == entry.InstanceId.HasValue)
            { error = $"Saved item '{entry.ItemId}' must have exactly one identity type."; return false; }
            if (definition.IsStackable != entry.StackId.HasValue)
            { error = $"Saved identity type for '{entry.ItemId}' does not match its definition."; return false; }
            long identity = entry.StackId ?? entry.InstanceId ?? 0;
            if (identity <= 0 || !identities.Add(identity))
            { error = $"Save contains invalid or duplicate identity {identity}."; return false; }
            if (entry.Quantity < 1 || entry.Quantity > definition.MaximumStackSize)
            { error = $"Saved quantity for '{entry.ItemId}' is invalid."; return false; }
            target.Add(BackpackItemState.CreateInventoryItem(definition, identity, entry.Quantity));
            if (entry.StackId.HasValue)
                _preparingSnapshot!.MaximumStackId = Math.Max(_preparingSnapshot.MaximumStackId, identity);
            else
                _preparingSnapshot!.MaximumInstanceId = Math.Max(_preparingSnapshot.MaximumInstanceId, identity);
        }
        return true;
    }

    private static bool ValidateCapacity(List<BackpackItemState?> bags,
        List<BackpackItemState?> storage, out string error)
    {
        int capacity = BackpackInventoryState.BaseStorageSlotCount;
        if (bags.Count != BackpackInventoryState.BagEquipmentSlotCount)
        { error = "Saved bag-slot count is invalid."; return false; }
        foreach (BackpackItemState? bag in bags)
        {
            if (bag is not null && !bag.IsBag) { error = "A saved bag slot contains a non-bag."; return false; }
            capacity += bag?.AddedInventorySlots ?? 0;
        }
        if (storage.Count != capacity)
        { error = $"Saved storage count {storage.Count} does not match capacity {capacity}."; return false; }
        error = string.Empty; return true;
    }

    /// <summary>
    /// Upgrades a v2 ownership save by adding each currently spawned hero's
    /// authored starting progression, since v2 never recorded earned progress.
    /// </summary>
    private string MigrateVersionTwoSave()
    {
        if (!TryRead(
            VersionTwoSavePath,
            out InventorySaveData? versionTwo,
            out string error))
        {
            return error;
        }

        if (versionTwo!.Version != 2)
            return "Inventory v2 migration found an unsupported save version.";

        foreach (SavedHero savedHero in versionTwo.Heroes)
        {
            HeroActorController? hero =
                Party.GetHeroAtSlot(savedHero.PartySlotIndex);

            if (!GodotObject.IsInstanceValid(hero))
                continue;

            savedHero.Level = hero!.Progression.Level;
            savedHero.Experience = hero.Progression.Experience;
        }

        versionTwo.Version = CurrentSaveVersion;

        if (!TryPrepare(versionTwo, out PreparedSnapshot? prepared, out error))
            return $"Inventory v2 migration rejected: {error}";

        Commit(prepared!);
        string snapshot = Serialize(BuildSnapshot());

        if (!WriteSnapshot(snapshot, out string writeResult))
            return writeResult;

        return
            "Inventory v2 save migrated to v3 with persistent hero " +
            "level and XP.";
    }

    /// <summary>
    /// Migrates the original inventory-only format directly to v3, retaining
    /// its ownership repair and adding current authored hero progression.
    /// </summary>
    private string MigrateVersionOneSave()
    {
        if (!TryRead(VersionOneSavePath, out InventorySaveData? legacy, out string error)) return error;
        if (legacy!.Version != 1) return "Legacy inventory save version is unsupported.";

        // V1 did not record hero slots. If a saved unique equipment definition
        // matches starter equipment, ownership is assigned to the Backpack.
        HashSet<string> backpackEquipmentIds = new(StringComparer.OrdinalIgnoreCase);
        foreach (SavedItem? item in legacy.Storage)
            if (item is not null && Registry.TryGet(item.ItemId, out ItemDefinition definition) &&
                definition is EquipmentDefinition) backpackEquipmentIds.Add(item.ItemId);

        foreach (HeroActorController hero in Party.SpawnedHeroes)
        {
            foreach (EquipmentSlot slot in Enum.GetValues<EquipmentSlot>())
            {
                IResolvedEquipmentProfile? profile = hero.Equipment.GetItem(slot);
                if (profile is not null && backpackEquipmentIds.Contains(profile.DefinitionContentId))
                    hero.Equipment.Unequip(slot);
            }
        }

        legacy.Version = CurrentSaveVersion;
        legacy.Heroes.Clear();
        for (int i = 0; i < PartyController.MaximumPartySize; i++)
        {
            HeroActorController? hero = Party.GetHeroAtSlot(i);
            if (!GodotObject.IsInstanceValid(hero)) continue;
            SavedHero savedHero = new()
            {
                PartySlotIndex = i,
                HeroContentId = hero!.Definition.ContentId,
                Level = hero.Progression.Level,
                Experience = hero.Progression.Experience
            };
            foreach (EquipmentSlot slot in Enum.GetValues<EquipmentSlot>())
            {
                IResolvedEquipmentProfile? profile = hero.Equipment.GetItem(slot);
                BackpackItemState? item = profile is null ? null : EquipmentPanel.GetEquipmentInstance(hero, slot, profile);
                savedHero.Equipment.Add(new SavedEquipmentSlot { Slot = slot.ToString(), Item = ToData(item) });
            }
            legacy.Heroes.Add(savedHero);
        }

        if (!TryPrepare(legacy, out PreparedSnapshot? prepared, out error)) return $"Legacy migration rejected: {error}";
        Commit(prepared!);
        string snapshot = Serialize(BuildSnapshot());
        if (!WriteSnapshot(snapshot, out string writeResult))
            return writeResult;
        return
            "Legacy inventory save migrated to v3 ownership and hero " +
            "progression persistence.";
    }

    private static bool TryRead(string path, out InventorySaveData? data, out string error)
    {
        data = null; error = string.Empty;
        using FileAccess file = FileAccess.Open(path, FileAccess.ModeFlags.Read);
        if (file is null) { error = $"Could not open {path}."; return false; }
        try { data = JsonSerializer.Deserialize<InventorySaveData>(file.GetAsText()); }
        catch (Exception exception) { error = $"Save is invalid JSON: {exception.Message}"; return false; }
        if (data is null) { error = "Save contains no data."; return false; }
        return true;
    }

    private bool WriteSnapshot(string json, out string result)
    {
        const string temporaryPath = "user://inventory_v3.tmp";
        using (FileAccess file = FileAccess.Open(temporaryPath, FileAccess.ModeFlags.Write))
        {
            if (file is null) { result = $"Could not open {temporaryPath} for writing."; return false; }
            file.StoreString(json);
            file.Flush();
        }

        string temporaryAbsolute = ProjectSettings.GlobalizePath(temporaryPath);
        string saveAbsolute = ProjectSettings.GlobalizePath(SavePath);
        try
        {
            if (System.IO.File.Exists(saveAbsolute))
                System.IO.File.Replace(temporaryAbsolute, saveAbsolute, null);
            else
                System.IO.File.Move(temporaryAbsolute, saveAbsolute);
        }
        catch (Exception exception)
        {
            result = $"Could not atomically replace the inventory save: {exception.Message}";
            return false;
        }

        _lastSavedSnapshot = json;
        result =
            $"Inventory, equipment, currency, and hero progression saved " +
            $"to {SavePath}.";
        return true;
    }

    private static string Serialize(InventorySaveData data) =>
        JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });

    private static SavedItem? ToData(BackpackItemState? item) => item is null ? null : new SavedItem
    { ItemId = item.ItemId, Quantity = item.Quantity, StackId = item.StackId, InstanceId = item.UniqueInstanceId };
}

internal sealed class PreparedSnapshot
{
    public long TotalCopper;
    public List<BackpackItemState?> Bags { get; } = new();
    public List<BackpackItemState?> Storage { get; } = new();
    public List<(
        HeroActorController Hero,
        int Level,
        double Experience,
        Dictionary<EquipmentSlot, BackpackItemState?> Equipment)> Heroes
    { get; } = new();
    public long MaximumInstanceId;
    public long MaximumStackId;
}

public sealed class InventorySaveData
{
    public int Version { get; set; }
    public long TotalCopper { get; set; }
    public List<SavedItem?> Bags { get; set; } = new();
    public List<SavedItem?> Storage { get; set; } = new();
    public List<SavedHero> Heroes { get; set; } = new();
}

public sealed class SavedHero
{
    public int PartySlotIndex { get; set; }
    public string HeroContentId { get; set; } = string.Empty;
    public int Level { get; set; } = 1;
    public double Experience { get; set; }
    public List<SavedEquipmentSlot> Equipment { get; set; } = new();
}

public sealed class SavedEquipmentSlot
{
    public string Slot { get; set; } = string.Empty;
    public SavedItem? Item { get; set; }
}

public sealed class SavedItem
{
    public string ItemId { get; set; } = string.Empty;
    public int Quantity { get; set; } = 1;
    public long? StackId { get; set; }
    public long? InstanceId { get; set; }
}
