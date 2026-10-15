# Level Generation Architecture

This is the complete guide to the Level Generation system in the **3D 2D World** Unity project. This document serves as the primary guide for future contributors and AI sessions working on procedural generation.

> **`Corrected 2026-10-15`.** Since the last refresh this document covered a renderer that has been substantially extended: block selection is now role-based (no hardcoded config IDs), rooms carry `Archetype`/`Palette` overrides and a `FloorLevel` tier, the generator assigns vertical tiers, corridors grade between tiers with stair runs, `VoxelStairBuilder` adds sunken pits / raised daises / pillars / niches / parapets / room sockets, and each room spawns a `RoomTrackerTrigger` that drives `AreaSchedulerService`. The parameters table, algorithm description, and data model were updated against the actual code; Section 4 was rewritten to match. The old "replaces a pooled-tile-prefab renderer" note is retained but the preceding voxel/meshing description was itself already stale and is now corrected.

## Architecture Overview

The level generation follows strict data/presentation separation:
1. `LevelGenerationParameters`: Pure data config, serializable, exposed in the Inspector.
2. `RoomGraphGenerator`: Pure C# algorithm, no Unity dependencies except `Vector2Int` and `Mathf`.
3. `LevelLayout`/`LevelRoomNode`/`LevelConnection`: Pure data output (rooms, connections, tiers, archetypes, sockets, start/exit states).
4. `LevelGenerationService`: `IGameService` that listens for requests, runs the generator (or loads a save), and publishes the result.
5. `LevelLayoutRenderer`: `MonoBehaviour` that listens for the completed layout and physically renders it by voxelizing each room/corridor into a `RoomGridData` grid and meshing it with `GreedyMeshingService`, then spawns a `RoomTrackerTrigger` per room and requests a player spawn.

```
RoomGraphGenerator (pure math: grid positions, tiers, connections)
   │   produces LevelLayout (LevelRoomNode[], LevelConnection[])
   └► LevelGenerationService (IGameService; save-load gate)
        ──LevelGenerationCompletedEvent──► LevelLayoutRenderer
              ├── RoomGridData voxelization (floor/wall/ceiling via BlockRole + Palette + Archetype)
              ├── VoxelStairBuilder (sunken pits / raised daises / stairs / pillars / niches / parapets / sockets)
              ├── GreedyMeshingService → one combined mesh per room/corridor
              ├── RoomTrackerTrigger per room ──RoomEnteredEvent──► AreaSchedulerService
              └── PlayerSpawnRequestedEvent ──► PlayerSpawnService
```

---

## 1. Generation Parameters

These fields are exposed in the Inspector on the `LevelLayoutRenderer` (serialized as `_parameters`). Defaults below are the **current** code defaults in `LevelGenerationParameters` (`Domain/LevelGeneration/LevelGenerationData.cs`), which differ from the prior snapshot.

*   **`Seed` (int, default -1):** Controls randomness. `-1` means fully random. Any positive value results in a deterministic/reproducible layout. The same seed also selects floor/wall/ceiling block variants deterministically.
*   **`MainPathRooms` (int, default 6):** Number of rooms on the guaranteed critical path from Start to Exit. *(was 5)*
*   **`BranchRooms` (int, default 5):** Number of optional dead-end or side rooms spawned off the main path. *(was 4)*
*   **`MinRoomSize` / `MaxRoomSize` (int, default 12 / 22):** Minimum/maximum room dimensions (in world units). Snapped to even numbers for perfect doorway grid alignment. *(was 6 / 10)*
*   **`GridSpacing` (int, default 32):** World-space distance between abstract grid cells. *(was 16)*
*   **`BranchChance` (float, default 0.6):** Probability (0.0 to 1.0) that an attempted branch room successfully spawns.
*   **`CycleChance` (float, default 0.35):** Probability (0.0 to 1.0) that adjacent unconnected rooms will form a cyclic corridor/shortcut. *(was 0.3)*
*   **`GridHalfExtents` (Vector2Int, default 6,6):** Maximum grid coordinates rooms can occupy, acting as a bounding box to prevent infinite sprawl. *(was 5,5)*
*   **`RaycastDistance` (float, default 50):** Max distance for the downward raycast used to find solid ground for the player spawn point. *(new — absent from prior snapshot)*
*   **`SpawnOffset` (Vector3, default (0, 5, 0)):** Vertical offset added to the start room position before the ground raycast. *(new)*

