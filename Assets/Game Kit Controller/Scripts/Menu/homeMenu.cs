using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using static menuPause;

public class homeMenu : MonoBehaviour
{
    [Space]
    [Header ("Main Settings")]
    [Space]

    public string mainMenuName = "Main Menu";

    [Space]
    [Header ("Load Screen Settings")]
    [Space]

    public bool useUINavigationOnStart;

    public bool activateCursorOnStartEnabled = true;

    [Space]
    [Header ("UI Navigation Settings")]
    [Space]

    public bool ignoreSelectUIOnGamepadOnUINavigation;

    public bool toggleCursorOrUINaivgationOnPauseMenusEnabled;

    [Space]
    [Header ("Load Screen Settings")]
    [Space]

    public bool useLoadScreen;

    public int loadScreenScene = 1;

    public bool useLastSceneIndexAsLoadScreen = true;

    public string sceneToLoadAsyncPrefsName = "SceneToLoadAsync";

    public bool checkLoadingScreenSceneConfigured = true;

    public string loadingScreenSceneName = "Loading Screen Scene";

    [Space]
    [Header ("Debug")]
    [Space]

    public bool showDebugPrint;

    public bool subMenuActive;

    public string currentMenuName = "";

    public string currentSubMenuActiveName = "";

    [Space]

    public bool menuUsingUINavigationActive;

    [Space]
    [Header ("Menu Settings")]
    [Space]

    public List<menuPause.submenuInfo> submenuInfoList = new List<menuPause.submenuInfo> ();

    [Space]
    [Header ("Event Settings")]
    [Space]

    public UnityEvent eventOnOpenAnyMenu;

    [Space]
    [Header ("Components")]
    [Space]

    public mouseCursorController mainMouseCursorController;

    public virtualCursorControllerSystem mainVirtualCursorControllerSystem;
    public virtualTouchSystem mainVirtualTouchSystem;

    public UIRouter mainUIRouter;

    bool previousMenuUsingUINavigationActive;

    bool useUINavigationOnStartChecked;

    bool mainVirtualTouchSystemLocated;

    bool menuSelectedOnStartWithGamepadChecked;


    void Awake ()
    {
        checkInitialComponents ();
    }

    void Start ()
    {
        AudioListener.pause = false;

        Time.timeScale = 1;

        if (activateCursorOnStartEnabled) {
            showOrHideMouseCursorController (true);
        }
    }

    void checkInitialComponents ()
    {
        bool mouseCursorControllerManagerLocated = mainMouseCursorController != null;

        if (!mouseCursorControllerManagerLocated) {
            mainMouseCursorController = mouseCursorController.Instance;

            mouseCursorControllerManagerLocated = mainMouseCursorController != null;
        }

        if (!mouseCursorControllerManagerLocated) {
            mainMouseCursorController = GKC_Utils.findObjectByComponent<mouseCursorController> ();

            mainMouseCursorController.getComponentInstanceOnApplicationPlaying ();
        }

        mainVirtualTouchSystemLocated = mainVirtualTouchSystem != null;
    }

    public void showOrHideMouseCursorController (bool state)
    {
        mainMouseCursorController.setCursorVisibleStateExternally (state);

        checkIfUseVirtualMouseCursorController (state);

        if (mainVirtualTouchSystemLocated) {
            mainVirtualTouchSystem.setVirtualTouchSystemActiveState (state);
        }
    }

    void checkIfUseVirtualMouseCursorController (bool state)
    {
        if (mainVirtualCursorControllerSystem != null && mainVirtualCursorControllerSystem.isVirtualCursorControllerEnabled ()) {
            mainMouseCursorController.setVirtualCursorCurrentlyActive (state);

            mainVirtualCursorControllerSystem.setCursorActiveState (state);
        }
    }

    void Update ()
    {
        if (InputAxisWrapper.GetKeyDown (KeyCode.Escape)) {

            openOrClosePauseMenuByName (mainMenuName, true);
        }

        if (useUINavigationOnStart) {
            if (!useUINavigationOnStartChecked) {
                if (GamepadRegistry.anyGamepadFound ()) {
                    openOrClosePauseMenuByName (mainMenuName, true);

                    useUINavigationOnStartChecked = true;

                    menuSelectedOnStartWithGamepadChecked = true;
                }

                if (Time.unscaledTime > 0.5f) {
                    useUINavigationOnStartChecked = true;

                    if (!menuSelectedOnStartWithGamepadChecked) {
                        openOrClosePauseMenuByName (mainMenuName, true);
                    }
                }
            }
        }
    }

