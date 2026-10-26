using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class ingameMenuPanel : MonoBehaviour
{
    [Header ("Main Settings")]
    [Space]

    public string menuPanelName;

    public bool closeMenuIfGamePaused = true;

    public bool playerCanMoveStatePausedOnMenuOpened = true;

    [Space]

    public bool useUpdateMenuStateWhileMenuOpened;

    [Space]
    [Header ("Canvas Settings")]
    [Space]

    public bool useBlurUIPanel;

    public bool setSortOrderOnOpenCloseMenu;
    public int sortOrderOnMenuOpened;
    public int sortOrderOnMenuClosed;

    [Space]

    public Canvas mainCanvas;

    public RectTransform canvasRect;

    public Camera uiCamera;

    [Space]
    [Header ("Move Panels")]
    [Space]

    public bool movePanelsEnabled;

    public RectTransform mainPanelToMoveTransform;

    public float movePanelsUINavigationSpeed = 4;

    [Space]
    [Space]

    public float screenLimitOffset = 20;

    public bool useScreenLimitOffsets;
    public float leftScreenLimitOffset;
    public float rightScreenLimitOffset;
    public float upScreenLimitOffset;
    public float downScreenLimitOffset;

    [Space]
    [Header ("Enable/disable Panel Settings")]
    [Space]

    public bool enableDisablePanelsEnabled;

    public List<menuPanelInfo> menuPanelInfoList = new List<menuPanelInfo> ();

    [Space]
    [Header ("Debug")]
    [Space]

    public bool menuPanelOpened;

    public bool movingPanelActive;

    public Vector2 newPosition;

    public Vector2 screenLimit;

    public bool UINavigationMoveInputPaused;

    [Space]
    [Header ("Components")]
    [Space]

    public GameObject menuPanelObject;

    public menuPause pauseManager;

    public playerInputManager playerInput;

    public UIPage mainUIPage;

    [Space]
    [Header ("Events Settings")]
    [Space]

    public bool activateEventsOnOpenState;

    public bool useEventsOnOpenClostMenu;
    public UnityEvent eventOnOpenMenu;
    public UnityEvent eventOnCloseMenu;


    [HideInInspector] public bool touchPlatform;

    [HideInInspector] public Touch currentTouch;

    [HideInInspector] public InputAxisWrapper.TouchPressType touchPressType;

    [HideInInspector] public bool useNewInputSystem;

    [HideInInspector] public Vector2 currentTouchPosition;


    bool pauseManagerLocated;

    Coroutine updateCoroutine;

    Transform previousParent;

    RectTransform currentPanelToMove;

    bool mainCanvasLocated;


    public virtual void closeMenuPanelIfOpened ()
    {
        if (menuPanelOpened) {
            openOrCloseMenuPanel (false);
        }
    }

    public virtual void openOrCloseMenuPanel (bool state)
    {
        menuPanelOpened = state;

        pauseManagerLocated = pauseManager != null;

        menuPanelObject.SetActive (menuPanelOpened);

        if (pauseManagerLocated) {
            pauseManager.openOrClosePlayerMenu (menuPanelOpened, menuPanelObject.transform, useBlurUIPanel);

            pauseManager.setIngameMenuOpenedState (menuPanelName, menuPanelOpened, activateEventsOnOpenState);

            pauseManager.enableOrDisablePlayerMenu (menuPanelOpened, playerCanMoveStatePausedOnMenuOpened, false);

            pauseManager.checkUpdateReticleActiveState (state);

            if (playerInput == null) {
                playerInput = pauseManager.getPlayerInput ();
            }
        }

        if (menuPanelOpened) {
            openMenuPanelState ();
        } else {
            closeMenuPanelState ();
        }

        checkEventsOnStateChange (menuPanelOpened);

        if (!menuPanelOpened) {
            if (movingPanelActive) {
                disableMovePanel ();
            }
        }

        if (!mainCanvasLocated) {
            mainCanvasLocated = mainCanvas != null;

            if (!mainCanvasLocated) {
                mainCanvas = GetComponent<Canvas> ();

                mainCanvasLocated = mainCanvas != null;
            }

            if (mainCanvasLocated) {
                canvasRect = mainCanvas.GetComponent<RectTransform> ();

                if (pauseManagerLocated) {
                    uiCamera = pauseManager.getMainCanvasCamera ();
                }
            }
        }

        if (setSortOrderOnOpenCloseMenu) {
            if (mainCanvasLocated) {
                if (menuPanelOpened) {
                    mainCanvas.sortingOrder = sortOrderOnMenuOpened;
                } else {
                    mainCanvas.sortingOrder = sortOrderOnMenuClosed;
                }
            }
        }

        if (useUpdateMenuStateWhileMenuOpened) {
            if (state) {
                updateCoroutine = StartCoroutine (updateSystemCoroutine ());
            } else {
                stopUpdateCoroutine ();
            }
        }
    }

    public virtual void closeMenuFromEscapeInput ()
    {
        if (menuPanelOpened) {
            openOrCloseMenuFromTouch ();
        }
    }

    public void openOrCloseMenuFromTouch ()
    {
        openOrCloseMenuPanel (!menuPanelOpened);

        pauseManagerLocated = pauseManager != null;

        if (pauseManagerLocated) {
            pauseManager.setIngameMenuOpenedState (menuPanelName, menuPanelOpened, true);
        }
    }

    public virtual void openOrCloseMenuFromTouchExternally ()
    {

    }

    public virtual void openMenuPanelState ()
    {

    }

    public virtual void closeMenuPanelState ()
    {

    }

    public void setPauseManager (menuPause currentMenuPause)
    {
        pauseManager = currentMenuPause;

        initializeMenuPanel ();
    }

    public virtual void initializeMenuPanel ()
    {

    }

    public virtual void checkMenuIfCurrentOpened ()
    {

    }

    public virtual void checkMenuIfNotCurrentOpened ()
    {

    }

    public virtual void checkAllMenusClosed ()
    {

    }

    public void openNextPlayerOpenMenu ()
    {
        pauseManagerLocated = pauseManager != null;

        if (pauseManagerLocated) {
            pauseManager.openNextPlayerOpenMenu ();
        }
    }

    public void openPreviousPlayerOpenMenu ()
    {
        pauseManagerLocated = pauseManager != null;

        if (pauseManagerLocated) {
            pauseManager.openPreviousPlayerOpenMenu ();
        }
    }

    public void closePlayerMenuByName (string menuName)
    {
        pauseManagerLocated = pauseManager != null;

        if (pauseManagerLocated) {
            pauseManager.closePlayerMenuByName (menuName);
        }
    }

    public void checkRefreshItemListOnCurrentContextOnUINavigation ()
    {
        if (pauseManagerLocated) {
            pauseManager.checkRefreshItemListOnCurrentContextOnUINavigation ();
        }
    }

    public void selectNavigationZonePanelByNameOnCurrentContextOnUINavigation (string zoneName)
    {
        if (pauseManagerLocated) {
            pauseManager.selectNavigationZonePanelByNameOnCurrentContextOnUINavigation (zoneName);
        }
    }

    public void checkEventsOnStateChange (bool state)
    {
        if (useEventsOnOpenClostMenu) {
            if (state) {
                eventOnOpenMenu.Invoke ();
            } else {
                eventOnCloseMenu.Invoke ();
            }
        }
    }

    public void stopUpdateCoroutine ()
    {
        if (updateCoroutine != null) {
            StopCoroutine (updateCoroutine);
        }
    }

    IEnumerator updateSystemCoroutine ()
    {
        var waitTime = new WaitForSecondsRealtime (0.0001f);

        while (true) {
            updateSystem ();

            yield return waitTime;
        }
    }

    public virtual void updateMenuState ()
    {

    }

    void updateSystem ()
    {
        updateMenuState ();

        if (movingPanelActive) {
            updatePanelPosition ();
        }
    }

    void updatePanelPosition ()
    {
        if (playerInput.isUINavigationCurrentlyActive ()) {
            newPosition += playerInput.getRealMovementGamepadAxisInput () * movePanelsUINavigationSpeed;

            movePanel ();

            return;
        }

        int touchCount = 0;

        if (touchPlatform && useNewInputSystem) {
            touchCount = InputAxisWrapper.getNewTouchCount ();
        } else {
            touchCount = InputAxisWrapper.getTouchCount ();
        }

        bool usingGamepadOnTouchPlatform = false;

        if (touchPlatform) {
            if (touchCount == 0 && playerInput.isUsingGamepad ()) {
                touchCount++;

                usingGamepadOnTouchPlatform = true;
            }
        } else {
            touchCount++;
        }


        for (int i = 0; i < touchCount; i++) {
            if (!touchPlatform) {
                if (!useNewInputSystem) {
                    currentTouch = InputAxisWrapper.convertMouseIntoFinger ();
                }
            } else {
                if (!useNewInputSystem) {
                    currentTouch = InputAxisWrapper.getTouch (i);
                }
            }

            if (touchPlatform && useNewInputSystem) {
                touchPressType = InputAxisWrapper.GetTouchPressType (i);

                currentTouchPosition = InputAxisWrapper.GetTouchPosition (i);

                if (usingGamepadOnTouchPlatform) {
                    touchPressType = playerInput.GetTouchPressType ();

                    currentTouchPosition = playerInput.getCurrentTouchPosition ();
                }
            } else {
                if (useNewInputSystem) {
                    touchPressType = playerInput.GetTouchPressType ();

                    currentTouchPosition = playerInput.getCurrentTouchPosition ();
                } else {
                    touchPressType = InputAxisWrapper.GetTouchPressType (currentTouch);

                    currentTouchPosition = currentTouch.position;
                }
            }

            newPosition = currentTouchPosition;

            movePanel ();
        }
    }

    void movePanel ()
    {
        clampPanelToCanvas ();

        mainPanelToMoveTransform.position = newPosition;
    }

    void clampPanelToCanvas ()
    {
        Vector2 localPoint;

        RectTransformUtility.ScreenPointToLocalPointInRectangle (
            canvasRect,
            newPosition,
            mainCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : uiCamera,
            out localPoint
        );

        Rect rect = canvasRect.rect;

        if (useScreenLimitOffsets) {
            localPoint.x = Mathf.Clamp (localPoint.x, (rect.xMin + screenLimitOffset + leftScreenLimitOffset),
                (rect.xMax - screenLimitOffset - rightScreenLimitOffset));

            localPoint.y = Mathf.Clamp (localPoint.y, (rect.yMin + screenLimitOffset + downScreenLimitOffset),
                (rect.yMax - screenLimitOffset - upScreenLimitOffset));
        } else {
            localPoint.x = Mathf.Clamp (localPoint.x, rect.xMin + screenLimitOffset, rect.xMax - screenLimitOffset);
            localPoint.y = Mathf.Clamp (localPoint.y, rect.yMin + screenLimitOffset, rect.yMax - screenLimitOffset);
        }

        newPosition = RectTransformUtility.WorldToScreenPoint (
            mainCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : uiCamera,
            canvasRect.TransformPoint (localPoint)
        );
    }

    public void enableMovePanel (RectTransform panelToMove)
    {
        if (movePanelsEnabled) {
            if (!movingPanelActive) {
                screenLimit = new Vector2 (Screen.currentResolution.width, Screen.currentResolution.height);

                touchPlatform = InputAxisWrapper.checkTouchPlatform ();

                useNewInputSystem = InputAxisWrapper.isUseNewInputSystem ();

                currentPanelToMove = panelToMove;

                if (playerInput.isUINavigationCurrentlyActive ()) {
                    GameObject currentSelection = pauseManager.getCurrentSelection ();

                    if (currentSelection != null) {
                        mainPanelToMoveTransform.position = currentSelection.transform.position;
                    }

                    newPosition = mainPanelToMoveTransform.position;

                    pauseManager.setUINavigationMoveInputPausedState (true);

                    UINavigationMoveInputPaused = true;
                } else {
                    updatePanelPosition ();
                }

                previousParent = panelToMove.parent;

                panelToMove.SetParent (mainPanelToMoveTransform);

                if (!useUpdateMenuStateWhileMenuOpened) {
                    stopUpdateCoroutine ();

                    updateCoroutine = StartCoroutine (updateSystemCoroutine ());
                }

                movingPanelActive = true;
            }
        }
    }

    public void disableMovePanel ()
    {
        if (movePanelsEnabled) {
            if (movingPanelActive) {
                if (!useUpdateMenuStateWhileMenuOpened) {
                    stopUpdateCoroutine ();
                }

                if (currentPanelToMove != null) {
                    currentPanelToMove.SetParent (previousParent);

                    currentPanelToMove = null;
                }

                movingPanelActive = false;

                if (UINavigationMoveInputPaused) {
                    if (playerInput.isUINavigationCurrentlyActive ()) {
                        pauseManager.setUINavigationMoveInputPausedState (false);
                    }

                    UINavigationMoveInputPaused = false;
                }
            }
        }
    }

    public void toggleMenuPanelActiveState (GameObject buttonObject)
    {
        if (enableDisablePanelsEnabled) {
            if (buttonObject != null) {
                int currentIndex = menuPanelInfoList.FindIndex (s => s.mainPanelButton.Equals (buttonObject));

                if (currentIndex > -1) {
                    menuPanelInfo currentMenuPanelInfo = menuPanelInfoList [currentIndex];

                    bool panelState = !currentMenuPanelInfo.mainPanel.activeSelf;

                    currentMenuPanelInfo.mainPanel.SetActive (panelState);

                    if (currentMenuPanelInfo.disabledPanel != null) {
                        currentMenuPanelInfo.disabledPanel.SetActive (!panelState);
                    }
                }
            }
        }
    }

    public void openOrCloseAllMenuPanels (bool state)
    {
        if (enableDisablePanelsEnabled) {
            for (int i = 0; i < menuPanelInfoList.Count; i++) {
                setMenuPanelActiveState (state, menuPanelInfoList [i].mainPanelButton);
            }
        }
    }

    public void setMenuPanelActive (GameObject panelGameObject)
    {
        setMenuPanelActiveState (true, panelGameObject);
    }

    public void setMenuPanelDeactive (GameObject panelGameObject)
    {
        setMenuPanelActiveState (false, panelGameObject);
    }

    public void setMenuPanelActiveState (bool state, GameObject buttonObject)
    {
        if (enableDisablePanelsEnabled) {
            if (buttonObject != null) {
                int currentIndex = menuPanelInfoList.FindIndex (s => s.mainPanelButton.Equals (buttonObject));

                if (currentIndex > -1) {
                    menuPanelInfo currentMenuPanelInfo = menuPanelInfoList [currentIndex];

                    currentMenuPanelInfo.mainPanel.SetActive (state);

                    if (currentMenuPanelInfo.disabledPanel != null) {
                        currentMenuPanelInfo.disabledPanel.SetActive (!state);
                    }
                }
            }
        }
    }

    [System.Serializable]
    public class menuPanelInfo
    {
        public string Name;

        public GameObject mainPanel;

        public GameObject disabledPanel;

        public GameObject mainPanelButton;
    }
}