**Rendering-config-driven data:** The visual block palette is owned by the `LevelRenderConfig` ScriptableObject (`Assets/Data/Config/WorldGeneration/LevelRenderConfig.asset`), which references a `BlockRegistry` and lists `Archetypes` + themed `Palettes`. Block *selection* at runtime is role-based, not ID-based (see Section 4).

---

## 2. Generation Algorithm (`RoomGraphGenerator.Generate`)

The generation happens in distinct phases. All math is pure C# (`System.Random`); the generator touches no Unity services beyond `Vector2Int`/`Mathf`.

1.  **Initialization:** If `Seed < 0`, pick a random seed via `UnityEngine.Random.Range`. Store the seed in the layout. Create the `occupied` grid-position dictionary.
2.  **Start Room:** Place the `StartRoom` at grid `(0,0)`, `FloorLevel = 0`, `IsStart = true`.
3.  **Main Path Generation:** Loop `MainPathRooms` times. For each step, attempt `TryExtendPath` — pick a random unoccupied cardinal neighbor within `GridHalfExtents` (directions shuffled, preferring not to reverse the previous direction), create the room, add a `LevelConnection(IsMainPath=true)`.
4.  **Vertical Tiering (main path):** After each main-path extension, ~45% chance to climb or descend a floor tier: `currentTier += (random < 0.5) ? +1 : -1`, clamped to `[-2, 2]`, assigned to `nextRoom.FloorLevel`. **This is the tiering pass** — each room's `FloorLevel` later offsets its world Y by `FloorLevel * TierHeightInBlocks`.
5.  **Exit Assignment:** The final room on the main path is marked `IsExit = true`.
6.  **Branch Generation:** Up to `BranchRooms * 3` attempts. Pick a random existing room, roll against `BranchChance`; on success, `TryExtendPath` as a non-main-path branch. The branch's `FloorLevel` may match its source or deviate by ±1 (40% chance), clamped `[-2, 2]`.
7.  **Cyclic Pass (`AddCycles`):** For every room, check all 4 cardinal neighbors. If a neighbor exists but no connection is present, roll against `CycleChance`. If successful, add a `LevelConnection(IsMainPath=false)` between them, turning the spanning tree into a graph with loops.
8.  **Output:** Returns the `LevelLayout` (rooms, connections, start/exit) ready for rendering.

> **Note:** `RoomGraphGenerator` only produces the *abstract* graph. It does **not** yet assign `Archetype` or `Sockets` — those are populated later, during rendering, by `LevelLayoutRenderer.BuildRoomGrid` (deterministic per-room archetype selection + `VoxelStairBuilder.DiscoverRoomSockets`).

---

## 3. Data Model

### `LevelLayout`
*   `int Seed`
*   `List<LevelRoomNode> Rooms`
*   `List<LevelConnection> Connections`
*   `LevelRoomNode StartRoom` / `LevelRoomNode ExitRoom` — helper `Find` lookups
*   `bool AreConnected(string roomA, string roomB)` — checks both connection directions

### `LevelRoomNode` (updated)
*   `string Id` (e.g., "Room_0")
*   `Vector2Int GridPosition` — abstract mathematical coordinate
*   `Vector3 WorldPosition` — `GridPosition * GridSpacing`
*   `Vector2Int Size` — room dimensions in world units (always even numbers)
*   `int FloorLevel` — **vertical floor tier** (e.g. 0, 1, -1); offsets the room's rendered Y by `FloorLevel * TierHeightInBlocks`. *(new)*
*   `bool IsStart` / `bool IsExit` — role flags
*   `RoomArchetype Archetype` — **assigned at render time** (see Section 4); null until `BuildRoomGrid` picks one. *(new)*
*   `List<RoomSocket> Sockets` — **assigned at render time** via `VoxelStairBuilder.DiscoverRoomSockets`; spawn-point candidates for entities/chests. *(new)*

