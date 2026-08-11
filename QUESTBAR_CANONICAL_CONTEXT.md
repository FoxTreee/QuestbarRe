# Questbar — Canonical Design, Architecture, and Development Context

> **Purpose:** This is the consolidated source of truth for Questbar. It replaces the need to consult both `QUESTBAR_CONTEXT.md` and `DECISIONS.MD` for ordinary development work.
>
> **Conflict rule:** If a proposed change conflicts with an accepted decision here, stop and discuss the conflict before changing the architecture.
>
> **Accuracy rule:** Current project files and verified runtime behavior outrank this document when implementation details have changed. Never guess when the current `.cs`, `.tscn`, `.tres`, logs, screenshots, or project ZIP can answer the question.
>
> **Maintenance rule:** Update this file at meaningful checkpoints. Mark decisions as amended or superseded; do not silently rewrite history.

---

# 1. Status Vocabulary and Source Priority

Every mechanic or architectural statement should use one of these statuses:

- **Implemented and verified:** Present in the current working project and tested by the user.
- **Implemented, awaiting verification:** Code/checkpoint exists, but the user has not yet confirmed the runtime test.
- **Accepted design:** The behavior is settled, even if implementation is incomplete.
- **Deferred/open:** Direction may exist, but important rules or values remain undecided.
- **Historical:** Useful context, not necessarily the current implementation.

When sources disagree, use this priority:

1. Current files and observed runtime behavior.
2. The latest explicit decision in the current conversation.
3. Accepted decisions in this document.
4. Latest verified milestone/checkpoint.
5. Older conversation recollection.
6. Assumptions—which must be labeled and verified.

---

# 2. Project Identity and Product Vision

## Technology

- Godot 4, C#/.NET/Mono.
- Development has used Godot 4.7.x.
- Windows is the primary platform.

## Product concept

Questbar is a desktop-integrated idle party RPG designed to live along the Windows taskbar. It should remain glanceable while the player works, but provide a deeper expanded view for party management, combat inspection, and progression.

The long-term experience combines:

- passive regional travel;
- encounters and multi-monster party combat;
- a five-hero party with distinct classes and roles;
- WoW-Vanilla-inspired dungeon-party synergy;
- threat, tanking, healing, damage, crowd control, and stance-based autonomous decisions;
- persistent heroes, equipment, inventory, progression, and abilities;
- data-driven monsters, encounters, regions, heroes, classes, and abilities;
- collapsed and expanded desktop presentation;
- a world that feels active without requiring constant input.

The player’s central combat problem is not direct unit control. It is building and tuning a five-hero group whose roles, gear, abilities, threat, and AI behaviors work together. Easy encounter zones provide grinding and preparation; harder dungeon-style encounters test whether the party actually synergizes.

## Quality bar

The guiding question is:

> Will this still be understandable and maintainable in two years, and is it something worth selling?

Optimize for clear ownership, testability, extensibility, data-driven authoring, useful debugging, and predictable Godot behavior. Maintainability and correctness beat short-term speed.

---

# 3. Required Development Workflow

Questbar is developed collaboratively and educationally. Avoid large unexplained rewrites.

For architecture-sensitive work:

1. Identify the exact problem.
2. Identify the system that should own the solution.
3. Explain the data flow and relevant Godot/C# concept.
4. Inspect current files before relying on remembered implementation details.
5. Name the exact files, classes, methods, and Inspector references involved.
6. State whether code is added, replaced, moved, or removed.
7. Implement one narrow, testable checkpoint.
8. Preserve existing behavior not included in the checkpoint.
9. Test and debug the checkpoint before layering on another feature.
10. Stop for design review after a successful checkpoint.

## Scene-edit rule

Do not modify `.tscn` scene files directly unless the user explicitly asks. When a scene edit is required, provide:

- an exact scene tree;
- the Godot node type beside every node;
- the script attachment;
- every Inspector property/reference to set;
- clear save and runtime-test steps.

Example:

```text
HeroActor                         [Node2D]
└── VisualRoot                   [Node2D]
    └── HeroResourceBar          [Node2D]
```

## Change-safety rules

