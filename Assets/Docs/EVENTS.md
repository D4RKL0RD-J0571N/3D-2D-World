# Game Event Catalog

This document is the definitive catalog of all `GameEvent` subclasses currently implemented in the **3D 2D World** project. It is meant to be updated whenever a new event is added to the `GameEventManager` bus.

> Each row below was verified against actual `Publish<T>` / `AddListener<T>` call sites in the codebase. **Updated 2026-10-15** — a second audit pass (post the 2026-08-21 refresh) surfaced five events that were added to code but never entered this catalog: `PlayerHotbarSlotSelectedEvent`, `HotbarStateChangedEvent`, `RoomEnteredEvent`, `RoomExitedEvent`, and `AreaTierChangedEvent`. This raised the known event count from 21 to 26. Several pre-existing rows also gained new consumers (notably `LevelGenerationCompletedEvent`) and are corrected inline below.

## Summary Table

| Event Name | Namespace | Publisher(s) | Subscriber(s) | Description |
|---|---|---|---|---|
| `LevelGenerationRequestedEvent` | `Gameplay.World` | `LevelLayoutRenderer.Start()`, `Playing` state (`OnLevelCompleted`) | `LevelGenerationService` | Requests a new dungeon layout based on parameters. |
| `LevelGenerationCompletedEvent` | `Gameplay.World` | `LevelGenerationService` | `LevelLayoutRenderer`, `AreaSchedulerService`, `HotbarService` | Fired when a layout is successfully generated. |
| `LevelGenerationFailedEvent` | `Gameplay.World` | `LevelGenerationService` | `LevelLayoutRenderer` (logs an error) | Fired when layout generation fails. |
| `LevelCompletedEvent` | `Gameplay.World` | `ExitTrigger` | `Playing` state | Fired when the player reaches the exit. |
| `PlayerSpawnRequestedEvent` | `Gameplay.World` | `LevelLayoutRenderer` (`BuildLayout`, `OnGameLoaded`) | `PlayerSpawnService` | Requests the player be spawned/repositioned at a world position. |
| `PlayerMoveInputEvent` | `Gameplay.Player` | `InputManager` | `Player_Movement` | Passes normalized move input. Payload field is `Move`, not `Direction`. |
| `PlayerLookInputEvent` | `Gameplay.Player` | `InputManager` | `Player_Look` | Passes mouse-delta input. Payload field is `LookDelta`, not `Delta`. |
| `PlayerRunInputEvent` | `Gameplay.Player` | `InputManager` | `Player_Movement` | Sprint held/released. Payload field is `Held`, not `IsRunning`. |
| `PlayerCrouchInputEvent` | `Gameplay.Player` | `InputManager` | `Player_Movement` | Crouch held/released. Payload field is `Held`, not `IsCrouching`. |
| `PlayerJumpInputEvent` | `Gameplay.Player` | `InputManager` | `Player_Movement` | Jump pressed. No payload. |
| `PlayerInteractPrimaryInputEvent` | `Gameplay.Player` | `InputManager` | `PlayerGridInteractor` | Primary interact pressed. No payload. |
| `PlayerInteractSecondaryInputEvent` | `Gameplay.Player` | `InputManager` | `PlayerGridInteractor` | Secondary interact pressed. No payload. |
| `PlayerHotbarSlotSelectedEvent` | `Domain.Grid` (new) | `InputManager` (`OnHotbar`) | `HotbarService`, `BlockPlacementService` | Numeric slot (0–8) selected; selects the block at that hotbar index. |
| `GridBlockClickedEvent` | `Domain.Interaction` | `PlayerGridInteractor` | `BlockPlacementService` | Fired when a grid block is clicked. Previously mis-attributed: publisher was listed as `BlockPlacementService` itself — it's actually the consumer, not the publisher. |
| `EntityDamagedEvent` | `Domain.Entities` | `AttributeComponent` | *(none currently)* | Fired when an actor takes damage. Previously attributed to a `Combat/DamageSources` class that doesn't exist in the codebase. No listener is currently registered anywhere. |
| `EntityHealedEvent` | `Domain.Entities` | `AttributeComponent` | *(none currently)* | Fired when an actor is healed. Same correction as above; no listener currently registered. |
| `EntityStateChangedEvent` | `Domain.Entities` | `StateComponent` | *(none currently)* | Fired when an actor's `ActorState` changes. Previously mis-attributed to `GameStateMachine` — that's a different "state" concept entirely (game flow vs. per-actor status). No listener currently registered. |
| `EntityLevelUpEvent` | `Domain.Entities` | `StatsComponent` | *(none currently)* | Fired when an actor levels up. No listener currently registered. |
| `GameSaveRequestedEvent` | `Domain.Persistence` | `InputManager` (`F5`) | `LevelLayoutRenderer` | Requests a save. Previously attributed to `UI`/`GameStateMachine` as publisher and `FileSaveService` as subscriber — neither is accurate. `LevelLayoutRenderer` handles it and calls `FileSaveService.WriteData()` directly (a normal method call, not an event). |
| `GameLoadRequestedEvent` | `Domain.Persistence` | *(none — dead)* | *(none — dead)* | Defined but never published or subscribed anywhere. The real load path is `LevelGenerationService` checking `FileSaveService.HasSaveFile()` directly whenever `LevelGenerationRequestedEvent` fires. |
| `GameDataLoadedEvent` | `Domain.Persistence` | `LevelGenerationService` | `LevelLayoutRenderer` | Fired when a save file was found and loaded during level-generation. Previously attributed to `FileSaveService` as publisher — `FileSaveService` never publishes events; it's a plain read/write service. |
| `EntityStateLoadedEvent` | `Domain.Persistence` | `LevelLayoutRenderer` (`OnGameLoaded`) | *(none currently)* | Fired after a save is loaded, carrying restored player state. Previously attributed to `FileSaveService` as publisher and `EntityRegistry` as subscriber — neither matches; no listener is currently registered anywhere. |
| `HotbarStateChangedEvent` | `Systems.Interaction` (new) | `HotbarService` | `HotbarHUD` | Broadcast whenever the selected slot or block palette changes, so the UI can refresh. |
| `RoomEnteredEvent` | `Domain.Entities` (new) | `RoomTrackerTrigger` (per-room trigger) | `AreaSchedulerService` | Published when the player walks into a room's tracker volume; drives area-simulation tiering. |
| `RoomExitedEvent` | `Domain.Entities` (new) | *(none — dead)* | *(none — dead)* | Defined but never published or subscribed anywhere. Scaffolded alongside `RoomEnteredEvent`; the tracker only fires enter, not exit. |
| `AreaTierChangedEvent` | `Domain.Entities` (new) | `AreaSchedulerService` (`UpdateRoomTiers`) | *(none currently)* | Published when a room's simulation tier transitions between Active/Dormant/Purged. `AreaSchedulerService` is currently the only consumer-aware domain type; no other system subscribes to act on the tier change yet. |

