using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using static UINavigationZoneManager.NavigationZone;

public class UINavigationContext : MonoBehaviour
{
    [Header ("Main Settings")]
    [Space]

    [SerializeField] private bool autoCaptureIfEmpty = true;

    [Space]

    public bool ignoreLastSelectedItem;

    [Space]
    [Header ("Items Settings")]
    [Space]

    [SerializeField] private List<UINavigationItem> items = new List<UINavigationItem> ();

    [Space]

    public bool getItemsFromExtraPanels;
    public List<Transform> extraPanelsForItems = new List<Transform> ();

    [Space]

    public bool ignoreItemsFromExtraPanels;
    public List<Transform> extraPanelsToIgnoreForItems = new List<Transform> ();

    [Space]
    [Header ("Navigation Space Settings")]
    [Space]

    public RectTransform navigationSpace;

    [Space]
    [Header ("UI Navigation Zone Manager Settings")]
    [Space]

    public bool allowQuickSelectionBetweenNavigationZonesEnabled;

    public UINavigationZoneManager zoneManager;

    [Space]
    [Header ("Debug")]
    [Space]

    public UINavigationItem lastSelectedItem;

    public bool HasZoneManager =>
        zoneManager != null &&
        zoneManager.IsEnabled;

    public UINavigationItem CurrentEditingItem => currentEditingItem;

    private UINavigationItem currentEditingItem;

    private static readonly Vector3 [] corners = new Vector3 [4];

    // ======================================================
    // PUBLIC ACCESS
    // ======================================================

    public IReadOnlyList<UINavigationItem> Items => items;

    public void clearLastSelectedItem ()
    {
        lastSelectedItem = null;
    }

    public UINavigationItem getLastSelectedItem ()
    {
        return lastSelectedItem;
    }

    public void setLastSelectedItem (UINavigationItem newItem)
    {
        if (ignoreLastSelectedItem) {
            return;
        }

        if (newItem == null) {
            return;
        }

        lastSelectedItem = newItem;
    }

    // ======================================================
    // REGISTER / UNREGISTER
    // ======================================================

    public void Register (UINavigationItem item)
    {
        if (item == null)
            return;

        if (!items.Contains (item))
            items.Add (item);
    }

    public void Unregister (UINavigationItem item)
    {
        items.Remove (item);
    }

    // ======================================================
    // AUTO CAPTURE
    // ======================================================

    public void EnsureItems ()
    {
        if (items != null && items.Count > 0)
            return;

        CaptureItemsFromPanel ();
    }

    public void CaptureItemsFromPanel ()
    {
        items.Clear ();

        var navigationItemsFound = GetNavigationItemsFromPanels ();

        foreach (var item in navigationItemsFound) {
            if (item == null)
                continue;

            item.Bake ();

            Register (item);
        }

        checkForNullElements ();
    }

    public void addAnyNewItemFromPanel ()
    {
        var navigationItemsFound = GetNavigationItemsFromPanels ();

        foreach (var item in navigationItemsFound) {
            if (item == null)
                continue;

            if (!items.Contains (item)) {
                item.Bake ();

                Register (item);
            }
        }

        checkForNullElements ();
    }

    public void registerNewItem (GameObject newObject)
    {
        if (newObject == null) {
            return;
        }

        UINavigationItem newItem = newObject.GetComponent<UINavigationItem> ();

        if (newItem == null) {
            return;
        }

        Transform newItemTransform = newItem.transform;

        if (ignoreItemsFromExtraPanels) {

            foreach (var panel in extraPanelsToIgnoreForItems) {
                if (panel != null && newItemTransform.IsChildOf (panel))
                    return;
            }
        }

        if (!items.Contains (newItem)) {
            newItem.Bake ();

            Register (newItem);
        }
    }

    private List<UINavigationItem> GetNavigationItemsFromPanels ()
    {
        var found = new List<UINavigationItem> (GetComponentsInChildren<UINavigationItem> (true));

        if (ignoreItemsFromExtraPanels) {
            found.RemoveAll (item =>
            {
                if (item == null)
                    return true;

                foreach (var panel in extraPanelsToIgnoreForItems) {
                    if (panel != null && item.transform.IsChildOf (panel))
                        return true;
                }

                return false;
            });
        }

        if (getItemsFromExtraPanels) {
            foreach (var panel in extraPanelsForItems) {
                if (panel != null) {
                    found.AddRange (panel.GetComponentsInChildren<UINavigationItem> (true));
                }
            }
        }

        return found;
    }

    public void checkForNullElements ()
    {
        for (int i = items.Count - 1; i >= 0; i--) {
            if (items [i] == null) {
                items.RemoveAt (i);
            }
        }
    }

    // ======================================================
    // SAFE HOOK FOR RUNTIME
    // ======================================================

