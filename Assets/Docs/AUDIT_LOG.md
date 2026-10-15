# Documentation Audit Log

This document tracks the progress of the project documentation update. It serves as the persistent state for this task to ensure continuity across sessions.

## Project Status Overview
- **Current Phase**: Documentation Audit & Refinement
- **Status**: Phase 2 complete (this session, 2026-08-21). Phase 3 partially started. Pass 2 (2026-10-15) — precision corrections and milestone sync.

## Master Plan

### Phase 1: Discovery & Inventory
- [x] Task 1.1: Core System Audit (Verified)
- [x] Task 1.2: Domain & Gameplay Mapping (Completed Discovery)
- [x] Task 1.3: Redundancy Check (Resolved — see below)

### Phase 2: Documentation Synthesis
- [x] Task 2.1: Populate `EVENTS.md` — full rewrite this session, every entry verified against real `Publish`/`AddListener` call sites
- [x] Task 2.2: Populate `STATES.md` — full rewrite this session, corrected from a broken auto-extraction (see Known Issues)
- [ ] Task 2.3: Create Feature Docs (`behavior.md`) — not started. `Docs/Features/` doesn't exist yet; this is genuinely new work, not a correction

### Phase 3: Cleanup & Hardening
- [ ] Task 3.1: Consolidation of Redundant Logic — N/A, see Task 1.3 resolution below
- [ ] Task 3.2: ADR Logging (`DECISIONS.md`) — no new architecture *decisions* came out of this pass (it was a documentation audit, not a design session); `DECISIONS.md` itself was checked against code and found accurate, no changes made
- [x] Task 3.3: Final Context Update — `CONTEXT.md` updated this session

---

## Audit Log

### Pass 1: 2026-08-21 — Full Discovery & Rewrite
(See the full findings below in "Audit Findings" and "Known Issues / Notes".)

### Pass 2: 2026-10-15 — Precision Corrections & Phase 2 Milestone Sync
Scope: targeted verification and precision edits, no full rewrites.

**Files updated:**
- `STATES.md` — Added two precision notes to § Notes:
  1. `Playing.OnLevelCompleted` publishes `LevelGenerationRequestedEvent` with `Parameters = new LevelGenerationParameters { Seed = -1 }` — a **fresh object**, not a mutation. All non-Seed parameters revert to defaults on each loop iteration. (Code: `GameStates.cs:27-30`)
  2. `AreaSchedulerService` implements `ITickable` but its `Tick` is a no-op (comment-only body). Furthermore, `GameBootstrapper.Update()` (line 35) only ticks `GameStateMachine`, which in turn only forwards to the active state's `OnTick`. No registered `ITickable` service's `Tick` is ever actually invoked. The `AreaSchedulerService` operates entirely through event-driven handlers. (Code: `GameBootstrapper.cs:35`, `GameStateMachine.cs:59-62`, `AreaSchedulerService.cs:56-58`)
- `CONTEXT.md` — Expanded "Completed Milestones" to reflect 6 Phase 2 additions (area lifecycle scheduling, room-entry tracking, vertical tier generation, hotbar UI, 2026-10-15 precision corrections). Expanded "Next Immediate Tasks" from 3 to 7 items, derived from the Log.md audit findings. Added three new "Recent Decisions & Context" bullets (room-tier simulation, hotbar input path, ITickable wiring gap).
- `AUDIT_LOG.md` — Added this pass entry.

**What was NOT changed (intentional):**
- `EVENTS.md`, `LEVEL_GENERATION.md`, `Log.md`, `ARCHITECTURE.md` — verified accurate during this pass; no edits needed.

---

## Audit Findings

