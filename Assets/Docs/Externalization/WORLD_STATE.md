# RPG PROJECT — WORLD STATE & LORE EXTERNALIZATION
**Status Classification**: `CANONICAL` | `IMPLEMENTED` | `DOCUMENTED` | `INFERRED` | `PROPOSED` | `UNKNOWN` | `CONFLICTING`

---

## 1. World Identity & The Dual-Layer Divergence

A critical reality of this project is that it contains two distinct, un-reconciled narrative/worldbuilding strata:

### Layer A: "Jack & Lucian: The Silent Canopy" (`DOCUMENTED` in `Docs/GDD.md`, `Docs/GAME_DESIGN.md`)
- **Premise**: Modern portal-transported survival fantasy. Jack (modern survivor/soldier) and Lucian crash-land or are dragged through an anomalous portal into an ancient sentient forest realm called *The Silent Canopy*.
- **Setting**: Dense primal woodland, ruined stone structures overgrown by ancient vegetation, corrupted blight glades.
- **Narrative Arc**:
  1. Crash site scavenging (MRE rations, combat survival knife, entrenching tool).
  2. Following water downstream towards signs of civilization.
  3. Confronting the corrupted *Tree Spirit* (guardian corrupted by blight).
  4. Finding an ancient flagstone trail leading towards human settlement.

### Layer B: Skyrim Nordic Fantasy & Faction Kingdom (`IMPLEMENTED` in `Assets/Scripts/Domain/Skyrim/` and `RPGProvingGrounds.unity`)
- **Premise**: Nordic/Medieval European high fantasy world with feudal kingdoms, holds, taverns, bandit camps, and ancient Nordic tomb barrows.
- **Locations & Geography**:
  - `Riverwood`, `Whiterun`, `Bleak Falls Barrow`, `Sleeping Giant Inn` (explicitly used as test seeds and POI targets in `RadiantQuestService.cs`).
  - Cobblestone village squares, blacksmith forges, village taverns, fortress walls, and iron gates (assembled in `RPGProvingGrounds.unity`).
- **Factions (`IMPLEMENTED` in `FactionService.cs`)**:
  - `Player`: The protagonist.
  - `Guards`: Law enforcers of the towns; hostile to Bandits and Draugr; retaliate if provoked by Player.
  - `Bandits`: Outlaws attacking Player and Townsfolk on sight.
  - `Draugr`: Undead guardians inhabiting barrows and tombs; hostile to all living beings.
  - `Beasts`: Wildlife hostile to Player and Neutral NPCs.
  - `Neutral`: Merchants, blacksmiths, innkeepers, commoners.

---

## 2. World Conflict & Magic
- **Magic**: Implemented as skill schools (`Destruction`, `Restoration`, `Alteration`, `Conjuration`, `Illusion`, `Enchanting`).
- **Supernatural Principles**:
  - Blight/Corruption (from Layer A): corrupts natural spirits into hostile entities.
  - Undead Reanimation (from Layer B): ancient Draugr rise to defend crypts and relics.
- **Current Ambiguity**: Whether Jack is the protagonist in the Skyrim world (portal fantasy) or if the project has pivoted completely to a classic medieval fantasy protagonist.
