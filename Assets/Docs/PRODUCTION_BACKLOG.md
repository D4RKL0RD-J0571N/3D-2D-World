# Production Backlog & Execution Plan: Forest Vertical Slice

**Project**: 3D 2D World — Hybrid Procedural RPG  
**Target Milestone**: Vertical Slice 1 ("The Silent Canopy")  
**Branch Convention**: `feature/forest-quest-*`  
**Status**: Approved Production Backlog  
**Last Updated**: October 2026  

---

## 1. Milestone Overview & Release Criteria

### Vertical Slice Definition
A complete, playable single-player quest slice where Jack awakens at a forest crash site, gathers emergency rations and survival tools, explores down a wooded ravine to a mountain spring, encounters and survives the Corrupted Tree Spirit, and ascends an ancient stone trail toward civilization.

### Acceptance Gates for Release
1. **Zero Compilation Errors / Warnings**: Clean build on `Assembly-CSharp.csproj` and `Assembly-CSharp-Editor.csproj`.
2. **Gameplay Flow Completeness**: Player can start, scavenge, battle, defeat boss, and reach exit without console exceptions or infinite loops.
3. **Save/Load Integrity**: Player can save mid-quest (F5), reload, and resume with intact position, inventory, health, and quest objective.
4. **Performance Target**: Consistent $\ge 60\,\text{FPS}$ in 1080p standalone build; greedy meshing execution $\le 35\,\text{ms}$ per room.
5. **Clean Scene**: `SampleScene.unity` contains zero leftover static prototype tiles.

---

## 2. Dependency Graph & Parallel Execution Map

```
┌────────────────────────────────────────────────────────┐
│             TASK 1: Bugfixes & Scene Cleanup           │
│         (Remove double-tick & purge static clutter)    │
└──────────────────────────┬─────────────────────────────┘
                           │
                           ▼
┌────────────────────────────────────────────────────────┐
│           TASK 2: Actor & Attribute Hardening          │
│       (Fix regen, JackActorData with Health/Stamina)   │
└──────────────┬───────────────────────────┬─────────────┘
               │                           │
               ▼                           ▼
┌──────────────────────────────┐ ┌──────────────────────────────┐
│  PARALLEL TRACK A (Gameplay) │ │ PARALLEL TRACK B (World Gen) │
│                              │ │                              │
│ Task 3: Interaction & Inv    │ │ Task 5: Forest Clearings &   │
│   ├── IInteractable          │ │   ├── ForestClearing Archetype│
│   ├── BaseItem / Container   │ │   ├── Water Pond generation  │
│   └── PlayerInventory        │ │   └── Socket Tree Decorators │
│                              │ │                              │
│ Task 4: Combat Pipeline      │ └──────────────┬───────────────┘
│   ├── WeaponComponent        │                │
│   ├── DamageInfo / IDamageable                │
│   └── Slime Attack Loop      │                │
│                              │                │
│ Task 6: Tree Spirit Boss AI  │                │
│   ├── Root spike telegraph   │                │
│   └── Branch slam & defeat   │                │
│                              │                │
│ Task 7: Quest State Machine  │                │
│   ├── QuestManager service   │                │
│   └── Step progression       │                │
└──────────────┬───────────────┘                │
               │                                │
               └───────────────┬────────────────┘
                               │
                               ▼
┌────────────────────────────────────────────────────────┐
│          TASK 8: Forest Quest Level Integration        │
│      (Assemble cohesive slice in SampleScene.unity)    │
└──────────────────────────────┬─────────────────────────┘
                               │
                               ▼
┌────────────────────────────────────────────────────────┐
│       TASK 9: Persistence & Regression Hardening       │
│     (Save quest state, entity states, automated tests) │
└────────────────────────────────────────────────────────┘
```

### Parallel Execution Safety Matrix
- **Safe to run in parallel**:
  - **Track A (Combat / Quest)**: `WeaponComponent`, `DamageInfo`, `QuestManager`, `TreeSpiritEntity` (touches `Gameplay/Combat`, `Domain/Quest`, `Gameplay/Entities`).
  - **Track B (World Generation)**: `RoomGraphGenerator`, `LevelRenderConfig`, `VoxelStairBuilder` (touches `Domain/LevelGeneration`, `Domain/Grid`, `Gameplay/World`).
