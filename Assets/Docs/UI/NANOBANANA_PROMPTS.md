# NanoBanana Image Generation Prompts — 3D 2D World (The Lost Survivor)
**Date**: October 2026  
**Purpose**: Visual direction, concept art, and asset reference for UI implementation  
**Important**: These are flat mockup images. All functional UI must be rebuilt as Unity UI Toolkit elements. Identify per prompt which parts need individual sprites vs. runtime elements.

---

## How to Read These Prompts

Each prompt specifies:
- **Purpose**: What this image is for
- **Theme**: A (Dark Fantasy) or B (Militarized), or Both
- **Resolution**: Target render resolution
- **Composition**: What the image should contain
- **Style**: Visual direction
- **Must include**: Required content
- **Must NOT include**: Explicit exclusions
- **Implementation note**: How this translates to Unity

---

## Prompt 01 — Gameplay HUD (Theme A: Dark Fantasy)

**Purpose**: Full gameplay HUD mockup for Dark Fantasy theme visual direction  
**Theme**: A — Dark Fantasy ("The Realm")  
**Resolution**: 1920×1080 px  
**Aspect Ratio**: 16:9

**Composition**:
- Mostly empty dark background (representing game world behind UI).
- Top-left: compact health and stamina status bar panel.
- Bottom-center: 9-slot hotbar with one slot highlighted.
- Center: minimal crosshair dot.
- Center (56% down): contextual interaction prompt pill.

**Style**:
- Dark, heavy, weathered. Blackened iron panel edges with tarnished brass rivet corners.
- Backgrounds: very dark charcoal (`#13131A`) with slight grain/stone texture.
- Borders: aged iron (`#3A3A48`) default; ember gold (`#C9962A`) on selected/active.
- Health bar: dark crimson fill (`#B52B2B`), track in very dark slate.
- Stamina bar: amber-forged fill (`#C98C20`), same track.
- Bar badges: small carved shield rune for HP; boot/fist rune for SP.
- Font: Cinzel or a sharp serif with slight weathering. NOT gothic blackletter.
- Hotbar: iron-tray style. Slot wells slightly recessed with riveted corner accents. Selected slot has gold border glow.
- Crosshair: thin stone-engraved cross, 4 gap lines, very slight amber tint at center.
- Interaction prompt: dark pill background, 1.5 px tarnished gold border, parchment-white text.

**Color Palette**: `#0C0C0E` (void), `#13131A` (panel), `#3A3A48` (border), `#C9962A` (gold), `#B52B2B` (health), `#C98C20` (stamina), `#EDE8DC` (text), `#9A9688` (secondary text)

**Must include**:
- Status bar panel top-left with both HP and SP bars showing partial fill (~65% health, ~80% stamina)
- Numeric labels inside bars ("65 / 100" and "80 / 100")
- HP and SP badge labels
- 9-slot hotbar at bottom-center, slot 3 selected (gold border), slots 1–3 showing small block texture icons, rest empty
- Small crosshair at exact screen center
- Interaction prompt: "[E]  Harvest Oak Tree" at 56% screen height

**Must NOT include**:
- Neon glows, purple/blue magic effects
- Excessive ornament that obscures readability
- Any gothic blackletter or illegible fonts
- Floating numbers or damage popups
- Any quest tracker, minimap, or boss bar (those are separate prompts)

**Implementation note**: Health bar, stamina bar, crosshair, and interaction prompt must be rebuilt as UXML/USS elements — do NOT use this image as a sprite. Only the panel frame background and any corner/edge ornaments would be delivered as separate nine-slice sprites.

---

## Prompt 02 — Gameplay HUD (Theme B: Militarized)

**Purpose**: Full gameplay HUD for Militarized / Earth-side theme  
**Theme**: B — Militarized  
**Resolution**: 1920×1080 px

**Composition**: Same layout as Prompt 01. Same element positions.