### `LevelConnection`
*   `FromRoomId` / `ToRoomId` (string)
*   `Direction` (Vector2Int) — cardinal direction from source to target on the grid
*   `IsMainPath` (bool)

### `LevelRenderConfig` (ScriptableObject — updated)
> The config **no longer** exposes `DefaultFloorBlockID`/`DefaultWallBlockID`/`DefaultCeilingBlockID`. Block selection is role-based (see Section 4).
*   `float RoomFloorHeight` (0.1), `float RoomCeilingHeight` (4.1) — base room vertical extent
*   `BlockRegistry BlockRegistry` — owns all `BlockDefinition`s
*   `List<RoomArchetype> Archetypes` — room-shape profiles (StandardHall, RaisedDais, SunkenArena) picked deterministically per room
*   `int TierHeightInBlocks` (4) — world-Y offset between floor tiers
*   `List<BlockPalette> Palettes` — themed material wear sets (ClassicCrypt, CastleBastion, OvergrownCatacomb)

### Supporting types (new since last snapshot)
*   `RoomArchetype` (`Domain/LevelGeneration/RoomArchetype.cs`): `RoomElevationMode` (StandardFlat / SunkenPit / RaisedDais / StaircaseConnector), `ElevationStepHeight`, `CustomFloorBlock`/`CustomWallBlock`/`CustomCeilingBlock`/`StepBlock` overrides, `ThemePalette` override, pillar flags (`HasPillars`, `PillarThickness`, `PillarInset`), and spawn weights (`MaxEnemySpawns`, `MaxChestSpawns`, `AllowNpcSpawn`).
*   `BlockPalette` (`Domain/Grid/BlockPalette.cs`): `DefaultFloor/Wall/Ceiling/Pillar/Steps`, weighted `FloorVariants`/`WallVariants` (`BlockVariant{Block,Weight}`) for procedural wear (cracked/mossy/grates), plus a `LiquidBlock` slot. `PickFloorBlock(rng)`/`PickWallBlock(rng)` weighted rolls.
*   `RoomSocket` / `SocketType` (`Domain/Grid/RoomSocketData.cs`): `SocketType` = { Walkway, PlayerSpawn, EnemySpawn, NpcSpawn, Chest, Interactable, PropLight }; `RoomSocket` carries `LocalGridPosition`, `WorldPosition`, `Type`, `LookDirection`.

---

## 4. Rendering & Meshing (`LevelLayoutRenderer`)

`LevelLayoutRenderer` is a `MonoBehaviour` (self-registers `GreedyMeshingService` in its `Awake`) that listens for `LevelGenerationCompletedEvent`, `LevelGenerationFailedEvent`, `GameSaveRequestedEvent`, and `GameDataLoadedEvent`, and translates the pure layout data into voxels + meshes.

### Listening
Subscribes in `OnEnable`:
*   `LevelGenerationCompletedEvent` → `BuildLayout(layout)`
*   `LevelGenerationFailedEvent` → logs the error
*   `GameSaveRequestedEvent` → assembles + writes `LevelSaveData`
*   `GameDataLoadedEvent` → `OnGameLoaded` (rebuilds saved grids)

### Block resolution (role-based, not ID-based)
> The prior snapshot described selecting blocks by `_config.DefaultFloorBlockID` etc. That no longer exists. Selection is now:
1.  `ResolveDefaultBlocks(layout)`: query `BlockRegistry.GetBlocksByRole(Floor/Wall/Ceiling)` and pick **one random block of each role** via `System.Random(layout.Seed)`. These become the level-wide fallbacks `_floorBlock`/`_wallBlock`/`_ceilingBlock`.
2.  Per room, resolve the effective block set with this override chain: `room.Archetype.Custom*Block` → `palette.Default*` → `_config` fallback for that role. `palette` = `room.Archetype.ThemePalette` ?? `_config.Palettes[0]` ?? null.