    public void confirmExit ()
    {
        Application.Quit ();
    }

    public void loadScene (int sceneNumber)
    {
        GKC_Utils.loadScene (sceneNumber, useLoadScreen, loadScreenScene, sceneToLoadAsyncPrefsName,
            useLastSceneIndexAsLoadScreen, checkLoadingScreenSceneConfigured, loadingScreenSceneName);
    }

    public void disableMenuList ()
    {
        for (int i = 0; i < submenuInfoList.Count; i++) {
            if (submenuInfoList [i].menuGameObject != null && submenuInfoList [i].menuGameObject.activeSelf != false) {
                submenuInfoList [i].menuGameObject.SetActive (false);
            }

            if (submenuInfoList [i].menuOpened) {
                submenuInfoList [i].menuOpened = false;

                if (submenuInfoList [i].useEventOnClose) {
                    if (submenuInfoList [i].eventOnClose.GetPersistentEventCount () > 0) {
                        submenuInfoList [i].eventOnClose.Invoke ();
                    }
                }
            }
        }
    }

    public void openOrClosePauseMenuByName (string menuName, bool state)
    {
        int pauseMenuIndex = submenuInfoList.FindIndex (s => s.Name.Equals (menuName));

        bool menuUsingUINavigationActivePreviouslyActive = false;

        setMenuUsingUINavigationActiveState (false);

        if (pauseMenuIndex > -1) {
            if (state) {
                for (int i = 0; i < submenuInfoList.Count; i++) {
                    if (pauseMenuIndex != i) {
                        if (submenuInfoList [i].menuGameObject != null && submenuInfoList [i].menuGameObject.activeSelf != false) {
                            submenuInfoList [i].menuGameObject.SetActive (false);
                        }

                        if (submenuInfoList [i].menuOpened) {
                            submenuInfoList [i].menuOpened = false;

                            if (submenuInfoList [i].useEventOnClose) {
                                if (submenuInfoList [i].eventOnClose.GetPersistentEventCount () > 0) {
                                    submenuInfoList [i].eventOnClose.Invoke ();
                                }
                            }

                            if (submenuInfoList [i].isSubMenu) {
                                exitSubMenu ();
                            }

                            if (submenuInfoList [i].menuUsingUINavigationActive) {
                                //reset all UI navigation state here before opening a new menu
                                //print ("closing UI navigation on menu before opening another " + submenuInfoList [i].Name);

                                mainUIRouter.Pop (submenuInfoList [i].mainUIPage);

                                mainMouseCursorController.setUINavigationCurrentlyActive (false);

                                if (mainVirtualTouchSystemLocated) {
                                    mainVirtualTouchSystem.setUINavigationCurrentlyActive (false);
                                }

                                submenuInfoList [i].menuUsingUINavigationActive = false;

                                menuUsingUINavigationActivePreviouslyActive = true;

                                setMenuUsingUINavigationActiveState (false);
                            }
                        }
                    }
                }
            }

            menuPause.submenuInfo currentSubmenuInfo = submenuInfoList [pauseMenuIndex];

            if (state) {
                currentSubmenuInfo.eventOnOpen.Invoke ();
            } else {
                currentSubmenuInfo.eventOnClose.Invoke ();
            }

            if (currentSubmenuInfo.menuGameObject != null && currentSubmenuInfo.menuGameObject.activeSelf != state) {
                currentSubmenuInfo.menuGameObject.SetActive (state);
            }

            currentSubmenuInfo.menuOpened = state;

            bool isUsingGamepad = GamepadRegistry.anyGamepadFound ();

            bool useUINavigationResult = false;

            bool hideCursorResult = false;

            if (isUsingGamepad) {
                if (mainUIRouter.isUIRouterEnabled ()) {
                    if (currentSubmenuInfo.mainUIPage != null && !currentSubmenuInfo.useCursorOnMenu) {
                        useUINavigationResult = true;

                        hideCursorResult = true;
                    }
                }
            }

            if (state) {
                if (!ignoreSelectUIOnGamepadOnUINavigation) {
                    if (isUsingGamepad) {
                        if (previousMenuUsingUINavigationActive && !useUINavigationResult) {
                            if (menuUsingUINavigationActivePreviouslyActive) {
                                mainMouseCursorController.setForceResetCursorPositionActiveState (true);

                                if (mainVirtualTouchSystemLocated) {
                                    mainVirtualTouchSystem.setForceResetCursorPositionActiveState (true);
                                }
                            }

                            showOrHideMouseCursorController (state);

                            bool canShowRealCursorResult = true;

                            if (mainVirtualCursorControllerSystem != null &&
                                mainVirtualCursorControllerSystem.isVirtualCursorControllerEnabled () &&
                                mainVirtualCursorControllerSystem.isCursorCurrentlyActive ()) {
                                canShowRealCursorResult = false;
                            }

                            if (hideCursorResult) {
                                canShowRealCursorResult = false;
                            }

                            if (canShowRealCursorResult) {
                                menuPause.setCursorLockState (false);

                                menuPause.setCursorVisibleState (true);
                            }
                        }

                        previousMenuUsingUINavigationActive = false;
                    }
                }

                if (hideCursorResult) {
                    previousMenuUsingUINavigationActive = true;

                    menuPause.setCursorLockState (true);

                    menuPause.setCursorVisibleState (false);
                }
            }

            if (useUINavigationResult) {
                if (state) {
                    bool replaceResult = mainUIRouter.Replace (currentSubmenuInfo.mainUIPage);

                    //print ("replaceResult " + replaceResult + " " + currentSubmenuInfo.Name);

                    mainMouseCursorController.setUINavigationCurrentlyActive (true);

                    //mainMouseCursorController.moveCursorToCornerNextVisibleTime ();

                    if (mainVirtualTouchSystemLocated) {
                        mainVirtualTouchSystem.setUINavigationCurrentlyActive (true);
                    }

                    currentSubmenuInfo.menuUsingUINavigationActive = true;
                } else {
                    mainUIRouter.Pop (currentSubmenuInfo.mainUIPage);

                    mainMouseCursorController.setUINavigationCurrentlyActive (false);

                    if (mainVirtualTouchSystemLocated) {
                        mainVirtualTouchSystem.setUINavigationCurrentlyActive (false);
                    }

                    currentSubmenuInfo.menuUsingUINavigationActive = false;
                }
            } else {
                currentSubmenuInfo.menuUsingUINavigationActive = false;
            }

            setMenuUsingUINavigationActiveState (currentSubmenuInfo.menuUsingUINavigationActive);

            if (currentSubmenuInfo.isSubMenu) {
                if (state) {
                    enterSubMenu ();
                } else {
                    exitSubMenu ();
                }
            }

            currentMenuName = menuName;

            eventOnOpenAnyMenu.Invoke ();
        }
    }