**Style**:
- Gunmetal panels (`#111318`), matte olive-drab borders (`#556B2F`), sharp 90° corners.
- All bars segmented: 10 evenly spaced tick marks on each progress track.
- Health fill: deep military red (`#8B1A1A`). Stamina fill: caution amber (`#B87A10`).
- Font: DIN Condensed or Rajdhani. Clean, tight, technical.
- Bar badges: stencil-cut letters "HP" and "SP", white on dark. No rune icons.
- Hotbar: stamped metal plate grid, square slots, phosphor-green slot number labels.
- Crosshair: military dot with bracket corner reticle. Phosphor green.
- Interaction prompt: monospace font, tight letterforms, olive border.

**Color Palette**: `#08090C` (ops black), `#111318` (gunmetal), `#2E3340` (steel), `#556B2F` (olive), `#8B1A1A` (health), `#B87A10` (stamina), `#E8EAE4` (field white), `#6DBE6D` (phosphor)

**Must include**: Same elements as Prompt 01 with Theme B styling. Segmented bar fills clearly visible.  
**Must NOT include**: Fantasy ornamentation, curves on panels, serif fonts.

**Implementation note**: Same as Prompt 01 — all bars are runtime elements. Deliver panel background sprites and segment tick sprites separately.

---

## Prompt 03 — Minimap (Theme A: Dark Fantasy)

**Purpose**: Minimap visual design concept for Dark Fantasy theme  
**Theme**: A  
**Resolution**: 400×400 px (200×200 logical px, delivered at 2× for detail)

**Composition**:
- Square map, 200×200 logical size.
- 5–7 revealed room rectangles in gray-blue tones, connected by thin corridor lines.
- 2–3 unrevealed rooms shown as very dark near-black fill (not absent — present but obscured).
- Player marker: small bright white arrow or dot at center.
- Enemy marker: small red dot in one corner of the map.
- NPC marker: small yellow dot near a room.
- Decorative frame: outer border of blackened stone or tarnished iron with slight engraving. 4 cardinal direction marks (N/S/E/W) etched onto the border at compass positions.

**Style**:
- Revealed rooms: `#595965` (dungeon stone) on dark background.
- Corridors: `#474754` thin lines.
- Unexplored: `#050508` near-black fill (slightly visible so player knows space exists).
- Player arrow: bright white, pointing upward (facing north).
- Frame: rough-cut stone or hammered iron with subtle surface texture. NOT a photograph — stylized illustration.
- No labels inside the map itself. Cardinal letters on frame only.

**Must include**: At least 5 revealed rooms, 2 dark unrevealed rooms, corridors between revealed, player icon (center, up-facing arrow), 1 enemy icon, 1 NPC icon, decorative stone/iron frame, N/S/E/W cardinal marks.  
**Must NOT include**: Any text labels inside the map area. No room names. No quest markers. No grid overlay.

**Delivery as sprites needed**:
1. Minimap frame as nine-slice PNG (transparent center, decorated border only)
2. Player direction arrow icon (16×16 or 32×32, transparent background)
3. Enemy icon (12×12, transparent)
4. NPC icon (12×12, transparent)

---

## Prompt 04 — Minimap (Theme B: Militarized)

**Purpose**: Minimap for Militarized theme  
**Theme**: B  
**Resolution**: 400×400 px

**Style**: Same layout as Prompt 03 but with tactical/military aesthetics.
- Map area background: dark ops black `#08090C`.
- Rooms: flat phosphor-green outline rectangles, very thin lines, no fill (or near-transparent fill).
- Corridors: thin single-pixel green lines.
- Frame: square tactical compass housing. Stamped metal surface. Corner bracket mounts. "TACTICAL MAP" or grid markings etched on border.
- Player marker: green triangle pointing toward facing direction.
- Enemy marker: red diamond/cross.
- NPC: yellow square.
- Unexplored: rooms not shown at all (pure military doctrine: only known terrain is rendered).

**Must NOT include**: Stone, runes, organic shapes, warm colors.

---

## Prompt 05 — Fullscreen World Map (Theme A: Dark Fantasy)

**Purpose**: Fullscreen exploration map for Dark Fantasy theme  
**Theme**: A  
**Resolution**: 1920×1080 px