### Voxelizing (`BuildRoomGrid` + `BuildConnectionGrid`)
*   **Per room:** create a `RoomGridData(Size.x, height+1, Size.y)` where `height = RoundToInt(RoomCeilingHeight - RoomFloorHeight)` plus `ElevationStepHeight` when the archetype is non-flat. Fill base floor (each cell rolled via `palette.PickFloorBlock(rng)` for wear variants) and ceiling. Apply elevation architecture via `VoxelStairBuilder`:
    *   `SunkenPit` → trench + 4 perimeter stair ramps (`VoxelStairBuilder.BuildSunkenPit`)
    *   `RaisedDais` → central platform + approach stairs (`BuildRaisedDais`)
    *   `HasPillars` → corner pillars (`BuildPillars`)
    *   `CarveWallNiches` → alcoves for torches/barrels/chests (skipped on sides that have door openings)
    *   `BuildLowCoverParapets` → low obstacle bars in large flat rooms (tactical cover)
    *   4× `BuildGridWallEdge` → solid walls with **doorway gaps** computed from the room's connections (centered on the wall, `IsDoorwayIndex` for width>2)
*   **Tier offset:** `tierYOffset = room.FloorLevel * _config.TierHeightInBlocks`; the grid's world position is shifted by this so multi-level rooms stack correctly.
*   **Sockets:** `room.Sockets.Clear()` then `VoxelStairBuilder.DiscoverRoomSockets(grid, bottomLeft, room.Sockets, maxSockets:14)` — scans the interior for solid-floor-with-headroom cells and records spawn candidates (first = `Chest`, rest = `EnemySpawn`). *(Data exists; consumption/spawning is not yet wired — see note.)*
*   **Room tracker:** one `RoomTrackerTrigger` GameObject per room, parented under `_generatedRoot`, positioned at the room center, configured with `(room.Id, room.GridPosition, size)`; publishes `RoomEnteredEvent` → `AreaSchedulerService`.
*   **Archetype assignment:** deterministic `archIndex = |layout.Seed ^ room.GridPosition.GetHashCode() ^ (roomIndex*71)| % Archetypes.Count` so a given seed+position always yields the same archetype.
*   **Per connection:** `BuildConnectionGrid` computes horizontal/vertical orientation, `heightDelta` between the two rooms' tiers, corridor width = 4 (2 walkable + 1 wall each side), and builds a graded staircase run (`BuildStraightStairRun`-style logic inlined) so corridors can climb/descend. The corridor's grid world Y is `Min(sourceY, targetY)` and floors fill up to each step's current level.

### Meshing
All voxel grids (rooms + corridors) are collected into `_generatedGrids`. Once every grid for the layout is built, `_meshingService.Initialize(_config.BlockRegistry)` runs once, then `_meshingService.GenerateMeshForRoom(grid, worldPosition, _generatedRoot)` is called per grid. `GreedyMeshingService` runs the Burst-compiled greedy-meshing job that merges adjacent identical-block faces into larger quads, and `GridMeshBuilder` bakes the result into a single `MeshFilter`/`MeshRenderer`/`MeshCollider`/`GridRoomLink` GameObject per room/corridor — not one GameObject per block.

### Configuration
All visuals are data-driven through `LevelRenderConfig` → `BlockRegistry` → `BlockDefinition`s (each with `RuntimeId`, `BlockRenderType` {SolidMesh/TransparentMesh/Prefab}, `Material`, `Roles` list, `SurfaceType`, `IsSolid`). **There is no pooling in this path** — meshes are rebuilt from scratch on every generation/load (see "Cleanup").

### Cleanup
`ClearExistingLayout()` destroys all children of `_generatedRoot` (`Destroy` in play mode, `DestroyImmediate` in edit mode) and clears `_generatedGrids`. Triggered before every fresh build and on save-load.

