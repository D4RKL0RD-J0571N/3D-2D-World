# STATES

> Reflects the actual state machine implementation as of 2026-08-21. Re-audit `Assets/Scripts/Domain/StateMachine/` against this table whenever states are added, removed, or newly wired to a transition. Precision corrections dated 2026-10-15: see Notes §2 below.

## State Table

| Name | Description | Entry Conditions | Exit Conditions | Triggering Events | Source File |
|---|---|---|---|---|---|
| `Playing` | Active gameplay state. While active, subscribes to `LevelCompletedEvent` and republishes `LevelGenerationRequestedEvent` when it fires — this single state drives the entire infinite dungeon-crawler loop. | Constructed and passed to `GameStateMachine.TransitionToAsync()` exactly once, in `GameBootstrapper.InitializeServices()`. This is the **only** `TransitionToAsync` call site anywhere in the codebase — the machine boots directly into `Playing`. | Never exited in current code — no other state is ever transitioned into, so `OnExitAsync` is never called in practice. | Entry: none (direct bootstrap call, not event-driven). While active, consumes `LevelCompletedEvent` and in response publishes `LevelGenerationRequestedEvent` (`Seed = -1`). | `Assets/Scripts/Domain/StateMachine/GameStates.cs` |
| `MainMenu` | Defined shell state. `OnEnterAsync`/`OnExitAsync` return `Task.CompletedTask`; `OnTick` is empty. | None — nothing in the codebase calls `TransitionToAsync(new MainMenu())`. | N/A — never entered. | None wired. | `Assets/Scripts/Domain/StateMachine/GameStates.cs` |
| `GameOver` | Defined shell state, same no-op shape as `MainMenu`. | None — never transitioned into. | N/A — never entered. | None wired. | `Assets/Scripts/Domain/StateMachine/GameStates.cs` |
| `Paused` | Defined shell state, same no-op shape as `MainMenu`. Its `GameStateType` is read by `InputManager` for input-gating comparisons (`CurrentStateType == GameStateType.Paused`), but the state itself is never entered. | None — never transitioned into. | N/A — never entered. | None wired. | `Assets/Scripts/Domain/StateMachine/GameStates.cs` |
| `Loading` | Defined shell state, same no-op shape as `MainMenu`. | None — never transitioned into. | N/A — never entered. | None wired. | `Assets/Scripts/Domain/StateMachine/GameStates.cs` |

**Supporting types:**
- `GameStateType` enum — `None, MainMenu, Playing, Paused, GameOver, Loading` — `Assets/Scripts/Domain/StateMachine/GameStateType.cs`
- `IGameState` interface — `StateType`, `OnEnterAsync()`, `OnExitAsync()`, `OnTick(float)` — `Assets/Scripts/Domain/StateMachine/IGameState.cs`
- `GameStateMachine` — owns `CurrentState` / `CurrentStateType`, exposes `TransitionToAsync`, registered as an `IGameService` and ticked from `GameBootstrapper.Update()` — `Assets/Scripts/Domain/StateMachine/GameStateMachine.cs`

## State Diagram

````mermaid
stateDiagram-v2
    direction LR
    [*] --> Playing : GameBootstrapper.InitializeServices()
    Playing --> Playing : LevelCompletedEvent\n(republishes LevelGenerationRequestedEvent)

    state "MainMenu (unwired)" as MainMenu
    state "GameOver (unwired)" as GameOver
    state "Paused (unwired)" as Paused
    state "Loading (unwired)" as Loading
````

> `MainMenu`, `GameOver`, `Paused`, and `Loading` are drawn with no edges intentionally. Nothing in the current codebase transitions into or out of them — they exist as `IGameState` shells, not as reachable states. Adding a real transition (e.g. a pause input triggering `Playing → Paused`) should update this diagram in the same change.

## Notes

- **Namespace/folder mismatch**: the C# namespace for these files is `Domain.Entities.StateMachine`, but the physical folder is `Assets/Scripts/Domain/StateMachine/` — not `Domain/Entities/StateMachine/`. Worth fixing one way or the other; documenting it here so it isn't mistaken for a typo in this file.
- The previous version of this file was stale in a specific way worth knowing about: it listed `GameplayState` as a state class. That name only exists inside an `<example>` XML-doc code sample in `IGameState.cs` — it is not a real class. The five real state classes (`Playing`, `MainMenu`, `GameOver`, `Paused`, `Loading`) were missing entirely.
- If/when `MainMenu → Loading → Playing` and pause/game-over flows get wired up, this is the file to update — both the table and the diagram.
- **2026-10-15 precision note (Parameters replacement)**: `Playing.OnLevelCompleted` publishes `LevelGenerationRequestedEvent` with `Parameters = new LevelGenerationParameters { Seed = -1 }` — a brand-new object, not a mutation of any existing parameters. This means the entire `LevelGenerationParameters` is replaced on each loop iteration, so only `Seed = -1` carries over and all other parameters revert to their defaults (`MainPathRooms = 6`, `GridHalfExtents = (6, 6)`, etc.). If a future change customizes parameters mid-run (e.g. increasing difficulty), the `OnLevelCompleted` handler must be updated to preserve those before republishing.
- **2026-10-15 precision note (ITickable wiring gap)**: `AreaSchedulerService` implements `ITickable` but its `Tick` method is a no-op (only a placeholder comment), and — despite the `ITickable` interface documenting that tickable services are "driven by `GameBootstrapper.Update()`" — `GameBootstrapper.Update()` only ticks the `GameStateMachine`, not registered services. `GameStateMachine.Tick` in turn only forwards to the active state's `OnTick`. So no `ITickable` service's `Tick` is ever actually invoked in the current codebase. The `AreaSchedulerService` instead operates entirely through event-driven handlers (`OnLevelGenerated`, `OnPlayerRoomEntered`).