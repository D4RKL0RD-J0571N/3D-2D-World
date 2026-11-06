# RPG PROJECT — GAPS, DISCREPANCIES & UNRESOLVED TENSIONS
**Status Classification**: `CANONICAL` | `IMPLEMENTED` | `DOCUMENTED` | `INFERRED` | `PROPOSED` | `UNKNOWN` | `CONFLICTING`

---

## 1. Documentation vs. Implementation Gaps
- **Missing `QuestManager`**:
  - `Docs/TECHNICAL_DESIGN.md` describes a monolithic `QuestManager` state machine handling Jack's tutorial quests (`CrashSiteScavenge`, `FindWaterSource`, `DefeatTreeSpirit`).
  - *Reality*: This class does not exist in `Assets/Scripts/`. Instead, `RadiantQuestService.cs` implements procedural Skyrim-style bounties.
- **Missing Combat Hitboxes / Weapon Equip Component**:
  - `Docs/TECHNICAL_DESIGN.md` specifies `WeaponComponent` and `HitboxComponent` with frame-based raycasting.
  - *Reality*: Melee combat in `BaseActor` currently processes abstract `TakeDamage` calls; physical weapon colliders and swing animation triggers are not yet hooked up.
- **Unlinked Game States**:
  - `GameStateMachine` only enters `GameState.Playing`. `MainMenu`, `Paused`, `GameOver`, and `Loading` states exist as shells without transitions or UI binding.

---

## 2. Worldbuilding & Narrative Tensions
- **Jack (Modern Soldier) vs. Pure Medieval Fantasy**:
  - Layer A has modern MREs, military survival knives, and crash sites.
  - Layer B has Jarls, Draugr barrows, blacksmith forges, and medieval guards.
  - *Resolution Required*: Is Jack a stranger in a strange land (portal fantasy), or is the project evolving into a pure fantasy RPG?

---

## 3. Audio & Animation Asset Gaps
- **Animations**: Characters in `RPGProvingGrounds.unity` currently use static T-poses or default Synty rigs without an active `AnimatorController` state machine (locomotion, attack swings, hit reactions).
- **Audio FX**: `AudioService` is stubbed in `ServiceLocator`, but sound clips for footstep surfaces, weapon impacts, and ambience need to be assigned.