- Identify current ownership and dependencies before changing a working system.
- Inspect exported references, NodePaths, event subscriptions, and transform inheritance.
- Move ownership first, verify it, then add complexity.
- Never allow two unrelated scripts to continuously control the same transform, position, viewport size, background offset, or authoritative state.
- Preserve unrelated user changes in a dirty working tree.
- Keep useful debug tools until a production replacement exists.

## Checkpoints and commits

Preferred commit format:

```text
Milestone: <Architectural Checkpoint>

Implemented
- completed and verified behavior

This establishes the foundation for
- next capability
```

Commits should represent meaningful, testable checkpoints—not arbitrary coding sessions.

---

# 4. Core Architectural Decisions

## ADR-001 — Permanent gameplay direction

**Status: Accepted**

Questbar travels and fights from right to left:

- party formation is on the right;
- heroes face/adventure toward the left;
- monsters originate on the left and approach toward the right;
- melee heroes move left to engage and return right after combat;
- ranged heroes generally hold formation;
- hero projectiles travel right-to-left;
- travel backgrounds scroll rightward.

Physical Windows screen anchoring is independent of gameplay direction.

## ADR-002 — Stable logical gameplay space

**Status: Accepted**

- Reference width: `800` logical pixels.
- Collapsed reference height: `64` logical pixels.
- Expanded logical gameplay height: `192` logical pixels.
- Logical bottom baseline: `Y = 192`.

Gameplay coordinates, movement distances, ranges, spawn points, and anchors live in this stable logical world. Physical window dimensions and presentation scaling must not redefine gameplay geometry.

## ADR-003 — Fixed-bottom window expansion

**Status: Accepted**

The physical window’s bottom edge stays fixed. Expansion moves the top edge upward; it does not grow downward over the taskbar.

## ADR-004 — User-controlled window placement

**Status: Accepted, amended from the original collision-first roadmap**

Taskbar detection supplies first-launch defaults. Saved user settings control final placement:

- width;
- collapsed and expanded height;
- selected monitor;
- left/right anchor;
- horizontal offset;
- bottom offset.

Placement is stored relative to the selected monitor/anchor and clamped to its usable rectangle. Manual placement reduces the need for complex taskbar-button collision detection unless that feature is deliberately revived later.

## ADR-005 — Windows host ownership

**Status: Accepted**

`DesktopWindowHostController` owns native Windows behavior:

- size and position;
- collapsed/expanded mode;
- monitor and anchor selection;
- taskbar measurement;
- offsets and clamping;
- borderless and always-on-top behavior.

It does not own gameplay direction, backgrounds, actor movement, combat, targeting, inventory, or content.

## ADR-006 — Gameplay and presentation coordinates are separate

**Status: Accepted**

Presentation may scale, clip, reveal, or frame the logical world, but gameplay logic must remain stable. Never fix a presentation bug by moving gameplay anchors unless the gameplay design itself changed.

## ADR-007 — Background and ground scaling ownership

**Status: Accepted**

- Background/sky presentation must not inherit miniature gameplay scaling.
- Backgrounds remain visually stable, bottom-anchored, clipped by the native window, and reveal more content upward as the window expands.
- Full-width ground must remain outside actor-miniature scaling when it is intended to span the window.
- Screen-space fog, tint, and vignette belong in screen presentation, typically a `CanvasLayer`.

## ADR-008 — Profiles own values; controllers own behavior

**Status: Accepted**

Examples:

- definitions/resources store authored values;
- `HeroCombatProfile` and `MonsterCombatProfile` store resolved gameplay values;
- actor controllers execute local behavior;
- combat/encounter controllers orchestrate systems.

Do not put orchestration inside data resources or stat containers.

## ADR-009 — Runtime state has explicit owners

**Status: Accepted**

- Maximum health comes from content/resolved profiles.
- `CombatHealthState` owns runtime current health.
- Hero runtime state owns current/max class resource and combo points.
- State objects report facts; orchestration systems decide the broader consequences.

## ADR-010 — Gameplay announces facts; presentation reacts

**Status: Accepted**

Gameplay determines and announces completed facts such as damage, impact, incapacitation, resource spending, or combo gain. Presentation listens and visualizes them. Presentation must not determine authoritative outcomes.

## ADR-011 — Heroes persist and incapacitate

