---
name: northstar
description: >-
  Build and wire up Northstar — Nav & Tracking System in a Unity project: add
  the compass, radar, screen + edge overlay, and navigation (compass) bar to a
  scene; make GameObjects trackable and set their values (icon, color, label,
  distance, which displays they show on, draw order); create and tune Northstar
  Overlay Settings; swap or duplicate the compass / radar / nav-bar prefabs;
  override icon graphics at edit time and at runtime; and write custom C# —
  custom distance text, custom icons, custom compass/radar/nav-bar subclasses,
  and demo-style "actions" like hold-to-collect progress bars and floating
  health bars driven by an icon's distance. Use this skill whenever the user is
  in a Unity project that contains Northstar / Magic Pig Games / the
  MagicPigGames.Northstar namespace and asks to create, add, configure, or
  script anything that points the player toward, marks, or measures the
  position of an object — a compass, radar, minimap, waypoint, quest marker,
  objective marker, point-of-interest icon, HUD marker, off-screen indicator,
  edge arrow, distance label, "track this enemy", "show where the loot is",
  "put a marker over the NPC", "fade the icon as I get closer", "show north on
  a compass bar" — even when they don't say "Northstar" explicitly. It knows
  the two-systems architecture (Compass/Radar with TrackedTargetCompassRadar +
  CompassIcon, and Screen/Edge Overlay + Navigation Bar with TrackedTargetOverlay
  + OverlayIcon), the ScriptableObject settings model, the runtime override API,
  the play-start icon-creation timing trap, and drives the Unity Editor through
  the unity-mcp bridge to build it for real.
---

# Northstar — Nav & Tracking System

Northstar is a Unity asset by Magic Pig Games / Infinity PBR (namespace
`MagicPigGames.Northstar`). It gives the player **multiple, combinable ways to
see where things are**: a rotating **Compass** or **Radar**, on-screen **Overlay**
icons drawn over objects, **Edge** arrows that point at off-screen objects, and a
horizontal **Navigation Bar** (compass bar). Anything in the world you want the
player to find — an enemy, a quest objective, a pickup, a teammate, a point of
interest, or "north" itself — becomes a **tracked target**, and its look is
driven by reusable **Northstar Overlay Settings** assets you mix, match, and
override.

This skill lets you do all of it for the developer: **add the displays to a
scene, make objects trackable, set and override their icon graphics, tune the
settings, change the prefabs, and write the custom C#** (custom distance text,
custom icons, and demo-style progress/health bars on icons).

The guiding principle for the user: *if the player needs to know where something
is, reach for Northstar.* Your job is to make that easy — wire the compass and
overlay, mark the objects, and dial in the graphics, without them learning every
field first.

## First: confirm this is a Northstar project

Before doing anything, verify Northstar is present (don't assume from the
conversation):

- Look for the source folder `Assets/**/Northstar*/Scripts/` and the namespace
  `MagicPigGames.Northstar` in `.cs` files.
- Quick check: `grep -rl "namespace MagicPigGames.Northstar" --include=*.cs Assets | head`.
- The display scenes have a `Compass` or `Radar` component and a `NorthstarOverlay`
  component; tracked objects have a `TrackedTargetOverlay` and/or a
  `TrackedTargetCompassRadar` component.

If Northstar is **not** found, say so and stop — this skill only applies to
Northstar projects.

## Mental model — read this before acting

Northstar is **two parallel systems**. They are independent: an object can be on
one, the other, or both. Each system has a *scene UI component*, a *tracked-target
component you add to the world object*, and an *icon* created at runtime.

| System | Scene UI component(s) | Add to the world object | Icon (runtime) |
|---|---|---|---|
| **Compass / Radar** | `Compass` **or** `Radar` (both extend `NorthstarSystem`) | `TrackedTargetCompassRadar` | `CompassIcon` |
| **Screen Overlay + Edge Overlay + Navigation Bar** | `NorthstarOverlay` (master) + optional `NavigationBar` | `TrackedTargetOverlay` | `OverlayIcon` (+ optional `DistanceText`) |

Five things to keep straight:

1. **`NorthstarSystem`** — the abstract base for `Compass` and `Radar`. It is a
   scene singleton (`NorthstarSystem.Instance`). It holds the `player`, the
   `defaultTrackedIcon` prefab, the detection range and fade ranges, and the
   `GlobalNorthstarSettings`. **There can be only one Compass *or* Radar in a
   scene** (Radar is a Compass that also pings/sweeps).
2. **`NorthstarOverlay`** — the master singleton (`NorthstarOverlay.Instance`)
   for the on-screen world: screen icons, edge arrows, and the navigation bar.
   It needs a `targetCamera`, the icon prefabs, and a `NorthstarOverlaySettings`
   default. `NavigationBar` (`NavigationBar.Instance`) is the horizontal compass
   bar; it lives under the overlay object. **There can be only one of each.**
