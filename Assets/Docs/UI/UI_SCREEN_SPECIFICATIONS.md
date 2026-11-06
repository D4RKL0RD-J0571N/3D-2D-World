# UI Screen Specifications — 3D 2D World (The Lost Survivor)
**Date**: October 2026  
**Framework**: Unity UI Toolkit (UXML + USS)  
**Themes**: Dark Fantasy (A) | Militarized (B)

> All measurements at 1920×1080 reference resolution. Controller navigation support required for all screens.

---

## Screen 1: Gameplay HUD

**Controller**: `Gameplay.UI.PlayerHUD` + `Gameplay.UI.HotbarHUD` + `Gameplay.UI.WorldMapHUD`  
**Layer**: Always-on during `GameStateType.Playing`  
**Sort Order**: 0 (background layer — below all modal screens)

### 1.1 Status Bars Panel (Top-Left)

**Anchor**: `position: absolute; left: 20px; top: 20px`  
**Behavior**: Visible during gameplay. Hidden only by death screen.

```
┌──────────────────────────────────┐
│ [HP] ████████████░░░░░  100/100  │
│ [SP] ████████████░░░░░  100/100  │
└──────────────────────────────────┘
```

**Elements:**
- `StatusContainer`: dark panel, 8 px radius, 2 px iron border, 8×12 px padding.
- `HealthBar`: row — badge (HP) + track (180 px wide × 16 px tall) + fill (crimson) + label ("100 / 100").
- `StaminaBar`: row — badge (SP) + track (180 px wide × 16 px tall) + fill (amber) + label.
- Fills transition width over 100 ms.
- **Below-25% health**: health fill pulses (opacity 1.0 → 0.6 → 1.0, 1s loop via USS animation class).
- **Stamina exhausted**: border of `StaminaBar` flashes ash-orange for 200 ms.
- **Damage vignette**: full-screen `rgba(180,30,30,0)` flashes to `rgba(180,30,30,0.35)` for 180 ms on any player damage.

**Theme A differences**: Carved stone badge icons (shield for HP, boot/fist for SP). Gold-tinted border. Parchment-texture background.  
**Theme B differences**: Segmented fill (10 tick marks). Square badge glyphs. Stencil font.

---

### 1.2 Crosshair (Center)

**Anchor**: Absolute center of viewport.  
**Behavior**: Always visible during gameplay. May be hidden in dialogue or menus.

- Default: 6×6 px dot, white, 1 px dark border, 3 px radius.
- Interaction hover: expand to 10 px + show 4-segment gap reticle (4 lines, 3 px thick, 4 px gap) — triggered when `PlayerInteractionController.CurrentTarget != null`.

**Theme A**: Cross reticle, thin engraved lines, slight amber tint.  
**Theme B**: Military dot + bracket corners, phosphor green.

---

### 1.3 Interaction Prompt (Center, 56% down)

**Behavior**: Hidden (`DisplayStyle.None`) when no target. Shows when `CurrentPrompt` is non-empty.

```
         ┌─────────────────────────┐
         │  [E]  Harvest Oak Tree  │
         └─────────────────────────┘
```

- `PromptContainer`: centered, absolute, Y at 56%.
- `PromptBadge`: dark pill background, 1.5 px gold border, 6×14 px padding.
- `PromptLabel`: body text, parchment white, bold, 13 px.
- Key badge (future): 18×18 px rounded key glyph sprite ("E") + space + action text.

---

### 1.4 Hotbar (Bottom-Center)

**Anchor**: `align-items: center; justify-content: flex-end` (bottom-center), `padding-bottom: 24px`.

```
[ 1 ][ 2 ][ 3 ][ 4 ][ 5 ][ 6 ][ 7 ][ 8 ][ 9 ]
  ▲ selected (gold border, warm bg)
```