---

## Detailed Event Documentation

### World & Level Events (`Gameplay.World`)

#### `LevelGenerationRequestedEvent`

- **Payload:** `LevelGenerationParameters Parameters`
- **Published by:** `LevelLayoutRenderer.Start()` (when `_generateOnStart` is true) and the `Playing` state's `OnLevelCompleted` handler (regeneration, `Seed = -1`).
- **Consumed by:** `LevelGenerationService.OnLevelGenerationRequested`
- **Usage:** Triggers either a fresh procedural layout, or a save-file load if one exists (see `GameDataLoadedEvent`).

#### `LevelGenerationCompletedEvent`

- **Payload:** `LevelLayout Layout`
- **Published by:** `LevelGenerationService` when generation succeeds (and re-emitted with the same payload by the save-load path in `OnLevelGenerationRequested`).
- **Consumed by:** `LevelLayoutRenderer.OnLevelGenerationCompleted` (builds voxels + meshes + spawns room tracker), `AreaSchedulerService.OnLevelGenerated` (re-initializes room tiers), `HotbarService.OnLevelGenerated` (scaffolds registry rebind on fresh layout — body currently a no-op stub).
- **Usage:** The single signal that a layout is ready to render and that simulation scheduling should reset.

#### `LevelGenerationFailedEvent`

- **Payload:** `string Reason`
- **Published by:** `LevelGenerationService` if the generator produces zero rooms.
- **Consumed by:** `LevelLayoutRenderer.OnLevelGenerationFailed` (logs an error only — no fallback behavior implemented).

#### `LevelCompletedEvent`

- **Payload:** none
- **Published by:** `ExitTrigger.OnTriggerEnter` (tag check for `"Player"`; disables itself after firing once).
- **Consumed by:** `Playing.OnLevelCompleted`, which republishes `LevelGenerationRequestedEvent`.
- **Usage:** Drives the infinite crawler loop.
- **Verified 2026-10-15:** No code path in `LevelLayoutRenderer` instantiates or attaches `ExitTrigger` during generation. The component is correct, but it must be placed manually in-editor for the loop to fire.

#### `PlayerSpawnRequestedEvent`

- **Payload:** `Vector3 SpawnPosition`
- **Published by:** `LevelLayoutRenderer.BuildLayout` (after a fresh generation, positioned at the start room, raycast down via `PhysicsUtils.FindGroundPosition`) and `LevelLayoutRenderer.OnGameLoaded` (after a save load, positioned at the saved player position or the start room as fallback).
- **Consumed by:** `PlayerSpawnService.OnPlayerSpawnRequested`.

