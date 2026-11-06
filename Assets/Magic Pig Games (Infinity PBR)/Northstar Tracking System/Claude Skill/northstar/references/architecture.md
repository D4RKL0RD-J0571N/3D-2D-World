# Northstar architecture

Namespace `MagicPigGames.Northstar`. Source lives in
`Assets/**/Northstar*/Scripts/`. This file is the object model: load it before
writing code so you use the real class, field, and method names.

## The two systems

Northstar is **two independent systems**. Each has a scene UI component, a
tracked-target component you add to the world object, and an icon created at
runtime. An object can be on one, the other, or both.

```
COMPASS / RADAR system                     SCREEN OVERLAY system
──────────────────────                     ─────────────────────
NorthstarSystem (abstract, singleton)      NorthstarOverlay (singleton)
   ├─ Compass : NorthstarSystem               ├─ screen icons (over objects)
   └─ Radar   : NorthstarSystem               ├─ edge icons + arrows (off-screen)
                                              └─ NavigationBar (singleton, horizontal bar)
add to world object:                       add to world object:
   TrackedTargetCompassRadar                  TrackedTargetOverlay
runtime icon:                              runtime icon:
   CompassIcon                                OverlayIcon (+ optional DistanceText)
```

Settings shared by both: `GlobalNorthstarSettings` (one per project).
Settings for the overlay system: `NorthstarOverlaySettings` (many; per "type").

## Compass / Radar system

### `NorthstarSystem` (abstract `MonoBehaviour`, singleton)

The base class for `Compass` and `Radar`. `public static NorthstarSystem Instance`.
You never add `NorthstarSystem` directly — you add a `Compass` or a `Radar`.

Key serialized fields:

```csharp
[Header("Required")]
public Transform player;                 // MUST be set by hand — not auto-assigned
public GameObject defaultTrackedIcon;    // CompassIcon prefab used when a target has none

[Header("Distance to Target")]
public float detectionRange = 200;       // beyond this, not tracked
public Vector2 rangeFromCenter = new(10, 100);     // UI px: center → edge of compass/radar
public Vector2 objectDistanceRange = new(0, 50);   // world distance mapped onto the dial
public bool fadeOnDistance = true;
public Vector2 maxDistanceFadeRange = new(50, 60);
public bool fadeOnYDelta = true;                   // fade by height difference
public Vector2 maxYDeltaFadeRangeAbove = new(10, 50);
public Vector2 maxYDeltaFadeRangeBelow = new(-50, -10);

[Header("Active Objects - Set at Edit Time")]
public List<CompassIcon> presetTrackedObjects = new();  // child icons always shown (e.g. North)

public GlobalNorthstarSettings northstarSettings;
public NorthstarPauseLevel PauseLevel = NorthstarPauseLevel.Unpaused;
```

Key API:

```csharp
public CompassIcon AddTarget(TrackedTargetCompassRadar t);   // register → returns the new CompassIcon
public virtual void RemoveTarget(Transform target);          // unregister + destroy the icon
public Dictionary<Transform, CompassIcon> CompassIcons { get; }
public GlobalNorthstarSettings NorthstarSettings => northstarSettings;
public CompassIcon PlayerIcon { get; }                       // the "player forward" icon, if any

// Runtime overrides (per-icon and per-type) — see runtime-api.md
public virtual void SetOverrideColor   (CompassIcon icon, Color value);
public virtual void SetOverrideOpacity (CompassIcon icon, float value);
public virtual void SetOverrideScale   (CompassIcon icon, Vector3 value);
public virtual void SetOverrideColor   (string type, Color value);   // + Opacity / Scale
// …each has a matching ResetOverride*.
```

`Compass : NorthstarSystem` is a thin subclass (singleton `Compass.CompassInstance`,
calls `SetDictionary()` to register preset objects). `Radar : NorthstarSystem`
adds the sweep/ping behavior (see `displays.md`). **Only one Compass *or* Radar per
scene.**

### `TrackedTargetCompassRadar` (`MonoBehaviour`) — add to the world object

```csharp
public TrackingType trackingType = TrackingType.RelativePoint;
public GameObject trackedObjectPrefab;        // icon for RelativePoint mode (else defaultTrackedIcon)
public bool adjustBasedOnPlayerIcon = false;
public string targetType = "Default";
[HideInInspector] public CompassIcon compassIcon;   // runtime: the created icon
```

On `OnEnable` it starts a coroutine that waits for `NorthstarSystem.Instance`,
then calls `AddTarget(this)` and stores the result in `compassIcon`. On
`OnDisable` it stops the pending coroutine and calls `RemoveRegistration()`.
Convenience: `SetOverrideColor(CompassIcon, Color)` / `ResetOverrideColor(CompassIcon)`.

