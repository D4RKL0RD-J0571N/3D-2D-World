# RPG PROJECT — NARRATIVE & STORY EXTERNALIZATION
**Status Classification**: `CANONICAL` | `IMPLEMENTED` | `DOCUMENTED` | `INFERRED` | `PROPOSED` | `UNKNOWN` | `CONFLICTING`

---

## 1. Documented Narrative Arcs

### The Silent Canopy ("Jack & Lucian")
- **Protagonist**: Jack (military specialist / survivalist transported through an anomalous rift).
- **Secondary Character**: Lucian (squadmate / companion whose status is documented as either wounded, separated, or lost in the anomaly).
- **Inciting Incident**: Catastrophic atmospheric or temporal rift transports modern personnel into an untouched primeval realm where natural flora and fauna possess ancient magical sentience.
- **Story Beats Documented (`Docs/GDD.md`)**:
  - *Act 1: Survival*: Scavenge immediate crash site, preserve medical supplies and rations.
  - *Act 2: The Blight*: Discover that the local biosphere is corrupted by a necrotic blight turning benign woodland spirits aggressive.
  - *Act 3: Guardian Encounter*: Jack must defeat or cleanse the Corrupted Tree Spirit to reopen the forest pathway.
  - *Act 4: First Contact*: Emerging from the canopy to discover stone ruins and the outskirts of human medieval civilization.

---

## 2. Implemented Narrative Elements

### Skyrim-Style Radiant Bounties (`Assets/Scripts/Domain/Skyrim/QuestData.cs`)
- **Structure**: Infinite radiant contract system.
- **Quest Giver**: Local Jarls, Stewards, or Town Notice Boards (`BountyBoardInteractable`).
- **Archetypes**:
  - `KillBanditLeader`: Infiltrate a camp or fort to execute a hostile outlaw commander.
  - `ClearDungeon`: Eliminate undead Draugr or beasts infesting ancient Nordic barrows.
  - `RetrieveItem`: Recover stolen family heirlooms or enchanted relics.
- **Rewards**: Gold, faction reputation bonuses, and character progression XP.
