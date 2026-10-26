# Custom logic: distance text, custom icons, and icon "actions"

Northstar is built to be extended two ways:

- **Subclass** a `virtual`-heavy core class (`DistanceText`, `OverlayIcon`,
  `CompassIcon`, `NavigationBar`, `Compass`, `Radar`) to change how something is
  computed or drawn.
- **Attach a component to the icon prefab** that reads the icon's live state
  (`DistanceToTarget`, `PercentToEdge`, `TrackedObject`) and does whatever you
  want — a progress bar, a health bar, a label, an interaction. This is how the
  demo's Berries pickup and floating health bar work, and it's the most common
  "custom action."

All of this is **runtime** code (no editor APIs). Read `runtime-api.md` for the
play-start timing rule — it applies here too.

## 1. Custom distance text — subclass `DistanceText`

`DistanceText` already shows distance in meters with short-scale notation. To
change the **wording or logic**, subclass it and override `UpdateText()`. The
parent caches the parent `OverlayIcon` as `_icon` and the last string as
`_lastDistanceText` (both are `private` in the base — use the public
`OverlayIcon` members and your own cache field).

The base `UpdateText()` is:

```csharp
protected virtual void UpdateText()
{
    var newText = $"{NorthstarUtilities.ShortScaleString(_icon.DistanceToTarget, maxDecimals, maxDigits)}{meters}";
    if (newText == _lastDistanceText) return;
    _lastDistanceText = newText;
    text.text = newText;
}
```

`_icon` is `private` in the base, so a subclass reaches the icon via the component
tree. Example — show a word band instead of a number:

```csharp
using UnityEngine;

namespace MyGame
{
    public class WordDistanceText : MagicPigGames.Northstar.DistanceText
    {
        public float near = 8f, mid = 30f;
        private MagicPigGames.Northstar.OverlayIcon _icon;
        private string _last;

        protected override void Awake()
        {
            base.Awake();                                   // base caches text + rect transforms
            _icon = GetComponentInParent<MagicPigGames.Northstar.OverlayIcon>();
        }

        protected override void UpdateText()
        {
            if (_icon == null) return;
            var d = _icon.DistanceToTarget;
            var word = d <= near ? "CLOSE" : d <= mid ? "NEAR" : "FAR";
            if (word == _last) return;                       // skip redundant assignments
            _last = word;
            text.text = word;                               // 'text' is the public TMP_Text field
        }
    }
}
```

You can also override `UpdateTextColorAndOpacity()`, `UpdateTextSize()`, or
`UpdateTextOffset()` (all `virtual`) — e.g. tint the label red inside `near`.
Put the subclass on the icon prefab in place of the stock `DistanceText` (or use
a custom icon prefab via `TrackedTargetOverlay.customScreenIcon`).

## 2. The icon "action" pattern (Berries progress bar)

This is the user-favorite: a **hold-to-collect progress bar drawn on the icon**,
exactly like the demo Berries. The recipe is a component on the icon prefab that
reads its sibling `OverlayIcon`, plus a small component on the world object that
kicks it off. The demo splits it across `PickupsIndicator` (on the icon) and
`Pickup` (on the world object, implementing `IInteractable`).

### The indicator (lives on the icon prefab)

It gets the `OverlayIcon` via `GetComponent`, reads `TrackedObject` /
`TrackedTransform`, animates a UI `Image`'s width for the fill, and fades in/out:

```csharp
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using MagicPigGames.Northstar;

public class PickupsIndicator : MonoBehaviour
{
    public float pickupTime = 5f;
    public Image progressBar, progressBarBackground;

    private OverlayIcon _screenIcon;
    private GameObject TrackedObject => _screenIcon.TrackedObject;     // the world object this icon tracks
    private Transform TrackedTransform => TrackedObject.transform;
    private float _barWidth, _progress;
    private Coroutine _running;

    private void Awake()
    {
        _barWidth = progressBar.rectTransform.rect.width;
        _screenIcon = GetComponent<OverlayIcon>();                    // the icon on this prefab
    }

    public void StartProgress(Key key)
    {
        _progress = 0;
        // gate on a real game condition — distance, LOS, etc.
        if (!SimplePlayerActions.Instance.CloseEnoughToInteractWith(TrackedTransform)) return;
        _running = StartCoroutine(Progress(key));
    }

    private IEnumerator Progress(Key key)
    {
        while (_progress < 1)
        {
            if (Keyboard.current != null && Keyboard.current[key].wasReleasedThisFrame) yield break; // cancel
            if (!SimplePlayerActions.Instance.CloseEnoughToInteractWith(TrackedTransform)) yield break;
            _progress += Time.deltaTime / pickupTime;
            // fill the bar by sliding its right edge:
            progressBar.rectTransform.offsetMax =
                new Vector2(-_barWidth * (1 - _progress), progressBar.rectTransform.offsetMax.y);
            yield return null;
        }
        Destroy(TrackedObject);     // complete — do your reward here, then remove the object
    }
}
```

(The shipped version adds fade-in/out coroutines and a `pickupTimeModifier` hook
for "faster pickup based on player stats" — read `Demo Scene & Objects/Scripts/
PickupsIndicator.cs` for the complete version.)

### The trigger (lives on the world object)

Implements the demo's `IInteractable` and finds the indicator **inside the icon**,
after the icon exists, via the poll pattern:

```csharp
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using MagicPigGames.Northstar;
using MagicPigGames.Northstar.Demo;   // IInteractable

public class Pickup : MonoBehaviour, IInteractable
{
    public TrackedTargetOverlay trackedTargetOverlay;
    private PickupsIndicator _indicator;

    private void Awake() => StartCoroutine(FindIndicator());

    private IEnumerator FindIndicator()
    {
        while (_indicator == null)
        {
            var icon = trackedTargetOverlay.ScreenIcon;                 // null until Play creates it
            if (icon != null) { _indicator = icon.GetComponentInChildren<PickupsIndicator>(); yield break; }
            yield return null;
        }
    }

    public void Interact(Key key) => _indicator?.StartProgress(key);    // call from your interaction system
}
```

Your player controller then raycasts/spheres for `IInteractable` and calls
`Interact(key)` when the player holds the use button (the demo's
`SimplePlayerActions` does this; `CloseEnoughToInteractWith(Transform)` is its
distance+angle gate).

**Why on the icon, not the world object?** The bar is a UI element that must live
in the overlay canvas and follow the icon. Putting it on the icon prefab means it
inherits the icon's screen position, fade, and `DistanceToTarget` for free.

## 3. Floating status bar (health bar) pattern

The demo's `SimpleHealthBar` is the same idea for a value that isn't an
interaction: a component on the icon prefab that lerps an `Image`'s width to a
percent and fades itself based on `OverlayIcon` state.

```csharp
private OverlayIcon _screenIcon;
private float DistanceToTarget => _screenIcon.DistanceToTarget;
private float PercentToEdge   => _screenIcon.PercentToEdge;   // 0 on-screen … 1 at edge

private bool ShouldShow()
{
    if (!showOnEdges && PercentToEdge > 0) return false;       // hide while it's an edge arrow
    if (showOnValueChange && _valueChangeTimer > 0f) { _valueChangeTimer -= Time.deltaTime; return true; }
    if (showBasedOnDistance && (DistanceToTarget < minDistance || DistanceToTarget > maxDistance)) return false;
    return true;
}

public void TakeDamage(int dmg = 1)
{
    health -= dmg;
    _valueChangeTimer = valueChangeTime;                       // pop the bar for a few seconds
    healthBar.rectTransform.sizeDelta =
        new Vector2(Mathf.Lerp(0, background.rectTransform.sizeDelta.x, (float)health / healthMax),
                    healthBar.rectTransform.sizeDelta.y);
}
```

The world object's damage code finds the bar through
`trackedTargetOverlay.ScreenIcon.GetComponentInChildren<SimpleHealthBar>()` (poll
for the icon first), and calls `SetCharacter(go)` / `SetOverlayIcon(icon)` once so
the bar knows what it represents. See `ClickToDamage.cs` for that wiring.

## 4. Custom icon — subclass `OverlayIcon` / `CompassIcon`

When you need to change how the icon itself is drawn or positioned (not just add
a widget to it), subclass the icon. Both are `virtual`-heavy:

- `OverlayIcon`: override `SetValues()` / `SetNavigationBarValues()` (per-frame
  appearance), `ShouldShow()`, the `virtual` sprite/color/size properties, or
  `LateUpdate()`. Keep `base` calls unless you fully replace the behavior.
- `CompassIcon`: override `SetColor()`, `SetScale()`, `GetAngle()`,
  `PointAtTarget()`, etc.

Put the subclass on a custom icon prefab and reference it via
`TrackedTargetOverlay.customScreenIcon` / `TrackedTargetCompassRadar.trackedObjectPrefab`.

## 5. Custom Compass / Radar / Navigation Bar

The source comments invite subclassing these for unique behavior:

- `Compass : NorthstarSystem` / `Radar : NorthstarSystem` — override `Awake`,
  `LateUpdate`, `PaintTrackedObjects` (Radar), or the `SetOverride*` methods. Add
  your subclass to a duplicated compass/radar prefab (kept in `Resources`).
- `NavigationBar` — override `GetPositionOnBar`, `PlaceObject`, `FadeAtEdges`,
  `ComputeEdgeFade`, `AddOverlay` to change layout/fade math. Comment in source:
  *"You can override this class to create your own custom Compass Bar."*

## Conventions for custom code

- **It's runtime code — no editor APIs.** `AssetDatabase`, `PrefabUtility`,
  `CreateInstance` for assets belong in a `CommandScript` / `#if UNITY_EDITOR`,
  never in a shipped icon component or subclass.
- **Respect the play-start timing.** Anything reading `ScreenIcon` /
  `NavigationBarIcon` / `compassIcon` must poll or use `OnIconCreated`.
- **Call `base` in overrides** unless you intend to fully replace the behavior —
  the base methods cache references and drive the per-frame update.
- **Drive UI off `OverlayIcon` state**, not your own distance math, so your widget
  stays in sync with the icon's fade/visibility (`DistanceToTarget`,
  `PercentToEdge`, `Image.color.a`).
- **Clean up:** stop coroutines on disable; the icon (and its child widgets) are
  destroyed when the target is removed, so don't hold references across removal.
- The demo scripts are the canonical reference — read
  `Demo Scene & Objects/Scripts/` (`PickupsIndicator`, `Pickup`, `IInteractable`,
  `SimpleHealthBar`, `ClickToDamage`, `SimplePlayerActions`) before writing a new
  variant; reuse their structure.
