# Setting up Northstar in a scene

Goal: get the **displays** into the scene and wired so tracked targets have
somewhere to appear. A tracked-target component shows nothing until its display
exists. Order doesn't matter functionally (everything registers via coroutines),
but build the displays first so you can immediately verify.

There are two routes. Prefer **A** (the supported window) when the user is at the
keyboard; use **B** (unity-mcp) to do it instantly yourself.

## A. The Northstar window (supported, safe)

Unity menu **`GameObject → Northstar → Create Window`** opens a dockable window
(`NorthstarMenu`). From it you click to add:

- **Add Northstar Screen Overlay** — adds the `NorthstarOverlay` master object
  (the screen / edge / nav-bar canvas).
- A **Compass** or **Radar** button (one per shipped prefab) — adds that display
  and **carries the `player` reference over** if you swap to another.
- A **Navigation Bar** button — adds a nav bar under the overlay.

It enforces "only one Compass *or* Radar, one Overlay, one Navigation Bar" and
warns before replacing an existing one. This is the cleanest path for adding
displays. You can open the window for the user, then have them set `player` /
`targetCamera`. You can also open it from code:

```csharp
EditorApplication.ExecuteMenuItem("GameObject/Northstar/Create Window");
```

## B. unity-mcp `CommandScript` recipes (instant)

The display prefabs ship under a `Resources` folder, so `Resources.Load` finds
them by name. Names (see `displays.md` for the full catalog):

- `"Northstar Screen Overlay"` — the overlay master (screen + edge + nav-bar host)
- `"Compass 1"` … `"Compass 17"` — compass styles
- `"Radar 1"` … `"Radar 5"` — radar styles
- `"Navigation Bar 1"` … `"Navigation Bar 3"` — compass bars

### Add the screen overlay and set its camera

```csharp
using UnityEngine;
using UnityEditor;
using MagicPigGames.Northstar;

internal class CommandScript : IRunCommand
{
    public void Execute(ExecutionResult result)
    {
        // Don't add a second one.
        var overlay = Object.FindObjectOfType<NorthstarOverlay>();
        if (overlay == null)
        {
            var prefab = Resources.Load<GameObject>("Northstar Screen Overlay");
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            Undo.RegisterCreatedObjectUndo(go, "Add Northstar Overlay");
            overlay = go.GetComponent<NorthstarOverlay>();
        }

        // REQUIRED: the camera the overlay projects from. No fallback.
        if (overlay.targetCamera == null)
            overlay.targetCamera = Camera.main;

        EditorUtility.SetDirty(overlay);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        result.Log($"Overlay ready. targetCamera = {overlay.targetCamera}");
    }
}
```

### Add a compass (or radar) and set the player

```csharp
internal class CommandScript : IRunCommand
{
    public void Execute(ExecutionResult result)
    {
        if (Object.FindObjectOfType<NorthstarSystem>() != null)
        { result.Log("A Compass/Radar already exists — only one allowed."); return; }

        var prefab = Resources.Load<GameObject>("Compass 1");   // or "Radar 1"
        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        Undo.RegisterCreatedObjectUndo(go, "Add Compass");
        var system = go.GetComponent<NorthstarSystem>();

        // REQUIRED: the transform everything is measured from (usually the player).
        var player = GameObject.Find("Player");
        if (player != null) system.player = player.transform;

        EditorUtility.SetDirty(system);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        result.Log(system.player == null
            ? "Compass added — SET ITS player FIELD (not auto-assigned)."
            : $"Compass added, player = {system.player.name}");
    }
}
```

### Add a navigation bar under the overlay

```csharp
internal class CommandScript : IRunCommand
{
    public void Execute(ExecutionResult result)
    {
        var overlay = Object.FindObjectOfType<NorthstarOverlay>();
        if (overlay == null) { result.Log("Add the Northstar Screen Overlay first."); return; }
        if (NavigationBar.Instance != null || Object.FindObjectOfType<NavigationBar>() != null)
        { result.Log("A Navigation Bar already exists — only one allowed."); return; }

        var prefab = Resources.Load<GameObject>("Navigation Bar 1");
        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        go.transform.SetParent(overlay.transform, false);   // must live under the overlay
        Undo.RegisterCreatedObjectUndo(go, "Add Navigation Bar");

        var nav = go.GetComponent<NavigationBar>();
        overlay.SetNavigationBar(nav);
        overlay.enableNavigationBar = true;

        EditorUtility.SetDirty(overlay);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        result.Log("Navigation Bar added under the overlay.");
    }
}
```

> If the prefab names differ in this project (renamed/duplicated), enumerate them
> first: `AssetDatabase.FindAssets("t:Prefab Compass")` or browse
> `Assets/**/Northstar*/Prefabs/Resources/`. The window's button labels come from
> the prefab file names in `Resources`.

## Global settings

`GlobalNorthstarSettings` is usually created on import (it's a singleton; only
one). If a project is missing it, create one and set North:

```csharp
var s = ScriptableObject.CreateInstance<GlobalNorthstarSettings>();
s.NorthAngle = 0f;    // where north points on the UI dial (usually up = 0)
s.WorldNorth = 0f;    // where north is in world space (usually 0)
AssetDatabase.CreateAsset(s, "Assets/Northstar/GlobalNorthstarSettings.asset");
```

Then assign it to each Compass/Radar's `northstarSettings` field. See
`overlay-settings.md` for what these values mean.

## No-bridge fallback (no unity-mcp)

If the bridge isn't available, write a one-shot editor script with a `[MenuItem]`
and tell the user to click it. Put it in any `Editor/` folder:

```csharp
// Assets/Editor/NorthstarSetup.cs
#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using MagicPigGames.Northstar;

public static class NorthstarSetup
{
    [MenuItem("Tools/Northstar/Add Overlay + Compass")]
    public static void Build()
    {
        // …the same body as a CommandScript Execute()…
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Northstar setup done.");
    }
}
#endif
```

Tell the user: *"Run **Tools ▸ Northstar ▸ Add Overlay + Compass** once, then you
can delete the script."* (The bundled `scripts/NorthstarAgentTools.cs.txt` packages
the common operations so you don't re-write them each time.)

## Verify, then report

After any setup CommandScript:

1. **Read back** the components: `FindObjectOfType<NorthstarOverlay>()` / `<NorthstarSystem>()`
   non-null, `targetCamera` and `player` set.
2. **Check the console**: `mcp__unity-mcp__Unity_GetConsoleLogs` with
   `logTypes: "Error"` — a missing camera/player surfaces as a runtime warning on
   Play, not a compile error, so call it out explicitly.
3. **Report** what's wired and what the user must still do (almost always: confirm
   `player` and `targetCamera`, then press Play). Tracked-target icons appear only
   on Play — see `tracking-objects.md` to add objects, and `runtime-api.md` for
   the play-start timing.
