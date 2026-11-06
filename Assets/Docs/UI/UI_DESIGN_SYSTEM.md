# UI Design System — 3D 2D World (The Lost Survivor)
**Date**: October 2026  
**Framework**: Unity UI Toolkit (UXML + USS + UIDocument)  
**Render Pipeline**: URP 17.3.0 (ACES tonemapping, volumetric fog, bloom)

---

## 1. Design Philosophy

The UI serves a game about **tension, discovery, and survival in an ancient hostile world**. Visual design principles:

1. **Readability first** — Every element must be legible at a glance during active combat. Never sacrifice function for ornament.
2. **Atmosphere through restraint** — Darkness, weathered materials, and minimal gold accents evoke danger without visual noise. No neon, no excessive glow.
3. **Transparency of systems** — The player should always know their health, stamina, what they're holding, and what they can do. UI is not decorative — it is a tactical tool.
4. **Common architecture, two skins** — Both visual themes (Dark Fantasy / Militarized) share identical information architecture, element IDs, USS class structure, and interaction conventions. Only color palettes, ornamental textures, and typography differ.

---

## 2. Visual Themes

### Theme A: Dark Fantasy — "The Realm"

Inspired by ancient cartography, blackened iron fittings, weathered stone, tarnished brass, and occult map-making traditions. The UI looks like equipment made by scholars and hunters who live in this world — functional objects worn by time.

**Mood references**: Dark Souls message stones, Morrowind journal, Darkwood inventory, Disco Elysium skill panels.

#### Theme A Color Palette

| Token | Name | Hex | Usage |
|---|---|---|---|
| `--color-bg-deep` | Void Black | `#0C0C0E` | Full-screen backgrounds, modal overlays |
| `--color-bg-panel` | Slate Obsidian | `#13131A` | Panel / frame backgrounds |
| `--color-bg-panel-alt` | Iron Charcoal | `#1C1C24` | Track backgrounds, slot backgrounds |
| `--color-border-primary` | Tarnished Iron | `#3A3A48` | Default element borders |
| `--color-border-accent` | Aged Brass | `#8B6F3A` | Hover, active, selected states |
| `--color-border-focus` | Ember Gold | `#C9962A` | Selected slot highlight, active element |
| `--color-health-fill` | Crimson Steel | `#B52B2B` | Health bar fill |
| `--color-health-low` | Blight Red | `#FF1A1A` | Health < 25% pulse color |
| `--color-stamina-fill` | Amber Forge | `#C98C20` | Stamina bar fill |
| `--color-stamina-depleted` | Ash Orange | `#FF4500` | Stamina exhaustion flash border |
| `--color-text-primary` | Parchment White | `#EDE8DC` | Primary labels |
| `--color-text-secondary` | Stone Gray | `#9A9688` | Secondary labels, hints |
| `--color-text-accent` | Runic Gold | `#D4A847` | Header labels, selected slot number |
| `--color-text-danger` | Bloodstone | `#E04444` | HP badge, death warnings |
| `--color-interactive` | Brass Glow | `#A07840` | Button default |
| `--color-interactive-hover` | Polished Bronze | `#C49A50` | Button hover |
| `--color-interactive-pressed` | Ember | `#8B5E2A` | Button pressed |
| `--color-interactive-disabled` | Slate Mute | `#4A4A55` | Disabled state |
| `--color-fog` | Unexplored Void | `#050508` | Map fog-of-war tiles |
| `--color-map-floor` | Dungeon Stone | `#595965` | Revealed room tiles |
| `--color-map-corridor` | Passage Gray | `#474754` | Corridor lines |

#### Theme A Typography

| Role | Font | Size | Weight | Letter Spacing | Usage |
|---|---|---|---|---|---|
| **HUD Badge** | Cinzel (or fallback monospace) | 11 px | Bold | +0.05 em | HP / SP / slot number glyphs |
| **HUD Label** | Cinzel | 12 px | Regular | +0.02 em | Value text inside bars |
| **Body / Prompt** | EB Garamond (or system serif) | 13–14 px | Regular | +0.01 em | Interaction prompts, tooltips |
| **Panel Header** | Cinzel | 16 px | Bold | +0.08 em | Screen titles, map title |
| **Section Header** | Cinzel | 13 px | Regular | +0.06 em | Tab labels, section dividers |
| **Item Name** | Cinzel | 14 px | Bold | +0.03 em | Item tooltips, crafting names |
| **Item Description** | EB Garamond | 12 px | Italic | 0 | Flavour and mechanical text |
| **Numeric / Value** | Bebas Neue (or monospace) | 13–20 px | Regular | +0.1 em | XP values, damage numbers |
> **Note**: All fonts must be imported as TrueType assets and assigned in `PanelSettings` or via USS `font`. Unity UI Toolkit does not support web fonts at runtime. Cinzel is available as an open-license Google Font. If unavailable at build time, substitute with a bold serif fallback.

