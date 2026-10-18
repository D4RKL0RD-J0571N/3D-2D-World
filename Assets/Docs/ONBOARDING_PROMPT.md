# Project Onboarding Prompt: 3D 2D World

You are a Unity/C# engineer being added to the "3D 2D World" project. Your job is to understand the codebase, its architecture, and its current state well enough to contribute meaningfully. Read the following context carefully before doing any work.

---

## 1. What this project is

A Unity 6 project implementing a **voxel-based procedural dungeon crawler** with an infinite loop structure. Rooms are generated procedurally as a connected graph, voxelized into a grid, and rendered as combined meshes via greedy meshing. The player explores rooms, interacts with voxels (block placement/destruction presumably via the interaction system), and transitions between rooms trigger area-scheduling events that manage entity sleep/wake states across load boundaries (work in progress).

The project lives at `D:\Development\Game Development\3D 2D World\` and follows a standard Unity layout:
- `Assets/Scripts/` — all source code (73 .cs files across ~10 namespaces)
- `Assets/Docs/` — project documentation (this prompt draws heavily from these files)
- `Assets/Data/` — C# config/runtime data alongside ScriptableObject assets

---

## 2. Core Architecture

### 2.1 Entry Point & Lifecycle
- `GameBootstrapper` (Core namespace, `Assets/Scripts/Core/GameBootstrapper.cs`) is a singleton `MonoBehaviour` with `DefaultExecutionOrder(-100)`.
- Its `Awake()` clears the event bus and registers all services via `ServiceLocator.Register(...)`.
- `InitializeServices()` currently registers **10 services** (not 8 — 2 were added in Phase 2):
  1. `GameStateMachine` (Core)
  2. `InputManager` (Systems.Input)
  3. `PoolingManager` (Systems.Pooling)
  4. `AreaSchedulerService` (Systems.Scheduler) — **NEW**
  5. `LevelGenerationService` (Systems.LevelGeneration)
  6. `GridInteractionService` (Systems.Interaction)
  7. `HotbarService` (Systems.Interaction) — **NEW**
  8. `BlockPlacementService` (Systems.Interaction)
  9. `FileSaveService` (Systems.Persistence)
  10. `PlayerSpawnService` (Systems.Player) — registered late with a comment "Also making sure the spawn service is registered!"
- The **only** call to `GameStateMachine.TransitionToAsync()` is `TransitionToAsync(new Playing())` at `GameBootstrapper.cs:74`.
- `HotbarHUD` (Gameplay.UI) is auto-instantiated if not found in the scene (`GameBootstrapper.cs:68-72`).
- `Update()` only ticks the state machine: `_stateMachine?.Tick(Time.deltaTime)`.

### 2.2 Service Locator
- `ServiceLocator` (Core namespace) follows `IGameService` contract.
- Has `Register<T>`, `Get<T>`, `TryGet<T>`, `Unregister<T>`, and `Clear()`.
- Handles overwriting on scene reloads.
- `IGameService` requires `Initialize()` and `Shutdown()` methods.

### 2.3 State Machine
- `GameStateMachine` implements `IGameService, ITickable`.
- Current state: `Playing` (the only state ever entered; no other transitions exist).
- `Playing.OnEnterAsync()` subscribes to `LevelCompletedEvent`; `OnLevelCompleted()` publishes `LevelGenerationRequestedEvent` with `Parameters = new LevelGenerationParameters { Seed = -1 }` (a **fresh** parameters object, not a mutation).
- `MainMenu`, `GameOver`, `Paused`, `Loading` are real `IGameState` implementations but **never instantiated** — they're shells.
- `ITickable` interface documents services are "driven by `GameBootstrapper.Update()`" but **no `ITickable` service's Tick is actually invoked** — only the state machine is ticked, and it only forwards to the active state's `OnTick`.

### 2.4 Event Bus
- `GameEventManager<T>` (Core namespace) — generic typed event bus, no static scatter.
- As of 2026-10-15: **26 events** cataloged across 8 files.
- **9 events have publishers but no subscribers**: 4 original entity events (`EntityDamagedEvent`, `EntityHealedEvent`, `EntityStateChangedEvent`, `EntityLevelUpEvent`) + 5 added in Phase 2 (`PlayerHotbarSlotSelectedEvent`, `HotbarStateChangedEvent`, `RoomEnteredEvent`, `RoomExitedEvent`, `AreaTierChangedEvent`).
- `GameLoadRequestedEvent` is fully dead (no publisher, no subscriber).
- Full event details: see `Assets/Docs/EVENTS.md`.

---

## 3. Key Subsystems

### 3.1 Procedural Level Generation
- `LevelGenerationService` generates a room graph, voxelizes rooms/corridors into `RoomGridData`, and meshes them via `GreedyMeshingService` (Burst-compiled).
- `LevelGenerationParameters` (ScriptableObject-style config):
  - `MainPathRooms = 6`, `GridSpacing = 32`, `GridHalfExtents = (6, 6)`, `MinRoomSize = 12`, `MaxRoomSize = 22`, `CycleChance = 0.35`, `RaycastDistance`, `SpawnOffset`.
- Rooms have `FloorLevel` (−2..2 range, ~45% per main-path level), `Archetype`, `Sockets`, and block palettes.
- Block selection is **role-based** via `BlockRegistry.GetBlocksByRole(BlockRole)` — not hardcoded IDs.
- `LevelLayoutRenderer` (Gameplay.World) handles: voxelization → greedy meshing → elevation/stair/socket pipelines → spawns `RoomTrackerTrigger` prefabs for inter-room tracking → wires hotbar UI → handles save/load rendering gap (not yet fully wired).

### 3.2 Voxel Grid & Rendering
- `RoomGridData` + `GreedyMeshingService` produce one combined mesh per room/corridor.
- `RoomPrimitiveTile` still exists but is **dead code** (no references).
- The old doc description of pooled `RoomPrimitiveTile` prefabs is obsolete.

### 3.3 Room Transitions & Area Scheduling
- `RoomTrackerTrigger` (Gameplay.World) publishes `LevelCompletedEvent` when triggered (presumably by reaching the exit socket).
- `RoomEnteredEvent` / `RoomExitedEvent` are published during room transitions but currently have no subscribers.
- `AreaSchedulerService` (newer):
  - Implements `ITickable` (but Tick is a no-op and never called).
  - Subscribes to `LevelGenerationCompletedEvent` and `RoomEnteredEvent`.
  - Manages per-room simulation tiers: Active / Dormant / Purged via `AreaTierChangedEvent`.
  - Maintains a virtual entity registry for entities in non-active rooms with time-based HP regeneration catch-up.
  - Entity lifecycle policies: `DespawnAndRespawn` vs `PersistAndSleep`.

### 3.4 Player Interaction
- Input path: `InputManager` → `PlayerGridInteractor` → publishes `GridBlockClickedEvent` → consumed by `BlockPlacementService`.
- Hotbar: `HotbarService` (System) + `HotbarHUD` (Gameplay.UI) — `PlayerHotbarSlotSelectedEvent` → `HotbarService` → `HotbarStateChangedEvent` → `HotbarHUD`.
- `PlayerSpawnService` — exists but its trigger (`PlayerSpawnRequestedEvent`) has no subscriber.

### 3.5 Persistence
- `FileSaveService` handles save/load.
- `LevelLayoutRenderer.OnGameLoaded` is supposed to respawn `RoomTrackerTrigger` instances for save/load — exists but needs runtime verification.
- Data uses SOs as read-only blueprints + pure C# classes for runtime instances (strict separation enforced).

---

## 4. Known Issues & Gaps (as of 2026-10-15)

### ✅ Fixed
- **`ExitTrigger` placement** — `LevelLayoutRenderer` now spawns `ExitTrigger` in the exit room during generation and re-spawns it on save/load. Verified in Play mode.

### 🔧 Remaining Gaps (in rough priority order)

1. **Enemy spawning from sockets** — `VoxelStairBuilder.DiscoverRoomSockets` populates `EnemySpawn` sockets per room, but nothing consumes them. `SlimeEntity` AI is complete but never instantiated. This is the highest-impact content gap.
2. **Combat loop is unclosed** — `AttributeComponent.ApplyDamage` exists and publishes `EntityDamagedEvent`, but there is no damage source (no weapon, no hit detection, no projectile). The player cannot fight back.
3. **Entity events have zero subscribers** — `EntityDamagedEvent`, `EntityHealedEvent`, `EntityStateChangedEvent`, `EntityLevelUpEvent` publish correctly but no UI, audio, or game-flow system reacts. A health bar would close this loop.
4. **Area events are inert** — `AreaTierChangedEvent` is published by `AreaSchedulerService` but has no subscriber. `RoomExitedEvent` is defined but never published.
5. **State machine shells** — `MainMenu`, `GameOver`, `Paused`, `Loading` are real `IGameState` implementations but never entered. Game boots straight into `Playing`.
6. **`ITickable` wiring gap** — services implement `ITickable` but `GameBootstrapper.Update()` only ticks the state machine; no service-level tick loop exists.
7. **`PlayerSpawnRequestedEvent` subscriber** — `PlayerSpawnService` exists but its handler isn't wired to the event bus.
8. **No audio system** — no audio sources, managers, or assets wired.
9. **Namespace/folder mismatch in StateMachine** — namespace is `Domain.Entities.StateMachine` but folder is `Assets/Scripts/Domain/StateMachine/`; `IGameState.cs` is in namespace `Core`.

---

## 5. File Map (important paths)

| Path | Purpose |
|---|---|
| `Assets/Scripts/Core/GameBootstrapper.cs` | Entry point, service registration, sole `TransitionToAsync` call (line 74) |
| `Assets/Scripts/Core/ServiceLocator.cs` | DI container for `IGameService` |
| `Assets/Scripts/Core/GameEventManager.cs` | Generic typed event bus |
| `Assets/Scripts/Domain/StateMachine/` | State machine (`GameStateMachine`, `IGameState`, state classes, `GameStateType`) |
| `Assets/Scripts/System/Scheduler/AreaSchedulerService.cs` | Room-tier entity scheduling |
| `Assets/Scripts/Systems/LevelGeneration/` | Level generation pipeline |
| `Assets/Scripts/Systems/Interaction/` | Grid interaction, block placement, hotbar |
| `Assets/Scripts/Gameplay/World/LevelLayoutRenderer.cs` | Voxelization, meshing, trigger spawning |
| `Assets/Scripts/Gameplay/World/ExitTrigger.cs` | Publishes `LevelCompletedEvent` |
| `Assets/Scripts/Domain/Grid/GridEvents.cs` | Grid-related events |
| `Assets/Scripts/Domain/Entities/AreaLifecycleEvents.cs` | Area/room lifecycle events |
| `Assets/Docs/EVENTS.md` | Full 26-event catalog with publishers/subscribers |
| `Assets/Docs/STATES.md` | State machine documentation with wiring gaps |
| `Assets/Docs/LEVEL_GENERATION.md` | Level gen parameters, algorithm, data model, renderer pipeline |
| `Assets/Docs/Log.md` | Script inventory, service wiring map, what's next |
| `Assets/Docs/CONTEXT.md` | Living project status and next tasks |
| `Assets/Docs/AUDIT_LOG.md` | Audit history and findings |
| `Assets/Docs/ARCHITECTURE.md` | Philosophy + concrete technical sections |

---

## 6. Your Job

Before making any changes, you should:
1. Read the relevant doc files above to understand context.
2. Read the relevant C# source files.
3. Verify claims against actual `Publish`/`AddListener` call sites (the docs were audited against code, but always double-check yourself).
4. When you find discrepancies or make decisions, note them.

**Do not edit scene/prefab YAML files unless you've confirmed via `unity status` that no live Editor is reachable.**

## 5. Suggested Next Milestones (Content & Polish)

Work in small, player-visible milestones. Do not build frameworks for hypothetical future needs.

### Milestone A — Spawn Enemies (highest impact)
**What the player notices:** Enemies appear in rooms and move around.
**Files involved:** `LevelLayoutRenderer.cs`, `SlimeEntity.cs`, `PoolingManager`, `AreaSchedulerService`.
**Approach:** In `BuildRoomGrid`, after `DiscoverRoomSockets`, iterate `room.Sockets` where `SocketType == EnemySpawn` and spawn a `SlimeEntity` prefab via `PoolingManager` (or `Instantiate` if pooling isn't wired yet). Ensure `SlimeEntity` sleeps when its room goes `Purged` by subscribing to `AreaTierChangedEvent`.
**Completion criteria:** Enter Play mode, walk into a room, see at least one Slime hopping.

### Milestone B — Close the Combat Loop
**What the player notices:** They can damage and kill enemies.
**Files involved:** `PlayerGridInteractor.cs` or a new `WeaponSystem`, `AttributeComponent.cs`, `EntityDamagedEvent`.
**Approach:** Add a primary-fire raycast or melee sweep from the player. On hit, call `attributeComponent.ApplyDamage(amount)`. `SlimeEntity` already has health via `BaseActor` + `AttributeComponent`.
**Completion criteria:** Attack a Slime, its health drops, `EntityDamagedEvent` fires, and at 0 HP it dies (disable or destroy).

### Milestone C — Minimal Health HUD
**What the player notices:** A health bar reflects damage taken.
**Files involved:** New `Gameplay/UI/HealthHUD.cs`, `EntityDamagedEvent`, `EntityHealedEvent`.
**Approach:** UI Toolkit or uGUI canvas. Subscribe to `EntityDamagedEvent`/`EntityHealedEvent` when the player is the target. Update a slider. Keep it minimal — one bar, no frames.
**Completion criteria:** Take damage from a Slime, health bar decreases.

### Milestone D — Main Menu + Boot Flow
**What the player notices:** A title screen instead of dropping straight into the dungeon.
**Files involved:** `GameStates.cs`, `GameBootstrapper.cs`, `InputManager.cs`, UI canvas.
**Approach:** Create a simple UI canvas with "Play" button. Wire `GameBootstrapper` to transition `None → MainMenu` instead of `None → Playing`. On button click, transition `MainMenu → Playing`. Gate gameplay input behind `CurrentStateType == Playing` (infrastructure already exists in `InputManager`).
**Completion criteria:** Launch game, see title screen, click Play, enter dungeon.

### Milestone E — Audio Feedback
**What the player notices:** Sounds for footsteps, block breaks, enemy hits.
**Files involved:** New `Systems/Audio/AudioManager.cs` (ServiceLocator-registered), event subscriptions.
**Approach:** Minimal `AudioManager` that subscribes to `GridBlockClickedEvent`, `EntityDamagedEvent`, `LevelCompletedEvent`. Play one-shot clips. Use free placeholder SFX.
**Completion criteria:** Breaking a block and hitting an enemy both make distinct sounds.

---

## 6. Your Job

Before making any changes, you should:
1. Read the relevant doc files above to understand context.
2. Read the relevant C# source files.
3. Verify claims against actual `Publish`/`AddListener` call sites (the docs were audited against code, but always double-check yourself).
4. When you find discrepancies or make decisions, note them.

**Do not edit scene/prefab YAML files unless you've confirmed via `unity status` that no live Editor is reachable.**

When you're ready to work, ask the user what they'd like you to do, or propose a next step based on the "Suggested Next Milestones" above.
