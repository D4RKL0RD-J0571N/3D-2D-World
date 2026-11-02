# RPG PROJECT — GAME DESIGN & SYSTEMS EXTERNALIZATION
**Status Classification**: `CANONICAL` | `IMPLEMENTED` | `DOCUMENTED` | `INFERRED` | `PROPOSED` | `UNKNOWN` | `CONFLICTING`

---

## 1. Core Gameplay Loops

### A. The Skyrim-Style Exploration & Progression Loop (`IMPLEMENTED`)
1. **Explore & Navigate**: First-person locomotion (`Player_Movement`, `Player_Look`, `Player_CameraRig`). Compass HUD bar displays nearby Points of Interest (POIs), dungeons, and quest targets.
2. **Interact & Loot**: Raycast interaction (`PlayerInteractionController`). Players encounter locked chests (`Novice`, `Apprentice`, `Adept`, `Expert`, `Master`). Lockpicking skill check determines success or pick breakage, granting `Lockpicking` XP.
3. **Engage in Combat / Stealth**:
   - **Stealth**: NPCs run line-of-sight and light-level raycasts via `StealthDetectionService`. The HUD sneak eye displays `Hidden`, `Searching`, or `Detected`.
   - **Sneak Attacks**: Attacking from stealth yields a 3.0x sneak critical damage multiplier.
   - **Melee/Magic**: `BaseActor` processes incoming damage and floating damage numbers (`DamageNumbersPro`).
4. **Progression ("Learn by Doing")**:
   - 18 core Skyrim skills tracked via `SkillProgressionService`.
   - XP accumulates per action (e.g. landing hits, picking locks, casting spells).
   - Upon filling skill XP bar, skill levels up and contributes to overall character level.
5. **Radiant Quests**:
   - Interact with `BountyBoardInteractable` in town squares to dynamically receive radiant contracts (e.g. "Kill Bandit Leader at Bleak Falls Barrow").

### B. The Voxel / Sandbox Creative Loop (`IMPLEMENTED` in legacy domain & Creator Studio)
- Voxel grid block placement and removal (`CreatorStudioService`, `BlockPlacementService`).
- Dynamic catalog registry allowing runtime testing of blocks and props.

---

## 2. Interaction Systems Architecture
- **Interface**: `IInteractable` (`Interact(GameObject instigator)`, `CanInteract(GameObject instigator)`, `GetInteractionPrompt()`).
- **Implementations**:
  - `ChestInteractable`: Handles lock difficulties, key requirements, lockpick consumption, and skill XP awards.
  - `BountyBoardInteractable`: Generates and hands radiant quests to the player.
  - `LeverInteractable`: Triggers mechanical events (doors, gates, traps).
  - `PressurePlateInteractable`: Stepped-on physical triggers for spike traps or gates.

---

## 3. Skills Matrix (18 Skills Implemented in `SkillData.cs`)
- **Combat**: `OneHanded`, `TwoHanded`, `Archery`, `Block`, `HeavyArmor`, `LightArmor`.
- **Stealth**: `Sneak`, `Lockpicking`, `Pickpocket`, `Speech`, `Alchemy`.
- **Magic**: `Destruction`, `Restoration`, `Alteration`, `Conjuration`, `Illusion`, `Enchanting`, `Smithing`.