**Composition**:
- Map content area: 1700 px wide, fills left portion of screen.
- Right panel (200 px): map controls column.
- Map content shows a procedurally generated dungeon layout — 8–12 rooms, varied sizes, multiple corridors, 2–3 unrevealed dark regions.

**Map Visual Style (Theme A)**:
- Background: ancient parchment or aged vellum texture — warm dark sepia, NOT bright. Think 19th century expedition journal left in a damp cave.
- Revealed rooms: drawn as irregular stone chambers with rough edges. Warm gray fill (`#595965`). Thin inner wall lines suggesting room edges.
- Corridors: hand-drawn thin lines, slightly wobbled (NOT perfectly straight CAD lines).
- Unexplored zones: dark ink blot / smear treatment. Present but illegible.
- Decorative border: full-width parchment/leather border frame. Torn edge treatment on inner edge. Small compass rose (bottom-right, 80×80 px). Cardinal direction labels in calligraphic serif.
- Player marker: golden arrow or wax-seal icon at player position.
- Enemy markers: red skull-and-crossbones or crossed daggers symbol.

**Right Controls Panel**:
- Dark stone/iron inset panel.
- Title: "WORLD MAP" in Cinzel, 16 px.
- Opacity, zoom sliders with styled brass/iron thumbs.
- Player/Enemies/NPCs toggle rows.
- "Close" button — iron bracket button style.

**Must include**: Parchment map background with vellum texture, at least 8 rooms of varied sizes, corridors, 2 dark fog-of-war zones, player marker, 2 enemy markers, decorative border with compass rose, right controls panel.  
**Must NOT include**: Clean white background. Grid lines. CAD-straight corridors. Modern UI widgets (native sliders/toggles visible).

**Delivery as sprites needed**:
1. Parchment background texture (seamless, for map area) — can be a Texture2D applied to `_worldMapContainer.style.backgroundImage`.
2. Map frame border (nine-slice PNG).
3. Compass rose sprite.
4. Player map icon.
5. Enemy map icon.

---

## Prompt 06 — Fullscreen World Map (Theme B: Militarized)

**Purpose**: Fullscreen tactical map for Militarized theme  
**Theme**: B  
**Resolution**: 1920×1080 px

**Style**: Military tactical map / field grid paper.
- Background: dark olive/gunmetal with printed grid lines visible (5 px grid cells, `rgba(80,90,70,0.3)` lines).
- Header strip: "TACTICAL OPERATIONS MAP — SECTOR UNKNOWN" in stencil font.
- Rooms: rectangular outlines in phosphor green. Fill very dark (`rgba(40,60,30,0.4)`).
- Corridors: straight dashed green lines with arrow direction indicators.
- Unexplored areas: hatched void (`// // //` crosshatch pattern in very dark olive).
- Player marker: pulsing green circle with central dot.
- Enemy markers: red triangle with exclamation.
- Controls panel: metal clipboard panel with toggle switches (physical rocker style).

**Must NOT include**: Parchment, calligraphy, warm tones, organic shapes, runes.

---

## Prompt 07 — Inventory Screen (Theme A: Dark Fantasy)

**Purpose**: Inventory screen layout and visual direction for Dark Fantasy theme  
**Theme**: A  
**Resolution**: 1920×1080 px

**Composition**:
- Game world visible but darkened behind a full-screen dim overlay.
- Center-left: inventory grid panel (5 columns × 4 rows = 20 slots).
- Center-right: character information panel (name, level, XP bar, attributes).
- Bottom: item tooltip shown for one hovered item.

**Inventory Panel**:
- Dark stone/iron panel with ornamental carved border.
- 20 slots in a 5×4 grid. Each slot 64×64 px.
- 6 slots occupied with various item icons (axe, log, plank, rations, amulet, stone chunk).
- Stack count badge on stackable items (bottom-right corner, small dark pill with white number).
- One slot hovered: gold border glow + slight scale up.
- Empty slots: subtle etched outline only.

**Tooltip Panel (for hovered item)**:
- Floats to the right of hovered slot.
- Shows: Item Name (bold, 16 px Cinzel), Item Type (12 px italic), Description (12 px body), Stack: "3 / 10".
- Dark iron panel with amber border.