**Status: Accepted and implemented**

Heroes are persistent party members. They become incapacitated instead of being destroyed like ordinary monsters. Living actors retarget appropriately, and recovery decisions occur through the incapacitation/revival flow.

## ADR-012 — Systems are generic and content is data-driven

**Status: Accepted**

> Build systems once; feed them data forever.

New monsters, encounters, regions, heroes, classes, resources, abilities, targeting preferences, and selection styles should normally be data/resources, not new controller branches.

## ADR-013 — Target preference and target selection style are different concepts

**Status: Accepted design**

- **Target preference** answers *who is eligible or desirable*, using composable combat tags such as `Melee`, `Ranged`, `Healer`, `Caster`, `Tank`, or `Any`.
- **Target selection style** answers *how one target is chosen* among eligible candidates, such as random, nearest, lowest health, or highest threat.

Do not collapse these into one enum. A monster might prefer healers but choose the nearest healer, or accept anyone and choose the highest-threat target.

## ADR-014 — Encounter definitions sit above monster definitions

**Status: Accepted and implemented foundation**

- `MonsterDefinition` owns one monster type’s stats, abilities, tags, targeting, and presentation references.
- `EncounterDefinition` owns composition, count ranges, waves/spawn behavior, and rewards.
- Encounter pools select among encounter definitions using adjustable weights.

Do not put encounter composition into a monster definition.

---

# 5. Runtime Ownership Map

| Responsibility | Owner |
|---|---|
| Traveling/Encounter journey state | `JourneyStateService` |
| Encounter lifecycle and active monster roster | `EncounterController` |
| Encounter composition | `EncounterDefinition` / encounter pool data |
| Combat orchestration and combat facts | `CombatController` |
| Target selection algorithms | `TargetingService` |
| Hero-local state and behavior | `HeroActorController` / hero runtime state |
| Monster-local state and behavior | `MonsterActorController` |
| Runtime health | `CombatHealthState` |
| Resolved hero/monster values | combat profiles |
| Authored monster data | `MonsterDefinition` |
| Monster lookup and creation | `MonsterContentRegistry` / `MonsterFactory` |
| Ability data, cooldown, and resource cost | `AbilityDefinition` and ability runtime |
| Native window behavior | `DesktopWindowHostController` |
| Region travel presentation | `RegionPresentationController` |
| Background/native presentation | dedicated presentation controller/layer |
| Debug commands and in-game logs | debug console/command service |

If new behavior does not fit cleanly, settle ownership before coding it.

---

# 6. Combat Design Truths

## Core combat fantasy

Questbar combat should resemble the decision pressure of a five-player Vanilla WoW dungeon group, expressed through autonomous heroes. Success comes from party construction and synergy:

- a tank must control dangerous enemies and generate enough threat;
- healers must keep the party alive without automatically being safe from aggro;
- damage dealers must balance output, target priority, survival, and threat;
- overpowered heroes or underpowered tanks can destabilize the group;
- easier regional encounters let the player train and gear the party before dungeons.

The combat system must allow failure caused by poor party balance. A Priest should be able to pull aggro through healing or damage if the tank cannot keep up. A tank threat multiplier must help rather than make aggro loss impossible.

## Combat movement

- Attack range is an approach threshold, not a desired orbit distance.
- Actors move closer when out of range; they do not automatically retreat merely because they are closer than maximum range.
- Melee heroes approach and return to formation after combat.
- Ranged heroes usually hold formation.
- Movement and attack presentation must match actual gameplay state.
- Damage/impact occurs at the authored release or impact point, not arbitrarily at animation start.
- Semantic sockets such as `ProjectileOrigin`, `ImpactOrigin`, `WeaponSocket`, or `StatusEffectSocket` are presentation anchors; they do not redefine gameplay roots.

## Damage and direct-threat intent

**Status: Accepted design; exact coefficients remain tunable/open**

- Successful direct damage generates threat.
- Direct damage needs meaningful threat so a damage dealer can choose to stop attacking or peel instead of attacking with no aggro consequence.
- Healing also generates threat; its exact distribution and coefficient remain to be tuned.
- Threat is evaluated per monster, not as a single global aggro score.
- Threat should not automatically decay during ordinary combat unless a later mechanic explicitly changes that rule.
- Threat modifiers belong in data/ability/class definitions rather than hidden class-name branches.

