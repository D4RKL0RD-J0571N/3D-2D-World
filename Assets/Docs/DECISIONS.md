# Architecture Decision Records (ADR)

This document catalogs non-obvious architectural choices made during the development of the **3D 2D World** project. Each entry captures the context, the chosen solution, and the alternatives considered.

---

## [2025-08-19] — Generic Typed Event Bus

**Decision**: Use a centralized, generic `GameEventManager<T>` instead of per-event static Action fields (e.g., `public static event Action<X> OnX`).

**Context**: As the project grows, scatter-shot static events across dozens of classes make it impossible to audit what events exist, who is listening to them, and where they are being leaked.

**Alternatives considered**:
- Per-event static events (Simpler per-site, but unscalable and prone to memory leaks).
- Unity's built-in `UnityEvent` (Inspector-friendly, but high overhead and difficult to manage for complex logic).

**Why this**: 
- Single source of truth for all event contracts.
- Centralized `Clear()` method for scene transitions to prevent memory leaks.
- Type-safety provided by generics.
- Simplifies AI-assisted development by providing a single place to look for event definitions.

---

## [2025-08-19] — Async State Machine

**Decision**: Implement the `GameStateMachine` using an `Async` pattern where each state is an `IGameState` module with `OnEnterAsync` and `OnExitAsync` methods.

**Context**: Complex gameplay transitions (e.g., loading a level, spawning a large dungeon, or initializing a UI) are often asynchronous and can lead to "race conditions" or "frame-hiccups" if handled in a standard synchronous switch statement.

**Alternatives considered**:
- Standard Switch-based FSM (Easier for simple logic, but handles async transitions poorly).
- Hierarchical State Machines (Good for nested logic, but adds significant complexity).

**Why this**:
- Ensures clean, sequential transitions (Exit current $\rightarrow$ Switch $\rightarrow$ Enter next).
- Naturally supports `await` calls for level loading and procedural generation.
- Decouples state logic into independent, testable C# classes.

---

## [2025-08-19] — Data / Instance Separation

**Decision**: Enforce a strict separation between ScriptableObjects (Data/Blueprints) and pure C# classes (Entities/Instances).

**Context**: Mixing runtime mutable state (e.g., current HP, position) into ScriptableObjects leads to "bleeding" state between play sessions and makes it impossible to run parallel simulations or easy unit tests.

**Alternatives considered**:
- Storing everything in ScriptableObjects (Easy for small projects, but creates persistent state bugs).
- Pure JSON/Serialized files (Safe, but harder to manage in the Unity Editor).

**Why this**:
- ScriptableObjects serve as "Read-Only" configurations.
- Pure C# instances (e.g., `EntityInstance`) hold the "Live" state.
- Makes the system highly predictable and easier to serialize/save.

---

## [2025-08-19] — Service Locator

**Decision**: Use a `ServiceLocator` for shared runtime services (Audio, Input, Save, UI, StateMachine).

**Context**: Deep singleton chains (`Manager.Instance.SubSystem.Thing`) create hidden, hard-to-track dependencies and make unit testing nearly impossible.

**Alternatives considered**:
- Dependency Injection (DI) Framework (High overhead for indie/small teams).
- Singletons (Easy to start, but leads to "God Object" patterns).

**Why this**:
- Explicitly declares shared dependencies at the Bootstrapper phase.
- Avoids the "spaghetti" of singletons while remaining simpler than a full DI framework.
- Allows for easy mocking or swapping of services during testing.