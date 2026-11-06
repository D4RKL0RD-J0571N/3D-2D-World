# RPG PROJECT — SOURCE & ASSET AUTHORITY INDEX
**Classification Standard**: `CANONICAL` | `IMPLEMENTED` | `DOCUMENTED` | `PROPOSED`

---

## 1. Core Architecture Scripts (`Assets/Scripts/Core/`)
| Script | Authority | Responsibility |
|---|---|---|
| `GameBootstrapper.cs` | `IMPLEMENTED` | `[DefaultExecutionOrder(-100)]` Entry point, registers services, starts `GameStateMachine`. |
| `ServiceLocator.cs` | `IMPLEMENTED` | Global decoupled service provider (`Register`, `Get`, `TryGet`, `Unregister`). |
| `GameEventManager.cs` | `IMPLEMENTED` | Generic strongly-typed pub/sub event bus. |
| `GameStateMachine.cs` | `IMPLEMENTED` | Async state machine orchestrating game lifecycle. |
| `SaveManager.cs` | `IMPLEMENTED` | JSON-based persistent game save and load handler. |

---

## 2. Skyrim RPG Domain Scripts (`Assets/Scripts/Domain/Skyrim/`)
| Script | Authority | Responsibility |
|---|---|---|
| `SkillData.cs` | `IMPLEMENTED` | Defines the 18 Skyrim skills, XP scaling formulas, and level-up milestones. |
| `SkillProgressionService.cs` | `IMPLEMENTED` | Manages runtime player skill XP accumulation and broadcasts `SkillLeveledUpEvent`. |
| `FactionData.cs` | `IMPLEMENTED` | Defines `FactionType` enum and pairwise disposition/hostility matrices. |
| `FactionService.cs` | `IMPLEMENTED` | Queries hostility, manages faction reputation, and tags actors. |
| `QuestData.cs` | `IMPLEMENTED` | Definitions for procedural radiant quests, objectives, and bounty contracts. |
| `RadiantQuestService.cs` | `IMPLEMENTED` | Generates procedural bounties and integrates with compass waypoints. |
| `StealthData.cs` | `IMPLEMENTED` | Definitions for stealth detection states (`Hidden`, `Searching`, `Detected`). |
| `StealthDetectionService.cs` | `IMPLEMENTED` | Calculates line of sight, awareness meters, and sneak crit multipliers. |

---

## 3. Gameplay & World Interaction (`Assets/Scripts/Gameplay/`)
| Script | Authority | Responsibility |
|---|---|---|
| `BaseActor.cs` | `IMPLEMENTED` | Base entity with health, damage calculation, floating numbers, and faction tags. |
| `PlayerInteractionController.cs` | `IMPLEMENTED` | First-person raycaster querying `IInteractable` objects in range. |
| `ChestInteractable.cs` | `IMPLEMENTED` | Locked containers with lock difficulty tiers and lockpick skill checks. |
| `BountyBoardInteractable.cs` | `IMPLEMENTED` | Interactable notice board generating radiant quests for the player. |
| `LeverInteractable.cs` | `IMPLEMENTED` | Mechanical levers triggering doors, gates, or traps. |
| `PressurePlateInteractable.cs` | `IMPLEMENTED` | Floor plate triggering traps when stepped on by entities. |
| `CompassService.cs` | `IMPLEMENTED` | Compass HUD service rendering directional pins to points of interest. |

---

## 4. Key Scenes
| Scene Path | Authority | Purpose |
|---|---|---|
| `Assets/Scenes/RPGProvingGrounds.unity` | `CANONICAL` | Primary active test environment featuring Synty Fantasy Kingdom buildings, locked chests, NPCs, and interactive props. |
| `Assets/Scenes/SampleScene.unity` | `LEGACY` | Original empty default scene. |
