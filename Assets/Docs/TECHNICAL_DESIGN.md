# Technical Design Document: Hybrid Procedural RPG Engine

**Project**: 3D 2D World  
**Engine**: Unity 6 (6000.3.8f1)  
**Render Pipeline**: Universal Render Pipeline (URP 17.3.0)  
**Compilation Target**: .NET 9 / C# 12  
**Status**: Architecture Blueprint  
**Last Updated**: October 2026  

---

## 1. Architectural Philosophy & Layer Model

The project strictly enforces a five-layer decoupled architecture:

```
┌────────────────────────────────────────────────────────┐
│                   INFRASTRUCTURE / DATA                │
│    • ScriptableObject Blueprints (ActorData, ItemData) │
│    • BlockRegistry & BlockDefinition Assets            │
│    • LevelRenderConfig & RoomArchetypes                │
└──────────────────────────┬─────────────────────────────┘
                           │
                           ▼
┌────────────────────────────────────────────────────────┐
│                          CORE                          │
│    • ServiceLocator (Dependency Injection)             │
│    • GameEventManager (Typed Generic Event Bus)        │
│    • GameBootstrapper (Singleton Lifecycle Driver)     │
│    • ITickable & IGameService Contracts                │
└──────────────────────────┬─────────────────────────────┘
                           │
                           ▼
┌────────────────────────────────────────────────────────┐
│                         DOMAIN                         │
│    • Pure C# State Machine (GameStateMachine)          │
│    • Procedural Math (RoomGraphGenerator)              │
│    • Grid Data (RoomGridData, RoomSocketData)          │
│    • Entity Schemas (BaseAttribute, ActorState)        │
└──────────────────────────┬─────────────────────────────┘
                           │
                           ▼
┌────────────────────────────────────────────────────────┐
│                        SYSTEMS                         │
│    • Meshing (GreedyMeshingService, GridMeshBuilder)   │
│    • Level Generation (LevelGenerationService)         │
│    • Scheduling (AreaSchedulerService)                 │
│    • Persistence (FileSaveService)                     │
│    • Questing (QuestManager)                           │
│    • Input (InputManager)                              │
└──────────────────────────┬─────────────────────────────┘
                           │
                           ▼
┌────────────────────────────────────────────────────────┐
│                        GAMEPLAY                        │
│    • Player Controllers (Movement, Look, CameraRig)    │
│    • Interaction (PlayerInteractionController)         │
│    • Combat (WeaponComponent, HitboxComponent)         │
│    • Entity Behaviors (SlimeEntity, TreeSpiritEntity)  │
│    • World Triggers (RoomTrackerTrigger, ExitTrigger)  │
│    • UI Presenters (HotbarHUD, WorldMapHUD, QuestHUD)  │
└────────────────────────────────────────────────────────┘
```

### Architectural Principles
- **Separation of Concerns**: Domain math and business logic do not depend on `UnityEngine.Object` or scene state. Views and presenters read state but do not execute gameplay rules.
- **Data-Driven Configuration**: ScriptableObjects represent immutable blueprints; runtime instances maintain mutable state. No hardcoded IDs or asset paths.
- **Event-Driven Communication**: Cross-subsystem messaging flows through `GameEventManager.Publish<T>` rather than tight singleton coupling.

---

## 2. Resolution of Verified Audit Findings

The architecture explicitly resolves the defects identified during the repository audit:

| Defect Identified in Audit | Root Cause | Architectural Resolution |
|---|---|---|
| **Double State Ticking** | `GameBootstrapper.Update` explicitly ticks `_stateMachine` AND registers it in `_tickables`. | Remove explicit tick on `_stateMachine`; rely exclusively on `_tickables` iteration. |
| **Missing Player Health** | `DebugActorData.asset` has an empty `StartingAttributes` list. | Create `JackActorData.asset` with properly initialized `Health` (100) and `Stamina` (100) attributes. |
| **Empty Attribute Regen** | `AttributeComponent.Update()` computes `restored` but leaves application block empty. | Implement fractional accumulator: accumulate `float delta`, apply integer steps to `CurrentValue`, and clamp to `MaximumValue`. |
| **Interaction Ignores Entities** | `PlayerGridInteractor` only targets `GridRoomLink` on layer mask 1. | Decouple mining/building from interaction. Create `PlayerInteractionController` targeting `IInteractable` (items, NPCs, crates) and `IDamageable` (enemies). |
| **Non-Attacking Enemies** | `SlimeEntity` resets timer when in attack range with no damage logic. | Implement attack state execution that calls `AttributeComponent.ApplyDamage` on Jack with audio/visual feedback. |
| **Save Traps New Generation** | `LevelGenerationService` blindly reloads saved game if save file exists. | Decouple `Continue` from `GenerateNew`. Only load save when explicitly requested or flag `LoadFromSave` is set; clean save on new game. |
| **Save Discards Entities** | `LevelSaveData` hardcodes `Entities = Array.Empty<EntitySaveData>()`. | Add dynamic entity serialization for all active/dormant actors with their room IDs, health, and transform. |
| **Save Discards Room Triggers** | `LevelLayoutRenderer.OnGameLoaded` rebuilds voxels but skips `RoomTrackerTrigger`. | Extract trigger instantiation into a shared helper invoked by both `BuildLayout` and `OnGameLoaded`. |
| **Scene Clutter in SampleScene** | Over 150 static test objects remain in `SampleScene.unity`. | Purge legacy static geometry; ensure only `GameBootstrapper`, `Level Generator`, `Actor`, and environment lighting exist. |