- **Must run sequentially**:
  - Task 1 must complete before any scene or bootstrapper work.
  - Task 2 must complete before Combat (Task 4) or Tree Spirit (Task 6), as both rely on validated `AttributeComponent` logic.
  - Task 8 requires completion of all gameplay and world gen tasks.
  - Task 9 requires completed quest and entity schemas to serialize.

---

## 3. Work Breakdown Structure (Detailed Tickets)

### Epic 1: Stability & Foundation

#### `TICK-01`: Core Engine Bugfixes & Scene Cleanup
- **Priority**: P0 (Blocker)
- **Status**: COMPLETED (Pending Review)
- **Dependencies**: None
- **Affected Files**:
  - [`Assets/Scripts/Core/GameBootstrapper.cs`](file:///D:/Development/Game%20Development/3D%202D%20World/Assets/Scripts/Core/GameBootstrapper.cs)
  - [`Assets/Scenes/SampleScene.unity`](file:///D:/Development/Game%20Development/3D%202D%20World/Assets/Scenes/SampleScene.unity)
- **Specification**:
  1. In `GameBootstrapper.Update()`, delete line 37 (`_stateMachine?.Tick(Time.deltaTime);`). The state machine is already ticked via line 39 (`foreach (var tickable in _tickables)`).
  2. In `SampleScene.unity`, delete all static prototype gameobjects (`Wall_*`, `Floor_*`, `Ceiling_*`, `OpenRoomTest`, `Path_*`, `Slime_Placeholder`).
  3. Ensure only `GameBootstrapper`, `Level Generator`, `Actor`, `Directional Light`, and `Global Volume` remain in root.
- **Acceptance Criteria**:
  - `dotnet build "Assembly-CSharp.csproj"` succeeds with 0 errors.
  - Launching scene in Editor shows only procedural level geometry; zero duplicate ticks logged.

---

#### `TICK-02`: Actor Attributes & Player Blueprint Hardening
- **Priority**: P0 (Blocker)
- **Dependencies**: `TICK-01`
- **Affected Files**:
  - [`Assets/Scripts/Gameplay/Entities/AttributeComponent.cs`](file:///D:/Development/Game%20Development/3D%202D%20World/Assets/Scripts/Gameplay/Entities/AttributeComponent.cs)
  - [`Assets/Scripts/Gameplay/Entities/BaseEntity.cs`](file:///D:/Development/Game%20Development/3D%202D%20World/Assets/Scripts/Gameplay/Entities/BaseEntity.cs)
  - [`Assets/Scripts/Gameplay/Entities/BaseActor.cs`](file:///D:/Development/Game%20Development/3D%202D%20World/Assets/Scripts/Gameplay/Entities/BaseActor.cs)
  - `Assets/Data/Definitions/ActorData/JackActorData.asset` (New)
- **Specification**:
  1. In `AttributeComponent.Update()`, implement float accumulation for regeneration:
     ```csharp
     float rate = attr.RegenerationRate * attr.RegenerationMultiplier * (inCombat ? attr.CombatRegenerationRate : 1.0f);
     attr.CurrentValue = Mathf.Min(attr.MaximumValue, Mathf.RoundToInt(attr.CurrentValue + (rate * dt)));
     ```
  2. Create `JackActorData.asset` derived from `ActorData`:
     - `StartingLevel`: 1
     - `StartingAttributes`:
       - `Health`: Max 100, Current 100, RegenRate 0.5, CombatRegen 0.0
       - `Stamina`: Max 100, Current 100, RegenRate 25.0, CombatRegen 15.0
  3. Assign `JackActorData.asset` to the `Actor` component in `SampleScene.unity`.
  4. Replace `SendMessage("OnActorInitialized")` with direct calls or interface `IActorInitializable`.
- **Acceptance Criteria**:
  - In Play mode, inspect player `AttributeComponent`: `Health` is initialized to $100 / 100$ and `Stamina` is $100 / 100$.
  - Damaging Jack from code decreases Health; Health slowly regenerates out of combat.

---

### Epic 2: Interaction & Player Capabilities

#### `TICK-03`: Interaction Controller & Inventory Foundation
- **Priority**: P1
- **Dependencies**: `TICK-02`
- **Affected Files**:
  - `Assets/Scripts/Domain/Interaction/IInteractable.cs` (New)
  - `Assets/Scripts/Gameplay/Player/PlayerInteractionController.cs` (New)
  - [`Assets/Scripts/Gameplay/Entities/BaseItem.cs`](file:///D:/Development/Game%20Development/3D%202D%20World/Assets/Scripts/Gameplay/Entities/BaseItem.cs)
  - [`Assets/Scripts/Gameplay/Entities/BaseContainer.cs`](file:///D:/Development/Game%20Development/3D%202D%20World/Assets/Scripts/Gameplay/Entities/BaseContainer.cs)
  - `Assets/Scripts/Gameplay/Entities/InventoryComponent.cs` (New)
  - [`Assets/Scripts/Systems/Input/InputManager.cs`](file:///D:/Development/Game%20Development/3D%202D%20World/Assets/Scripts/Systems/Input/InputManager.cs)
- **Specification**:
  1. Create `IInteractable` interface (`string PromptText`, `void Interact(GameObject user)`).
  2. Implement `IInteractable` on `BaseItem` (adds item to user's `InventoryComponent` and destroys world gameobject).
  3. Implement `IInteractable` on `BaseContainer` (logs contents, transfers items to user, triggers opening animation).
  4. Create `InventoryComponent` holding `List<InventorySlot>` with `ItemData` and `Count`.
  5. Add `_interactAction` (Key `E` / Gamepad `X`) to `InputManager` and publish `PlayerInteractInputEvent`.
  6. Create `PlayerInteractionController` on player camera: performs $2.5\,\text{m}$ raycast for `IInteractable`.
- **Acceptance Criteria**:
  - Approaching a crate displays prompt; pressing `E` transfers items to player inventory.
  - Picking up loose item adds it to inventory and removes it from the scene.

---

#### `TICK-04`: Melee Combat Pipeline & Minion Attack Loop
- **Priority**: P1
- **Dependencies**: `TICK-02`, `TICK-03`
- **Affected Files**:
  - `Assets/Scripts/Domain/Combat/DamageInfo.cs` (New)
  - `Assets/Scripts/Domain/Combat/IDamageable.cs` (New)
  - `Assets/Scripts/Gameplay/Combat/WeaponComponent.cs` (New)
  - [`Assets/Scripts/Gameplay/Entities/SlimeEntity.cs`](file:///D:/Development/Game%20Development/3D%202D%20World/Assets/Scripts/Gameplay/Entities/SlimeEntity.cs)
  - [`Assets/Scripts/Gameplay/Player/PlayerGridInteractor.cs`](file:///D:/Development/Game%20Development/3D%202D%20World/Assets/Scripts/Gameplay/Player/PlayerGridInteractor.cs)
- **Specification**:
  1. Implement `DamageInfo` struct (`Amount`, `DamageType`, `Instigator`, `HitPoint`, `HitNormal`).
  2. Implement `IDamageable` on `BaseActor`, routing to `AttributeComponent.ApplyDamage("Health", amount)`.
  3. Create `WeaponComponent` on player:
     - Left-click triggers melee swing ($2.2\,\text{m}$ spherecast forward).
     - Consumes $20$ Stamina; requires at least $10$ Stamina to initiate.
     - On hit with `IDamageable`, delivers $25$ physical damage.
  4. In `SlimeEntity.cs`, implement attack execution:
     - In `SlimeAIState.Chasing`, when `distToPlayer <= _attackRange`, execute bite/lunge.
     - Calls `IDamageable.TakeDamage` on Jack for $10$ Acid damage.
     - Puts attack on a $1.2\text{s}$ cooldown.
  5. Decouple `PlayerGridInteractor`: ensure primary click only breaks blocks when a building tool is active, not during weapon combat.
- **Acceptance Criteria**:
  - Player left-clicks near Slime $\to$ Slime takes $25$ damage, flashes, and dies upon reaching $0$ HP.
  - Slime reaches Player $\to$ Player takes $10$ damage, reducing Health attribute.
  - Exhausted Stamina prevents immediate swinging.

---

### Epic 3: World Generation & Boss Encounter

#### `TICK-05`: Forest Biome Clearings & Prefab Sockets
- **Priority**: P1
- **Dependencies**: `TICK-01`
- **Affected Files**:
  - `Assets/Scripts/Domain/LevelGeneration/RoomGraphGenerator.cs` (Extract from `LevelGenerationData.cs`)
  - [`Assets/Scripts/Domain/LevelGeneration/RoomArchetype.cs`](file:///D:/Development/Game%20Development/3D%202D%20World/Assets/Scripts/Domain/LevelGeneration/RoomArchetype.cs)
  - [`Assets/Scripts/Gameplay/World/LevelLayoutRenderer.cs`](file:///D:/Development/Game%20Development/3D%202D%20World/Assets/Scripts/Gameplay/World/LevelLayoutRenderer.cs)
  - `Assets/Data/Config/WorldGeneration/Archetypes/ForestClearing.asset` (New)
  - `Assets/Data/Config/WorldGeneration/Archetypes/ForestPond.asset` (New)
- **Specification**:
  1. Extract `RoomGraphGenerator` to its own file.
  2. Create `ForestClearing` archetype: open ceiling (air), grass floor palette, perimeter barrier blocks.
  3. Create `ForestPond` archetype: sunken pit ($2$ blocks deep) using water block (`BLCK_0030`) and sloped bank stairs.
  4. In `LevelLayoutRenderer.BuildRoomGrid`:
     - Read `room.Sockets` filtered by `SocketType.PropFoliage` / `PropLight`.
     - Instantiate conventional tree and rock prefabs at sockets with random Y-rotations.
- **Acceptance Criteria**:
  - Procedural level generates open forest glades and natural water ponds without ceiling voxels.
  - Trees and rocks instantiate cleanly at socket coordinates.

---

#### `TICK-06`: Corrupted Tree Spirit Boss Encounter
- **Priority**: P1
- **Dependencies**: `TICK-02`, `TICK-04`, `TICK-05`
- **Affected Files**:
  - `Assets/Scripts/Gameplay/Entities/TreeSpiritEntity.cs` (New)
  - `Assets/Prefabs/Entities/TreeSpirit.prefab` (New)
  - `Assets/Data/Definitions/ActorData/TreeSpiritActorData.asset` (New)
- **Specification**:
  1. Create `TreeSpiritActorData`: Health $250$, StartingLevel $3$.
  2. Build `TreeSpirit.prefab` using graybox treant mesh, `CharacterController`, `AttributeComponent`, and `TreeSpiritEntity`.
  3. Implement `TreeSpiritEntity` state machine:
     - **Dormant**: Stands inert until player enters $12\,\text{m}$ radius.
     - **Root Spike Attack**: Channels for $1.0\text{s}$, spawns telegraph circle on player position; wooden spikes erupt $1.2\text{s}$ later, dealing $20$ damage.
     - **Branch Swipe**: Melee sweep dealing $30$ damage + knockback if player is within $4.0\,\text{m}$.
     - **Stagger State**: Becomes immobile for $2.5\text{s}$ after swiping, allowing player damage window.
     - **Defeat**: At $0$ HP, plays dissolve/retreat animation, drops "Ancient Forest Heart", and publishes `BossDefeatedEvent`.
- **Acceptance Criteria**:
  - Entering spring glade triggers boss awakening.
  - Root spikes spawn warning visuals and deal damage if player fails to dodge.
  - Boss can be reduced to $0$ HP, firing defeat event.

---

### Epic 4: Narrative, Progression & Persistence

#### `TICK-07`: Quest State Machine & Objective Tracking
- **Priority**: P1
- **Dependencies**: `TICK-03`, `TICK-04`, `TICK-06`
- **Affected Files**:
  - `Assets/Scripts/Domain/Quest/QuestState.cs` (New)
  - `Assets/Scripts/Domain/Quest/QuestEvents.cs` (New)
  - `Assets/Scripts/Systems/Quest/QuestManager.cs` (New `IGameService`)
  - `Assets/Scripts/Gameplay/World/QuestTriggerVolume.cs` (New)
  - `Assets/Scripts/Gameplay/UI/QuestHUD.cs` (New)
- **Specification**:
  1. Define `QuestStep` enum (`CrashSiteScavenge`, `FindWaterSource`, `DefeatTreeSpirit`, `ReachCivilization`, `Completed`).
  2. Create `QuestManager` registered in `ServiceLocator`:
     - Tracks current step.
     - Advances upon `ItemCollectedEvent` (Knife acquired $\to$ Step 1).
     - Advances upon `QuestTriggerVolumeEvent` (Entered spring clearing $\to$ Step 2).
     - Advances upon `BossDefeatedEvent` (Tree Spirit slain $\to$ Step 3).
     - Advances upon `ExitTrigger` reach $\to$ Step 4 / Completed.
  3. Create `QuestHUD` displaying current objective banner in top-left.
- **Acceptance Criteria**:
  - Quest prompts update sequentially as objectives are fulfilled.
  - Objective cannot be skipped out of order.

---

#### `TICK-08`: Vertical Slice Scene Integration & Playtest
- **Priority**: P1
- **Dependencies**: `TICK-01` through `TICK-07`
- **Affected Files**:
  - [`Assets/Scenes/SampleScene.unity`](file:///D:/Development/Game%20Development/3D%202D%20World/Assets/Scenes/SampleScene.unity)
  - [`Assets/Scripts/Gameplay/World/LevelLayoutRenderer.cs`](file:///D:/Development/Game%20Development/3D%202D%20World/Assets/Scripts/Gameplay/World/LevelLayoutRenderer.cs)
- **Specification**:
  1. Configure fixed seed layout: Crash Site (Start Room) $\to$ Woodland Clearing $\to$ Ancient Pond (Boss Room) $\to$ Mountain Trail (Exit Room).
  2. Place cargo crates with survival supplies at start room.
  3. Verify complete playthrough from awakening to exit trail.
- **Acceptance Criteria**:
  - Player completes quest slice from start to finish without console errors or game-halting bugs.
  - Level-up fires upon defeating boss, granting perk point.

---

#### `TICK-09`: Persistence & Save/Load Hardening
- **Priority**: P1
- **Dependencies**: `TICK-08`
- **Affected Files**:
  - [`Assets/Scripts/Systems/Persistence/FileSaveService.cs`](file:///D:/Development/Game%20Development/3D%202D%20World/Assets/Scripts/Systems/Persistence/FileSaveService.cs)
  - [`Assets/Scripts/Domain/Persistence/SaveData.cs`](file:///D:/Development/Game%20Development/3D%202D%20World/Assets/Scripts/Domain/Persistence/SaveData.cs)
  - [`Assets/Scripts/Systems/LevelGeneration/LevelGenerationService.cs`](file:///D:/Development/Game%20Development/3D%202D%20World/Assets/Scripts/Systems/LevelGeneration/LevelGenerationService.cs)
  - [`Assets/Scripts/Gameplay/World/LevelLayoutRenderer.cs`](file:///D:/Development/Game%20Development/3D%202D%20World/Assets/Scripts/Gameplay/World/LevelLayoutRenderer.cs)
- **Specification**:
  1. Add `QuestSaveData` and `InventorySaveData` to `LevelSaveData`.
  2. In `LevelGenerationService`, decouple save loading: introduce explicit boolean `loadSavedGame`; do not automatically trap fresh generation requests.
  3. In `LevelLayoutRenderer.OnGameLoaded`, respawn `RoomTrackerTrigger`s so area scheduling continues functioning after loading.
  4. Serialize active enemy states (RoomId, Health, Position) in `LevelSaveData.Entities`.
- **Acceptance Criteria**:
  - Save mid-quest (F5) $\to$ restart game $\to$ load game $\to$ player position, health, inventory, and active quest step are completely restored.

---

## 4. Content Validation Strategy

To prevent runtime null-reference exceptions and corrupted asset references, every content type must conform to these validation rules:

| Content Type | Required Fields / Constraints | Validation Mechanism |
|---|---|---|
| **ActorData** | `Id` must start with `ACT_`. `DisplayName` non-empty. `StartingAttributes` must contain at least `Health` ($> 0$). | `OnValidate()` in `ActorData.cs` flags missing attributes in Editor. |
| **ItemData** | `Id` must start with `ITM_`. `DisplayName` non-empty. `Weight` $\ge 0$. `MaxStackSize` $\ge 1$. | `OnValidate()` in `ItemData.cs`. |
| **BlockDefinition** | `RuntimeId` must be non-zero. If `SolidMesh`, `BlockMaterial` must be assigned. `Roles` list must not be empty. | `BlockRegistry.Initialize()` logs errors for invalid blocks on load. |
| **RoomArchetype** | `ElevationStepHeight` $\ge 0$. If `HasPillars`, `PillarThickness` $\ge 1$. | Asset inspection test in EditMode. |
| **Scene Prefabs** | Prefabs must have layer set correctly. No missing script references (`m_Script: {fileID: 0}`). | Automated Editor test scanning all prefabs in `Assets/Prefabs/`. |
| **QuestStep** | Each step must define a valid objective string, target criteria, and next step pointer. | `QuestManager.ValidateStepGraph()` on startup. |
