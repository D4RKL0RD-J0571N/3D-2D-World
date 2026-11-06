# FANTASY ENGINE — ARCHITECTURE TEST & VERIFICATION STRATEGY
**Document ID**: `ARCH-12`  
**Role**: Principal Engine Architect & Verification Planner  
**Purpose**: Concrete EditMode and PlayMode NUnit test specifications (`com.unity.test-framework`), performance benchmarks, and failure-injection scenarios across all 7 architectural domains.

---

## 1. Test Suite Organization & Assembly Structure

Currently, the repository contains a single EditMode test file: [`Assets/Tests/Editor/CraftingServiceTests.cs`](file:///d:/Development/Game%20Development/3D%202D%20World/Assets/Tests/Editor/CraftingServiceTests.cs). To verify every milestone without waiting for manual scene playtests, the test suite will expand into 7 dedicated NUnit test fixtures under `Assets/Tests/Editor/` (fast synchronous EditMode tests, `< 2 seconds` total run time) and `Assets/Tests/PlayMode/` (scene and lifecycle integration tests):

```text
Assets/Tests/
├── Editor/
│   ├── CraftingServiceTests.cs                  (EXISTING — Preserved 100%)
│   ├── Domain1_EngineIndependenceTests.cs       (Milestone 2)
│   ├── Domain2_ModuleLifecycleTests.cs          (Milestone 2)
│   ├── Domain3_ContentRegistryTests.cs          (Milestone 1 & 3)
│   ├── Domain4_ModLoaderAndPatchTests.cs        (Milestone 6)
│   ├── Domain5_InGameEditorTests.cs             (Milestone 5)
│   └── Domain6_PersistenceAndVoxelCodecTests.cs (Milestone 4)
└── PlayMode/
    └── Domain7_ProvingGroundsRegressionTests.cs (Milestones 1–6)
```

---

## 2. Domain-by-Domain Test Specifications

### Domain 1: Engine Independence (`Domain1_EngineIndependenceTests.cs`)

**Goal**: Prove mathematically and at runtime that Layer A (`Engine.Core`) operates with zero compile-time or runtime dependency on Layer B (`Fantasy Framework`) or Layer C (`Nordic RPG Content`).

| Test ID | Test Name | Type | Setup & Execution | Expected Assertion |
|---|---|---|---|---|
| `ENG-01` | `BootEngine_WithoutFantasyFramework_Succeeds` | EditMode | Initialize `ModuleHost` with an `EngineBootstrapProfile` containing only `CoreInfrastructureModule` and `EditorStudioModule` (omitting all `Fantasy*` modules). | `ModuleHost.State == ModuleHostState.Active`; `ServiceLocator.TryGet<IContentService>(out _)` is `true`; `ServiceLocator.TryGet<SkillProgressionService>(out _)` is `false`; zero errors logged. |
| `ENG-02` | `CoreNamespaceScripts_HaveZeroImportsOfSkyrimOrGameplay` | EditMode (Static Reflection / AST Scan) | Scan all `.cs` files under `Assets/Scripts/Core/` after Milestone 2. | Assert zero matches for `using Systems.Skyrim;`, `using Domain.Skyrim;`, or `using Gameplay.Entities;` inside `Assets/Scripts/Core/`. |
| `ENG-03` | `GameEventManager_IsolatesThrowingSubscriber` | EditMode | Subscribe Handler A (throws `InvalidOperationException`) and Handler B (increments counter) to a test `GameEvent`. Call `GameEventManager.Publish(...)`. | Handler B is still invoked (`counter == 1`), and `IDiagnosticService` records 1 caught exception from Handler A. |

---

### Domain 2: Module Lifecycle & Two-Phase Service Locator (`Domain2_ModuleLifecycleTests.cs`)

**Goal**: Verify deterministic topological sorting, dependency validation, two-phase service resolution, and reverse-order disposal.

| Test ID | Test Name | Type | Setup & Execution | Expected Assertion |
|---|---|---|---|---|
| `MOD-01` | `ModuleHost_TopologicalSort_OrdersDependenciesDeterministically` | EditMode | Register Mock Modules `C` (depends on `B`), `A` (no deps), and `B` (depends on `A`) in scrambled order `[C, A, B]`. | Execution log matches exact order: `A.Register -> B.Register -> C.Register -> A.Init -> B.Init -> C.Init -> A.Activate -> B.Activate -> C.Activate`. |
| `MOD-02` | `ModuleHost_Shutdown_DisposesInReverseTopologicalOrder` | EditMode | Shut down the `ModuleHost` from `MOD-01`. | Shutdown log matches exact reverse order: `C.Shutdown -> B.Shutdown -> A.Shutdown`. |
| `MOD-03` | `ModuleHost_CircularDependency_ThrowsDiagnosticException` | EditMode | Register Module `X` (depends on `Y`) and Module `Y` (depends on `X`). | `ModuleHost.Boot()` throws `ModuleDependencyCycleException` naming `X -> Y -> X` before any service is initialized. |
| `MOD-04` | `ModuleHost_MissingRequiredDependency_FailsCleanly` | EditMode | Register Module `Quests` requiring `"framework:factions"`, without registering `FactionsModule`. | Throws `MissingModuleDependencyException("framework:quests_nav", "framework:factions")`. |
| `MOD-05` | `TwoPhaseServiceLocator_ResolvesCrossServiceInPhase2RegardlessOfRegistrationOrder` | EditMode | Register `BlockPlacementService` *before* `GreedyMeshingService` during Phase 1 (`BeginRegistrationPhase`), then invoke `CompleteRegistrationAndInitialize()`. | `BlockPlacementService` resolves `GreedyMeshingService` non-null during Phase 2 `Initialize()`. |

---

### Domain 3: Content & Registries (`Domain3_ContentRegistryTests.cs`)

**Goal**: Verify `ResourceId` parsing/hashing, `DefinitionRef<T>` type safety, `ContentRegistry<T>` O(1) lookup, legacy database bridging, and custom non-enum skills/factions.

| Test ID | Test Name | Type | Setup & Execution | Expected Assertion |
|---|---|---|---|---|
| `CNT-01` | `ResourceId_Validation_AcceptsValidAndRejectsMalformed` | EditMode | Test valid (`"nordic_rpg:item/iron_sword"`, `"core:block/stone_01"`) and invalid (`"IronSword"`, `"UPPER:case"`, `"missing_colon"`, `""`). | Valid strings parse with deterministic FNV-1a `Hash`; invalid strings return `false` on `TryParse` and throw `FormatException` on `Parse`. |
| `CNT-02` | `ContentRegistry_Freeze_PreventsLateMutationUntilUnfrozen` | EditMode | Register `ItemDefinition`, call `registry.Freeze()`, attempt `registry.Register(...)`. | Throws `RegistryFrozenException`. Calling `UnfreezeForReload()` allows registration again. |
| `CNT-03` | `LegacyDatabaseBridge_ImportsExistingScriptableObjectsWithAliases` | EditMode | Feed a mock `ItemData` (`_id = "ITM_0042"`, `name = "ElvenBow"`) into `LegacyDatabaseBridge`. | `registry.TryGet(new ResourceId("nordic_rpg:item/itm_0042"), out var def)` is `true` AND `registry.TryGetByLegacyAlias("ITM_0042", out def)` is `true`. |
| `CNT-04` | `CustomNonEnumSkill_GainsXPAndTriggersPlayerLevelUp` | EditMode | Register a 19th skill `new ResourceId("chronomancy_mod:skill/time_weaving")` in `ContentRegistry<SkillDefinition>`. Call `skillService.AddSkillXP(id, 5000f)`. | Custom skill increases in level, grants player perk points every 10 skill-ups, and fires `SkillLevelUpEvent`. |
| `CNT-05` | `BlockDefinition_OnValidate_InvokesBaseEntityDataValidation` | EditMode | Create an in-memory `BlockDefinition` via `ScriptableObject.CreateInstance<BlockDefinition>()` and invoke `OnValidate` via reflection. | Base `EntityData` validation executes without being shadowed. |

---

### Domain 4: Modding (`Domain4_ModLoaderAndPatchTests.cs`)

**Goal**: Verify `mod.json` parsing, SemVer compatibility, RFC 7396 `MergePatch` field overrides, `OverrideExisting`, and resilience against malformed mod files.

| Test ID | Test Name | Type | Setup & Execution | Expected Assertion |
|---|---|---|---|---|
| `MDP-01` | `ModMergePatch_TwoModsPatchingDifferentFieldsOfSameItem_PreservesBothChanges` | EditMode | Base item `nordic_rpg:item/iron_sword` has `damage: 12, weight: 9, value: 45`. Mod A (`MergePatch`) sets `damage: 18`. Mod B (`MergePatch`, `loadAfter: [Mod A]`) sets `value: 100`. | Final resolved `iron_sword` has `damage == 18`, `weight == 9`, `value == 100`, `SourceModId == "nordic_rpg"`, and `LastModifiedByModId == "mod_b"`. |
| `MDP-02` | `ModConflictPolicy_AddOnly_RejectsCollisionWithBaseDefinition` | EditMode | Mod C (`conflictPolicy: "AddOnly"`) attempts to define `nordic_rpg:item/iron_sword`. | Base `iron_sword` is untouched; `ModDiagnosticReport` logs a blocked collision warning for Mod C. |
| `MDP-03` | `MalformedJsonMod_IsSkippedCleanlyWithoutCrashingEngine` | EditMode | Mount a virtual mod folder containing `Items/broken.json` with truncated JSON syntax `{ "id": "bad:item/x", `. | `ModManagerService` catches `JsonReaderException`, logs file/line number in `ModDiagnosticReport`, skips `broken.json`, and completes boot. |
| `MDP-04` | `MissingAssetInMod_ReturnsFallbackAssetInsteadOfNull` | EditMode | Request `assetProvider.LoadAssetAsync<GameObject>(new ResourceId("broken_mod:mesh/non_existent"))`. | Returns `FallbackAssetCatalog.MissingMeshPlaceholder` (non-null) and logs 1 diagnostic warning. |

---

### Domain 5: In-Game Editor (`Domain5_InGameEditorTests.cs`)

**Goal**: Verify persistent `InstanceGuid` assignment, `ICommandHistory` Undo/Redo determinism, `CrossObjectLink` resolution, Playtest state isolation, and Synty catalog integrity.

| Test ID | Test Name | Type | Setup & Execution | Expected Assertion |
|---|---|---|---|---|
| `EDT-01` | `CommandHistory_PlaceTransformDelete_UndoAndRedoRestoresExactInstanceGuid` | EditMode | Execute `PlaceObjectCommand` (captures generated `InstanceGuid G`), `TransformObjectCommand` (moves to `(10, 0, 10)`), `DeleteObjectCommand`. Call `Undo()` 3 times, then `Redo()` 3 times. | After Undo(1), object exists at `(10, 0, 10)` with `InstanceId == G`. After Undo(2), object is back at `(0, 0, 0)` with `InstanceId == G`. |
| `EDT-02` | `EditorPlaytestCoordinator_MutatingWorldDuringPlaytest_RestoresAuthoredStateOnExit` | PlayMode | In Editor Mode, place a `ChestInteractable` (locked) and `BaseActor` (100 HP). Enter Playtest, unlock chest, deal 100 damage to kill actor. Exit Playtest. | Upon returning to Editor Mode, chest is locked again (`IsLocked == true`), actor is alive with `100 HP`, and `InstanceGuid`s are unchanged. |
| `EDT-03` | `CrossObjectLink_WiresLeverToTargetInteractableByInstanceGuid` | EditMode | Create `PlacedObjectRecord` A (`LeverInteractable`, `GuidA`) with `CrossObjectLink(Event: "OnLeverPulled", Target: GuidB, Action: "Activate")` and Record B (`GuidB`). Spawn both via `CreatorStudioService`. | Pulling Lever A invokes `ILinkReceiver.ExecuteLinkAction("Activate")` on Target B. |
| `EDT-04` | `DefaultCreatorCatalog_HasZeroMissingPrefabsAndZeroCowsinsReferences` | EditMode | Load `Assets/Data/Creator/DefaultCreatorCatalog.asset` after Milestone 5 migration. Iterate all `Items`. | Every `item.Prefab != null`, and `AssetDatabase.GetAssetPath(item.Prefab)` does **not** start with `"Assets/Cowsins/"` or `"Assets/SoftKitty/"`. |

---

### Domain 6: Persistence & Voxel Compression Benchmarks (`Domain6_PersistenceAndVoxelCodecTests.cs`)

**Goal**: Verify `SaveEnvelope` round-trip accuracy, `.vxlbin` binary codec correctness & performance (`ADR-009`), corrupt header detection, and missing-mod save loading.

| Test ID | Test Name | Type | Setup & Execution | Expected Assertion |
|---|---|---|---|---|
| `SAV-01` | `VoxelPayloadCodec_RoundTrip_BitIdenticalAcrossAllCodecs` | EditMode | Generate a 20-room dungeon (`92,160` `ushort` voxels) and a high-entropy random chunk. Encode and decode with `CodecId = 0` (`Raw`), `CodecId = 1` (`RLE16`), and `CodecId = 2` (`RLE16_Deflate`). | Decoded `ushort[]` arrays are 100% bit-for-bit identical to the input arrays (`CollectionAssert.AreEqual`); CRC-32 checksums match. |
| `SAV-02` | `VoxelPayloadCodec_Benchmark_MeetsSizeAndLatencyTargets` | EditMode | Measure byte size, encode time (`Stopwatch`), and decode time on a 20-room structured dungeon (`184.3 KB` raw). | `RLE16` and `RLE16_Deflate` reduce payload size by `>= 80%` (`< 36 KB`) vs raw binary and `>= 95%` vs `JsonUtility`, with decode time `< 5 ms`. |
| `SAV-03` | `VoxelPayloadCodec_CorruptedByte_ThrowsChecksumMismatchException` | EditMode | Encode `.vxlbin` buffer, flip 1 bit in the payload body, and call `Decode()`. | Throws `SavePayloadCorruptedException` (CRC-32 mismatch) instead of silently producing corrupted geometry or overflowing buffers. |
| `SAV-04` | `SaveEnvelope_FullRoundTrip_RestoresSkillsFactionsQuestsAndEntities` | EditMode | Populate `SkillProgressionService` (One-Handed Lv 25), `FactionService` (Companions +50 rep), `RadiantQuestService` (1 active bounty), and 1 `ChestInteractable` (unlocked). Save to memory/temp file, reset all services, Load. | All skill levels, XP, faction standings, active quests, and entity component states match pre-save state 100%. |
| `SAV-05` | `SaveEnvelope_LoadWithRemovedMod_PreservesUnknownPlaceholdersWithoutCrash` | EditMode | Create a `SaveEnvelope` containing an inventory item `"deleted_mod:item/dragon_halberd"` and an entity `"deleted_mod:actor/fire_wyrm"`. Unregister `"deleted_mod"` and load the save. | Load succeeds with `0` unhandled exceptions; unknown item/entity records are preserved inside `UnknownContentPlaceholders`; diagnostic warning is logged. |

---

### Domain 7: Regression Suite (`Domain7_ProvingGroundsRegressionTests.cs`)

**Goal**: Automated PlayMode smoke tests ensuring existing gameplay in `RPGProvingGrounds.unity` and existing unit tests (`CraftingServiceTests.cs`) remain unbroken after every milestone.

| Test ID | Test Name | Type | Setup & Execution | Expected Assertion |
|---|---|---|---|---|
| `REG-01` | `ExistingCraftingServiceTests_PassUnmodified` | EditMode | Run existing [`Assets/Tests/Editor/CraftingServiceTests.cs`](file:///d:/Development/Game%20Development/3D%202D%20World/Assets/Tests/Editor/CraftingServiceTests.cs) (`TryCraft_WithValidIngredients_ConsumesItemsAndAddsOutput`, etc.). | All 4 existing tests pass in `< 50 ms`. |
| `REG-02` | `RPGProvingGrounds_BootsAndEntersGameplayStateWithoutConsoleErrors` | PlayMode | Load `Assets/Scenes/RPGProvingGrounds.unity` via `SceneManager.LoadSceneAsync`. Wait 3 frames. | `GameBootstrapper.Instance != null`; `ServiceLocator` contains all core & framework services; `LogAssert.NoUnexpectedReceived()` confirms zero `LogType.Error` or `LogType.Exception`. |
| `REG-03` | `VoxelDungeonGeneration_AndBurstGreedyMeshing_ProducesValidMesh` | PlayMode / EditMode | Run `LevelGenerationService.GenerateLevel(...)` followed by `GreedyMeshingService.GenerateMesh(room.Grid, ...)`. | Generated `Mesh.vertexCount > 0` and `Mesh.triangles.Length > 0` with zero NativeCollection leak warnings. |
