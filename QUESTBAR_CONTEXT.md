# QUESTBAR_CONTEXT.md

> **Purpose:** This is the durable source-of-truth context for the Questbar project.  
> It exists so development can continue across chats, machines, model sessions, and future milestones without silently losing architectural decisions, workflow rules, coordinate conventions, current system ownership, or completed milestones.
>
> **Primary rule:** If a future implementation conflicts with this document, stop and explicitly discuss the conflict before changing architecture.
>
> **Maintenance rule:** Update this file at meaningful architectural checkpoints. Do not rewrite accepted decisions casually. Mark decisions as superseded or amended instead of silently changing history.

---

# 0. How to Use This File

This file is intended to be uploaded into the Questbar ChatGPT Project and treated as the first source of context for future development conversations.

When starting a new Questbar conversation:

1. Read this file before suggesting architecture-sensitive changes.
2. Preserve accepted architectural boundaries unless the user explicitly chooses to revise them.
3. Treat completed milestones as working foundations that should not be casually destabilized.
4. Ask for current files when a change depends on exact implementation details.
5. Make one small checkpoint at a time.
6. Test before stacking another subsystem.
7. After each successful checkpoint, stop for a design review before continuing.
8. Never invent old decisions, rules, or prior implementation details if they are not present here or in the current files.

This file intentionally separates:

- **Product vision**
- **Stable architectural decisions**
- **Current implementation facts**
- **Development workflow**
- **Completed milestones**
- **Current presentation state**
- **Known deferred decisions**
- **Roadmap / pipeline**
- **Anti-regression rules**
- **Future design ideas**

That distinction matters. An idea is not automatically an accepted architectural rule.

---

# 1. Project Identity

## Project Name

**Questbar**

## Engine / Language

- **Godot 4**
- **C# / .NET**
- Current development has been performed in a Godot 4.7.x Mono environment.

## Product Concept

Questbar is a desktop-integrated idle RPG designed to live along the Windows taskbar area rather than behaving like a conventional full-screen or standalone game window.

The player should be able to glance at Questbar while doing other work. The game is intentionally compact, persistent, visually readable, and integrated into the desktop experience.

The long-term experience combines:

- passive travel
- encounters
- party combat
- progression
- data-driven monsters/content
- region presentation
- equipment/inventory
- desktop-native presentation
- collapsed and expanded taskbar modes
- a persistent game world that feels alive without demanding constant interaction

Questbar should feel like a polished product, not a prototype permanently held together by special cases.

---

# 2. Development Philosophy

The core development question is:

> **“Will I still be happy maintaining this in two years, and is it something I can feel proud to sell?”**

Architecture should optimize for:

- clarity
- stable ownership boundaries
- testability
- extensibility
- data-driven content
- minimal hidden coupling
- predictable Godot scene behavior
- reusable systems
- future server-authoritative possibilities
- ease of debugging
- ease of content authoring

Speed is secondary to correctness and maintainability.

Do not trade architecture for short-term convenience unless the tradeoff is explicitly discussed and accepted.

---

# 3. Collaboration / ChatGPT Development Workflow

Questbar development is intentionally collaborative and educational.

The user does **not** want large unexplained code dumps or rapid multi-system rewrites.

## Required workflow

For architecture-sensitive work:

1. Identify the exact problem.
2. Identify which object/system should own the solution.
3. Explain the data flow.
4. Explain the relevant Godot concept.
5. Explain runtime vs editor behavior when relevant.
6. Explain coupling / maintenance consequences.
7. Give the exact filename.
8. Give the exact method / class / location.
9. Clearly state whether code is:
   - added
   - replaced
   - deleted
   - moved
10. Make one small checkpoint.
11. Test it.
12. Stop.
13. Ask whether there are design ideas to consider before the next checkpoint.

## Important communication preferences

- Accuracy over guessing.
- If uncertain, inspect current files.
- Never pretend to know current code when it has changed.
- Preserve working features.
- Prefer deliberate migrations over broad refactors.
- Explain why a change is architecturally correct.
- Keep code maintainable and readable.
- Small educational Godot/C# tidbits are welcome.
- Scene-tree diagrams should always include the **node type next to each rung**.

Example:

```text
Main                         [Node]
└── RegionViewportContainer  [SubViewportContainer]
    └── RegionViewport       [SubViewport]
```

## When changing a working system

Before changing it:

- identify who currently owns it
- identify dependencies
- identify existing NodePaths / exported references
- identify what code continuously controls transforms or state
- migrate only one ownership responsibility at a time
- remove the old behavior only after the replacement works

**Never allow two independent scripts to continuously control the same transform, position, viewport size, background offset, or equivalent property unless that coordination is deliberate.**

This rule was reinforced by the world-presentation debugging work, where overlapping controllers caused confusing scaling and movement.

---

# 4. Git / Milestone Workflow

Questbar uses checkpoint-style commits.

Preferred commit format:

**Title**

```text
Milestone: <Architectural Checkpoint>
```

**Description**

```text
Implemented
- what was completed
- what was verified

This establishes the foundation for
- future capability
- future capability
```

Commits should correspond to meaningful, testable architectural checkpoints rather than arbitrary coding sessions.

Completed features should be preserved unless a deliberate architectural migration is underway.

A rollback ZIP exists from the milestone:

```text
Milestone: Random grid monster spawning
```

That rollback was used to recover the stable 800×192 logical gameplay coordinate system after experimental presentation work destabilized viewport behavior.

---

# 5. Architectural Decision Log

The original `DECISIONS.MD` established ADR-001 through ADR-004. Those decisions are preserved below and expanded with additional accepted architectural decisions that became clear during subsequent development.

---

## ADR-001 — Permanent Gameplay Direction

**Status:** Accepted

Questbar travels and fights from **right to left**.

- Party formation is on the right side of the game stage.
- Heroes face and adventure toward the left.
- Background layers move from left to right during travel.
- Monsters enter / originate from the left side.
- Monsters move toward the right when approaching the party.
- A priority monster target historically used greatest global X when choosing the closest front-line monster, although target selection is now data-driven through `TargetingService`.
- Melee heroes move left toward enemies.
- A melee destination is based on the target X plus attack range.
- Ranged heroes generally hold ground toward the right.
- Hero projectiles travel right-to-left.
- Melee heroes return toward the right after combat.

**Critical separation:** Physical Windows desktop placement has nothing to do with gameplay travel direction.

---

## ADR-002 — Initial Window Design References

**Status:** Accepted, with later clarification

Original visual design references:

- Reference width: **800 px**
- Reference collapsed height: **64 px**
- Reference expanded height: **192 px**

Original physical test references:

- Simulated taskbar thickness: 48 px
- Default expanded multiplier: 3
- Default physical anchor: right
- Default horizontal offset: 0
- Default bottom offset: 0

When Questbar expands:

- the **bottom edge remains fixed**
- the **top edge moves upward**
- the window does not expand downward

### Later clarification

The **800×192 region/gameplay coordinate space is now a protected logical coordinate system**.

Physical Windows dimensions may differ because the player may configure width/heights.

The logical gameplay world must not be redefined merely because native window dimensions change.

---

## ADR-003 — User-Controlled Window Placement

**Status:** Accepted

Taskbar detection can provide first-launch defaults, while saved user settings control final placement.

Player-configurable placement includes:

- window width
- collapsed height
- expanded height
- selected monitor
- left/right screen anchor
- horizontal offset
- bottom offset

Placement should be monitor-relative rather than primarily absolute desktop coordinates.

Saved settings currently load from a `user://window_settings.cfg`-style persistent configuration.

### Important lesson

Runtime settings can override scene Inspector defaults.

When diagnosing window dimensions, trust runtime diagnostics over Inspector values if persisted settings are being loaded.

---

## ADR-004 — Windows Host Responsibility

**Status:** Accepted

`DesktopWindowHostController` owns:

- native window behavior
- native window size
- native window position
- collapsed / expanded physical modes
- selected monitor
- physical screen anchoring
- placement offsets
- taskbar integration / measurement
- placement clamping
- always-on-top behavior
- borderless behavior
- native transparency setup where appropriate

It does **not** own:

- background scrolling
- visual parallax
- gameplay direction
- hero movement
- monster spawning
- target selection
- combat
- inventory
- equipment
- region content
- gameplay-world transforms

---

## ADR-005 — Stable Logical Gameplay Coordinate Space

**Status:** Accepted

Questbar gameplay is authored in a stable logical viewport:

```text
Width  = 800
Height = 192
```

Coordinate reference:

```text
(0,0) -------------------------------- (800,0)
  |                                        |
  |                                        |
  |                                        |
(0,192) ============================ (800,192)
```

Therefore:

- top-left = `(0, 0)`
- top-right = `(800, 0)`
- bottom-left = `(0, 192)`
- bottom-right = `(800, 192)`

### Invariant

**Native window resizing must never redefine this logical gameplay coordinate system.**

Existing gameplay coordinates, anchors, movement distances, ranges, spawn positions, and projectile behavior should continue to make sense regardless of physical window height.

The random-grid monster spawning milestone was authored against this coordinate system.

---

## ADR-006 — Fixed Bottom Expansion

**Status:** Accepted

