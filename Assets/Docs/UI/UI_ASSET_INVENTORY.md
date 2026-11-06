# UI Asset Inventory — 3D 2D World (The Lost Survivor)
**Date**: October 2026  
**Status Legend**: ✅ Implemented | ⚠️ Partial | 🔲 Planned | ❌ Not justified / future

---

## Priority Tiers

- **P1** — Required for a functional vertical slice. Must ship.
- **P2** — Required for a polished slice. High value.
- **P3** — Planned features with backend support. Implement next sprint.
- **P4** — Future-facing. Do not block on these.

---

## A. Always-On Gameplay HUD

| # | Asset | Priority | Status | Supporting System | Notes |
|---|---|---|---|---|---|
| A1 | **Health bar** (fill + track + numeric label + HP badge) | P1 | ✅ Implemented | `AttributeComponent "Health"` | Exists in `PlayerHUD.uxml/uss`. Needs visual redesign. |
| A2 | **Stamina bar** (fill + track + numeric label + SP badge) | P1 | ✅ Implemented | `AttributeComponent "Stamina"` | Exists in `PlayerHUD.uxml/uss`. Needs visual redesign. |
| A3 | **Damage vignette** (full-screen red flash) | P1 | ✅ Implemented | `EntityDamagedEvent` | Plain color fill. Restyle to radial gradient in dark theme. |
| A4 | **Stamina exhaustion flash** | P1 | ✅ Implemented | `PlayerStaminaExhaustedEvent` | Border flash on stamina bar. |
| A5 | **Crosshair / reticle** (center dot) | P1 | ✅ Implemented | Always on | Minimal 6 px dot. Replace with styled reticle sprite. |
| A6 | **Interaction prompt** (center-screen contextual) | P1 | ✅ Implemented | `PlayerInteractionController.CurrentPrompt` | Key badge + text label. Needs key icon sprite. |
| A7 | **9-slot hotbar** (slots + icons + selection state) | P1 | ✅ Implemented | `HotbarService`, `HotbarStateChangedEvent` | Currently block-type palette, not consumable bar. |
| A8 | **Active block / item name label** | P1 | ⚠️ Partial | `HotbarService.SelectedBlock` | Debug label exists. Style as proper name tag. |
| A9 | **Status effect icons** (Poison, Sleep, Electrified, Enchanted) | P2 | 🔲 Planned | `ActorState` flags — no applier system yet | Backend enum exists. UI needed when appliers are built. |
| A10 | **Enemy health bar** (world-space or screen-edge) | P2 | 🔲 Planned | `AttributeComponent` on `BaseActor` enemies | No enemy HUD component. Requires new `EnemyHealthBarHUD`. |
| A11 | **Boss health bar** (fullscreen, named, phased) | P1 | 🔲 Planned | `GameDesign: Tree Spirit boss` | Boss defined in GDD. No boss state machine found yet. |
| A12 | **Level-up notification toast** | P2 | 🔲 Planned | `EntityLevelUpEvent` | Backend event published. UI not built. |
| A13 | **Item pickup / harvest notification** | P2 | 🔲 Planned | `InventoryChangedEvent`, `HarvestNodeDamagedEvent` | Events exist. No toast system. |
| A14 | **Quest objective tracker** (HUD overlay) | P2 | 🔲 Planned | No quest system code | Future — requires QuestManager first. |
| A15 | **Combat state indicator** (InCombat flag visual) | P3 | 🔲 Planned | `ActorState.InCombat` | Minor — regen rate differs; subtle red pulse on health badge. |
| A16 | **Compass / direction indicator** | P3 | ❌ Not justified | No compass data source | Mark future. |
| A17 | **Contextual key binding hint** (key sprite in prompt) | P2 | 🔲 Planned | `InputManager` action names | Style: dark panel + key icon glyph + action text. |

---

## B. Navigation

