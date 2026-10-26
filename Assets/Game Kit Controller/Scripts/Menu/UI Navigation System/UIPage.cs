using System;
using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using static UINavigationZoneManager.NavigationZone;

public class UIPage : MonoBehaviour
{
    [Header ("Main Settings")]
    [Space]

    public bool refreshItemListInEachPageOpeningEnabled;

    [Space]

    public UIPageType pageType = UIPageType.Normal;

    [Space]
    [Header ("Default UI to Select")]
    [Space]

    public UINavigationItem defaultSelection;

    [Space]

    public List<UINavigationItem> defaultSelectionList = new List<UINavigationItem> ();

    [Space]

    public ScrollRect defaultScrollRect;

    public ScrollRectSelectionMode defaultScrollRectSelectionMode = ScrollRectSelectionMode.First;

    [Space]
    [Header ("Components")]
    [Space]

    public UINavigationContext context;

    public enum UIPageType
    {
        Normal,
        Popup
    }

    public enum ScrollRectSelectionMode
    {
        First,
        Last
    }

    public event Func<bool> OnCancelEvent;

    public bool IsVisible { get; private set; }

    public void SetVisible (bool state)
    {
        IsVisible = state;
    }

    public void OnShow (UIRouter router)
    {
        if (context != null) {
            context.enabled = true;

            if (refreshItemListInEachPageOpeningEnabled) {
                context.addAnyNewItemFromPanel ();
            } else {
                context.EnsureItems ();
            }

            router.PushContext (context);
        }

        SetupInitialSelection (router);
    }

    public void OnHide (UIRouter router)
    {
        if (context != null) {
            router.PopContext (context);

            context.enabled = false;
        }
    }

    private void SetupInitialSelection (UIRouter router)
    {
        var nav = router.Nav;

        // ==================================================
        // if there is a valid interactable object, ignore
        // ==================================================

        nav.validateCurrentSelectionOnCurrentContext ();

        if (nav.CurrentSelection != null) {
            return;
        }

        nav.RestoreCurrentContextSelection ();

        if (nav.CurrentSelection != null)
            return;

        //if (context != null) {
        //    UINavigationItem lastSelectedItem = context.getLastSelectedItem ();

        //    if (lastSelectedItem != null && lastSelectedItem.IsInteractable ()) {

        //        nav.SetSelection (lastSelectedItem);

        //        return;
        //    }
        //}

        // ==================================================
        // use the default selection
        // ==================================================

        if (defaultSelectionList.Count > 0) {
            for (int i = 0; i < defaultSelectionList.Count; i++) {
                if (defaultSelectionList [i] != null &&
                    defaultSelectionList [i].IsInteractable ()) {
                    nav.SetSelection (defaultSelectionList [i]);

                    return;
                }
            }
        }

        if (defaultSelection != null &&
            defaultSelection.IsInteractable ()) {
            nav.SetSelection (defaultSelection);

            return;
        }

        // ==================================================
        // use the first/last item from the default ScrollRect
        // ==================================================

        if (defaultScrollRect != null) {

            UINavigationItem item = null;

            switch (defaultScrollRectSelectionMode) {

                case ScrollRectSelectionMode.First:
                    item = context.getFirstItemFromScrollRect (defaultScrollRect);
                    break;

                case ScrollRectSelectionMode.Last:
                    item = context.getLastItemFromScrollRect (defaultScrollRect);
                    break;
            }

            if (item != null && item.IsInteractable ()) {
                nav.SetSelection (item);
                return;
            }
        }

        // ==================================================
        // else, check for the next one possible
        // ==================================================

        var ctx = context;
        if (ctx == null || ctx.Items == null)
            return;

        foreach (var item in ctx.Items) {
            if (item != null && item.IsInteractable ()) {
                nav.SetSelection (item);
                return;
            }
        }
    }

    public void checkRefreshItemListOnCurrentContext ()
    {
        if (!refreshItemListInEachPageOpeningEnabled || context == null) {
            return;
        }

        context.addAnyNewItemFromPanel ();
    }

    public void registerNewItem (GameObject newObject)
    {
        if (context == null) {
            return;
        }

        context.registerNewItem (newObject);
    }

    //EVENTS

    public virtual bool ProcessCancelEvent ()
    {
        if (OnCancelEvent == null)
            return false;

        foreach (Func<bool> callback in OnCancelEvent.GetInvocationList ()) {

            if (callback ())
                return true;
        }

        return false;
    }

    public void ClearCancelEvent ()
    {
        OnCancelEvent = null;
    }
}