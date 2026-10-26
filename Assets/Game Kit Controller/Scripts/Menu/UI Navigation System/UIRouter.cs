using System;
using System.Collections.Generic;
using UnityEngine;
using static UIPage;

public class UIRouter : MonoBehaviour
{
    [Header ("Main Settings")]
    [Space]

    public int playerId;

    public bool UIRouterEnabled = true;

    public bool setActiveStatesOnPanelsEnabled;

    [Header ("Debug")]
    [Space]

    public bool showDebugPrint;

    public bool updatePageStackDebugEnabled;

    [SerializeField]
    private List<UIPage> pageStackDebug = new List<UIPage> ();

    [SerializeField]
    private int pageStackCount;

    [Space]
    [Header ("Components")]
    [Space]

    public UINavigationManager navigationManager;

    public Transform mainCanvasTransform;

    [Space]

    public List<UIPage> UIPageList = new List<UIPage> ();

    public event Action<UIMenuState> OnMenuChanged;

    private Stack<UIPage> pageStack = new Stack<UIPage> ();

    public UINavigationManager Nav => navigationManager;

    public UIPage CurrentPage =>
        pageStack.Count > 0 ? pageStack.Peek () : null;

    public int PlayerId => playerId;

    public bool isUIRouterEnabled ()
    {
        return UIRouterEnabled;
    }

    private void Notify ()
    {
        RefreshDebugStack ();

        if (CurrentPage == null)
            return;

        OnMenuChanged?.Invoke (new UIMenuState
        {
            playerId = playerId,
            menuName = CurrentPage.name,
            page = CurrentPage
        });
    }


    //STACK FUNCTIONS START
    public bool Push (UIPage page)
    {
        if (page == null) {
            if (showDebugPrint) {
                Debug.LogWarning ("[UIRouter] Tried to Push a NULL page.");
            }

            return false;
        }

        // avoid duplicates on stack
        if (pageStack.Contains (page)) {
            if (showDebugPrint) {
                Debug.LogWarning (
                $"[UIRouter] Tried to Push duplicated page: {page.name}");
            }

            return false;
        }

        // avoid to open the current page already opened
        if (CurrentPage == page) {
            if (showDebugPrint) {
                Debug.LogWarning (
                $"[UIRouter] Page already active: {page.name}");
            }

            return false;
        }

        if (CurrentPage != null &&
            page.pageType == UIPageType.Normal) {

            CurrentPage.OnHide (this);

            setActivePanelState (false, CurrentPage.gameObject);

            CurrentPage.SetVisible (false);
        }

        //------------------------------------------------------
        // Push new page
        //------------------------------------------------------

        pageStack.Push (page);

        ShowPage (page);

        Notify ();

        return true;
    }

    public bool Pop (UIPage page)
    {
        if (page == null) {
            if (showDebugPrint) {
                Debug.LogWarning ("[UIRouter] Tried to Pop a NULL page.");
            }

            return false;
        }

        if (!pageStack.Contains (page)) {
            if (showDebugPrint) {
                Debug.LogWarning (
                $"[UIRouter] Tried to Pop a page not present in the stack: {page.name}");
            }

            return false;
        }

        Stack<UIPage> temp = new Stack<UIPage> ();

        //take elements from the list to find the one that is searched
        while (pageStack.Count > 0) {

            UIPage current = pageStack.Pop ();

            if (current == page) {

                RemovePage (current);
                break;
            }

            temp.Push (current);
        }

        // restore the rest of the stack
        while (temp.Count > 0) {
            pageStack.Push (temp.Pop ());
        }

        Notify ();

        return true;
    }

    public bool PopTopPage ()
    {
        if (pageStack.Count == 0) {
            if (showDebugPrint) {
                Debug.LogWarning ("[UIRouter] Pop on empty stack.");
            }

            return false;
        }

        var top = pageStack.Pop ();

        RemovePage (top);

        while (pageStack.Count > 0 && pageStack.Peek () == null) {
            pageStack.Pop ();
        }

        //==================================================
        // If a normal page is removed, the previous normal
        // page was hidden when it was pushed, so show it again.
        //
        // Popups never hide the page below them, so no need
        // to restore visibility when closing a popup.
        //==================================================

        if (CurrentPage != null) {

            if (top.pageType == UIPageType.Normal) {

                setActivePanelState (true, CurrentPage.gameObject);

                CurrentPage.SetVisible (true);

                //CurrentPage.OnShow (this);
            }
        }

        Notify ();

        return true;
    }

    public bool PopIfCurrent (UIPage page)
    {
        if (page == null)
            return false;

        if (CurrentPage != page)
            return false;

        return PopTopPage ();
    }

    public bool Replace (UIPage page)
    {
        if (page == null) {
            if (showDebugPrint) {
                Debug.LogWarning ("[UIRouter] Tried to Replace with NULL page.");
            }

            return false;
        }

        while (pageStack.Count > 0) {
            RemovePage (pageStack.Pop ());
        }

        return Push (page);
    }

    public bool RefreshCurrentPage ()
    {
        if (CurrentPage == null)
            return false;

        ShowPage (CurrentPage);

        Notify ();

        return true;
    }

    void ShowPage (UIPage page)
    {
        if (page == null)
            return;

        setActivePanelState (true, page.gameObject);

        page.SetVisible (true);

        page.OnShow (this);
    }