## Target locks and retargeting

- Actors acquire a valid target and keep it while it remains valid.
- When a target dies or becomes incapacitated, actors retarget rather than following it off-screen.
- Heroes clear dead monster targets and return to formation when combat ends.
- Multiple-monster targeting and natural encounter completion are implemented foundations.

## Target selection

Historically supported selection styles include:

- nearest living hero;
- lowest-health living hero;
- random living hero.

`HighestThreatHero` exists in the schema. It must use the real threat system; never silently substitute a fake fallback.

---

# 7. Threat, Taunt, Peeling, and Stances

## Threat model

**Status: Accepted behavioral design; full numeric implementation/tuning is still evolving**

- Every monster maintains its own threat table.
- Damage and healing can add threat.
- Tanks can have threat-generation modifiers, but those modifiers must not guarantee permanent aggro.
- Some abilities can apply explicit threat multipliers or flat threat.
- A monster normally chooses the highest valid threat target after preference/eligibility rules are applied.
- Threat persists during the encounter and is removed/invalidated when participants leave or become invalid.

Open tuning questions that must remain Inspector/data adjustable include damage-to-threat ratio, healing threat, tank multipliers, ranged/melee pull thresholds if used, and ability-specific modifiers.

## Taunt

**Status: Resource cost implemented and verified; exact threat-forcing semantics should remain explicit in current ability data/code**

- Taunt is a Warrior/tank ability.
- It uses Rage.
- Default cost is `25 Rage`.
- `Resource Cost` is adjustable on `ability.core.taunt.tres` through the generic `AbilityDefinition` cost field.
- The hero must be able to afford the ability before beginning it.
- Resource is spent only when the ability successfully commits/resolves according to the ability pipeline; failed attempts must not consume Rage.
- Rage cost is tuning data, not hard-coded Warrior behavior.

When threat behavior is finalized, Taunt must explicitly define whether it sets threat to the current leader plus a margin, temporarily forces target lock, applies bonus threat, or combines these. Do not allow ambiguous hidden behavior.

## Peeling and protective decisions

Heroes need autonomous tools to react when a vulnerable ally is attacked. “Peel” behavior is not the same as ordinary target preference:

- a tank may switch to a monster threatening a healer or fragile ally;
- direct damage and threat generation give that switch a meaningful way to regain aggro;
- taunts or control abilities may be reserved for emergencies;
- the decision should consider current target danger, ally role, distance, threat gap, and ability readiness.

## Stances

**Status: Accepted design direction; exact weights/thresholds remain data-driven and tunable**

Stances do not replace shared intelligent decision-making. They adjust priorities, risk tolerance, and thresholds.

### Passive

- Survival and threat avoidance have highest priority.
- Avoid initiating unnecessary targets.
- Reduce or stop damage when close to pulling aggro.
- Prefer returning to formation/safety when no urgent responsibility exists.
- Use emergency self-preservation and essential role actions conservatively.
- Tanks in Passive still protect the group, but avoid aggressive pickup/chase behavior unless an ally is endangered.

### Defensive

- Balanced default behavior.
- Perform the hero’s role while respecting threat and party safety.
- Tanks peel endangered allies and stabilize loose monsters.
- Damage dealers continue attacking until threat or danger crosses a moderate threshold.
- Healers balance healing urgency, efficiency, and personal safety.

### Aggressive

- Damage, pressure, and rapid target acquisition have higher priority.
- Accept more threat and positional risk.
- Use offensive abilities sooner and tolerate smaller safety margins.
- Tanks proactively pick up enemies and build threat.
- Aggressive does not mean suicidal: invalid targets, incapacitation risk, and essential emergency actions still override offense.

The same decision inputs should be available in all three stances; stance changes their weighting rather than creating three unrelated AI systems.

---

# 8. Health, Incapacitation, Revive, and Defeat

## Health ownership

- Content/profiles provide maximum health.
- `CombatHealthState` owns current health and damage application.
- Health state reports death/incapacitation; it does not destroy actors, complete encounters, or orchestrate party recovery.

## Hero incapacitation