- 9 slots, each 52×52 px, 3 px margin.
- Selected slot: 3 px gold border, warm dark background.
- Slot number: top-left corner, 11 px bold, semi-transparent.
- Slot icon: 38×38 px, scale-to-fit.
- **Future**: Stack count badge (bottom-right corner, 11 px, white on dark pill).
- **Future**: Cooldown overlay (dark fill fading out on use).

**Theme A**: Iron-tray frame with riveted corners. Slot wells slightly recessed.  
**Theme B**: Flat stamped-metal grid. Square corners.

---

### 1.5 Boss Health Bar (Future — P1)

**Anchor**: Bottom-center, above hotbar by 12 px. `width: 50%; min-width: 400px`.  
**Visibility**: Only when `BossEncounterStartEvent` fires. Hidden otherwise.

```
┌─────────────────────────────────────────────────┐
│  CORRUPTED TREE SPIRIT          ██████████ 250/250 │
│  Phase: Awakened                                   │
└─────────────────────────────────────────────────┘
```

- Boss name label (header font, 16 px).
- Phase label (body font, 12 px, italic).
- Wide health bar (full panel width minus 24 px, 18 px tall).
- Fill: dark crimson with a subtle inner glow at fill edge.
- **Requires**: `BossEncounterStartEvent` event + boss health subscriber.

---

### 1.6 Status Effect Icons (Future — P2)

**Anchor**: Below status bars, left-aligned, horizontal row of 24×24 px icons with 4 px gaps.  
**Behavior**: One icon per active `ActorState` flag (Poison, Sleep, Electrified, Enchanted). Duration timer as radial overlay.  
**Requires**: Status applier systems and icon sprites.

---

### 1.7 Level-Up Toast (Future — P2)

**Anchor**: Right edge, center-Y. Slides in from right over 200 ms.

```
         ┌──────────────────────────┐
         │  ↑  LEVEL UP  →  Lv. 2  │
         │     +1 Perk Point         │
         └──────────────────────────┘
```

Auto-dismisses after 3 seconds. Stacks with other toasts.  
**Trigger**: `EntityLevelUpEvent` targeting player.

---

## Screen 2: Minimap

**Controller**: `Gameplay.UI.WorldMapHUD` (minimap mode)  
**Toggle**: Tab key  
**Anchor**: Top-right, −20 px from screen edges  
**Size**: 200×200 px

### Behavior
- Visible by default when in `Playing` state. Hidden by Tab toggle. Hidden when fullscreen map is open.
- Player always at center.
- Content scrolls as player moves (CSS `left`/`top` offset updated every frame).
- Rooms revealed progressively. Unrevealed rooms: hidden (`DisplayStyle.None`) on minimap — **future**: show as dark `UnexploredColor` rectangles instead of nothing.
- Entity icons positioned relative to player center.

### Information Hierarchy
1. Player marker (white sprite, always center)
2. Revealed room tiles (gray rectangles)
3. Revealed corridors (thin gray lines)
4. Enemy icons (red dot, 8 px)
5. NPC icons (yellow dot, 8 px)
6. Interactable icons (white dot, 8 px)

### Visual Design
- Frame: dark iron/stone border, 4 px, 8 px radius. **Future**: nine-slice ornamental border sprite.
- Background: `rgba(0,0,0,0.6)` with overflow hidden.
- **Player direction arrow** (future): 10 px arrow sprite rotated by `player.transform.eulerAngles.y`.
- Cardinal direction labels (future): N/S/E/W text at border edges, 10 px.

### Orientation
- **North = up** (world +Z axis maps to map +Y). This matches `WorldMapService.WorldToMap` which maps `worldPosition.z → mapY`.
- **Does not rotate with player** — minimap is fixed orientation.

---

## Screen 3: Fullscreen World Map

**Controller**: `Gameplay.UI.WorldMapHUD` (world map mode)  
**Toggle**: M key  
**Coverage**: Full screen with dark overlay (0.85 alpha)

### Layout (1080p)