### Hotbar wiring
On a fresh `BuildLayout`, `HotbarService.SetRegistry(_config.BlockRegistry)` is called (if registered) so the 9-slot palette can populate from the active block set.

### Save/load rendering (`OnGameLoaded`)
On load, the saved `RoomSaveData[]` grids are rehydrated into `RoomGridData` (voxel data copied via `GetRawData`/`Array.Copy`), meshed per-grid, then `PlayerSpawnRequestedEvent` and `EntityStateLoadedEvent` are published. Note: **the load path does not re-run `BuildRoomGrid`, so no `RoomTrackerTrigger`s are re-spawned from a save** — only fresh generations place per-room trackers. *(Potential inconsistency.)*

### Player Handling
The renderer never moves the player itself. It computes a spawn position — `layout.StartRoom.WorldPosition + _parameters.SpawnOffset`, raycast down via `PhysicsUtils.FindGroundPosition(basePos, _parameters.RaycastDistance)` to find solid ground — and publishes `PlayerSpawnRequestedEvent`. `PlayerSpawnService` then positions the player (`GameObject.FindWithTag("Player")` + `CharacterController`).

> **Gap (confirmed, unchanged):** `ExitTrigger` placement. The component exists (`Gameplay/World/ExitTrigger.cs`) and correctly publishes `LevelCompletedEvent` on player collision, but nothing in `BuildRoomGrid`/`BuildLayout`/`BuildConnectionGrid` (or anywhere else in the codebase) instantiates or attaches it during generation. If the infinite-crawler loop works in practice, it's because `ExitTrigger` is placed manually somewhere not covered by this audit.

> **Gap (new):** socket consumption / enemy spawning. `VoxelStairBuilder.DiscoverRoomSockets` records `EnemySpawn` and `Chest` sockets per room and `BuildRoomGrid` calls it, but **no code iterates `room.Sockets` to spawn a `SlimeEntity` (or any actor) or a chest prefab**. `SlimeEntity` exists as a full AI component but has no factory wiring. Same half-built shape as the `ExitTrigger` gap.

### Gizmos
`OnDrawGizmos`: green wire sphere = Start room, red wire sphere = Exit room, cyan lines = graph connections. No trigger-bounds box is drawn.

---

## 5. The Core Game Loop

This is how the infinite crawler loop flows at runtime:

1.  `GameBootstrapper` starts the game, entering the `Playing` state (the only state ever entered — see `STATES.md`).
2.  `Playing.OnEnterAsync` subscribes to `LevelCompletedEvent`.
3.  `LevelLayoutRenderer.Start` publishes a `LevelGenerationRequestedEvent` (when `_generateOnStart` is true).
4.  `LevelGenerationService` catches it. If a save file exists (`FileSaveService.HasSaveFile()`), it loads and publishes `GameDataLoadedEvent` instead of generating fresh — see `EVENTS.md`. Otherwise it generates a mathematical layout (with vertical tiers — Section 2) and publishes `LevelGenerationCompletedEvent`.
5.  `LevelLayoutRenderer` catches the completed event, voxelizes and meshes the dungeon (Section 4), spawns per-room `RoomTrackerTrigger`s, wires the `HotbarService` registry, and publishes `PlayerSpawnRequestedEvent` so `PlayerSpawnService` can position the player.
6.  `AreaSchedulerService` (listening on the same completed event + `RoomEnteredEvent`) initializes all rooms to `Purged`, then promotes the start room + neighbors to `Active`/`Dormant`.
7.  The player explores, enters room tracker volumes (ticking `RoomEnteredEvent` → area tiering), and steps into an `ExitTrigger` volume (placement caveat noted above).
8.  `ExitTrigger` publishes `LevelCompletedEvent`.
9.  `Playing.OnLevelCompleted` catches it and immediately publishes a new `LevelGenerationRequestedEvent` with `Seed = -1` (fully random) — and a **fresh** `LevelGenerationParameters()` (other fields revert to defaults). This happens entirely within the `Playing` state — no state transition occurs.
10. The loop repeats infinitely.

