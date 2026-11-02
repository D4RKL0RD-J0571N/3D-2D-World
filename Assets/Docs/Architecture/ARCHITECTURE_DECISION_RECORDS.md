# FANTASY ENGINE — ARCHITECTURE DECISION RECORDS (ADR) & ASSET GOVERNANCE
**Document ID**: `ARCH-07`  
**Scope**: Canonical record of architectural decisions, evaluated alternatives, tradeoffs, third-party framework governance, and resolved engineering questions (`ADR-001` through `ADR-009`).  
**Status Labels**: `ACCEPTED` | `SUPERSEDED` | `UNRESOLVED`

---

## ADR-001: Evolve `ServiceLocator` + `IEngineModule` Over Full Third-Party DI Framework
- **Status**: `ACCEPTED`
- **Context**: [`GameBootstrapper`](file:///d:/Development/Game%20Development/3D%202D%20World/Assets/Scripts/Core/Bootstrap/GameBootstrapper.cs) currently registers 17 concrete services in [`ServiceLocator`](file:///d:/Development/Game%20Development/3D%202D%20World/Assets/Scripts/Core/Services/ServiceLocator.cs). As the project evolves into a modular Fantasy Engine, modules need clean dependency ordering and interface-based service swapping without hardcoding Skyrim services into Engine Core.
- **Alternatives Considered**:
  1. *Adopt VContainer or Zenject*: Full constructor-injection container.
  2. *Two-Phase `ServiceLocator` + `IServiceRegistry` + `IEngineModule` topological host*.
- **Decision**: Option 2 (Two-Phase `ServiceLocator` + `IEngineModule`).
- **Rationale & Tradeoffs**:
  - **Pros**: Preserves 100% compatibility with all 20+ existing services and gameplay scripts (`ServiceLocator.Get<T>()`); zero IL-weaving or reflection startup penalty; trivial for modders to understand; solves initialization order bugs (such as [`BlockPlacementService.Initialize()`](file:///d:/Development/Game%20Development/3D%202D%20World/Assets/Scripts/Systems/Building/BlockPlacementService.cs#L15-L26) failing to resolve [`GreedyMeshingService`](file:///d:/Development/Game%20Development/3D%202D%20World/Assets/Scripts/Systems/Meshing/GreedyMeshingService.cs) at boot) by separating Phase 1 (`RegisterServices`) from Phase 2 (`InitializeAsync`).
  - **Cons**: Requires discipline not to call `ServiceLocator.Get<T>()` inside constructors or Phase 1 registration (enforced at runtime by a `_isRegistrationPhase` guard in `ServiceLocator`).

---

## ADR-002: Namespaced `ResourceId` (`namespace:path`) & `DefinitionRef<T>` Over Enums and Sequential IDs
- **Status**: `ACCEPTED`
- **Context**: Existing systems use C# `enum`s (`SkyrimSkill`, `FactionType`, `QuestType`) or sequential Editor-generated IDs (`ACT_0001`, `ITM_0001` in [`EntityData.OnValidate()`](file:///d:/Development/Game%20Development/3D%202D%20World/Assets/Data/Config/EntityData.cs#L17-L24)).
- **Alternatives Considered**:
  1. *Keep C# enums and reserve integer ranges for mods (e.g., `1000..9999`)*.
  2. *Use raw `string` IDs everywhere*.
  3. *Use immutable `ResourceId` (`namespace:path`) wrapped in strongly-typed `DefinitionRef<T>` structs*.
- **Decision**: Option 3 (`ResourceId` + `DefinitionRef<T>`).
- **Rationale & Tradeoffs**:
  - **Pros**: Eliminates cross-mod ID collisions (`mod_a:item/ruby` vs `mod_b:item/ruby`); allows modders to add new skills, factions, damage types, and quest archetypes without recompiling C#; `DefinitionRef<T>` prevents passing an Item ID where a Skill ID is expected at compile time; removes the fragile `block.DisplayName == item.DisplayName` string match in [`HotbarService.ResolveBlock()`](file:///d:/Development/Game%20Development/3D%202D%20World/Assets/Scripts/Systems/HUD/HotbarService.cs#L177-L186).
  - **Cons**: Slightly higher memory footprint than a bare integer, mitigated by precomputing a 32-bit FNV-1a `Hash` inside `ResourceId`, caching the resolved reference inside `DefinitionRef<T>`, and translating `ResourceId <-> ushort` via `VoxelPaletteMap` at the Burst boundary for [`GreedyMeshingJob`](file:///d:/Development/Game%20Development/3D%202D%20World/Assets/Scripts/Systems/Meshing/GreedyMeshingJob.cs).

---

## ADR-003: `Newtonsoft.Json` for External Mod/Save Files + `Odin Inspector` for Unity Editor Tooling
- **Status**: `ACCEPTED`
- **Context**: [`CreatorStudioService`](file:///d:/Development/Game%20Development/3D%202D%20World/Assets/Scripts/Systems/Creator/CreatorStudioService.cs), [`CuratorService`](file:///d:/Development/Game%20Development/3D%202D%20World/Assets/Scripts/Systems/Showcase/CuratorService.cs), and [`FileSaveService`](file:///d:/Development/Game%20Development/3D%202D%20World/Assets/Scripts/Systems/Save/FileSaveService.cs) currently use `UnityEngine.JsonUtility`, which cannot serialize `Dictionary<K,V>`, polymorphic fragments, or perform `JObject` schema migrations.
- **Alternatives Considered**:
  1. *Unity `JsonUtility` with `[SerializeReference]`*.
  2. *`OdinSerializer` (JSON/Binary) for all external files*.
  3. *`Newtonsoft.Json` for external JSON files (mods, worlds, saves) + `Odin Inspector` for Editor UI*.
- **Decision**: Option 3 (`Newtonsoft.Json` + `Odin Inspector`).
- **Rationale & Tradeoffs**:
  - **Pros**: `Newtonsoft.Json` (`com.unity.nuget.newtonsoft-json`) produces clean, standard JSON that modders can edit in VS Code with JSON Schema intellisense and comments (`//`), and supports `JObject` DOM inspection for version migrations without coupling JSON files to internal C# assembly/class names. `Odin Inspector` (already installed in `Assets/Plugins/Sirenix/`) provides best-in-class Editor drawers.
  - **Cons**: Requires registering lightweight `JsonConverter`s for `Vector3`, `Quaternion`, `Color`, `ResourceId`, and `InstanceGuid`.

---

## ADR-004: Hybrid Asset Loading (`Addressables` + `AssetBundles` + `External Raw Files`) Behind `IAssetProvider`
- **Status**: `ACCEPTED`
- **Context**: Built-in packages (`POLYGON Fantasy Kingdom`) contain thousands of Unity prefabs, while casual community modders want to drop a `.png` icon, `.ogg` sound, or 3D model into a folder without installing a 15 GB Unity Editor.
- **Alternatives Considered**:
  1. *Require all mods to be compiled as Unity Addressables or AssetBundles*.
  2. *Multi-source `IAssetProvider` supporting Addressables/Registries (built-in), AssetBundles (complex prefab/VFX mods), and direct disk loading for external files*.
- **Decision**: Option 2 (Multi-source `IAssetProvider` with `FallbackAssetCatalog`).
- **Rationale & Tradeoffs**:
  - **Pros**: Zero barrier to entry for data/icon/sound modders; full Unity prefab/shader power for advanced modders; guaranteed non-null fallback assets prevent runtime crashes.
  - **Cons**: Requires maintaining the `ExternalFileMountSource` loader behind `IAssetProvider`.

---

## ADR-005: Default Mod Scripting via Declarative Action Schemas (Tier 4A); Opt-In `TrustedAssembly` (Tier 4B)
- **Status**: `ACCEPTED`
- **Context**: Mods need to trigger custom gameplay logic (e.g., custom spell effects, trap sequences, quest stages).
- **Alternatives Considered**:
  1. *Unrestricted C# `.dll` loading by default for all mods*.
  2. *Embed a third-party Lua/JavaScript interpreter (MoonSharp / Jint)*.
  3. *Declarative Event-Condition-Action JSON Schemas (Tier 4A) by default + explicit opt-in `TrustedAssembly` C# plugins (Tier 4B)*.
- **Decision**: Option 3.
- **Rationale & Tradeoffs**:
  - **Pros**: Tier 4A is 100% deterministic, sandbox-safe, serializable inside the In-Game Editor, and cannot execute malicious OS commands. Tier 4B remains available for trusted total-conversion teams who need low-level C# `IEngineModule` access.
  - **Cons**: Requires a well-designed library of built-in Condition and Action primitives in the Fantasy Framework.

---

## ADR-006: Third-Party Asset Library & Framework Governance
- **Status**: `ACCEPTED`
- **Context**: The project owner has a vast collection of Unity assets (`Assets/` and `D:\Development\Game Development\Downloads`). Indiscriminate importing previously caused 2.77 GB of bloat and compiler/input conflicts (`Game Kit Controller`, `SimpleApocalypse`, `RetroWeaponsPack`).
- **Governance Matrix**:

| Asset / Framework | Governance Classification | Architectural Integration Rule |
|---|---|---|
| **Synty POLYGON Fantasy Kingdom** | **Production Content Pack** | Packaged as `nordic_rpg` base content assets; modular pieces registered in `CreatorCatalogSO` for the In-Game Editor. |
| **Odin Inspector & DOTween Pro** | **Core Engine Infrastructure** | Direct engine dependency for Editor drawers and UI/object tweening. |
| **Feel (`MoreMountains`) & `DamageNumbersPro`** | **Presentation / Feedback Infrastructure** | Triggered strictly via `GameEventManager` bridges (`CombatFeedbackBridge.cs`). Core domain logic never references `MMF_Player` or `DamageNumber` directly! |
| **Magic Pig Games (`Northstar Compass`)** | **Framework UI Adapter** | Wrapped behind [`CompassService.cs`](file:///d:/Development/Game%20Development/3D%202D%20World/Assets/Scripts/Systems/HUD/CompassService.cs) (`framework:quests_nav`). |
| **Behavior Designer** | **Framework AI Module** | Used for modular NPC behavior trees; trees communicate with `StealthDetectionService` and `FactionService` via blackboard interfaces, never hardcoded scene singletons. |
| **GSpawn & Hypertonic Grid Placement** | **Architectural Reference / Editor Utility** | Study snapping math and gizmo UX to upgrade [`CreatorPlacementController.cs`](file:///d:/Development/Game%20Development/3D%202D%20World/Assets/Scripts/Gameplay/Creator/CreatorPlacementController.cs). Do not couple runtime saves to GSpawn editor-only assemblies. |
| **ORK Framework 3 / Makinom 2 (`Gaming Is Love`)** | **Study Reference & Optional Bridge** | Study its data-driven formulas and schematic triggers. Keep existing [`ORKCombatantBridge.cs`](file:///d:/Development/Game%20Development/3D%202D%20World/Assets/Scripts/Gameplay/Integration/ORKCombatantBridge.cs) and [`MakinomSchematicTrigger.cs`](file:///d:/Development/Game%20Development/3D%202D%20World/Assets/Scripts/Gameplay/Integration/MakinomSchematicTrigger.cs) isolated in an optional adapter module so the Fantasy Engine never *requires* ORK to run. |
| **Ultimate Inventory System & Survival Template PRO** | **Sandbox Study Reference Only** | Do **not** import into the main project. Study their item fragment and survival stamina/building patterns in a separate sandbox. |

---

## ADR-007: Staged External 3D Mesh Mod Support (Resolving `UNRESOLVED-01`)
- **Status**: `ACCEPTED`
- **Context**: Community modders and world creators need a clear path to introduce custom 3D meshes and prefabs into the engine without destabilizing early core/framework milestones.
- **Decision**: Adopt a **staged 3D asset-loading architecture** strictly behind [`IAssetProvider`](file:///d:/Development/Game%20Development/3D%202D%20World/Assets/Docs/Architecture/CONTENT_ARCHITECTURE.md):
  1. **Phase 1 (Milestones 1–5)**: Support built-in project assets (Synty `POLYGON Fantasy Kingdom`, primitives, and ScriptableObject-registered prefabs) and compiled **Unity AssetBundles** for complex prefabs, custom shaders, and particle systems, plus external 2D textures (`.png`/`.jpg`) and audio clips (`.ogg`/`.wav`).
  2. **Phase 2 (Milestone 6 / Optional Extension)**: Integrate `com.atteneder.gltfast` (`glTFast`) as an optional `IAssetMountSource` plug-in so external `.glb`/`.gltf` 3D models can be loaded directly from disk and wrapped into runtime collider/material templates without requiring modders to install the Unity Editor.
- **Architectural Mandate**: No gameplay system, editor controller, or content definition may call `Resources.Load`, `AssetDatabase.LoadAssetAtPath`, `AssetBundle.LoadFromFile`, or `GltfImport` directly. Every asset load request **must** pass through `IAssetProvider.LoadAssetAsync<T>(ResourceId)` so adding `glTFast` in Milestone 6 requires zero changes to calling code.

---

## ADR-008: Conditional Archival of `Cowsins` and `SoftKitty` (Resolving `UNRESOLVED-02`)
- **Status**: `ACCEPTED` (Conditional on Dependency Migration & Reference Preservation)
- **Context**: `Assets/Cowsins/` (~133.5 MB, FPS Engine) and `Assets/SoftKitty/` (~249.6 MB, Master Inventory Engine) are legacy third-party frameworks from earlier prototyping.
- **Source-Code Dependency Audit Findings**:
  - **`SoftKitty` Audit**: Zero (`0`) references exist in `Assets/Scripts/`, `Assets/Scenes/RPGProvingGrounds.unity`, or `Assets/Data/`.
  - **`Cowsins` Audit (CRITICAL BLOCKING DEPENDENCY FOUND)**: [`Assets/Scripts/Editor/CreatorStudioSetup.cs`](file:///d:/Development/Game%20Development/3D%202D%20World/Assets/Scripts/Editor/CreatorStudioSetup.cs#L57-L95) **actively references 12 prefabs inside `Assets/Cowsins/Prefabs/DragAndDropExtras/`** (`Turret.prefab`, `ExplosiveBarrel.prefab`, `JumpPad.prefab`, `NonFracturedCrate_01.prefab`, `Lootbox_Free.prefab`, `Door.prefab`, `BasicGround.prefab`, `PointToCapture.prefab`, `Coin.prefab`, `GenericPowerup.prefab`, `Bullet Pickeable.prefab`, `WeaponPickeableGeneric.prefab`), and these 12 GUIDs are serialized inside `Assets/Data/Creator/DefaultCreatorCatalog.asset`!
- **Decision**:
  1. **Approve archival** of `Assets/Cowsins/` and `Assets/SoftKitty/` to the external reference library (`D:\Development\Game Development\Downloads\ReferenceArchives\`), **strictly conditional on completing the following prerequisites first**:
     - **Prerequisite A (Catalog Migration)**: In Milestone 5, update [`CreatorStudioSetup.cs`](file:///d:/Development/Game%20Development/3D%202D%20World/Assets/Scripts/Editor/CreatorStudioSetup.cs) and regenerate `Assets/Data/Creator/DefaultCreatorCatalog.asset` so all default catalog entries reference `Assets/PolygonFantasyKingdom/Prefabs/` props and structures instead of `Assets/Cowsins/Prefabs/DragAndDropExtras/`.
     - **Prerequisite B (Indirect GUID Verification)**: Run an automated `.unity`, `.prefab`, and `.asset` YAML GUID scan confirming zero remaining GUID references to `Assets/Cowsins/` or `Assets/SoftKitty/`.
     - **Prerequisite C (External Reference Preservation)**: Archive the complete folders along with a `MANIFEST_AND_LICENSE.md` recording package names, exact version numbers, publisher info, Unity Asset Store license terms, and extracted architectural notes (e.g., Cowsins procedural weapon recoil curves and radar math; SoftKitty dynamic stat modifier tables).
  2. **Prohibition**: Neither `Assets/Cowsins/` nor `Assets/SoftKitty/` may be deleted or moved during Planning or Milestones 1–4 before Prerequisite A and B are verified.

---

## ADR-009: Voxel Save Storage & Compression Strategy (Resolving `UNRESOLVED-03`)
- **Status**: `ACCEPTED`
- **Context**: [`RoomGridData`](file:///d:/Development/Game%20Development/3D%202D%20World/Assets/Scripts/Systems/Generation/LevelGenerationService.cs#L19-L77) and [`RoomSaveData`](file:///d:/Development/Game%20Development/3D%202D%20World/Assets/Scripts/Systems/Save/SaveData.cs#L24-L32) store 3D voxel grids as `ushort[]` arrays. Currently, [`FileSaveService`](file:///d:/Development/Game%20Development/3D%202D%20World/Assets/Scripts/Systems/Save/FileSaveService.cs) serializes `RoomSaveData.VoxelData` directly inside `savegame.json` via `JsonUtility.ToJson(data, true)`.
- **Quantitative Format & Compression Comparison**:
  - **Representative Workloads**:
    - *Scenario A (20-Room Procedural Dungeon, avg `24×8×24` per room)*: `92,160` `ushort` voxels (`184.3 KB` raw binary).
    - *Scenario B (Custom Outdoor/Multi-Chunk Voxel Region, `128×32×128`)*: `524,288` `ushort` voxels (`1,048.6 KB` raw binary).

| Format Option | Scenario A Size (20 Rooms) | Scenario B Size (`128×32×128`) | Serialize Time (Est. Desktop) | Deserialize Time (Est. Desktop) | Pros & Cons |
|---|---|---|---|---|---|
| **1. Raw JSON Array (`JsonUtility` / `Newtonsoft`)** | ~680 KB – 1.1 MB (pretty-printed text) | ~3.8 MB – 6.2 MB text | ~18 ms | ~35 ms (high GC string/token allocs) | **Worst**: Bloats save file 4–6×, ruins human readability of `SaveEnvelope.json`, causes GC spikes on load. |
| **2. Raw Binary Sidecar (`CodecId = 0`, `ushort[]`)** | `184.3 KB` | `1,048.6 KB` | `< 0.3 ms` (`Buffer.BlockCopy`) | `< 0.3 ms` (direct span read) | Fastest CPU speed, zero dependencies, deterministic size, but uncompressed on disk. |
| **3. Binary RLE16 Sidecar (`CodecId = 1`, `[runLength, blockId]`)** | `14 KB – 26 KB` (~88% reduction) | `65 KB – 140 KB` (structured) / `> 1 MB` (noisy terrain) | `~0.8 ms` | `~0.5 ms` | Extremely fast single-pass C# loop; ideal for structured dungeon rooms with large air (`0`) and floor (`1`) runs; degrades on high-entropy noise. |
| **4. Binary RLE16 + Deflate/Brotli Sidecar (`CodecId = 2`, `.NET Standard 2.1`)** | `4 KB – 9 KB` (~96% reduction) | `22 KB – 55 KB` (~95% reduction) | `~3.5 ms` | `~1.8 ms` | Best compression across both structured dungeons and noisy terrain; uses built-in `System.IO.Compression.DeflateStream` / `BrotliStream` (zero third-party native DLLs). |

- **Decision**:
  1. **Logical & Physical Separation of Metadata and Binary Payloads**:
     - `SaveEnvelope.json` (and `WorldAuthoringData.json`) **must never embed massive voxel arrays inline**. Instead, JSON metadata stores room/chunk bounds, `VoxelPaletteMap` (`ushort -> ResourceId`), and a relative pointer to a binary sidecar file: `"voxelPayloadFile": "slot_01_voxels.vxlbin"`.
  2. **Versioned Binary Voxel Container (`.vxlbin`) with Pluggable `CodecId`**:
     - Every `.vxlbin` file begins with a fixed **24-byte Binary Header**:
       - `Magic` (4 bytes): ASCII `"FXVL"` (`0x4C565846`)
       - `FormatVersion` (2 bytes, `ushort`): `1`
       - `CodecId` (2 bytes, `ushort`):
         - `0` = `RawUShortLittleEndian` (uncompressed `ushort[]` bytes)
         - `1` = `RLE16` (`ushort runLength, ushort paletteIndex` pairs)
         - `2` = `RLE16_Deflate` (`RLE16` stream compressed via `System.IO.Compression.DeflateStream`)
       - `ChunkCount` (4 bytes, `uint`): Number of rooms/chunks in the payload.
       - `UncompressedByteLength` (4 bytes, `uint`): Total byte size of unpacked `ushort[]` buffers for pre-allocation validation.
       - `PayloadCrc32` (4 bytes, `uint`): IEEE 802.3 CRC-32 checksum of the encoded payload bytes.
       - `Reserved` (4 bytes, `uint`): `0` (for future alignment/flags).
  3. **Benchmark-Driven Default Codec Selection**:
     - Implement `IVoxelPayloadCodec` supporting `CodecId = 0` (`Raw`), `CodecId = 1` (`RLE16`), and `CodecId = 2` (`RLE16_Deflate`).
     - Default to `CodecId = 1` (`RLE16`) or `CodecId = 2` (`RLE16_Deflate`) based on the automated NUnit benchmark suite in [`ARCHITECTURE_TEST_STRATEGY.md`](file:///d:/Development/Game%20Development/3D%202D%20World/Assets/Docs/Architecture/ARCHITECTURE_TEST_STRATEGY.md) (with an automatic fallback to `CodecId = 0` if `RLE16` output size exceeds 90% of `UncompressedByteLength` on high-entropy rooms).