**Status: Implemented and verified foundation**

- Heroes incapacitate at zero health and remain persistent party members.
- Incapacitated heroes stop participating as living combatants.
- Enemies and allies retarget valid living actors.
- Ordinary monsters are removed when defeated; heroes are not.
- Rook’s combo points clear when he becomes incapacitated.

## Incapacitation choice popup

The intended scene ownership is:

```text
Main                                      [Node]
├── Controllers                           [Node]
│   └── IncapacitationChoicePopupController [Node]
└── PopupWindows                          [Node]
    └── IncapacitationChoiceWindow        [Window]
```

The controller script belongs on its own controller node, not on the `Window`. The window is presentation and remains scriptless unless architecture is deliberately changed.

## Revive/reset truths

- Revival restores the hero to a valid active state.
- The generic class-resource pool refills on revival and debug/full reset.
- Full hero/run reconfiguration may clear transient class mechanics such as combo points.
- The exact long-term cost/tradeoff between “revive” and “incapacitate” choices should remain documented in the current implementation/content; do not invent it from memory.

## Combat resolution

- Victory occurs when the active monster roster is naturally empty while heroes remain able to continue.
- Defeat occurs when no living heroes remain against active monsters.
- The journey returns to Traveling after resolution according to the encounter/run flow.

---

# 9. Generic Class Resource System

## Resource types

**Status: Implemented and verified through Checkpoint 21C**

```text
None
Mana
Energy
Rage
```

The system is class-agnostic and data-driven. Every hero runtime can own:

- resource type;
- current amount;
- maximum amount;
- starting state;
- regeneration amount;
- regeneration tick interval;
- safe affordability/spending/restoration operations.

Resource spending is atomic and abilities must check affordability before casting. Failed ability attempts do not spend resources.

## Class assignments

| Class | Resource | Current rules |
|---|---|---|
| Rogue | Energy | Maximum 100, starts full, +10 every 2 seconds |
| Warrior | Rage | Maximum 100, starts full, +10 every 2 seconds |
| Priest | Mana | Starts at 100 in current foundation; regeneration/scaling not yet designed |
| Mage | Mana | Starts at 100 in current foundation; regeneration/scaling not yet designed |
| Hunter | Mana | Starts at 100 in current foundation; regeneration/scaling not yet designed |

## Fixed vs scalable resources

- Energy normally stays capped at `100`; it does not increase through ordinary levels, stats, or gear.
- Rage currently follows the same fixed-100 foundation.
- A future endgame skill may increase Energy, but that is not ordinary scaling and must be an explicit modifier.
- Mana is intended to be the scalable resource, but its class-specific maximum, stat scaling, regeneration, and costs remain deferred/open.

## Regeneration

- Energy: discrete `+10` ticks every `2` seconds, capped at maximum.
- Rage: currently discrete `+10` ticks every `2` seconds, capped at maximum.
- Mana: supported by the generic system, but no accepted regeneration rule yet.
- Energy and Rage begin full and remain full until an ability spends them.

## Resource bars

**Status: Implemented and verified through Checkpoint 21B/21C**

- One reusable `HeroResourceBar` scene sits beneath the health bar.
- It hides for `None`.
- Mana is blue, Energy yellow, Rage red.
- The bar reads generic current/max state rather than class-specific fields.
- It is hero-specific today but remains reusable for future companions, summons, previews, or resource-bearing enemies.

---

# 10. Rogue / Rook Design

## Identity

- Hero ID: `hero.core.rook`.
- Class ID: `class.core.rogue`.
- Rogues use Energy, not Mana.

## Combo points

**Status: Checkpoint 21D implemented; runtime verification should be recorded after testing**

- A successful basic attack that actually deals damage grants one combo point.
- Attack animation release alone does not grant a point.
- Misses, dodges, zero-damage results, poison ticks, and non-basic damage do not grant a point unless a future ability explicitly says otherwise.
- Combo points cap at `5`.
- Combo state lives on the hero runtime, not the encounter.
- Combo points persist between encounters and through regrouping.
- Encounter completion does not clear them.
- Incapacitation and full hero/run reset clear them.
- Non-Rogues hide the display automatically.

## Combo-point presentation

