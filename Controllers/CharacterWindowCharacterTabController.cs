using Godot;
using System;

public partial class CharacterWindowCharacterTabController : Node
{
    [ExportCategory("Dependencies")]

    /// <summary>
    /// Shared Character Window selection context. This controller presents the
    /// selected hero but does not own hero selection itself.
    /// </summary>
    [Export]
    public CharacterWindowController CharacterWindow { get; set; } = null!;

    /// <summary>
    /// CharacterVBox root containing the Stats, Damage, and Ranged Damage
    /// panels created in CharacterWindow.tscn.
    /// </summary>
    [Export]
    public Control CharacterTabRoot { get; set; } = null!;


    private Label _strengthValue = null!;
    private Label _agilityValue = null!;
    private Label _staminaValue = null!;
    private Label _intellectValue = null!;
    private Label _spiritValue = null!;
    private Label _armorValue = null!;

    private Label _minimumDamageValue = null!;
    private Label _maximumDamageValue = null!;
    private Label _attackSpeedValue = null!;
    private Label _dpsValue = null!;

    private Label _rangedMinimumDamageValue = null!;
    private Label _rangedMaximumDamageValue = null!;
    private Label _rangedAttackSpeedValue = null!;
    private Label _rangedDpsValue = null!;

    private HeroActorController? _observedHero;


    /// <summary>
    /// Resolves the Character-tab labels once, subscribes to the shared hero
    /// selection context, and immediately presents any hero already selected.
    /// </summary>
    public override void _Ready()
    {
        if (!ValidateReferences())
            return;

        if (!TryResolveLabels())
            return;

        CharacterWindow.SelectedHeroChanged +=
            OnSelectedHeroChanged;

        ObserveHero(CharacterWindow.SelectedHero);
    }


    /// <summary>
    /// Removes the selection listener owned by this presentation controller.
    /// </summary>
    public override void _ExitTree()
    {
        if (GodotObject.IsInstanceValid(
            CharacterWindow))
        {
            CharacterWindow.SelectedHeroChanged -=
                OnSelectedHeroChanged;
        }

        StopObservingHero();
    }


    /// <summary>
    /// Refreshes all Character-tab values whenever the shared hero selection
    /// changes.
    /// </summary>
    private void OnSelectedHeroChanged(
        HeroActorController hero)
    {
        ObserveHero(hero);
    }

    /// <summary>
    /// Follows equipment changes only for the currently selected hero, avoiding
    /// stale redraws and unnecessary listeners on the other party members.
    /// </summary>
    private void ObserveHero(HeroActorController? hero)
    {
        StopObservingHero();

        if (GodotObject.IsInstanceValid(hero))
        {
            _observedHero = hero;
            _observedHero!.EquipmentChanged += OnEquipmentChanged;
        }

        Refresh(hero);
    }

    private void StopObservingHero()
    {
        if (GodotObject.IsInstanceValid(_observedHero))
            _observedHero!.EquipmentChanged -= OnEquipmentChanged;

        _observedHero = null;
    }

    private void OnEquipmentChanged(HeroActorController hero)
    {
        if (ReferenceEquals(hero, CharacterWindow.SelectedHero))
            Refresh(hero);
    }


    /// <summary>
    /// Presents equipment-derived stats and raw authored weapon values for the
    /// selected hero. No character-stat or armor gameplay formulas are applied.
    /// </summary>
    public void Refresh(
        HeroActorController? hero)
    {
        if (!GodotObject.IsInstanceValid(hero))
        {
            ShowEmptyState();
            return;
        }

        EquipmentStatTotals stats =
            hero!.EquipmentStats;

        _strengthValue.Text =
            stats.Strength.ToString();

        _agilityValue.Text =
            stats.Agility.ToString();

        _staminaValue.Text =
            stats.Stamina.ToString();

        _intellectValue.Text =
            stats.Intellect.ToString();

        _spiritValue.Text =
            stats.Spirit.ToString();

        _armorValue.Text =
            hero.EquipmentArmor.ToString();

        PresentWeapon(
            hero.Equipment.MainHandWeapon,
            _minimumDamageValue,
            _maximumDamageValue,
            _attackSpeedValue,
            _dpsValue);

        PresentWeapon(
            hero.Equipment.RangedWeapon,
            _rangedMinimumDamageValue,
            _rangedMaximumDamageValue,
            _rangedAttackSpeedValue,
            _rangedDpsValue);
    }


    /// <summary>
    /// Presents one resolved weapon without changing combat. DPS is a display
    /// value calculated from the weapon's average authored damage divided by
    /// its authored attack-speed interval.
    /// </summary>
    private static void PresentWeapon(
        ResolvedWeaponProfile? weapon,
        Label minimumDamageValue,
        Label maximumDamageValue,
        Label attackSpeedValue,
        Label dpsValue)
    {
        if (weapon is null)
        {
            minimumDamageValue.Text = "—";
            maximumDamageValue.Text = "—";
            attackSpeedValue.Text = "—";
            dpsValue.Text = "—";
            return;
        }

        int minimumDamage =
            Math.Max(
                0,
                Mathf.RoundToInt(
                    weapon.MinimumDamage));

        int maximumDamage =
            Math.Max(
                minimumDamage,
                Mathf.RoundToInt(
                    weapon.MaximumDamage));

        float attackSpeed =
            Mathf.Max(
                weapon.AttackSpeedSeconds,
                0.01f);

        float averageDamage =
            (minimumDamage + maximumDamage)
            / 2.0f;

        float displayDps =
            averageDamage
            / attackSpeed;

        minimumDamageValue.Text =
            minimumDamage.ToString();

        maximumDamageValue.Text =
            maximumDamage.ToString();

        attackSpeedValue.Text =
            attackSpeed.ToString("0.00");

        dpsValue.Text =
            displayDps.ToString("0.0");
    }