| # | Asset | Priority | Status | Supporting System | Notes |
|---|---|---|---|---|---|
| B1 | **Minimap container** (200×200 px, top-right) | P1 | ✅ Implemented | `WorldMapHUD`, `WorldMapService` | Functional. No decorative frame or border. |
| B2 | **Minimap room tiles** (colored VisualElements) | P1 | ✅ Implemented | `WorldMapService.Rooms` → `RoomMapData.IsRevealed` | Functional. Colors from `WorldMapConfig`. |
| B3 | **Minimap corridor lines** (rotated rectangles) | P1 | ✅ Implemented | `WorldMapService.Connections` | Functional. |
| B4 | **Minimap player icon** (center white dot/sprite) | P1 | ✅ Implemented | `WorldMapConfig.PlayerIcon` | Sprite slot in config — no sprite assigned. Needs asset. |
| B5 | **Minimap entity icons** (enemy, NPC, interactable) | P1 | ✅ Implemented | `WorldMapService.TrackedEntities` → `MapEntity` | Functional. Sprite slots in config — no sprites assigned. |
| B6 | **Minimap decorative frame / border** | P2 | 🔲 Planned | UI Toolkit border styling | Dark stone frame for Theme A. Tactical border for Theme B. |
| B7 | **Minimap player direction arrow** | P2 | 🔲 Planned | `Player_Look` / camera transform | Not implemented. Requires reading player Y rotation. |
| B8 | **Fullscreen world map container** | P1 | ✅ Implemented | `WorldMapHUD` | Full-screen dark overlay. No frame. |
| B9 | **Fullscreen map room/corridor rendering** | P1 | ✅ Implemented | `WorldMapService` | Same element system as minimap. |
| B10 | **Fullscreen map controls panel** (opacity, zoom, toggles, close) | P1 | ⚠️ Partial | `WorldMapHUD.BuildWorldMapControls()` | Functional but uses unstyled default widgets. |
| B11 | **Fullscreen map decorative frame** | P2 | 🔲 Planned | Visual design only | Ancient parchment/stone border for Theme A. |
| B12 | **Map legend panel** | P2 | 🔲 Planned | Icon type enum `MapEntityType` | Not built. Icons + labels needed. |
| B13 | **Fog-of-war visual** (unexplored region treatment) | P2 | ⚠️ Partial | `RoomMapData.IsRevealed` + `UnexploredColor` | Rooms hidden on minimap by `DisplayStyle.None`. Should show dark "undiscovered" fill instead of nothing. |
| B14 | **Region / room label overlays** | P3 | 🔲 Planned | No region name data | Future — requires region naming system. |
| B15 | **Map quest markers** | P3 | 🔲 Planned | No quest system | Future. |
| B16 | **Custom/player-placed markers** | P4 | ❌ Not justified | No marker system | Future feature. |
| B17 | **Map filter buttons** (by entity type) | P2 | ⚠️ Partial | `WorldMapHUD` toggle booleans | Toggles exist, unstyled. Need styled tab/button bar. |

---

## C. Inventory and Equipment

| # | Asset | Priority | Status | Supporting System | Notes |
|---|---|---|---|---|---|
| C1 | **Inventory screen container** | P1 | 🔲 Planned | `InventoryComponent` (20 slots) | Data model complete. UI missing. |
| C2 | **Item grid** (20 slot grid layout) | P1 | 🔲 Planned | `InventoryComponent.Slots` | Grid of `InventorySlot` tiles. |
| C3 | **Item slot tile** (icon + stack count badge) | P1 | 🔲 Planned | `InventorySlot.Item`, `InventorySlot.Count` | 6 `ItemData` assets exist. |
| C4 | **Item tooltip panel** (name, description, rarity, stats) | P1 | 🔲 Planned | `ItemData` fields | No tooltip system built. |
| C5 | **Item rarity border / badge** | P2 | 🔲 Planned | No rarity field yet in `ItemData` | Recommend adding rarity enum. Design border tiers. |
| C6 | **Stack count label** | P1 | 🔲 Planned | `InventorySlot.Count` | Number badge on slot icon. |
| C7 | **Drag-and-drop / selection states** | P2 | 🔲 Planned | UI Toolkit `DragEnterEvent` | Not implemented. |
| C8 | **Equipment slots panel** (weapon, armor, etc.) | P3 | 🔲 Planned | No equipment slot system in code | Requires new `EquipmentComponent`. |
| C9 | **Character equipment appearance panel** | P4 | ❌ Not justified | No character mesh system | Future. |
| C10 | **Container / loot interface** | P2 | 🔲 Planned | `BaseContainer` exists, no loot UI | Side-by-side: container inventory + player inventory. |
| C11 | **Item context menu** (Use, Drop, Equip, Inspect) | P2 | 🔲 Planned | `InventoryComponent.RemoveItem/AddItem` | Right-click or hold action. |
| C12 | **Weight / capacity bar** | ❌ | ❌ Not justified | No weight system in code | Skip unless added to data model. |

---

## D. RPG Systems

| # | Asset | Priority | Status | Supporting System | Notes |
|---|---|---|---|---|---|
| D1 | **Character sheet** (Level, XP bar, Perk Points) | P2 | 🔲 Planned | `StatsComponent`: Level, Experience, PerkPoints, XpNeeded | Data complete. UI missing. |
| D2 | **XP progress bar** (current/needed with level labels) | P2 | 🔲 Planned | `StatsComponent.Experience`, `XpNeeded` | Should appear in both character sheet and HUD toast. |
| D3 | **Perk allocation UI** | P3 | 🔲 Planned | `StatsComponent.PerkPoints` | GDD mentions Melee Damage / Sprint Efficiency perks. No perk data model. |
| D4 | **Attribute display panel** (Health max, Stamina max) | P2 | 🔲 Planned | `AttributeComponent` attribute list | Dynamic: reads all registered attributes by name. |
| D5 | **Quest journal screen** | P2 | 🔲 Planned | No quest system code | Requires QuestManager + QuestData SO first. |
| D6 | **Active quest objective tracker** (HUD) | P2 | 🔲 Planned | No quest system | Future. |
| D7 | **Dialogue interface** | P3 | 🔲 Planned | No dialogue system code | NPC interaction present as design goal. Not in slice 1 scope. |
| D8 | **Crafting screen** | P2 | 🔲 Planned | `CraftingService` + `CraftingRecipe` SO | Backend complete. 1 recipe exists. |
| D9 | **Lore / codex screen** | P4 | ❌ Not justified | No lore data model | Future vertical. |
| D10 | **Discovery notification** (location, secret found) | P3 | 🔲 Planned | `RoomEnteredEvent` → `WorldMapService.RevealRoom` | Toast-style. |
| D11 | **Status effect panel** (active buffs/debuffs) | P2 | 🔲 Planned | `ActorState` flags | No applier system yet. |

