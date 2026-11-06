# UI Audit — 3D 2D World (The Lost Survivor)
**Date**: October 2026  
**Auditor**: Antigravity (Senior UI/UX + Systems Analyst)  
**Scope**: Full codebase inspection — all scripts, UXML, USS, ScriptableObjects, events, and documentation reviewed.

---

## 1. Project Overview

The game is a first-person hybrid voxel/mesh RPG titled **The Lost Survivor**. The protagonist **Jack** is a modern military survivor stranded in a dark fantasy forest realm. The project uses **Unity UI Toolkit** (UIDocument / UXML / USS) exclusively — no legacy uGUI (Canvas) components were found in any script. The render pipeline is **URP 17.3.0**. The project is in Vertical Slice 1 production.

---

## 2. Existing UI Implementation Inventory

### 2.1 PlayerHUD (`Gameplay.UI.PlayerHUD`)
**Files**: [`PlayerHUD.cs`](file:///D:/Development/Game%20Development/3D%202D%20World/Assets/Scripts/Gameplay/UI/PlayerHUD.cs), [`PlayerHUD.uxml`](file:///D:/Development/Game%20Development/3D%202D%20World/Assets/UI/PlayerHUD.uxml), [`PlayerHUD.uss`](file:///D:/Development/Game%20Development/3D%202D%20World/Assets/UI/PlayerHUD.uss)

**What it does:**
- Renders **Health Bar** and **Stamina Bar** in the top-left corner, pulling live data from `AttributeComponent.GetAttribute("Health")` and `GetAttribute("Stamina")` every frame.
- Shows a **numeric label** (`"100 / 100"`) inside each bar track.
- Displays a **center crosshair** (6 px white dot).
- Shows a **contextual interaction prompt** (center-screen at 56% vertical), driven by `PlayerInteractionController.CurrentPrompt`.
- Fires a **red damage vignette flash** (0.18 s coroutine) on `EntityDamagedEvent` when the target is the player actor.
- Fires a **stamina exhaustion flash** on `PlayerStaminaExhaustedEvent`.
- Both bars use USS `transition-duration: 0.1s` for smooth fill animation.

**Status bars driving data:**
- Health: `AttributeComponent → "Health"` attribute (MaximumValue / CurrentValue int).
- Stamina: `AttributeComponent → "Stamina"` attribute.

**Known gaps / issues:**
- Player actor is resolved every frame with `FindWithTag("Player")` — no event-driven binding once found (only skipped if `_playerActor != null`).
- No third resource bar (Mana is referenced in documentation but no Mana attribute exists in any ScriptableObject yet).
- No visual state for InCombat vs out-of-combat regeneration difference.
- Vignette and exhaustion flash use raw USS `background-color` on a full-screen element — functional but cosmetically plain.

---

### 2.2 HotbarHUD (`Gameplay.UI.HotbarHUD`)
**Files**: [`HotbarHUD.cs`](file:///D:/Development/Game%20Development/3D%202D%20World/Assets/Scripts/Gameplay/UI/HotbarHUD.cs), [`Hotbar.uxml`](file:///D:/Development/Game%20Development/3D%202D%20World/Assets/UI/Hotbar.uxml), [`Hotbar.uss`](file:///D:/Development/Game%20Development/3D%202D%20World/Assets/UI/Hotbar.uss)

**What it does:**
- Renders a **9-slot hotbar** at the bottom-center of the screen.
- Selected slot is visually highlighted via USS class `hotbar-slot--selected` (gold border, warm background).
- Each slot displays a slot number label (1–9) and a `BlockMaterial.mainTexture` icon.
- Responds to `HotbarStateChangedEvent` from `HotbarService`.
- Mouse clicks on slots call `HotbarService.SelectSlot(slotIndex)`.
- Keyboard 1–9 bindings dispatch `PlayerHotbarSlotSelectedEvent` via `InputManager`.

**Current content:**
- The hotbar currently holds **`BlockDefinition`** objects (voxel blocks for placement), not RPG inventory items. There is a design split: the hotbar is currently a block-placement palette, not a quick-item/consumable bar.
- An `ActiveBlockLabel` text label shows the selected block name and RuntimeId — this is a debug artifact, not a styled HUD element.

**Known gaps:**
- No item icons from `ItemData` — only voxel block material textures.
- No consumable items assigned to hotbar slots via `InventoryComponent`.
- `ActiveBlockLabel` (debug label) should be replaced with a styled block-name tooltip or removed in final HUD.

---

### 2.3 WorldMapHUD (`Gameplay.UI.WorldMapHUD`)
**Files**: [`WorldMapHUD.cs`](file:///D:/Development/Game%20Development/3D%202D%20World/Assets/Scripts/Gameplay/UI/WorldMapHUD.cs)

**What it does:**
- Renders both a **minimap** and a **fullscreen world map** as procedurally constructed UI Toolkit `VisualElement` trees.
- Map geometry is drawn as colored rectangle elements positioned by `WorldMapService` room data (no texture generation).
- Rooms: colored `Rect` elements (`FloorColor` for revealed, `UnexploredColor` for hidden).
- Corridors: rotated rectangular elements connecting room centers.
- Entity icons: pooled `VisualElement` circles/sprites for Player, Enemies, NPCs, Interactables.
- Player icon is always centered; all other entities are offset relative to player position.
- Minimap is toggled via `Tab` (`ToggleMinimapInputEvent`), world map via `M` (`ToggleWorldMapInputEvent`).
- World map has a side-panel controls UI: opacity slider, zoom slider, player/enemy/NPC toggles, close button.

**Minimap config (WorldMapConfig ScriptableObject):**
- Size: 200×200 px, anchored top-right, offset (−20, −20).
- Black semi-transparent background (0.6 alpha), 8 px border radius, overflow hidden.
- Player-centered; content shifts by `(containerCenter − playerMapPos × zoom)`.
- Reveal-on-entry with configurable neighbor reveal distance.

**Known gaps:**
- No player direction indicator (arrow showing facing direction) on minimap or world map.
- No map legend panel on fullscreen map.
- No region labels.
- No fog-of-war visual treatment (unexplored rooms use `UnexploredColor` but are hidden on minimap — fully invisible rather than a visually present fog).
- Fullscreen map has no frame or decorative border.
- Controls panel (opacity/zoom/toggles) uses unstyled default UI Toolkit widget appearance.
- `WorldMapConfig.PlayerIcon` sprite is serialized but no sprite is assigned by default.
- Map is procedurally generated voxel dungeon rooms — no outdoor terrain overview (outdoor voxel world has no map representation).

---

### 2.4 Input System (`Systems.Input.InputManager`)
**File**: [`InputManager.cs`](file:///D:/Development/Game%20Development/3D%202D%20World/Assets/Scripts/Systems/Input/InputManager.cs)

**Confirmed bound actions:**
| Action | Keyboard | Gamepad |
|---|---|---|
| Move | WASD / Arrow keys | Left stick / D-pad |
| Look | Mouse delta | Right stick |
| Run | Left Shift | L3 |
| Jump | Space | A (South) |
| Crouch | Left Ctrl | R3 |
| Pause | Escape / P | Start |
| Confirm | Enter / Space | A (South) |
| Cancel | Backspace | B (East) |
| InteractPrimary (Attack) | Left Mouse | Right Trigger |
| InteractSecondary | Right Mouse | Left Trigger |
| InteractUse (E) | E | X (West) |
| Save | F5 | — |
| Hotbar slots 1–9 | 1–9 | — |
| Toggle World Map | M | — |
| Toggle Minimap | Tab | — |

**Known gaps:**
- `OnPause` handler is empty — state transition between `Playing` and `Paused` is not implemented.
- `OnConfirm` handler is empty.
- No scroll-wheel hotbar cycling binding.
- No gamepad bindings for hotbar slot selection.
- No keyboard bindings for inventory open, quest journal, or character screen.

---

## 3. Gameplay Systems Affecting UI

### 3.1 Attributes (`Domain.Entities.BaseAttribute`, `Gameplay.Entities.AttributeComponent`)
- **Confirmed attributes**: Health, Stamina. Both have `CurrentValue`, `MaximumValue`, `RegenerationRate`, `RegenerationMultiplier`, `CombatRegenerationRate`.
- `ActorState.InCombat` flag changes regen rate at runtime.
- No Mana attribute defined in any ScriptableObject or `ActorData` file.
- Death state set when `Health == 0` via `StateComponent.SetState(ActorState.Death)`.

### 3.2 Status Effects (`Domain.Entities.ActorState`)
Defined flags: `None`, `Death`, `Poison`, `Sleep`, `Electrified`, `Enchanted`, `InCombat`.
- `Poison`, `Sleep`, `Electrified`, `Enchanted` are defined as enum values but **no system applies or removes them** in any reviewed script.
- `StateComponent` exists and manages these flags, but no UI displays active status effects.

### 3.3 Combat (`Gameplay.Combat.WeaponComponent`)
- Melee attack: spherecast, stamina-gated. Single weapon type per actor.
- `EntityDamagedEvent` published on hit.
- `PlayerStaminaExhaustedEvent` published when swing fails due to stamina.
- No ranged attack, no weapon switching, no equipment slots with multiple weapon types.
- `DamageType` enum exists (Physical + at minimum Acid from `SlimeEntity`).
- No weapon HUD indicator (current weapon icon/name not displayed).

### 3.4 Inventory (`Gameplay.Entities.InventoryComponent`)
- 20-slot capacity (configurable via `_capacity`).
- Items: `ItemData` ScriptableObject reference + stack `Count`.
- Stacking supported via `item.IsStackable` / `item.MaxStackSize`.
- `InventoryChangedEvent` published on add/remove.
- Confirmed item assets: `Item_AncientAmulet`, `Item_StoneChunk`, `Item_SurvivalRations`, `Item_WoodcutterAxe`, `Item_WoodLog`, `Item_WoodPlank`.
- **No inventory screen UI exists.** Inventory is data-only.
- No equipment slots (no equipped weapon/armor concept in code — `WeaponComponent` is a hardcoded component, not driven by equipped item data).
- No item tooltip UI.
- No drag-and-drop system.

### 3.5 Crafting (`Systems.Crafting.CraftingService`, `Domain.Crafting.CraftingRecipe`)
- Recipes are ScriptableObjects with ingredients (ItemData + count) and a result (ItemData + count).
- `CraftItemRequestedEvent` → `CraftingService.TryCraft()` validates ingredients, consumes, produces, publishes `ItemCraftedEvent`.
- Confirmed recipe: `Recipe_WoodPlanks` (WoodLog → WoodPlanks).
- **No crafting UI exists.** Logic is complete; presentation layer is absent.

### 3.6 Progression (`Gameplay.Entities.StatsComponent`)
- Level, Experience, PerkPoints, XpNeeded tracked.
- `LevelUp()` increments level, decrements XP, awards +1 PerkPoint.
- `EntityLevelUpEvent` published.
- XP curve: `XpLevelUpBase + (Level × XpLevelUpMult)` from ActorData.
- Confirmed actor data: `JackActorData` (player), `SlimeActorData`, `DebugActorData`.
- **No character/stats screen UI exists.** No perk allocation UI exists.

### 3.7 World Map (`Systems.WorldMap.WorldMapService`)
- Rooms revealed on `RoomEnteredEvent` with configurable neighbor reveal radius.
- Entity types: Player, Enemy, Npc, Interactable, Custom.
- Map is built from `LevelLayout.Rooms` + `LevelLayout.Connections`.
- World bounds computed from room extents.
- `WorldToMap` converts world 3D position to 2D map coordinates.

### 3.8 Save/Load (`Systems.Persistence.FileSaveService`, `Domain.Persistence.SaveData`)
- Single JSON save file at `Application.persistentDataPath/save_game.json`.
- Saves: full `LevelLayout`, room voxel grids, entity states, player state (health, level, XP, inventory).
- Manual save: F5 → `GameSaveRequestedEvent`.
- `EntityStateLoadedEvent` restores actor state on load.
- **No save/load UI.** No main menu. No pause menu.

### 3.9 Interaction (`Gameplay.Player.PlayerInteractionController`, `Gameplay.Entities.BaseContainer`)
- 2.5 m raycast from player camera.
- Highlights `BaseEntity` with `InteractionPrompt` string.
- `HarvestableNode.InteractionPrompt` = "Harvest {Name} (Swing weapon/tool to chop/mine)".
- `BaseContainer` exists with interact API — no container loot UI.
- **No door, NPC dialogue, or merchant system found.**

### 3.10 Quest System
- Quest structure defined textually in `GAME_DESIGN.md` (QS_00–QS_04).
- `LevelCompletedEvent` triggers next level generation (Playing state).
- **No QuestManager, QuestData ScriptableObject, or quest UI found.** Quest tracking is not implemented in code.

### 3.11 Game State Machine (`Domain.StateMachine.GameStateMachine`)
- States: `None`, `MainMenu`, `Playing`, `Paused`, `GameOver`, `Loading`.
- `Playing` state handles `LevelCompletedEvent` → triggers new level generation.
- **No UI hooked to state transitions.** `OnPause` in InputManager is empty. `MainMenu`, `GameOver`, and `Loading` states have no associated UI.

---

## 4. Existing Visual Assets

### 4.1 UI Assets
- `Assets/UI/`: `PlayerHUD.uxml`, `PlayerHUD.uss`, `Hotbar.uxml`, `Hotbar.uss`, `PanelSettings.asset`
- `Assets/UI Toolkit/UnityThemes/`: Unity default theme (no custom theme file authored)
- `Assets/Sprites/UI/Materials/`: Folder exists but no inspectable sprite or texture files found at audit time.

### 4.2 Sprite Categories (Non-UI)
World sprites organized into: `Bricks`, `BuildingTextures`, `Doors`, `Elements`, `Entities`, `Industrial`, `Rocks`, `Tech`, `Urban`, `Wood`. These are voxel/world sprites, not UI sprites.

### 4.3 URP Settings
- `PC_RPAsset.asset` + `PC_Renderer.asset` (separate mobile variant).
- ACES tonemapping, bloom, volumetric fog confirmed in `DefaultVolumeProfile.asset`.
- URP 17.3.0.

### 4.4 Dungeon Palettes
Three confirmed palette definitions: `CastleBastionPalette`, `ClassicCryptPalette`, `OvergrownCatacombPalette`. These inform the visual mood for dungeon-themed UI variants.

---

## 5. Confirmed Missing UI Systems

| System | Code Status | UI Status |
|---|---|---|
| Main Menu | State stub only | **Missing** |
| Pause Menu | State stub, empty OnPause | **Missing** |
| Death / Game Over Screen | `GameOver` state stub | **Missing** |
| Loading Screen | `Loading` state stub | **Missing** |
| Inventory Screen | Full data model + events | **Missing** |
| Equipment Screen | No equipment slot system | **Missing** |
| Item Tooltip | Item data exists | **Missing** |
| Crafting UI | Full backend logic | **Missing** |
| Quest Journal | No quest system code | **Missing** |
| Quest Tracker (HUD) | No quest system code | **Missing** |
| Dialogue System | No dialogue code | **Missing** |
| Character / Stats Screen | StatsComponent + XP data | **Missing** |
| Status Effects HUD | Enum + StateComponent exist | **Missing** |
| Enemy Health Bar | No enemy HUD component | **Missing** |
| Boss Health Bar | Boss designed in GDD | **Missing** |
| Save / Load UI | FileSaveService complete | **Missing** |
| Settings Screen | Input deadzone/sensitivity exist | **Missing** |
| Confirmation Dialogs | — | **Missing** |
| Notifications / Toasts | — | **Missing** |
| Container / Loot UI | BaseContainer exists | **Missing** |
| Level Up Notification | EntityLevelUpEvent exists | **Missing** |

---

## 6. Architecture Notes for UI Implementation

- **Framework**: Unity UI Toolkit exclusively. Do not introduce uGUI.
- **Event Bus**: `GameEventManager` (pub/sub) is the correct integration path for all UI data binding. UI components should subscribe via `GameEventManager.AddListener<T>` in `OnEnable` and unsubscribe in `OnDisable`.
- **ServiceLocator**: Used for service access (`ServiceLocator.TryGet<T>()`). UI controllers should use this pattern to access `HotbarService`, `WorldMapService`, `CraftingService`, etc.
- **Input**: `InputManager` dispatches typed event objects. New UI actions (inventory open, journal, etc.) should be added as new `InputAction` entries and corresponding event types.
- **Asset Resolution**: Assets are resolved via `Resources.Load` with `AssetDatabase` fallback in Editor — consistent across all three existing UI controllers.
- **PanelSettings**: Single `PanelSettings.asset` shared across all `UIDocument` instances.
- **Sorting/Layering**: No explicit sort order is set on current `UIDocument` components. The HUD, hotbar, and map are on separate GameObjects — panel sort order will need to be managed when modal screens (pause, inventory) are added.