#### Theme A Ornamental Rules
- **Panel frames**: Decorated corners (carved stone / brass rivets). Implemented as nine-slice sprites with fixed 12–16 px corner regions.
- **Section dividers**: Single-pixel horizontal line with center-fade (lighter at edges). Apply as `border-bottom` on USS, or as a sprite.
- **Health bar badge**: Small carved rune or shield icon adjacent to HP label.
- **Map frame (fullscreen)**: Parchment/vellum edge with torn edge treatment, compass rose in corner, cardinal direction labels.
- **Minimap frame**: Circular or rounded-square dark iron border. Inner ring compass markings (N/S/E/W) at cardinal positions.
- **Hotbar frame**: Iron tray style — slightly recessed slot wells with riveted edges.
- **No neon glows.** Emissive accents limited to health-critical states. No purple or blue magical glow on base UI chrome.

---

### Theme B: Militarized — "Earth Side"

Worn tactical field equipment. Jack's interface — the HUD elements that represent his surviving gear, his soldier's discipline, and the technological contrast with the magical world.

**Mood references**: STALKER inventory, Escape from Tarkov UI, Alien: Isolation terminal screens, Metal Gear Solid codec.

#### Theme B Color Palette

| Token | Name | Hex | Usage |
|---|---|---|---|
| `--color-bg-deep` | Ops Black | `#08090C` | Full-screen backgrounds |
| `--color-bg-panel` | Gunmetal | `#111318` | Panel / frame backgrounds |
| `--color-bg-panel-alt` | Field Gray | `#1A1D22` | Track backgrounds |
| `--color-border-primary` | Steel Matte | `#2E3340` | Default element borders |
| `--color-border-accent` | Tactical Olive | `#556B2F` | Hover / selected states |
| `--color-border-focus` | ACU Green | `#6A8C4A` | Selected, active focus |
| `--color-health-fill` | OD Red | `#8B1A1A` | Health bar fill |
| `--color-stamina-fill` | Caution Amber | `#B87A10` | Stamina bar fill |
| `--color-text-primary` | Field White | `#E8EAE4` | Primary labels |
| `--color-text-secondary` | Drab Khaki | `#8A8E7C` | Secondary, hints |
| `--color-text-accent` | Phosphor Green | `#6DBE6D` | Header labels, emphasis |
| `--color-text-danger` | Alert Red | `#CC2222` | Low health warnings |
| `--color-interactive` | Tactical Olive | `#4A6030` | Button default |
| `--color-interactive-hover` | ACU Highlight | `#5C7A3C` | Button hover |
| `--color-interactive-pressed` | Combat Shadow | `#384820` | Button pressed |
| `--color-interactive-disabled` | Fatigues Gray | `#3A3E46` | Disabled state |

#### Theme B Typography

| Role | Font | Size | Weight | Usage |
|---|---|---|---|---|
| **HUD Badge** | Rajdhani / DIN Condensed | 11 px | Bold | HP / SP badges, slot numbers |
| **HUD Label / Numeric** | Rajdhani | 12–14 px | Regular | Values, telemetry |
| **Body / Prompt** | Share Tech Mono | 13 px | Regular | Interaction prompts |
| **Panel Header** | Rajdhani | 16 px | Bold | Screen titles, map labels |
| **Item Name** | Rajdhani | 14 px | SemiBold | Item tooltips |
| **Item Description** | Share Tech Mono | 12 px | Regular | Mechanical properties |

#### Theme B Ornamental Rules
- **Panel frames**: Stamped metal plates with bolt-head corners. Sharp right angles. No curves except on progress bars.
- **Section dividers**: Double-line military rule (thin + thick). `border-bottom: 2px + 1px` offset.
- **Map frame (fullscreen)**: Grid-paper tactical map. Printed-on grid lines, stenciled region labels, military grid coordinate overlay.
- **Minimap frame**: Square compass rose; cardinal directions stenciled. Corner bracket fittings.
- **HUD bars**: Segmented fills (every 10% shows a tick mark) rather than smooth gradient.
- **No ornamental curves or fantasy motifs.**

---

## 3. Common Information Architecture

Both themes share identical:
- Element names, USS class names, and UXML hierarchy
- Z-layering order: HUD → Hotbar → Minimap → Fullscreen overlays (Map, Inventory, Pause) → Modal dialogs → Death/Loading screens
- Interaction model: hover → active → focus → disabled
- Grid and spacing system

---

## 4. Spacing and Sizing System

All measurements are in **logical pixels at 1080p reference**. UI Toolkit `PanelSettings` should use **Screen Space – Overlay** with **Scale Mode: Scale With Screen Size** and reference resolution **1920×1080**.

| Token | Value | Usage |
|---|---|---|
| `--spacing-xs` | 4 px | Icon padding, tight gaps |
| `--spacing-sm` | 8 px | Element inner padding |
| `--spacing-md` | 12 px | Standard gap between siblings |
| `--spacing-lg` | 20 px | Section spacing, panel padding |
| `--spacing-xl` | 32 px | Major layout separations |
| `--radius-sm` | 4 px | Slot corners, small elements |
| `--radius-md` | 6–8 px | Panel corners |
| `--radius-lg` | 12 px | Large containers |
| `--border-thin` | 1 px | Dividers, subtle outlines |
| `--border-standard` | 2 px | Standard element borders |
| `--border-thick` | 3 px | Selected state borders |

