# Tracking objects & setting their values

To make a GameObject show up in Northstar, add the tracked-target component for
each system you want it on. They're independent:

| Want it on… | Add this component | Icon it creates |
|---|---|---|
| the compass / radar dial | `TrackedTargetCompassRadar` | `CompassIcon` |
| screen / edge / navigation bar | `TrackedTargetOverlay` | `OverlayIcon` |
| everywhere | **both** | both |

Both auto-register on `OnEnable` (a coroutine waits for the system singleton) and
auto-unregister on `OnDisable`. **The icon is created on Play, not at edit time.**

## Compass / radar: `TrackedTargetCompassRadar`

```csharp
public TrackingType trackingType = TrackingType.RelativePoint;
public GameObject trackedObjectPrefab;        // icon for RelativePoint mode; else the system's defaultTrackedIcon
public bool adjustBasedOnPlayerIcon = false;  // rotate relative to the player icon
public string targetType = "Default";         // grouping key for bulk runtime overrides
[HideInInspector] public CompassIcon compassIcon;  // runtime only
```

### TrackingType — how the icon behaves on the dial

| Value | Meaning |
|---|---|
| `RelativePoint` | **The usual "where is it" mode.** Icon sits at the object's bearing/distance on the dial. |
| `PointAtTarget` | Icon rotates to *point at* a target (with optional `adjustBasedOnPlayerIcon`). |
| `PointNorth` | Icon always points to north (e.g. a compass needle). |
| `PlayerForward` | Icon points where the player faces. |
| `DoNotMove` | Static — for fixed decorations. |

### Add it via unity-mcp

```csharp
internal class CommandScript : IRunCommand
{
    public void Execute(ExecutionResult result)
    {
        var go = GameObject.Find("Enemy");
        var t = go.GetComponent<TrackedTargetCompassRadar>() ?? go.AddComponent<TrackedTargetCompassRadar>();
        t.trackingType = MagicPigGames.Northstar.GlobalNorthstarSettings.TrackingType.RelativePoint;
        t.targetType = "Enemy";
        // t.trackedObjectPrefab = Resources.Load<GameObject>("Target Icon Wolf"); // optional custom blip
        EditorUtility.SetDirty(go);
        AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
        result.Log("Enemy now tracked on the compass/radar.");
    }
}
```

### Line of sight (compass icons only)

Add `RequireLineOfSightCompassIcon` to the **icon** (it has
`[RequireComponent(typeof(CompassIcon))]`, so add it to the icon prefab or the
created icon) to hide the blip when the target isn't visible:

```csharp
public Transform playerTransform;       // defaults to Camera.main if null
public bool mustBeInCameraView = true;  // also require the target in the camera frustum
public LayerMask layerMask = -1;        // raycast mask — EXCLUDE the player/own layers or it flickers
```

It raycasts player → target each `LateUpdate` and enables the icon only if the
first hit is the target (or a child) and the target is within `detectionRange`.

## Screen / edge / nav bar: `TrackedTargetOverlay`

The fields you set most often:

```csharp
public NorthstarOverlaySettings overlaySettings;  // THIS target's settings (read when overriding)
public Vector3 iconOffset = Vector3.zero;         // nudge the icon off the pivot (e.g. above the head)
public string targetType = "Default";             // grouping key for bulk runtime overrides
public int layerOrder = 0;                         // draw order; negative draws behind higher
public GameObject customScreenIcon;                // swap the default screen-icon prefab for this target
public GameObject customNavigationBarIcon;         // swap the default nav-bar-icon prefab
public bool overrideScreenOverlay = false;         // turn screen overlay on/off for this target
public bool overrideEdgeOverlay = false;           // …edge overlay
public bool overrideNavigationBar = false;         // …navigation bar
public bool ignoreRadar = false;
public bool fixedNavigationBarAngle = false;       // pin this icon to a fixed bar angle…
public float fixedAngle = 0f;                       // …this one (e.g. an always-"N" marker)
```

### Add it via unity-mcp

```csharp
internal class CommandScript : IRunCommand
{
    public void Execute(ExecutionResult result)
    {
        var go = GameObject.Find("Enemy");
        var t = go.GetComponent<TrackedTargetOverlay>() ?? go.AddComponent<TrackedTargetOverlay>();
        t.targetType = "Enemy";
        t.layerOrder = 0;
        t.iconOffset = new Vector3(0, 2f, 0);    // float the icon above the model
        // Show a distance label: use the shipped icon prefab that has a DistanceText child.
        // NOTE: icon prefabs are NOT in a Resources folder (only the compass/radar/nav-bar/
        // overlay *display* prefabs are), so locate them by name with AssetDatabase, not Resources.Load.
        var iconGuid = AssetDatabase.FindAssets("Northstar Icon Distance t:Prefab")[0];
        t.customScreenIcon = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(iconGuid));
        // Give it a look:
        t.overlaySettings = AssetDatabase.LoadAssetAtPath<NorthstarOverlaySettings>(
            "Assets/.../Northstar Overlay Settings - Wolf.asset");
        EditorUtility.SetDirty(go);
        AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
        result.Log("Enemy now has a screen overlay icon with distance text.");
    }
}
```