Questbar’s physical bottom edge is fixed during expand/collapse.

The game expands **upward only**.

This was part of the original design and should not be reopened casually as a new design choice.

For gameplay presentation, logical baseline:

```text
Y = 192
```

is the bottom reference.

For native presentation, the physical bottom of the Questbar window is the corresponding fixed baseline.

---

## ADR-007 — Gameplay and Presentation Coordinates Are Separate

**Status:** Accepted

Questbar distinguishes:

### Logical gameplay space

```text
800 × 192
```

Used by:

- heroes
- monsters
- combat movement
- spawn formations
- attack range
- projectiles
- gameplay-local ground relationships

### Native presentation space

Uses actual physical Questbar window dimensions.

Used by:

- background/sky presentation
- native clipping / reveal
- desktop-integrated visual framing
- physical expanded/collapsed window behavior

The two coordinate systems can correspond visually, but they are not interchangeable.

---

## ADR-008 — Background Presentation Is Independent of Gameplay Scaling

**Status:** Accepted

Background / sky presentation must not inherit gameplay-world miniature scaling.

The background should be able to:

- remain at a stable visual scale
- stay bottom-anchored
- reveal more content upward as the window expands
- be clipped by the native window
- eventually support parallax / region variants / atmosphere

Background presentation therefore belongs outside the gameplay-scaled transform hierarchy.

---

## ADR-009 — Controllers Own Behavior; Profiles Own Values

**Status:** Accepted

General design rule:

> **Profiles own gameplay values. Controllers own gameplay behavior.**

Examples:

- `HeroCombatProfile` owns resolved hero combat values.
- `MonsterCombatProfile` owns resolved monster combat values.
- Actor controllers execute behavior.
- Encounter/combat controllers orchestrate systems.

Avoid putting orchestration into data resources or resolved stat containers.

---

## ADR-010 — Runtime Health Has Separate Ownership

**Status:** Accepted

Maximum health and current health do not have the same owner.

- Profiles/content provide maximum/resolved health values.
- `CombatHealthState` owns runtime current health.

Health state can report that death/incapacitation occurred, but it does not own broad orchestration.

---

## ADR-011 — Gameplay Announces Facts; Presentation Reacts

**Status:** Accepted

Gameplay systems should announce completed gameplay facts.

Presentation should react to those facts.

Examples:

- gameplay says an impact occurred
- presentation shows hit effect
- gameplay says actor incapacitated
- presentation changes animation/state

Do not make presentation determine authoritative gameplay outcomes.

---

## ADR-012 — Heroes Are Incapacitated, Not Destroyed

**Status:** Accepted

Heroes are persistent party members.

When defeated:

- heroes become incapacitated
- they are not removed/destroyed like ordinary monsters
- combat systems retarget living heroes appropriately

This supports persistent party progression and later recovery systems.

---

## ADR-013 — Content Should Be Data-Driven

**Status:** Accepted

Strategic direction:

> **Build systems once, feed them data forever.**

New content should usually be represented by data/resources rather than new controller branches.

Examples:

- monster definitions
- targeting style
- combat values
- presentation values
- future encounter definitions
- future region definitions

Runtime code should become more stable as content volume increases.

---

# 6. Established Development Rules

Some rules were historically numbered. Only wording known with confidence is reproduced here.

**Do not invent missing rule wording.**

Rules 1–21 are not reconstructed here because their exact wording is not currently available.

Rule 36 was historically inconsistent and should not be reused casually.

---

## Rule 22 — Attack / Movement Presentation Matches Actual Behavior

Attack presentation overrides movement presentation when attacking.

Visual state must reflect actual gameplay state rather than independent animation guesses.

---

## Rule 23 — Impact Timing Matches Defined Visual Impact / Release

Damage or gameplay impact should occur at the intended release / impact moment rather than arbitrarily at animation start.

---

## Rule 24 — Attack Range Is an Approach Threshold, Not a Retreat Rule

Actors move closer when necessary to enter attack range.

They do not automatically retreat simply because they are closer than maximum range.

This prevents oscillation and unnatural ranged behavior.

---

## Rule 26 — Gameplay Roots and Presentation Sockets Are Different Concepts

Gameplay positions and semantic presentation sockets should not be conflated.

Examples:

- actor root
- projectile origin socket
- impact origin socket
- weapon socket

Semantic sockets exist so presentation can evolve without corrupting gameplay coordinates.

---

## Rule 28 — Profiles Own Gameplay Values; Controllers Own Gameplay Behavior

See ADR-009.

---

## Rule 29 — Move Ownership Before Adding Complexity

When a system’s responsibility is currently misplaced:

1. move ownership first
2. verify behavior
3. then add new complexity

Do not stack new features on top of incorrect ownership.

---

## Rule 30 — Maximum Health and Current Health Have Different Owners

See ADR-010.

---

## Rule 31 — Health State Reports Death; It Does Not Orchestrate Death

`CombatHealthState` reports runtime health/death state.

Encounter/combat/actor systems decide broader consequences.

---

## Rule 32 — Temporary Debug Tools Stay Until Replaced

Do not prematurely remove useful debug systems.

Temporary debug infrastructure should remain until a production replacement actually exists.

---

## Rule 33 — Gameplay Announces Completed Facts; Presentation Reacts

See ADR-011.

---

## Rule 34 — Heroes Are Incapacitated, Not Destroyed

See ADR-012.

---

## Rule 35 — Commit Style

Use milestone commits with clear “Implemented” and “foundation for” sections.

---

## Rule 37 — Log Decisions, Not Just Outcomes

Debug logs should expose useful decision context.

Examples:

Bad:

```text
Target selected.
```

Better:

```text
Monster selected LowestHealthHero: HeroActor2, HP=31/100.
```

Logs should help explain why the runtime made a decision.

---

## Rule 38 — New Content Usually Means Data, Not Code

If adding a new monster requires editing multiple controllers, question the architecture.

---

## Rule 39 — Validate Content Before Shipping

Data-driven content should have validation for:

- IDs
- references
- required scenes
- numeric ranges
- unsupported enum modes
- missing dependencies

---

## Proposed Rule 40 — Build Systems Once, Feed Them Data Forever

This phrase captures the long-term content architecture.

Treat as a guiding principle unless formally renumbered later.

---

## Proposed Rule 41 — Author Content in Portable Data

Content authoring should increasingly live in portable structured data rather than being trapped in scene-specific manual configuration.

Treat as a guiding principle unless formally renumbered later.

---

# 7. Current High-Level Runtime Architecture

The main gameplay responsibilities currently look approximately like this:

```text
JourneyStateService
    owns Traveling / Encounter journey state
              │
              ▼
EncounterController
    owns encounter lifecycle and monster roster
              │
              ▼
CombatController
    owns combat orchestration / participants / impacts / events
              │
        ┌─────┴─────┐
        ▼           ▼
 Hero Actors     Monster Actors
        │           │
        └─────┬─────┘
              ▼
       TargetingService
       owns target selection
```

Additional runtime concepts:

```text
HeroCombatProfile
MonsterCombatProfile
        ↓
resolved gameplay values

CombatHealthState
        ↓
runtime current health

MonsterDefinition
        ↓
data-driven monster content

MonsterContentRegistry
        ↓
content lookup

MonsterFactory
        ↓
runtime actor creation
```

---

# 8. Journey State

## `JourneyStateService`

Owns broad journey state such as:

- Traveling
- Encounter

Presentation systems observe journey state and react.

It should not own:

- combat mechanics
- actor movement details
- background node implementation
- monster content definitions

Travel and encounter are high-level game-state concepts.

---

# 9. Encounter Architecture

## `EncounterController`

Owns:

- active monster roster
- spawning
- removal
- encounter presentation lifecycle
- natural encounter completion
- default monster content used for testing
- spawn formation sequencing

It does not own:

- monster combat values
- targeting algorithm details
- health internals
- individual actor movement mechanics

---

# 10. Combat Architecture

## `CombatController`

Owns:

- combat orchestration
- hero participant collection
- monster participant collection
- attack / impact coordination
- combat events
- participant state refresh
- combat-level facts

It should not own:

- monster definition data
- target-selection algorithms
- actor-local presentation
- health storage
- content lookup

`CombatEvent` is a strongly typed C# event system used to announce gameplay facts.

---

# 11. Targeting Architecture

## `TargetingService`

Owns target selection.

Supported monster targeting styles:

```csharp
public enum MonsterTargetingStyle
{
    NearestHero,
    LowestHealthHero,
    HighestThreatHero,
    RandomLivingHero
}
```

### Currently supported

- `NearestHero`
- `LowestHealthHero`
- `RandomLivingHero`

### Intentionally deferred

- `HighestThreatHero`

Do **not** silently implement a fake fallback for HighestThreatHero.

The mode exists so data/schema can support it later, but actual threat mechanics are deliberately postponed.

### Target lock behavior

Monsters:

- acquire a valid target
- keep the target while valid
- retarget when target becomes invalid/incapacitated

Heroes similarly maintain target locks until the target becomes invalid/dies.

### Deferred threat questions

When threat is revisited with representative party/wave combat, explicitly design:

- Is damage 1:1 threat?
- Does healing generate shared/global threat?
- Do tanks modify generated threat?
- Is threat per-monster?
- How does taunt work?
- Does threat decay?
- Are some abilities threat modifiers?
- How do wave transitions affect tables?

Threat should not be invented prematurely.

---

# 12. Actor Architecture

## `HeroActorController`

Owns hero-local runtime behavior such as:

- actor state
- local movement behavior
- attack presentation coordination
- formation behavior
- hero-local animation decisions

Heroes are persistent and incapacitate rather than being destroyed.

---

## `MonsterActorController`

Owns monster-local runtime behavior such as:

- actor state
- movement
- target engagement
- attack behavior
- presentation application from definition
- runtime health/profile configuration

Monsters are content-driven through `MonsterDefinition`.

---

# 13. Combat Movement Rules

Important behavior:

- melee heroes approach enemies as needed
- monsters approach heroes as needed
- ranged heroes generally hold ground
- actors do not automatically retreat merely because they are inside maximum range
- attack range is the distance required to begin attacking, not a preferred orbit distance
- movement is bidirectional where required by behavior
- actors should not jitter between move and attack states

Combat movement and attack animation must reflect actual state.

---

# 14. Projectile / Impact Presentation

Projectile systems use semantic origin concepts such as:

- `ProjectileOrigin`
- `ImpactOrigin`

This avoids coupling gameplay roots directly to visual launch/impact positions.

Gameplay impact timing follows the defined attack release / impact timing.

The long-term principle is:

- gameplay determines whether and when an attack occurs
- presentation determines how that fact is visually represented
- visual sockets should not redefine gameplay state

---

# 15. Health Architecture

## `CombatHealthState`

Owns runtime health.

Responsibilities include:

- current health
- damage application
- health status
- reporting death/incapacitation state

Does not own:

- destroying actors
- encounter completion
- combat target reassignment
- party persistence

Maximum health originates from resolved profiles/content.

---

# 16. Content Architecture

Questbar is transitioning toward data-driven content.

Current relevant folders:

```text
res://Scripts/Content/
res://Scripts/Content/Definitions/
res://Content/Monsters/Core/
```

---

# 17. Content ID Convention

Content IDs use:

```text
<category>.<namespace>.<name>
```

Rules:

- lowercase
- periods represent hierarchy
- underscores may be used inside a name
- stable
- globally unique
- should not change merely because a display name changes

Examples:

```text
monster.core.training_monster
monster.core.heavy_training_monster
```

A working class named:

```text
ContentId
```

already exists.

**Do not rename it again without a deliberate reason.**

This class was previously difficult to rename safely, and the current implementation is working.

---

# 18. `MonsterDefinition`

`MonsterDefinition` is a Godot `[GlobalClass] Resource`.

It contains data for areas such as:

- identity
- runtime actor scene
- presentation
- health
- attack
- movement
- targeting
- validation

Presentation properties currently include concepts such as:

```csharp
[ExportCategory("Presentation")]
[Export] public Vector2 VisualScale { get; set; } = Vector2.One;
[Export] public Color VisualModulate { get; set; } = Colors.White;
```

`MonsterActorController.ApplyDefinition(...)` applies presentation and profile values.

This allows variants such as a heavy training monster to use the same actor scene while changing scale/color/stats through data.

Targeting style is stored in the definition.

Unsupported targeting modes should fail validation rather than silently falling back.

---

# 19. Monster Registry / Factory

Implemented:

## `MonsterContentRegistry`

Owns definition lookup by content ID.

## `MonsterFactory`

Has a registry dependency and creates runtime actors.

Conceptual flow:

```text
content ID
   ↓
MonsterContentRegistry
   ↓
MonsterDefinition
   ↓
ActorScene
   ↓
instantiate MonsterActorController
   ↓
Configure(definition)
   ↓
return unparented actor
```

The encounter controller decides where/when to parent/spawn the monster.

The factory should not own encounter sequencing.

---

# 20. Debug Console

Questbar has a debug console and command service.

Known behavior includes commands for spawning monsters.

A representative command pattern:

```text
monster.spawn <content_id> [count]
```

Debug tooling should call the real production APIs rather than maintaining separate fake gameplay paths.

The debug console is intentionally retained until production systems replace its purposes.

---

# 21. Random Grid Monster Spawning Milestone

**Status:** Completed and considered a stable gameplay milestone.

This was the last clean rollback checkpoint used during presentation debugging.

## Design

Normal monster spawning uses a random **4×N grid** concept.

Defaults / design choices:

- 4 rows
- start with 2 columns
- unlock one additional column every 6 monsters spawned
- choose a random unused slot from currently available slots
- when all currently available slots are exhausted, clear the used-slot bag and allow reuse
- additional columns extend farther left
- vertical rows are centered around `MonsterSpawnAnchor`
- spawn sequence count is monotonic during the encounter
- reset formation sequencing per encounter for now

Representative spacing:

```text
VerticalSpawnSpacing   ≈ 24
HorizontalSpawnSpacing ≈ 48
```

The exact exported values can be tuned.

## Why slot reuse exists

4 slots per column are available, while new columns unlock every 6 spawns.

Therefore permanent uniqueness would eventually exhaust available slots.

Monsters move away after spawning, so recycling used slots after exhaustion is acceptable.

## Important ownership

Formation settings belong to encounter/spawn presentation, **not** to individual monster definitions.

---

# 22. Spawn Formation Settings

Encounter-level exports include concepts like:

```csharp
[ExportCategory("Monster Spawn Formation")]
public int SpawnRows { get; set; } = 4;
public int StartingSpawnColumns { get; set; } = 2;
public int SpawnsPerColumnExpansion { get; set; } = 6;
public float VerticalSpawnSpacing { get; set; } = 24.0f;
public float HorizontalSpawnSpacing { get; set; } = 48.0f;
```

Runtime support includes:

- random generator
- used-slot set
- spawn sequence count

Spawn offset principle:

```text
X offset = farther left for later columns
Y offset = centered row spacing
```

---

# 23. Monster Entry-State Cleanup

The old special monster entry state was removed during the random-grid milestone.

Removed concepts included:

- `MonsterState.Entering`
- `IsEntering`
- `EntryDestination`
- `_entrySpeed`
- arrival-specific entry movement
- `InitializeEntrance`
- `UpdateEntrance`
- entry-state process branch

The intent was to simplify spawning and avoid maintaining a redundant entrance state when normal actor movement already handles approach behavior.

---

# 24. Target Acquisition Cleanup

`TryEngage` was renamed to a concept equivalent to:

```text
TryAcquireTarget
```

to more accurately describe responsibility.

Hit-based initial aggro was removed as a competing targeting system.

Targeting should have one clear owner.

---

# 25. Future Boss Entrance Design

Normal grid spawning is not intended to define boss presentation.

Future concept:

```text
SpawnStyle = BossCenter
```

Boss behavior may include:

- boss enters alone
- vertically centered
- begins several column lengths deeper/farther left
- dramatic approach

This should eventually belong to encounter/wave/spawn-style data.

Do **not** put “boss entrance formation” into `MonsterDefinition.Role`.

A likely future owner is:

```text
EncounterDefinition.SpawnStyle
```

or an equivalent encounter/wave presentation definition.

---

# 26. Region / Travel Presentation

## `RegionPresentationController`

Current known responsibility:

- observes journey state
- enables/disables travel presentation
- moves region tiles during travel
- wraps region tiles when they pass the tile width

Representative settings:

```text
TravelSpeed ≈ 60
TileWidth   ≈ 800
```

Background travel direction is rightward while the party conceptually travels left.

Important:

`RegionPresentationController` should not own native window resizing or desktop placement.

Long-term it may evolve into broader region visual presentation, but new ownership should be added deliberately.

---

# 27. World Presentation Goals

Long-term presentation goals include:

- transparent desktop-backed sky
- fixed/stable sky visual scale
- upward reveal during expansion
- collapsed/expanded taskbar integration
- gameplay miniature presentation
- bottom-anchored gameplay
- ground presentation
- far/mid/near parallax
- fog
- weather
- color overlays / region tint
- vignette
- foreground overlap
- region-driven visuals

The final design should support art assets without requiring arbitrary per-resolution manual repositioning.

---

# 28. Current Presentation Architecture

After experimentation and rollback/recovery, the architecture has been moving toward separating background presentation from the stable gameplay viewport.

A representative current structure is:

```text
Main                                [Node]
│
├── BackgroundPresentation          [Node2D]
│   └── CloudBackground             [Sprite2D]
│
├── RegionViewportContainer         [SubViewportContainer]
│   └── RegionViewport              [SubViewport]
│       │
│       ├── ScalableWorld           [Node2D]
│       │   ├── RegionPresentation  [Node2D]
│       │   └── ActorLayer          [Node2D]
│       │
│       ├── GroundLayer             [Node2D]
│       │   └── GroundPlaceholder   [Polygon2D]
│       │
│       ├── RegionEffects           [Node2D]
│       └── ScreenEffects           [CanvasLayer]
│
├── PopupWindows                    [Node]
├── Services                        [Node]
└── Controllers                     [Node]
    └── DesktopWindowHostController [Node]
```

