# Settings: NorthstarOverlaySettings & GlobalNorthstarSettings

Two ScriptableObjects drive Northstar's configuration. `GlobalNorthstarSettings`
is project-wide (one asset); `NorthstarOverlaySettings` is the per-"type" visual
config for the overlay system (make many).

## GlobalNorthstarSettings

`[CreateAssetMenu(fileName = "GlobalNorthstarSettings", menuName = "Northstar/Global Settings")]`.
Singleton (`Instance`), usually auto-created on import. **One per project.**

```csharp
public float NorthAngle { get; set; }   // clamped -180..180 — where north sits on the UI dial; usually 0 (up)
public float WorldNorth { get; set; }   // clamped 0..359.9  — where north is in the world; usually 0
public List<TrackedTargetOverlay> trackedTargetOverlayPrefabs;   // optional registry
```

- **North Angle** rotates the entire compass/radar/nav-bar reading. Leave 0 unless
  your UI "north" isn't straight up.
- **World North** tells Northstar which world direction is north. Leave 0 unless
  your world's north isn't +Z.
- Assign the asset to each Compass/Radar's `northstarSettings` field.
  `Initialize()` is called from `NorthstarSystem.Awake` so the singleton is set in
  builds even without a direct scene reference.

## NorthstarOverlaySettings — the visual config

`[CreateAssetMenu(fileName = "Northstar Overlay Settings", menuName = "Northstar/Overlay Settings")]`.
Each asset is one **look** for a kind of tracked object. Assign one as the default
on `NorthstarOverlay.northstarOverlaySettings`; each `TrackedTargetOverlay` can
point at its own and override individual fields (see the cascade in
`architecture.md` and the override flags in `tracking-objects.md`).

The `type` field is the grouping key (default `"Default"`).

### Full field catalog (verbatim from source)

```csharp
public Vector3 iconOffset = Vector3.zero;
public string  type = "Default";

// Which sub-displays this settings object enables
public bool enableScreenOverlay = true;
public bool enableEdgeOverlay   = true;
public bool enableNavigationBar = true;

// ── Screen overlay (icon drawn over the object on screen) ──
public Sprite screenSprite;
public Color  screenSpriteColor = Color.white;
public float  screenDistanceMin = 0f;     // show range (world distance)
public float  screenDistanceMax = 200f;
public int    screenSpriteSize  = 100;    // px
public bool   screenSpriteSizeUseCurve   = true;
public AnimationCurve screenSpriteSizeCurve   = AnimationCurve.Linear(0,1,1,0);  // size vs distance%
public float  screenSpriteOpacity = 1f;
public bool   screenSpriteOpacityUseCurve= true;
public AnimationCurve screenSpriteOpacityCurve= AnimationCurve.Linear(0,1,1,0);  // opacity vs distance%

// ── Edge arrow (points at off-screen objects) ──
public Sprite arrowSprite;                // null = no arrow
public int    arrowSize        = 30;
public int    arrowSizeInitial = 10;      // size at the start of the screen→edge transition
public int    offset = 5;                 // px from the screen edge
public int    iconOffsetFromArrow = 10;

// ── Edge overlay (icon at the edge) ──
public Sprite edgeSprite;
public Color  edgeSpriteColor   = Color.white;
public Color  arrowColor        = Color.white;
public Color  arrowColorInitial = Color.white;   // arrow color before reaching the edge
public float  edgeDistanceMin = 0f;
public float  edgeDistanceMax = 200f;
public bool   rotateArrow = true;
public int    edgeSpriteSize = 100;
public bool   edgeSpriteSizeUseCurve = true;
public AnimationCurve edgeSpriteSizeCurve = AnimationCurve.Linear(0,1,1,0);
public bool   edgeSizeByDistance    = false;
public bool   edgeOpacityByDistance = false;

// Arrow size / opacity curves
public bool   arrowSizeUseCurve = false;
public AnimationCurve arrowSizeCurve = AnimationCurve.Linear(0,1,1,0);
public bool   arrowSizeByDistance    = false;
public bool   arrowOpacityByDistance = false;
public bool   arrowOpacityUseCurve   = false;
public AnimationCurve arrowOpacityCurve = AnimationCurve.Linear(0,1,1,0);

// Edge opacity
public float  edgeSpriteOpacity = 1f;
public bool   edgeSpriteOpacityUseCurve = true;
public AnimationCurve edgeSpriteOpacityCurve = AnimationCurve.Linear(0,1,1,0);
public float  arrowOpacity        = 1f;
public float  arrowOpacityInitial = 0f;          // arrow opacity at the start of the transition

// ── Navigation bar (horizontal compass bar) ──
public bool   overrideYPosition = false;
public float  xPosition = 0;
public float  yPosition = 0;
public bool   moveWithRotation = true;           // icon slides along the bar with heading…
public Sprite navigationBarSprite;               // …else it stays at xPosition
public Color  navigationBarSpriteColor = Color.white;
public float  navigationBarDistanceMin = 0f;     // [FormerlySerializedAs("compassBarDistanceMin")]
public float  navigationBarDistanceMax = 200f;   // [FormerlySerializedAs("compassBarDistanceMax")]
public int    navigationBarSize = 100;
public bool   compassBarSizeByDistance    = false;
public bool   compassBarOpacityByDistance = false;
public bool   navigationBarSizeUseCurve = true;
public AnimationCurve navigationBarSizeCurve = AnimationCurve.Linear(0,1,1,0);
public float  navigationBarSpriteOpacity = 1f;
public bool   navigationBarSpriteOpacityUseCurve = true;
public AnimationCurve navigationBarSpriteOpacityCurve = AnimationCurve.Linear(0,1,1,0);
public bool   fadeAtEdges = true;
public bool   clampPositionAtEdges = false;

// Optional nav-bar arrow shown when the target is off the bar
public bool   showArrowWhenOffBar = false;
public Sprite compassBarArrowSprite;
public Color  compassBarArrowSpriteColor = Color.white;
public int    compassBarArrowSize = 30;
public float  compassBarArrowOpacity = 1f;
```

