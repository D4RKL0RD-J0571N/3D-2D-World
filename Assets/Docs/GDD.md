# Game Design Document: 3D 2D World

## 1. Vision & Executive Summary
**3D 2D World** is a procedural dungeon crawler that blends the immersive depth of 3D environments with the precise, tactical navigation and interaction of 2D/2.5D systems. The goal is to create a "grounded" exploration experience where the player navigates complex, 3D-generated spaces while maintaining a sense of deliberate, skill-based movement.

**The "Why":** 
To provide a high-fidelity exploration loop that feels "snappy" and reactive. By using procedural generation, we ensure infinite replayability; by using a decoupled architecture (Service Locator, Event Bus), we ensure the game can scale with new mechanics without becoming a "spaghetti" codebase.

## 2. Core Gameplay Loop
The game is built around an **Infinite Crawler Loop**:

1.  **Generate**: The system requests and generates a unique layout using the `LevelGenerationService`.
2.  **Explore**: The player enters the environment, navigating 3D space using responsive movement and look mechanics.
3.  **Interact**: The player interacts with environmental hazards, triggers, and objects (handled via the Event Bus).
4.  **Complete**: Upon reaching a designated `ExitTrigger`, the `LevelCompletedEvent` is fired.
5.  **Transition**: The `GameStateMachine` handles the asynchronous transition to the next layout, clearing the previous state and preparing the next generation.

## 3. Gameplay Pillars
- **Precise Navigation**: Movement feels weightless yet intentional. The "2D" influence ensures that despite 3D space, the player's primary pathfinding and movement feel predictable and tight.
- **Procedural Variety**: No two runs are the same. The environment is a mathematical construct translated into physical reality.
- **Reactive Feedback**: Every input (Move, Look, Run, Crouch) and every world event (Completion, Generation) provides immediate visual/audio feedback via the Service Locator.

## 4. Mechanics Overview
### Player Mechanics
- **Locomotion**: Standard WASD movement with "Run" (Sprint) and "Crouch" modifiers.
- **Vision**: Mouse-delta based looking, allowing for 3D orientation within the 2D-style navigation.
- **Interaction**: Trigger-based interaction with the world (e.g., Exit Triggers, Loot, Hazards).

### World Mechanics
- **Procedural Generation**: Rooms and corridors are generated based on `LevelGenerationParameters`.
- **State Management**: Seamless transitions between "Playing," "Loading," and "Generating" states to prevent frame-hiccups.

## 5. Aesthetic & Feel
- **Perspective**: 3D environments with a focus on clear spatial awareness.
- **Pacing**: Fast-paced exploration with clear goals.
- **Feedback**: High-contrast indicators for interactable objects and level boundaries.

---

## Implementation Note (2026-08-21)

This document describes design intent, not a claim about current implementation — that's what `STATES.md` and `Log.md` are for. Worth flagging one specific gap between the two: Section 2 step 5 and Section 4's "State Management" line describe state-machine-driven transitions between Playing/Loading/Generating. In the actual code, regeneration happens entirely through event subscription *inside* the `Playing` state (`LevelCompletedEvent` → republish `LevelGenerationRequestedEvent`) — no state transition occurs, and there's no `Generating` state at all in the `GameStateType` enum. If the seamless-transition vision here still holds, `STATES.md`'s "Next Immediate Tasks" has the concrete next step for it (flesh out `Loading` as a real transition state).