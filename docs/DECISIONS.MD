# Questbar Architectural Decision Log

This file records decisions that should remain stable during the clean rebuild of Questbar. Decisions may be revised deliberately, but they should not be silently changed while implementing unrelated systems.

## ADR-001 — Permanent Gameplay Direction

**Status:** Accepted

Questbar travels and fights from right to left.

* Party formation is on the right side of the game stage.
* Heroes face and adventure toward the left.
* Background layers move from left to right during travel.
* Monsters enter from the left.
* Monsters move toward the right when approaching the party.
* The priority target is the living, visible monster with the greatest global X coordinate.
* Melee heroes move left.
* A melee destination is the target’s X coordinate plus melee range.
* Ranged heroes generally remain toward the right.
* Hero projectiles travel from right to left.
* Melee heroes return toward the right after combat.

Physical Windows desktop placement must remain separate from this gameplay direction.

## ADR-002 — Initial Window Design References

**Status:** Accepted

Questbar uses the following visual design references:

* Reference width: 800 pixels.
* Reference collapsed height: 64 pixels.
* Reference expanded height: 192 pixels.

The initial physical-window test values are:

* Simulated taskbar thickness: 48 pixels.
* Default expanded multiplier: 3.
* Default physical anchor: right.
* Default horizontal offset: 0 pixels.
* Default bottom offset: 0 pixels.

When Questbar expands, its bottom edge remains fixed and its top edge moves upward.

Reference design dimensions and physical Windows window dimensions are separate concepts.

## ADR-003 — User-Controlled Window Placement

**Status:** Accepted

Taskbar detection provides first-launch defaults; saved user width, height, anchor, and offset settings control final placement.

On first launch:

* Detected taskbar thickness provides the default collapsed height.
* Default expanded height is three times the collapsed height.
* Questbar begins near the bottom-right corner of the selected monitor.

The player may later adjust and save:

* Window width.
* Collapsed height.
* Expanded height.
* Selected monitor.
* Left or right screen anchor.
* Horizontal offset.
* Bottom offset.

Placement is stored relative to the selected monitor and anchor rather than primarily as absolute desktop coordinates.

Every loaded or calculated size and position must be clamped to the selected monitor’s usable rectangle.

Manual placement settings replace the need for taskbar-icon, pinned-button, and running-application collision detection unless that feature is deliberately reconsidered later.

## ADR-004 — Windows Host Responsibility

**Status:** Accepted

`DesktopWindowHostController` owns:

* Native window behavior.
* Window size.
* Window position.
* Collapsed and expanded modes.
* Monitor selection.
* Physical screen anchoring.
* Placement offsets.
* Taskbar measurement and integration.
* Final placement clamping.
* Always-on-top behavior.
* Borderless behavior.

It does not own:

* Background scrolling.
* Gameplay direction.
* Hero movement.
* Monster spawning.
* Target selection.
* Combat.
* Inventory.
* Equipment.