    public void returnToPreviousMenu ()
    {
        if (currentMenuName == "") {
            return;
        }

        if (subMenuActive) {
            openPauseMenuByName (currentSubMenuActiveName);

            exitSubMenu ();
        } else {
            openPauseMenuByName (mainMenuName);
        }
    }

    public void openPauseMenuByName (string menuName)
    {
        openOrClosePauseMenuByName (menuName, true);
    }

    public void closePauseMenuByName (string menuName)
    {
        openOrClosePauseMenuByName (menuName, false);
    }

    public void enterSubMenu ()
    {
        subMenuActive = true;

        currentSubMenuActiveName = currentMenuName;
    }

    public void exitSubMenu ()
    {
        subMenuActive = false;
    }

    //check used when a gamepad is connected or disconnected to enable or disable the UI navigation if used
    public void showOrHideCursorOnGamepadStateChange ()
    {
        if (ignoreSelectUIOnGamepadOnUINavigation) {
            return;
        }

        if (currentMenuName == "") {
            return;
        }

        int pauseMenuIndex = submenuInfoList.FindIndex (s => s.Name.Equals (currentMenuName));

        if (pauseMenuIndex <= -1) {
            return;
        }

        menuPause.submenuInfo currentSubmenuInfo = submenuInfoList [pauseMenuIndex];

        //print ("showOrHideCursorOnGamepadStateChange " + currentSubmenuInfo.Name);

        bool menuUsingUINavigationActive = currentSubmenuInfo.menuUsingUINavigationActive;

        bool isUsingGamepad = GamepadRegistry.anyGamepadFound ();

        //print (menuUsingUINavigationActive + " " + isUsingGamepad);

        if (isUsingGamepad) {
            if (menuUsingUINavigationActive) {
                print ("using gamepad and ui navigation already active, cancelling");

                return;
            }
        } else {
            if (!menuUsingUINavigationActive) {
                print ("no using gamepad and cursor already active, cancelling");

                return;
            }
        }

        bool useUINavigationResult = false;

        bool hideCursorResult = false;

        if (isUsingGamepad) {
            if (mainUIRouter.isUIRouterEnabled ()) {
                if (currentSubmenuInfo.mainUIPage != null && !currentSubmenuInfo.useCursorOnMenu) {
                    useUINavigationResult = true;

                    hideCursorResult = true;
                }
            }
        }

        if (!ignoreSelectUIOnGamepadOnUINavigation) {
            if (isUsingGamepad) {
                if (previousMenuUsingUINavigationActive && !useUINavigationResult) {
                    showOrHideMouseCursorController (true);

                    bool canShowRealCursorResult = true;

                    if (mainVirtualCursorControllerSystem != null &&
                        mainVirtualCursorControllerSystem.isVirtualCursorControllerEnabled () &&
                        mainVirtualCursorControllerSystem.isCursorCurrentlyActive ()) {
                        canShowRealCursorResult = false;
                    }

                    if (hideCursorResult) {
                        canShowRealCursorResult = false;
                    }

                    if (canShowRealCursorResult) {
                        menuPause.setCursorLockState (false);

                        menuPause.setCursorVisibleState (true);
                    }
                }

                previousMenuUsingUINavigationActive = false;
            } else {
                showOrHideMouseCursorController (true);

                bool canShowRealCursorResult = true;

                if (mainVirtualCursorControllerSystem != null &&
                    mainVirtualCursorControllerSystem.isVirtualCursorControllerEnabled () &&
                    mainVirtualCursorControllerSystem.isCursorCurrentlyActive ()) {
                    canShowRealCursorResult = false;
                }

                if (hideCursorResult) {
                    canShowRealCursorResult = false;
                }

                if (canShowRealCursorResult) {
                    menuPause.setCursorLockState (false);

                    menuPause.setCursorVisibleState (true);
                }
            }
        }

        if (hideCursorResult) {
            previousMenuUsingUINavigationActive = true;

            menuPause.setCursorLockState (true);

            menuPause.setCursorVisibleState (false);
        }

        //print ("useUINavigationResult " + useUINavigationResult);

        if (useUINavigationResult) {

            mainUIRouter.Replace (currentSubmenuInfo.mainUIPage);

            mainMouseCursorController.setUINavigationCurrentlyActive (true);

            //mainMouseCursorController.moveCursorToCornerNextVisibleTime ();

            if (mainVirtualTouchSystemLocated) {
                mainVirtualTouchSystem.setUINavigationCurrentlyActive (true);
            }

            currentSubmenuInfo.menuUsingUINavigationActive = true;

        } else {
            currentSubmenuInfo.menuUsingUINavigationActive = false;

            mainUIRouter.Pop (currentSubmenuInfo.mainUIPage);
        }

        setMenuUsingUINavigationActiveState (currentSubmenuInfo.menuUsingUINavigationActive);
    }