    void RemovePage (UIPage page)
    {
        if (page == null)
            return;

        page.OnHide (this);

        if (page.pageType == UIPageType.Normal &&
            page.context != null) {

            page.context.clearLastSelectedItem ();
        }

        setActivePanelState (false, page.gameObject);

        page.SetVisible (false);
    }

    //STACK FUNCTIONS END

    void setActivePanelState (bool state, GameObject panelObject)
    {
        if (setActiveStatesOnPanelsEnabled) {
            if (panelObject.activeSelf != state) {
                panelObject.SetActive (state);
            }
        }
    }

    public void PushContext (UINavigationContext ctx)
    {
        navigationManager.PushContext (ctx);
    }

    public void PopContext (UINavigationContext ctx)
    {
        navigationManager.PopContext (ctx);
    }

    public void setUIRouterEnabledState (bool state)
    {
        UIRouterEnabled = state;
    }

    public void checkRefreshItemListOnCurrentContext ()
    {
        if (UIRouterEnabled) {
            navigationManager.checkRefreshItemListOnCurrentContext ();
        }
    }

    public void setObjectAsCurrentSelectionExternally (GameObject objectToSelect)
    {
        if (UIRouterEnabled) {
            if (objectToSelect == null) {
                return;
            }

            UINavigationItem objectToSelectUINavigationItem = objectToSelect.GetComponent<UINavigationItem> ();

            if (objectToSelectUINavigationItem != null) {
                navigationManager.SetSelection (objectToSelectUINavigationItem);
            }
        }
    }

    public GameObject getCurrentSelection ()
    {
        if (UIRouterEnabled) {
            return navigationManager.getCurrentSelection ();
        }

        return null;
    }

    public void setUINavigationMoveInputPausedState (bool state)
    {
        if (UIRouterEnabled) {
            navigationManager.setUINavigationMoveInputPausedState (state);
        }
    }

    public void registerNewItemOnUIPage (GameObject newObject, UIPage UIPageToUse)
    {
        if (UIRouterEnabled) {
            if (UIPageToUse != null) {
                UIPageToUse.registerNewItem (newObject);
            }
        }
    }

    public bool HasCurrentPage ()
    {
        return CurrentPage != null;
    }

    public bool HasPopupOpen ()
    {
        return CurrentPage != null &&
               CurrentPage.pageType == UIPageType.Popup;
    }

    public bool HasPages ()
    {
        return pageStack.Count > 0;
    }

    public void closeCurrentPageIfItIsPopup ()
    {
        if (!UIRouterEnabled)
            return;

        //--------------------------------------------------
        // Close current popup if there is one
        //--------------------------------------------------

        if (CurrentPage != null &&
            CurrentPage.pageType == UIPageType.Popup) {

            PopTopPage ();
        }
    }

    public bool processCancelEventOnCurrentPage ()
    {
        if (!UIRouterEnabled)
            return false;

        //--------------------------------------------------
        // call the event on cancel on the current page
        //--------------------------------------------------

        if (CurrentPage != null) {
            return CurrentPage.ProcessCancelEvent ();
        }

        return false;
    }

    public bool IsCurrentPage (UIPage page)
    {
        return CurrentPage == page;
    }

    private void RefreshDebugStack ()
    {
        if (!updatePageStackDebugEnabled) {
            return;
        }

        pageStackDebug.Clear ();

        pageStackDebug.AddRange (pageStack);

        // Top del stack en el índice 0
        pageStackDebug.Reverse ();

        pageStackCount = pageStack.Count;
    }

    public void selectNavigationZonePanelByName (string zoneName)
    {
        if (UIRouterEnabled) {
            navigationManager.selectNavigationZonePanelByName (zoneName);
        }
    }

    //------------------------------------------------------
    // INPUT
    //------------------------------------------------------

    public void inputSubmit ()
    {
        if (!UIRouterEnabled || navigationManager == null)
            return;

        //navigationManager.InputSubmit ();
    }

    public void inputCloseCurrentPopUp ()
    {
        closeCurrentPageIfItIsPopup ();
    }

    public void inputPreviousTab ()
    {
        if (!UIRouterEnabled || navigationManager == null)
            return;

        //navigationManager.InputPreviousTab ();
    }

    public void inputNextTab ()
    {
        if (!UIRouterEnabled || navigationManager == null)
            return;

        //navigationManager.InputNextTab ();
    }


    //EDITOR FUNCTIONS
    public void setUIRouterEnabledStateFromEditor (bool state)
    {
        setUIRouterEnabledState (state);

        updateComponent ();
    }

    public void storeAllUIPages ()
    {
        UIPageList.Clear ();

        var found = new List<UIPage> (mainCanvasTransform.GetComponentsInChildren<UIPage> (true));

        foreach (var item in found) {
            if (item == null)
                continue;

            UIPageList.Add (item);
        }

        updateComponent ();
    }

    public void bakeAllUIPages ()
    {
        for (int i = 0; i < UIPageList.Count; i++) {
            if (UIPageList [i] != null) {
                if (UIPageList [i].context != null) {
                    UIPageList [i].context.bakeNavigationItemsFromEditor ();
                }
            }
        }
    }

    void updateComponent ()
    {
        GKC_Utils.updateComponent (this);

        GKC_Utils.updateDirtyScene ("Update UI Router", gameObject);
    }

    public struct UIMenuState
    {
        public int playerId;
        public string menuName;
        public UIPage page;
    }
}