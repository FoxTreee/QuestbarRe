using Godot;
using System;

public partial class CharacterWindowController : Node
{
    [ExportCategory("Dependencies")]

    /// <summary>
    /// Runtime party whose five authored slots drive the character selector.
    /// </summary>
    [Export]
    public PartyController Party { get; set; } = null!;


    [ExportCategory("Character Header")]

    /// <summary>
    /// Displays the currently selected hero's authored display name.
    /// Level remains a placeholder until Questbar has authoritative level data.
    /// </summary>
    [Export]
    public Label SelectedHeroLabel { get; set; } = null!;


    [ExportCategory("Party Selector")]

    [Export]
    public Button PartySlot1Button { get; set; } = null!;

    [Export]
    public Button PartySlot2Button { get; set; } = null!;

    [Export]
    public Button PartySlot3Button { get; set; } = null!;

    [Export]
    public Button PartySlot4Button { get; set; } = null!;

    [Export]
    public Button PartySlot5Button { get; set; } = null!;


    /// <summary>
    /// Runtime hero currently selected by the character-management window.
    /// Character, Skills, Reputation, equipment, stats, and preview systems
    /// should consume this shared context rather than choosing independently.
    /// </summary>
    public HeroActorController? SelectedHero
    {
        get;
        private set;
    }

    /// <summary>
    /// Zero-based authored party slot occupied by SelectedHero.
    /// </summary>
    public int SelectedPartySlotIndex
    {
        get;
        private set;
    } = -1;

    /// <summary>
    /// Raised whenever the player changes the hero-management context.
    /// Later Character, Skills, Reputation, equipment, and preview bindings
    /// can listen to this without owning selection themselves.
    /// </summary>
    public event Action<HeroActorController>?
        SelectedHeroChanged;


    private Button[] _partyButtons =
        Array.Empty<Button>();

    private Action[] _pressedHandlers =
        Array.Empty<Action>();


    /// <summary>
    /// Connects the five selector buttons to the real runtime party and waits
    /// for PartyController to finish its deferred spawn when necessary.
    /// </summary>
    public override void _Ready()
    {
        if (!ValidateReferences())
            return;

        _partyButtons =
        [
            PartySlot1Button,
            PartySlot2Button,
            PartySlot3Button,
            PartySlot4Button,
            PartySlot5Button
        ];

        _pressedHandlers =
            new Action[_partyButtons.Length];

        for (int slotIndex = 0;
            slotIndex < _partyButtons.Length;
            slotIndex++)
        {
            int capturedSlotIndex =
                slotIndex;

            Action handler =
                () => SelectPartySlot(
                    capturedSlotIndex);

            _pressedHandlers[slotIndex] =
                handler;

            Button button =
                _partyButtons[slotIndex];

            button.ToggleMode =
                true;

            button.Pressed +=
                handler;
        }

        Party.PartySpawned +=
            OnPartySpawned;

        if (Party.SpawnedHeroCount > 0)
        {
            RefreshPartySelector();
        }
        else
        {
            ShowWaitingState();
        }
    }


    /// <summary>
    /// Disconnects selector and party events owned by this controller.
    /// </summary>
    public override void _ExitTree()
    {
        if (GodotObject.IsInstanceValid(Party))
        {
            Party.PartySpawned -=
                OnPartySpawned;
        }

        int connectionCount =
            Mathf.Min(
                _partyButtons.Length,
                _pressedHandlers.Length);

        for (int index = 0;
            index < connectionCount;
            index++)
        {
            Button button =
                _partyButtons[index];

            Action handler =
                _pressedHandlers[index];

            if (GodotObject.IsInstanceValid(button)
                && handler != null)
            {
                button.Pressed -=
                    handler;
            }
        }
    }


    /// <summary>
    /// Refreshes the selector after the equipped party has finished spawning.
    /// </summary>
    private void OnPartySpawned(
        int heroCount)
    {
        RefreshPartySelector();
    }