---

## 5. Layout Anchors at 1080p

| Element | Position | Size |
|---|---|---|
| **Status bars panel** (HP + SP) | Top-left: (20, 20) | Flexible width ~220 px |
| **Hotbar** | Bottom-center, 24 px from bottom | 9 × 52 px slots + 6 px gaps = ~530 px wide |
| **Minimap** | Top-right: (−20, −20) offset | 200×200 px |
| **Crosshair** | Absolute center | 6–10 px |
| **Interaction prompt** | Center-X, 56% down | Auto-width pill |
| **Enemy health bar** | Above enemy, world-space | 120×10 px |
| **Boss health bar** | Bottom-center, above hotbar | Full-width panel ~600 px |
| **Notifications / toasts** | Right edge, center-Y | 280 px wide, stacked |
| **HUD status effects** | Below status bars | Row of 24×24 icons |

### Responsive Scaling
- **1080p** (1920×1080): Reference resolution. All measurements above apply.
- **1440p** (2560×1440): UI scales proportionally. Minimap remains 200 px logical.
- **4K** (3840×2160): Full scale. Status bars appear slightly further from edges due to fixed offsets — adjust to viewport-relative `%` anchors.
- **Ultrawide (21:9)**: Anchors must use left/right offsets from screen edges, not fixed absolute positions. Hotbar must remain centered. WorldMap must account for full width.
- **1280×720**: Text should remain legible. Minimum font size 10 px. Hotbar slots scale down to 44×44 px.

---

## 6. Icon System

### Icon Grid
- **HUD status icons**: 24×24 px, 2 px border, transparent background.
- **Hotbar slot icons**: 38×38 px within a 52×52 px slot.
- **Map entity markers**: 8–12 px at minimap scale; 14–18 px on fullscreen map.
- **Key glyph icons**: 18×18 px for keyboard key badges in prompts.
- **Item icons**: 64×64 px at 1× (displayed at 48×48 in slot, 80×80 in tooltip).

### Icon Style Rules
- **Theme A**: Hand-drawn engraved/etched aesthetic. Thin, even line weight. Off-white on dark background.
- **Theme B**: Military stencil / silhouette style. Flat, high-contrast. Phosphor-green or white on dark background.
- All icons must be delivered with **transparent background**.
- Line icons preferred over filled for HUD; filled silhouettes for map markers.
- All icons must read clearly at minimum 24×24 px.

---

## 7. Panel and Nine-Slice Rules

Every panel background sprite must be designed as a **nine-slice** asset:
- **Minimum sprite size**: 64×64 px for simple panels; 128×128 px for ornamental frames.
- **Fixed corner region**: 16×16 px for simple; 24×24 px for ornamental corners.
- **Stretch region**: Center 1×1 px for solid fill; center texture for noise/grain.
- **USS application**: `border-image` with `border-image-slice` matching fixed pixel values.
- **Delivery**: Each frame sprite must be a single PNG with all four corners and edges defined. No separate corner pieces.

---

## 8. State Definitions (USS)

Every interactive element must implement all four states:

```uss
/* Default */
.ui-button { background-color: var(--color-interactive); }

/* Hover */
.ui-button:hover { background-color: var(--color-interactive-hover); }

/* Pressed / Active */
.ui-button:active { background-color: var(--color-interactive-pressed); }

/* Disabled */
.ui-button:disabled { background-color: var(--color-interactive-disabled); opacity: 0.5; }

/* Focus (keyboard nav) */
.ui-button:focus { border-color: var(--color-border-focus); border-width: 2px; }
```

---

## 9. Transition Animations

| Element | Transition | Duration | Easing |
|---|---|---|---|
| Health / Stamina bar fill width | `width` | 100 ms | ease-out |
| Damage vignette fade | `background-color` | 120 ms / 300 ms | linear |
| Stamina exhaustion border | `border-color` | 200 ms | ease-out |
| Button hover | `background-color` | 80 ms | ease |
| Panel enter/exit | `opacity` + `scale` | 150 ms | ease-out |
| Toast notification slide | `translate` + `opacity` | 200 ms in / 400 ms out | ease-out |
| Map fade on toggle | `opacity` | 200 ms | ease |

---

## 10. Accessibility Baseline

- All text must meet **WCAG AA contrast** against its background (minimum 4.5:1 for body; 3:1 for large text).
- Interactive elements must have a visible **focus ring** for keyboard navigation.
- No information conveyed by color alone — use icons + labels in addition to color.
- Minimum touch target size: **44×44 px** (even on PC, keeps elements comfortable for controller navigation).
- Screen Reader support: not in scope for slice 1, but USS `name` attributes on all interactive elements enables future accessibility labelling.