### `CompassIcon` (`MonoBehaviour`) — the runtime icon

Created by `AddTarget`. **Do not edit its inspector** — the tracked-target
component (or the Compass/Radar, for preset objects) drives it. It reads
`trackingType` to decide how to rotate/position (`SetCompass`, `PointAtTarget`,
`PointAtPlayerForward`), supports `Ping()` for the radar, and exposes
`DistanceToTarget(bool cache)`, `YDelta(bool cache)`, `DirectionToTarget(bool cache)`,
`Angle`, and `SetOverrideColor/Opacity/Scale` (+ resets). Companion component
`RequireLineOfSightCompassIcon` (with `[RequireComponent(typeof(CompassIcon))]`)
hides the icon when there's no line of sight (see `tracking-objects.md`).

## Screen Overlay system

### `NorthstarOverlay` (`MonoBehaviour`, singleton)

`public static NorthstarOverlay Instance`. The master for screen icons, edge
icons/arrows, and (via `NavigationBar`) the nav bar.

Key serialized fields:

```csharp
public NorthstarOverlaySettings northstarOverlaySettings;  // the DEFAULT settings
public Camera targetCamera;                                // REQUIRED, no fallback
public GameObject overlayIconPrefab;                       // default OverlayIcon prefab
public GameObject edgeOverlayPrefab;
public NavigationBar navigationBar;

public bool enableScreenOverlay = true;
public bool enableEdgeOverlay   = false;
public bool enableNavigationBar = false;   // [FormerlySerializedAs("enableCompassBar")]
```

Key API:

```csharp
public virtual OverlayIcon AddScreenOverlay(TrackedTargetOverlay t);  // create + register icon
public virtual void RemoveTarget(TrackedTargetOverlay t);
public OverlayIcon GetIcon(TrackedTargetOverlay t);                    // null until play start
public void SetNavigationBar(NavigationBar nb);
public void SetNorthstarOverlaySettings(NorthstarOverlaySettings s);

// Runtime overrides — per icon AND per type — each with a ResetOverride* twin:
SetOverrideColor / SetOverrideScreenColor / SetOverrideEdgeColor
SetOverrideArrowColor / SetOverrideArrowColorInitial
SetOverrideSprite / SetOverrideMaxOpacity / SetOverrideMaxSize
SetOverrideDistanceMin / SetOverrideDistanceMax (+ Screen/Edge variants)
```

### `TrackedTargetOverlay` (`MonoBehaviour`) — add to the world object

The screen/edge/nav-bar tracked target. The fields you set most:

```csharp
public NorthstarOverlaySettings overlaySettings;  // this target's settings, used when overriding
public Vector3 iconOffset = Vector3.zero;
public string targetType = "Default";             // group key for bulk runtime overrides
public int layerOrder = 0;                         // draw order; negative draws behind higher
public GameObject customScreenIcon;                // swap the default screen icon prefab
public GameObject customNavigationBarIcon;         // swap the default nav-bar icon prefab
public bool overrideScreenOverlay, overrideEdgeOverlay, overrideNavigationBar;  // per-system enable
public bool ignoreRadar = false;
public bool fixedNavigationBarAngle = false;       // lock the nav-bar angle…
public float fixedAngle = 0f;                       // …to this value
// + ~40 bool overrideX flags (one per overridable setting) — full list in tracking-objects.md
```

Runtime members (the important timing detail):

```csharp
public OverlayIcon ScreenIcon { get; }          // lazy: null until NorthstarOverlay creates it
public OverlayIcon NavigationBarIcon { get; }   // lazy: null until NavigationBar creates it
public event Action<OverlayIcon> OnIconCreated; // fires once the screen icon is created
public virtual void RemoveRegistration();
public bool EnableNavigationBar(bool currentOption);
```

On `OnEnable` it waits (coroutine) for `NorthstarOverlay.Instance`, calls
`AddScreenOverlay(this)`, stores `overlayScreenIcon`, and fires `OnIconCreated`.

### `OverlayIcon` (`MonoBehaviour`) — the runtime icon

The on-screen/edge/nav-bar icon. Designed to be subclassed (almost everything is
`virtual`). The properties custom logic reads:

```csharp
public float DistanceToTarget { get; }     // world distance camera → tracked object
public float PercentToEdge { get; }        // 0 = fully on screen, 1 = fully at edge, between = transitioning
public bool IsNavigationBarIcon { get; }
public Image Image { get; }
public virtual GameObject TrackedObject { get; }
public virtual Transform TrackedTransform { get; }
public virtual Vector3 TrackedPosition { get; }
public float ScreenDistancePercent / EdgeDistancePercent / NavigationBarDistancePercent { get; }
```