3. **Tracked-target components** — `TrackedTargetCompassRadar` and
   `TrackedTargetOverlay` are what you add to a GameObject to make it show up.
   They auto-register with their system on `OnEnable` (via a coroutine, because
   the system may not exist yet) and create the icon. A tracked target only
   appears if the matching display exists in the scene.
4. **Icons** — `CompassIcon` (compass/radar) and `OverlayIcon` (screen/edge/nav
   bar) are the runtime UI pieces. **They are created only when the game starts,
   not at edit time.** Any runtime code that reads an icon must poll for it (see
   `references/runtime-api.md`). Don't edit values on a `CompassIcon` inspector —
   it's driven by its tracked-target component.
5. **Settings** — `GlobalNorthstarSettings` (one per project; North Angle /
   World North) and `NorthstarOverlaySettings` (the per-"type" visual config:
   sprites, colors, sizes, opacity curves, distances for screen/edge/arrow/nav
   bar). Values resolve in a cascade: **runtime override → per-target
   `overlaySettings` field → the master `NorthstarOverlay` default.**

The deep details for each area are in `references/`. **Load the relevant file
before writing code or building assets** — don't invent field names; the
references list the real ones, copied from source.

## How you actually build and edit things

Northstar is built two ways, both of which you can drive yourself:

**A. The Northstar window (the supported, safe path for adding displays).**
Unity menu **`GameObject → Northstar → Create Window`** opens a dockable window
that instantiates the compass / radar / navigation bar / screen overlay prefabs
into the scene (from `Resources/`), preserving the `player` reference between
swaps. This is the cleanest way to add a display. You can trigger it for the
user, or open it yourself via editor code.

**B. Editor C# through the `unity-mcp` bridge (the instant path).** With the
Unity Editor open and the bridge running (the user's "Unity MCP setup" memory
covers it), run editor C# with `mcp__unity-mcp__Unity_RunCommand`. Its `Code`
must follow Unity MCP's **"Golden Template"** exactly — a class named
`CommandScript`:

```csharp
using UnityEngine;
using UnityEditor;
using MagicPigGames.Northstar;

internal class CommandScript : IRunCommand   // MUST be internal, MUST be named CommandScript
{
    public void Execute(ExecutionResult result)
    {
        // ... Northstar editor calls here (instantiate prefabs, AddComponent the
        //     tracked-target components, create NorthstarOverlaySettings assets) ...
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        result.Log("done");
    }
}
```

| To do… | You… |
|---|---|
| add a **display** (compass / radar / nav bar / overlay) | instantiate the prefab from `Resources` (e.g. `Resources.Load<GameObject>("Compass 1")`) under the right parent, or open the Northstar window |
| make an object **tracked** | `go.AddComponent<TrackedTargetOverlay>()` and/or `go.AddComponent<TrackedTargetCompassRadar>()`, then set the fields |
| create a **settings** asset | `ScriptableObject.CreateInstance<NorthstarOverlaySettings>()` → set fields → `AssetDatabase.CreateAsset(...)` |
| **override** an icon's graphics at runtime | `NorthstarOverlay.Instance.SetOverride*` / `NavigationBar.Instance.SetOverride*` / `NorthstarSystem.Instance.SetOverride*` |
| add **custom logic** | write a subclass (`DistanceText`, `OverlayIcon`, `CompassIcon`, `NavigationBar`, `Compass`/`Radar`) or a component on the icon prefab |

`references/setup-and-scene.md` has the full recipes, the bundled
`NorthstarAgentTools` helper (`scripts/NorthstarAgentTools.cs.txt`), verification
steps, and the **no-bridge fallback** (write an editor script with a `[MenuItem]`
the user clicks). After any change, **verify**: read the component/asset back and
check `mcp__unity-mcp__Unity_GetConsoleLogs` (with `logTypes: "Error"`) for
compile/runtime errors.

## How to work with the user

Tracking is assembled from a display + tracked objects + settings, and a wrong
field is invisible until playtest, so **plan before you build** and confirm
anything non-obvious:

1. **Restate the goal and decompose it into Northstar parts.** "Track this enemy
   on the compass and show its distance" → a `Compass` (or `Radar`) in the scene
   + a `NorthstarOverlay` + a `TrackedTargetCompassRadar` on the enemy + a
   `TrackedTargetOverlay` on the enemy whose icon prefab has a `DistanceText`.
   "Quest markers that arrow off-screen and fade as you near them" → a
   `TrackedTargetOverlay` with a `NorthstarOverlaySettings` whose edge arrow is
   enabled and whose screen opacity curve fades to zero up close.