    //function used to toggle the UI and cursor at any point when using the menu
    void showOrHideCursorOnCurrentPauseMenu ()
    {
        //used to toggle the mouse cursor control and UI navigation on the current pause menu

        if (ignoreSelectUIOnGamepadOnUINavigation) {
            return;
        }

        bool usingGamepad = GamepadRegistry.anyGamepadFound ();

        if (!usingGamepad) {
            return;
        }

        if (currentMenuName == "") {
            return;
        }

        int pauseMenuIndex = submenuInfoList.FindIndex (s => s.Name.Equals (currentMenuName));

        if (pauseMenuIndex <= -1) {
            return;
        }

        menuPause.submenuInfo currentSubmenuInfo = submenuInfoList [pauseMenuIndex];

        if (showDebugPrint) {
            print ("showOrHideCursorOnCurrentPauseMenu " + currentSubmenuInfo.Name);
        }

        bool menuUsingUINavigationActive = currentSubmenuInfo.menuUsingUINavigationActive;

        if (showDebugPrint) {
            print (menuUsingUINavigationActive + " " + usingGamepad);
        }

        if (usingGamepad) {
            if (menuUsingUINavigationActive) {
                if (showDebugPrint) {
                    print ("using gamepad and ui navigation, changing to cursor control ");
                }

                usingGamepad = false;
            } else {
                if (showDebugPrint) {
                    print ("using gamepad with cursor control, changing to UI navgiation");
                }
            }
        }

        bool useUINavigationResult = false;

        bool hideCursorResult = false;

        if (usingGamepad) {
            if (mainUIRouter.isUIRouterEnabled ()) {
                if (currentSubmenuInfo.mainUIPage != null && !currentSubmenuInfo.useCursorOnMenu) {
                    useUINavigationResult = true;

                    hideCursorResult = true;
                }
            }
        }

        if (!ignoreSelectUIOnGamepadOnUINavigation) {
            if (usingGamepad) {
                if (previousMenuUsingUINavigationActive && !useUINavigationResult) {
                    showOrHideMouseCursorController (true);

                    bool canShowRealCursorResult = true;

                    if (mainVirtualCursorControllerSystem != null &&
                        mainVirtualCursorControllerSystem.isVirtualCursorControllerEnabled () &&
                        mainVirtualCursorControllerSystem.isCursorCurrentlyActive ()) {
                        canShowRealCursorResult = false;
                    }

                    if (hideCursorResult) {
                        canShowRealCursorResult = false;
                    }

                    if (canShowRealCursorResult) {
                        setCursorLockState (false);

                        setCursorVisibleState (true);
                    }
                }

                previousMenuUsingUINavigationActive = false;
            } else {
                showOrHideMouseCursorController (true);

                bool canShowRealCursorResult = true;

                if (mainVirtualCursorControllerSystem != null &&
                    mainVirtualCursorControllerSystem.isVirtualCursorControllerEnabled () &&
                    mainVirtualCursorControllerSystem.isCursorCurrentlyActive ()) {
                    canShowRealCursorResult = false;
                }

                if (hideCursorResult) {
                    canShowRealCursorResult = false;
                }

                if (canShowRealCursorResult) {
                    setCursorLockState (false);

                    setCursorVisibleState (true);
                }
            }
        }

        if (hideCursorResult) {
            previousMenuUsingUINavigationActive = true;

            setCursorLockState (true);

            setCursorVisibleState (false);
        }

        if (showDebugPrint) {
            print ("useUINavigationResult " + useUINavigationResult);
        }

        if (useUINavigationResult) {
            mainUIRouter.Replace (currentSubmenuInfo.mainUIPage);

            mainMouseCursorController.setUINavigationCurrentlyActive (true);

            if (mainVirtualTouchSystemLocated) {
                mainVirtualTouchSystem.setUINavigationCurrentlyActive (true);
            }

            currentSubmenuInfo.menuUsingUINavigationActive = true;

        } else {
            mainMouseCursorController.setUINavigationCurrentlyActive (false);

            if (mainVirtualTouchSystemLocated) {
                mainVirtualTouchSystem.setUINavigationCurrentlyActive (false);
            }

            currentSubmenuInfo.menuUsingUINavigationActive = false;

            mainUIRouter.Pop (currentSubmenuInfo.mainUIPage);
        }

        setMenuUsingUINavigationActiveState (currentSubmenuInfo.menuUsingUINavigationActive);
    }

    void setMenuUsingUINavigationActiveState (bool state)
    {
        menuUsingUINavigationActive = state;
    }

    public bool isMenuUsingUINavigationActive ()
    {
        return menuUsingUINavigationActive;
    }

    public void setGameVolume (float newValue)
    {
        AudioListener.volume = newValue;
    }


    //INPUT FUNCTIONS
    public void inputShowOrHideCursorOnCurrentPauseMenu ()
    {
        if (toggleCursorOrUINaivgationOnPauseMenusEnabled) {
            showOrHideCursorOnCurrentPauseMenu ();
        }
    }
}