---

### Player Input Events (`Gameplay.Player`)

All seven of these are published from `InputManager`, one per bound input action. A sixth action, `PlayerHotbarSlotSelectedEvent`, is published via the hotbar action (see the Grid section — it lives in `Domain.Grid` because it carries no player-specific semantics and is consumed by services, not by `Player_*` components).

#### `PlayerMoveInputEvent`

- **Payload:** `Vector2 Move`
- **Consumed by:** `Player_Movement.OnMoveInput`

#### `PlayerLookInputEvent`

- **Payload:** `Vector2 LookDelta`
- **Consumed by:** `Player_Look.OnLookInput`

#### `PlayerRunInputEvent`

- **Payload:** `bool Held`
- **Consumed by:** `Player_Movement.OnRunInput`

#### `PlayerCrouchInputEvent`

- **Payload:** `bool Held`
- **Consumed by:** `Player_Movement.OnCrouchInput`

#### `PlayerJumpInputEvent`

- **Payload:** none
- **Consumed by:** `Player_Movement.OnJumpInput`

#### `PlayerInteractPrimaryInputEvent`

- **Payload:** none
- **Consumed by:** `PlayerGridInteractor.OnPrimaryInteract`

#### `PlayerInteractSecondaryInputEvent`

- **Payload:** none
- **Consumed by:** `PlayerGridInteractor.OnSecondaryInteract`

> **Input gating (verified 2026-10-15):** `InputManager` now defines `_pauseAction`, `_confirmAction`, `_cancelAction`, and `_saveAction` (F5). `_moveAction`, `_lookAction`, `_interactPrimary/Secondary`, and `_hotbarAction` are gated behind `CanReadGameplayInput()`, which returns true only when `GameStateMachine.CurrentStateType == GameStateType.Playing`. The pause handler body is still a no-op — no `Playing → Paused` transition is wired despite the gating existing.

---

### Hotbar & Interaction Events

#### `PlayerHotbarSlotSelectedEvent`

- **Payload:** `int SlotIndex` (0–8)
- **Published by:** `InputManager.OnHotbar` (parses the pressed number key; keyboard 1–9 map to slots 0–8; gamepad returns −1 and is ignored).
- **Consumed by:** `HotbarService.OnSlotSelected` (sets the active slot + notifies the UI), `BlockPlacementService.OnHotbarSlotSelected` (updates `SelectedBlock` so the next right-click places the picked block).
- **Usage:** Decouples the 9-key hotbar binding from the block-placement logic; both the hotbar service and the placement tool react to the same event.

#### `GridBlockClickedEvent`

- **Payload:** `GridHitInfo HitInfo`, `int MouseButton` (0 = left, 1 = right)
- **Published by:** `PlayerGridInteractor` (in response to a primary/secondary interact input, after a raycast through `InteractionPhysicsUtils.GetCenterRay`).
- **Consumed by:** `BlockPlacementService.OnBlockClicked`
- **Usage:** The bridge between raw interact input and world/grid modification.

#### `HotbarStateChangedEvent`

- **Payload:** `int SelectedSlotIndex`, `BlockDefinition SelectedBlock`, `BlockDefinition[] Slots` (cloned copy of the full 9-slot array)
- **Published by:** `HotbarService.NotifyStateChanged()` (on slot select, cycling, and registry population).
- **Consumed by:** `HotbarHUD.OnHotbarStateChanged`
- **Usage:** Lets the UI react to hotbar changes without a direct service reference; the `Slots` clone lets the HUD render all icons without querying the service.

---

### Entity Events (`Domain.Entities`)

These four are published by real components but currently have **no listeners anywhere in the codebase** — they're wired for publishing but nothing downstream (UI, audio, etc.) consumes them yet. (`EntityDamagedEvent`/`EntityHealedEvent` originate in `AttributeComponent`; `EntityStateChangedEvent` in `StateComponent`; `EntityLevelUpEvent` in `StatsComponent`.)

#### `EntityDamagedEvent`