    public void Refresh ()
    {
        if (autoCaptureIfEmpty && (items == null || items.Count == 0)) {
            CaptureItemsFromPanel ();
        }

        if (HasZoneManager) {
            zoneManager.ClearCurrentZone ();
        }

        ClearEditing ();
    }

    // ======================================================
    // EDITING CONTROL
    // ======================================================

    public void SetEditingItem (UINavigationItem item)
    {
        if (currentEditingItem == item)
            return;

        if (currentEditingItem != null)
            currentEditingItem.EndEditInternal ();

        currentEditingItem = item;

        if (currentEditingItem != null)
            currentEditingItem.BeginEditInternal ();
    }

    public bool HasEditingItem ()
    {
        return currentEditingItem != null;
    }

    public void ClearEditing ()
    {
        if (currentEditingItem == null)
            return;

        currentEditingItem.EndEditInternal ();
        currentEditingItem = null;
    }


    //SCROLL RECT AND NAVIGATION ZONE MANAGER FUNCTIONS

    public void HandleFocus (UINavigationItem item)
    {
        if (item == null)
            return;

        if (HasZoneManager) {

            var zone = zoneManager.GetCurrentZone ();

            if (zone != null &&
                zone.HasScrollRect &&
                item.ScrollRect == zone.scrollRect) {

                RectTransform rectToFocus = item.getFocusTarget ();

                if (rectToFocus == null) {
                    rectToFocus = (RectTransform)item.transform;
                }

                FocusItem (zone.scrollRect, rectToFocus);

                return;
            }
        }

        //print (item.gameObject.name + " " + item.HasScrollRect);

        if (item.HasScrollRect) {
            RectTransform rectToFocus = item.getFocusTarget ();

            if (rectToFocus == null) {
                rectToFocus = (RectTransform)item.transform;
            }

            //print (rectToFocus.name);

            FocusItem (item.ScrollRect, rectToFocus);
        }
    }

    private static Bounds GetBoundsInViewport (RectTransform viewport, RectTransform target)
    {
        target.GetWorldCorners (corners);

        Matrix4x4 m = viewport.worldToLocalMatrix;

        Vector3 min = new Vector3 (float.MaxValue, float.MaxValue, float.MaxValue);
        Vector3 max = new Vector3 (float.MinValue, float.MinValue, float.MinValue);

        for (int i = 0; i < 4; i++) {
            Vector3 p = m.MultiplyPoint3x4 (corners [i]);

            min = Vector3.Min (min, p);
            max = Vector3.Max (max, p);
        }

        Bounds b = new Bounds (min, Vector3.zero);
        b.Encapsulate (max);

        return b;
    }

    //old funcion, it gives trouble on horizontal scrollrect
    //private void FocusItem (ScrollRect sr, RectTransform rectToFocus)
    //{
    //    if (sr == null || rectToFocus == null)
    //        return;

    //    RectTransform viewport = sr.viewport != null
    //        ? sr.viewport
    //        : (RectTransform)sr.transform;

    //    RectTransform content = sr.content;
    //    RectTransform itemRect = (RectTransform)rectToFocus.transform;

    //    Canvas.ForceUpdateCanvases ();
    //    LayoutRebuilder.ForceRebuildLayoutImmediate (content);

    //    Bounds itemBounds = GetBoundsInViewport (viewport, itemRect);

    //    float moveX = 0f;
    //    float moveY = 0f;

    //    float offsetX = content.localPosition.x - content.anchoredPosition.x;
    //    float offsetY = content.localPosition.y - content.anchoredPosition.y;

    //    float minLocalX = offsetX;
    //    float maxLocalX = offsetX + Mathf.Max (0f, content.rect.width - viewport.rect.width);

    //    float minLocalY = offsetY;
    //    float maxLocalY = offsetY + Mathf.Max (0f, content.rect.height - viewport.rect.height);

    //    Vector3 localPos = content.localPosition;

    //    //------------------------------------------------------
    //    // Vertical
    //    //------------------------------------------------------

    //    if (sr.vertical) {
    //        if (itemBounds.max.y > viewport.rect.yMax)
    //            moveY = itemBounds.max.y - viewport.rect.yMax;
    //        else if (itemBounds.min.y < viewport.rect.yMin)
    //            moveY = itemBounds.min.y - viewport.rect.yMin;

    //        localPos.y -= moveY;
    //    }

    //    //------------------------------------------------------
    //    // Horizontal
    //    //------------------------------------------------------

    //    if (sr.horizontal) {
    //        if (itemBounds.min.x < viewport.rect.xMin)
    //            moveX = itemBounds.min.x - viewport.rect.xMin;
    //        else if (itemBounds.max.x > viewport.rect.xMax)
    //            moveX = itemBounds.max.x - viewport.rect.xMax;