**Character Panel**:
- Jack's silhouette or name plate at top.
- Level badge: "Lv. 1".
- XP progress bar with "10 / 100 XP" label.
- HP: 100/100, SP: 100/100.
- Perk Points: 0.

**Must include**: 20-slot inventory grid, 6 occupied slots with recognizable RPG item icons, stack count on stacked items, visible tooltip for one item, character panel with level and XP bar, ornamental dark frame, gold accent borders.  
**Must NOT include**: Equipment slot panel (not implemented), weight bar, currency display, white or light backgrounds.

---

## Prompt 08 — Hotbar and HUD Components Sheet (Both Themes)

**Purpose**: Asset reference sheet showing all individual HUD component states for both themes  
**Theme**: A + B side by side  
**Resolution**: 2048×1024 px (side-by-side comparison sheet)

**Left half (Theme A)**: Dark Fantasy  
**Right half (Theme B)**: Militarized

**Show these elements for each theme (stacked vertically):**
1. Health bar — full, at 50%, at 20% (critical pulse)
2. Stamina bar — full, at 40%, at 0% (exhaustion flash)
3. Single hotbar slot — empty, occupied with icon, selected, selected+occupied
4. Crosshair — normal, interaction hover state
5. Interaction prompt — style and text
6. Status effect icon row (Poison, Sleep, Electrified, Enchanted placeholder icons)
7. Level-up toast notification
8. Slot stack count badge (show on a filled slot)

**Style**: Same color palettes as Prompts 01 and 02. All elements isolated on transparent-like dark neutral background.

**Delivery as sprites needed from this sheet**:
- All icon glyphs (HP badge, SP badge, each status effect)
- Slot frame sprite (empty slot background)
- Selected slot glow/border overlay
- Toast notification background
- Key glyph "E" button icon

---

## Prompt 09 — Shared UI Component Sheet (Theme A)

**Purpose**: Complete shared component library reference for Dark Fantasy theme  
**Theme**: A  
**Resolution**: 2048×2048 px

**Arrange in a grid layout with labels:**

**Row 1 — Buttons**:
- Primary button: default, hover, pressed, disabled (4 states)
- Secondary/ghost button: default, hover, pressed, disabled

**Row 2 — Panels and Frames**:
- Small panel (200×100 px example)
- Medium panel (400×200 px example)
- Large panel (600×300 px example)
- Minimap frame (200×200)
- Dialog box frame (500×200)

**Row 3 — Progress Elements**:
- Horizontal bar (100%, 60%, 25%)
- Vertical bar (100%, 60%)
- XP progress bar style (thinner, with percentage label)
- Slider with thumb and track

**Row 4 — Interactive Controls**:
- Toggle/checkbox: on, off states
- Tab bar (3 tabs, first active)
- Scrollbar (vertical)
- Dropdown (closed, open states)

**Row 5 — Indicators and Badges**:
- Notification dot (red)
- Stack count badge (number 3, number 99)
- Rarity borders: 4 tiers (common gray, uncommon green, rare blue, epic gold — future)
- Key glyph examples: [E], [F5], [M], [Tab], [Esc]

**Row 6 — Icons**:
- Map markers: Player, Enemy, NPC, Interactable (all 4, 24×24)
- Status effects: Poison (purple drop), Sleep (moon/zzz), Electrified (lightning), Enchanted (star/sparkle)
- Action icons: Attack, Interact, Run, Jump, Crouch

**Style**: Dark Fantasy palette. All components isolated on neutral dark background with label captions.

**Must NOT include**: White backgrounds on any component. Modern/flat design aesthetics. Sans-serif fonts.

---

## Prompt 10 — Pause Menu and System Screens (Theme A)

**Purpose**: Pause menu and death screen visual direction  
**Theme**: A  
**Resolution**: 1920×1080 px (deliver as two separate panels side by side on a 3840×1080 canvas)

**Left panel — Pause Menu (1920×1080)**:
- Dark modal overlay behind a centered card.
- Card: carved stone or iron panel with ornamental top border.
- Title: "PAUSED" in Cinzel, 20 px.
- 5 buttons: Resume, Inventory, Settings, Save Game, Quit to Main Menu.
- Buttons: 220 px wide, iron-bar style with gold hover accents.
- Subtle atmospheric background: barely visible forest/ruin silhouette behind the overlay.

