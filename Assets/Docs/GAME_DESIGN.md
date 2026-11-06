# Game Design Document: The Lost Survivor (Forest Slice)

**Project**: 3D 2D World — Hybrid Procedural RPG  
**Target Platform**: PC (Steam / Standalone)  
**Perspective**: First-Person (with modular camera abstraction)  
**Status**: Production Blueprint — Vertical Slice 1  
**Last Updated**: October 2026  

---

## 1. Vision & Core Philosophy

### High Concept
A tactical fantasy action RPG following **Jack**, a modern military survivor stranded through an anomalous portal in a hostile, living fantasy realm. Armed initially with military training, shattered modern gear, and scavenged tools, Jack must adapt to a world governed by ancient runes, magical blight, and primeval ecology.

### Design Pillars
1. **Grounded Survival & Visceral Action**: Combat is weighty, deliberate, and dangerous. Health does not regenerate instantly; stamina dictates exertion; positioning and observation turn lethal encounters into survivable victories.
2. **Hybrid Procedural World**: Mathematical voxel foundations provide vertical, deformable, and deterministic terrain; hand-crafted mesh landmarks and decorative prefabs provide emotional resonance, narrative storytelling, and organic beauty.
3. **Player Agency & Transparent Rules**: Predictable systems over hidden dice rolls. Audio-visual telegraphs, clear hit detection, and inspectable attributes give the player mastery over the environment.

---

## 2. Narrative Context: "The Silent Canopy" (Vertical Slice)

### The Setup
Jack awakens amidst smoking wreckage in an unfamiliar depression deep within an ancient forest. His squad's transport aircraft has clipped through reality. Radios emit static; compasses spin wildly. The flora is gargantuan, glowing with faint luminescent veins.

### The Objectives
1. **Scavenge the Crash Site**: Recover survival supplies (ration pack, emergency medical kit, military entrenching tool / combat knife) from shattered cargo crates.
2. **Seek Fresh Water**: Follow the natural downhill gradient to locate an uncorrupted water source.
3. **Survive the Corrupted Glade**: The local water basin is guarded by an enraged, blighted **Tree Spirit** (an ancient forest dryad/treant infected with crystalline red blight).
4. **Break Through to Civilization**: Defeat or weaken the guardian to cleanse the pathway, revealing an ancient flagstone trail leading upward toward human settlements.

---

## 3. Core Gameplay Loop

```
┌────────────────────────────────────────────────────────┐
│                      SPAWN / LOAD                      │
│     Player initializes at Crash Site or active checkpoint│
└──────────────────────────┬─────────────────────────────┘
                           │
                           ▼
┌────────────────────────────────────────────────────────┐
│                   EXPLORATION & SCAVENGING             │
│   • Navigate 3D voxel terrain and natural clearings    │
│   • Identify landmarks via minimap / compass           │
│   • Interact (E) with crates, plants, and debris      │
└──────────────────────────┬─────────────────────────────┘
                           │
                           ▼
┌────────────────────────────────────────────────────────┐
│                     TACTICAL COMBAT                    │
│   • Engage roaming minions (Blight Slimes)             │
│   • Manage Stamina (dodge, sprint, melee swing)        │
│   • Execute telegraphed combat vs. Tree Spirit Boss    │
└──────────────────────────┬─────────────────────────────┘
                           │
                           ▼
┌────────────────────────────────────────────────────────┐
│                PROGRESSION & MILESTONES                │
│   • Earn Experience (XP) and advance Level             │
│   • Allocate Perk Points / Attribute upgrades          │
│   • Advance Quest State Machine                        │
└──────────────────────────┬─────────────────────────────┘
                           │
                           ▼
┌────────────────────────────────────────────────────────┐
│                 SAVE / TRANSITION / EXIT               │
│   • Manual (F5) or checkpoint autosave                 │
│   • Step onto civilization trail to conclude slice    │
└────────────────────────────────────────────────────────┘
```

---

## 4. Mechanics Specification

### 4.1 Locomotion & Controls
- **Movement (WASD / Left Stick)**: Grounded physics via `CharacterController`. Normal walk speed: $5.0\,\text{m/s}$.
- **Sprint (Left Shift / L3)**: Increases movement speed by $1.6\times$ ($8.0\,\text{m/s}$), drains Stamina at $15\,\text{points/sec}$.
- **Crouch (Left Ctrl / R3)**: Lowers stance from $2.0\,\text{m}$ to $1.1\,\text{m}$, reduces speed to $0.55\times$ ($2.75\,\text{m/s}$), reduces enemy detection radius by $50\%$.
- **Jump (Space / A Button)**: Vertical leap ($1.35\,\text{m}$ clearance) with ground-sticking force on landing. Cost: $15$ Stamina.
- **Voxel Step Offset**: $1.05\,\text{m}$ step offset allows climbing single-block voxel rises without jumping.

