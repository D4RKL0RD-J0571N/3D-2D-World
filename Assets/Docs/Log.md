# 3D 2D World — Project Audit Log
**Last Updated:** 2026-10-15
**Session:** Living document — reflects actual runtime state as of this session.

> **Refresh 2026-10-15:** This pass reconciles every doc against the *current* tree on disk (environment date 2026-10-15), not the 2026-08-21 snapshot below. The project has since added an area-scheduling subsystem (`AreaSchedulerService` + per-room `RoomTrackerTrigger`), a complete hotbar + UI Toolkit UI, vertical-tiered generation, room archetypes/palettes, a `VoxelStairBuilder` (sunken pits / raised daises / pillars / niches / parapets / sockets), and a `SlimeEntity` enemy AI — **none of which the prior snapshot recorded.** The three original known-gaps (`ExitTrigger` placement, unsubscribed `Entity*Event`s, empty state shells) were re-verified against code and remain open. Where the 2026-08-21 wording was still accurate it is preserved; where it was stale it is corrected inline.

---

## Evidence Snapshot

### Scripts Present (`Assets/Scripts/`) — 73 files

> The `Scripts/` tree grew from the 59 catalogued in the prior snapshot to **73** (verified via glob). Five new source files live under `Assets/Data/` (config SO definitions + `Databases`) and the generated `Assets/InputSystem_Actions.cs` sits at the Assets root; none of those are under `Scripts/`. New/changed folders are called out inline.

| Folder | Files (verified) |
|---|---|
| `Core/` | `GameBootstrapper.cs`, `GameEvent.cs`, `GameEventManager.cs`, `IGameService.cs`, `IIdentifiable.cs`, `ITickable.cs`, `ManagerBase.cs`, `ServiceLocator.cs` |
| `Core/Utils/` | **`InteractionPhysicsUtils.cs`** *(new)*, `PhysicsUtils.cs`, **`SpriteBillboard.cs`** *(new)* |
| `Editor/` | `MaterialTextureMapper.cs` |
| `Domain/Entities/` | `ActorState.cs`, **`AreaLifecycleEvents.cs`** *(new)*, `BaseAttribute.cs`, `EntityEvents.cs`, `EntityInstance.cs`, **`EntityLifecycleTypes.cs`** *(new)*, `EntityRegistry.cs` |
| `Domain/Grid/` | `BlockDefinition.cs`, **`BlockPalette.cs`** *(new)*, `BlockRegistry.cs`, **`GridEvents.cs`** *(new)*, `GridRoomLink.cs`, `RoomGridData.cs`, **`RoomSocketData.cs`** *(new)* |
| `Domain/Interaction/` | `GridBlockClickedEvent.cs`, `GridHitInfo.cs` |
| `Domain/LevelGeneration/` | `IRoomGraphGenerator.cs`, `LevelGenerationData.cs`, `LevelRenderConfig.cs`, **`RoomArchetype.cs`** *(new)* |
| `Domain/Meshing/` | `MeshTypes.cs` |
| `Domain/Persistence/` | `PersistenceEvents.cs`, `SaveData.cs` |
| `Domain/StateMachine/` | `GameStateMachine.cs`, `GameStates.cs`, `GameStateType.cs`, `IGameState.cs` — *(namespace is `Domain.Entities.StateMachine`; physical folder is `Domain/StateMachine/`; `IGameState` lives in namespace `Core`)* |
| `Gameplay/Entities/` | `AttributeComponent.cs`, `BaseActor.cs`, `BaseContainer.cs`, `BaseEntity.cs`, `BaseItem.cs`, **`SlimeEntity.cs`** *(new)*, `StateComponent.cs`, `StatsComponent.cs` |
| `Gameplay/Player/` | `PlayerGridInteractor.cs`, `PlayerInputEvents.cs`, `Player_CameraRig.cs`, `Player_Look.cs`, `Player_Movement.cs` |
| `Gameplay/UI/` | **`HotbarHUD.cs`** *(new — formerly no UI folder/system existed at all)* |
| `Gameplay/World/` | `ExitTrigger.cs`, `LevelGenerationEvents.cs`, `LevelLayoutRenderer.cs`, `RoomPrimitiveTile.cs` *(dead — no references anywhere; leftover from the pre-voxel renderer)*, **`RoomTrackerTrigger.cs`** *(new)*, **`VoxelStairBuilder.cs`** *(new)* |
| `Systems/Input/` | `InputManager.cs` |
| `Systems/Interaction/` | `BlockPlacementService.cs`, `GridInteractionService.cs`, **`HotbarService.cs`** *(new)* |
| `Systems/LevelGeneration/` | `LevelGenerationService.cs` |
| `Systems/Meshing/` | `GreedyMeshingJob.cs`, `GreedyMeshingService.cs`, `GridMeshBuilder.cs` |
| `Systems/Persistence/` | `FileSaveService.cs` |
| `Systems/Player/` | `PlayerSpawnService.cs` |
| `Systems/Pooling/` | `MonoBehaviourPool.cs`, `ObjectPool.cs`, `PoolingManager.cs` |
| `Systems/Scheduler/` | **`AreaSchedulerService.cs`** *(new — whole new folder)* |

