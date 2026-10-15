# Unity Game Template — Architecture Blueprint

**Version**: 4.0
**Philosophy**: Design-Guided Development (DGD)
**Audience**: Solo Developer, Indie, AI-Assisted Workflows

---

## Core Philosophy

> "Design discovers the game. Architecture supports the design.
> Code expresses behavior. Documentation clarifies intent."

This template exists for indie realities:

- rapid iteration
- evolving mechanics
- changing direction
- small teams
- AI-assisted development

The architecture's job is to:

- maintain clarity as complexity grows
- reduce chaos without adding ceremony
- accelerate iteration, not protect it from happening
- help both humans and AI understand the project at any point

This is **Design-Guided Development (DGD)**:

- behavior is explored before implementation
- logic is reasoned before coding
- gameplay discovery is allowed to shape architecture
- documentation clarifies intent, not creates bureaucracy

**The final principle**: Architecture should help you finish the game — not become the game itself.

---

## Development Phases

Not every feature requires the same process. The phase determines which rules apply.

---

### Phase 1 — Exploration

**Goal**: Discover if the mechanic is fun and understand its behavior.

Everything is allowed here:

- direct references
- temporary systems
- tightly coupled code
- quick experiments
- disposable prototypes
- ugly code that works

**Preferred tools for thinking before coding**:

- state diagrams
- event flow sketches
- UML (sequence, class, activity)
- flowcharts
- pseudocode
- gameplay loop diagrams
- markdown behavior notes
- AI discussions and iteration

Questions to explore:

- What should happen?
- Why does this mechanic exist?
- How does the player interact with it?
- What other systems react to it?
- What feels wrong?

The goal is **behavioral clarity**, not architectural perfection.

**Phase transition trigger**: Move to Phase 2 when you touch the same code three times, when another system starts depending on it, or when a bug takes more than 30 minutes to find.

---

### Phase 2 — Stabilization

**Goal**: Turn proven mechanics into maintainable systems.

Once a mechanic proves its value:

- modularize it
- document it
- decouple it where it matters
- register it in the ServiceLocator if it is shared
- add it to EVENTS.md if it publishes events
- write a DECISIONS.md entry if the architecture choice is non-obvious

Architecture follows **validated gameplay**, not hypothetical gameplay.

**Phase transition trigger**: Move to Phase 3 when a system is stable, widely depended on, or risky enough that a silent failure would be expensive.

---

### Phase 3 — Production Hardening

**Goal**: Prepare long-term systems for stability.

Now stronger practices matter:

- automated tests for risky systems
- event contracts documented
- save compatibility guaranteed
- data validation
- performance profiling
- AI governance context updated

Only stable, proven systems reach this phase.

---

## Constitutional Articles

These are guiding principles. They have weight. They are not rigid dogma.

The one meta-rule that governs all others: **if following a rule consistently slows iteration, adds friction without value, or discourages experimentation, reconsider the rule.** Process serves the game.

---

### Article I — Intent Before Implementation

Every meaningful system should have a clearly understood purpose before large-scale implementation begins.

That understanding may live as:

- markdown notes
- state diagrams
- event flow sketches
- pseudocode
- gameplay loop descriptions
- UML diagrams

The goal is **behavioral clarity**. Not paperwork. Not a formal spec pipeline.

---

### Article II — Prototype First, Extract Later

Prototype systems may begin tightly coupled. Reusable architecture should emerge from proven needs, not assumptions.

When a system is:

- reused in two or more places
- stable enough to depend on
- hard to understand due to coupling
- widely interconnected

refactor it into a cleaner modular design.

Avoid **premature architecture**. Do not design for imaginary future requirements.

---

### Article III — Risk-Based Testing

Testing effort should match failure cost.

**High priority** — write tests before or immediately after implementation:

- save/load systems
- inventory integrity
- procedural generation
- economy and stat calculations
- utility libraries reused across systems
- serialization
- AI decision systems

**Lower priority** — playtesting and manual verification are sufficient:

- experimental gameplay feel
- UI experimentation
- early prototype mechanics

**Regression rule**: if fixing a bug in a system takes more than 30 minutes, write a regression test before moving on. The test documents what broke and prevents repeat failures.

Automated testing is a **tool**, not a religion.