### 4.2 Interaction Model
- **Primary Interact Key (`E` / `X Button`)**:
  - Center-screen raycast ($2.5\,\text{m}$ range).
  - Highlighting / UI prompt appears when hovering over interactable objects (`IInteractable`).
  - Target types: Crates (`BaseContainer`), Loose items (`BaseItem`), Quest triggers (`QuestTriggerVolume`).
- **Mining / Placement Toggle**:
  - Separate tool mode or dedicated keys for voxel alteration, preventing accidental block breaking during combat.

### 4.3 Combat Mechanics
- **Player Melee Attack (Left Mouse / Right Trigger)**:
  - Weapon: Military Entrenching Tool / Combat Knife.
  - Type: Forward spherecast ($2.2\,\text{m}$ range, $0.5\,\text{m}$ radius).
  - Damage: $25$ Physical Damage.
  - Stamina Cost: $20$ Stamina per swing.
  - Cooldown / Cadence: $0.65$ seconds between swings.
  - Impact Feedback: Screen shake, enemy hit-flash, impact particle, audio cue.
- **Attributes Impacted**:
  - `Health`: Decreases upon taking damage. At $0$, actor enters `ActorState.Death`.
  - `Stamina`: Decreases on attack/sprint/jump. Regenerates when unexhausted after a $1.0\text{s}$ delay at $25\,\text{pts/sec}$.
- **Stagger & Hitstun**:
  - Heavy hits interrupt enemy wind-ups and push them back along the hit normal.

---

## 5. Entity Profiles (Vertical Slice)

### 5.1 Protagonist: Jack
- **Archetype**: Modern Military Survivor.
- **Starting Attributes**:
  - Health: $100 / 100$ (Passive regen: $0.5\,\text{HP/s}$ out of combat).
  - Stamina: $100 / 100$ (Regen: $25\,\text{pts/s}$ after delay).
- **Starting Gear**:
  - Military Fatigues (basic defense).
  - Combat Knife / Entrenching Tool ($25$ damage).
  - Field Rations ($+30$ Health over $10\text{s}$).

### 5.2 Minion: Blight Slime
- **Archetype**: Corrosive minor elemental.
- **Visuals**: 2.5D billboard sprite with dynamic squash-and-stretch.
- **Health**: $30 / 30$.
- **Behavior**:
  - *Idle / Wander*: Hops erratically across glade floor.
  - *Aggro Range*: $8.0\,\text{m}$ (reduced to $4.0\,\text{m}$ if player crouches).
  - *Attack*: Leaps toward player when within $2.0\,\text{m}$, dealing $10$ Acid Damage on contact.

### 5.3 Boss: Corrupted Tree Spirit
- **Archetype**: Ancient woodland dryad corrupted by crystalline blight.
- **Visuals**: Hybrid towering bark golem with twisted roots and luminescent red sap veins.
- **Health**: $250 / 250$.
- **Combat Phases**:
  - **Phase 1 (Waking / Proximity)**: Rises from tree form when Jack enters the Ancient Spring clearing ($12\,\text{m}$ distance).
  - **Phase 2 (Root Spikes)**: Slams the ground, telegraphing circular ground zones where wooden spikes erupt after $1.2\text{s}$ (deals $20$ damage).
  - **Phase 3 (Branch Swipe)**: Wide arc melee swipe ($4.0\,\text{m}$ reach, deals $30$ damage + knockback).
  - **Phase 4 (Exhaustion / Stagger)**: After executing a swipe or taking $60$ damage, the spirit enters a $2.5\text{s}$ recovery window (takes $1.5\times$ damage).
  - **Defeat**: At $0$ HP, the crystalline blight shatters; the spirit retreats into the earth, dropping the "Ancient Forest Heart" quest item and opening the mountain path.

---

## 6. Quest Structure: "The Silent Canopy"

| Step ID | Objective Name | Trigger Condition | Success Criteria | Rewards |
|---|---|---|---|---|
| `QS_00` | **Awakening** | Game Start | Stand up, pick up Knife from crate | $10$ XP |
| `QS_01` | **Scavenge Supplies** | Knife acquired | Collect 2 Rations + 1 Medkit | $25$ XP |
| `QS_02` | **Follow the Water** | Supplies collected | Reach Ancient Spring clearing | $25$ XP |
| `QS_03` | **Slay Corrupted Guardian** | Enter spring | Reduce Tree Spirit to $0$ HP | $150$ XP, Forest Heart |
| `QS_04` | **Path to Civilization** | Boss defeated | Step onto Ancient Trail exit | Slice Complete / Victory Screen |

---

## 7. Progression & Economy

- **Experience Curve**:
  $$\text{XP Required} = \text{XpLevelUpBase} + (\text{CurrentLevel} \times \text{XpLevelUpMult})$$
  - Level 1 $\to$ 2: $75 + 25 = 100\,\text{XP}$.
  - Level 2 $\to$ 3: $75 + 50 = 125\,\text{XP}$.
- **Level-Up Reward**:
  - $+10$ Max Health.
  - $+10$ Max Stamina.
  - $+1$ Perk Point (can be spent to increase Melee Damage or Sprint Efficiency).