Exact current scene hierarchy should still be verified from the latest `Main.tscn` before architecture-sensitive edits.

---

# 29. Ground Presentation Decision

A temporary ground placeholder was originally implemented as a `ColorRect`.

That caused confusion because:

- `ColorRect` is a `Control`
- gameplay world uses `Node2D` transforms
- Control layout/anchor behavior differs from Node2D transform behavior

It was replaced with:

```text
GroundPlaceholder [Polygon2D]
```

Ground was later moved outside the uniformly scaled gameplay group because the desired presentation required the ground to remain full-width.

Current conceptual ownership:

```text
GroundLayer [Node2D]
└── GroundPlaceholder [Polygon2D]
```

Ground should not inherit miniature actor/world scaling if the design requires full-width ground during collapsed mode.

---

# 30. Background Presentation Milestone

A recent presentation checkpoint separated cloud/sky presentation from the gameplay viewport.

The key realization was:

> A background inside the same automatically stretched gameplay viewport cannot remain visually independent from that viewport.

The background was moved to:

```text
BackgroundPresentation [Node2D]
└── CloudBackground [Sprite2D]
```

outside the gameplay `RegionViewport`.

Root automatic canvas stretching was disabled during this migration so that the background does not automatically resize with native window height.

A dedicated background presentation controller was introduced to bottom-anchor the presentation layer while the native window changes height.

### Intended behavior

When the physical window expands:

- bottom edge stays fixed
- top edge moves upward
- cloud/background visual scale stays stable
- more of the background is revealed upward

When collapsed:

- the native window clips the upper background
- the bottom region remains aligned

### Important editor/runtime lesson

Background presentation now exists in **native presentation coordinates**, not the same 800×192 gameplay coordinates.

Therefore, visually comparing cloud size against the blue 800×192 `RegionViewport` editor rectangle is not necessarily a meaningful 1:1 comparison.

A future editor-authoring solution should provide a clear native expanded-size guide.

---

# 31. Background Authoring Frame

Long-term, background art needs a WYSIWYG authoring frame.

Recommended concept:

```text
BackgroundPresentation  [Node2D]
├── BackgroundGuide     [Polygon2D or editor-only guide]
└── CloudBackground     [Sprite2D]
```

The guide represents the configured **expanded native presentation size**, e.g.:

```text
800 × ExpandedHeight
```

This prevents artists/designers from incorrectly composing native-space background art against the logical 800×192 gameplay rectangle.

Eventually the guide should derive automatically from window settings rather than requiring manual point edits.

---

# 32. Background Bottom Anchoring

The background controller should preserve bottom anchoring independently of the gameplay viewport.

Concept:

```text
BackgroundPresentation.Y =
CurrentNativeWindowHeight - ExpandedPresentationHeight
```

This lets the native window clip the same full expanded background.

For example:

```text
ExpandedHeight = 306
Collapsed actual height = 64

Collapsed BackgroundPresentation.Y =
64 - 306
= -242
```

Thus the background itself stays full-size while the native window exposes only its bottom portion.

The cloud sprite may additionally need its own bottom alignment inside the expanded background presentation frame depending on asset dimensions.

---

# 33. Experimental Controllers That Caused Instability

Two experimental controllers were created during presentation work:

- `WorldScaleController`
- `PresentationFrameController`

When both were active, they created overlapping transformations.

Symptoms included:

- background scaling unexpectedly
- ground moving into the sky
- frame moving
- confusing crop behavior
- visual size mismatch
- movement that appeared to originate from the wrong corner

At one recovery point, detaching both scripts returned the project to normal scale.

### Anti-regression rule

Do not reactivate old experimental controller logic blindly.

If a controller is reintroduced:

- inspect current source
- define one responsibility
- ensure no other system controls the same transform
- test one transform at a time

---

# 34. Proven Bottom-Pivot Scaling Math

One useful result from the experimentation was verified:

If a gameplay group must scale uniformly while preserving logical baseline `Y = 192`, then:

```csharp
ScalableWorld.Scale =
    new Vector2(scale, scale);

ScalableWorld.Position =
    new Vector2(
        xOffset,
        192.0f * (1.0f - scale));
```

preserves the logical Y=192 baseline.

Why:

```text
scaledY = 192 * scale + 192 * (1 - scale)
        = 192
```

This successfully kept the ground/baseline at the bottom during a test.

However, because ground was later separated into `GroundLayer`, future use of this math should be limited to content that is actually intended to miniaturize.

Do not apply this transform to background or full-width ground by default.

---

# 35. Physical Window Runtime Behavior

Questbar native window settings are persisted and can override Inspector defaults.

A runtime diagnostic demonstrated values such as:

```text
ConfiguredWidth
CollapsedHeight
ExpandedHeight
RequestedHeight
FinalSize
ActualSize
RequestedPosition
ActualPosition
ScreenPosition
ScreenSize
ScreenScale
```

This diagnostic was valuable and should be retained while the native host is still evolving.

A known test configuration restored the intended physical behavior around:

```text
Width            = 800
Collapsed Height ≈ 60
Expanded Height  ≈ 180
```

Actual collapsed Windows size may be a few pixels larger than the requested value.

Do not assume Inspector values are active if saved settings are loaded.

---

# 36. Windows Taskbar Relationship

Important clarification:

Questbar is intended to sit **inside the Windows taskbar area**, not above it.

The bottom of Questbar aligns with the bottom of the taskbar / physical bottom edge.

The original product concept is not:

```text
desktop area
Questbar
taskbar
```

It is closer to:

```text
desktop area
----------------
Windows taskbar area
[ Questbar lives within this vertical band when collapsed ]
----------------
physical screen bottom
```

Expanded mode grows upward from that fixed bottom.

This distinction is critical when reasoning about taskbar geometry.

---

# 37. Windows Platform Roadmap

A historical adaptive Windows host roadmap was defined:

### W1 — Native Topmost Behavior

**Status:** Complete and verified.

Questbar remains topmost reliably while:

- switching ordinary applications
- maximizing other windows
- typing in other applications

Existing hover/placement behavior continued working at the time of verification.

### W2 — Read Actual Windows Taskbar Rectangle

Originally planned next.

### W3 — Detect Notification / Tray Area

Place Questbar immediately to the left of tray.

### W4 — Detect Collisions With Pinned / Running Taskbar Buttons

### W5 — Fallback Policies

Possible strategies:

- shift
- shrink
- compact/icon
- above-taskbar fallback

### W6 — Robust Desktop Support

- centered/left-aligned taskbars
- multiple monitors
- DPI/display scaling
- auto-hide
- Explorer restarts

### W7 — User Placement / Collision Settings + Platform Testing

### Later design evolution

Manual placement settings became more important and may replace some automatic collision-detection requirements.

Do not automatically revive every W3–W5 idea without reconciling it with ADR-003.

---

# 38. `DesktopWindowHostController`

Known responsibilities include:

- `GetWindow()`
- borderless window
- unresizable
- always-on-top
- native transparency
- selected monitor
- right/left anchoring
- native width
- collapsed/expanded height
- placement offsets
- expanded state toggle
- setting persistence
- placement diagnostics

It may expose:

```text
IsExpanded
ExpandedChanged
```

for presentation observers.

If expanded-state events are retained, they should announce state changes rather than directly orchestrating every presentation layer.

---

# 39. Window Settings Persistence

A storage layer such as:

```text
WindowSettingsStorage
```

loads/saves settings from:

```text
user://window_settings.cfg
```

Possible persisted values include:

- selected monitor
- window width
- collapsed height
- expanded height
- screen anchor
- horizontal offset
- bottom offset

### Debugging rule

If runtime size differs from Inspector size:

1. inspect persisted values
2. inspect startup logs
3. compare `FinalSize` to `ActualSize`
4. do not immediately blame DPI scaling

A previous diagnostic showed that Windows/Godot applied exactly the requested size; the unexpected dimensions came from persisted settings.

---

# 40. Native Transparency

Questbar uses transparent desktop integration.

Relevant configuration includes:

```text
window/per_pixel_transparency/allowed = true
```

and native/runtime setup equivalent to:

```csharp
_window.Transparent = true;
GetViewport().TransparentBg = true;
```

The exact current implementation should be verified before modifying it.

Transparency is a platform/presentation concern and should remain separate from gameplay.

---

# 41. Root Canvas Stretch Lesson

Godot root `canvas_items` stretching affected root-level 2D presentation even after the background was moved outside the gameplay `SubViewport`.

This explained why the cloud continued changing size despite no longer being a child of `ScalableWorld`.

A later controlled test disabled root canvas stretching and confirmed:

- background visual size stopped changing
- background then moved with the native window because it was still top-anchored
- bottom-anchor presentation logic was required next

This was a useful architectural discovery.

### Rule

Do not change project stretch mode casually.

It affects the entire scene coordinate/presentation pipeline.

When changing it, explicitly replace any scaling/layout responsibility it previously provided.

---

# 42. Region Viewport Anti-Regression Rule

The 800×192 logical gameplay viewport was destabilized when its size was manually changed to match physical window values such as 611×215.

That caused:

- hero anchor appearing far off-right
- monster positions no longer matching composition
- gameplay layout confusion

Example:

