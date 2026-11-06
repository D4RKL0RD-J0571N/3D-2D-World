# RPG PROJECT — STATE OF THE PROJECT
**Classification Standard**: `CANONICAL` | `IMPLEMENTED` | `DOCUMENTED` | `INFERRED` | `PROPOSED` | `UNKNOWN` | `CONFLICTING`

---

## 1. Executive Summary & Core Vision (`CANONICAL`)
- **Project Identity**: A **Modular, Extensible, Data-Driven Fantasy Engine** built in Unity 6 (`6000.3.8f1`, URP 17.3.0) with first-class support for **modding** and an **integrated In-Game Editor**.
- **Architectural Stratification**:
  1. **Layer A — Engine Core**: Domain-agnostic runtime infrastructure (`GameBootstrapper`, `ServiceLocator`, `GameEventManager<T>`, `ITickable`, `PoolingManager`, `AreaSchedulerService`, `FileSaveService`, `CreatorStudioService`, `LabService`, `CuratorService`).
  2. **Layer B — Fantasy Framework**: Opt-in, configurable RPG building blocks (`SkillProgressionService`, `FactionService`, `RadiantQuestService`, `StealthDetectionService`, `CompassService`, `CraftingService`, `LevelGenerationService`, `GreedyMeshingService`, `EntityRegistry`, `BlockRegistry`).
  3. **Layer C — Game Content & Proving Grounds**: Distinct RPG experiences and mods authored atop the framework:
     - *Proving Ground 1 (Current Active)*: **Skyrim-Style Nordic RPG** ([Assets/Scenes/RPGProvingGrounds.unity](file:///d:/Development/Game%20Development/3D%202D%20World/Assets/Scenes/RPGProvingGrounds.unity)) featuring **Synty POLYGON Fantasy Kingdom** modular assets, "learn by doing" skill progression, faction hostility matrices, lockpicking chests, stealth perception, and procedural radiant quests.
     - *Proving Ground 2 (Documented Vertical Slice)*: **"Jack & Lucian: The Silent Canopy"** — portal survival fantasy vertical slice (`Docs/GDD.md`, `Docs/GAME_DESIGN.md`).
     - *Proving Ground 3 (Implemented Prototype)*: **2.5D Voxel Dungeon Crawler & Creator Sandbox** — procedural room graphs, runtime greedy meshing, and 9-slot block hotbar.

---

## 2. Architectural Framework (`IMPLEMENTED`)
The engine enforces strict decoupling via Service Locator and Event Bus patterns:
- **Service Locator ([`ServiceLocator.cs`](file:///d:/Development/Game%20Development/3D%202D%20World/Assets/Scripts/Core/ServiceLocator.cs))**:
  - Central registry for all `IGameService` modules without static singleton spaghetti.
- **Typed Event Bus ([`GameEventManager.cs`](file:///d:/Development/Game%20Development/3D%202D%20World/Assets/Scripts/Core/GameEventManager.cs))**:
  - Strongly-typed pub/sub communication across Engine Core, Fantasy Framework, UI, and future Mod extensions.
- **In-Game Editor Foundations**:
  - [`CreatorStudioService.cs`](file:///d:/Development/Game%20Development/3D%202D%20World/Assets/Scripts/Systems/Creator/CreatorStudioService.cs): Runtime catalog browsing, 3D prop placement, and JSON level persistence (`Assets/Data/SavedWorlds/*.json`).
  - [`LabService.cs`](file:///d:/Development/Game%20Development/3D%202D%20World/Assets/Scripts/Systems/Lab/LabService.cs): Runtime parameter tuning (`LabParameterGroup`).
  - [`CuratorService.cs`](file:///d:/Development/Game%20Development/3D%202D%20World/Assets/Scripts/Systems/Museum/CuratorService.cs): In-game exhibit/asset auditing and report export.

---

## 3. Technology Stack & Curated Packages
- **Engine**: Unity 6 (`6000.3.8f1`), Universal Render Pipeline (URP 17.3.0).
- **Input System**: Hybrid mode (`activeInputHandler: 2` — Both `UnityEngine.Input` and `com.unity.inputsystem` active).
- **Active Engine Tooling & Art**:
  - `Synty POLYGON Fantasy Kingdom` (2,625 modular medieval prefabs).
  - `Odin Inspector & Serializer`, `DOTween Pro`, `Feel (MoreMountains)`, `DamageNumbersPro`, `Northstar Compass`, `Behavior Designer`, `AllIn1SpriteShader`, `GSpawn - Level Designer`, `FBX Editor`.
- **Strategic Reference Documents**:
  - See [`DESIGN_READINESS.md`](file:///d:/Development/Game%20Development/3D%202D%20World/Assets/Docs/Externalization/DESIGN_READINESS.md) for the full 3-layer engine, modding, and in-game editor architectural assessment.
  - See [`ASSET_LIBRARY_STRATEGY.md`](file:///d:/Development/Game%20Development/3D%202D%20World/Assets/Docs/Externalization/ASSET_LIBRARY_STRATEGY.md) for the 14-category asset inventory and ingestion strategy.
