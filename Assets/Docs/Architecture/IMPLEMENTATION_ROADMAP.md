# FANTASY ENGINE — INCREMENTAL IMPLEMENTATION ROADMAP
**Document ID**: `ARCH-11`  
**Role**: Principal Engine Architect & Migration Planner  
**Purpose**: Step-by-step, compile-safe implementation roadmap divided into 6 independently verifiable milestones. Every milestone leaves `RPGProvingGrounds.unity` compilable and playable and includes explicit rollback procedures.

---

## Milestone Overview & Architectural Progression

| Milestone | Title | Primary Architectural Layers | Breaking Changes to Existing Scene? | Key Outcome |
|---|---|---|---|---|
| **Milestone 1** | **Core Identifiers, `DefinitionRef<T>`, & Unified `ContentRegistry<T>`** | Layer A (`Engine.Core`) | **Zero** (Additive + Bug Fixes) | `ResourceId`, `InstanceGuid`, `ContentRegistry<T>`, `LegacyDatabaseBridge`, and `BlockDefinition.OnValidate` fix. |
| **Milestone 2** | **Two-Phase `ServiceLocator`, Hardened Events, & `IEngineModule` Host** | Layer A (`Engine.Core`) | **Zero** (Transitional Wrappers) | Deterministic boot lifecycle; fixes `GreedyMeshingService` / `BlockPlacementService` initialization bug; isolates `GameBootstrapper`. |
| **Milestone 3** | **`IAssetProvider` & Fantasy Framework Registry Migration** | Layer A & Layer B (`Engine.Framework`) | **Zero** (Dual-Mode Enum Adapters) | `SkillProgressionService`, `FactionService`, `RadiantQuestService`, and `HotbarService` run on `ResourceId` and `ContentRegistry<T>` without `#if UNITY_EDITOR` hacks. |
| **Milestone 4** | **Modular Persistence (`SaveEnvelope` & `.vxlbin` Binary Sidecar)** | Layer A & Layer B | **Zero** (Upgrades Save Pipeline) | Saving/loading works in `RPGProvingGrounds.unity` and voxel scenes alike; decouples save logic from `LevelLayoutRenderer`; benchmarks `.vxlbin` codecs (`ADR-009`). |
| **Milestone 5** | **In-Game Editor Deepening, Synty Catalog Migration & `Cowsins` Archival** | Layer A (`core:editor_studio`) | **Zero** (Replaces Catalog Prefabs) | `InstanceGuid` on placed objects, Undo/Redo `ICommandHistory`, `CrossObjectLink` wiring, Playtest state isolation, Synty catalog replacement, and safe archival of `Cowsins`/`SoftKitty` (`ADR-008`). |
| **Milestone 6** | **Mod Loader Pipeline (`mod.json`, MergePatch, Profiles & Optional `glTFast`)** | Layer A & Layer C (`Mods`) | **Zero** | External mods (`StreamingAssets/ContentPacks/` & `PersistentDataPath/Mods/`), DAG load order, JSON Merge Patching, and staged `glTFast` support (`ADR-007`). |

---

## Milestone 1: Core Identifiers, `DefinitionRef<T>`, & Unified `ContentRegistry<T>`

### 1. Objective
Introduce the foundational identity primitives (`ResourceId`, `InstanceGuid`, `DefinitionRef<T>`) and the unified generic `ContentRegistry<T>` (`IContentService`), bridge existing `Databases` / `EntityRegistry` / `BlockRegistry` into `IContentService` without breaking a single existing script, and fix the `BlockDefinition.OnValidate()` shadowing bug and `HotbarService` item-to-block coupling.

### 2. Prerequisites
- None (First milestone).

### 3. Required Changes & Implementation Order
1. **Create Core Identity Primitives (`Assets/Scripts/Core/Ids/`)**:
   - `ResourceId.cs`: Serializable struct (`[SerializeField] private string _value;`) enforcing lowercase `namespace:path`, precomputing 32-bit FNV-1a hash, implementing `IEquatable<ResourceId>`, `IComparable<ResourceId>`.
   - `InstanceGuid.cs`: Serializable 128-bit UUID struct for scene/save object instances.
   - `DefinitionRef.cs`: Generic serializable `DefinitionRef<T> where T : class, IContentDefinition` with cached runtime reference resolution.
   - `NewtonsoftConverters.cs`: Custom `JsonConverter`s for `ResourceId`, `InstanceGuid`, `Vector3`, `Quaternion`, and `Color` (`ADR-003`).