- Five small squares sit beneath the Energy bar.
- Squares fill individually from 0 to 5.
- The display is a reusable `HeroComboPointDisplay` scene, not five one-off nodes embedded in gameplay logic.
- Presentation reads runtime combo state; it does not award or consume points.

## Sinister Strike

**Status: Accepted design; implementation follows verified combo-point checkpoint**

- Automatically becomes usable at 5 combo points.
- Requires a valid target, cooldown ready, and sufficient Energy.
- Default cooldown: `5 seconds`.
- Default Energy cost: `25`.
- Energy cost uses the generic adjustable `AbilityDefinition.ResourceCost` field so it can be tuned in the Inspector.
- Damage is `200%` of Rook’s normal/basic attack damage—twice normal damage, not normal plus an additional 200%.
- When Rook commits to using Sinister Strike, Energy and all 5 combo points are consumed immediately.
- The cooldown begins on committed use.
- If the target dodges or the attack misses, damage is zero and the spent Energy/combo points remain lost.
- Sinister Strike does not grant a replacement combo point.

The consumption sequence is authoritative:

```text
5 combo points + enough Energy + cooldown ready
    → commit Sinister Strike
    → spend Energy
    → reset combo points to 0
    → start cooldown
    → resolve hit / miss / dodge
    → apply 200% damage only on a successful hit
```

---

# 11. Abilities and Cooldowns

## Generic ability truths

- Abilities are data-driven through `AbilityDefinition` resources.
- Resource cost is generic; the ability does not need Rogue-, Warrior-, Mana-, Energy-, or Rage-specific spending code.
- Automatic ability logic checks target validity, range, cooldown, and affordability before beginning.
- Resource spending, cooldown start, damage resolution, and presentation events must have explicit timing.
- Ability definitions may later carry threat modifiers, targeting rules, tags, cast time, release point, and presentation data.

## Known ability content

- `ability.core.heavy_slam`: Heavy Slam; known prototype values included 6-second cooldown, 1.6-second cast, 45 range, 45 damage, current-target selection.
- `ability.core.taunt`: Warrior Taunt; adjustable Rage cost, default 25.
- Sinister Strike: Rogue finisher; accepted values are 200% basic damage, 25 Energy default, 5-second cooldown, consumes combo points and Energy on use even if avoided.

Always inspect the current `.tres` files before treating remembered prototype numbers as current authored data.

---

# 12. Encounter and Content Architecture

## Travel/combat loop

**Status: Implemented foundation**

```text
Travel
  → encounter selected
  → monsters spawn
  → combat begins
  → actors attack, heal, threaten, and incapacitate/die
  → encounter resolves
  → return to travel
```

Multiple monsters, participant refresh, retargeting, and natural encounter completion are implemented foundations. Future waves reuse this system instead of creating a separate combat mode.

## Definitions and registries

- `MonsterDefinition`: one monster type’s authored stats, abilities, tags, targeting, movement, rewards, and presentation references.
- `EncounterDefinition`: composition/count ranges, spawn/wave rules, and rewards.
- Encounter pool: weighted selection among encounter definitions; weights and monster counts should be easy to tune in the Inspector/data.
- `MonsterContentRegistry`: content lookup.
- `MonsterFactory`: validated runtime creation.

## Content IDs

Use stable, lowercase, period-prefixed hierarchy:

```text
<category>.<namespace>.<name>
```

Examples:

```text
monster.core.training_monster
monster.core.heavy_training_monster
hero.core.rook
class.core.rogue
ability.core.taunt
```

Display-name changes do not rename stable IDs. Underscores are allowed inside a segment.

## Debug/admin commands

Use a period-prefixed action plus stable ID:

```text
.startEncounter <ENCOUNTER_ID>
.startEncounterPool <POOL_ID>
.addItem <ITEM_ID>
.revive <HERO_ID>
```

Commands should be human-readable, consistent, ID-driven, documented in `.help`, and support deliberate sequential chaining where implemented.

## Validation

Fail clearly during development for:

- duplicate/invalid IDs;
- missing scenes or definitions;
- invalid numeric ranges;
- missing dependencies/references;
- unsupported targeting/ability modes;
- invalid output paths;
- incomplete required gameplay fields.