    //        localPos.x += moveX;
    //    }

    //    //------------------------------------------------------
    //    // Clamp
    //    //------------------------------------------------------

    //    localPos.x = Mathf.Clamp (localPos.x, minLocalX, maxLocalX);
    //    localPos.y = Mathf.Clamp (localPos.y, minLocalY, maxLocalY);



    //    if ((content.localPosition - localPos).sqrMagnitude > 0.01f) {
    //        content.localPosition = localPos;
    //    }
    //}

    private void FocusItem (ScrollRect sr, RectTransform rectToFocus)
    {
        if (sr == null || rectToFocus == null)
            return;

        RectTransform viewport = sr.viewport != null
            ? sr.viewport
            : (RectTransform)sr.transform;

        RectTransform content = sr.content;

        Canvas.ForceUpdateCanvases ();
        LayoutRebuilder.ForceRebuildLayoutImmediate (content);

        Bounds itemBounds = GetBoundsInViewport (viewport, rectToFocus);

        Vector2 delta = Vector2.zero;

        //------------------------------------------------------
        // Horizontal
        //------------------------------------------------------

        if (sr.horizontal) {
            if (itemBounds.min.x < viewport.rect.xMin)
                delta.x = itemBounds.min.x - viewport.rect.xMin;
            else if (itemBounds.max.x > viewport.rect.xMax)
                delta.x = itemBounds.max.x - viewport.rect.xMax;
        }

        //------------------------------------------------------
        // Vertical
        //------------------------------------------------------

        if (sr.vertical) {
            if (itemBounds.max.y > viewport.rect.yMax)
                delta.y = itemBounds.max.y - viewport.rect.yMax;
            else if (itemBounds.min.y < viewport.rect.yMin)
                delta.y = itemBounds.min.y - viewport.rect.yMin;
        }

        ApplyScroll (sr, delta);
    }


    private void ApplyScroll (ScrollRect sr, Vector2 delta)
    {
        Vector2 pos = sr.content.anchoredPosition;

        if (sr.horizontal && !sr.vertical) {
            pos.x -= delta.x;
        } else if (sr.vertical && !sr.horizontal) {
            pos.y -= delta.y;
        } else {
            // grid / dual axis
            pos -= delta;
        }

        sr.content.anchoredPosition = pos;
    }

    private int GetZoneIndex (UINavigationItem item)
    {
        if (!HasZoneManager || item == null)
            return -1;

        for (int i = 0; i < zoneManager.ZoneCount; i++) {

            var zone = zoneManager.GetZone (i);

            if (zone == null)
                continue;

            //==================================================
            // Explicit default item
            //==================================================

            if (ReferenceEquals (zone.defaultItem, item))
                return i;

            //==================================================
            // Explicit item list
            //==================================================

            foreach (var zoneItem in zone.items) {

                if (ReferenceEquals (zoneItem, item))
                    return i;
            }

            //==================================================
            // Fast path:
            // if the item belongs to the zone ScrollRect,
            // this is its zone.
            //==================================================

            if (zone.HasScrollRect) {
                //&&
                //item.ScrollRect == zone.scrollRect) {

                //return i;
                UINavigationItem zoneItem = null;

                switch (zone.scrollRectSelectionMode) {
                    case ScrollRectSelectionMode.First:
                        zoneItem = getFirstItemFromScrollRect (zone.scrollRect);
                        break;

                    case ScrollRectSelectionMode.Last:
                        zoneItem = getLastItemFromScrollRect (zone.scrollRect);
                        break;
                }

                if (ReferenceEquals (zoneItem, item))
                    return i;

            }

            if (zone.HasPanel) {
                UINavigationItem zoneItem = null;

                switch (zone.panelSelectionMode) {
                    case PanelSelectionMode.First:
                        zoneItem = getFirstItemFromPanel (zone.panel);
                        break;

                    case PanelSelectionMode.Last:
                        zoneItem = getLastItemFromPanel (zone.panel);
                        break;
                }

                if (ReferenceEquals (zoneItem, item))
                    return i;
            }
        }

        return -1;
    }

    public UINavigationItem getNextNavigationZoneItem (bool forward)
    {
        if (HasZoneManager) {

            for (int i = 0; i < zoneManager.ZoneCount; i++) {

                int zoneIndex = forward ?
                    zoneManager.SelectNextZone () :
                    zoneManager.SelectPreviousZone ();

                UINavigationItem item =
                    GetDefaultItemFromZone (zoneIndex);

                if (item != null) {
                    return item;
                }
            }

            return null;
        }

        return null;
    }