2. **Create `IContentDefinition`, `ContentRegistry<T>`, and `ContentService` (`Assets/Scripts/Core/Content/`)**:
   - Implement `IContentDefinition` (`ResourceId ContentId { get; }`, `string DisplayName { get; }`).
   - Implement `ContentRegistry<T>` with O(1) `ResourceId` lookup, legacy string alias index (`"ACT_0001"`), secondary index registration, and `Freeze()` / `Unfreeze()`.
   - Implement `ContentService : IContentService` (`IGameService`).
3. **Update `EntityData`, `ItemData`, and `BlockDefinition` (Non-Breaking)**:
   - In [`EntityData.cs`](file:///d:/Development/Game%20Development/3D%202D%20World/Assets/Data/Config/EntityData.cs): Change `private void OnValidate()` to `protected virtual void OnValidate()`, implement `IContentDefinition`, and expose `ResourceId ContentId` (auto-synthesizing `nordic_rpg:<type>/<id>` if `_resourceId` is unset).
   - In [`BlockDefinition.cs`](file:///d:/Development/Game%20Development/3D%202D%20World/Assets/Scripts/Domain/World/BlockDefinition.cs): Change `private void OnValidate()` to `protected override void OnValidate()` and call `base.OnValidate()`.
   - In [`ItemData.cs`](file:///d:/Development/Game%20Development/3D%202D%20World/Assets/Data/Config/ItemData.cs): Add `[SerializeField] private DefinitionRef<BlockDefinition> _placeableBlock;` and update [`HotbarService.ResolveBlock()`](file:///d:/Development/Game%20Development/3D%202D%20World/Assets/Scripts/Systems/HUD/HotbarService.cs#L177-L186) to check `item.PlaceableBlock` first before falling back to legacy `DisplayName` matching.
4. **Implement `LegacyDatabaseBridge` (`Assets/Scripts/Core/Content/LegacyDatabaseBridge.cs`)**:
   - Populates `IContentService` registries from `Databases`, `EntityRegistry`, `BlockRegistry`, and `CreatorCatalogSO` during startup.

### 4. Files & Systems Affected
- **New Files**: `Assets/Scripts/Core/Ids/ResourceId.cs`, `InstanceGuid.cs`, `DefinitionRef.cs`, `Assets/Scripts/Core/Content/IContentDefinition.cs`, `ContentRegistry.cs`, `ContentService.cs`, `LegacyDatabaseBridge.cs`.
- **Modified Files**: `EntityData.cs`, `ItemData.cs`, `BlockDefinition.cs`, `HotbarService.cs`, `GameBootstrapper.cs` (registers `ContentService` and runs `LegacyDatabaseBridge`).

### 5. Acceptance Criteria
- `ResourceId.Parse("nordic_rpg:item/iron_sword")` succeeds; invalid strings throw `FormatException`.
- All existing `.asset` files in `Databases`, `EntityRegistry`, and `BlockRegistry` are queryable via `ServiceLocator.Get<IContentService>().GetRegistry<T>()` by both `ResourceId` and legacy string ID.
- `BlockDefinition.OnValidate()` properly invokes `EntityData.OnValidate()`.

### 6. Automated Tests & Manual Unity Verification
- **EditMode Tests**: `ResourceIdTests`, `InstanceGuidTests`, `ContentRegistryTests`, `LegacyDatabaseBridgeTests`.
- **Manual Verification**: Open `RPGProvingGrounds.unity`, press Play, verify zero Console errors/warnings, and verify hotbar item selection and crafting still work identically.

### 7. Regression Risks & Rollback Strategy
- **Risk**: Serialization of `EntityData` ScriptableObjects if existing `_id` field is renamed.
- **Mitigation & Rollback**: Keep `[SerializeField] private string _id;` intact on `EntityData` and add `[SerializeField] private ResourceId _contentId;` alongside it. Rollback is a clean `git checkout` of the 5 modified files and deletion of `Assets/Scripts/Core/Ids/` and `Assets/Scripts/Core/Content/`.

---

## Milestone 2: Two-Phase `ServiceLocator`, Hardened Events, & `IEngineModule` Host

### 1. Objective
Upgrade `ServiceLocator` to a two-phase lifecycle (`RegisterServices` -> `InitializeAsync`), harden `GameEventManager` with exception isolation and scoped subscriptions, introduce `TickScheduler` (`TickGroup`), and refactor `GameBootstrapper` to boot via `ModuleHost` and `IEngineModule` wrappers—fixing the `GreedyMeshingService` / `BlockPlacementService` initialization bug in the process.

### 2. Prerequisites
- **Milestone 1** completed and green.

### 3. Required Changes & Implementation Order
1. **Upgrade `ServiceLocator` (`Assets/Scripts/Core/Services/ServiceLocator.cs`)**:
   - Add `IServiceRegistry` and `IServiceProvider` interfaces.
   - Add `Register<TInterface, TImpl>(TImpl service)` and two-phase registration state (`BeginRegistrationPhase()`, `CompleteRegistrationAndInitialize()`).
   - Keep `ServiceLocator.Register<T>(T)` and `ServiceLocator.Get<T>()` working so existing callers require zero syntax changes.
2. **Harden `GameEventManager` (`Assets/Scripts/Core/Events/GameEventManager.cs`)**:
   - Replace `Delegate.Combine` with snapshot-safe list iteration and per-handler `try/catch` error isolation (`IDiagnosticService`).
   - Add `IEventScope` for automatic module/mod unsubscription.
3. **Create `IEngineModule`, `ModuleHost`, and `TickScheduler` (`Assets/Scripts/Core/Modules/`)**:
   - Implement `IEngineModule`, `ModuleDescriptor`, `IModuleContext`, `TickGroup` (`Input`, `Simulation`, `AI`, `WorldStreaming`, `UI`, `LateCleanup`), and Kahn's topological sort in `ModuleHost`.
   - Ensure synchronous completion of Phase 1 & Phase 2 inside `GameBootstrapper.Awake()` for built-in modules so scene `MonoBehaviour.Start()` methods in `RPGProvingGrounds.unity` never run against uninitialized services.
4. **Wrap Existing Services into Built-In `IEngineModule` Implementations**:
   - `CoreInfrastructureModule` (`PoolingManager`, `AreaSchedulerService`, `AudioService`, `ContentService`, `FileSaveService`).
   - `EditorStudioModule` (`CreatorStudioService`).
   - `FantasyEntitiesModule`, `FantasyInventoryModule` (`CraftingService`, `HotbarService`), `FantasyFactionsModule` (`FactionService`), `FantasyProgressionModule` (`SkillProgressionService`), `FantasyCombatStealthModule` (`StealthDetectionService`), `FantasyQuestsNavModule` (`RadiantQuestService`, `CompassService`), `VoxelDungeonModule` (`LevelGenerationService`, `GreedyMeshingService`, `BlockPlacementService`), `ShowcaseModule` (`LabService`, `CuratorService`).
5. **Fix `GreedyMeshingService` Registration**:
   - Register and initialize `GreedyMeshingService` inside `VoxelDungeonModule` (with `DefaultBlockRegistry`) so `BlockPlacementService.Initialize()` resolves it cleanly in Phase 2. Remove the duplicate `ServiceLocator.Register( _meshingService )` from `LevelLayoutRenderer.Awake()`.

### 4. Files & Systems Affected
- **New Files**: `Assets/Scripts/Core/Modules/IEngineModule.cs`, `ModuleDescriptor.cs`, `ModuleHost.cs`, `TickScheduler.cs`, `EngineBootstrapProfile.cs`, and module wrapper classes.
- **Modified Files**: `ServiceLocator.cs`, `GameEventManager.cs`, `GameBootstrapper.cs`, `GreedyMeshingService.cs`, `LevelLayoutRenderer.cs`.

### 5. Acceptance Criteria
- `GameBootstrapper.cs` contains zero `using Systems.Skyrim;` imports.
- `BlockPlacementService.Initialize()` resolves `GreedyMeshingService` on its first attempt during Phase 2 boot (verified by unit test).
- Throwing an exception inside a test subscriber in `GameEventManager` logs an error via `IDiagnosticService` without preventing subsequent subscribers from receiving the event.
- Engine Core boots cleanly in an EditMode/PlayMode test with only `CoreInfrastructureModule` and `EditorStudioModule` enabled (zero Fantasy Framework modules loaded).

### 6. Automated Tests & Manual Unity Verification
- **EditMode Tests**: `ModuleHostTopologicalSortTests` (cycle detection, missing dependency detection, deterministic boot order), `ServiceLocatorTwoPhaseTests`, `GameEventManagerIsolationTests`, `EngineCoreIndependenceTests`.
- **Manual Verification**: Play `RPGProvingGrounds.unity`; verify all 6 zones (Combat, Stealth, Forge, Dungeon interactables, Bounty Board, Creator Studio) function identically.

### 7. Regression Risks & Rollback Strategy
- **Risk**: A service calling `ServiceLocator.Get<T>()` inside its constructor during Phase 1.
- **Mitigation & Rollback**: Audited all 17 services—none call `ServiceLocator.Get<T>()` in their constructors (all lookups happen in `Initialize()`). If any issue arises, `GameBootstrapper` can toggle a `[SerializeField] private bool _useLegacyBoot = false;` flag to fall back immediately.

---

## Milestone 3: `IAssetProvider` & Fantasy Framework Registry Migration

### 1. Objective
Introduce `IAssetProvider` (`FallbackAssetCatalog`) to eliminate `#if UNITY_EDITOR AssetDatabase` runtime calls, migrate `SkillProgressionService`, `FactionService`, and `RadiantQuestService` to load data-driven definitions from `ContentRegistry<T>` keyed by `ResourceId`, and fix `BaseActor` and `StealthDetectionService` coupling/performance issues.

### 2. Prerequisites
- **Milestone 1** and **Milestone 2** completed and green.

### 3. Required Changes & Implementation Order
1. **Implement `IAssetProvider` and `FallbackAssetCatalog` (`Assets/Scripts/Core/Assets/`)**:
   - Create `AssetProviderService : IAssetProvider` supporting built-in registry assets, `Resources` fallback, Unity `AssetBundle` mounts, and external `.png`/`.ogg` disk loaders (`ADR-004`, `ADR-007`).
   - Create `FallbackAssetCatalog` providing guaranteed non-null fallback mesh (magenta/black checker cube), sprite, and audio clip on missing asset IDs.
2. **Migrate `SkillProgressionService` to `ContentRegistry<SkillDefinition>`**:
   - Create `SkillDefinition` & `ProgressionRuleDefinition` (`IContentDefinition`).
   - Export the 18 default Nordic RPG skills into `StreamingAssets/ContentPacks/nordic_rpg/Skills/skills_default.json` (and/or seed via `NordicRpgContentInitializer` if `StreamingAssets` file is absent in unit tests).
   - Refactor `SkillProgressionService` internal dictionary to `Dictionary<ResourceId, SkillInstance>`; retain `GetSkill(SkyrimSkill)` and `AddSkillXP(SkyrimSkill, float)` as `[Obsolete]` adapter methods mapping `SkyrimSkill.OneHanded -> new ResourceId("nordic_rpg:skill/one_handed")`.
3. **Migrate `FactionService` to `ContentRegistry<FactionDefinition>`**:
   - Remove `#if UNITY_EDITOR AssetDatabase.FindAssets("t:FactionDefinitionSO")` from [`FactionService.cs`](file:///d:/Development/Game%20Development/3D%202D%20World/Assets/Scripts/Systems/Skyrim/FactionService.cs#L44-L57).
   - Load `FactionDefinition`s from `ContentRegistry<FactionDefinition>` (`StreamingAssets/ContentPacks/nordic_rpg/Factions/factions_default.json` + registered SOs).
   - Key standings and bounties by `ResourceId` while keeping `FactionType` enum overloads working.
4. **Migrate `RadiantQuestService` to `ContentRegistry<QuestTemplateDefinition>`**:
   - Replace hardcoded `TargetLocations` and `TargetEnemies` string arrays in [`RadiantQuestService.cs`](file:///d:/Development/Game%20Development/3D%202D%20World/Assets/Scripts/Systems/Skyrim/RadiantQuestService.cs#L30-L40) with `QuestTemplateDefinition`s from `ContentRegistry<QuestTemplateDefinition>`.
5. **Decouple `BaseActor` and Optimize `StealthDetectionService`**:
   - In [`BaseActor.cs`](file:///d:/Development/Game%20Development/3D%202D%20World/Assets/Scripts/Gameplay/Entities/BaseActor.cs#L159-L171): Remove direct `ServiceLocator.TryGet<SkillProgressionService>` and `AssetDatabase.LoadAssetAtPath<ItemData>`; publish `WeaponAttackHitEvent` and resolve items from `ContentRegistry<ItemData>`.
   - In [`StealthDetectionService.cs`](file:///d:/Development/Game%20Development/3D%202D%20World/Assets/Scripts/Systems/Skyrim/StealthDetectionService.cs#L120-L125): Replace per-frame `GameObject.FindGameObjectsWithTag("Enemy")` with an event-maintained `HashSet<BaseActor>` of active actors and run LOS raycasts on `TickGroup.AI` (10 Hz).

### 4. Files & Systems Affected
- **New Files**: `Assets/Scripts/Core/Assets/IAssetProvider.cs`, `AssetProviderService.cs`, `FallbackAssetCatalog.cs`, `Assets/StreamingAssets/ContentPacks/nordic_rpg/pack_manifest.json`, `Skills/skills_default.json`, `Factions/factions_default.json`, `Quests/quests_default.json`.
- **Modified Files**: `SkillData.cs`, `SkillProgressionService.cs`, `FactionData.cs`, `FactionService.cs`, `QuestData.cs`, `RadiantQuestService.cs`, `BaseActor.cs`, `StealthDetectionService.cs`.

### 5. Acceptance Criteria
- Zero occurrences of `#if UNITY_EDITOR` inside `FactionService.cs` or `BaseActor.cs`.
- Registering a 19th custom skill (`test_mod:skill/chronomancy`) or a custom faction (`test_mod:faction/silver_hand`) in `ContentRegistry<T>` allows XP gain, level-ups, reputation changes, and radiant quest generation without modifying any C# enum.
- `StealthDetectionService.Tick()` allocates `0 B` of GC memory per frame.

### 6. Automated Tests & Manual Unity Verification
- **EditMode Tests**: `AssetProviderFallbackTests`, `CustomSkillProgressionTests` (verifies a 19th non-enum skill levels up the player), `CustomFactionHostilityTests`, `DataDrivenRadiantQuestTests`.
- **Manual Verification**: Play `RPGProvingGrounds.unity`; hit combat dummies (verify One-Handed XP & damage numbers), crouch in Stealth Courtyard (verify detection meter), accept and complete a bounty at the Bounty Board.

### 7. Regression Risks & Rollback Strategy
- **Risk**: Existing HUD or interactable scripts passing `SkyrimSkill` or `FactionType` enums.
- **Mitigation & Rollback**: All enum overloads (`GetSkill(SkyrimSkill)`, `ModifyReputation(FactionType, int)`) are preserved and mapped via a static lookup table `NordicRpgLegacyIds.FromEnum(...)`.

---

## Milestone 4: Modular Persistence (`SaveEnvelope` & `.vxlbin` Binary Sidecar)

### 1. Objective
Upgrade `FileSaveService` to orchestrate modular save participants (`ISaveableService`, `ISaveableComponent`, `PersistentEntityComponent`) into a versioned `SaveEnvelope` JSON file paired with a `.vxlbin` binary voxel sidecar (`ADR-009`), and decouple save/load orchestration from `LevelLayoutRenderer` so saving and loading works in `RPGProvingGrounds.unity` as well as procedural voxel dungeons.

### 2. Prerequisites
- **Milestones 1–3** completed and green.

### 3. Required Changes & Implementation Order
1. **Implement Binary Voxel Sidecar Codec (`Assets/Scripts/Core/Persistence/VoxelPayloadCodec.cs`)**:
   - Implement the 24-byte `"FXVL"` binary header, IEEE CRC-32 checksum, and codecs (`CodecId = 0` Raw, `CodecId = 1` RLE16, `CodecId = 2` RLE16 + `DeflateStream`) per `ADR-009`.
   - Implement `VoxelPaletteMap` (`ushort <-> ResourceId`) serialization.
2. **Implement `SaveEnvelope`, `ISaveableService`, `ISaveableComponent`, and `PersistentEntityComponent`**:
   - Create `SaveEnvelope`, `SaveHeader` (recording active `ModDependencyRecord`s), and `EntitySaveRecord` (`InstanceGuid`, `DefinitionId`, `ComponentStates`).
   - Add `PersistentEntityComponent` (`InstanceGuid`) to `BaseEntity` (`BaseActor`, `ChestInteractable`, `LeverInteractable`).
3. **Implement `ISaveableService` Across Framework Services**:
   - `SkillProgressionService` (saves skill XP, levels, perk points, and unlocked perks).
   - `FactionService` (saves reputation standings and crime bounties).
   - `RadiantQuestService` (saves active/completed quests and objective counts).
   - `VoxelWorldSaveParticipant` (saves room layout metadata in `SaveEnvelope` and writes voxel buffers to `slot_XX_voxels.vxlbin`).
4. **Implement `ISaveableComponent` on World Entities**:
   - `BaseActor` / `AttributeComponent` / `InventoryComponent` (health, stamina, mana, transform, inventory items by `ResourceId`).
   - `ChestInteractable` (`_isLocked`, `_isOpen`, container inventory) and `LeverInteractable` (`_isActivated`).
5. **Move `GameSaveRequestedEvent` / `GameLoadRequestedEvent` Handling to `FileSaveService` & Clean Up `LevelLayoutRenderer`**:
   - `FileSaveService` subscribes to `GameSaveRequestedEvent` and `GameLoadRequestedEvent`, writes atomically via `.tmp` -> `.bak` -> `.json`, and handles missing mod definitions gracefully via `UnknownContentPlaceholder` instead of throwing null-reference exceptions.
   - Remove player/inventory save/load code from `LevelLayoutRenderer.cs`.

### 4. Files & Systems Affected
- **New Files**: `Assets/Scripts/Core/Persistence/SaveEnvelope.cs`, `ISaveableService.cs`, `ISaveableComponent.cs`, `PersistentEntityComponent.cs`, `VoxelPayloadCodec.cs`, `VoxelWorldSaveParticipant.cs`.
- **Modified Files**: `FileSaveService.cs`, `SaveData.cs`, `LevelLayoutRenderer.cs`, `SkillProgressionService.cs`, `FactionService.cs`, `RadiantQuestService.cs`, `BaseEntity.cs`, `BaseActor.cs`, `ChestInteractable.cs`, `LeverInteractable.cs`.

### 5. Acceptance Criteria
- Triggering `GameSaveRequestedEvent` inside `RPGProvingGrounds.unity` writes a valid `SaveEnvelope` JSON file containing Player health, inventory, skill levels, unlocked perks, faction reputation, active quests, and opened/unlocked chest states, and `GameLoadRequestedEvent` restores all of them accurately.
- Voxel dungeon saves write metadata to `.json` and voxel grids to `.vxlbin` with verified CRC-32 checksums and `< 5 ms` encode/decode times.
- Loading a save file that references a removed mod item (`removed_mod:item/magic_ring`) preserves the unresolvable JSON token in `UnknownContentPlaceholders` and logs a warning without crashing.

### 6. Automated Tests & Manual Unity Verification
- **EditMode Tests**: `VoxelPayloadCodecBenchmarkTests` (compares Raw, RLE16, and RLE16+Deflate across 20-room dungeons and noisy chunks), `SaveEnvelopeRoundTripTests`, `MissingModSaveResilienceTests`.
- **Manual Verification**: In `RPGProvingGrounds.unity`, gain One-Handed XP, unlock a chest, pull a lever, accept a bounty, save the game, reset state, load the game, and verify all state is restored.

### 7. Regression Risks & Rollback Strategy
- **Risk**: Existing old `savegame.json` files in `Application.persistentDataPath` failing to parse under the new schema.
- **Mitigation & Rollback**: `FileSaveService` inspects the root JSON object for `"schemaVersion"`; if absent, it either migrates the v0 `LevelSaveData` or logs a clean version notice without throwing an unhandled exception.

---

## Milestone 5: In-Game Editor Deepening, Synty Catalog Migration & `Cowsins` Archival

### 1. Objective
Upgrade the In-Game Editor (`CreatorStudioService`, `CreatorPlacementController`, `CreatorSaveData`) with persistent `InstanceGuid`s, Undo/Redo command history (`ICommandHistory`), cross-object trigger linking (`CrossObjectLink` for levers/plates/doors), and snapshot-isolated Playtesting (`EditorPlaytestCoordinator`). Migrate `CreatorStudioSetup.cs` and `DefaultCreatorCatalog.asset` from `Assets/Cowsins/` prefabs to Synty `POLYGON Fantasy Kingdom` prefabs, verify zero remaining references, and execute the conditional archival of `Cowsins` and `SoftKitty` (`ADR-008`).

### 2. Prerequisites
- **Milestones 1–4** completed and green.

### 3. Required Changes & Implementation Order
1. **Upgrade `CreatorSaveData` to `WorldAuthoringData` v2 (`Assets/Scripts/Systems/Creator/CreatorSaveData.cs`)**:
   - Add `InstanceGuid InstanceId`, `ResourceId DefinitionId`, `Dictionary<string, JToken> PropertyOverrides`, and `List<CrossObjectLink> Links` to `PlacedObjectRecord`.
   - Maintain a v1 migration reader so any existing `CreatorLevels/*.json` files load seamlessly.
2. **Add `ICommandHistory` (Undo/Redo) and `EditorPlaytestCoordinator` to `CreatorStudioService`**:
   - Implement `PlaceObjectCommand`, `TransformObjectCommand`, `DeleteObjectCommand`, `ModifyPropertyCommand`, and `LinkObjectCommand`.
   - Implement `EditorPlaytestCoordinator`: when transitioning `CreatorState -> GameplayState`, serialize an in-memory snapshot of `WorldAuthoringData`; when returning `GameplayState -> CreatorState`, destroy runtime-mutated objects and restore the exact pre-playtest scene state.
3. **Upgrade `CreatorPlacementController` & Implement `ILinkReceiver` on Interactables**:
   - Attach `EditorPlacedEntityTag` (`InstanceGuid`) to placed objects; fix right-click deletion to use `EditorPlacedEntityTag.InstanceId` instead of `GetInstanceID()`.
   - Cache ghost preview material.
   - Implement `ILinkReceiver` on `LeverInteractable`, `PressurePlateInteractable`, and `ChestInteractable` so links authored in the In-Game Editor (`OnLeverPulled -> ToggleDoor`) wire up automatically when a level spawns.
4. **Migrate `CreatorStudioSetup.cs` & `DefaultCreatorCatalog.asset` to Synty `POLYGON Fantasy Kingdom` (`ADR-008`)**:
   - Replace the 12 `Assets/Cowsins/Prefabs/DragAndDropExtras/*.prefab` entries in [`CreatorStudioSetup.cs`](file:///d:/Development/Game%20Development/3D%202D%20World/Assets/Scripts/Editor/CreatorStudioSetup.cs#L57-L95) with authentic fantasy props, walls, doors, chests, and traps from `Assets/PolygonFantasyKingdom/Prefabs/`.
   - Re-run `CreatorStudioSetup.PopulateDefaultCatalog()` to update `Assets/Data/Creator/DefaultCreatorCatalog.asset`.
5. **Verify Zero GUID References & Archive `Cowsins` and `SoftKitty` (`ADR-008`)**:
   - Run an automated GUID scan across all `.unity`, `.prefab`, and `.asset` files confirming zero references to any GUID inside `Assets/Cowsins/` or `Assets/SoftKitty/`.
   - Move `Assets/Cowsins/` and `Assets/SoftKitty/` (+ `.meta` files and a `MANIFEST_AND_LICENSE.md`) to `D:\Development\Game Development\Downloads\ReferenceArchives\`.

### 4. Files & Systems Affected
- **New Files**: `Assets/Scripts/Systems/Creator/ICommandHistory.cs`, `EditorCommands.cs`, `EditorPlaytestCoordinator.cs`, `EditorPlacedEntityTag.cs`, `ILinkReceiver.cs`.
- **Modified Files**: `CreatorSaveData.cs`, `CreatorStudioService.cs`, `CreatorPlacementController.cs`, `CreatorStudioSetup.cs`, `DefaultCreatorCatalog.asset`, `LeverInteractable.cs`, `PressurePlateInteractable.cs`.
- **Archived Folders (After GUID Audit)**: `Assets/Cowsins/`, `Assets/SoftKitty/`.

### 5. Acceptance Criteria
- Placing 3 props, deleting 1 prop, pressing `Ctrl+Z` (Undo), and pressing `Ctrl+Y` (Redo) deterministically restores and re-applies the exact object and `InstanceGuid`.
- Entering Playtest in the In-Game Editor, opening a chest, killing an enemy, and exiting Playtest restores the unopened chest and live enemy with zero state leakage.
- Zero references to `Assets/Cowsins/` or `Assets/SoftKitty/` exist anywhere in the project, and archiving both folders saves ~383 MB with zero compiler or missing-prefab errors.

### 6. Automated Tests & Manual Unity Verification
- **EditMode & PlayMode Tests**: `EditorCommandHistoryUndoRedoTests`, `EditorPlaytestIsolationTests`, `CrossObjectLinkResolutionTests`, `CatalogAssetIntegrityTests`.
- **Manual Verification**: Enter Creator Mode (`Tab` or Creator Studio zone in `RPGProvingGrounds.unity`), place Synty fantasy structures, link a lever to a door, test Undo/Redo, enter Playtest, pull the lever, exit Playtest, and verify state restoration.

### 7. Regression Risks & Rollback Strategy
- **Risk**: Missing prefab GUID in `DefaultCreatorCatalog.asset` if a Synty path is mistyped.
- **Mitigation & Rollback**: `CatalogAssetIntegrityTests` verifies that every `CreatorItemDefinition` in `DefaultCreatorCatalog.asset` resolves a non-null prefab before `Assets/Cowsins/` is moved to the external archive.

---

## Milestone 6: Mod Loader Pipeline (`mod.json`, MergePatch, Profiles & Optional `glTFast`)

### 1. Objective
Implement `ModManagerService` (`IModManagerService`) to discover, validate, topologically sort, and mount external content packs and user mods from `StreamingAssets/ContentPacks/` and `Application.persistentDataPath/Mods/`, support RFC 7396 JSON `MergePatch` and `OverrideExisting` conflict resolution, provide In-Game Editor Mod Export, and add optional `glTFast` external 3D mesh loading behind `IAssetProvider` (`ADR-007`).

### 2. Prerequisites
- **Milestones 1–5** completed and green.

### 3. Required Changes & Implementation Order
1. **Implement `ModManifest` (`mod.json`) Parser & Topological Load-Order Solver (`Assets/Scripts/Core/Modding/`)**:
   - Parse `modId`, `version` (SemVer), `engineVersion`, `dependencies`, `loadBefore`, `loadAfter`, `conflictPolicy`, and `trustLevel`.
   - Validate dependencies, detect cycles, and emit structured `ModDiagnosticReport`s for missing dependencies or incompatible engine versions.
2. **Implement JSON Definition Loader & `MergePatch` Engine**:
   - Load JSON definitions (`Items/*.json`, `Skills/*.json`, `Factions/*.json`, `Quests/*.json`, `Recipes/*.json`, `Blocks/*.json`, `Placeables/*.json`).
   - Apply `AddOnly`, `MergePatch` (`JObject.Merge`), and `OverrideExisting` policies in load order, recording provenance (`SourceModId` -> `LastModifiedByModId`) in `ContentRegistry<T>`.
3. **Implement In-Game Editor Mod Exporter**:
   - Add `ExportModPackage(string modId, WorldAuthoringData world)` to `CreatorStudioService` so worlds and custom item/quest definitions created in-game export directly to `Application.persistentDataPath/Mods/<modId>/`.
4. **Optional Stage 2 Mesh Extension (`glTFast` behind `IAssetProvider`, per `ADR-007`)**:
   - Register `GltfAssetMountSource` inside `AssetProviderService` so `.glb`/`.gltf` files in a mod's `Assets/Meshes/` directory load transparently via `IAssetProvider.LoadAssetAsync<GameObject>(ResourceId)`.

### 4. Files & Systems Affected
- **New Files**: `Assets/Scripts/Core/Modding/ModManifest.cs`, `ModManagerService.cs`, `ModLoadOrderSolver.cs`, `JsonMergePatcher.cs`, `ModProfile.cs`, `GltfAssetMountSource.cs`.
- **Modified Files**: `ContentService.cs`, `AssetProviderService.cs`, `CreatorStudioService.cs`.

### 5. Acceptance Criteria
- A test mod in `Mods/test_expansion/` adding a new skill, a new faction, a new item, and a `MergePatch` tweaking `nordic_rpg:item/iron_sword`'s damage loads cleanly in topological order and records `"test_expansion"` as `LastModifiedByModId`.
- A broken mod with a syntax error in `Items/bad.json` or a missing dependency is skipped safely with a clear diagnostic report, without crashing the engine or preventing `RPGProvingGrounds.unity` from running.

### 6. Automated Tests & Manual Unity Verification
- **EditMode Tests**: `ModDependencySolverTests` (validates SemVer ranges, `loadAfter`, missing dependency rejection, cycle rejection), `ModMergePatchConflictTests` (two mods patching different fields of the same item), `MalformedModResilienceTests`.
- **Manual Verification**: Author a mini-dungeon in the In-Game Editor, export it as a mod pack, reload the mod profile, and play the exported mod.

### 7. Regression Risks & Rollback Strategy
- **Risk**: Malformed user mod JSON corrupting base `nordic_rpg` definitions.
- **Mitigation & Rollback**: `ModManagerService` clones base definitions before applying mod patches; switching to the `"Vanilla (No Mods)"` profile re-hydrates `ContentRegistry<T>` purely from `StreamingAssets/ContentPacks/nordic_rpg/`.