---

### Article IV — Practical Domain Separation

Pure C# systems are encouraged where they provide real value.

Good candidates for pure domain logic:

- stats and attribute calculations
- dialogue logic
- economy systems
- procedural generation algorithms
- utility libraries
- simulation systems

Engine-bound gameplay does not need forced purity. Unity types may be used directly when abstraction provides no meaningful benefit. Unnecessary wrapper layers are prohibited by Article VII.

---

### Article V — Explicit Dependency Access

Hidden dependencies create chaos. Be explicit about how systems find each other.

**Decision rule**:

- **Direct reference**: use when the dependency is internal to a single feature or tightly coupled by nature.
- **ServiceLocator**: use when a system is consumed by two or more other systems, or when it is a shared runtime service (audio, save, UI, state machine, input).
- **Serialized reference**: use for Unity components that are wired in the Inspector and do not need runtime resolution.

Avoid:

- `FindObjectOfType`
- deep singleton chains (`Manager.Instance.SubSystem.Thing`)
- hidden scene load-order dependencies

Architecture should reduce confusion, not increase indirection.

---

### Article VI — Event-Driven Communication

Use events when systems benefit from decoupling.

Excellent use cases:

- UI reacting to game state changes
- combat notifications
- quest and achievement triggers
- inventory changes
- AI reactions to world events
- simulation system updates
- gameplay triggers crossing system boundaries

Not every interaction needs an event. Use **direct calls** when simpler and clearer. Use **events** when relationships become interconnected or when adding a direct call would create an unwanted dependency.

---

### Article VII — Complexity Must Justify Itself

Start simple. Only introduce abstractions, interfaces, generic systems, or architectural layers when a real, existing requirement justifies them.

Prohibited without justification:

- speculative scalability
- future-proofing for imaginary problems
- abstraction without two concrete use cases already existing
- wrapper layers that add indirection without adding value

Ask before adding complexity: _does this solve a real problem I have right now?_

---

### Article VIII — Use the Engine

Unity provides physics, transforms, ScriptableObjects, animation, coroutines, serialization, and scene management.

Use built-in systems directly unless abstraction creates meaningful, demonstrated value.

Do not wrap engine features that do not need wrapping.

---

### Article IX — Architecture Must Accelerate Iteration

Architecture exists to help development move faster and safer.

If a rule consistently slows experimentation, increases friction, creates ceremony, or discourages iteration, the rule should be reconsidered. This article grants explicit permission to override any other article with documented justification.

---

## Folder Structure

> **Corrected 2026-08-21.** The block below previously showed the template's target/aspirational structure (`Infrastructure/`, `Utilities/`, `Tests/`, `Domain/Attributes/`, `Domain/Events/`, `Docs/Features/`, and an `Audio`/`UI`/`Save` split under `Systems/`). None of those exist in this project yet. What follows is the **actual current structure**, verified against the filesystem, with a note underneath about which template folders are still just conventions waiting to be used.