    /// <summary>
    /// Reads all five authored party slots, labels occupied buttons with the
    /// actual hero DisplayName, disables empty slots, and preserves selection
    /// when possible.
    /// </summary>
    private void RefreshPartySelector()
    {
        for (int slotIndex = 0;
            slotIndex < _partyButtons.Length;
            slotIndex++)
        {
            HeroActorController? hero =
                Party.GetHeroAtSlot(
                    slotIndex);

            Button button =
                _partyButtons[slotIndex];

            if (!GodotObject.IsInstanceValid(hero))
            {
                button.Text =
                    "Empty";

                button.TooltipText =
                    $"Party Slot {slotIndex + 1} is empty.";

                button.Disabled =
                    true;

                button.ButtonPressed =
                    false;

                continue;
            }

            string displayName =
                GetHeroDisplayName(
                    hero!);

            button.Text =
                $"{displayName}\nLvl —";

            button.TooltipText =
                hero!.Definition?.ContentId
                ?? displayName;

            button.Disabled =
                false;
        }

        if (SelectedPartySlotIndex >= 0
            && GodotObject.IsInstanceValid(
                Party.GetHeroAtSlot(
                    SelectedPartySlotIndex)))
        {
            SelectPartySlot(
                SelectedPartySlotIndex);

            return;
        }

        SelectFirstOccupiedPartySlot();
    }


    /// <summary>
    /// Selects the hero occupying a zero-based authored party slot.
    /// Empty/invalid slots cannot become the character-management context.
    /// </summary>
    public bool SelectPartySlot(
        int slotIndex)
    {
        HeroActorController? hero =
            Party.GetHeroAtSlot(
                slotIndex);

        if (!GodotObject.IsInstanceValid(hero))
            return false;

        SelectedHero =
            hero;

        SelectedPartySlotIndex =
            slotIndex;

        string displayName =
            GetHeroDisplayName(
                hero!);

        SelectedHeroLabel.Text =
            $"{displayName} — Level —";

        for (int buttonIndex = 0;
            buttonIndex < _partyButtons.Length;
            buttonIndex++)
        {
            _partyButtons[buttonIndex]
                .ButtonPressed =
                    buttonIndex == slotIndex;
        }

        DebugLog.Print(
            $"Character window selected party slot " +
            $"{slotIndex + 1}: " +
            $"{hero!.Definition?.ContentId ?? "(unknown)"} " +
            $"('{displayName}').");

        SelectedHeroChanged?.Invoke(
            hero!);

        return true;
    }


    /// <summary>
    /// Selects the first occupied runtime party slot after party spawn.
    /// </summary>
    private void SelectFirstOccupiedPartySlot()
    {
        for (int slotIndex = 0;
            slotIndex < PartyController.MaximumPartySize;
            slotIndex++)
        {
            if (SelectPartySlot(
                slotIndex))
            {
                return;
            }
        }

        SelectedHero =
            null;

        SelectedPartySlotIndex =
            -1;

        SelectedHeroLabel.Text =
            "No Hero Selected";
    }


    /// <summary>
    /// Shows a neutral state while PartyController's deferred spawn is pending.
    /// </summary>
    private void ShowWaitingState()
    {
        SelectedHeroLabel.Text =
            "Loading Party…";

        for (int slotIndex = 0;
            slotIndex < _partyButtons.Length;
            slotIndex++)
        {
            Button button =
                _partyButtons[slotIndex];

            button.Text =
                $"Party Slot {slotIndex + 1}";

            button.Disabled =
                true;

            button.ButtonPressed =
                false;
        }
    }


    /// <summary>
    /// Resolves a player-facing name from authored hero data, falling back to
    /// the runtime node name only when the definition is unavailable.
    /// </summary>
    private static string GetHeroDisplayName(
        HeroActorController hero)
    {
        string? displayName =
            hero.Definition?.DisplayName;

        return string.IsNullOrWhiteSpace(
            displayName)
            ? hero.Name.ToString()
            : displayName;
    }


    /// <summary>
    /// Verifies all Inspector references required by the selector.
    /// </summary>
    private bool ValidateReferences()
    {
        bool valid = true;

        valid &=
            Require(
                Party,
                nameof(Party));

        valid &=
            Require(
                SelectedHeroLabel,
                nameof(SelectedHeroLabel));

        valid &=
            Require(
                PartySlot1Button,
                nameof(PartySlot1Button));

        valid &=
            Require(
                PartySlot2Button,
                nameof(PartySlot2Button));

        valid &=
            Require(
                PartySlot3Button,
                nameof(PartySlot3Button));

        valid &=
            Require(
                PartySlot4Button,
                nameof(PartySlot4Button));

        valid &=
            Require(
                PartySlot5Button,
                nameof(PartySlot5Button));

        return valid;
    }


    /// <summary>
    /// Reports a missing Inspector dependency without allowing partial setup.
    /// </summary>
    private static bool Require(
        GodotObject value,
        string propertyName)
    {
        if (GodotObject.IsInstanceValid(
            value))
        {
            return true;
        }

        GD.PushError(
            $"CharacterWindowController is missing " +
            $"Inspector reference '{propertyName}'.");

        return false;
    }
}
