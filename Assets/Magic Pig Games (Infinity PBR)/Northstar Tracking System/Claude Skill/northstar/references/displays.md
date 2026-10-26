# Displays: Compass, Radar, Navigation Bar, Distance Text

The display widgets are what the player actually sees. Each is configured by
serialized fields and can be swapped for a shipped prefab variant. All are
designed for subclassing (the comments in source say so) — see `custom-logic.md`.

## Compass / Radar

Both extend `NorthstarSystem` (the shared base — `player`, `defaultTrackedIcon`,
ranges, fades, `northstarSettings`; full list in `architecture.md`). **Only one
Compass *or* Radar per scene.**

### Compass

A thin subclass — the base behavior with no extra fields. Add `Compass 1`..`17`
from `Resources` for different visual styles.

### Radar (`Radar : NorthstarSystem`)

A compass that also sweeps/pings. Extra fields:

```csharp
public bool rotateWithPlayer = true;     // radar rotates to follow the player's heading
public GameObject radarLineObject;       // the rotating scan line (a RadarLine)
public float radarRotationSpeed = 180;   // deg/sec; 0 = no line, ping on interval instead
public float pingInterval = 2f;          // seconds between pings when there's no line
public bool fadeRadarPings = true;
public float radarPingFadeTime = 1f;
public AnimationCurve radarPingFadeCurve = AnimationCurve.Linear(0,1,1,0);
public bool showPingMovement = false;    // pings keep moving, or freeze where painted
```

`Radar` decides its paint strategy automatically: with a `radarLineObject` it
pings each blip as the line sweeps over its bearing; without one it pings all in
range every `pingInterval`. `RadarInstance` is its singleton accessor.

### RadarLine

The rotating UI line (`Resources/Radar Line` / under the radar prefab).
`clockwise` toggles direction; it reads `Radar.radarRotationSpeed` each frame.
`SetAdditionalRotation(float)` applies a one-shot offset. Not meant for
subclassing.

## Navigation Bar (`NavigationBar`)

The horizontal compass bar; singleton `NavigationBar.Instance`. **Must be parented
under the `NorthstarOverlay` object.** Key fields:

```csharp
public NorthstarOverlay northstarOverlay;
public float visibleAngle = 180f;   // total FOV of the bar (180 = 90° each side)
public float fadeAngle    = 85f;    // angle where icons begin to fade
public float fadeAngleEnd = 120f;   // angle where icons are fully gone (can exceed half of visibleAngle)
public NorthstarOverlaySettings northstarOverlaySettings;
public List<GameObject> evergreenObjects;   // always-shown markers (N/E/S/W, ticks, "forward")
```

**Evergreen objects** are static bar markers — cardinal directions, tick marks, a
forward indicator — each carrying a `NavigationBarEvergreenObject`:

```csharp
[Range(-180,180)] public float angle;   // fixed bearing on the bar
public bool  overrideYPosition = false;
public float yPos = 0;
public bool  moveWithRotation = true;
public bool  fadeAtEdges = true;
public bool  clampPositionAtEdges = false;
public bool  useMaxAlpha = false;
[Range(0,1)] public float maxAlpha = 1f;
```

To add your own evergreen marker, drop a UI object with a
`NavigationBarEvergreenObject` into the bar's `evergreenObjects` list and set its
`angle`. `PlaceEvergreenObject(...)` positions it. The bar's placement/fade math
(`GetPositionOnBar`, `PlaceObject`, `FadeAtEdges`, `ComputeEdgeFade`, `AddOverlay`)
and the `SetOverride*` family are all `virtual`.

## Distance Text (`DistanceText`)

Add to (a child of) an `OverlayIcon` prefab to show the distance to the tracked
object. Reads `OverlayIcon.DistanceToTarget`. The shipped
`Northstar Icon Distance` prefab already has one.