### How the distance curves work

When `*UseCurve` is on, the corresponding `*Curve` is evaluated at the **distance
percentage** within `[distanceMin, distanceMax]` to scale size or opacity. The
default `Linear(0,1,1,0)` means "full at the near end, fades to zero at the far
end." Flip it (`Linear(0,0,1,1)`) for "grows with distance." This is how you make
an icon fade out as the player gets close, or shrink in the distance.

## Creating a settings asset from code

```csharp
internal class CommandScript : IRunCommand
{
    public void Execute(ExecutionResult result)
    {
        var s = ScriptableObject.CreateInstance<NorthstarOverlaySettings>();
        s.type = "Objective";
        s.screenSprite      = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/.../star.png");
        s.screenSpriteColor = new Color(1f, 0.85f, 0.2f);   // gold
        s.enableEdgeOverlay = true;                         // arrow off-screen
        s.arrowSprite       = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/.../arrow.png");
        // fade the on-screen icon out as the player closes in:
        s.screenSpriteOpacityUseCurve = true;
        s.screenSpriteOpacityCurve    = AnimationCurve.Linear(0, 0, 1, 1);

        AssetDatabase.CreateAsset(s, "Assets/Northstar/Overlay Settings - Objective.asset");
        AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
        result.Log("Created Objective overlay settings.");
    }
}
```

Then either set it as the master default
(`overlay.SetNorthstarOverlaySettings(s)`) or assign it to a target's
`TrackedTargetOverlay.overlaySettings` and flip the relevant `overrideX` flags.

> **Tip:** the fastest path is usually to **duplicate a shipped preset** and tweak
> a field or two, rather than building one from scratch. Find them with
> `AssetDatabase.FindAssets("t:NorthstarOverlaySettings")`.

## Shipped presets (examples to duplicate)

Under `Assets/**/Northstar*/Northstar Overlay Settings/`:

- `Northstar Overlay Settings - Default`
- `Northstar Overlay Settings - Wolf`
- `Northstar Overlay Settings - Locations`
- `Northstar Overlay Settings - Blue Berries`  ← the demo pickup look
- Navigation-bar presets: `… - Navigation Bar 1/2/3 - North`,
  `… - Cardinal Directions Small/Big`, `… - Cardinal Direction Ticks`,
  `… - Nav Bar 3 - Default Settings`

These show the range of looks: a simple POI marker, a themed enemy, location pins,
the Berries pickup, and the cardinal-direction / tick markers that populate the
navigation bar as evergreen objects.
