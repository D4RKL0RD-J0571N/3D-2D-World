# Runtime API: getting icons & overriding values at play time

Everything here runs at **play time** in normal runtime C# (not editor code). The
two themes: (1) icons exist only after Play starts, so get a reference safely;
(2) once you have it, change its look with the `SetOverride*` / `ResetOverride*`
API.

## Get the icon (the play-start timing trap)

A tracked-target component creates its icon from an `OnEnable` coroutine that
waits for the system singleton. So `ScreenIcon` / `NavigationBarIcon` /
`compassIcon` are **null in `Awake` and possibly for a few frames after**. Two
safe patterns:

### Subscribe to `OnIconCreated` (overlay only)

```csharp
private OverlayIcon _icon;

private void Awake()
{
    var t = GetComponent<TrackedTargetOverlay>();
    t.OnIconCreated += i => _icon = i;   // fires once the screen icon is created
}
```

### Poll in a coroutine (works for any icon)

```csharp
using System.Collections;
using UnityEngine;
using MagicPigGames.Northstar;

public class TrackedReactor : MonoBehaviour
{
    private TrackedTargetOverlay _tracked;
    private OverlayIcon _icon;

    private void Awake()
    {
        _tracked = GetComponent<TrackedTargetOverlay>();
        StartCoroutine(WaitForIcon());
    }

    private IEnumerator WaitForIcon()
    {
        while (_icon == null)
        {
            _icon = _tracked.ScreenIcon;          // or .NavigationBarIcon
            if (_icon != null) break;
            yield return null;
        }
        // icon is ready — read distance, react, override…
    }

    private void Update()
    {
        if (_icon == null) return;
        if (_icon.DistanceToTarget < 5f)
            _tracked.ScreenIcon.SetOverrideColor(Color.red);
    }
}
```

The compass equivalent: `GetComponent<TrackedTargetCompassRadar>().compassIcon`
(poll until non-null).

Useful read-only members once you have an icon:

```csharp
OverlayIcon: DistanceToTarget, PercentToEdge (0=on-screen,1=on-edge),
             IsNavigationBarIcon, TrackedObject, TrackedTransform, TrackedPosition,
             ScreenDistancePercent, EdgeDistancePercent, NavigationBarDistancePercent
CompassIcon: DistanceToTarget(cache), YDelta(cache), DirectionToTarget(cache), Angle
```

## Override API — change a look at runtime

Every `SetOverride*` has **two signatures** — one for a single icon, one for all
icons of a `string` type — and a matching `ResetOverride*` that clears it (returns
to the settings cascade). Call them on the relevant singleton.

### Compass / Radar — `NorthstarSystem.Instance`

```csharp
NorthstarSystem.Instance.SetOverrideColor(compassIcon, Color.red);
NorthstarSystem.Instance.SetOverrideOpacity(compassIcon, 0.5f);
NorthstarSystem.Instance.SetOverrideScale(compassIcon, Vector3.one * 1.5f);

NorthstarSystem.Instance.SetOverrideColor("Enemy", Color.red);     // every "Enemy" blip
NorthstarSystem.Instance.ResetOverrideColor(compassIcon);          // back to default
NorthstarSystem.Instance.ResetOverrideOpacity("Enemy");
```

(`Compass` and `Radar` both inherit these — `NorthstarSystem.Instance` is the one
live system, whichever it is.) You can also call
`compassIcon.SetOverrideColor/Opacity/Scale(...)` directly on the icon.

### Screen / Edge Overlay — `NorthstarOverlay.Instance`

Per-icon and per-type variants exist for each (with `ResetOverride*` twins):

```csharp
SetOverrideColor            // both screen + edge
SetOverrideScreenColor
SetOverrideEdgeColor
SetOverrideArrowColor
SetOverrideArrowColorInitial
SetOverrideSprite
SetOverrideMaxOpacity       // cap opacity
SetOverrideMaxSize          // cap size
SetOverrideDistanceMin / SetOverrideDistanceMax
SetOverrideScreenDistanceMin / …Max
SetOverrideEdgeDistanceMin / …Max
```

```csharp
var icon = trackedTargetOverlay.ScreenIcon;
NorthstarOverlay.Instance.SetOverrideColor(icon, Color.red);
NorthstarOverlay.Instance.SetOverrideSprite(icon, alertSprite);
NorthstarOverlay.Instance.SetOverrideMaxOpacity("Objective", 0.4f);   // dim all objectives
NorthstarOverlay.Instance.ResetOverrideColor(icon);
// or directly on the icon:
icon.SetOverrideMaxSize(150f);
icon.ResetOverrideMaxSize();
```

### Navigation Bar — `NavigationBar.Instance`

```csharp
SetOverrideColor
SetOverrideNavigationBarMaxOpacity
SetOverrideNavigationBarMaxSize
SetOverrideNavigationBarSprite
SetOverrideNavigationBarYPosition
SetOverrideNavigationBarXPosition
SetOverrideNavigationBarDistanceMin / …Max
```

```csharp
var navIcon = trackedTargetOverlay.NavigationBarIcon;
NavigationBar.Instance.SetOverrideColor(navIcon, Color.cyan);
NavigationBar.Instance.SetOverrideNavigationBarMaxSize("Quest", 60f);
NavigationBar.Instance.ResetOverrideColor(navIcon);
```

> The `…NavigationBar…` (formerly `…CompassBar…`) naming reflects the rename from
> "Compass Bar." If a method name doesn't resolve, check for the `CompassBar`
> spelling in this project's version.

## Add / remove targets in code

Usually you just `AddComponent` the tracked-target component and it self-registers
(see `tracking-objects.md`). To drive registration directly:

```csharp
// Compass/Radar
CompassIcon icon = NorthstarSystem.Instance.AddTarget(trackedTargetCompassRadar);
NorthstarSystem.Instance.RemoveTarget(someTransform);

// Overlay
OverlayIcon screenIcon = NorthstarOverlay.Instance.AddScreenOverlay(trackedTargetOverlay);
NorthstarOverlay.Instance.RemoveTarget(trackedTargetOverlay);
OverlayIcon current = NorthstarOverlay.Instance.GetIcon(trackedTargetOverlay);

// or via the component
trackedTargetOverlay.RemoveRegistration();
trackedTargetCompassRadar.RemoveRegistration();
```

`RemoveTarget` destroys the icon GameObject — don't keep a stale reference after
removing (or after the component is disabled, which removes it too).

## Pausing

`NorthstarSystem.PauseLevel` takes a `NorthstarPauseLevel`:

```csharp
NorthstarSystem.Instance.PauseLevel = NorthstarPauseLevel.FullPause;     // freeze everything
// FullPause | PauseRotation | PauseMovement | Unpaused
```

Use it to freeze the compass during a cutscene or menu, then set back to
`Unpaused`.

## Gotchas

- **Null icon = you read it too early.** Always poll / use `OnIconCreated`; never
  assume an icon in `Awake` or `Start`.
- **Overrides persist until reset.** They sit on top of the settings cascade. If a
  target should return to normal (enemy de-aggros), call the matching
  `ResetOverride*` — don't just stop setting it.
- **Per-type overrides apply to icons that exist now.** Targets registered later
  inherit the cascade, not a previously-set type override, unless you re-apply it.
- **Use one singleton per system.** `NorthstarSystem.Instance` is the active
  Compass or Radar; `NorthstarOverlay.Instance` and `NavigationBar.Instance` are
  the overlay and bar. They're null if that display isn't in the scene — guard
  before calling.