---

## 3. Subsystem Specifications

### 3.1 Hybrid World Generation Pipeline
The world generation system combines deterministic procedural voxel foundations with socket-based prefab decoration.

```
LevelGenerationParameters (Seed, Dimensions, GladeCount)
               │
               ▼
RoomGraphGenerator (Pure C# Graph + Biome / Elevation Layout)
               │ produces LevelLayout (Rooms, Sockets, Tiers, Connections)
               ▼
LevelGenerationService (IGameService save gate & coordination)
               │ LevelGenerationCompletedEvent
               ▼
LevelLayoutRenderer (MonoBehaviour Orchestrator)
  ├── 1. Role-based Block Resolution (BlockRegistry: Grass, Dirt, Stone, Water)
  ├── 2. Voxelization into RoomGridData (Floors, Sunken Ponds, Sloped Banks)
  ├── 3. VoxelStairBuilder (Stepped elevation changes, natural paths)
  ├── 4. Sockets Discovery (DiscoverRoomSockets: Chests, Spawns, Decorators)
  ├── 5. Prefab Decoration (Instantiate Trees, Rocks, Foliage at Sockets)
  ├── 6. GreedyMeshingService (Burst Job merging coplanar faces)
  └── 7. GridMeshBuilder (Bake Unity Mesh with submeshes and colliders)
```

#### Block Role Contract
Block selection is strictly data-driven via `BlockRegistry.GetBlocksByRole(BlockRole)`:
- `BlockRole.Floor`: Resolved via active `BlockPalette` (Dirt, Grass, Mossy Stone with weighted variants).
- `BlockRole.Wall`: Resolved for cliff faces and perimeter barriers.
- `BlockRole.Liquid`: Resolved for water sources and forest streams.
- `BlockRole.Accent`: Resolved for ancient flagstones and ruin remnants.

#### Socket Decorator System
`VoxelStairBuilder.DiscoverRoomSockets` identifies candidate coordinates with solid ground and overhead clearance. Sockets are typed via `SocketType`:
- `SocketType.PlayerSpawn`: Initial crash site origin.
- `SocketType.EnemySpawn`: Minion and boss spawn origins.
- `SocketType.Chest`: Supply crates and reward containers.
- `SocketType.PropLight` / `PropFoliage`: Conventional 3D mesh prefabs (ancient oaks, boulder clusters).

---

### 3.2 Interaction & Combat Architecture

#### Interaction System
```csharp
public interface IInteractable
{
    string InteractionPrompt { get; }
    bool CanInteract(GameObject user);
    void Interact(GameObject user);
}
```
- `PlayerInteractionController`: Fires center-screen raycast ($2.5\,\text{m}$).
- Detects any object implementing `IInteractable` (e.g., `BaseContainer`, `BaseItem`, `QuestTriggerVolume`).
- Displays UI prompt (e.g., *"[E] Open Cargo Crate"*).

#### Combat Pipeline
```csharp
public readonly struct DamageInfo
{
    public readonly int Amount;
    public readonly DamageType Type;
    public readonly GameObject Instigator;
    public readonly Vector3 HitPoint;
    public readonly Vector3 HitNormal;
}

public interface IDamageable
{
    void TakeDamage(DamageInfo damage);
}
```
- `WeaponComponent`: Attached to camera rig. When primary attack is triggered:
  1. Verifies player has sufficient Stamina ($20$).
  2. Drains Stamina via `AttributeComponent`.
  3. Spherecasts forward ($2.2\,\text{m}$ range, $0.5\,\text{m}$ radius).
  4. If target implements `IDamageable`, sends `DamageInfo`.
  5. Target's `AttributeComponent.ApplyDamage` executes, triggers `EntityDamagedEvent`, and checks for `ActorState.Death`.

---

### 3.3 Quest State Machine

```csharp
public enum QuestStep
{
    CrashSiteScavenge = 0,
    FindWaterSource = 1,
    DefeatTreeSpirit = 2,
    ReachCivilization = 3,
    Completed = 4
}
```
- `QuestManager` (`IGameService`):
  - Maintains `CurrentQuestStep`.
  - Subscribes to `ItemCollectedEvent`, `QuestTriggerVolumeEvent`, and `EntityStateChangedEvent`.
  - Publishes `QuestStepChangedEvent` to update `QuestHUD`.
  - Upon reaching `QuestStep.Completed`, grants progression XP via `StatsComponent.GainExperience`.

---

### 3.4 Persistence Specification

#### Schema: `LevelSaveData`
```csharp
[Serializable]
public class LevelSaveData
{
    public int SaveVersion = 1;
    public LevelLayout OriginalLayout;
    public RoomSaveData[] Grids;
    public EntitySaveData[] Entities;
    public EntitySaveData PlayerState;
    public QuestSaveData QuestProgress;
    public InventorySaveData PlayerInventory;
}
```

#### Safe Loading Routine:
1. `FileSaveService.ReadData()` loads JSON from persistent disk.
2. `LevelLayoutRenderer.OnGameLoaded`:
   - Reconstructs `RoomGridData` from `RoomSaveData.VoxelData`.
   - Dispatches meshing through `GreedyMeshingService`.
   - Spawns `RoomTrackerTrigger` instances for all rooms to restore `AreaSchedulerService`.
   - Spawns saved active entities and restores their health attributes.
3. `QuestManager.LoadProgress(saveData.QuestProgress)` restores active objective.
4. `PlayerSpawnService` repositions `Actor` and applies saved health/stamina/XP.