> **Corrected 2026-08-21, 2026-10-15.** The block below previously showed the template's target/aspirational structure (`Infrastructure/`, `Utilities/`, `Tests/`, `Domain/Attributes/`, `Domain/Events/`, `Docs/Features/`, and an `Audio`/`UI`/`Save` split under `Systems/`). None of those exist in this project yet. What follows is the **actual current structure**, verified against the filesystem (glob + read), with a note underneath about which template folders are still just conventions waiting to be used. Note the `IGameState` namespace/folder detail called out under `Domain/StateMachine/` below.
```
Assets/
├── Scripts/
│   ├── Core/                      ← Foundation.
│   │   ├── GameBootstrapper.cs
│   │   ├── GameEvent.cs
│   │   ├── GameEventManager.cs
│   │   ├── IGameService.cs
│   │   ├── IIdentifiable.cs
│   │   ├── ITickable.cs
│   │   ├── ManagerBase.cs
│   │   ├── ServiceLocator.cs
│   │   └── Utils/
│   │       ├── InteractionPhysicsUtils.cs      ← NEW (center-screen raycast helpers for grid interaction)
│   │       ├── PhysicsUtils.cs               ← downward-ground raycast for spawn positioning
│   │       └── SpriteBillboard.cs            ← NEW (billboard utility used by SlimeEntity)
│   │
│   ├── Editor/
│   │   └── MaterialTextureMapper.cs
│   │
│   ├── Domain/                    ← Pure C# logic. Convention, not enforced by compiler.
│   │   ├── Entities/               ← ActorState, AreaLifecycleEvents*(NEW), BaseAttribute, EntityEvents, EntityInstance, EntityLifecycleTypes*(NEW), EntityRegistry
│   │   ├── Grid/                   ← BlockDefinition, BlockPalette*(NEW), BlockRegistry, GridEvents*(NEW), GridRoomLink, RoomGridData, RoomSocketData*(NEW)
│   │   ├── Interaction/             ← GridBlockClickedEvent, GridHitInfo
│   │   ├── LevelGeneration/         ← IRoomGraphGenerator, LevelGenerationData, LevelRenderConfig, RoomArchetype*(NEW)
│   │   ├── Meshing/                 ← MeshTypes
│   │   ├── Persistence/             ← PersistenceEvents, SaveData
│   │   └── StateMachine/            ← IGameState (namespace Core — folder mismatch), GameStateMachine, GameStates, GameStateType (namespace Domain.Entities.StateMachine)
│   │
│   ├── Gameplay/                  ← Feature systems.
│   │   ├── Entities/               ← AttributeComponent, BaseActor, BaseContainer, BaseEntity, BaseItem, SlimeEntity*(NEW), StateComponent, StatsComponent
│   │   ├── Player/                 ← PlayerGridInteractor, PlayerInputEvents, Player_CameraRig, Player_Look, Player_Movement
│   │   ├── UI/                     ← NEW — HotbarHUD (minimal UI Toolkit HUD, see Log.md)
│   │   └── World/                  ← ExitTrigger, LevelGenerationEvents, LevelLayoutRenderer, RoomPrimitiveTile (dead code — no references), RoomTrackerTrigger*(NEW), VoxelStairBuilder*(NEW)
│   │
│   └── Systems/                   ← Shared IGameService implementations.
│       ├── Input/                 ← InputManager
│       ├── Interaction/            ← BlockPlacementService, GridInteractionService, HotbarService*(NEW)
│       ├── LevelGeneration/        ← LevelGenerationService
│       ├── Meshing/                ← GreedyMeshingJob, GreedyMeshingService, GridMeshBuilder
│       ├── Persistence/            ← FileSaveService
│       ├── Player/                 ← PlayerSpawnService
│       ├── Pooling/                ← MonoBehaviourPool, ObjectPool, PoolingManager
│       └── Scheduler/              ← NEW — AreaSchedulerService (ITickable; room-tier lifecycle via RoomEnteredEvent). Tick() is a no-op stub
│
├── Data/                          ← ScriptableObject blueprints + C# config scripts + Databases (NOT under Scripts/).
│   ├── Config/                    ← ActorData, ContainerData, EntityData, ItemData (.cs, namespace Data.Config) + WorldGeneration/ (LevelRenderConfig.asset, BlockRegistry.asset, Blocks/*, Archetypes/*, Palettes/*)
│   ├── Databases/                 ← Database.cs, DatabaseLookup.cs, Databases.cs (the latter two bodies are commented-out placeholders; no concrete DB active)
│   └── Definitions/                ← EntityRegistry.asset, ActorData/DebugActorData.asset
│
└── Docs/
    ├── GDD.md
    ├── ARCHITECTURE.md
    ├── EVENTS.md
    ├── STATES.md
    ├── DECISIONS.md
    ├── CONTEXT.md
    ├── AUDIT_LOG.md
    ├── Log.md
    ├── LEVEL_GENERATION.md
    └── reusable_code_combinations.md
```

