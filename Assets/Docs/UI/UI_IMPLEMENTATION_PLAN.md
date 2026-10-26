# UI Implementation Plan — 3D 2D World (The Lost Survivor)
**Date**: October 2026  
**Framework**: Unity UI Toolkit (UIDocument / UXML / USS)  
**Status**: Pre-implementation — audit complete, no UI code changes made

> **Rule**: Do not implement anything in this plan without first completing the NanoBanana visual direction phase for that screen. Visual assets and layout must be confirmed before UXML/USS authoring.

---

## Phase 0 — Foundation (Prerequisite for all phases)

### 0.1 Shared USS Variables File
**Priority**: P1  
**Effort**: 1 session  
**Dependencies**: None  
**File**: `Assets/UI/Styles/DesignTokens.uss`

Create a single USS stylesheet defining all color tokens, spacing tokens, and typography tokens from `UI_DESIGN_SYSTEM.md`. This file must be included as the first `StyleSheet` on every `UIDocument`.

```uss
:root {
    --color-bg-panel: #13131A;
    --color-border-primary: #3A3A48;
    --color-border-accent: #C9962A;
    /* ... all tokens ... */
    --spacing-sm: 8px;
    --radius-md: 8px;
    /* ... */
}
```

**Acceptance criteria**: All existing USS files (`PlayerHUD.uss`, `Hotbar.uss`) are refactored to use variable references. No raw hex color literals in component stylesheets.

---

### 0.2 PanelSettings Audit
**Priority**: P1  
**Effort**: 30 min  
**Dependencies**: None

Confirm `Assets/UI/PanelSettings.asset` is configured:
- Scale Mode: `Scale With Screen Size`
- Reference Resolution: `1920 × 1080`
- Screen Match Mode: `Match Width or Height`, value `0.5`

If not, adjust. All three existing UIDocument controllers (PlayerHUD, HotbarHUD, WorldMapHUD) already reference this single asset.

---

### 0.3 UIDocument Sort Order Assignment
**Priority**: P1  
**Effort**: 30 min  
**Dependencies**: None

Set sort orders on existing UIDocument components:

| UIDocument | Sort Order |
|---|---|
| PlayerHUD | 0 |
| HotbarHUD | 1 |
| WorldMapHUD | 2 |
| (future) InventoryHUD | 20 |
| (future) PauseMenuHUD | 30 |
| (future) DeathScreenHUD | 50 |
| (future) LoadingScreenHUD | 60 |

---