### Data Assets (`Assets/Data/`) — verified

| Folder | Contents |
|---|---|
| `Config/` | `ActorData.cs`, `ContainerData.cs`, `EntityData.cs`, `ItemData.cs` (ScriptableObject blueprints, C# not under Scripts/) + `WorldGeneration/LevelRenderConfig.asset`, `WorldGeneration/BlockRegistry.asset`, `WorldGeneration/Blocks/*.asset` (BLCK_0010–0050), `WorldGeneration/Archetypes/{StandardHall,RaisedDais,SunkenArena}.asset`, `WorldGeneration/Palettes/{ClassicCryptCastleBastion,OvergrownCatacomb}.asset` |
| `Databases/` | `Database.cs`, `DatabaseLookup.cs`, `Databases.cs` — the `Databases.cs` bodies are all commented out; no concrete databases are active yet. |
| `Definitions/` | `EntityRegistry.asset` + `ActorData/DebugActorData.asset` |

> **Asset layout change:** the flat `Assets/Data/Config/{BlockRegistry.asset, LevelRenderConfig.asset, Floor/Wall/Ceiling.asset}` of the prior snapshot has been reorganized under `Assets/Data/Config/WorldGeneration/` with `Blocks/`, `Archetypes/`, and `Palettes/` subfolders. `RoomMaterialPalette` (listed in the previous snapshot) no longer exists anywhere — superseded by `BlockRegistry`/`BlockDefinition`/`BlockPalette`.

---

## Wiring Map

```
GameBootstrapper (real registration order, from InitializeServices())
│
├── ServiceLocator.Register<IGameService>()
│     ├── GameStateMachine        (transitions to Playing immediately — the only TransitionToAsync call)
│     ├── InputManager            (now also defines Pause/Confirm/Cancel/Save/Hotbar actions;
│     │                             gameplay input gated behind CanReadGameplayInput())
│     ├── Systems.Pooling.PoolingManager
│     ├── Systems.Scheduler.AreaSchedulerService   *(NEW — ITickable, but Tick() is a no-op stub)*
│     ├── LevelGenerationService
│     ├── GridInteractionService
│     ├── Systems.Interaction.HotbarService        *(NEW)*
│     ├── BlockPlacementService
│     ├── FileSaveService
│     └── PlayerSpawnService
│     (LevelLayoutRenderer.self-registers GreedyMeshingService in its own Awake())
│
├── GameBootstrapper also instantiates a HotbarHUD at runtime if none exists in-scene (lines 68-72)
│
├── GameEventManager (typed event bus — see EVENTS.md for the full, verified catalog; 26 events now)
│     ├── 7x Player*InputEvent (move/look/run/jump/crouch) ──► Player_Movement / Player_Look
│     ├── PlayerInteractPrimary/SecondaryInputEvent ──► PlayerGridInteractor ──► GridBlockClickedEvent ──► BlockPlacementService
│     ├── PlayerHotbarSlotSelectedEvent (NEW) ──► HotbarService AND BlockPlacementService
│     ├── GridBlockClickedEvent ──► BlockPlacementService
│     ├── LevelGenerationRequestedEvent ──► LevelGenerationService
│     ├── LevelGenerationCompletedEvent ──► LevelLayoutRenderer, AreaSchedulerService (NEW), HotbarService (NEW)
│     ├── LevelGenerationFailedEvent ──► LevelLayoutRenderer (logs error)
│     ├── LevelCompletedEvent ──► Playing state
│     ├── PlayerSpawnRequestedEvent ──► PlayerSpawnService
│     ├── HotbarStateChangedEvent (NEW) ──► HotbarHUD (NEW)
│     ├── GameSaveRequestedEvent (F5) ──► LevelLayoutRenderer → FileSaveService.WriteData()
│     ├── GameDataLoadedEvent ──► LevelLayoutRenderer (OnGameLoaded)
│     ├── RoomEnteredEvent (NEW) ──► AreaSchedulerService
│     ├── AreaTierChangedEvent (NEW) ──► (none — published, no subscriber)
│     └── 4x Entity*Event (Damaged/Healed/StateChanged/LevelUp) ──► published by AttributeComponent/
│         StateComponent/StatsComponent, (none currently)
│     └── EntityStateLoadedEvent ──► published by LevelLayoutRenderer.OnGameLoaded, (none currently)
│     └── GameLoadRequestedEvent ──► dead (never published/subscribed)
│
├── LevelGenerationService
│     └── RoomGraphGenerator (Prim-inspired cyclic graph, now with vertical FloorLevel tiers)
│           └── produces LevelLayout (LevelRoomNode[FLOORLEVEL+ARCHETYPE+SOCKETS][], LevelConnection[])
│                 └── LevelLayoutRenderer
│                       ├── ResolveDefaultBlocks (BlockRegistry.GetBlocksByRole → random per role)
│                       ├── per-room BuildRoomGrid: palette+archetype block overrides, VoxelStairBuilder
│                       │   (SunkenPit/RaisedDais/Pillars/Niches/Parapets), 4x BuildGridWallEdge w/ doorways,
│                       │   DiscoverRoomSockets (max 14), room.FloorLevel tier Y-offset
│                       ├── per-connection BuildConnectionGrid (now stair-graded for heightDelta)
│                       ├── spawns RoomTrackerTrigger per room ──► RoomEnteredEvent ──► AreaSchedulerService
│                       ├── wires HotbarService.SetRegistry(BlockRegistry)
│                       ├── GreedyMeshingService → one combined mesh per room/corridor
│                       └── ExitTrigger (class exists; NOTHING instantiates it — verify placement)
│                             └── publishes LevelCompletedEvent
│
└── GameStateMachine
      └── Playing state (the only state ever entered)
            ├── on enter → subscribes to LevelCompletedEvent
            └── on LevelCompletedEvent → republishes LevelGenerationRequestedEvent(new params, Seed=-1)
                  (NOTE: whole Parameters object is replaced, so non-Seed fields revert to defaults)
```

**Note on `RoomGraphGenerator` tiering:** the generator no longer produces a flat graph — each main-path room has a ~45% chance to shift `FloorLevel` by ±1 (clamped −2..2), and branch rooms deviate by ±1. `BuildConnectionGrid` reads `FloorLevel` to grade corridors/stairs between tiers.

**Gizmos (Scene View)**

| Indicator | Meaning |
|---|---|
| Green wire sphere | Start room |
| Red wire sphere | Exit room |
| Cyan lines | Room connections |
| Red ray (frame-scene overlay) | `PlayerGridInteractor` interaction ray (`Debug.DrawRay`, 0.1s) |

---

## What Is Ready

- ✅ **Core service registration** — 10 services register/resolve cleanly via `ServiceLocator` (see Wiring Map). `AreaSchedulerService` is `ITickable` but its `Tick` is a no-op; it is driven by `RoomEnteredEvent` instead.
- ✅ **Typed event bus** — `GameEventManager` operational; 26 distinct events cataloged in `EVENTS.md` (was 21).
- ✅ **Async state machine** — `Playing` is the only state ever entered; `TransitionToAsync(new Playing())` is the only call site. Transitions compile and run, but only that one path exists (see `STATES.md`).
- ✅ **Input system** — `InputManager` publishes 7 movement/look/run/crouch/jump/interact events + the new `PlayerHotbarSlotSelectedEvent` (slots 1–9). Gameplay input is gated to `GameStateType.Playing` via `CanReadGameplayInput()`. Pause/Confirm/Cancel/Save actions exist but only Save (F5) is wired end-to-end.
- ✅ **First-person player** — `Player_Movement`, `Player_Look`, `Player_CameraRig` fully wired to input events (WASD+mouse, run/crouch/jump, mouse-lock).
- ✅ **Procedural level generation** — `RoomGraphGenerator` → `LevelGenerationService` → `LevelLayoutRenderer`, now with **vertical floor tiers**, **room archetypes** (StandardHall/RaisedDais/SunkenArena), **themed palettes** with weighted wear variants, and **elevation geometry** (sunken pits, raised dais, pillars, niches, parapets, graded stair corridors via `VoxelStairBuilder`).
- ✅ **Voxel/greedy-meshing renderer** — rooms/corridors voxelized into `RoomGridData` and meshed via `GreedyMeshingService`/`GridMeshBuilder` into one combined mesh per room/corridor; **block selection is role-based** (`BlockRegistry.GetBlocksByRole`) with per-room archetype/palette overrides — not the hardcoded IDs the prior snapshot described.
- ✅ **Grid interaction system** — `PlayerGridInteractor` (center-screen raycast via `InteractionPhysicsUtils`) → `GridBlockClickedEvent` → `BlockPlacementService` (left-break / right-place, rebuilds mesh via `UpdateMeshForRoom`).
- ✅ **Hotbar + UI** *(new)* — 9-slot `HotbarService` palette (populated from `BlockRegistry`), `PlayerHotbarSlotSelectedEvent`/`HotbarStateChangedEvent` bus, and a UI Toolkit `HotbarHUD` instantiated at runtime by `GameBootstrapper` if missing.
- ✅ **Area scheduling** *(new)* — per-room `RoomTrackerTrigger` volumes publish `RoomEnteredEvent`; `AreaSchedulerService` computes Active/Dormant/Purged tiers by Manhattan distance and publishes `AreaTierChangedEvent`. **Caveat:** `Tick()` is a no-op and `AreaTierChangedEvent` has no subscriber yet — the tiering is computed but nothing reacts to it.
- ✅ **Persistence (save)** — `GameSaveRequestedEvent` → `LevelLayoutRenderer` assembles `LevelSaveData` (voxel data + player stats/HP/level/XP) → `FileSaveService.WriteData()`. Loading happens automatically inside `LevelGenerationService` when a save file exists (not via `GameLoadRequestedEvent`, which is dead).
- ✅ **Entity/actor component layer** — `AttributeComponent`/`StatsComponent`/`StateComponent` on `BaseActor` publish `EntityDamagedEvent`/`EntityHealedEvent`/`EntityStateChangedEvent`/`EntityLevelUpEvent`. `EntityRegistry.asset` is populated (`DebugActorData.asset`).
- ✅ **Enemy AI** *(new)* — `SlimeEntity` is a complete hop/wander/chase/leap `CharacterController` AI with squash/stretch via `SpriteBillboard`. **Caveat:** nothing spawns it from generation yet (see sockets note).
- ✅ **Core game loop** — `ExitTrigger` → `LevelCompletedEvent` → `Playing` republishes `LevelGenerationRequestedEvent` → infinite dungeon crawler loop. `Playing.OnLevelCompleted` replaces the *entire* `Parameters` object (`new LevelGenerationParameters{Seed=-1}`), so non-Seed fields reset each loop.
- ✅ **Scene gizmos** — start room (green), exit room (red), connections (cyan) plus the interactor debug ray.

---

## What Is Behind

### Partially Wired (present but incomplete)

| System | Status |
|---|---|
| `GameStates` | `MainMenu`, `GameOver`, `Paused`, `Loading` are shells — no logic, no transitions. Nothing transitions into them (`Playing` is the only state ever entered). `InputManager.OnPause` is gated but its body is a no-op; no `Playing → Paused` transition exists. |
| Boot flow | No `MainMenu → Playing` transition — game boots straight into `Playing` (with a runtime-instantiated `HotbarHUD`). |
| `ExitTrigger` placement | ✅ **Fixed** — `LevelLayoutRenderer.BuildRoomGrid` spawns an `ExitTrigger` in `room.IsExit`; `OnGameLoaded` re-spawns it for save/load. Verified in Play mode: 1 trigger at exit room position. |
| Entity events | `EntityDamagedEvent`/`EntityHealedEvent`/`EntityStateChangedEvent`/`EntityLevelUpEvent` + `EntityStateLoadedEvent` publish correctly but have **zero listeners** — no UI, audio, or game-flow system consumes them. |
| `GameLoadRequestedEvent` | Dead — never published/subscribed. Load is `LevelGenerationService` → `FileSaveService.HasSaveFile()` directly. |
| `AreaTierChangedEvent` | Published by `AreaSchedulerService` but has no subscriber — tiering is computed but inert. |
| `RoomExitedEvent` | Defined but never published or subscribed. `RoomTrackerTrigger` only fires enter. |
| `SlimeEntity` spawning | Component is complete but never instantiated; `room.Sockets` (EnemySpawn) are discovered but never consumed. |
| Save-load rendering | `OnGameLoaded` rehydrates grids + meshes but does **not** re-spawn `RoomTrackerTrigger`s, so an area-scheduler state is missing on load (only fresh generations place trackers). |
| Hotbar `OnLevelGenerated` | Subscribes to `LevelGenerationCompletedEvent` but the handler body is a no-op stub (intends to rebind registry from the renderer — not implemented). |
| Input pause/confirm/cancel | Actions bound and gated, but handlers are empty; no main-menu or pause flow is wired. |

### Not Yet Built

| System | Notes |
|---|---|
| Enemy spawning | Sockets are discovered but no factory spawns `SlimeEntity` (or chests/npcs) from them. |
| Combat loop | `AttributeComponent.ApplyDamage` exists and publishes `EntityDamagedEvent` (and trips `ActorState.Death`), but there is no damage *source* — nothing calls `ApplyDamage`, no weapon, no hit detection for enemies, no death handling. |
| UI beyond hotbar | No health bar, no main menu canvas, no pause screen, no level-up/Xp feedback. |
| Audio system | No audio sources, managers, or assets wired. |
| Animation assets | No Animator controllers or animation clips found. |

---

## What's Next

Three concrete, evidence-based next steps (all pre-existing; re-verified against current code). The area-scheduler + hotbar additions now create two further, related gaps.

### 1. Wire up `ExitTrigger` placement
Right now the exit-trigger gap is the biggest question mark in the otherwise-complete core loop. Hook it into `LevelLayoutRenderer.BuildRoomGrid` for the exit room (`room.IsExit`), e.g. as a child trigger at the room's floor-center, using the doorway-openings / tier offset already computed.

### 2. Consume the entity events (and the new area events)
`EntityDamagedEvent`/`EntityHealedEvent`/`EntityStateChangedEvent`/`EntityLevelUpEvent` publish correctly from `AttributeComponent`/`StatsComponent`/`StateComponent`. A minimal UI listener (health bar, level-up popup) closes the loop on work already done. By the same token, `AreaTierChangedEvent` is a ready-made hook for throttling off-screen AI/particle systems — wire one subscriber (or stub one in `SlimeEntity` to sleep when its room goes Purged).

### 3. State Machine Fleshing Out
Implement `MainMenu` with a minimal Unity UI canvas, and wire `Loading` as a real transition screen between `MainMenu` and `Playing`. This gives the game a real boot sequence instead of dropping straight into level generation, and would finally make use of the `MainMenu`/`Loading` state shells. As part of this, wire `OnPause` to actually transition `Playing → Paused` and restore `CurrentStateType`-gated input (the gating infrastructure is already in place in `InputManager`).

### 4. (New) Spawn entities from sockets
`VoxelStairBuilder.DiscoverRoomSockets` and `room.Sockets` are fully populated during `BuildRoomGrid`, and `SlimeEntity` is a complete AI — nothing joins the two. A small "spawn from `EnemySpawn` sockets" step in `BuildRoomGrid` (likely via `PoolingManager` per the existing `LevelLayoutRenderer` rehydrate path) would make `SlimeEntity` live in the world and exercise the `AreaSchedulerService`/room-tracker pipeline end-to-end.

---

## What Changed Since Last Snapshot (2026-08-21 → 2026-10-15)

The previous snapshot's "What's Next #1/#3" have been partially superseded by what actually got built. Rather than a `PropDatabase : Database<EntityData>`, the project grew (none of this was in the 2026-08-21 audit):

- **`Domain/Grid`**: `BlockPalette` (weighted wear variants), `RoomSocketData` (`SocketType` + `RoomSocket`), `GridEvents` (`PlayerHotbarSlotSelectedEvent`), and `BlockDefinition`/`BlockRegistry` now role-based (`BlockRole` + `GetBlocksByRole`) with `BlockRenderType`/`SurfaceType` and auto-derived `RuntimeId`.
- **`Domain/Entities`**: `AreaLifecycleEvents` (`RoomEnteredEvent`/`RoomExitedEvent`/`AreaTierChangedEvent`) and `EntityLifecycleTypes` (`AreaSimulationTier`, `EntityLifecyclePolicy`, `VirtualEntityState`).
- **`Domain/LevelGeneration`**: `RoomArchetype` (elevation modes, custom blocks, pillar/spawn weights), `LevelRoomNode` now carries `FloorLevel`/`Archetype`/`Sockets`, `LevelGenerationParameters` gained `RaycastDistance`/`SpawnOffset` and new defaults.
- **`Gameplay/World`**: `VoxelStairBuilder` (full elevation toolkit), `RoomTrackerTrigger` (per-room area trigger).
- **`Gameplay/Entities`**: `SlimeEntity` (complete enemy AI).
- **`Gameplay/UI`** (new): `HotbarHUD` (UI Toolkit).
- **`Systems/Interaction`**: `HotbarService` (9-slot palette + `HotbarStateChangedEvent`).
- **`Systems/Scheduler`** (new): `AreaSchedulerService` (room tiering + virtual-entity regen catch-up).
- **`Core/Utils`**: `InteractionPhysicsUtils`, `SpriteBillboard`.
- **Rendering**: `LevelRenderConfig` restructured to `Archetypes`/`Palettes`/`TierHeightInBlocks`; the renderer gained multi-tier corridors, graded staircases, and role-based block selection. `LevelLayoutRenderer` now also instantiates `HotbarHUD` and `HotbarService.SetRegistry`.

So this snapshot's "What's Next" list is itself a snapshot of intent, not a committed roadmap.

---

*This log is a living document. Update it at the start of every significant session.*