### Custom icon graphics for one target

Two ways, both edit-time:

- **`overlaySettings`** — point the target at a `NorthstarOverlaySettings` asset
  with a different sprite/color/size and flip the matching `overrideX` toggles
  (see the override list below). Best when many targets share one look.
- **`customScreenIcon` / `customNavigationBarIcon`** — give the target a different
  icon **prefab** entirely (its own hierarchy, child text, progress bar, etc.).
  Best for a one-off or a richer icon. The prefab's root needs an `OverlayIcon`.

Runtime changes (recolor on aggro, swap sprite on objective complete) use the
override API in `runtime-api.md` — they don't touch these edit-time fields.

## The per-target override flags (full list)

Each `overrideX` bool means "for this target, use the value from **my**
`overlaySettings` instead of the master default." Set the bool **and** assign an
`overlaySettings` asset that holds the value. They map onto the
`NorthstarOverlaySettings` fields documented in `overlay-settings.md`.

**System enable:** `overrideScreenOverlay`, `overrideEdgeOverlay`,
`overrideNavigationBar`.

**Screen:** `overrideScreenSprite`, `overrideScreenSpriteSize`,
`overrideScreenSpriteColor`, `overrideScreenSpriteOpacity`, `overrideScreenDistance`,
`overrideScreenSizeCurve`, `overrideScreenUseSizeCurve`, `overrideScreenOpacityCurve`,
`overrideScreenUseOpacityCurve`.

**Edge:** `overrideEdgeSprite`, `overrideEdgeSpriteSize`, `overrideEdgeSpriteColor`,
`overrideEdgeSpriteOpacity`, `overrideEdgeDistance`, `overrideEdgeSizeCurve`,
`overrideEdgeUseSizeCurve`, `overrideEdgeOpacityCurve`, `overrideEdgeUseOpacityCurve`,
`overrideEdgeArrow`.

**Arrow:** `overrideArrowSprite`, `overrideArrowColor`, `overrideArrowColorInitial`,
`overrideArrowOpacity`, `overrideArrowOpacityInitial`, `overrideArrowSize`,
`overrideArrowSizeInitial`, `overrideArrowRotation`, `overrideArrowSizeCurve`,
`overrideArrowUseSizeCurve`, `overrideArrowOpacityCurve`, `overrideArrowUseOpacityCurve`.

**Navigation bar:** `overrideNavigationBarSprite`, `overrideNavigationBarSpriteSize`,
`overrideNavigationBarSpriteColor`, `overrideNavigationBarSpriteOpacity`,
`overrideNavigationBarDistance`, `overrideNavigationBarSizeCurve`,
`overrideNavigationBarUseSizeCurve`, `overrideNavigationBarOpacityCurve`,
`overrideNavigationBarUseOpacityCurve`, `overrideNavigationBarMoveWithRotation`,
`overrideNavigationBarYPosition`, `overrideNavigationBarXPosition`
(+ `overrideNavigationBarXPositionValue`).

**Options:** `overrideClampPositionAtEdges`, `overrideFadeAtEdges`,
`overrideOffset`, `overrideIconOffsetFromArrow`.

## Reading the icon at runtime (the timing trap)

`ScreenIcon` / `NavigationBarIcon` (overlay) and `compassIcon`
(`TrackedTargetCompassRadar`) are **null until the system creates them on Play**.
Never read them in `Awake`. Either subscribe to `OnIconCreated` or poll:

```csharp
private TrackedTargetOverlay _tracked;
private OverlayIcon _icon;

private void Awake()
{
    _tracked = GetComponent<TrackedTargetOverlay>();
    _tracked.OnIconCreated += i => _icon = i;     // fires once the screen icon exists
}
```

Full poll-for-icon coroutine and the runtime override API are in
`runtime-api.md`.

## Gotchas

- **Nothing shows without a matching display in the scene.** `TrackedTargetOverlay`
  needs a `NorthstarOverlay`; `TrackedTargetCompassRadar` needs a `Compass`/`Radar`.
- **An override needs both halves:** the `overrideX` bool *and* an `overlaySettings`
  asset to read from. The bool alone does nothing.
- **`customScreenIcon` root must have an `OverlayIcon`** (and the nav-bar one too),
  or `AddScreenOverlay` returns null and logs a warning.
- **Disabling the component removes the registration** (and destroys the icon).
  Re-enabling re-registers and creates a fresh icon — don't cache an old reference
  across a disable/enable.
- **`targetType` is free-form and optional**, but reusing a consistent string is
  what makes the per-type runtime overrides (`SetOverrideColor("Enemy", …)`) work.