```text
HeroFormationAnchor X ≈ 720
```

makes sense inside width 800.

It is naturally beyond a viewport that has been incorrectly redefined to width 611.

### Permanent lesson

**Never resize the logical gameplay viewport merely to match a user’s native Questbar width/height.**

Presentation must adapt around the logical world.

---

# 43. Stable Gameplay Anchor References

During the random-grid milestone, representative positions included:

```text
MonsterSpawnAnchor ≈ (-40, 160)
HeroFormationAnchor ≈ (720, 160)
```

Later visual editing moved some hero formation Y values, e.g. around 175.

These are scene-authored gameplay positions and should not be “fixed” merely because native presentation changes.

Always inspect current `Main.tscn` before assuming exact current values.

---

# 44. Hero Formation

Questbar currently supports multiple hero actors.

Formation is anchored on the right side.

Representative hero formation offsets have been experimented with, including values such as:

```text
(-40, 0)
(-10, 15)
```

Exact current offsets are scene-specific and should be read from current files.

Do not normalize these from memory.

The key stable principle is:

- party belongs on the right
- formation coordinates live in the stable logical world

---

# 45. Scene Ownership Principles

A node’s parent is not merely visual organization.

Parenting determines transform inheritance.

Therefore:

- background must not live under a scaled gameplay parent
- full-width ground must not live under miniature actor scaling if it should remain full width
- semantic screen effects may belong in `CanvasLayer`
- gameplay actors belong in world-space Node2D hierarchy

Before reparenting nodes, inspect any NodePaths/exported references that may break.

---

# 46. Screen Effects

Current/future screen-space effects include:

- fog
- color overlay
- vignette

Representative hierarchy:

```text
ScreenEffects           [CanvasLayer]
├── FogPlaceholder      [ColorRect]
├── ColorOverlay        [ColorRect]
└── VignettePlaceholder [ColorRect]
```

Screen-space effects should generally not inherit gameplay miniaturization.

Their final ownership and clipping behavior should be tested once background/gameplay framing is stable.

---

# 47. Parallax Direction

Because Questbar travels right-to-left:

- party visually progresses leftward
- backgrounds scroll rightward during travel

Future parallax layers may include:

```text
FarBackground
MidBackground
NearBackground
Foreground
```

Each layer may scroll at different speeds.

Do not let both `RegionPresentationController` and a future parallax controller continuously manipulate the same node offsets.

Migrate ownership deliberately.

---

# 48. Region-Driven Presentation

Long-term, region visuals should become data-driven.

Possible future `RegionDefinition` content:

- background asset IDs
- parallax layer assets
- fog parameters
- ambient color
- weather
- vignette
- travel speed modifier
- region theme
- foreground assets

The goal is to avoid hard-coding individual regions into presentation controllers.

---

# 49. Data Spreadsheet / Content Master

A content spreadsheet was created:

```text
Questbar_Content_Master_Monsters_v2.xlsx
```

Known columns include:

- SchemaVersion
- ContentId
- DisplayName
- IsActive
- Status
- RegionId
- Role
- Tier
- Level
- ActorSceneId
- TresOutputPath
- VisualSetId
- PortraitAssetKey
- IconAssetKey
- SuggestedHealth
- MaximumHealth
- AttackDamage
- AttackRange
- AttackIntervalSec
- AttackDurationSec
- AttackReleasePoint
- AttackLungeDistance
- AttackDeliveryMode
- EntrySpeed
- CombatMoveSpeed
- MovementStyle
- TargetingStyle
- SuggestedXP
- ExperienceReward
- GoldMin
- GoldMax
- LootTableId
- Ability1
- Ability2
- Ability3
- Tags
- DesignNotes
- IdValid
- CoreStatsValid
- ReferencesReady
- RowReady

Monster role values:

```text
Melee
Ranged
Spellcaster
Tank
Boss
```

Tier values:

```text
Common
Uncommon
Unique
Elite
```

Level range:

```text
1–90
```

Representative suggested health formula:

```text
ROUND(8 * (level ^ 1.35), 0)
```

Representative suggested XP formula:

```text
ROUND(8 * (level ^ 0.77), 0)
```

Targeting dropdown includes:

- NearestHero
- LowestHealthHero
- HighestThreatHero
- RandomLivingHero

Remember: HighestThreatHero exists in schema but remains intentionally unsupported in runtime until threat is designed.

---

# 50. Content Validation

Content should eventually be validated both during authoring and runtime startup.

Validation categories include:

- valid ContentId
- required ActorScene present
- positive health
- valid attack interval
- valid range
- known targeting style
- unsupported feature flags rejected
- external references available
- output path valid
- required gameplay fields complete

Fail loudly during development rather than silently spawning broken content.

---

# 51. Travel → Encounter → Combat Loop Status

A broad gameplay loop has already been implemented:

```text
Travel
  ↓
Encounter begins
  ↓
Monsters spawn
  ↓
Combat begins
  ↓
Actors attack / take damage
  ↓
Monsters die / heroes incapacitate
  ↓
Encounter resolves
  ↓
Return to travel
```

This is a stable gameplay foundation and should not be disrupted by presentation work.

---

# 52. Multiple Monsters

Multiple monster support exists.

Combat participant handling, targeting, and encounter roster behavior were updated to support multiple simultaneous monsters.

Random-grid spawning builds on this.

Future waves should reuse this foundation rather than creating a second combat mode.

---

# 53. Natural Encounter Completion

Encounter progression can detect when the active monster roster has naturally emptied and transition appropriately.

Do not rely only on debug commands or manually forced state transitions.

---

# 54. Structured Logging

Structured logs are an intentional debugging tool.

Useful logs should include:

- state transitions
- selected target and reason
- spawn content ID
- spawn slot
- participant counts
- window requested/actual sizes
- placement calculations
- encounter completion cause

Avoid noisy per-frame logs unless debugging a specific issue.

---

# 55. Server-Authoritative Long-Term Direction

Long-term strategic direction favors server-authoritative gameplay if Questbar grows into an online/shared product.

This means client presentation architecture should not become inseparable from authoritative gameplay decisions.

Even while fully local today:

- keep gameplay facts distinct from presentation
- avoid UI/presentation deciding outcomes
- keep content IDs stable
- prefer deterministic/structured data paths where practical

This is a future-proofing direction, not a requirement to build networking now.

---

# 56. Current World Presentation Milestone — What Is Considered Stable

At the latest successful presentation checkpoint:

- background presentation was separated from gameplay viewport scaling
- root automatic canvas stretching was disabled so background art would stop changing visual size with native window height
- background presentation used a dedicated `Node2D`
- background was bottom-anchored so collapse/expand could reveal/hide the same background
- gameplay 800×192 coordinates were preserved
- experimental world/frame scripts were not trusted as a combined system

A commit was prepared under the title:

```text
Milestone: Bottom-anchored background presentation
```

with the intent to establish:

- stable expanded/collapsed background reveals
- future parallax
- region-specific sky/atmosphere
- independent gameplay scaling

Before relying on this as exact implementation truth, read the latest files because presentation code was actively evolving.

---

# 57. Current Presentation Problems / Open Visual Work

Even after functional bottom-anchor behavior, visual-authoring issues remained important:

## Editor vs runtime framing

The cloud may look very different in the editor because:

- gameplay viewport is 800×192 logical space
- background is native presentation space
- expanded physical height may be 180, 220, 306, etc.

Need a deliberate authoring guide or preview workflow.

## Background asset bottom alignment

If expanded height changes, the cloud asset itself needs a defined fitting/alignment rule.

Potential strategies:

- bottom-align native-size asset
- FitWidth
- Cover
- Contain
- native pixel size
- region-specific crop framing

Avoid arbitrary manual scale values becoming the permanent content workflow.

## Example observed transform

A cloud sprite had a manually stretched X scale around:

```text
X ≈ 1.406
Y = 1.0
```

This may be acceptable temporarily, but long-term background sizing should use a clear fitting policy rather than unexplained hand-authored scale numbers.

---

# 58. Future Background Fit Policy

A future background presentation system should probably expose an explicit mode such as:

```text
NativeSize
FitWidth
Cover
Contain
```

Possible requirements:

### NativeSize

- preserve exact pixel art scale
- crop if necessary

### FitWidth

- fit horizontal width
- preserve aspect ratio
- crop/reveal vertically

### Cover

- fill presentation area without letterboxing
- preserve aspect ratio
- crop overflow

### Contain

- show entire background
- preserve aspect ratio
- may introduce empty space

For pixel art, nearest-neighbor / texture filtering behavior also matters.

Do not choose a permanent fit policy before representative assets are tested.

---

# 59. Collapsed Gameplay Miniature — Desired Direction

The long-term collapsed presentation concept remains:

- native window is roughly taskbar height
- background remains independently presented
- gameplay actors may become uniformly miniature
- gameplay miniature remains anchored to logical bottom baseline
- ground can remain full-width
- no vertical squashing
- no logical gameplay coordinate changes

One proven formula exists for Y-baseline-preserving scale.

Horizontal collapsed arrangement still requires deliberate design.

Possible choices include:

- preserve left-origin
- center miniature actors
- preserve party/right bias
- adaptive composition