Never silently substitute an unsupported feature with a different behavior.

---

# 13. Logging, Console, and Inspector Documentation

## Logging

- Runtime gameplay logs should appear in the custom in-game console rather than relying on scattered `GD.Print()` output.
- Logs should explain decisions, not only outcomes.

Example:

```text
Monster selected LowestHealthHero: HeroActor2, HP=31/100.
Rook gained a combo point. Combo=3/5.
```

- High-volume logs use bounded storage with chunk deletion rather than repeatedly removing one entry at a time. When the cap is reached, delete a large oldest block so stress cleanup does not freeze the game.

## Inspector documentation

- GDScript `##` export documentation does not apply to C# exports.
- Questbar uses an editor-only Inspector Help plugin backed by property descriptions.
- The plugin covers exported C# properties with units, effects, examples, and cautions.
- It must not affect exported gameplay behavior.
- Keep its property mapping synchronized when exports are added/renamed.

---

# 14. Presentation and Scene Truths

## Node and transform principles

- `Node` has no 2D transform; scripts inheriting `Node2D` must attach to `Node2D` nodes.
- `Node2D` is for world-space transforms.
- `Control` is for UI layout/anchors.
- `CanvasLayer` is for screen-space content independent of world transforms.
- Parenting is functional: children inherit transforms.
- Do not mix `Control` layout and `Node2D` transforms accidentally.
- The runtime Remote scene tree and runtime Inspector values are authoritative when editor and runtime disagree.

## Actor UI scenes

- Health bar is reusable.
- Resource bar is a standalone reusable scene, not embedded one-off UI.
- Combo-point display is a standalone reusable scene.
- Resource and combo references are assigned once on the shared `HeroActor.tscn`, fixing all hero instances.

## World presentation

- Background presentation is separate from scaled gameplay actors.
- Ground remains full-width when intended.
- Collapsed presentation may miniaturize actors while keeping the logical world intact.
- Expanded/collapsed transitions must be fluid and must not cause events to snap actors to incorrect scale.
- Actors remain within authored ground top/bottom boundaries.

## Anti-regression rules

- Do not resize the logical viewport to native dimensions.
- Do not change project stretch, viewport stretch, and multiple transforms in one checkpoint.
- Do not compensate for ancestor stretch by blindly scaling a child sprite.
- Do not assume `Process Mode = Disabled` removes event callbacks.
- Do not allow multiple presentation controllers to manipulate the same frame.
- Do not move gameplay anchors to fix visual framing.

---

# 15. Windows Host and Platform Roadmap

## Verified foundation

- Native topmost behavior works during ordinary app switching/maximizing/typing.
- Borderless taskbar-adjacent presentation exists.
- Actual taskbar/tray-area detection has been developed.
- Legacy fixed placement values remain a fallback until adaptive placement is proven.

## Platform roadmap

1. W1 — enforce native topmost behavior without changing placement. **Verified.**
2. W2 — read the actual Windows taskbar rectangle.
3. W3 — detect the tray/notification area and place Questbar immediately to its left.
4. W4 — detect collisions with taskbar buttons if still needed after user-placement design.
5. W5 — shift, shrink, compact/icon, or above-taskbar fallback policies.
6. W6 — centered/left layouts, multiple monitors, DPI scaling, auto-hide, Explorer restarts.
7. W7 — placement/collision settings and complete platform testing.

Reconcile W3–W5 with user-controlled placement before reviving complex automatic collision behavior.

---

# 16. Stable Milestones and Current Progress

## Verified broad foundations

- travel → encounter → combat → resolution → travel;
- health/damage architecture;
- persistent hero incapacitation;
- retargeting after target death/incapacitation;
- multiple monsters and natural encounter completion;
- data-driven monster definitions, registry, factory, targeting, encounter pools;
- random grid monster spawning;
- structured in-game logging and command console;
- native Windows topmost behavior;
- stable logical gameplay coordinate recovery;
- bottom-anchored background presentation;
- actor scaling and ground containment;
- generic resources (`None`, `Mana`, `Energy`, `Rage`);
- reusable resource bars and class assignments;
- generic adjustable ability resource costs;
- Taunt spending Rage;
- Inspector Help plugin for C# exported properties.

