# Project Context: 3D 2D World

This document maintains the "living" state of the project development. It is updated as milestones are reached and focus shifts to ensure continuity across sessions.

## Current Status
- **Project Phase**: Phase 2 - Stabilization
- **Current Focus**: Player Systems & World Interaction refinement (room-level entity scheduling, hotbar UI wiring, inter-room transition feedback).

## Completed Milestones
- [x] Core Bootstrapper & Service Locator
- [x] Generic Typed Event Bus (`Core.GameEventManager`)
- [x] Async State Machine (`IGameState`)
- [x] Procedural Level Generation System
- [x] Architecture Decision Records (ADRs)
- [x] Voxel grid rendering (`RoomGridData` + `GreedyMeshingService`), grid interaction, and save/load persistence — added since the last full doc pass, see `Log.md`
- [x] Area lifecycle scheduling & entity sleep/wake (`AreaSchedulerService`) — room-tier simulation (Active/Dormant/Purged), virtual entity registry, time-based HP regeneration catch-up
- [x] Room-entry transition tracking (`RoomTrackerTrigger` + `RoomEnteredEvent`/`RoomExitedEvent`) — LevelLayoutRenderer respawns `RoomTrackerTrigger` prefabs per socket
- [x] Vertical tier generation (floor-level room graph in `RoomGraphGenerator`, ~45% of rooms per main-path level, clamped −2..2)
- [x] Minimal hotbar UI (`Systems.Interaction.HotbarService` + `Gameplay.UI.HotbarHUD`, auto-instantiated by `GameBootstrapper`)
- [x] Documentation Refresh (2026-08-21) — `STATES.md`, `EVENTS.md`, `LEVEL_GENERATION.md`, `Log.md`, `AUDIT_LOG.md`, and `ARCHITECTURE.md`'s technical sections corrected against actual code; see `AUDIT_LOG.md` for the full list of what was wrong and why
- [x] Documentation Refresh (2026-10-15) — precision corrections to `STATES.md`, expanded Phase 2 milestones in `CONTEXT.md`; see `AUDIT_LOG.md`
- [x] ExitTrigger placement wired (2026-10-15) — `LevelLayoutRenderer` now spawns `ExitTrigger` in the exit room during generation and re-spawns it on save/load
- [x] Milestone A — Enemy spawning from `EnemySpawn` sockets (2026-10-15) — `LevelLayoutRenderer` now spawns `SlimeEntity` prefabs via `Instantiate`, assigns a `SlimeActorData` blueprint, and `SlimeEntity` sleeps/wakes on `AreaTierChangedEvent`

## Active Work / In-Progress
- **Combat Loop**: Closing the player-to-enemy damage path (Milestone B).
- **World Interaction**: Ensuring snappy transitions between player actions and environmental responses.

## Recent Decisions & Context
- **Data/Instance Separation**: Strict enforcement of ScriptableObjects as read-only blueprints and pure C# classes for runtime state.
- **Async Transitions**: All major state changes (Level Load, State Switch) are handled via `OnEnterAsync` / `OnExitAsync`. In practice only one transition (`None → Playing`) is ever triggered right now — see `STATES.md`.
- **Event Architecture**: Decoupled communication via `GameEventManager<T>`.
- **Room-tier simulation**: `AreaSchedulerService` manages per-room simulation tiers via `AreaTierChangedEvent`; entities enter/exit rooms are tracked via `RoomEnteredEvent`/`RoomExitedEvent`, with virtual entity state persisted between visits.
- **Hotbar input path**: `HotbarService` subscribes `PlayerHotbarSlotSelectedEvent` and publishes `HotbarStateChangedEvent`; `HotbarHUD` consumes the latter. See `EVENTS.md` and `Log.md`.

## Next Immediate Tasks
The 2026-08-21 doc audit plus the 2026-10-15 follow-up surfaced these evidence-based next steps (full detail in `Log.md`'s "What's Next" section):
1. Wire a listener for the 4 entity events that publish correctly but have no subscribers (`EntityDamagedEvent`, `EntityHealedEvent`, `EntityStateChangedEvent`, `EntityLevelUpEvent`).
3. Wire listeners for the 5 new events that have publishers but no subscribers yet (`PlayerHotbarSlotSelectedEvent`, `HotbarStateChangedEvent`, `RoomEnteredEvent`, `RoomExitedEvent`, `AreaTierChangedEvent`).
4. Wire the `AreaSchedulerService` Tick — it implements `ITickable` but is never actually ticked (see STATES.md § Notes); either wire service ticking in `GameBootstrapper.Update()` or rely solely on the event-driven handlers.
5. Flesh out `MainMenu`/`Loading` states with a minimal boot flow — they're correctly-shaped `IGameState` shells that have never been wired to a transition.
6. Verify that `RoomTrackerTrigger` respawn in `LevelLayoutRenderer.OnGameLoaded` works end-to-end for save/load (the respawn logic exists but needs runtime confirmation across area boundaries).
7. Investigate `PlayerSpawnRequestedEvent` usage — it publishes but has no subscriber; unclear whether player respawn on death is intended.