### Task 1.1: Core System Audit
- **Service Locator**: Robust. Follows `IGameService` contract. Handles overwriting for scene reloads. Also has `TryGet<T>` and `Unregister<T>` methods not previously documented in `ARCHITECTURE.md`'s code sample — added this session.
- **Bootstrapper**: Correctly handles singleton lifecycle. Registers 8 services (not the Phase-1/2/3/4-commented structure `ARCHITECTURE.md`'s sample showed) — no `AudioManager`/`UIManager` exist yet, contrary to what that sample implied.
- **State Machine**: Properly integrated as a service; supports async transitions. **Caveat found this session**: only one transition (`None → Playing`) is ever actually called anywhere in the codebase. `MainMenu`/`GameOver`/`Paused`/`Loading` are real, correctly-shaped `IGameState` implementations that are never instantiated. See `STATES.md`.
- **Event Bus**: Correctly uses generic `GameEvent` to avoid static scatter. 21 events cataloged this session (up from an unverified prior state) — 4 of them (`EntityDamagedEvent`, `EntityHealedEvent`, `EntityStateChangedEvent`, `EntityLevelUpEvent`) have publishers but no subscribers yet, and 1 (`GameLoadRequestedEvent`) has neither.
- **Risk**: `GameBootstrapper` is currently a "God Object" for registration. May need sub-bootstrappers later. (Unchanged from prior finding — still true at 8 services.)

### Task 1.2: Domain & Gameplay Mapping
- **Pillar 1: Entity & Attribute System**: Uses SO Blueprints + Pure C# Instances, confirmed. `EntityRegistry.asset` now has at least one populated entry (`DebugActorData.asset`) — this pillar has moved past pure skeleton.
- **Pillar 2: Grid & Room System**: Corrected this session — this is **not** "primitive tiles." Rooms/corridors are voxelized into `RoomGridData` and meshed via `GreedyMeshingService` (Burst-compiled greedy meshing) into one combined mesh per room/corridor. `RoomPrimitiveTile.cs` still exists in the project but is dead code — no references anywhere.
- **Pillar 3: Procedural Generation**: Includes Room Graph Generation and Greedy Meshing. Confirmed accurate.
- **Pillar 4: Player Interaction**: Input → Interactor → Grid Service loop. Confirmed accurate: `InputManager` → `PlayerGridInteractor` → `GridBlockClickedEvent` → `BlockPlacementService`.
- **Redundancy Flag — RESOLVED**: The prior flag ("Potential overlap between `BlockPlacementService` and `GridInteractionService`") was marked `// TODO: THESE DO NOT OVERLAP AND RATHER WORK TOGETHER` by a previous session. This session's code audit confirms that annotation: `PlayerGridInteractor` publishes `GridBlockClickedEvent`, which only `BlockPlacementService` consumes. `GridInteractionService` was registered in `GameBootstrapper` but no `Publish`/`AddListener` call site involving it turned up in this session's search — its role wasn't fully traced. Recommend a quick manual check of what `GridInteractionService` actually does if its purpose isn't obvious from a read-through, but there's no evidence of overlapping responsibility with `BlockPlacementService`.

---

## Known Issues / Notes

This session (2026-08-21) replaced the empty "no issues identified" note below with real findings from a full documentation-vs-code audit:

- **`STATES.md` was broken.** The previous version listed `GameplayState` as a real class — it's actually just example code inside `IGameState.cs`'s XML doc comment. All 5 real state classes (`Playing`, `MainMenu`, `GameOver`, `Paused`, `Loading`) were missing. Rewritten this session.
- **`EVENTS.md` had multiple wrong publisher/subscriber attributions** and was missing 4 real events entirely (`PlayerJumpInputEvent`, `PlayerInteractPrimaryInputEvent`, `PlayerInteractSecondaryInputEvent`, `PlayerSpawnRequestedEvent`). Full list of corrections is in `EVENTS.md`'s per-event notes. Rewritten this session.
- **`LEVEL_GENERATION.md` Section 4 described a renderer that no longer exists** (pooled `RoomPrimitiveTile` prefabs). The actual renderer voxelizes into `RoomGridData` and uses greedy meshing. Sections 1–3 were verified accurate and left untouched. Rewritten this session.
- **`Log.md` was stale relative to a large chunk of new work** — the entire `Domain/Grid`, `Domain/Interaction`, `Domain/Persistence`, and `Gameplay/Entities` layers were added after its last update and weren't reflected. Refreshed this session.
- **Namespace/folder inconsistencies** (documented, not necessarily wrong, just worth knowing): the state machine's physical folder is `Domain/StateMachine/`, but its namespace is `Domain.Entities.StateMachine` — except `IGameState.cs`, which is in namespace `Core`.
- **`ExitTrigger` placement is unverified.** The component exists and is correctly wired to publish `LevelCompletedEvent`, but no code path was found anywhere that instantiates or attaches it during level generation. If the core loop works in practice, it's likely being placed manually somewhere not covered by this audit (e.g. directly in a prefab or scene) — worth a manual confirmation.
- **`reusable_code_combinations.md` is out of scope for a "match the current code" audit** — it catalogs class names from other prototypes (`PlayerController`, `SkillsCollection`, `ObjectPoolManager`, etc.) for salvage purposes, none of which exist in this project. Left untouched apart from a clarifying header, since rewriting it to match this codebase would defeat its actual purpose.
- **`ARCHITECTURE.md` is a cross-project template** (v4.0, explicitly attributes patterns to `snake-prototype`, `Prototype_RGP`, `3D_Learning_Prototype`, `DatingSimEngine`/`VisualNovelEngine`, `Maze`). Only the concrete, project-specific sections (Folder Structure, and the Service Locator / Async State Machine / Bootstrapper code samples) were corrected against real code this session. The philosophy/phases/articles/anti-patterns content was left as-is, since it's intentionally general-purpose rather than a claim about this project's current state.