    public UINavigationItem getNavigationZoneItemByName (string zoneName)
    {
        if (HasZoneManager) {

            for (int i = 0; i < zoneManager.ZoneCount; i++) {

                int zoneIndex = zoneManager.getNavigationZoneIndexByName (zoneName);

                UINavigationItem item =
                    GetDefaultItemFromZone (zoneIndex);

                if (item != null) {
                    return item;
                }
            }

            return null;
        }

        return null;
    }

    public UINavigationItem getFirstItemFromScrollRect (ScrollRect sr)
    {
        if (sr == null || sr.content == null)
            return null;

        foreach (var item in items) {

            if (!item.IsInteractable ())
                continue;

            if (item.ScrollRect == sr)
                return item;
        }

        return null;
    }

    public UINavigationItem getLastItemFromScrollRect (ScrollRect sr)
    {
        if (sr == null || sr.content == null)
            return null;

        for (int i = items.Count - 1; i >= 0; i--) {

            UINavigationItem item = items [i];

            if (!item.IsInteractable ())
                continue;

            if (item.ScrollRect == sr)
                return item;
        }

        return null;
    }

    public UINavigationItem getFirstItemFromPanel (Transform panel)
    {
        if (panel == null)
            return null;

        foreach (var item in items) {
            if (!item.IsInteractable ())
                continue;

            if (item.transform.IsChildOf (panel))
                return item;
        }

        return null;
    }

    public UINavigationItem getLastItemFromPanel (Transform panel)
    {
        if (panel == null)
            return null;

        for (int i = items.Count - 1; i >= 0; i--) {
            var item = items [i];

            if (!item.IsInteractable ())
                continue;

            if (item.transform.IsChildOf (panel))
                return item;
        }

        return null;
    }

    private UINavigationItem GetFirstItemFromZone (int zoneIndex)
    {
        var zone = zoneManager.GetZone (zoneIndex);

        if (zone == null)
            return null;

        foreach (var item in zone.items) {

            if (!item.IsInteractable ())
                continue;

            return item;
        }

        if (zone.HasScrollRect) {
            switch (zone.scrollRectSelectionMode) {

                case ScrollRectSelectionMode.First:
                    return getFirstItemFromScrollRect (zone.scrollRect);

                case ScrollRectSelectionMode.Last:
                    return getLastItemFromScrollRect (zone.scrollRect);
            }
        }

        if (zone.HasPanel) {
            switch (zone.panelSelectionMode) {

                case PanelSelectionMode.First:
                    return getFirstItemFromPanel (zone.panel);

                case PanelSelectionMode.Last:
                    return getLastItemFromPanel (zone.panel);
            }
        }

        return null;
    }

    private UINavigationItem GetDefaultItemFromZone (int zoneIndex)
    {
        var zone = zoneManager.GetZone (zoneIndex);

        if (zone == null)
            return null;

        if (zone.restoreLastSelection) {

            UINavigationItem last =
                zoneManager.GetLastSelected (zoneIndex);

            if (last != null &&
                GetZoneIndex (last) == zoneIndex &&
                last.IsInteractable ()) {
                return last;
            }
        }

        if (zone.defaultItem != null && zone.defaultItem.IsInteractable ())
            return zone.defaultItem;

        return GetFirstItemFromZone (zoneIndex);
    }

    public void updateNavigationZoneState (UINavigationItem item)
    {
        if (!HasZoneManager) {
            return;
        }

        if (item == null) {
            zoneManager.ClearCurrentZone ();

            return;
        }

        int zoneIndex = GetZoneIndex (item);

        if (zoneIndex != -1) {

            zoneManager.SetCurrentZone (zoneIndex);
            zoneManager.SetLastSelected (zoneIndex, item);
        } else {
            zoneManager.ClearCurrentZone ();
        }
    }

    public bool isAllowQuickSelectionBetweenNavigationZonesEnabled ()
    {
        if (!allowQuickSelectionBetweenNavigationZonesEnabled) {
            return false;
        }

        if (HasZoneManager) {
            return zoneManager.ZoneCount > 1;
        }

        return false;
    }

    public RectTransform getNavigationSpace ()
    {
        if (navigationSpace == null) {
            navigationSpace = GetComponent<RectTransform> ();
        }

        return navigationSpace;
    }

    // ======================================================
    // EDITOR / DEBUG
    // ======================================================

    public void bakeNavigationItemsFromEditor ()
    {
        CaptureItemsFromPanel ();

        updateComponent ();

        print ("Bake Navigation Items From Editor done");
    }

    public void clearNavigationItemsFromEditor ()
    {
        items.Clear ();

        updateComponent ();
    }

    void updateComponent ()
    {
        GKC_Utils.updateComponent (this);

        GKC_Utils.updateDirtyScene ("Update UI Navigation Context", gameObject);
    }

}