Plus the same `SetOverride* / ResetOverride*` family as the overlay (color,
sprite, size, opacity, distance — screen / edge / nav-bar / arrow variants).
`Setup(...)` and `SetupNavigationBar(...)` initialize it; `LateUpdate` and the
`virtual` `SetValues()` / `SetNavigationBarValues()` recompute every frame.
Optional `DistanceText` lives on (a child of) the icon prefab (see `custom-logic.md`).

### `NavigationBar` (`MonoBehaviour`, singleton)

The horizontal compass bar; `NavigationBar.Instance`. Parented under the overlay.
Key fields: `visibleAngle` (e.g. 180 = 90° each side), `fadeAngle` / `fadeAngleEnd`,
`evergreenObjects` (always-shown markers like cardinal directions / ticks, each a
`NavigationBarEvergreenObject`). Designed for subclassing — `GetPositionOnBar`,
`PlaceObject`, `FadeAtEdges`, `AddOverlay`, and the `SetOverride*` family are all
`virtual`. Details in `displays.md`.

## Settings ScriptableObjects

### `GlobalNorthstarSettings`

`[CreateAssetMenu(menuName = "Northstar/Global Settings")]`. **One per project**;
usually auto-created on import. Singleton via `OnEnable` and
`Initialize(settings)` (called from `NorthstarSystem.Awake` so it's set in builds).

```csharp
public float NorthAngle  { get; set; }  // where "north" sits on the UI; clamped -180..180 (usually 0)
public float WorldNorth  { get; set; }  // where north is in the world;     clamped 0..359.9 (usually 0)
public List<TrackedTargetOverlay> trackedTargetOverlayPrefabs;  // optional prefab registry
public void AddTrackedTargetOverlayPrefab(TrackedTargetOverlay prefab);
public List<TrackedTargetOverlay> TrackedTargetOverlayPrefabsByType(string targetType);
```

### `NorthstarOverlaySettings`

`[CreateAssetMenu(menuName = "Northstar/Overlay Settings")]`. The per-"type"
visual config — make as many as you want, assign one as the default on
`NorthstarOverlay`, and let each tracked target override fields from its own.
Full field catalog in `overlay-settings.md`. The `type` string (default
`"Default"`) is the grouping key.

## The settings cascade (how a final value is chosen)

For any overlay visual (sprite, color, size, opacity, distance):

```
1. Runtime override   (OverlayIcon.SetOverride* / NorthstarOverlay.SetOverride*)   ← wins if set
2. Per-target value   (TrackedTargetOverlay.overlaySettings) IF its overrideX flag is on
3. Master default     (NorthstarOverlay.northstarOverlaySettings)                  ← fallback
```

A per-target override needs **both** its `overrideX` toggle on **and** an
`overlaySettings` asset assigned to read the value from. Runtime overrides use the
`-1f` / `bool` sentinel fields on `OverlayIcon` and are cleared by the matching
`ResetOverride*`.

## Enums (in `GlobalNorthstarSettings`)

```csharp
public enum NorthstarPauseLevel { FullPause, PauseRotation, PauseMovement, Unpaused }
public enum TrackingType        { PointNorth, PointAtTarget, RelativePoint, DoNotMove, PlayerForward }
```

`using static MagicPigGames.Northstar.GlobalNorthstarSettings;` lets you write
`TrackingType.RelativePoint` unqualified, which the tracked-target components do.

## Lifecycle / gotchas

- **Icons are created on Play, not at edit time.** `AddTarget` /
  `AddScreenOverlay` run from the tracked-target component's `OnEnable` coroutine
  *after* the system singleton exists. So `ScreenIcon` / `NavigationBarIcon` /
  `compassIcon` are null in `Awake` — poll for them (see `runtime-api.md`).
- **Singletons are first-one-wins / last-destroyed.** A second Compass/Radar,
  `NorthstarOverlay`, or `NavigationBar` will conflict. Keep one of each.
- **`player` (Compass/Radar) and `targetCamera` (overlay) have no fallback** —
  unset means nothing tracks. Always wire them.
- **No core events except `TrackedTargetOverlay.OnIconCreated`.** The systems are
  poll/update-driven, not event-driven; use `OnIconCreated` (or a coroutine) to
  know when an icon is ready.
- **`NorthstarUtilities`** (static) has the math helpers (`GetAngleBetweenPoints`,
  `EdgeScreenPosition`, `ShortScaleString`, `SortLayers`); `NorthstarExtensions`
  adds `float.Between` / `float.BetweenAngle`.