> **Discrepancy with GDD (unchanged in intent):** The GDD's step 5 / "State Management" line describes a state-machine-driven `Playing ↔ Loading ↔ Generating` transition. The actual loop regenerates via event subscription *inside* `Playing` with no `Generating` state and no state transition. If the seamless-transition vision still holds, `STATES.md`'s "Next Immediate Tasks" has the concrete next step (flesh out `Loading` as a real transition state). That gap is unchanged; this doc's Section 5 just additionally reflects the room-tracker + tiering steps now in the loop.

---

## Extending the Generator

Here is exactly how to add more data to the generation system.

### 1. Adding a New Room Role (e.g., BossRoom)
**Step A:** Add a flag to `LevelRoomNode` in `LevelGenerationData.cs`.
```csharp
public class LevelRoomNode
{
    // ... existing fields
    public bool IsBossRoom;
}
```

**Step B:** Mark it during generation in `RoomGraphGenerator.Generate()`.
```csharp
// e.g., Pick a random branch room to be the boss room
var branchRooms = layout.Rooms.FindAll(r => !r.IsStart && !r.IsExit);
if (branchRooms.Count > 0) {
    branchRooms[random.Next(branchRooms.Count)].IsBossRoom = true;
}
```

**Step C:** Use it in `LevelLayoutRenderer.BuildRoomGrid`, selecting a different `BlockDefinition` ID from `_config` when filling floor/wall blocks for that room instead of `DefaultFloorBlockID`.
> **Updated 2026-10-15:** block selection is now role/palette/archetype-based (Section 4), so "Step C" should read as "select a different *palette/block variant* for that room" via the `Archetype.ThemePalette`/`Custom*Block` overrides, or by stamping a special `BlockRole`.

### 2. Adding Prop Placement (Chests, Torches)
**Step A:** Define a placement struct and add a list to `LevelLayout`.
```csharp
[Serializable]
public struct PropPlacement
{
    public string PropId; // e.g., "chest_basic"
    public Vector3 WorldPosition;
    public Quaternion Rotation;
}

public class LevelLayout
{
    // ... existing fields
    public List<PropPlacement> Props = new();
}
```

**Step B:** Calculate placements in `RoomGraphGenerator` (pure math).
```csharp
// Inside CreateRoom() or after the graph is built
layout.Props.Add(new PropPlacement {
    PropId = "torch",
    WorldPosition = room.WorldPosition + new Vector3(0, 2f, room.Size.y * 0.5f - 0.5f),
    Rotation = Quaternion.Euler(0, 180, 0)
});
```

**Step C:** Read it in `LevelLayoutRenderer.cs`. Note: unlike room/corridor geometry (which is meshed, not pooled — see Section 4), individual props are still good candidates for `PoolingManager`, since they're separate GameObjects rather than part of the fused room mesh.
```csharp
foreach (var prop in layout.Props)
{
    var prefab = _propDatabase.GetPrefab(prop.PropId);
    var instance = pool.GetPooledObject<PropView>(prefab);
    instance.transform.SetPositionAndRotation(prop.WorldPosition, prop.Rotation);
}
```
> **Updated 2026-10-15:** the socket-based placement (`VoxelStairBuilder.DiscoverRoomSockets`) is the *preferred, already-integrated* path for chest/enemy spawns. New props should preferably consume `room.Sockets` (filtered by `SocketType`) rather than a parallel `Props` list, to stay consistent with the tier/archetype pipeline.

### 3. Adding a New Generation Parameter
**Step A:** Add the field to `LevelGenerationParameters`.
```csharp
public int MaxChestsPerLevel = 3;
```
*Because it is serialized, it immediately appears in the Unity Inspector!*

**Step B:** Use it in the algorithm.
```csharp
var chestsSpawned = 0;
while (chestsSpawned < parameters.MaxChestsPerLevel) { ... }
```