- **Payload:** `BaseActor Target`, `int Amount`
- **Published by:** `AttributeComponent.ApplyDamage` (after subtracting from the `Health` attribute; fires `EntityLevelUpEvent`-style but here it's damage).

#### `EntityHealedEvent`

- **Payload:** `BaseActor Target`, `int Amount`
- **Published by:** `AttributeComponent.ApplyHealing`.

#### `EntityStateChangedEvent`

- **Payload:** `BaseActor Target`, `ActorState NewStates`
- **Published by:** `StateComponent` (three call sites: `SetState`, `RemoveState`, `ClearStates` — all three publish when the flags actually change).

#### `EntityLevelUpEvent`

- **Payload:** `BaseActor Target`, `int NewLevel`
- **Published by:** `StatsComponent.LevelUp`.

---

### Area & Lifecycle Events (`Domain.Entities`)

> **`Corrected 2026-10-15`.** This group did not exist in the prior catalog. They back the `AreaSchedulerService` + per-room `RoomTrackerTrigger` system added after the last audit.

#### `RoomEnteredEvent`

- **Payload:** `string RoomId`, `UnityEngine.Vector2Int RoomGridPosition`
- **Published by:** `RoomTrackerTrigger.OnTriggerEnter(Collider)` (tag-checked against `"Player"`). One trigger per room, spawned during `LevelLayoutRenderer.BuildRoomGrid`.
- **Consumed by:** `AreaSchedulerService.OnPlayerRoomEntered` (recomputes Active/Dormant/Purged tiers for every room via Manhattan distance).

#### `RoomExitedEvent`

- **Payload:** `string RoomId`
- **Published by:** nobody
- **Consumed by:** nobody
- **Status:** Dead/scaffolded. `RoomTrackerTrigger` only publishes enter, never exit; nothing ever transitions to a Paused/`GameOver` flow that would need exit events either.

#### `AreaTierChangedEvent`

- **Payload:** `string RoomId`, `AreaSimulationTier PreviousTier`, `AreaSimulationTier NewTier`
- **Published by:** `AreaSchedulerService.UpdateRoomTiers` (once per room whose tier changes on a player-room entry).
- **Consumed by:** nobody currently. `AreaSimulationTier` is { Active, Dormant, Purged } and exists so future entity/AI/particle systems can gate their work on the player's proximity.

---

### Persistence Events (`Domain.Persistence`)

#### `GameSaveRequestedEvent`

- **Payload:** none
- **Published by:** `InputManager` (bound input action, `F5`/`_saveAction`).
- **Consumed by:** `LevelLayoutRenderer.OnSaveRequested`, which assembles `LevelSaveData` and calls `FileSaveService.WriteData()` directly.

#### `GameLoadRequestedEvent`

- **Payload:** none
- **Published by:** nobody
- **Consumed by:** nobody
- **Status:** Dead code. Loading is not event-driven — `LevelGenerationService` calls `FileSaveService.HasSaveFile()` / `ReadData()` directly whenever `LevelGenerationRequestedEvent` fires. This event can either be wired up to a real "load game" UI action, or removed.

#### `GameDataLoadedEvent`

- **Payload:** `LevelSaveData SaveData`
- **Published by:** `LevelGenerationService.OnLevelGenerationRequested`, when a save file exists and is successfully read.
- **Consumed by:** `LevelLayoutRenderer.OnGameLoaded`, which rebuilds the saved grids and publishes `PlayerSpawnRequestedEvent` + `EntityStateLoadedEvent`.

#### `EntityStateLoadedEvent`

- **Payload:** `EntitySaveData State`
- **Published by:** `LevelLayoutRenderer.OnGameLoaded`
- **Consumed by:** nobody currently. Scaffolded for a future system (e.g. a player-state restorer) to pick up saved HP/level/experience.

---

## Adding New Events

Adding a new event is simple. You do not need to register it in a central manager — the generic type signature automatically handles registration when you publish or subscribe.

### 1. Define the Event

Create a new class inheriting from `GameEvent` (the base class is `abstract`) in the appropriate namespace file (e.g., `PlayerInputEvents.cs` or a new `CombatEvents.cs`). Existing files are inconsistent between `sealed` and unsealed — `PlayerInputEvents.cs` and `LevelGenerationEvents.cs` use `sealed`, `EntityEvents.cs` and `PersistenceEvents.cs` don't. Prefer `sealed` for new events unless you specifically need to derive further. Always use the `Event` suffix.

```csharp
namespace Gameplay.Combat
{
    using Core;

    public sealed class EntityDamagedEvent : GameEvent
    {
        public string TargetId;
        public int DamageAmount;
    }
}
```

### 2. Subscribe and Unsubscribe

**CRITICAL:** Always remove your listeners in `OnDisable`/`OnDestroy` (MonoBehaviours) or `OnExitAsync` (states) to prevent leaks.

```csharp
private void OnEnable()
{
    GameEventManager.AddListener<EntityDamagedEvent>(OnEntityDamaged);
}

private void OnDisable()
{
    GameEventManager.RemoveListener<EntityDamagedEvent>(OnEntityDamaged);
}

private void OnEntityDamaged(EntityDamagedEvent e)
{
    Debug.Log($"{e.TargetId} took {e.DamageAmount} damage!");
}
```

### 3. Publish

```csharp
GameEventManager.Publish(new EntityDamagedEvent
{
    TargetId = "Enemy_Goblin_01",
    DamageAmount = 25
});
```

### 4. Document

Add it to the table at the top of this document — and verify the publisher/subscriber against actual call sites, not intent, before writing it down.