**Right panel — Death Screen (1920×1080)**:
- Full-screen very dark near-black.
- Faint red vignette at edges (from damage — should feel like the screen is still bleeding).
- Large center text: "YOU DIED" in weathered Cinzel, 48 px. Should feel heavy, not flashy.
- Two buttons: "Try Again" and "Return to Menu".
- Optional: faint background of the ancient corrupted forest silhouette, barely visible.

**Must NOT include**: Jump scares, excessive red. "GAME OVER" text (use "YOU DIED"). Bright colors. Encouraging friendly tone. Modern flat button style.

---

## Prompt 11 — Loading Screen (Theme A + B)

**Purpose**: Loading screen concepts for both themes  
**Theme**: A + B (two separate images)  
**Resolution**: 1920×1080 px each

**Theme A Loading Screen**:
- Full-screen atmospheric dark illustration: silhouette of an ancient enormous forest canopy against a deep indigo-teal sky. Bioluminescent flora glowing faintly at ground level.
- Center-bottom: thin progress bar (400 px wide, 8 px tall), dark with amber fill.
- Below bar: rotating loading hint text (tip about mechanics) in 12 px Cinzel italic.
- Top-center: Game title treatment — "THE LOST SURVIVOR" in large spaced Cinzel.
- No other UI elements. Pure atmosphere.

**Theme B Loading Screen**:
- Full-screen dark gunmetal/ops black.
- Subtle animated scan line texture suggestion.
- Center: "INITIALIZING..." in large stencil Rajdhani.
- Below: "LOADING TACTICAL DATA" + progress bar (phosphor green fill, segmented).
- Corner: CLASSIFIED stamp watermark (faint, 15° angle, dark red).
- Bottom: Loading tips in Share Tech Mono 12 px.

**Must NOT include (Theme A)**: Modern UI, clean white, cheerful colors.  
**Must NOT include (Theme B)**: Fantasy elements, serif fonts, warm colors.

---

## Prompt 12 — Map Icon Sprite Sheet

**Purpose**: All map and HUD icon sprites for direct use in Unity  
**Theme**: A (Dark Fantasy) — will double as base; Theme B uses same shapes with different color treatment  
**Resolution**: 512×512 px sprite sheet (individual icons arranged on a grid)

**Grid layout (8×8 grid of 64×64 px cells)**:

**Row 1 — Map entity markers (all on transparent bg)**:
- Player: forward-pointing arrow (12 px), white fill
- Enemy: downward triangle, red fill
- NPC: circle with inner dot, yellow fill
- Interactable: diamond shape, white fill
- Custom/unknown: star, gray fill

**Row 2 — Status Effect Icons (24×24 px, shown at 2×)**:
- Poison: green dripping orb
- Sleep: crescent moon with small "Z"
- Electrified: yellow lightning bolt
- Enchanted: 4-point star with glow
- InCombat: crossed swords silhouette
- Death: skull outline

**Row 3 — Action/Key Glyphs**:
- Keyboard keys: [E], [M], [Tab], [F5], [Esc], [Space], [LShift], [LCtrl]
- Each on a square dark key background with white glyph
- Gamepad: A/B/X/Y face buttons, RT, LT icons

**Row 4 — Item Category Icons**:
- Weapon (sword silhouette)
- Consumable (flask/potion shape)
- Material/Resource (cube/crystal)
- Quest item (scroll with ribbon seal)
- Armor (chest plate)

**Row 5 — HUD Badge Icons**:
- HP: small shield or heart (engraved style)
- SP: boot/sprint or flame
- XP: star or gem
- Perk: gear/cog

**Style**: Thin engraved line art style. White lines on transparent background (can be tinted in Unity). Each icon must be recognizable at 12×12 px minimum. No gradients. No realistic shading. Clean silhouette reads are required.

**Delivery**: Full sprite sheet PNG (512×512) + individual atlas slicing data. Each cell is 64×64 px with 4 px padding.