**Template conventions not yet in use** (still valid future targets per Article VII — don't add them until a real need shows up): `Infrastructure/Views` and `Infrastructure/Adapters` for Unity-dependent view/adapter separation, a top-level `Utilities/` folder, `Tests/EditMode` and `Tests/PlayMode`, `Docs/Features/[feature-name]/` for per-feature behavior notes, and dedicated `Systems/Audio`/`Systems/UI` (no audio or UI system exists yet at all — see `Log.md`).

**Note on `Domain/`**: This folder is a convention that signals intent — pure C# logic that could theoretically run without Unity. The compiler does not enforce this. You enforce it when it matters. For complex logic systems (voxel meshing, simulation, stat calculations), keeping them Unity-free makes them easier to test and reason about. For gameplay systems, use judgment.

---

## Core Patterns

---

### 1. Service Locator

The single source of dependency resolution for shared runtime systems.

> **Corrected 2026-08-21** — the real `ServiceLocator.cs` also has `TryGet<T>` (for optional lookups that shouldn't log an error on miss) and `Unregister<T>`, neither of which were in this sample before. `ShutdownAll()` is actually named `Clear()`.

```csharp
public interface IGameService
{
    void Initialize();
    void Shutdown();
}

public static class ServiceLocator
{
    private static readonly Dictionary<Type, IGameService> _services = new();

    public static void Register<T>(T service) where T : class, IGameService
    {
        var type = typeof(T);
        if (_services.ContainsKey(type))
        {
            Debug.LogWarning($"[ServiceLocator] {type.Name} already registered. Overwriting.");
            _services[type].Shutdown();
        }
        _services[type] = service;
        service.Initialize();
    }

    public static T Get<T>() where T : class, IGameService
    {
        if (_services.TryGetValue(typeof(T), out var service)) return (T)service;
        Debug.LogError($"[ServiceLocator] Service {typeof(T).Name} not found.");
        return null;
    }

    // Optional retrieval, no error logged on miss — use for services that may not be registered.
    public static bool TryGet<T>(out T service) where T : class, IGameService
    {
        if (_services.TryGetValue(typeof(T), out var raw))
        {
            service = (T)raw;
            return true;
        }
        service = null;
        return false;
    }

    public static void Unregister<T>() where T : class, IGameService
    {
        var type = typeof(T);
        if (!_services.TryGetValue(type, out var service))
        {
            Debug.LogWarning($"[ServiceLocator] Cannot unregister {type.Name} — not registered.");
            return;
        }
        _services.Remove(type);
        service.Shutdown();
    }

    public static void Clear()
    {
        foreach (var service in _services.Values) service.Shutdown();
        _services.Clear();
    }
}
```

Register here: `GameStateMachine`, `InputManager`, `PoolingManager`, `LevelGenerationService`, `GridInteractionService`, `BlockPlacementService`, `FileSaveService`, `PlayerSpawnService` — the 8 services currently registered by `GameBootstrapper`. (`AudioManager`, `SaveManager`, `DialogueManager`, and `UIManager` don't exist yet; treat them as illustrative future examples, not current registrations.)

Do not over-register. Local features that are not shared do not need the ServiceLocator.

---

### 2. Typed Event Bus

Decoupled gameplay communication. Generic and typed — no per-event static fields.

```csharp
public abstract class GameEvent { }

public static class GameEventManager
{
    private static readonly Dictionary<Type, List<Delegate>> _listeners = new();

    public static void AddListener<T>(Action<T> listener) where T : GameEvent
    {
        var type = typeof(T);
        if (!_listeners.ContainsKey(type)) _listeners[type] = new List<Delegate>();
        _listeners[type].Add(listener);
    }

    public static void RemoveListener<T>(Action<T> listener) where T : GameEvent
    {
        if (_listeners.TryGetValue(typeof(T), out var list))
            list.Remove(listener);
    }

    public static void Publish<T>(T gameEvent) where T : GameEvent
    {
        if (_listeners.TryGetValue(typeof(T), out var list))
            foreach (var listener in list.ToArray())
                ((Action<T>)listener)?.Invoke(gameEvent);
    }

    public static void Clear()
    {
        _listeners.Clear();
    }
}
```

**Why generic, not per-event statics**: per-event static fields (`public static event Action<X> OnX`) scatter across the codebase with no central clear, no single subscription point, and no way to audit what events exist. The generic bus centralizes this. EVENTS.md catalogs everything that passes through it — 21 events as of the 2026-08-21 audit.

---

### 3. Async State Machine

Each state is a standalone module with clean async enter/exit transitions.

> **Corrected 2026-08-21** — the real `GameStateMachine` also implements `ITickable` and exposes `CurrentState`/`CurrentStateType` (used by `InputManager` for input-gating comparisons), none of which were in this sample before. The "Standard states" list below was also wrong — see the note under the sample.

```csharp
// Lives in Domain/StateMachine/ — pure C#
public interface IGameState
{
    GameStateType StateType { get; }
    Task OnEnterAsync();
    Task OnExitAsync();
    void OnTick(float deltaTime);
}

public class GameStateMachine : IGameService, ITickable
{
    private IGameState _currentState;

    public IGameState CurrentState => _currentState;
    public GameStateType CurrentStateType => _currentState != null ? _currentState.StateType : GameStateType.None;

    public async Task TransitionToAsync(IGameState newState)
    {
        if (_currentState != null) await _currentState.OnExitAsync();
        _currentState = newState;
        if (_currentState != null) await _currentState.OnEnterAsync();
    }

    public void Tick(float deltaTime) => _currentState?.OnTick(deltaTime);
    public void Initialize() { }
    public void Shutdown() { }
}
```

Real states in this project: `Playing`, `MainMenu`, `GameOver`, `Paused`, `Loading` (matching the `GameStateType` enum) — not the `BootState`/`MainMenuState`/`GameplayState`/`PauseState`/`GameOverState` names this section previously listed. As of the 2026-08-21 audit, `Playing` is the only one ever actually transitioned into — see `STATES.md` for the full picture, including which states are unwired shells.

Each state entry in `Docs/STATES.md` should note: enter condition, exit conditions, events published, events consumed.

---

### 4. Data / Instance Separation

ScriptableObjects are blueprints. Runtime objects are pure C# instances holding live state. They are never mixed.

```csharp
// Blueprint — ScriptableObject. Lives in Data/. Configuration only, never runtime state.
[CreateAssetMenu(menuName = "Game/EntityData")]
public class EntityData : ScriptableObject
{
    public string Id;
    public string DisplayName;
    // stats, configuration — never runtime mutable state
}

// Runtime instance — pure C#. Lives in Domain/Entities/.
public class EntityInstance
{
    public EntityData Data { get; }
    public AttributeCollection Attributes { get; }

    public EntityInstance(EntityData data)
    {
        Data = data;
        Attributes = new AttributeCollection(data);
    }
}
```

---

### 5. Centralized Bootstrapper

Single entry point. Explicit initialization phases. No scene order magic.

> **Corrected 2026-08-21** — the real `GameBootstrapper` uses a static `Instance` property for its singleton check rather than `FindObjectsOfType`, clears both `ServiceLocator` and `GameEventManager` in `OnDestroy` (missing from the old sample), and its `Update()` ticks only the cached `GameStateMachine` reference directly rather than looping over multiple services. The registered-service list below is also real, not illustrative.

```csharp
[DefaultExecutionOrder(-100)]
public class GameBootstrapper : MonoBehaviour
{
    public static GameBootstrapper Instance { get; private set; }

    private GameStateMachine _stateMachine;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        InitializeServices();
    }

    private void Update()
    {
        _stateMachine?.Tick(Time.deltaTime);
    }

    private void OnDestroy()
    {
        if (Instance != this) return;
        ServiceLocator.Clear();
        GameEventManager.Clear();
        Instance = null;
    }

    private void InitializeServices()
    {
        GameEventManager.Clear();

        _stateMachine = new GameStateMachine();
        ServiceLocator.Register(_stateMachine);

        ServiceLocator.Register(new InputManager());
        ServiceLocator.Register(new Systems.Pooling.PoolingManager());
        ServiceLocator.Register(new LevelGenerationService());
        ServiceLocator.Register(new Systems.Interaction.GridInteractionService());
        ServiceLocator.Register(new Systems.Interaction.BlockPlacementService());
        ServiceLocator.Register(new Systems.Persistence.FileSaveService());
        ServiceLocator.Register(new Systems.Player.PlayerSpawnService());

        _ = _stateMachine.TransitionToAsync(new Playing());
    }
}
```

No `AudioManager`/`UIManager` registrations exist yet — the "Presentation always Phase 4" guidance below is still good practice for when those are added, it just doesn't describe a phase split that exists in code today.

Presentation always Phase 4. Core always Phase 1. A new system's phase belongs in whatever note or behavior doc covers that feature.

---

### 6. Manager / View Separation

A reliable boundary that keeps logic testable and views dumb.

```
Manager  → pure C#, IGameService — owns logic, state, rules
View     → MonoBehaviour — reads manager state, renders it, never owns logic
```

Views call no business logic. Managers hold no GameObjects. If testing a manager requires launching a Unity scene, the logic is in the wrong layer.

---

### 7. Generic Manager Base

For content-heavy systems: RPG actors, inventory, dialogue, skills, quests.

```csharp
public abstract class ManagerBase<TInstance, TData, TDatabase> : IGameService
    where TData     : ScriptableObject
    where TDatabase : ScriptableObject
{
    protected TDatabase Database { get; private set; }
    protected Dictionary<string, TInstance> ActiveInstances { get; } = new();

    public virtual void Initialize() => LoadDatabase();
    public virtual void Shutdown()   => ActiveInstances.Clear();

    protected abstract TInstance CreateInstance(TData data);
    protected abstract void LoadDatabase();
}
```

Use when you have a database of definitions and a runtime collection of instances. Do not use it for one-off systems.

---

### 8. Object Pooling

Any system spawning/destroying GameObjects at runtime should use a pool.

```
Systems/Pooling/
  ├── PoolingManager.cs    ← IGameService, registered Phase 3
  ├── IPoolable.cs         ← Contract for pooled objects
  └── ObjectPool<T>.cs     ← Generic typed pool
```

Register in Bootstrapper Phase 3. Direct `Instantiate` in gameplay code is acceptable in Phase 1 exploration. Migrate to pooling in Phase 2 if the system causes visible allocation spikes.

> **Note (2026-08-21):** `PoolingManager` is registered and functional, but nothing currently calls `GetPooledObject` anywhere in the project — the level renderer moved to mesh generation instead of spawning individual pooled GameObjects (see `LEVEL_GENERATION.md`). This pattern is still the right one for future prop/entity spawning, it's just unused today.

---

## Documentation Layer

Documentation clarifies intent, models interactions, tracks decisions, and reduces confusion. It does not duplicate obvious code.

---

### Core Docs

|File|Purpose|Updated When|
|---|---|---|
|`GDD.md`|Vision, core loop, mechanics, out-of-scope guard|Design decisions|
|`ARCHITECTURE.md`|High-level system map, folder conventions|Structural changes|
|`EVENTS.md`|Event catalog: publisher, subscriber, payload|Every new GameEvent|
|`STATES.md`|State catalog: enter/exit conditions, events|Every new game state|
|`DECISIONS.md`|Architecture decision log (ADR format)|Non-obvious choices|
|`CONTEXT.md`|Session continuity: done, pending, known issues|End of session|

---

### DECISIONS.md — Architecture Decision Records

The most valuable doc for AI-assisted work and future-you. When you make a non-obvious architecture choice, log it here.

Minimal format:

```markdown
## [Date] — [Short title]

**Decision**: What was chosen.
**Context**: What problem existed. What constraints applied.
**Alternatives considered**: What else was evaluated and why it was rejected.
**Why this**: The actual reason.
```

Example:

```markdown
## 2025-06-01 — Generic EventBus over per-event statics

**Decision**: GameEventManager<T> generic bus instead of per-event static Action fields.
**Context**: Per-event statics scatter across 40+ files with no central clear.
Impossible to audit all subscriptions. Memory leaks from forgotten unsubs.
**Alternatives considered**: Per-event static events (simpler per-site), 
Unity's built-in UnityEvent (Inspector-friendly but slow and verbose).
**Why this**: Single Clear() on scene unload. Single place to audit all event types.
EVENTS.md becomes the complete catalog. AI sessions benefit from one source of truth.
```

---

### CONTEXT.md — Session Continuity

Written at the end of every meaningful session. Answers the question: "what was happening and what comes next?"

```markdown
## Session — [Date]

**Completed**: What was finished.
**In progress**: What was started but not finished.
**Pending**: What is next.
**Known issues**: What is broken or fragile.
**Decisions made**: Any architectural choices (reference DECISIONS.md).
```

---

### Features/ — Per-Feature Docs

Lightweight. Only what is not obvious from reading the code.

```
Docs/Features/combat/
  ├── behavior.md    ← State diagram, event flow, gameplay loop description
  └── notes.md       ← Known issues, deferred ideas, why it works this way
```

Useful content:

- state diagrams (what states exist, transitions, conditions)
- event flow maps (what triggers what)
- gameplay loop descriptions (player action → system reaction → feedback)
- known problems
- intentional design constraints

Not useful: restating what the code already clearly says.

---

## AI Collaboration

AI works best when given intent, context, constraints, and behavioral expectations. It does not require a rigid formal specification pipeline to produce good output.

**Effective AI sessions look like**:

- describing the mechanic's behavior and purpose
- sharing the relevant state diagram or event flow sketch
- pointing to CONTEXT.md for current project state
- pointing to EVENTS.md and STATES.md for existing contracts
- discussing design options before committing to implementation

**Seed every AI session with**:

```
1. CONTEXT.md         → current state of the project and session
2. ARCHITECTURE.md    → system map and conventions
3. EVENTS.md          → existing event contracts
4. STATES.md          → existing state catalog
5. Feature behavior doc (if working on a specific feature)
```

AI does not need: fully planned task lists, mandatory test-first gates, or enterprise spec pipelines to be useful. It needs enough context to understand intent and constraints.

**Update after sessions**:

- CONTEXT.md with what was done and what is next
- EVENTS.md if new events were added
- STATES.md if new states were added
- DECISIONS.md if non-obvious choices were made

---

## Anti-Patterns

Extracted from real project failures. Mapped to the article each violates.

|Anti-Pattern|Violates|Consequence|
|---|---|---|
|`Manager.Instance` singleton chains|Article V|Untestable, hidden coupling|
|God class (400+ lines with mixed concerns)|Article VII|Breaks unrelated features, AI hallucinates scope|
|`FindObjectOfType`|Article V|Implicit, scene-order fragile|
|Logic in Views|Article IV / VI|Cannot reason about behavior without running Unity|
|Per-event static fields (`static event Action<X>`)|Article VI|No central clear, impossible to audit, memory leaks|
|Commented-out code left in project|Article I|AI inherits broken patterns, reads dead code as intent|
|`Old/` or `.TMP` folders in project|Article I|LLM works in stale or garbage context|
|Wrapping engine features that don't need wrapping|Article VIII|Indirection without value|
|Over-abstraction before requirements exist|Article VII|Architecture built for imaginary problems|
|Hardcoded magic values|Article I|Breaks on change, invisible to configuration|
|Runtime state stored in ScriptableObjects|Article IV|State bleeds between playtest sessions|
|Mocking real systems without reason|Article III|Tests pass, production breaks|
|Writing architecture before validating the mechanic is fun|Article II|Throws away work when direction changes|

---

## Quick Reference — Decision Heuristics

Before adding architecture:

```
Does this solve a real problem I have right now?
Does this improve iteration speed or safety?
Does this reduce confusion for me or an AI reading this later?
Will this still matter in 3 months?
```

If the answer is no — keep it simpler.

Before moving from Phase 1 to Phase 2:

```
Has this code been touched 3+ times?
Does another system depend on it?
Did a bug take more than 30 minutes to find?
```

If yes to any — stabilize it.

Before writing a test:

```
Would a silent failure in this system be expensive?
Is this logic reused in multiple places?
Did I just spend 30+ minutes debugging a regression?
```

If yes — write the test.

---

## Pattern Source Attribution

|Pattern|Origin|Article|
|---|---|---|
|ServiceLocator|`snake-prototype`|V|
|Typed Generic Event Bus|`snake-prototype` + `project_management_prototype`|VI|
|Async State Machine|`Prototype_RGP`|II|
|Data / Instance Split|`Prototype_RGP` + `3D_Learning_Prototype`|IV|
|Bootstrapper + Centralized Tick|`snake-prototype`|V + VII|
|Manager / View Separation|`snake-prototype`|IV + VI|
|Generic ManagerBase|`DatingSimEngine` / `VisualNovelEngine`|VII|
|Object Pooling|`Maze`|VII|
|Design-Guided Phases|v3.0 synthesis|IX|
|DECISIONS.md ADR format|New in v4.0|I|