```
┌──────────────────────────────────────────────────────────────┐
│  WORLD MAP                                     [Close]        │
│  ┌────────────────────────────────────────┐  ┌────────────┐  │
│  │                                        │  │ Opacity ─── │  │
│  │         (Map content area)             │  │ Zoom ──────  │  │
│  │                                        │  │ ☑ Player    │  │
│  │                                        │  │ ☑ Enemies   │  │
│  │                                        │  │ ☑ NPCs      │  │
│  └────────────────────────────────────────┘  └────────────┘  │
└──────────────────────────────────────────────────────────────┘
```

- Map content: fills screen minus 220 px right panel.
- Controls panel: right-aligned, 200 px wide.
- Player centered initially; pan by holding drag (future interaction).

### Controls Panel (Current — needs restyling)
- Title: `"World Map"` — 16 px header font.
- Opacity slider: 0.1–1.0 range.
- Zoom slider: 0.25–4.0 range.
- Player / Enemies / NPCs toggles.
- Close button → `ToggleWorldMap()`.

### Map Legend (Future — P2)
Small panel bottom-left of map area:
- Player icon + "Player" label
- Enemy icon + "Enemy" label
- NPC icon + "NPC" label
- Interactable icon + label
- Revealed Room + label
- Unexplored Room + label

### Fog of War (Future — P2)
- Revealed rooms: `FloorColor`.
- Unrevealed rooms: Show as semi-transparent dark tiles, not hidden. Opacity 0.3.
- Corridors to unrevealed rooms: hidden.

### Decorative Frame (Future — P2)
**Theme A**: Parchment/vellum border with torn-edge texture sprite. Compass rose (bottom-right or top-right corner). Cardinal direction labels at edges.  
**Theme B**: Printed military grid paper. Stenciled "TACTICAL MAP" header. Grid coordinate overlay.

---

## Screen 4: Inventory Screen (Future — P1)

**Controller**: `InventoryHUD` (to be built)  
**Open Binding**: Add `I` key → `ToggleInventoryInputEvent`  
**Sort Order**: 20 (above HUD, below pause)  
**Data Source**: `InventoryComponent.Slots` (20 slots max)

### Layout (1080p)

```
┌──────────────────────────────────────────────────────────────────────┐
│  INVENTORY                         CHARACTER                          │
│  ┌──────────────────────────────┐  ┌─────────────────────────────┐   │
│  │ [icon] [icon] [icon] [icon]  │  │  Jack                        │   │
│  │ [icon] [icon] [icon] [icon]  │  │  Level 1  ─────── XP 10/100 │   │
│  │ [icon] [icon] [icon] [icon]  │  │  HP: 100/100                 │   │
│  │ [icon] [icon] [icon] [icon]  │  │  SP: 100/100                 │   │
│  │ [icon] [icon] [icon] [icon]  │  │  Perk Points: 0              │   │
│  └──────────────────────────────┘  └─────────────────────────────┘   │
│                                                                        │
│  [Tooltip panel when hovering slot]                                   │
└──────────────────────────────────────────────────────────────────────┘
```

### Item Grid Slots
- 5×4 grid = 20 slots.
- Slot size: 64×64 px, 4 px gap.
- Empty slot: dark background + subtle border.
- Occupied slot: item icon (48×48 px) + stack count (bottom-right badge, 11 px).
- Hovered slot: `border-color → accent gold/olive`, slight scale-up (1.05×).
- Right-click (or hold): context menu (Use, Drop, Equip, Inspect).

### Item Tooltip
- Appears 12 px to the right of hovered slot (flips left if near screen edge).
- Contains: item name (bold, 14 px), rarity badge (future), description (12 px italic), stackable indicator.
- Dark panel background, gold border.

### Character Panel (right side)
- Actor name, level, XP progress bar.
- All confirmed attributes (HP max, SP max) from `AttributeComponent`.
- Perk Points available.
- **Future**: Equipped item slots when `EquipmentComponent` exists.

---

## Screen 5: Crafting Screen (Future — P2)