## Current rogue checkpoint chain

- **21A verified:** generic resource foundation.
- **21B verified:** reusable resource-bar UI and colors.
- **21C verified:** real class assignments and adjustable Taunt Rage cost.
- **21D implemented, verification pending in this record:** persistent Rogue combo points and five-square display.
- **21E next:** Sinister Strike using the accepted consumption/miss rules.

---

# 17. Deferred or Open Design

Do not present these as settled or fully implemented:

- exact threat coefficients and pull thresholds;
- final Taunt threat-forcing semantics;
- exact stance scoring weights and thresholds;
- Mana maximums, stat scaling, regeneration, and ability costs;
- whether Hunter permanently remains a Mana class after later class design review;
- future endgame Energy-increase skill details;
- full healing spell/resource design;
- dodge/miss formulas and avoidance stat ownership;
- full encounter wave/boss entrance system;
- final region definitions, weather, parallax, fog, vignette, and background-fit policy;
- final hover expansion behavior;
- advanced adaptive taskbar collision;
- full item/equipment/progression pipeline;
- server-authoritative networking.

When one of these is designed, record the behavioral truth, ownership, tunable values, and implementation status here.

---

# 18. Questions Before Adding or Moving a System

Before coding:

1. What problem does this solve?
2. Who owns the authoritative state?
3. Is there already an owner?
4. Is it gameplay, presentation, content, platform, UI, or tooling?
5. Is it actor-local or orchestration?
6. Does it need persistence?
7. Does it belong in a Resource/profile/runtime state/controller?
8. Is it physical-pixel or logical-gameplay data?
9. Can more content use it without more controller branches?
10. Can it be tested independently in one checkpoint?
11. Does it create a second owner for existing state or transforms?

Before reparenting a node, also inspect transform inheritance, exported references, relative paths, Node2D/Control boundaries, clipping, and whether it should scale with gameplay or native presentation.

---

# 19. Final Non-Negotiable Invariants

1. Gameplay direction remains right-to-left unless deliberately redesigned.
2. Logical gameplay coordinates remain `800 × 192`; logical bottom is `Y=192`.
3. The physical window’s bottom stays fixed and expansion occurs upward.
4. Physical window dimensions do not redefine gameplay geometry.
5. `DesktopWindowHostController` owns native Windows behavior.
6. Background and full-width ground do not inherit actor miniaturization unintentionally.
7. `TargetingService` owns target selection algorithms.
8. `CombatController` owns combat orchestration.
9. `EncounterController` owns encounter roster/spawn/completion.
10. Actor controllers own actor-local behavior.
11. Definitions/profiles own authored/resolved values; runtime state owns current values.
12. Heroes incapacitate and persist; they are not disposable monster actors.
13. Target preference and target selection style remain separate.
14. Encounter definitions remain above monster definitions.
15. New content normally means data, not controller branches.
16. Resource costs are generic, adjustable ability data.
17. Energy and Rage are fixed-100 foundations unless an explicit modifier says otherwise; Mana scaling remains separately designed.
18. Rogue combo points persist between encounters and are consumed on committed Sinister Strike use even if it misses/dodges.
19. Unsupported modes fail clearly; they never silently fall back.
20. Gameplay announces facts; presentation reacts.
21. One narrow checkpoint is implemented and tested before stacking another.
22. Scene edits are walked through manually with node types unless explicitly authorized otherwise.
23. Never let two systems silently fight over the same transform or authoritative state.
24. Never guess when current files can provide the truth.
25. Maintainability, correctness, and understandability beat speed.

---

# 20. Maintenance Log

## 2026-08-11 — Consolidated canonical edition

- Consolidated `QUESTBAR_CONTEXT(1).md` and `DECISIONS.MD`.
- Removed the duplicate historical copy of ADR-001 through ADR-004.
- Combined repeated ownership, presentation, workflow, and anti-regression rules.
- Added accepted/current design for class resources, Rage, Energy, Mana foundation, Taunt, Rook combo points, Sinister Strike, incapacitation/revival, threat goals, peeling, and Passive/Defensive/Aggressive stances.
- Distinguished verified implementation from accepted design and deferred tuning decisions.

---

**End of canonical Questbar context.**