```csharp
[Header("Required")] public TMP_Text text;
[Header("Options")]
public bool   overrideColor = false;
public Color  color = Color.white;
public int    maxDecimals = 2;       // decimals shown
public int    maxDigits = 3;         // significant digits (short-scale: 1.5K, 2.3M…)
public string meters = " m";         // unit suffix
public bool   matchIconOpacity = true;
public bool   matchIconSize = true;
public bool   screenAnchoredPositionYOffset = true;
public float  screenAnchoredPositionYOffsetPercent = 0.6f;
public bool   compassBarAnchoredPositionYOffset = true;
public float  compassBarAnchoredPositionYOffsetPercent = 0.6f;
```

Default text is `ShortScaleString(distance) + meters`. To change the **wording or
logic** (e.g. "CLOSE/FAR", time-to-reach, localized units), subclass it and
override `UpdateText()` — see `custom-logic.md`.

## Shipped prefab catalog (`Resources/`)

The display prefabs live in a `Resources` folder so they load by name and appear
in the Northstar window. Under `Assets/**/Northstar*/Prefabs/`:

| Name (`Resources.Load`) | What |
|---|---|
| `Northstar Screen Overlay` | the `NorthstarOverlay` master (screen + edge + nav-bar host) |
| `Compass 1` … `Compass 17` | compass styles |
| `Radar 1` … `Radar 5` | radar styles |
| `Navigation Bar 1` … `Navigation Bar 3` | compass-bar styles |

Icon & part prefabs (not in `Resources`, referenced as `customScreenIcon` etc.):

- `Icons - Overlay/Northstar Icon` — default screen icon
- `Icons - Overlay/Northstar Icon Distance` — icon + a `DistanceText`
- `Icons - Compass/…` — `North Point`, `Player Forward`, `Location Circle`,
  `Target Icon Default / Wolf / Blue Berries`, point-at-target variants
- `Navigation Bar 1/2/3/…` — per-bar direction, tick, and forward-arrow icons
- `Radar/Radar Line`
- `Additional Objects/Northstar Compass Title Bar`
- demo: `Custom Overlay Icons/Northstar Icon - Blue Berries`,
  `Northstar Icon Health Bar`

Sprite source folders: `Sprites/Cardinal Directions`, `Compass-Radar Parts`,
`Horizontal Compass Bar Elements`, `Gem Icons`, `Symbols`, `Alphas`.

## Changing the prefabs / overriding graphics

Two levels of customization, both supported:

1. **Re-skin via settings** (no new prefab): change sprites/colors/sizes on a
   `NorthstarOverlaySettings` (`overlay-settings.md`). This covers most "make the
   icon look different" requests.

2. **Duplicate and edit a display prefab** (new compass/radar/nav-bar style):
   - Duplicate a `Resources/Compass N` (or Radar/Nav Bar), **keep it in a
     `Resources` folder**, and give it a unique name — it then auto-appears as a
     button in the Northstar window.
   - Edit the prefab (swap the dial art, add a title bar, change layers). Changes
     made in Play mode are saved on exit.
   - For a custom nav bar, also copy its evergreen objects and re-point their
     per-object overlay settings.

3. **Custom icon prefab for a target**: assign `TrackedTargetOverlay.customScreenIcon`
   / `customNavigationBarIcon` (must have an `OverlayIcon` on the root), or
   `TrackedTargetCompassRadar.trackedObjectPrefab` (a `CompassIcon`).

Runtime graphic changes (recolor, swap sprite, resize, fade) go through the
override API — see `runtime-api.md`.

## Gotchas

- **One Compass *or* Radar; one Navigation Bar.** The window enforces it; code
  should too (check `FindObjectOfType` first).
- **Nav bar must be a child of the overlay** and have a `NorthstarOverlaySettings`
  assigned, or it won't position icons.
- **Duplicated display prefabs outside `Resources` won't be found** by
  `Resources.Load` or the window.
- **`radarRotationSpeed = 0` disables the sweep line** and switches the radar to
  interval pinging — make sure a `radarLineObject` is assigned only when you want
  the sweep.