Do not confuse actor miniature placement with native window anchoring.

---

# 60. Physical vs Logical Scale

Do not use formulas like:

```text
CollapsedHeight / ExpandedHeight
```

as a universal scale for every visual layer.

That may be appropriate for a specific miniature gameplay effect, but:

- background may not scale at all
- ground may not scale
- screen effects may use native dimensions
- gameplay logical world stays 800×192

Scale must be owned per presentation layer.

---

# 61. Godot Node-Type Lessons

Use node types according to responsibility.

## `Node`

Use for:

- non-spatial controllers
- services
- orchestration

## `Node2D`

Use for:

- world/presentation transforms
- 2D position/scale/rotation
- spatial layer roots

## `Control`

Use for:

- UI layout
- anchors
- offsets
- containers
- native window UI

## `CanvasLayer`

Use for:

- screen-space layers
- overlays
- HUD/effects that should not inherit world transforms

## Important scripting rule

A script declared:

```csharp
public partial class Something : Node2D
```

cannot be attached to a plain:

```text
Node
```

The scene node must be `Node2D` or a compatible subclass.

This exact error occurred with `BackgroundPresentationController`.

---

# 62. Do Not Mix Control Layout With World Transforms Accidentally

A previous `ColorRect` ground placeholder exposed this issue.

Controls have:

- anchors
- offsets
- layout presets
- parent Control layout behavior

Node2D objects have:

- position
- rotation
- scale
- transform inheritance

Use Controls for UI and Node2D/Polygon2D/Sprite2D for world-space visuals unless there is a deliberate reason otherwise.

---

# 63. Debugging Viewport / Presentation Problems

When presentation behaves strangely, inspect from outermost to innermost:

```text
Native Window
    ↓
Root Viewport / project stretch
    ↓
Control / presentation wrapper
    ↓
SubViewportContainer
    ↓
SubViewport
    ↓
Layer root
    ↓
individual sprite/actor
```

For each level inspect:

- size
- position
- scale
- anchors / offsets if Control
- stretch settings
- size override
- parent transforms

Do not immediately tweak child coordinates when an ancestor is scaling.

---

# 64. Runtime Remote Tree Is Authoritative

The Godot **Remote** scene tree is often more useful than Local editor values when diagnosing runtime transforms.

Compare expanded vs collapsed for:

- container size
- viewport size
- background position
- background scale
- gameplay root position
- gameplay root scale
- native window size
- native window position

Use runtime logs to confirm what values were actually applied.

---

# 65. Important Anti-Regression Checklist

Before merging a presentation/window change, verify:

## Gameplay

- [ ] heroes still form on the right
- [ ] monster spawn grid is still correct
- [ ] monsters can approach heroes
- [ ] ranged heroes hold appropriately
- [ ] projectiles travel correctly
- [ ] targeting still works
- [ ] combat completes
- [ ] hero incapacitation still works
- [ ] encounter returns to travel

## Logical coordinates

- [ ] `RegionViewport` remains logically 800×192
- [ ] hero anchors were not “fixed” for physical window dimensions
- [ ] monster anchors were not moved to compensate for clipping
- [ ] attack ranges remain gameplay-space distances

## Native window

- [ ] collapsed bottom stays fixed
- [ ] expanded bottom stays fixed
- [ ] top expands upward
- [ ] configured width is correct
- [ ] monitor selection works
- [ ] right/left anchoring works
- [ ] topmost behavior works

## Background

- [ ] visual size does not unexpectedly change
- [ ] bottom alignment remains correct
- [ ] expansion reveals upward
- [ ] transparency still works
- [ ] background does not inherit gameplay scaling

## Ground

- [ ] stays at intended baseline
- [ ] stays full width if that is the intended mode
- [ ] does not jump into the sky
- [ ] does not inherit an unintended Control layout transform

## Controllers

- [ ] exactly one system owns each continuously controlled transform
- [ ] no stale experimental controller is still subscribed to events
- [ ] disabling process mode is not mistaken for disabling `_Ready()` event subscriptions

---

# 66. Important Godot Event-Lifecycle Lesson

Setting:

```text
Process Mode = Disabled
```

does not necessarily mean a node’s `_Ready()` never ran.

If `_Ready()` subscribes to C# events, that object may continue responding to those events even if normal processing is disabled.

Therefore, for event-driven experimental controllers:

- detach the script
- remove the node
- or explicitly unsubscribe/guard behavior

when you need a truly clean diagnostic baseline.

This mattered during presentation debugging.

---

# 67. Current File / Folder Concepts

Representative structure:

```text
res://
├── Assets/
│   └── Background/
│
├── Content/
│   └── Monsters/
│       └── Core/
│
├── Scenes/
│   ├── Main.tscn
│   └── Actors/
│       └── Hero/
│
└── Scripts/
    ├── Content/
    │   └── Definitions/
    ├── Presentation/
    └── ...
```

Exact file tree should be read from the current project ZIP before moving files.

---

# 68. Main Scene Conceptual Structure

Representative current structure:

```text
Main                                      [Node]
│
├── BackgroundPresentation                [Node2D]
│   └── CloudBackground                   [Sprite2D]
│
├── RegionViewportContainer               [SubViewportContainer]
│   └── RegionViewport                    [SubViewport]
│       │
│       ├── ScalableWorld                 [Node2D]
│       │   ├── RegionPresentation        [Node2D]
│       │   │   ├── RegionTileA
│       │   │   ├── RegionTileB
│       │   │   └── GroundGuide
│       │   │
│       │   └── ActorLayer                [Node2D]
│       │       ├── MonsterSpawnAnchor
│       │       ├── HeroFormationAnchor
│       │       └── HeroActor(s)
│       │
│       ├── GroundLayer                   [Node2D]
│       │   └── GroundPlaceholder         [Polygon2D]
│       │
│       ├── RegionEffects                 [Node2D]
│       └── ScreenEffects                 [CanvasLayer]
│
├── PopupWindows                          [Node]
│   ├── WindowSettingsPopup
│   └── DebugConsoleWindow
│
├── Services                              [Node]
└── Controllers                           [Node]
    └── DesktopWindowHostController       [Node]
```

This is conceptual. Read the actual latest scene before relying on child names/paths.

---

# 69. Ownership Map

Use this as the first check when deciding where new code belongs.

| Responsibility | Owner |
|---|---|
| Journey state | `JourneyStateService` |
| Encounter lifecycle | `EncounterController` |
| Active monster roster | `EncounterController` |
| Spawn formation | `EncounterController` / future encounter definition |
| Combat orchestration | `CombatController` |
| Target selection | `TargetingService` |
| Hero-local behavior | `HeroActorController` |
| Monster-local behavior | `MonsterActorController` |
| Runtime health | `CombatHealthState` |
| Hero resolved combat values | `HeroCombatProfile` |
| Monster resolved combat values | `MonsterCombatProfile` |
| Monster content data | `MonsterDefinition` |
| Monster content lookup | `MonsterContentRegistry` |
| Monster runtime creation | `MonsterFactory` |
| Region travel visuals | `RegionPresentationController` |
| Native window behavior | `DesktopWindowHostController` |
| Background native presentation | dedicated presentation layer/controller |
| Full-width ground presentation | `GroundLayer` / future dedicated presentation logic |
| Screen-space overlays | `ScreenEffects` / `CanvasLayer` |
| Debug commands | debug console / command service |

When new behavior does not fit this map cleanly, discuss ownership before coding.

---

# 70. Stable Gameplay Milestones

The following broad milestones have been completed in the rebuild:

- travel → encounter → combat → death/completion → travel
- health/damage structure
- hero incapacitation persistence
- monster retargeting toward living heroes
- debug console commands
- multiple monster support
- structured logs
- data-driven monster definition foundation
- monster registry/factory
- data-driven monster targeting
- random grid monster spawning
- native topmost Windows behavior
- early background presentation separation / bottom-anchor work

Presentation is still evolving more actively than core gameplay.

---

# 71. Milestone — Data-Driven Monster Targeting

Completed.

### Checkpoint A

Added targeting enum/property to content.

### Checkpoint B

`TargetingService` became the owner of supported hero selection.

Supported behaviors verified.

### Checkpoint C cleanup

- target acquisition naming clarified
- hit-based competing aggro path removed
- targeting remains centralized

Highest threat remains intentionally deferred.

---

# 72. Milestone — Random Grid Monster Spawning

Completed and verified.

This milestone is especially important because a rollback snapshot exists and was used as the recovery point when presentation experiments destabilized layout.

Treat it as a known-good gameplay reference.

---

# 73. Milestone — Stable Presentation Baseline Recovery

After experimental scaling changes destabilized the logical world:

- the 800×192 coordinate system was restored
- hero/monster alignment returned to normal in expanded mode
- experimental transform controllers were detached
- native window settings were restored to sensible values
- background scaling behavior was investigated separately

This recovery reinforced the principle that presentation must not mutate gameplay coordinates.

---

# 74. Milestone — Bottom-Anchored Background Presentation

A commit checkpoint was prepared after achieving the desired background reveal behavior.

Intent:

- background independent from gameplay viewport
- root scale no longer resizes the sky
- dedicated `BackgroundPresentation [Node2D]`
- bottom anchoring
- upward-only reveal
- stable gameplay coordinates