### 0.4 Input Action Additions
**Priority**: P1  
**Effort**: 1 session  
**File**: [`InputManager.cs`](file:///D:/Development/Game%20Development/3D%202D%20World/Assets/Scripts/Systems/Input/InputManager.cs)  
**Dependencies**: None

Add the following missing `InputAction` entries and dispatch corresponding events:

| Action | Binding | Event |
|---|---|---|
| Inventory | `I` / Gamepad `Select` | `ToggleInventoryInputEvent` |
| Crafting | `C` | `ToggleCraftingInputEvent` |
| Journal | `J` | `ToggleJournalInputEvent` |
| Character | `K` | `ToggleCharacterInputEvent` |
| HotbarScroll | `Mouse/scroll` | `HotbarScrollInputEvent` (delta) |

Also: **implement `OnPause`** to publish `TogglePauseInputEvent` and transition `GameStateMachine` between `Playing` ↔ `Paused`.

**Acceptance criteria**: All new input actions are bound, dispatched, and cleanly unsubscribed in `Shutdown()`. `OnPause` is no longer empty.

---

## Phase 1 — Restyle Existing HUD (Visual Upgrade)

**Priority**: P1  
**Dependencies**: Phase 0 complete, NanoBanana Prompts 01 and 08 reviewed  
**Effort**: 2–3 sessions

### 1.1 PlayerHUD — Visual Redesign
**Files**: `PlayerHUD.uxml`, `PlayerHUD.uss`

- Apply `DesignTokens.uss` color variables.
- Replace plain `background-color` damage vignette with a **radial gradient** (vignette effect) — implement via a gradient texture applied to a full-screen VisualElement, toggled by opacity class.
- Replace plain status bar track background with themed dark slate.
- Add HP badge icon slot (pending icon sprite from NanoBanana Prompt 08).
- Add SP badge icon slot.
- Style interaction prompt key badge (add key icon sprite slot, restyle border with `--color-border-accent`).
- **Low-health pulse**: add USS animation class `status-bar__fill--health--critical` that pulses opacity when health < 25% threshold (requires threshold detection in C# code).

**Acceptance criteria**: Status bars match Dark Fantasy visual direction. Prompt displays key glyph sprite. Vignette is a radial gradient, not flat red fill.

---

### 1.2 HotbarHUD — Visual Redesign + Block→Item Transition
**Files**: `Hotbar.uxml`, `Hotbar.uss`

- Apply `DesignTokens.uss`.
- Style slot backgrounds as recessed wells (inner shadow via border trick or sprite).
- Remove or restyle `ActiveBlockLabel` — replace with tooltip-style hover label below hotbar.
- Add stack count badge element to each slot (`<Label name="Count_N" class="hotbar-slot__count" />`).
- **Future**: Bind hotbar to `InventoryComponent` ItemData icons in addition to/replacing BlockDefinition textures. Design decision required — see Questions section.

**Acceptance criteria**: Hotbar matches visual direction. No visible debug label in play mode. Stack counts display when slot has stackable item.

---

### 1.3 WorldMapHUD — Controls Panel Restyle
**File**: [`WorldMapHUD.cs`](file:///D:/Development/Game%20Development/3D%202D%20World/Assets/Scripts/Gameplay/UI/WorldMapHUD.cs)

- Apply `DesignTokens.uss` custom style sheet to `WorldMapHUD`'s UIDocument.
- Restyle `BuildWorldMapControls()` — replace default Unity Slider/Toggle appearance with USS classes from the shared component library.
- Add minimap frame element: a `VisualElement` with nine-slice frame sprite as background image (pending Prompt 03 sprite).
- **Fix fog-of-war on minimap**: Change `DisplayStyle.None` for unrevealed rooms to `DisplayStyle.Flex` with `UnexploredColor` background — makes the unexplored zone visible as a dark zone rather than invisible.
- Add player direction arrow element (pending sprite, requires player rotation input from `Player_Look`).

**Acceptance criteria**: Fullscreen map controls use styled components. Minimap shows dark fog tiles for unrevealed rooms. Map frame border is visible.

---

## Phase 2 — New System Screens (P1)

**Priority**: P1  
**Dependencies**: Phase 0 + Phase 1 complete. NanoBanana Prompts 10 and 11 approved.

### 2.1 Pause Menu
**New file**: `Assets/Scripts/Gameplay/UI/PauseMenuHUD.cs` + `Assets/UI/PauseMenu.uxml` + `Assets/UI/PauseMenu.uss`

**Data bindings**: None (static buttons only)  
**Events published**: `TogglePauseInputEvent` (Resume), `GameSaveRequestedEvent` (Save), `LoadMainMenuRequestedEvent` (Quit)

Steps:
1. Create `PauseMenuHUD` MonoBehaviour with `RequireComponent(UIDocument)`.
2. Subscribe to `TogglePauseInputEvent` in `OnEnable`.
3. Toggle `GameStateType.Paused` ↔ `GameStateType.Playing` via `GameStateMachine`.
4. Show/hide pause panel via `DisplayStyle`.
5. Set `Time.timeScale = 0` on pause; restore `1` on resume.
6. Wire Resume, Save, Quit buttons.

**Acceptance criteria**: Esc pauses game. All buttons functional. GameStateType transitions correctly. HUD remains visible behind pause overlay.

---

### 2.2 Death Screen
**New file**: `Assets/Scripts/Gameplay/UI/DeathScreenHUD.cs` + UXML/USS

**Trigger**: Subscribe to `EntityStateChangedEvent` where `NewStates == ActorState.Death` on player target.  
**Buttons**: "Try Again" → `GameSaveRequestedEvent` + scene/state reload | "Return to Menu" → `GameStateType.MainMenu`

Steps:
1. On player death detected, fade in over 1 second (opacity 0 → 1).
2. Lock all gameplay input while death screen is visible.
3. Implement `Time.timeScale = 0` after fade completes.

**Acceptance criteria**: Screen fades in on player death. Both buttons trigger correct behavior. Input is locked.

---

### 2.3 Loading Screen
**New file**: `Assets/Scripts/Gameplay/UI/LoadingScreenHUD.cs` + UXML/USS

**Trigger**: Subscribe to `GameStateType.Loading` entry.  
**Data binding**: None (no async progress reporting from `LevelGenerationService` yet).

Steps:
1. Show atmospheric background image (from NanaBanana Prompt 11).
2. Animate a spinner or pulsing bar (no real progress data yet).
3. Subscribe to `LevelGenerationCompletedEvent` → hide screen.

**Acceptance criteria**: Screen appears during level generation. Disappears on completion. No freeze visible.

---

### 2.4 Main Menu
**New file**: `Assets/Scripts/Gameplay/UI/MainMenuHUD.cs` + UXML/USS

**Data**: `FileSaveService.HasSaveFile()` drives "Continue" enabled state.  
**Buttons**: New Game → generate new level | Continue → load save | Settings → settings panel | Quit → `Application.Quit()`

Steps:
1. Shown in `GameStateType.MainMenu`.
2. "New Game" publishes `LevelGenerationRequestedEvent` with seed `-1`.
3. "Continue" loads `FileSaveService.ReadData()` and dispatches restoration events.
4. Settings: show inline settings panel (reuse from Pause menu).

**Acceptance criteria**: Main menu shows on startup. Both play modes functional. No save → Continue grayed out.

---

## Phase 3 — Inventory and RPG Screens (P1/P2)

**Priority**: P1 (Inventory), P2 (Character + Crafting)  
**Dependencies**: Phase 2 complete. Phase 0 complete. NanoBanana Prompt 07 approved.

### 3.1 Inventory Screen
**New file**: `Assets/Scripts/Gameplay/UI/InventoryHUD.cs` + `Assets/UI/Inventory.uxml` + `Assets/UI/Inventory.uss`

**Data source**: `InventoryComponent.Slots` (IReadOnlyList<InventorySlot>)  
**Event subscriptions**: `InventoryChangedEvent` → refresh display  
**Input**: `ToggleInventoryInputEvent` → show/hide. `Tab` cycles focus in inventory grid.

Steps:
1. Bind to player's `InventoryComponent` via `GameObject.FindWithTag("Player")`.
2. Generate 20 slot `VisualElement` instances from UXML template (one `<TemplateContainer>` per slot).
3. Update slot icon, stack count on `InventoryChangedEvent`.
4. Implement hover tooltip: `RegisterCallback<MouseEnterEvent>` on each slot → show/position `TooltipPanel`.
5. Implement right-click context menu: `RegisterCallback<MouseDownEvent>(evt => { if (evt.button == 1) ShowContextMenu(slotIndex); })`.
6. Context menu actions: Use (calls `InventoryComponent.RemoveItem` + gameplay effect), Drop (spawns `BaseItem` prefab at player position + removes), Inspect (show detail panel).
7. Pause gameplay input while inventory is open (suppress `CanReadGameplayInput`).

**Acceptance criteria**: All 20 slots render. `InventoryChangedEvent` updates display instantly. Tooltip shows on hover. Context menu functional for Use/Drop. Closing inventory restores gameplay input.

---

### 3.2 Character Panel (inside Inventory screen)
**Part of Inventory screen right panel**

**Data sources**: `StatsComponent` (Level, Experience, PerkPoints), `AttributeComponent` (all registered attributes)

Steps:
1. Resolve player `StatsComponent` and `AttributeComponent`.
2. Subscribe to `EntityLevelUpEvent` → refresh all stat labels.
3. Display: level number, XP progress bar (`Experience / XpNeeded`), all attribute current/max values.
4. **Future**: Perk allocation buttons when PerkData ScriptableObjects exist.

**Acceptance criteria**: All values reflect live game state. Level-up event refreshes display.

---

### 3.3 Crafting Screen
**New file**: `Assets/Scripts/Gameplay/UI/CraftingHUD.cs` + UXML/USS  
**Data source**: `CraftingRecipe` ScriptableObjects from `Resources.LoadAll<CraftingRecipe>` or a registry  
**Event**: `ToggleCraftingInputEvent` → show/hide | "Craft" button → `CraftItemRequestedEvent`

Steps:
1. Load all `CraftingRecipe` assets at startup.
2. List recipes in a scrollable column.
3. On selection: show recipe detail (ingredients with `inventory.HasItem` check, result preview).
4. Craft button: enabled only when `recipe.CanCraft(inventory)` returns true.
5. Subscribe to `ItemCraftedEvent` → refresh ingredient counts, show success toast.

**Acceptance criteria**: Recipe list shows `Recipe_WoodPlanks`. Craft button correctly enables/disables. Crafting consumes ingredients and adds result.

---

## Phase 4 — HUD Enhancements (P2)

**Dependencies**: Phase 1 + Phase 3 complete. Icon sprites from NanaBanana Prompt 12 imported.

### 4.1 Toast / Notification System
**New file**: `Assets/Scripts/Gameplay/UI/NotificationHUD.cs`

- Singleton-like HUD that manages a queue of timed notifications.
- Notifications slide in from right edge.
- Subscribe to: `EntityLevelUpEvent`, `InventoryChangedEvent` (item pickups), `ItemCraftedEvent`, room discovery events.
- Auto-dismiss after 3 seconds. Stack up to 4.

### 4.2 Enemy Health Bars
**New file**: `Assets/Scripts/Gameplay/UI/EnemyHealthBar.cs`

- World-space or screen-space overlay above each `BaseActor` enemy.
- Driven by `EntityDamagedEvent` → check if target is an enemy.
- Show for 3 seconds after last damage, then fade.
- Use existing `AttributeComponent.GetAttribute("Health")`.

### 4.3 Boss Health Bar
**New file**: `Assets/Scripts/Gameplay/UI/BossHealthBarHUD.cs`

- Full-width panel above hotbar.
- Requires new events: `BossEncounterStartEvent`, `BossDefeatedEvent`.
- Shows boss name, current phase, health bar.

### 4.4 Status Effect Icons
- Add to `PlayerHUD.uxml` a `StatusEffectsContainer` horizontal row below status bars.
- Subscribe to `EntityStateChangedEvent` for player.
- Show icon per active `ActorState` flag (Poison, Sleep, Electrified, Enchanted).
- Requires: icon sprites from NanaBanana Prompt 12 + status effect applier systems.

### 4.5 Player Direction Arrow on Minimap
- Add a `VisualElement` arrow child to `_minimapIconsContainer` always at center.
- Read `Camera.main.transform.eulerAngles.y` and set `rotate` style.
- Requires player arrow sprite from NanaBanana Prompt 03.

---

## Phase 5 — Settings and Polish (P2)

### 5.1 Settings Screen
- Sub-panel inside `PauseMenuHUD`.
- Audio: master/SFX/music volume sliders (requires `AudioMixer` references).
- Controls display: table of all `InputManager` action names and their current bindings (read from `InputAction.bindings`).
- Sensitivity / deadzone: expose `_sensitivity` and `_deadzone` from `InputManager` as serialized settings.
- For full rebinding: migrate from programmatic `InputAction` creation to `InputActionAsset` file format (this is a larger refactor — mark as P3).

### 5.2 Confirmation Dialog
- Generic reusable `ConfirmationDialog` component.
- Stack: text message + Confirm button + Cancel button.
- Show via static API: `ConfirmationDialog.Show("message", onConfirm, onCancel)`.
- Traps input focus while open.

### 5.3 Map Legend Panel
- Add `MapLegendPanel` `VisualElement` to fullscreen `WorldMapHUD`.
- Bottom-left of map content area.
- Lists all `MapEntityType` values with their icons and labels.
- Toggle via a "Legend" button on the controls panel.

---

## Dependency Graph

```
Phase 0 (Foundation)
    └── Phase 1 (HUD Restyle)
            └── Phase 2 (System Screens)
                    └── Phase 3 (Inventory + RPG)
                            └── Phase 4 (HUD Enhancements)
                                    └── Phase 5 (Settings + Polish)

NanaBanana visual work runs in parallel with all phases.
Icon sprites (Prompt 12) must be imported before Phase 4.
Frame sprites (Prompts 03, 05) must be imported before Phase 1.3 minimap frame.
```

---

## Implementation Decisions Required from Owner

> These questions cannot be answered from the codebase alone. Decisions needed before implementation.

### Q1 — Hotbar Dual Purpose
The hotbar currently holds `BlockDefinition` objects (voxel block palette). The game also has `ItemData` items (consumables, weapons, materials). **Should the hotbar hold:**
- (A) Block types only (current behavior — sandbox/building mode)
- (B) Items only (RPG consumable bar)
- (C) Separate modes (Tab to switch between block palette and item hotbar)
- (D) Unified — items can be blocks or ItemData based on type

**This decision drives Phase 1.2 and Phase 3.1 significantly.**

### Q2 — Item Rarity System
`ItemData` has no rarity field. Should a `ItemRarity` enum (Common / Uncommon / Rare / Epic / Legendary) be added to `ItemData` now, with corresponding border colors in the inventory UI?

### Q3 — Controller/Gamepad Inventory Navigation
Should the inventory be fully navigable by gamepad at this stage (requires focus management, directional navigation), or is keyboard/mouse only acceptable for Vertical Slice 1?

### Q4 — Quest System Scope
The Quest system exists in the GDD but has no code implementation. Should the Quest Journal and HUD objective tracker be deferred entirely until QuestManager is built, or should placeholder UI be built now with static data for the vertical slice narrative?

### Q5 — Theme Toggle in Game
Should both visual themes (Dark Fantasy + Militarized) be switchable in-game (e.g., Jack's UI changes as he "integrates" into the Realm), or are they separate builds/configurations? This affects how the shared USS token system is structured.

### Q6 — NPC Dialogue Scope
No NPC dialogue system exists in code. Is dialogue (with a full dialogue tree UI) planned for Vertical Slice 1, or should that screen be deferred?

---

## Acceptance Criteria Summary

| Phase | Done when |
|---|---|
| 0 — Foundation | `DesignTokens.uss` used by all UIDocuments. PanelSettings confirmed. Sort orders assigned. All new input actions dispatching events. `OnPause` implemented. |
| 1 — HUD Restyle | Health/stamina bars match visual theme. Prompt shows key glyph. Vignette is radial. Minimap fog tiles visible. Controls panel styled. |
| 2 — System Screens | Pause (Esc works), Death (triggers on player death), Loading (shows during generation), Main Menu (New Game + Continue work). |
| 3 — Inventory | All 20 slots render. Items display icons + stacks. Tooltip on hover. Context menu works. Character panel shows live data. Crafting screen validates and executes recipes. |
| 4 — HUD Enhancements | Toasts appear for level-up and item pickup. Enemy health bars visible above enemies. Boss bar appears on boss engagement. Status icons show for active states. |
| 5 — Polish | Settings screen exposes sensitivity. Confirmation dialog modal works. Map legend visible. |