2. **Reach for the shipped prefabs and a settings preset before building new.**
   Northstar ships 17 compass styles, 5 radars, 3 nav bars, ready icon prefabs,
   and many `NorthstarOverlaySettings` presets (Default, Wolf, Locations, Blue
   Berries, cardinal-direction bars). Duplicate and tweak rather than authoring
   from scratch. Catalog in `references/displays.md`.
3. **Propose smart defaults and surface the choices Northstar implies** — this is
   where you add value:
   - A tracked target shows **nothing** unless the matching display exists in the
     scene. If you add a `TrackedTargetOverlay`, make sure there's a
     `NorthstarOverlay`; for `TrackedTargetCompassRadar`, a `Compass`/`Radar`.
   - The `Compass`/`Radar` **`player` field must be set by hand** — it is not
     auto-assigned. Remind the user or set it.
   - `NorthstarOverlay` needs a **`targetCamera`** (no fallback).
   - Reuse a `targetType` string ("Enemy", "Objective") across similar objects so
     you can bulk-override them at runtime later.
   - A duplicated compass / radar / nav-bar prefab **must stay in a `Resources`
     folder** or the window won't find it.
4. **Ask only what you can't sensibly default** (which object(s) to track, which
   displays they appear on, what the icon looks like, whether distance shows,
   fade behavior). One concise batch, then proceed.
5. **Build, verify, and report** what you created, where it lives, and how it
   behaves at play time (icons appear on Play; runtime overrides need the
   poll-for-icon pattern).

### Flagship walkthrough — "Track this enemy on the compass and show a distance label on screen"

1. Confirm Northstar is present and the bridge responds (a trivial `CommandScript`
   that logs `typeof(NorthstarOverlay).FullName`).
2. **Ensure the displays exist.** If the scene has no `Compass`/`Radar`, add one
   (instantiate `Resources/Compass 1` or open the Northstar window) and set its
   `player`. If there's no `NorthstarOverlay`, instantiate
   `Resources/Northstar Screen Overlay` and set its `targetCamera`.
3. **Make the enemy tracked.** `AddComponent<TrackedTargetCompassRadar>()`
   (trackingType `RelativePoint` — the usual "where is it" mode) and
   `AddComponent<TrackedTargetOverlay>()` (set `targetType = "Enemy"`).
4. **Show distance.** Assign the enemy's `TrackedTargetOverlay.customScreenIcon`
   to the shipped **`Northstar Icon Distance`** prefab (an `OverlayIcon` with a
   child `DistanceText`), or add a `DistanceText` to your icon prefab. Default
   text is meters via short-scale notation.
5. **Verify** (read components back, check console) and **report**: "Press Play —
   a compass blip and an on-screen icon with a distance label appear for the
   enemy. To recolor it at runtime, call
   `NorthstarOverlay.Instance.SetOverrideColor(\"Enemy\", Color.red)`."

## Object catalog

What you create or add, and the reference with the details.

| Thing | What it is | How you make it | Reference |
|---|---|---|---|
| `Compass` / `Radar` | scene component (singleton) showing direction/blips | instantiate `Resources/Compass N` / `Radar N`, or Northstar window | `displays.md`, `architecture.md` |
| `NorthstarOverlay` | master scene component for screen/edge/nav-bar icons | instantiate `Resources/Northstar Screen Overlay` | `setup-and-scene.md` |
| `NavigationBar` | horizontal compass bar (under the overlay) | instantiate `Resources/Navigation Bar N` under the overlay | `displays.md` |
| `TrackedTargetCompassRadar` | makes a GameObject appear on compass/radar | `AddComponent` on the world object | `tracking-objects.md` |
| `TrackedTargetOverlay` | makes a GameObject appear on screen/edge/nav bar | `AddComponent` on the world object | `tracking-objects.md` |
| `GlobalNorthstarSettings` | `Create ▸ Northstar/Global Settings`; one per project | `CreateInstance` + `CreateAsset` (usually auto-created) | `overlay-settings.md` |
| `NorthstarOverlaySettings` | `Create ▸ Northstar/Overlay Settings`; per-type visuals | `CreateInstance` + `CreateAsset`, or duplicate a preset | `overlay-settings.md` |
| `DistanceText` | text under an overlay icon (distance label) | on the icon prefab; subclass for custom text | `custom-logic.md` |
| `RequireLineOfSightCompassIcon` | hides a compass icon with no line of sight | `AddComponent` alongside a `CompassIcon` | `tracking-objects.md` |
| Custom icon / progress bar / health bar | a component on the icon prefab driven by the icon | write C# (Berries/health-bar pattern) | `custom-logic.md` |

## Reference files