This is the current visual-presentation foundation, though authoring workflow still needs refinement.

---

# 75. Current Pipeline / Next Likely Work

The immediate presentation pipeline likely proceeds approximately:

1. Stabilize native/background authoring frame.
2. Make editor composition predictable for native expanded dimensions.
3. Establish a clear background fitting policy.
4. Verify collapsed/expanded reveal with multiple heights.
5. Reintroduce gameplay miniature scaling as an isolated transform.
6. Keep ground independent/full width.
7. Add hover expansion to replace temporary Spacebar trigger.
8. Introduce far/mid/near parallax.
9. Add region tint/fog/vignette.
10. Move region visuals into data-driven region definitions.
11. Add representative real art and verify scale.
12. Test with multiple user window widths/heights.
13. Test multi-monitor / DPI / taskbar configurations.

Do not implement all of these in one pass.

---

# 76. Temporary Expand Trigger

Spacebar has been used as a temporary development toggle for expanded/collapsed state.

Long-term desired interaction:

- mouse hover expands Questbar
- mouse leaving collapses Questbar

Hover behavior should be implemented only after presentation geometry is stable enough that repeated expand/collapse is predictable.

---

# 77. Window Dimension Philosophy

Physical dimensions should be user-configurable.

Historical/reference values include:

```text
Logical gameplay: 800 × 192
Physical collapsed: around 60–64 px height
Physical expanded: around 180–192 px height
Physical width: often around 800 px during current testing
```

Do not assume every user will use exactly 800 physical pixels.

Logical width and physical width are separate concepts even when current testing happens to use the same number.

---

# 78. Content Portability Goal

Questbar content should eventually be authorable outside core runtime code.

Potential workflow:

```text
Spreadsheet / structured source
        ↓
validation
        ↓
Godot resources / portable content data
        ↓
registry
        ↓
factory
        ↓
runtime
```

This enables:

- large content libraries
- balance iteration
- tooling
- validation
- future modding possibilities
- possible server-side content use

---

# 79. Avoid Scene-Only Content

Scene files are useful for actor/presentation templates.

But gameplay identity/data should increasingly come from definitions rather than creating one hard-coded scene per minor monster variant.

Example:

```text
Training Monster
Heavy Training Monster
```

may share an ActorScene and differ through `MonsterDefinition`.

---

# 80. Future Encounter Definitions

A likely next-level content abstraction:

```text
EncounterDefinition
```

Possible data:

- encounter ID
- region
- monster composition
- wave structure
- spawn style
- timing
- boss presentation
- rewards
- environmental effects

Do not force this abstraction prematurely, but it is the likely owner for future spawn-style complexity.

---

# 81. Future Region Definitions

Potential:

```text
RegionDefinition
```

May own:

- region ID
- background visual set
- parallax layer references
- ambient color
- fog
- weather
- travel presentation configuration
- encounter pools
- music / ambience IDs

Controllers should consume these definitions rather than knowing every region name.

---

# 82. Future Hero / Ability Content

Long-term data-driven direction should extend to:

- heroes
- abilities
- equipment
- items
- loot tables
- regions
- encounters
- status effects

The same architecture principle applies:

> behavior engines remain stable while content expands through data.

---

# 83. Debug vs Production APIs

Debug commands should invoke real APIs.

Bad pattern:

```text
Debug command manually edits internal arrays and bypasses controller logic.
```

Preferred:

```text
Debug command
    ↓
public production API
    ↓
real system
```

This makes debug tools useful as integration tests.

---

# 84. Testing Philosophy

For each checkpoint:

1. test the exact behavior changed
2. test a neighboring behavior that could regress
3. inspect logs
4. inspect remote tree if transforms are involved
5. do not continue until the current checkpoint is understood

Presentation changes should additionally test both:

- expanded
- collapsed

and should often test:

- restart while collapsed
- restart while expanded
- switch monitors if relevant
- persisted settings reload

---

# 85. Presentation Test Matrix

When background/gameplay presentation becomes stable, use a matrix such as:

| Width | Collapsed | Expanded | Expected |
|---:|---:|---:|---|
| 800 | 60 | 180 | baseline |
| 800 | 64 | 192 | reference |
| 800 | 70 | 220 | taller user preference |
| 961 | 70 | 294 | stress test from prior persisted settings |
| custom | custom | custom | verify no logical-world corruption |

For each:

- background scale stable
- background bottom anchor correct
- gameplay logical coordinates unchanged
- ground baseline correct
- native bottom fixed
- top grows upward

---

# 86. What Not to Do Again

Avoid repeating these failure modes:

### Do not manually redefine `RegionViewport` to native dimensions

This broke authored gameplay coordinates.

### Do not have two presentation controllers manipulate the same frame

This caused stacked transforms.

### Do not assume `Process Mode = Disabled` stops event callbacks

It may not.

### Do not use child sprite scale to compensate for ancestor stretch blindly

Find the actual transform owner.

### Do not assume Inspector window settings are active

Persisted user settings may override them.

### Do not modify project stretch plus viewport stretch plus transforms in one checkpoint

One variable at a time.

### Do not “fix” gameplay anchor coordinates to compensate for a presentation bug

Protect gameplay first.

---

# 87. Decision Logging Procedure

When a new durable architectural decision is made:

Add:

```text
## ADR-XXX — Name

Status: Proposed / Accepted / Superseded

Context:
...

Decision:
...

Consequences:
...

Supersedes:
...
```

Do not silently edit an accepted ADR if the new decision materially changes it.

---

# 88. Proposed ADR Template

```markdown
## ADR-XXX — <Title>

**Status:** Proposed

### Context

What problem are we solving?

### Decision

What are we choosing?

### Why

Why this option instead of alternatives?

### Consequences

What becomes easier?
What becomes harder?
What assumptions are created?

### Validation

How will we know the decision works?
```

---

# 89. Future Project Structure Goal

A mature Questbar project may evolve toward:

```text
Scripts/
├── Application/
├── Content/
│   ├── Definitions/
│   ├── Registries/
│   ├── Factories/
│   └── Validation/
├── Gameplay/
│   ├── Combat/
│   ├── Encounters/
│   ├── Actors/
│   ├── Journey/
│   └── Progression/
├── Presentation/
│   ├── Regions/
│   ├── Actors/
│   ├── Effects/
│   └── Desktop/
├── Platform/
│   └── Windows/
├── UI/
└── Debug/
```

This is directional, not a mandate to reorganize immediately.

Move files only when ownership clarity justifies migration.

---

# 90. Naming Philosophy

Names should describe ownership and behavior accurately.

Examples of improvements already made:

```text
TryEngage
→ TryAcquireTarget
```

Prefer:

```text
ApplyDefinition
TryCreate
SelectTarget
ConfirmHeroImpact
```

over vague verbs like:

```text
Handle
DoThing
ProcessStuff
```

Method names should reveal intent.

---

# 91. C# Style Direction

Current style favors:

- explicit types where clarity matters
- Godot exports grouped by categories
- validation helpers
- early returns on invalid dependencies
- strongly typed events
- small responsibility-focused methods
- readable logs
- avoiding unnecessary cached mirror variables
- data-driven enum/state behavior

Do not over-compress code merely to reduce line count.

Maintainability is more important than cleverness.

---

# 92. Godot Resource Validation

Resource classes should preferably expose validation that can catch:

- missing scene
- invalid ID
- unsupported targeting mode
- invalid stat ranges
- malformed data

Factories should also return meaningful errors when runtime creation fails.

Do not allow invalid content to fail later as a mysterious null reference.

---

# 93. Monster Factory Error Philosophy

Factory creation should return actionable failure context.

Example conceptual API:

```csharp
TryCreate(
    string contentId,
    out MonsterActorController monster,
    out string error)
```

Failures might include:

- ID not registered
- ActorScene missing
- instantiated root not expected actor type
- definition invalid

This helps debug both content and tooling.

---

# 94. Monster Targeting Validation

Because `HighestThreatHero` is declared but unsupported:

- content validation should reject it for shipping/runtime use
- do not silently convert it to LowestHealthHero
- do not silently choose NearestHero
- make unsupported state obvious

This preserves future design freedom.

---

# 95. Actor Presentation Data

Monster definitions can drive presentation such as:

- scale
- modulate
- visual set
- portrait/icon references

Presentation values should not become arbitrary gameplay mechanics.

Keep stat ownership and visual ownership conceptually distinct even if stored in the same content resource.

---

# 96. Ranged / Melee Behavior Direction

Broad combat feel:

### Melee

- moves toward target
- attacks when in range
- may lunge as presentation
- returns toward party/right after combat

### Ranged

- usually holds formation
- attacks when target is within range
- should not backpedal merely to maintain maximum range
- projectiles visually travel to target

The exact movement style may later become content-driven.

---

# 97. Monster Movement Direction

Because monsters originate left of party:

- they generally move rightward toward heroes

Do not accidentally mirror the entire gameplay direction while working on screen-right anchoring.

Physical right-anchor on Windows is unrelated to world direction.

---

# 98. Travel Background Direction

During travel:

- background moves right
- party conceptually travels left

