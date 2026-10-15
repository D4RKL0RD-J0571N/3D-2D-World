# Reusable Code Combinations

> **Scope note (added 2026-08-21):** this document catalogs class names and patterns from *other* prototypes in your portfolio (`snake-prototype`, `Prototype_RGP`, and similar — see `ARCHITECTURE.md`'s Pattern Source Attribution table) for salvage purposes when starting a new project. None of the classes named below (`PlayerController`, `SkillsCollection`, `ObjectPoolManager`, `TimeManager`, etc.) exist in **3D 2D World**'s actual codebase. This file was intentionally left out of the code-vs-docs audit pass that corrected the other files in this folder — rewriting it to match this project's code would defeat its purpose as a cross-project reference. Content below is unchanged.

This document maps code bundles to the kinds of games they can support.
Think of it as a pattern-to-genre guide for salvage work.

## 1. Platformer Movement Bundle

### Code bundle
- `BaseActor`
- `PlayerController`
- `MovementComponent`
- `JumpComponent`
- `GravityComponent`
- `RaycastCollisionsComponent`
- `SpriteComponent`
- `PlayerInputComponent`

### Best fit genres
- 2D platformer
- side-scroller
- metroidvania
- precision platformer
- action platformer

### Example usage
- Character runs, jumps, and lands on solid platforms
- Player dashes through a level with slope and ground detection
- Enemy patrols using the same movement shell with different input

### Notes
- This is the cleanest "movement-first" bundle in the project.
- It can stand alone even without the RPG systems.

## 2. Action RPG Bundle

### Code bundle
- `BaseActor`
- `StatsComponent`
- `BaseAttribute`
- `AttributesCollection`
- `BaseSkill`
- `SkillsCollection`
- `InventoryManagerComponent`
- `ItemComponent`
- `ItemData`

### Best fit genres
- action RPG
- hack-and-slash
- looter
- metroidvania with progression
- light fantasy adventure

### Example usage
- Player kills enemies, gains XP, levels up, and unlocks skills
- Items modify attributes and skill growth
- Inventory stores consumables, gear, and quest items

### Notes
- This is the strongest salvage path if you want a game with growth and loot.

## 3. Side-Scroller Combat Bundle

### Code bundle
- `PlayerController`
- `EnemyTemplate`
- `AttackComponent` concept
- `StatesComponent`
- `StatsComponent`
- `DamagePopUpComponent`
- `ObjectPoolManager`

### Best fit genres
- brawler
- combat platformer
- arcade action
- boss rush
- monster-hunting side-scroller

### Example usage
- Enemies spawn from pools and attack the player in waves
- Damage numbers use pooled popup objects
- Death state triggers game over flow

### Notes
- The attack system is unfinished, so this bundle needs rebuilding around a clean combat model.

## 4. Survival and Simulation Bundle

### Code bundle
- `TimeManager`
- `SaveLoadManager`
- `GameData`
- `InventoryManagerComponent`
- `BaseAttribute`
- `StatsComponent`

### Best fit genres
- survival
- life sim
- management sim
- sandbox
- day/night exploration game

### Example usage
- Time passes and changes the world state
- Player resources persist across sessions
- Attributes regenerate over time and influence survival choices

### Notes
- Best if the game has persistent state, schedules, or long-form progression.

## 5. Utility and Dev-Tool Bundle

### Code bundle
- `DebugConsoleComponent`
- `DebugCommandFactory`
- `DebugCommandBase`
- `DebugAddItem`
- `DebugIncreaseXpButton`
- `DebugLevelUpButton`
- `PrintPlayerStats`

### Best fit genres
- any internal prototype
- any game with cheat/debug modes
- QA-heavy projects
- fast iteration projects

### Example usage
- Spawn items during testing
- Force level-up for balance checks
- Inspect actor stats without pausing the game

### Notes
- This bundle is not genre-specific. It is a productivity multiplier.

## 6. Interaction and Choice Bundle

### Code bundle
- `ChoiceSystem`
- `WheelMenu`
- `InteractionManager`

### Best fit genres
- narrative adventure
- dialogue RPG
- visual novel hybrid
- exploration game
- choice-driven horror

### Example usage
- Present branch choices to the player
- Open a radial menu for inventory, skills, map, or magic
- Trigger contextual interactions with world objects

### Notes
- `ChoiceSystem` is conceptually strong even if implementation is incomplete.

## 7. Enemy AI Bundle

### Code bundle
- `AutomatedMovementComponent`
- `MoveToTargetComponent`
- `ObjectTrackerComponent`
- `EnemyTemplate`

### Best fit genres
- platformer with enemies
- tower defense
- escort mission game
- stealth chase game
- tactics-lite

### Example usage
- Enemy moves toward the player when in range
- Guard tracks a target object through a level
- Simple AI follows pathless pursuit behavior

### Notes
- This bundle is best for lightweight AI, not complex decision making.

## 8. World State Bundle

### Code bundle
- `TimeManager`
- `LoadingSceneManager`
- `LoadingProgressManager`
- `GameManager`
- `UserInterfaceManager`

### Best fit genres
- progression adventure
- hub-based game
- episodic game
- level-based platformer

### Example usage
- Loading screen manages scene transitions
- Game state changes between menu, play, pause, and game over
- Time changes trigger UI or world updates

### Notes
- This bundle is useful in almost any game with scene flow and meta state.

## 9. Recommended Salvage Recipes

### Recipe A: Minimal platformer
- `BaseActor`
- `PlayerController`
- `MovementComponent`
- `JumpComponent`
- `RaycastCollisionsComponent`
- `SpriteComponent`

### Recipe B: Action RPG starter
- Recipe A
- `StatsComponent`
- `BaseAttribute`
- `SkillsCollection`
- `InventoryManagerComponent`
- `ItemComponent`

### Recipe C: Survival side-scroller
- Recipe A
- `TimeManager`
- `SaveLoadManager`
- `GameData`
- `InventoryManagerComponent`

### Recipe D: Dev-heavy prototype
- Recipe A
- `ObjectPoolManager`
- `DebugConsoleComponent`
- `DebugCommandFactory`
- `WheelMenu`

## 10. Quick Rule

If a bundle has:
- movement
- progression
- inventory
- save/load

then it can usually power a game with much more longevity than the raw prototype suggests.

If it also has:
- time
- pooling
- debug tools

then it is especially good as a reusable code base.