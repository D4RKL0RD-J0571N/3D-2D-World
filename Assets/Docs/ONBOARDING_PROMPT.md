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

1. **`ExitTrigger` placement is unverified** — the component exists and publishes `LevelCompletedEvent`, but no code path instantiates it during generation. Likely placed manually somewhere.
2. **4 entity events publish but have no subscribers** — `EntityDamagedEvent`, `EntityHealedEvent`, `EntityStateChangedEvent`, `EntityLevelUpEvent`.
3. **5 new events publish but have no subscribers** — `PlayerHotbarSlotSelectedEvent`, `HotbarStateChangedEvent`, `RoomEnteredEvent`, `RoomExitedEvent`, `AreaTierChangedEvent`.
4. **`PlayerSpawnRequestedEvent` has no subscriber** — player respawn on death may be unimplemented.
5. **`ITickable` wiring gap** — services implement `ITickable` but `GameBootstrapper.Update()` only ticks the state machine; no service-level tick loop exists.
6. **`AreaSchedulerService.Tick` is a no-op** — the scheduling logic relies entirely on event-driven handlers.
7. **`RoomTrackerTrigger` respawn for save/load** — logic exists in `LevelLayoutRenderer.OnGameLoaded` but not runtime-verified.
8. **`GameBootstrapper` is a "God Object" for registration** — all services are registered there; may need sub-bootstrappers as the codebase grows.
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

When you're ready to work, ask the user what they'd like you to do, or propose a next step based on the "Known Issues & Gaps" above.
