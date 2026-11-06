# AI Work Rules & Operational Discipline: 3D 2D World

**Project**: 3D 2D World — Hybrid Procedural RPG  
**Target Audience**: AI Coding Assistants & Collaborating Developers  
**Philosophy**: Design-Guided Development (DGD) & Zero-Assumption Engineering  
**Status**: Mandatory Operational Guidelines  
**Last Updated**: October 2026  

---

## 1. Core Operating Principles

1. **Verify Before Trusting**:
   - Never assume a file, class, method, or asset exists based solely on documentation or memory. Always verify against the actual filesystem and codebase.
2. **Preserve What Works**:
   - Do not replace, rewrite, or refactor working subsystems (such as Burst-compiled greedy meshing or role-based block registries) unless an explicit architectural defect has been documented and verified.
3. **Never Fake Verification**:
   - Never claim a Unity scene, prefab, collider, lighting bake, animation, or gameplay mechanic was tested if it was not actually executed and validated. Clearly distinguish between code compilation checks, automated tests, and required manual playtest steps.
4. **Scope Restraint**:
   - Implement only the ticketed scope. Do not speculatively build systems for future milestones (e.g., multiplayer, inventory weight calculations, advanced dialogue trees) that are not required for the immediate vertical slice.
5. **No Unauthorized Commits**:
   - Under no circumstances will git commits or pushes be made without explicit user authorization.

---

## 2. Pre-Task Protocol

Before modifying any file or generating new code, every AI agent MUST complete this four-step checklist:

```
┌────────────────────────────────────────────────────────┐
│ 1. Read Documentation Context                          │
│    • Review GAME_DESIGN.md for intent and rules        │
│    • Review TECHNICAL_DESIGN.md for architecture       │
│    • Check PRODUCTION_BACKLOG.md for ticket acceptance │
└──────────────────────────┬─────────────────────────────┘
                           │
                           ▼
┌────────────────────────────────────────────────────────┐
│ 2. Inspect Target Code & Assets                        │
│    • View exact lines of target files                  │
│    • Check serialized references in relevant .meta     │
│    • Verify dependencies in ServiceLocator / Events    │
└──────────────────────────┬─────────────────────────────┘
                           │
                           ▼
┌────────────────────────────────────────────────────────┐
│ 3. Formulate Implementation Plan                       │
│    • State exact files to change / create              │
│    • State how dependencies will be satisfied          │
│    • Identify potential breakages or regressions       │
└──────────────────────────┬─────────────────────────────┘
                           │
                           ▼
┌────────────────────────────────────────────────────────┐
│ 4. Verify Baseline Compilation                         │
│    • Run `dotnet build "Assembly-CSharp.csproj"`       │
│    • Confirm 0 errors before starting edits            │
└────────────────────────────────────────────────────────┘
```

---

## 3. Code Quality Standards (Jostin's Style)

All generated C# code must conform strictly to these project standards:

### 3.1 Naming & Structure
- **C# Naming**: `PascalCase` for classes, structs, enums, methods, and public properties. `_camelCase` with leading underscore for private fields. `camelCase` for parameters and local variables.
- **Length Constraint**: Keep methods under **50 lines**. Split longer routines into clear, single-responsibility private helper methods.
- **Region Blocks**: Group class members using `#region` blocks:
  - `#region Fields & Serialized`
  - `#region Lifecycle Methods` (Awake, OnEnable, Start, Update, OnDisable, OnDestroy)
  - `#region Public API`
  - `#region Event Handlers`
  - `#region Private Helpers`
- **Component Menus**: Every new `MonoBehaviour` MUST include `[AddComponentMenu("3D2DWorld/...")]` and `[DisallowMultipleComponent]` where appropriate.

### 3.2 Architectural Discipline
- **Data-Driven SOs**: ScriptableObjects are blueprints only. NEVER store runtime mutable state (e.g., current player HP, quest step, cooldown timers) inside a ScriptableObject.
- **No Manager Singletons**: Do not use `Manager.Instance.Subsystem.DoThing()`. Use `ServiceLocator.Get<T>()` or `ServiceLocator.TryGet<T>(out var service)`.
- **Event Bus Decoupling**: Systems communicating across architectural layers (e.g., UI reacting to combat, or area scheduling reacting to room entry) must publish typed events via `GameEventManager.Publish<T>()`. Catalog all new events in [`EVENTS.md`](file:///D:/Development/Game%20Development/3D%202D%20World/Assets/Docs/EVENTS.md).
- **No Reflection Messaging**: Avoid `SendMessage()` or `BroadcastMessage()`. Use explicit interfaces (`IInteractable`, `IDamageable`) or direct typed component lookups.
- **No Magic Numbers**: Expose tunable parameters in ScriptableObjects or serialized fields with `[SerializeField]` and `[Tooltip]`.

---

## 4. Verification & Testing Protocol

Every completed ticket must pass through three verification gates before being marked ready for review:

### Gate 1: Static Compilation (Automated)
Execute command:
```powershell
dotnet build "Assembly-CSharp.csproj"
dotnet build "Assembly-CSharp-Editor.csproj"
```
- **Pass Threshold**: 0 Errors, 0 Warnings.

### Gate 2: Automated Unit Testing (Automated)
Where pure C# domain logic is introduced or changed (`RoomGraphGenerator`, `QuestManager`, `AttributeComponent` math, serialization):
- Execute associated EditMode test runners.
- Confirm assertions pass.

### Gate 3: PlayMode / Manual Verification Reporting
Since headless terminal tools cannot simulate human playfeel:
- State explicitly what was verified programmatically.
- Provide step-by-step instructions for the user to perform manual playtest verification in the Unity Editor:
  1. Open target scene (e.g., `Assets/Scenes/SampleScene.unity`).
  2. Press Play.
  3. Action to perform (e.g., *Walk up to cargo crate and press 'E'*).
  4. Expected outcome (e.g., *Console logs item pickup; Hotbar / Inventory displays Ration Pack*).

---

## 5. Git Branching, Checkpoint & Rollback Protocol

### 5.1 Branch Naming Conventions
- Feature tasks: `feature/forest-quest-<ticket-id>-<short-description>`  
  *Example*: `feature/forest-quest-tick03-interaction`
- Bug fixes: `fix/<short-description>`  
  *Example*: `fix/bootstrapper-double-tick`
- Documentation passes: `docs/<short-description>`  
  *Example*: `docs/production-foundation`

### 5.2 Commit Authorization Gate
- **RULE**: NEVER execute `git commit` or `git push` autonomously.
- Always stage changes and summarize them for the project owner:
  ```markdown
  ### Ready for Commit Review
  - **Branch**: `feature/forest-quest-tick01-cleanup`
  - **Files Modified**: `GameBootstrapper.cs`, `SampleScene.unity`
  - **Summary**: Removed duplicate state machine tick and purged 150+ static prototype objects.
  - **Checks**: `dotnet build` passed (0 errors, 0 warnings).
  - *Awaiting authorization to commit.*
  ```

### 5.3 Rollback & Stash Procedures
If a change introduces compilation errors or unexpected asset serialization conflicts:
1. Revert specific modified file:
   ```powershell
   git checkout -- <file-path>
   ```
2. Stash uncommitted changes safely:
   ```powershell
   git stash save "checkpoint-before-rollback"
   ```
3. Never force-clean (`git clean -fdx`) without explicit permission, as this will destroy untracked assets and meta files.