**Controller**: `CraftingHUD` (to be built)  
**Open Binding**: Add `C` key or workbench proximity → `OpenCraftingInputEvent`  
**Data Source**: `CraftingRecipe` ScriptableObjects (currently 1 recipe: `Recipe_WoodPlanks`)

### Layout
- Left panel: recipe list (scrollable).
- Right panel: selected recipe detail (ingredients with available/required counts, result preview, Craft button).
- Craft button disabled if any ingredient unsatisfied.
- `CraftItemRequestedEvent` published on craft.

---

## Screen 6: Pause Menu (Future — P1)

**Controller**: `PauseMenuHUD` (to be built)  
**Trigger**: `OnPause` handler in `InputManager` (currently empty — needs implementation)  
**State**: Pauses game time via `Time.timeScale = 0`  
**Sort Order**: 30

### Layout
```
              ┌──────────────────┐
              │   PAUSED          │
              │  [Resume]         │
              │  [Inventory]      │
              │  [Settings]       │
              │  [Save Game]      │
              │  [Quit to Menu]   │
              └──────────────────┘
```
- Centered card on dark modal overlay.
- All buttons 200 px wide, 48 px tall, 8 px gap.
- Esc / Start dismisses (Resume).

---

## Screen 7: Settings Screen (Future — P2)

**Controller**: Sub-panel within `PauseMenuHUD`  
**Sections**: Audio (master/SFX/music sliders) | Video (quality, resolution display) | Controls (key binding display, sensitivity, deadzone)

- Sensitivity and deadzone already exist in `InputManager` (`_sensitivity`, `_deadzone`) — expose to settings.
- Key bindings: read-only display in slice 1. Full rebinding requires migration from programmatic `InputAction` to `InputActionAsset`.

---

## Screen 8: Death Screen (Future — P1)

**Controller**: `DeathScreenHUD` (to be built)  
**Trigger**: `ActorState.Death` set on player → `GameStateType.GameOver`  
**Sort Order**: 50

```
             ┌────────────────────────────┐
             │                            │
             │       YOU DIED             │
             │                            │
             │   [Respawn at Checkpoint]  │
             │   [Return to Main Menu]    │
             │                            │
             └────────────────────────────┘
```
- Full-screen dark overlay, fades in over 1 second.
- Red vignette remains from damage flash, darkens further.
- "YOU DIED" in large header font (32 px, Theme A: Cinzel; Theme B: Rajdhani).
- Checkpoint respawn: reload save. "Return to Main Menu": `GameStateType.MainMenu`.

---

## Screen 9: Loading Screen (Future — P1)

**Controller**: `LoadingScreenHUD` (to be built)  
**Trigger**: `GameStateType.Loading` entry  
**Sort Order**: 60

- Full-screen dark background.
- Center: atmospheric world art or fog/shadow texture.
- Bottom: thin progress bar + loading hint text.
- **Theme A**: Canopy silhouette, flickering rune text hints.
- **Theme B**: Static / radio noise texture, tactical grid fade-in.

---

## Screen 10: Main Menu (Future — P1)

**Controller**: `MainMenuHUD` (to be built)  
**State**: `GameStateType.MainMenu`

```
         THE LOST SURVIVOR

         [New Game]
         [Continue]   (grayed if no save file)
         [Settings]
         [Quit]
```

- Atmospheric background (scene or video texture behind UI layer).
- Game title at center-top.
- Button stack centered, 220 px wide.
- `FileSaveService.HasSaveFile()` drives "Continue" enabled state.

---

## Screen 11: Confirmation Dialog (Future — P2)

**Controller**: `ConfirmationDialog` (to be built as reusable modal)

```
        ┌─────────────────────────────────┐
        │  Are you sure you want to quit? │
        │         [Confirm]  [Cancel]     │
        └─────────────────────────────────┘
```

- Modal card over current screen's backdrop.
- Trap focus inside (no interaction outside while open).
- Esc / B button = Cancel.
- Enter / A button = Confirm.
- Callable from any screen via static API: `ConfirmationDialog.Show(message, onConfirm, onCancel)`.