Load the file that matches the task; each lists exact field names, real APIs,
worked recipes, and gotchas pulled from source.

- `references/architecture.md` — the two systems in depth: `NorthstarSystem` /
  `Compass` / `Radar`, `NorthstarOverlay` / `NavigationBar`, the tracked-target
  components, `CompassIcon` / `OverlayIcon`, the settings cascade, the enums, and
  the icon **created-at-play-start** lifecycle.
- `references/setup-and-scene.md` — **the how-to for adding displays.** The
  Northstar `Create Window` menu, the unity-mcp `CommandScript` recipes to
  instantiate the overlay / compass / radar / nav bar from `Resources` and wire
  `player` / `targetCamera` / settings, the bundled helper, verification, and the
  no-bridge fallback.
- `references/tracking-objects.md` — making a GameObject tracked: adding
  `TrackedTargetOverlay` / `TrackedTargetCompassRadar`, setting `targetType`,
  `layerOrder`, `iconOffset`, `overlaySettings`, custom icons; the `TrackingType`
  choices; line-of-sight; the full per-target override flag list.
- `references/overlay-settings.md` — the complete `NorthstarOverlaySettings`
  field catalog (screen / edge / arrow / nav bar, curves, distances) and
  `GlobalNorthstarSettings`; creating settings assets from code; the shipped
  preset list.
- `references/displays.md` — `Compass`, `Radar` (+ `RadarLine`), `NavigationBar`
  (+ evergreen objects), `DistanceText` configuration; the `Resources` prefab
  catalog; duplicating/changing prefabs and overriding their graphics.
- `references/runtime-api.md` — the runtime registration (poll-for-icon)
  coroutine pattern, and the complete `SetOverride*` / `ResetOverride*` API across
  all three systems (per-icon and per-`type`), plus `AddTarget` / `RemoveTarget`
  and pause levels.
- `references/custom-logic.md` — **read before writing custom C#.** Subclass
  `DistanceText.UpdateText()` for custom distance text; custom `OverlayIcon` /
  `CompassIcon`; custom `NavigationBar` / `Compass` / `Radar`; and the demo
  "action" pattern (the Berries hold-to-collect progress bar and the floating
  health bar) — a component on the icon prefab driven by `OverlayIcon`.

Official docs (authoritative; deeper tables, demos, video tutorials):
<https://infinitypbr.gitbook.io/magic-pig-games/northstar-nav-and-tracking>.

## Conventions & guardrails

- **Two systems, two tracked-target components.** Compass/Radar uses
  `TrackedTargetCompassRadar` (+ `CompassIcon`); the overlay/edge/nav-bar uses
  `TrackedTargetOverlay` (+ `OverlayIcon`). They're independent — add both for an
  object that should appear everywhere. An icon prefab is one or the other, never
  both.
- **A tracked target needs its display present.** No `NorthstarOverlay` → no
  screen icons; no `Compass`/`Radar` → no compass blips. Adding the component is
  half the job; the matching display must be in the scene.
- **Only one of each display.** One Compass *or* Radar; one `NorthstarOverlay`;
  one `NavigationBar`. The Northstar window enforces this; respect it in code.
- **Icons exist only at play time.** Runtime code that reads
  `TrackedTargetOverlay.ScreenIcon` / `NavigationBarIcon` (or a `CompassIcon`)
  must poll in a coroutine until it's non-null — see `references/runtime-api.md`.
  Don't expect an icon in `Awake`.
- **Don't edit the `CompassIcon` inspector.** Its values are set by the
  `TrackedTargetCompassRadar` (or the Compass/Radar for preset objects). Configure
  the tracked-target component instead.
- **The settings cascade is runtime override → per-target `overlaySettings` →
  master default.** A per-target override only applies when its `overrideX`
  toggle is on *and* the target has an `overlaySettings` asset to read from.
- **Duplicated display prefabs must stay in a `Resources` folder** and have a
  unique name, or the Northstar window won't list them.
- **Custom subclasses should respect the `virtual` lifecycle.** Override the
  documented `virtual` methods (`UpdateText`, `SetValues`, `LateUpdate`, etc.) and
  call `base` unless you mean to replace behavior. Editor-only APIs
  (`AssetDatabase`, `PrefabUtility`, `CreateInstance` for assets) stay inside the
  `CommandScript` / `#if UNITY_EDITOR`, never in shipped runtime code.
- **Don't restructure prefabs/assets the developer authored** without confirming —
  tracked targets reference settings and custom icon prefabs by reference, and a
  broken link fails quietly at runtime.
- **Verify, then report.** State plainly what you built and where; if the console
  shows errors or a component didn't read back, say so rather than assuming
  success. (The user runs the actual in-editor playtest.)