This is intentional and should remain visually consistent across parallax layers.

---

# 99. Presentation Sockets

Semantic sockets should be preferred for effect placement.

Examples:

```text
ProjectileOrigin
ImpactOrigin
```

Future:

```text
WeaponSocket
HeadSocket
StatusEffectSocket
NameplateAnchor
```

Actor root transforms remain gameplay-oriented.

---

# 100. Current Debugging / Learning Culture

Questbar development should remain understandable.

When introducing a Godot concept, explain things like:

- why Node2D vs Control matters
- what SubViewport does
- how transforms inherit
- what local/global position means
- how runtime Remote scene differs from Local editor scene
- how C# events persist independently of `_Process`
- how Resource exports work

The goal is not only to make Questbar work, but to understand why it works.

---

# 101. Questions to Ask Before Adding a New System

Before coding, answer:

1. What problem is this solving?
2. Who should own it?
3. Is there already an owner?
4. Is this gameplay, presentation, content, platform, or UI?
5. Does it need persistent data?
6. Does it belong in a Resource?
7. Is it actor-local or orchestrated?
8. Does it depend on physical pixels or logical gameplay coordinates?
9. Will it break if native window dimensions change?
10. Will adding more content require more code?
11. Can it be tested independently?
12. Is there a stable API boundary?
13. Are we about to create two owners for the same transform/state?

If answers are unclear, stop and design first.

---

# 102. Questions to Ask Before Reparenting a Node

1. Does the node inherit transform from its current parent?
2. Will it inherit unwanted scale from the new parent?
3. Are exported NodePaths pointing to it?
4. Are scripts using relative paths?
5. Is it a Control moving under Node2D or vice versa?
6. Should it scale with gameplay?
7. Should it scale with native presentation?
8. Is it screen-space?
9. Does it need clipping?
10. Does the editor composition still make sense?

---

# 103. Questions to Ask Before Changing Viewport Settings

1. Is this the root viewport or a SubViewport?
2. Is this physical size or logical size?
3. Does the gameplay coordinate system depend on the current override?
4. Is `SubViewportContainer.Stretch` active?
5. Is project-level canvas stretch active?
6. What ancestor already scales this content?
7. Can presentation solve the problem without changing logical dimensions?
8. Have expanded and collapsed modes both been tested?
9. Have current anchors been authored against this viewport?
10. Are we solving a visual problem by changing gameplay geometry?

If #10 is yes, stop.

---

# 104. Deferred Systems / Ideas

Do not treat these as implemented:

- threat
- taunt
- threat decay
- full encounter definitions
- wave definitions
- boss spawn style
- region definitions
- production parallax
- weather
- production fog
- production vignette
- final background fitting policy
- hover expansion
- advanced adaptive taskbar collision
- server-authoritative networking
- complete item/equipment pipeline
- full hero/ability content pipeline

They are planned directions, not current facts.

---

# 105. Current Next-Step Philosophy

Presentation work should proceed from stable outer layers inward:

1. Native window behavior stable.
2. Root presentation coordinate behavior stable.
3. Background stable.
4. Gameplay viewport remains 800×192.
5. Ground stable.
6. Gameplay miniature transform.
7. Screen effects.
8. Parallax.
9. data-driven region presentation.
10. hover expansion.

Do not skip directly to fancy visual systems while base framing is unresolved.

---

# 106. Known Source-of-Truth Priority

When information conflicts, use this priority:

1. **Current runtime behavior and current files**
2. **Accepted ADRs in this context document**
3. **Latest verified milestone**
4. **Current conversation decisions**
5. **Older chat recollection**
6. **Assumptions / guesses**

Never override current code based solely on remembered old implementation details.

---

# 107. How ChatGPT Should Handle Missing Context

If the user asks to continue a Questbar system and the exact current implementation is not available:

**Do not guess.**

Ask for or inspect:

- relevant `.cs`
- `.tscn`
- `.tres`
- spreadsheet
- screenshot
- runtime log
- ZIP

Then give exact changes.

This is especially important for:

- scene tree paths
- exported dependencies
- viewport settings
- controller method names
- content resource schemas
- Windows host behavior

---

# 108. Project-Level Chat Organization Recommendation

Within a ChatGPT Project, future Questbar conversations can be split by area:

```text
Questbar — Core Architecture
Questbar — Combat
Questbar — Content Pipeline
Questbar — Windows Host
Questbar — World Presentation
Questbar — UI
Questbar — Bugs / Debugging
Questbar — Progression
```

All should reference this context document.

This avoids one endlessly large conversation while preserving durable architecture.

---

# 109. Recommended Project Instructions for ChatGPT

The following can be copied into ChatGPT Project Instructions:

```text
You are helping develop Questbar, a Godot 4 C# taskbar-integrated idle RPG.

Read QUESTBAR_CONTEXT.md before architecture-sensitive work.

Work one small checkpoint at a time.
Explain the problem, ownership, data flow, Godot concept, exact filename/method placement, and maintenance implications.
Do not guess current implementation details when files can resolve them.
Protect completed milestones.
Do not silently change accepted architectural decisions.
After each tested checkpoint, stop for a design review before continuing.
Always include node types in scene-tree diagrams.
Prefer data-driven architecture and clear ownership.
Do not let multiple scripts continuously control the same transform/state unless explicitly coordinated.
Treat the 800×192 gameplay viewport as a protected logical coordinate system.
Native Questbar window dimensions and gameplay coordinates are separate.
Accuracy and maintainability are more important than speed.
```

---

# 110. Quick Context Snapshot

If a future conversation needs the shortest useful orientation:

```text
Questbar:
- Godot 4 / C#
- Windows taskbar-integrated idle RPG
- gameplay travels/fights right-to-left
- party on right, monsters from left
- stable logical gameplay space = 800×192
- bottom baseline = Y 192
- physical window bottom stays fixed; expansion is upward
- native window behavior owned by DesktopWindowHostController
- JourneyStateService owns Traveling/Encounter
- EncounterController owns monster roster/spawn/completion
- CombatController owns orchestration
- TargetingService owns target selection
- actors own local behavior
- profiles own resolved values
- CombatHealthState owns runtime health
- MonsterDefinition + registry + factory are data-driven content foundation
- targeting styles: nearest / lowest HP / random supported; highest threat deferred
- random 4×N monster spawn grid milestone complete
- heroes incapacitate rather than die/remove
- gameplay announces facts; presentation reacts
- background presentation is being separated from gameplay viewport
- do not resize logical viewport to match native window
- one checkpoint at a time; test before stacking systems
```

---

# 111. Historical Original `DECISIONS.MD`

The original decision file contained these accepted decisions and should be retained for historical continuity:

## ADR-001 — Permanent Gameplay Direction

Questbar travels and fights right-to-left.

## ADR-002 — Initial Window Design References

Reference visual dimensions 800×64 collapsed / 800×192 expanded, with upward expansion and fixed bottom edge.

## ADR-003 — User-Controlled Window Placement

Saved user width/height/monitor/anchor/offset settings control placement after first-launch defaults.

## ADR-004 — Windows Host Responsibility

`DesktopWindowHostController` owns native window behavior, not gameplay or world presentation.

These ADRs have been expanded rather than discarded in this document.

---

# 112. Change Log for This Context Document

## Initial expanded context build

This document was created by expanding the existing `DECISIONS.MD` and consolidating known Questbar milestones, workflow requirements, architecture boundaries, content conventions, targeting design, random-grid spawning, Windows host work, viewport lessons, and current world-presentation direction.

Future updates should add dated entries here rather than silently replacing major history.

---

# 113. Final Non-Negotiable Invariants

These are the fastest way to prevent major regressions:

1. **Gameplay direction stays right-to-left unless deliberately redesigned.**
2. **Logical gameplay coordinates stay 800×192.**
3. **Logical bottom is Y=192.**
4. **Native window bottom stays fixed; expansion happens upward.**
5. **Physical window dimensions do not redefine gameplay coordinates.**
6. **`DesktopWindowHostController` owns native Windows behavior.**
7. **Gameplay presentation does not own native placement.**
8. **Background does not inherit miniature gameplay scaling.**
9. **Ground does not inherit gameplay scaling if it is meant to remain full-width.**
10. **Target selection belongs to `TargetingService`.**
11. **Combat orchestration belongs to `CombatController`.**
12. **Encounter roster/spawn lifecycle belongs to `EncounterController`.**
13. **Actor-local behavior belongs to actor controllers.**
14. **Profiles own resolved values.**
15. **Runtime current health belongs to `CombatHealthState`.**
16. **Heroes incapacitate; they are not disposable monster actors.**
17. **New content should usually be data, not controller branches.**
18. **Unsupported features should fail clearly rather than silently fallback.**
19. **One small checkpoint at a time.**
20. **Never guess when current files can tell us the truth.**
21. **Never let two systems silently fight over the same transform/state.**
22. **Never “fix” a presentation problem by moving gameplay anchors unless the gameplay design itself changed.**
23. **Protect verified milestones before adding visual complexity.**
24. **Maintainability and correctness beat speed.**
25. **The finished architecture should be something worth maintaining and selling.**

---

**End of QUESTBAR_CONTEXT.md**