    /// <summary>
    /// Clears presentation values when no runtime hero is selected.
    /// </summary>
    private void ShowEmptyState()
    {
        _strengthValue.Text = "—";
        _agilityValue.Text = "—";
        _staminaValue.Text = "—";
        _intellectValue.Text = "—";
        _spiritValue.Text = "—";
        _armorValue.Text = "—";

        _minimumDamageValue.Text = "—";
        _maximumDamageValue.Text = "—";
        _attackSpeedValue.Text = "—";
        _dpsValue.Text = "—";

        _rangedMinimumDamageValue.Text = "—";
        _rangedMaximumDamageValue.Text = "—";
        _rangedAttackSpeedValue.Text = "—";
        _rangedDpsValue.Text = "—";
    }


    /// <summary>
    /// Resolves the existing value Labels by their stable CharacterWindow node
    /// names so the Inspector only needs one Character-tab root reference.
    /// </summary>
    private bool TryResolveLabels()
    {
        _strengthValue =
            ResolveLabel(
                "StatStrip/StatsPanel/StatsVbox/" +
                "StatsGrid/StrengthValue");

        _agilityValue =
            ResolveLabel(
                "StatStrip/StatsPanel/StatsVbox/" +
                "StatsGrid/AgilityValue");

        _staminaValue =
            ResolveLabel(
                "StatStrip/StatsPanel/StatsVbox/" +
                "StatsGrid/StaminaValue");

        _intellectValue =
            ResolveLabel(
                "StatStrip/StatsPanel/StatsVbox/" +
                "StatsGrid/IntellectValue");

        _spiritValue =
            ResolveLabel(
                "StatStrip/StatsPanel/StatsVbox/" +
                "StatsGrid/SpiritValue");

        _armorValue =
            ResolveLabel(
                "StatStrip/StatsPanel/StatsVbox/" +
                "StatsGrid/ArmorValue");

        _minimumDamageValue =
            ResolveLabel(
                "StatStrip/DamagePanel/DamageVbox/" +
                "DamageGrid/MinDamageValue");

        _maximumDamageValue =
            ResolveLabel(
                "StatStrip/DamagePanel/DamageVbox/" +
                "DamageGrid/MaxDamageValue");

        _attackSpeedValue =
            ResolveLabel(
                "StatStrip/DamagePanel/DamageVbox/" +
                "DamageGrid/AttackSpeedValue");

        _dpsValue =
            ResolveLabel(
                "StatStrip/DamagePanel/DamageVbox/" +
                "DamageGrid/DpsValue");

        _rangedMinimumDamageValue =
            ResolveLabel(
                "StatStrip/RangedDamagePanel/RangedDamageVbox/" +
                "RangedDamageGrid/RangedMinDamageValue");

        _rangedMaximumDamageValue =
            ResolveLabel(
                "StatStrip/RangedDamagePanel/RangedDamageVbox/" +
                "RangedDamageGrid/RangedMaxDamageValue");

        _rangedAttackSpeedValue =
            ResolveLabel(
                "StatStrip/RangedDamagePanel/RangedDamageVbox/" +
                "RangedDamageGrid/RangedAttackSpeedValue");

        _rangedDpsValue =
            ResolveLabel(
                "StatStrip/RangedDamagePanel/RangedDamageVbox/" +
                "RangedDamageGrid/RangedDpsValue");

        return
            GodotObject.IsInstanceValid(_strengthValue)
            && GodotObject.IsInstanceValid(_agilityValue)
            && GodotObject.IsInstanceValid(_staminaValue)
            && GodotObject.IsInstanceValid(_intellectValue)
            && GodotObject.IsInstanceValid(_spiritValue)
            && GodotObject.IsInstanceValid(_armorValue)
            && GodotObject.IsInstanceValid(_minimumDamageValue)
            && GodotObject.IsInstanceValid(_maximumDamageValue)
            && GodotObject.IsInstanceValid(_attackSpeedValue)
            && GodotObject.IsInstanceValid(_dpsValue)
            && GodotObject.IsInstanceValid(_rangedMinimumDamageValue)
            && GodotObject.IsInstanceValid(_rangedMaximumDamageValue)
            && GodotObject.IsInstanceValid(_rangedAttackSpeedValue)
            && GodotObject.IsInstanceValid(_rangedDpsValue);
    }


    /// <summary>
    /// Finds one required label and reports a precise scene-path error when the
    /// CharacterWindow shell no longer matches the expected presentation tree.
    /// </summary>
    private Label ResolveLabel(
        string path)
    {
        Label? label =
            CharacterTabRoot.GetNodeOrNull<Label>(
                path);

        if (!GodotObject.IsInstanceValid(label))
        {
            GD.PushError(
                $"CharacterWindowCharacterTabController could not " +
                $"resolve Label '{path}' beneath " +
                $"'{CharacterTabRoot.GetPath()}'.");
        }

        return label!;
    }


    /// <summary>
    /// Verifies the two Inspector references required by this presentation
    /// controller.
    /// </summary>
    private bool ValidateReferences()
    {
        bool valid = true;

        if (!GodotObject.IsInstanceValid(
            CharacterWindow))
        {
            GD.PushError(
                "CharacterWindowCharacterTabController is missing " +
                "Inspector reference 'CharacterWindow'.");

            valid = false;
        }

        if (!GodotObject.IsInstanceValid(
            CharacterTabRoot))
        {
            GD.PushError(
                "CharacterWindowCharacterTabController is missing " +
                "Inspector reference 'CharacterTabRoot'.");

            valid = false;
        }

        return valid;
    }
}