---

## E. System Screens

| # | Asset | Priority | Status | Supporting System | Notes |
|---|---|---|---|---|---|
| E1 | **Main menu screen** | P1 | 🔲 Planned | `GameStateType.MainMenu` state stub | No UI. Requires new UIDocument + controller. |
| E2 | **Pause menu** | P1 | 🔲 Planned | `GameStateType.Paused` state stub; `OnPause` handler empty | Resume / Settings / Save / Quit buttons needed. |
| E3 | **Settings screen** (audio, graphics, controls display) | P2 | 🔲 Planned | `InputManager` has deadzone/sensitivity but no settings persistence | Nested in pause menu. |
| E4 | **Key bindings display** | P2 | 🔲 Planned | `InputManager` action list | Read-only display initially; rebinding requires InputActionAsset migration. |
| E5 | **Save / load screen** | P1 | 🔲 Planned | `FileSaveService` (single slot) | F5 save works. UI needed for manual trigger and load-on-start. |
| E6 | **Death screen** | P1 | 🔲 Planned | `ActorState.Death`, `GameStateType.GameOver` | Respawn / Main Menu options. |
| E7 | **Loading screen** | P1 | 🔲 Planned | `GameStateType.Loading`, `LevelGenerationService` | Progress indicator or atmospheric hold screen. |
| E8 | **Confirmation dialog** (Yes/No modal) | P2 | 🔲 Planned | — | Needed for quit, delete save, etc. |
| E9 | **Victory / level complete screen** | P2 | 🔲 Planned | `LevelCompletedEvent` | Triggered when player exits to Ancient Trail. |
| E10 | **Notification / toast system** | P2 | 🔲 Planned | Multiple events: `ItemCraftedEvent`, `EntityLevelUpEvent`, etc. | Stackable timed overlays. |

---

## F. Shared UI Asset Library

| # | Asset | Priority | Status | Notes |
|---|---|---|---|---|
| F1 | **Dark panel / frame background** (nine-slice capable) | P1 | 🔲 Planned | Base container bg. Must nine-slice in Unity. |
| F2 | **Decorative stone/iron border ornament** | P2 | 🔲 Planned | Theme A corner/edge piece. NanoBanana deliverable. |
| F3 | **Tactical frame border** | P2 | 🔲 Planned | Theme B equivalent. |
| F4 | **Primary button** (default + hover + pressed + disabled) | P1 | 🔲 Planned | USS states: `:hover`, `:active`, `:disabled`. |
| F5 | **Secondary / ghost button** | P2 | 🔲 Planned | Outlined, lower weight. |
| F6 | **Tab bar + tab button** | P2 | 🔲 Planned | Inventory categories, map filters, settings sections. |
| F7 | **Horizontal progress bar** (health, XP, stamina variants) | P1 | ⚠️ Partial | Health/Stamina bar exists. Needs visual redesign. |
| F8 | **Vertical divider / horizontal rule** | P2 | 🔲 Planned | Between UI sections. |
| F9 | **Slider** (styled, with thumb and track) | P2 | ⚠️ Partial | Map controls use default Slider. Style needed. |
| F10 | **Toggle** (styled checkbox/switch) | P2 | ⚠️ Partial | Map controls use default Toggle. Style needed. |
| F11 | **Scrollbar** | P2 | 🔲 Planned | Inventory grid, quest list, crafting list. |
| F12 | **Modal overlay / backdrop** | P1 | 🔲 Planned | Full-screen dim behind pause, inventory, death screens. |
| F13 | **Key glyph icons** (keyboard E, F5, M, Tab etc.) | P2 | 🔲 Planned | For interaction prompts and control hints. |
| F14 | **Entity type icons** (Player, Enemy, NPC, Interactable map sprites) | P1 | 🔲 Planned | Assigned to `WorldMapConfig` sprite fields. |
| F15 | **Item category icons** (weapon, consumable, material, quest) | P2 | 🔲 Planned | For item tooltips and inventory filters. |
| F16 | **Status effect icons** (Poison, Sleep, Electrified, Enchanted) | P2 | 🔲 Planned | 24×24 px icon sprites. |
| F17 | **Typography hierarchy** | P1 | ⚠️ Partial | Inline USS font sizes only. No shared font file or custom typeface. |
| F18 | **Color token stylesheet** (shared USS variables) | P1 | 🔲 Planned | Define CSS custom properties for theme switching. |
| F19 | **Notification / badge dot** (red unread indicator) | P3 | 🔲 Planned | For quest journal, new item alerts. |
| F20 | **Tooltip arrow pointer** | P2 | 🔲 Planned | For item tooltips anchored to slots. |
