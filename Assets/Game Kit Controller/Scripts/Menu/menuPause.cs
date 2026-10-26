using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class menuPause : MonoBehaviour
{
    [Header ("Main Settings")]
    [Space]

    public bool menuPauseEnabled;

    public bool activateDeathMenuWhenPlayeDies = true;

    public bool closeOnlySubMenuIfOpenOnEscape;

    public int sceneNumberForHomeMenu = 0;

    public bool saveGameAutomaticallyOnReturnToHome;

    public bool pauseAudioListenerOnGamePaused = true;

    public float timeScaleOnGamePaused = 0;

    public bool ignoreChangeFixedDeltaTime;

    public bool pauseAIWhenPauseMenuOpened;

    public int pauseCharacterPriority = 1;

    [Space]
    [Header ("UI Navigation Settings")]
    [Space]

    public bool ignoreSelectUIOnGamepadOnUINavigation;

    public bool toggleCursorOrUINaivgationOnPauseMenusEnabled;

    public bool toggleCursorOrUINaivgationOnIngameMenusEnabled;

    [Space]
    [Header ("Fade Black Screen On Start Settings")]
    [Space]

    public bool fadeScreenActive = true;
    public float fadeScreenSpeed = 2;

    [Space]
    [Header ("Canvas Group Menu Settings")]
    [Space]

    public bool useCanvasGroup;
    public float fadeOnPanelSpeed;
    public float fadeOffPanelSpeed;

    [Space]
    [Header ("Menu Settings")]
    [Space]

    public List<submenuInfo> submeneInfoList = new List<submenuInfo> ();

    [Space]
    [Space]

    public List<playerMenuInfo> playerMenuInfoList = new List<playerMenuInfo> ();

    [Space]
    [Space]

    public List<ingameMenuInfo> ingameMenuInfoList = new List<ingameMenuInfo> ();

    [Space]
    [Space]

    public bool useDefaultPlayerMenuNameToOpen;
    public string defaultPlayerMenuNameToOpen;
    public bool defaultPlayerMenuInputEnabled = true;

    [Space]
    [Header ("Other Settings")]
    [Space]

    public bool changeBetweenMenusInputEnabled = true;

    public string mainPauseMenuName = "Pause Menu";

    public string mainDeathMenuName = "Die Menu";

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
    [Header ("Restart Game Settings")]
    [Space]

    public bool useCustomSceneIndexOnRestartGame;
    public int customSceneIndexOnRestartGame;

    [Space]
    [Header ("Touch Zone List Settings")]
    [Space]

    public List<GameObject> touchZoneList = new List<GameObject> ();

    [Space]
    [Header ("Help Menu Settings")]
    [Space]

    public bool inputHelpMenuEnabled;
    public GameObject inputHelpMenuGameObject;
    bool inputHelpMenuEnabledState;

    [Space]
    [Header ("Mechanic Menu Settings")]
    [Space]

    public bool mechanicsHelpMenuEnabled;
    public GameObject mechanicsHelpMenuGameObject;
    bool mechanicsHelpMenuEnabledState;

    [Space]
    [Header ("Blur Background Settings")]
    [Space]

    public bool blurPanelEnabled = true;
    public bool useBlurUIPanelOnPlayerMenu;
    public bool useBlurUIPanelOnPause;
    public GameObject blurUIPanel;
    public Material blurUIPanelMaterial;
    public Image blurUIPanelImage;
    public float blurUIPanelValue = 4.4f;
    public float blurUIPanelSpeed = 10;
    public float blurUIPanelAlphaSpeed = 10;
    public float blurUIPanelAlphaValue = 120;
    public Transform blurUIPanelParent;

    [Space]
    [Header ("Event Settings")]
    [Space]

    public eventParameters.eventToCallWithBool eventToEnableOrDisableAllPlayerHUD;
    public eventParameters.eventToCallWithBool eventToEnableOrDisableSecondaryPlayerHUD;
    public eventParameters.eventToCallWithBool eventToEnableOrDisableAllVehicleHUD;

    [Space]

    public bool useEventsOnPauseResume;
    public UnityEvent eventOnPauseGame;
    public UnityEvent eventOnResumeGame;

    [Space]

    public bool useEventOnOpenCloseIngameMenu;
    public UnityEvent eventOnOpenIngameMenu;
    public UnityEvent eventOnCloseIngameMenu;

    [Space]

    public bool useEventOnShowCursor;
    public UnityEvent eventOnShowCursor;
    public UnityEvent eventOnHideCursor;

    [Space]
    [Header ("Menu Pause Elements")]
    [Space]

    public List<canvasInfo> canvasInfoList = new List<canvasInfo> ();

    [Space]
    [Header ("Pause Manager State/Debug")]
    [Space]

    public bool showDebugPrint;

    public cursorStateInfo cursorState;

    public int currentPlayerMenuInfoIndex;

    public int currentIngameMenuIndex;

    public bool gamePaused = false;

    public bool subMenuActive;
    public bool usingSubMenu;

    public bool playerMenuActive;

    public bool useTouchControls = false;
    public bool usingTouchControlsPreviously;

    public bool usingDevice;
    public bool dead;

    public bool ignoreChangeHUDElements;

    public bool ignoreDisableTouchZoneList;

    public bool pauseGameInputPaused;
    public bool changeBetweenIngameMenuPaused;
    public bool openOrClosePlayerOpenMenuByNamePaused;

    public bool regularTimeScaleActive = true;

    public bool openOrClosePauseMenuExternallyPaused;

    public bool openMenuSystemAssigned;

    public bool previousMenuUsingUINavigationActive;

    public bool ignoreBlurPanelActive;

    public bool ignoreShowOrHideMouseCursorAndPauseHideCheckOnClickExternally;

    [Space]

    public bool menuOpenedWithActionSystemActive;

    public string menuOpenedWithActionSystemName;

    [Space]

    public bool menuUsingUINavigationActive;

    [Space]

    public bool showCursorStateChangeDebugPrint;

    [Space]

    public bool ignoreTimeScaleOnMenusActive;

    public bool usingPointAndClick;

    [Space]
    [Header ("UI Elements")]
    [Space]

    public GameObject hudAndMenus;
    public GameObject touchPanel;
    public GameObject pauseMenu;
    public Transform pauseMenuParent;
    public GameObject dieMenu;

    public Image blackBottomImage;

    public GameObject dynamicUIElementsParent;

    public Canvas mainCanvas;
    public RectTransform mainCanvasRectTransform;
    public CanvasScaler mainCanvasScaler;
    public GraphicRaycaster mainGraphicRaycaster;

    [Space]
    [Header ("Components")]
    [Space]

    public gameManager mainGameManager;
    public playerInputManager playerInput;
    public playerHUDManager mainPlayerHUDManager;

    public Camera mainCanvasCamera;
    public inputManager input;
    public timeBullet timeManager;
    public playerWeaponsManager mainWeaponsManager;
    public playerHealthBarManagementSystem mainPlayerHealthBarManager;
    public playerScreenObjectivesSystem playerScreenObjectivesManager;
    public playerPickupIconManager mainPlayerPickupIconManager;
    public mouseCursorController mouseCursorControllerManager;
    public playerController playerControllerManager;
    public playerCamera playerCameraManager;
    public editControlPosition editControlPositionManager;
    public virtualCursorControllerSystem mainVirtualCursorControllerSystem;

    public UIRouter mainUIRouter;

    openMenuSystem currentOpenMenuSystem;

    bool showGUI = false;

    Color alpha;

    Coroutine blurUIPanelCoroutine;
    bool blurUIPanelActive;

    bool mouseButtonPressPaused;

    float timeToResetCursorAfterDisable = 0.1f;
    float lastTimeCursorDisabled = 10;

    bool checkLockCursorBetweenMenus;

    int blurSizeID = -1;

    float fadeScreenDirection = -1;

    float fadeScreenTarget = 0;

    bool ignoreHideCursorOnClickActive;

    bool ignoreSetSelectedGameObjectOnUIByGamepad;

    bool externalFadeScreenActive;

    bool allowToExitMenuWithTitleMenuTextButtonActive;


    bool openOrCloseePauseMenuByNameInProcess;

    bool openOrClosePauseMenuStored;
    string openOrClosePauseMenuNameStored = "";
    bool openOrClosePauseMenuStateStored;

    bool mainUIRouterLocated;

    static bool showCursorStateChangeDebugPrintValue;

    bool mainGraphicRaycasterLocated;

    void Awake ()
    {
        bool mainGameManagerLocated = mainGameManager != null;

        if (!mainGameManagerLocated) {
            mainGameManager = gameManager.Instance;

            mainGameManagerLocated = mainGameManager != null;
        }

        if (!mainGameManagerLocated) {
            mainGameManager = GKC_Utils.findObjectByComponent<gameManager> ();

            mainGameManager.getComponentInstanceOnApplicationPlaying ();
        }


        bool inputManagerLocated = input != null;

        if (!inputManagerLocated) {
            input = inputManager.Instance;

            inputManagerLocated = input != null;
        }

        if (!inputManagerLocated) {
            input = GKC_Utils.findObjectByComponent<inputManager> ();

            input.getComponentInstanceOnApplicationPlaying ();
        }


        bool mouseCursorControllerManagerLocated = mouseCursorControllerManager != null;

        if (!mouseCursorControllerManagerLocated) {
            mouseCursorControllerManager = mouseCursorController.Instance;

            mouseCursorControllerManagerLocated = mouseCursorControllerManager != null;
        }

        if (!mouseCursorControllerManagerLocated) {
            mouseCursorControllerManager = GKC_Utils.findObjectByComponent<mouseCursorController> ();

            mouseCursorControllerManager.getComponentInstanceOnApplicationPlaying ();
        }

        playerCameraManager.setMainCanvasRectTransform (mainCanvasRectTransform);
        playerCameraManager.setMainCanvasCamera (mainCanvasCamera);

        mainUIRouterLocated = mainUIRouter != null;

        showCursorStateChangeDebugPrintValue = showCursorStateChangeDebugPrint;
    }

    void Start ()
    {
        AudioListener.pause = false;

        if (blackBottomImage != null) {
            blackBottomImage.enabled = true;
        }

        if (!mainGameManager.isUsingTouchControls ()) {
            showOrHideCursor (false);
        } else {
            enableOrDisableTouchControls (true);
        }

        editControlPositionManager.getTouchButtons ();

        if (touchPanel.activeSelf != useTouchControls) {
            touchPanel.SetActive (useTouchControls);
        }

        alpha.a = 1;

        if (Time.timeScale != 1) {
            setTimeScale (1);
        }

        //if the fade of the screen is disabled, just set the alpha of the black panel to 0
        if (!fadeScreenActive) {
            alpha.a = 0;

            if (blackBottomImage != null) {
                blackBottomImage.color = alpha;
                blackBottomImage.enabled = false;
            }
        }

        if (pauseMenu.activeSelf) {
            pauseMenu.SetActive (false);
        }

        disableMenuList ();

        setCurrentCameraState (true);

        checkBlurSizeID ();

        blurUIPanelMaterial.SetFloat (blurSizeID, 0);

        for (int i = 0; i < ingameMenuInfoList.Count; i++) {
            createIngameMenuPanel (i);
        }
    }

    void Update ()
    {
        //if the fade is enabled, decrease the value of alpha to get a nice fading effect at the beginning of the game
        if (fadeScreenActive) {
            alpha.a += (getDeltaTime () / fadeScreenSpeed) * fadeScreenDirection;

            blackBottomImage.color = alpha;

            if ((fadeScreenTarget == 0 && alpha.a <= 0) || (fadeScreenTarget == 1 && alpha.a >= 1)) {
                if (!externalFadeScreenActive) {
                    blackBottomImage.enabled = false;
                }

                fadeScreenActive = false;
            }
        }

        //if the mouse is showed, press in the screen to lock it again
        if (!gamePaused && !mouseButtonPressPaused && !useTouchControls) {
            //check that the touch controls are disabled, the player is not dead, the powers is not being editing or selecting, the player is not using a device
            //or the cursor is visible
            if (input.GetMouseButtonDown (0) || input.GetMouseButtonDown (1)) {
                bool canHideCursor = true;

                if (ignoreHideCursorOnClickActive) {
                    canHideCursor = false;
                }

                if (useTouchControls) {
                    canHideCursor = false;
                }

                if (dead || mainGameManager.anyCharacterDead ()) {
                    canHideCursor = false;
                }

                if (!inGameWindowOpened ()) {
                    canHideCursor = false;
                }

                if (Cursor.lockState != CursorLockMode.None && !Cursor.visible) {
                    canHideCursor = false;
                }

                if (isUsingGamepad () && mouseCursorControllerManager.getCurrentJoystickNumber () != playerInput.getPlayerID ()) {
                    canHideCursor = false;
                }

                if (canHideCursor) {
                    showOrHideCursor (false);
                }
            }
        }

        if (inputHelpMenuEnabled) {
            if (InputAxisWrapper.GetKeyDown (KeyCode.F1)) {
                if (inputHelpMenuGameObject != null) {
                    inputHelpMenuEnabledState = !inputHelpMenuEnabledState;

                    if (inputHelpMenuGameObject.activeSelf != inputHelpMenuEnabledState) {
                        inputHelpMenuGameObject.SetActive (inputHelpMenuEnabledState);
                    }
                }
            }
        }

        if (mechanicsHelpMenuEnabled) {
            if (InputAxisWrapper.GetKeyDown (KeyCode.F2)) {
                if (mechanicsHelpMenuGameObject != null) {
                    mechanicsHelpMenuEnabledState = !mechanicsHelpMenuEnabledState;

                    if (mechanicsHelpMenuGameObject.activeSelf != mechanicsHelpMenuEnabledState) {
                        mechanicsHelpMenuGameObject.SetActive (mechanicsHelpMenuEnabledState);
                    }
                }
            }
        }
    }

    public void activateFadePanelBackgroundIn ()
    {
        alpha.a = 1;

        blackBottomImage.color = alpha;

        blackBottomImage.enabled = true;

        fadeScreenActive = true;

        fadeScreenDirection = -1;

        fadeScreenTarget = 0;

        externalFadeScreenActive = false;
    }

    public void activateFadePanelBackgroundOut ()
    {
        alpha.a = 0;

        blackBottomImage.color = alpha;

        blackBottomImage.enabled = true;

        fadeScreenActive = true;

        fadeScreenDirection = 1;

        fadeScreenTarget = 1;

        externalFadeScreenActive = true;
    }

    public void deactivateFadePanelBackground ()
    {
        alpha.a = 0;

        blackBottomImage.color = alpha;

        blackBottomImage.enabled = false;

        fadeScreenActive = false;

        externalFadeScreenActive = false;
    }

    public void setCustomFadeScreenSpeed (float newValue)
    {
        fadeScreenSpeed = newValue;
    }

    void OnDisable ()
    {
        if (blurUIPanelActive) {
            if (blurUIPanelMaterial != null) {
                checkBlurSizeID ();

                blurUIPanelMaterial.SetFloat (blurSizeID, 0);
            }
        }
    }

    public void disableMenuList ()
    {
        for (int i = 0; i < submeneInfoList.Count; i++) {
            if (submeneInfoList [i].menuGameObject != null && submeneInfoList [i].menuGameObject.activeSelf != false) {
                submeneInfoList [i].menuGameObject.SetActive (false);
            }

            if (submeneInfoList [i].menuOpened) {
                submeneInfoList [i].menuOpened = false;

                if (submeneInfoList [i].useEventOnClose) {
                    if (submeneInfoList [i].eventOnClose.GetPersistentEventCount () > 0) {
                        submeneInfoList [i].eventOnClose.Invoke ();
                    }
                }
            }
        }
    }

    public void setOpenOrClosePauseMenuExternallyPausedState (bool state)
    {
        openOrClosePauseMenuExternallyPaused = state;
    }

    public void openPauseMenuByName (string menuName)
    {
        if (openOrClosePauseMenuExternallyPaused) {
            return;
        }

        openOrClosePauseMenuByName (menuName, true);
    }

    public void closePauseMenuByName (string menuName)
    {
        if (openOrClosePauseMenuExternallyPaused) {
            return;
        }

        openOrClosePauseMenuByName (menuName, false);
    }

    public void openOrClosePauseMenuByName (string menuName, bool state)
    {
        if (openOrCloseePauseMenuByNameInProcess) {
            openOrClosePauseMenuNameStored = menuName;

            openOrClosePauseMenuStateStored = state;

            openOrClosePauseMenuStored = true;

            if (showDebugPrint) {
                print ("trying to open or close other menu while another in process, cancelling and storing " + menuName + " " + state);
            }

            return;
        }

        openOrCloseePauseMenuByNameInProcess = true;

        ignoreSetSelectedGameObjectOnUIByGamepad = false;

        setMenuUsingUINavigationActiveState (false);

        int pauseMenuIndex = submeneInfoList.FindIndex (s => s.Name.Equals (menuName));

        bool menuUsingUINavigationActivePreviouslyActive = false;

        if (pauseMenuIndex > -1) {
            if (state) {
                for (int i = 0; i < submeneInfoList.Count; i++) {
                    if (pauseMenuIndex != i) {
                        if (submeneInfoList [i].menuGameObject != null && submeneInfoList [i].menuGameObject.activeSelf != false) {
                            submeneInfoList [i].menuGameObject.SetActive (false);
                        }

                        if (submeneInfoList [i].menuOpened) {
                            submeneInfoList [i].menuOpened = false;

                            if (submeneInfoList [i].useEventOnClose) {
                                if (submeneInfoList [i].eventOnClose.GetPersistentEventCount () > 0) {
                                    submeneInfoList [i].eventOnClose.Invoke ();
                                }
                            }

                            if (submeneInfoList [i].isSubMenu) {
                                exitSubMenu ();
                            }

                            if (submeneInfoList [i].menuUsingUINavigationActive) {
                                //reset all UI navigation state here before opening a new menu
                                //print ("closing UI navigation on menu before opening another " + submeneInfoList [i].Name);

                                if (mainUIRouterLocated) {
                                    mainUIRouter.Pop (submeneInfoList [i].mainUIPage);
                                }

                                mouseCursorControllerManager.setUINavigationCurrentlyActive (false);

                                playerInput.setUINavigationCurrentlyActive (false);


                                submeneInfoList [i].menuUsingUINavigationActive = false;

                                menuUsingUINavigationActivePreviouslyActive = true;

                                setMenuUsingUINavigationActiveState (false);
                            }
                        }
                    }
                }
            }

            submenuInfo currentSubmenuInfo = submeneInfoList [pauseMenuIndex];

            if (state) {
                currentSubmenuInfo.eventOnOpen.Invoke ();
            } else {
                currentSubmenuInfo.eventOnClose.Invoke ();
            }

            if (currentSubmenuInfo.menuGameObject != null && currentSubmenuInfo.menuGameObject.activeSelf != state) {
                currentSubmenuInfo.menuGameObject.SetActive (state);
            }

            currentSubmenuInfo.menuOpened = state;

            bool useUINavigationResult = false;

            bool hideCursorResult = false;

            if (isUsingGamepad ()) {
                if (mainUIRouterLocated && isUIRouterEnabled ()) {
                    if (currentSubmenuInfo.mainUIPage != null && !currentSubmenuInfo.useCursorOnMenu) {
                        useUINavigationResult = true;

                        hideCursorResult = true;
                    }
                }
            }

            if (state) {
                if (!ignoreSelectUIOnGamepadOnUINavigation) {
                    if (isUsingGamepad ()) {
                        if (previousMenuUsingUINavigationActive && !useUINavigationResult) {
                            if (menuUsingUINavigationActivePreviouslyActive) {
                                mouseCursorControllerManager.setForceResetCursorPositionActiveState (true);

                                playerInput.setForceResetCursorPositionActiveState (true);
                            }

                            showOrHideMouseCursorController (playerInput, state, true);

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
                    }
                }

                if (hideCursorResult) {
                    ignoreSetSelectedGameObjectOnUIByGamepad = true;

                    previousMenuUsingUINavigationActive = true;

                    bool lockCursorResult = true;

                    if (isUsingGamepad () && useTouchControls && input.isAllowGamepadInTouchDevice () && !input.isTouchPlatform ()) {
                        lockCursorResult = false;
                    }

                    if (lockCursorResult) {
                        setCursorLockState (true);

                        setCursorVisibleState (false);
                    }
                }
            }

            if (useUINavigationResult) {
                //move the above check here in case of the pop is needed
                if (state) {
                    //bool replaceResult = 

                    if (mainUIRouterLocated) {
                        mainUIRouter.Push (currentSubmenuInfo.mainUIPage);
                    }

                    //print ("replaceResult " + replaceResult + " " + currentSubmenuInfo.Name);

                    mouseCursorControllerManager.setUINavigationCurrentlyActive (true);

                    //mouseCursorControllerManager.moveCursorToCornerNextVisibleTime ();

                    //playerInput.moveCursorToCornerNextVisibleTime ();

                    playerInput.setUINavigationCurrentlyActive (true);

                    currentSubmenuInfo.menuUsingUINavigationActive = true;
                } else {
                    if (mainUIRouterLocated) {
                        mainUIRouter.Pop (currentSubmenuInfo.mainUIPage);
                    }

                    mouseCursorControllerManager.setUINavigationCurrentlyActive (false);

                    playerInput.setUINavigationCurrentlyActive (false);

                    currentSubmenuInfo.menuUsingUINavigationActive = false;
                }
            } else {
                currentSubmenuInfo.menuUsingUINavigationActive = false;
            }

            if (currentSubmenuInfo.isSubMenu) {
                if (state) {
                    enterSubMenu ();
                } else {
                    exitSubMenu ();
                }
            }

            setMenuUsingUINavigationActiveState (currentSubmenuInfo.menuUsingUINavigationActive);
        }

        openOrCloseePauseMenuByNameInProcess = false;

        if (openOrClosePauseMenuStored) {
            openOrClosePauseMenuStored = false;

            openOrClosePauseMenuByName (openOrClosePauseMenuNameStored, openOrClosePauseMenuStateStored);
        }
    }

    void setMenuUsingUINavigationActiveState (bool state)
    {
        menuUsingUINavigationActive = state;
    }

    public bool isMenuUsingUINavigationActive ()
    {
        return menuUsingUINavigationActive;
    }

    void showOrHideCursorOnCurrentPauseMenu ()
    {
        //used to toggle the mouse cursor control and UI navigation on the current pause menu

        if (ignoreSelectUIOnGamepadOnUINavigation) {
            return;
        }

        if (!gamePaused) {
            return;
        }

        bool usingGamepad = isUsingGamepad ();

        if (!usingGamepad) {
            return;
        }

        int menuIndex = -1;

        for (int i = 0; i < submeneInfoList.Count; i++) {
            if (submeneInfoList [i].menuOpened) {
                menuIndex = i;
            }
        }

        if (menuIndex <= -1) {
            return;
        }

        submenuInfo currentSubmenuInfo = submeneInfoList [menuIndex];

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
            if (isUIRouterEnabled ()) {
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
            if (mainUIRouterLocated) {
                mainUIRouter.Push (currentSubmenuInfo.mainUIPage);
            }

            mouseCursorControllerManager.setUINavigationCurrentlyActive (true);

            //mouseCursorControllerManager.moveCursorToCornerNextVisibleTime ();

            playerInput.setUINavigationCurrentlyActive (true);

            currentSubmenuInfo.menuUsingUINavigationActive = true;

        } else {
            if (mainUIRouterLocated) {
                mainUIRouter.Pop (currentSubmenuInfo.mainUIPage);
            }

            mouseCursorControllerManager.setUINavigationCurrentlyActive (false);

            playerInput.setUINavigationCurrentlyActive (false);

            currentSubmenuInfo.menuUsingUINavigationActive = false;
        }

        setMenuUsingUINavigationActiveState (currentSubmenuInfo.menuUsingUINavigationActive);
    }

    void showOrHideCursorOnCurrentIngameMenu ()
    {
        //used to toggle the mouse cursor control and UI navigation on the current ingame menu

        if (showDebugPrint) {
            print ("showOrHideCursorOnCurrentIngameMenu" + usingSubMenu + " " + currentIngameMenuIndex);
        }

        if (ignoreSelectUIOnGamepadOnUINavigation) {
            return;
        }

        if (!usingSubMenu) {
            return;
        }

        bool usingGamepad = isUsingGamepad ();

        if (!usingGamepad) {
            return;
        }

        int menuIndex = -1;

        for (int i = 0; i < ingameMenuInfoList.Count; i++) {
            if (ingameMenuInfoList [i].menuOpened) {
                menuIndex = i;
            }
        }

        if (menuIndex == -1) {
            return;
        }

        ingameMenuInfo currentIngameMenuInfo = null;

        if (currentIngameMenuIndex > -1) {
            currentIngameMenuInfo = ingameMenuInfoList [currentIngameMenuIndex];
        } else {
            return;
        }

        if (!currentIngameMenuInfo.menuOpened) {
            return;
        }

        //print (currentIngameMenuInfo.menuOpened);

        if (showDebugPrint) {
            print ("showOrHideCursorOnCurrentIngameMenu " + currentIngameMenuInfo.Name);
        }

        bool menuUsingUINavigationActive = currentIngameMenuInfo.menuUsingUINavigationActive;

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
            if (isUIRouterEnabled ()) {
                if (currentIngameMenuInfo.mainUIPage != null && !currentIngameMenuInfo.useCursorOnMenu) {
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
            if (mainUIRouterLocated) {
                mainUIRouter.Push (currentIngameMenuInfo.mainUIPage);
            }

            mouseCursorControllerManager.setUINavigationCurrentlyActive (true);

            //mouseCursorControllerManager.moveCursorToCornerNextVisibleTime ();

            playerInput.setUINavigationCurrentlyActive (true);

            currentIngameMenuInfo.menuUsingUINavigationActive = true;

        } else {
            if (mainUIRouterLocated) {
                mainUIRouter.Pop (currentIngameMenuInfo.mainUIPage);
            }

            mouseCursorControllerManager.setUINavigationCurrentlyActive (false);

            playerInput.setUINavigationCurrentlyActive (false);

            currentIngameMenuInfo.menuUsingUINavigationActive = false;
        }

        setMenuUsingUINavigationActiveState (currentIngameMenuInfo.menuUsingUINavigationActive);
    }

    public void showCursorOrSetUINavigationByExternalSystem (bool state, bool useUINavigationIfGamepadActive, UIPage UIPageToUse)
    {
        bool usingGamepad = isUsingGamepad ();

        showOrHideCursor (state);

        if (usingGamepad) {
            showOrHideMouseCursorController (playerInput, state, true);
        } else {
            showOrHideMouseCursorController (state, true);
        }

        bool useUINavigationResult = false;

        bool hideCursorResult = false;

        if (usingGamepad) {
            if (isUIRouterEnabled ()) {
                if (useUINavigationIfGamepadActive) {
                    useUINavigationResult = true;

                    hideCursorResult = true;
                }
            }
        }

        if (state) {
            if (usingGamepad) {
                if (!useUINavigationResult) {
                    showOrHideMouseCursorController (playerInput, state, true);

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
                bool lockCursorResult = true;

                if (usingGamepad && useTouchControls && input.isAllowGamepadInTouchDevice () && !input.isTouchPlatform ()) {
                    lockCursorResult = false;
                }

                if (lockCursorResult) {
                    setCursorLockState (true);

                    setCursorVisibleState (false);
                }
            }
        }


        if (useUINavigationResult) {
            if (state) {
                if (mainUIRouterLocated && UIPageToUse != null) {
                    mainUIRouter.Push (UIPageToUse);
                }

                mouseCursorControllerManager.setUINavigationCurrentlyActive (true);

                //mouseCursorControllerManager.moveCursorToCornerNextVisibleTime ();

                playerInput.setUINavigationCurrentlyActive (true);
            } else {
                if (mainUIRouterLocated && UIPageToUse != null) {
                    mainUIRouter.Pop (UIPageToUse);
                }

                mouseCursorControllerManager.setUINavigationCurrentlyActive (false);

                playerInput.setUINavigationCurrentlyActive (false);
            }
        }
    }


    public void setPauseScreenWithoutPausingGameState (bool state)
    {
        mouseButtonPressPaused = state;

        changeCameraState (!state);

        //check if the touch controls were enabled
        if (!useTouchControls) {
            showOrHideCursor (state);
        }

        //pause game
        if (state) {
            alpha.a = 0.5f;

            //fade a little to black an UI panel
            blackBottomImage.enabled = true;
            blackBottomImage.color = alpha;

            //disable the event triggers in the touch buttons
            editControlPositionManager.changeButtonsState (false);
        }

        //resume game
        if (!state) {
            alpha.a = 0;

            //fade to transparent the UI panel
            blackBottomImage.enabled = false;
            blackBottomImage.color = alpha;

            //enable the event triggers in the touch buttons
            editControlPositionManager.changeButtonsState (true);
        }

        fadeScreenActive = false;

        externalFadeScreenActive = false;

        if (useBlurUIPanelOnPause) {
            changeBlurUIPanelValue (state, pauseMenuParent, useBlurUIPanelOnPause);
        }

        playerControllerManager.setGamePausedState (state);

        mainWeaponsManager.setGamePausedState (state);

        if (isUsingGamepad ()) {
            showOrHideMouseCursorController (playerInput, state, true);
        } else {
            showOrHideMouseCursorController (state, true);
        }
    }

    //save the previous and the current visibility of the cursor, to enable the mouse cursor correctly when the user enables the touch controls, or using a device
    //or editing the powers, or open the menu, or any action that enable and disable the mouse cursor
    void setCurrentCursorState (bool curVisible)
    {
        cursorState.currentVisible = curVisible;
    }

    void setPreviousCursorState (bool prevVisible)
    {
        cursorState.previousVisible = prevVisible;
    }

    //like the mouse, save the state of the camera, to prevent rotate it when a menu is enabled, or using a device, or the player is dead, etc...
    void setCurrentCameraState (bool curCamera)
    {
        cursorState.currentCameraEnabled = curCamera;
    }

    void setPreviousCameraState (bool prevCamera)
    {
        cursorState.previousCameraEnabled = prevCamera;
    }

    public bool inGameWindowOpened ()
    {
        if ((!usingDevice || (playerControllerManager.isPlayerDriving () && !playerMenuActive)) && !usingPointAndClick) {

            for (int i = 0; i < ingameMenuInfoList.Count; i++) {
                if (ingameMenuInfoList [i].menuOpened) {
                    return false;
                }
            }

            return true;
        }

        return false;
    }

    public ingameMenuPanel getIngameMenuPanelByName (string menueName)
    {
        for (int i = 0; i < ingameMenuInfoList.Count; i++) {
            if (ingameMenuInfoList [i].Name.Equals (menueName)) {
                if (ingameMenuInfoList [i].mainIngameMenuPanelAssigned) {
                    return ingameMenuInfoList [i].mainIngameMenuPanel;
                }
            }
        }

        return null;
    }

    public void setIngameMenuOpenedState (string ingameMenuName, bool state, bool activateEvents)
    {
        int ingameMenuInfoListCount = ingameMenuInfoList.Count;

        currentIngameMenuIndex = -1;

        setMenuUsingUINavigationActiveState (false);

        for (int i = 0; i < ingameMenuInfoListCount; i++) {
            ingameMenuInfo currentIngameMenuInfo = ingameMenuInfoList [i];

            if (currentIngameMenuInfo.Name.Equals (ingameMenuName)) {

                currentIngameMenuIndex = i;

                currentIngameMenuInfo.menuOpened = state;

                if (activateEvents && currentIngameMenuInfo.useEventOnOpenClose) {

                    if (state) {
                        currentIngameMenuInfo.eventOnOpen.Invoke ();
                    } else {
                        currentIngameMenuInfo.eventOnClose.Invoke ();
                    }

                    checkEventOnOpenCloseInGameMenu (state);
                }

                fadeInGameMenuPanel (currentIngameMenuInfo, !state);

                if (currentIngameMenuInfo.setCustomTimeScale) {
                    if (state) {
                        setTimeScale (currentIngameMenuInfo.customTimeScale);
                    } else {
                        setTimeScale (1);
                    }
                }

                if (currentIngameMenuInfo.pauseAIWhenOpenMenu) {
                    GKC_Utils.pauseOrResumeAIOnScene (state, currentIngameMenuInfo.pauseCharacterPriority);
                }

                if (currentIngameMenuInfo.setNewMouseCursorControllerSpeed) {
                    if (state) {
                        setMouseCursorControllerSpeedOnGameValue (currentIngameMenuInfo.newMouseCursroControllerSpeed);
                    } else {
                        setOriginalMouseCursorControllerSpeedOnGameValue ();
                    }
                }
            } else {
                if (currentIngameMenuInfo.mainIngameMenuPanelAssigned) {
                    currentIngameMenuInfo.mainIngameMenuPanel.checkMenuIfNotCurrentOpened ();
                }
            }
        }

        if (state) {
            if (currentIngameMenuIndex > -1) {
                ingameMenuInfo currentIngameMenuInfo = ingameMenuInfoList [currentIngameMenuIndex];

                if (currentIngameMenuInfo.mainIngameMenuPanelAssigned) {
                    currentIngameMenuInfo.mainIngameMenuPanel.checkMenuIfCurrentOpened ();
                }
            }
        } else {
            for (int i = 0; i < ingameMenuInfoListCount; i++) {
                ingameMenuInfo currentIngameMenuInfo = ingameMenuInfoList [i];

                if (currentIngameMenuInfo.mainIngameMenuPanelAssigned) {
                    currentIngameMenuInfo.mainIngameMenuPanel.checkAllMenusClosed ();
                }
            }
        }

        if (!ignoreSelectUIOnGamepadOnUINavigation && mainUIRouterLocated && currentIngameMenuIndex > -1) {
            ingameMenuInfo currentIngameMenuInfo = ingameMenuInfoList [currentIngameMenuIndex];

            bool useUINavigationResult = false;

            if (isUsingGamepad ()) {
                if (isUIRouterEnabled ()) {
                    if (currentIngameMenuInfo.mainUIPage != null && !currentIngameMenuInfo.useCursorOnMenu) {
                        useUINavigationResult = true;
                    }
                }
            }

            if (useUINavigationResult) {
                if (state) {
                    mainUIRouter.Push (currentIngameMenuInfo.mainUIPage);
                } else {
                    mainUIRouter.Pop (currentIngameMenuInfo.mainUIPage);
                }

                currentIngameMenuInfo.menuUsingUINavigationActive = state;
            }

            setMenuUsingUINavigationActiveState (currentIngameMenuInfo.menuUsingUINavigationActive);
        }


        int playerMenuInfoListCount = playerMenuInfoList.Count;

        for (int i = 0; i < playerMenuInfoListCount; i++) {
            if (playerMenuInfoList [i].Name.Equals (ingameMenuName)) {
                currentPlayerMenuInfoIndex = i;

                return;
            }
        }
    }

    public void setIgnoreTimeScaleOnMenusActiveState (bool state)
    {
        ignoreTimeScaleOnMenusActive = state;
    }

    public void fadeInGameMenuPanel (ingameMenuInfo newIngameMenuInfo, bool fadingPanel)
    {
        if (!useCanvasGroup) {
            return;
        }

        if (newIngameMenuInfo.useMenuCanvasGroup && newIngameMenuInfo.menuCanvasGroup) {
            if (newIngameMenuInfo.canvasGroupCoroutine != null) {
                StopCoroutine (newIngameMenuInfo.canvasGroupCoroutine);
            }

            newIngameMenuInfo.canvasGroupCoroutine = StartCoroutine (fadeInGameMenuPanelCoroutine (newIngameMenuInfo, fadingPanel));
        }
    }

    IEnumerator fadeInGameMenuPanelCoroutine (ingameMenuInfo newIngameMenuInfo, bool fadingPanel)
    {
        float targetValue = 1;

        float fadeSpeed = fadeOffPanelSpeed;

        if (fadingPanel) {
            targetValue = 0;

            fadeSpeed = fadeOnPanelSpeed;
        }

        if (fadingPanel) {
            if (newIngameMenuInfo.menuCanvasGroup.alpha > 0) {
                newIngameMenuInfo.menuPanelGameObject.SetActive (true);
            }
        } else {
            if (newIngameMenuInfo.menuCanvasGroup.alpha == 1) {
                newIngameMenuInfo.menuCanvasGroup.alpha = 0;
            }
        }

        while (newIngameMenuInfo.menuCanvasGroup.alpha != targetValue) {
            newIngameMenuInfo.menuCanvasGroup.alpha = Mathf.MoveTowards (newIngameMenuInfo.menuCanvasGroup.alpha, targetValue, getDeltaTime () * fadeSpeed);

            yield return null;
        }

        if (fadingPanel) {
            newIngameMenuInfo.menuPanelGameObject.SetActive (false);
        }
    }

    public void checkTouchControlsWithoutDisableThem (bool state)
    {
        if (!state) {
            usingTouchControlsPreviously = useTouchControls;
        }

        if (usingTouchControlsPreviously) {
            playerInput.changeControlsType (state);

            if (state && usingTouchControlsPreviously) {
                showOrHideCursor (true);
            }
        }
    }

    public void enableOrDisableTouchControlsExternally (bool state)
    {
        playerInput.enableOrDisableTouchControls (state);
    }

    public void enableOrDisablePlayerMenu (bool state, bool pausePlayerMovement, bool ignoreActionActiveCheck)
    {
        checkLockCursorBetweenMenus = true;

        bool currentIngameMenuLocated = false;

        ingameMenuInfo currentIngameMenuInfo = null;

        if (currentIngameMenuIndex > -1 && currentIngameMenuIndex < ingameMenuInfoList.Count) {
            currentIngameMenuInfo = ingameMenuInfoList [currentIngameMenuIndex];

            currentIngameMenuLocated = true;
        }

        //set to visible the cursor
        showOrHideCursor (state);

        //disable the touch controls
        checkTouchControlsWithoutDisableThem (!state);

        //disable the camera rotation
        changeCameraState (!state);

        if (!playerControllerManager.isPlayerDriving ()) {
            if (pausePlayerMovement) {

                bool setCanMoveStateResult = true;

                if (playerControllerManager.isActionActive () && !ignoreActionActiveCheck) {
                    if (state) {
                        setCanMoveStateResult = false;

                        playerControllerManager.setDisableChangeScriptStateAfterFinishActionState (true);

                        playerControllerManager.setEnableChangeScriptStateAfterFinishActionState (false);
                    } else {
                        if (!playerControllerManager.canPlayerMove ()) {
                            setCanMoveStateResult = false;

                            playerControllerManager.setDisableChangeScriptStateAfterFinishActionState (false);

                            playerControllerManager.setEnableChangeScriptStateAfterFinishActionState (true);
                        }
                    }
                }

                if (setCanMoveStateResult) {
                    playerControllerManager.setEnableChangeScriptStateAfterFinishActionState (false);
                    playerControllerManager.setDisableChangeScriptStateAfterFinishActionState (false);

                    playerControllerManager.setCanMoveState (!state);
                    playerControllerManager.resetPlayerControllerInput ();
                    playerControllerManager.resetOtherInputFields ();
                }
            }
        }

        usingSubMenuState (state);

        if (!ignoreChangeHUDElements) {
            enableOrDisableDynamicElementsOnScreen (!state);

            playerInput.enableOrDisableScreenActionParent (!state);
        }

        checkEnableOrDisableTouchZoneList (!state);

        if (isUsingGamepad ()) {
            showOrHideMouseCursorController (playerInput, state, false);
        } else {
            showOrHideMouseCursorController (state);
        }

        playerInput.setUsingPlayerMenuState (state);

        if (currentIngameMenuLocated && !ignoreSelectUIOnGamepadOnUINavigation) {
            bool useUINavigationResult = false;

            bool hideCursorResult = false;

            if (isUsingGamepad ()) {
                if (mainUIRouterLocated && isUIRouterEnabled ()) {
                    if (currentIngameMenuInfo.mainUIPage != null && !currentIngameMenuInfo.useCursorOnMenu) {
                        useUINavigationResult = true;

                        hideCursorResult = true;
                    }
                }

                //print ("hideCursorResult " + hideCursorResult);

                if (hideCursorResult && state) {

                    bool lockCursorResult = true;

                    if (isUsingGamepad () && useTouchControls && input.isAllowGamepadInTouchDevice () && !input.isTouchPlatform ()) {
                        lockCursorResult = false;
                    }

                    if (lockCursorResult) {
                        setCursorLockState (true);

                        setCursorVisibleState (false);
                    }
                }

                //print ("useUINavigationResult " + useUINavigationResult);

                if (useUINavigationResult) {
                    //move the above check here in case of the pop is needed
                    if (state) {

                        mouseCursorControllerManager.setUINavigationCurrentlyActive (true);

                        //mouseCursorControllerManager.moveCursorToCornerNextVisibleTime ();

                        //playerInput.moveCursorToCornerNextVisibleTime ();

                        playerInput.setUINavigationCurrentlyActive (true);

                        if (currentIngameMenuInfo.refreshItemListOnOpenMenu) {
                            checkRefreshItemListOnCurrentContextOnUINavigation ();
                        }
                    } else {
                        mouseCursorControllerManager.setUINavigationCurrentlyActive (false);

                        playerInput.setUINavigationCurrentlyActive (false);
                    }
                }
            }
        }
    }

    public bool isUIRouterEnabled ()
    {
        if (!mainUIRouterLocated) {
            return false;
        }

        return mainUIRouter.isUIRouterEnabled ();
    }

    public void checkRefreshItemListOnCurrentContextOnUINavigation ()
    {
        if (mainUIRouterLocated && isUIRouterEnabled ()) {
            mainUIRouter.checkRefreshItemListOnCurrentContext ();
        }
    }

    public void setObjectAsCurrentSelectionExternally (GameObject objectToSelect)
    {
        if (mainUIRouterLocated && isUIRouterEnabled () && playerInput.isUINavigationCurrentlyActive ()) {
            mainUIRouter.setObjectAsCurrentSelectionExternally (objectToSelect);
        }
    }

    public GameObject getCurrentSelection ()
    {
        if (mainUIRouterLocated && isUIRouterEnabled ()) {
            return mainUIRouter.getCurrentSelection ();
        }

        return null;
    }

    public void setUINavigationMoveInputPausedState (bool state)
    {
        if (mainUIRouterLocated && isUIRouterEnabled ()) {
            mainUIRouter.setUINavigationMoveInputPausedState (state);
        }
    }

    public void registerNewItemOnUIPage (GameObject newObject, UIPage UIPageToUse)
    {
        if (mainUIRouterLocated && isUIRouterEnabled ()) {
            mainUIRouter.registerNewItemOnUIPage (newObject, UIPageToUse);
        }
    }

    //UI ROUTER FOR UI NAVIGATION FUNCTIONS
    public void pushNewUIPageOnCurrentIngamePanel (UIPage newUIPage)
    {
        if (newUIPage == null) {
            return;
        }

        setNewUIPageOnCurrentIngamePanel (newUIPage, true, false, false, false);
    }

    public void replaceNewUIPageOnCurrentIngamePanel (UIPage newUIPage)
    {
        if (newUIPage == null) {
            return;
        }

        setNewUIPageOnCurrentIngamePanel (newUIPage, false, false, false, false);
    }

    public void popUIPageOnCurrentIngamePanel ()
    {
        setNewUIPageOnCurrentIngamePanel (null, false, true, false, false);
    }

    public void popUIPageIfCurrentOnCurrentIngamePanel (UIPage newUIPage)
    {
        if (newUIPage == null) {
            return;
        }

        setNewUIPageOnCurrentIngamePanel (newUIPage, false, true, true, false);
    }

    public void popUIPageOnCurrentIngamePanel (UIPage newUIPage)
    {
        if (newUIPage == null) {
            return;
        }

        setNewUIPageOnCurrentIngamePanel (newUIPage, false, true, false, false);
    }

    public void pushNewUIPage (UIPage newUIPage)
    {
        if (newUIPage == null) {
            return;
        }

        setNewUIPageOnCurrentIngamePanel (newUIPage, true, false, false, true);
    }

    public void popUIPageIfCurrentOne (UIPage newUIPage)
    {
        if (newUIPage == null) {
            return;
        }

        setNewUIPageOnCurrentIngamePanel (newUIPage, false, true, true, true);
    }

    void setNewUIPageOnCurrentIngamePanel (UIPage newUIPage, bool usePush, bool usePop, bool usePopIfCurrent,
        bool ignoreIngameMenusCheck)
    {
        if (!ignoreIngameMenusCheck) {
            if (!usingSubMenu && !usePop) {
                return;
            }
        }

        if (!mainUIRouterLocated || !isUIRouterEnabled ()) {
            return;
        }

        ingameMenuInfo currentIngameMenuInfo = null;

        if (!ignoreIngameMenusCheck) {
            if (currentIngameMenuIndex > -1 && currentIngameMenuIndex < ingameMenuInfoList.Count) {
                currentIngameMenuInfo = ingameMenuInfoList [currentIngameMenuIndex];
            } else {
                return;
            }

            if (!currentIngameMenuInfo.menuOpened || !currentIngameMenuInfo.menuUsingUINavigationActive) {
                if (!usePop) {
                    return;
                }
            }
        }

        bool useUINavigationResult = false;

        if (isUsingGamepad ()) {
            if (ignoreIngameMenusCheck) {
                useUINavigationResult = true;
            } else {
                if (currentIngameMenuInfo.mainUIPage != null && !currentIngameMenuInfo.useCursorOnMenu) {
                    useUINavigationResult = true;
                }
            }
        }

        if (useUINavigationResult) {
            if (usePop) {
                if (usePopIfCurrent) {
                    mainUIRouter.PopIfCurrent (newUIPage);
                } else {
                    if (newUIPage != null) {
                        mainUIRouter.Pop (newUIPage);
                    } else {
                        mainUIRouter.PopTopPage ();
                    }
                }
            } else {
                if (usePush) {
                    mainUIRouter.Push (newUIPage);
                } else {
                    mainUIRouter.Replace (newUIPage);
                }
            }
        }
    }

    void checkAnyRemainingUIPageOnRouter ()
    {
        if (ignoreSelectUIOnGamepadOnUINavigation) {
            return;
        }

        if (!mainUIRouterLocated) {
            return;
        }

        if (!isUIRouterEnabled ()) {
            return;
        }

        if (!isUsingGamepad ()) {
            return;
        }

        bool useUINavigationResult = false;

        if (mainUIRouter.HasPages ()) {
            if (mainUIRouter.RefreshCurrentPage ()) {
                useUINavigationResult = true;
            }
        }

        if (useUINavigationResult) {
            print ("checkAnyRemainingUIPageOnRouter UI page found, resuming that state to keep navigation active");

            showOrHideMouseCursorController (playerInput, true, true);

            setCursorLockState (true);

            setCursorVisibleState (false);

            if (!mouseCursorControllerManager.isUINavigationCurrentlyActive ()) {
                mouseCursorControllerManager.setUINavigationCurrentlyActive (true);
            }

            playerInput.setUINavigationCurrentlyActive (true);

            setMenuUsingUINavigationActiveState (useUINavigationResult);
        }
    }

    public void selectNavigationZonePanelByNameOnCurrentContextOnUINavigation (string zoneName)
    {
        if (mainUIRouterLocated && isUIRouterEnabled ()) {
            mainUIRouter.selectNavigationZonePanelByName (zoneName);
        }
    }

    public void checkEnableOrDisableTouchZoneList (bool state)
    {
        if (!ignoreDisableTouchZoneList || state) {
            for (int i = 0; i < touchZoneList.Count; i++) {
                if (touchZoneList [i] != null) {
                    if (touchZoneList [i].activeSelf != state) {
                        touchZoneList [i].SetActive (state);
                    }
                }
            }
        }
    }

    public void setIgnoreDisableTouchZoneListState (bool state)
    {
        ignoreDisableTouchZoneList = state;
    }

    //set in the player is using a device like a computer or a text device
    public void usingDeviceState (bool state)
    {
        usingDevice = state;
    }

    public void usingPointAndClickState (bool state)
    {
        usingPointAndClick = state;
    }

    public void usingSubMenuState (bool state)
    {
        usingSubMenu = state;

        playerControllerManager.setUsingSubMenuState (usingSubMenu);

        playerInput.setGamePausedStateOnGamepadByID (state);
    }

    public void pauseGame ()
    {
        if (!subMenuActive) {
            //if the main pause menu is the current place, resuem the game

            pause ();

            return;
        } else {
            if (!dead) {
                //else, the current menu place is a submenu, so disable all the submenus and set the main menu window
                openOrClosePauseMenuByName (mainPauseMenuName, true);
            }
        }

        //disable the edition of the touch button position if the player backs from that menu option
        editControlPositionManager.disableEdit ();

        if (input.isEditingInput ()) {
            input.cancelEditingInput ();
        }
    }

    bool pauseCalledFromGameManager;

    public void setMenuPausedState (bool state)
    {
        if (transform.parent.gameObject.activeSelf) {
            gamePaused = !state;

            pauseCalledFromGameManager = true;

            pause ();
        }
    }

    public void pause ()
    {
        //check if the game is going to be paused or resumed
        if (!dead && menuPauseEnabled) {
            //if the player pauses the game and he is editing the powers or selecting them, disable the power manager menu

            bool playerMenuActivePreviously = playerMenuActive;

            for (int i = 0; i < ingameMenuInfoList.Count; i++) {
                if (ingameMenuInfoList [i].menuOpened) {
                    bool closeMenuResult = ingameMenuInfoList [i].closeMenuIfGamePaused;

                    if (closeMenuResult) {
                        //print (ingameMenuInfoList [i].Name + " " + ingameMenuInfoList [i].mainIngameMenuPanelAssigned);

                        ingameMenuInfoList [i].closeMenuEvent.Invoke ();

                        if (ingameMenuInfoList [i].mainIngameMenuPanelAssigned) {
                            ingameMenuInfoList [i].mainIngameMenuPanel.closeMenuFromEscapeInput ();
                        }
                    }
                }
            }

            if (playerMenuActivePreviously && closeOnlySubMenuIfOpenOnEscape) {
                return;
            }

            gamePaused = !gamePaused;

            mainGameManager.setGamePauseState (gamePaused);

            if (!pauseCalledFromGameManager) {
                mainGameManager.setCharactersManagerPauseState (gamePaused);
            }

            input.setGamePausedStateOnAllGamepads (gamePaused);

            if (useTouchControls) {
                playerInput.setGamePausedStateOnGamepadByID (gamePaused);
            }

            showGUI = !showGUI;

            if (pauseAudioListenerOnGamePaused) {
                AudioListener.pause = gamePaused;
            }

            //enable or disable the main pause menu
            openOrClosePauseMenuByName (mainPauseMenuName, gamePaused);

            //change the camera state
            changeCameraState (!gamePaused);

            //check if the touch controls were enabled
            if (!useTouchControls) {
                showOrHideCursor (gamePaused);
            }

            input.setPauseState (gamePaused);

            //pause game
            if (gamePaused) {
                timeManager.disableTimeBullet ();

                setTimeScale (timeScaleOnGamePaused);

                alpha.a = 0.5f;

                //fade a little to black an UI panel
                blackBottomImage.enabled = true;
                blackBottomImage.color = alpha;

                //disable the event triggers in the touch buttons

                if (useTouchControls) {
                    editControlPositionManager.changeButtonsState (false);
                }
            }

            //resume game
            if (!gamePaused) {
                setTimeScale (1);

                alpha.a = 0;

                //fade to transparent the UI panel
                blackBottomImage.enabled = false;
                blackBottomImage.color = alpha;

                //enable the event triggers in the touch buttons
                if (useTouchControls) {
                    editControlPositionManager.changeButtonsState (true);
                }

                timeManager.reActivateTime ();
            }

            fadeScreenActive = false;

            externalFadeScreenActive = false;

            if (useBlurUIPanelOnPause) {
                changeBlurUIPanelValue (gamePaused, pauseMenuParent, useBlurUIPanelOnPause);
            }

            playerControllerManager.setGamePausedState (gamePaused);

            if (timeScaleOnGamePaused > 0) {
                playerControllerManager.setCanMoveState (!gamePaused);
                playerControllerManager.resetPlayerControllerInput ();
            }

            mainWeaponsManager.setGamePausedState (gamePaused);

            if (isUsingGamepad ()) {
                if (gamePaused) {
                    if (ignoreSetSelectedGameObjectOnUIByGamepad) {
                        mouseCursorControllerManager.setIgnoreSetSelectedGameObjectOnUIByGamepadState (true);

                        ignoreSetSelectedGameObjectOnUIByGamepad = false;

                        bool lockCursorResult = true;

                        if (useTouchControls && input.isAllowGamepadInTouchDevice () && !input.isTouchPlatform ()) {
                            lockCursorResult = false;
                        }

                        if (lockCursorResult) {
                            setCursorLockState (true);

                            setCursorVisibleState (false);
                        }
                    }
                } else {
                    playerInput.setPlayerInputEnabledStateWithDelay (false);

                    playerInput.setPlayerInputEnabledStateWithDelay (true);
                }

                showOrHideMouseCursorController (playerInput, gamePaused, true);
            } else {
                showOrHideMouseCursorController (gamePaused, true);
            }

            if (!gamePaused) {
                pauseCalledFromGameManager = false;
            }

            //print ("final " + transform.parent.name);

            checkEventOnPauseResumeGame (gamePaused);

            if (pauseAIWhenPauseMenuOpened) {
                GKC_Utils.pauseOrResumeAIOnScene (gamePaused, pauseCharacterPriority);
            }

            if (!gamePaused) {
                if (isUsingGamepad ()) {
                    if (showDebugPrint) {
                        print ("check for any remaining UI page active ");
                    }

                    checkAnyRemainingUIPageOnRouter ();
                }
            }
        }
    }

    public void checkEventOnPauseResumeGame (bool state)
    {
        if (useEventsOnPauseResume) {
            if (state) {
                eventOnPauseGame.Invoke ();
            } else {
                eventOnResumeGame.Invoke ();
            }
        }
    }

    public void checkEventOnOpenCloseInGameMenu (bool state)
    {
        if (useEventOnOpenCloseIngameMenu) {
            if (state) {
                eventOnOpenIngameMenu.Invoke ();
            } else {
                eventOnCloseIngameMenu.Invoke ();
            }
        }
    }

    public void setTimeScale (float newValue)
    {
        if (ignoreTimeScaleOnMenusActive) {
            return;
        }

        Time.timeScale = newValue;

        if (!ignoreChangeFixedDeltaTime) {
            if (newValue != 0) {
                Time.fixedDeltaTime = newValue * 0.02f;
            }
        }

        regularTimeScaleActive = newValue == 1;
    }

    public float getTimeTime ()
    {
        if (regularTimeScaleActive) {
            return Time.time;
        }

        return Time.unscaledTime;
    }

    public float getDeltaTime ()
    {
        if (regularTimeScaleActive) {
            return Time.deltaTime;
        }

        return Time.unscaledDeltaTime;
    }

    public bool isRegularTimeScaleActive ()
    {
        return regularTimeScaleActive;
    }

    public void setMouseButtonPressPausedState (bool state)
    {
        mouseButtonPressPaused = state;
    }

    public void showOrHideMouseCursorController (bool state)
    {
        mouseCursorControllerManager.showOrHideMouseController (state, false);

        checkIfUseVirtualMouseCursorController (state);

        checkEventOnShowHideCursor (state);
    }

    void showOrHideMouseCursorController (bool state, bool pausingGame)
    {
        mouseCursorControllerManager.showOrHideMouseController (state, pausingGame);

        checkIfUseVirtualMouseCursorController (state);

        checkEventOnShowHideCursor (state);
    }

    void showOrHideMouseCursorController (playerInputManager newInput, bool state, bool pausingGame)
    {
        mouseCursorControllerManager.showOrHideMouseController (newInput, state, playerInput.getPlayerID (), pausingGame);

        checkIfUseVirtualMouseCursorController (state);

        checkEventOnShowHideCursor (state);
    }

    void checkIfUseVirtualMouseCursorController (bool state)
    {
        if (mainVirtualCursorControllerSystem != null && mainVirtualCursorControllerSystem.isVirtualCursorControllerEnabled ()) {
            if (isUsingGamepad ()) {
                mouseCursorControllerManager.setVirtualCursorCurrentlyActive (state);
            }

            mainVirtualCursorControllerSystem.setCursorActiveState (state);
        }
    }

    void checkEventOnShowHideCursor (bool state)
    {
        if (useEventOnShowCursor) {
            if (state) {
                eventOnShowCursor.Invoke ();
            } else {
                eventOnHideCursor.Invoke ();
            }
        }
    }


    public void setMouseCursorControllerSpeedOnGameValue (float newValue)
    {
        mouseCursorControllerManager.setMouseCursorControllerSpeedOnGameValue (newValue);
    }

    public void setOriginalMouseCursorControllerSpeedOnGameValue ()
    {
        mouseCursorControllerManager.setOriginalMouseCursorControllerSpeedOnGameValue ();
    }

    public void setCursorSpeedValue (float newValue)
    {
        mouseCursorControllerManager.setCursorSpeedValue (newValue);
    }

    //set the state of the cursor, according to if the touch controls are enabled, if the game is pause, if the powers manager menu is enabled, etc...
    //so the cursor is always locked and not visible correctly and vice versa
    public void showOrHideCursor (bool value)
    {
        if (cursorState.currentVisible && cursorState.previousVisible) {
            setPreviousCursorState (false);
            setCurrentCursorState (true);

            return;
        }

        if (cursorState.currentVisible) {
            bool exitCursorCheckResult = false;

            if (useTouchControls) {
                if (input.isTouchPlatform ()) {
                    exitCursorCheckResult = true;
                } else {
                    if (!isUsingGamepad () || !input.isAllowGamepadInTouchDevice ()) {
                        exitCursorCheckResult = true;
                    }
                }
            }

            if (exitCursorCheckResult) {
                setPreviousCursorState (false);
                setCurrentCursorState (true);

                return;
            }
        }

        if (value) {
            setCursorLockState (false);
        } else {
            if (!checkLockCursorBetweenMenus || getTimeTime () < lastTimeCursorDisabled + timeToResetCursorAfterDisable) {
                setCursorLockState (true);
            }
        }

        checkLockCursorBetweenMenus = false;

        lastTimeCursorDisabled = getTimeTime ();

        setPreviousCursorState (Cursor.visible);

        setCursorVisibleState (value);

        setCurrentCursorState (Cursor.visible);

        if (showDebugPrint) {
            print ("show or hide cursor state: " + value + " " + Cursor.visible + " " + Cursor.lockState);
        }
    }

    public static void setCursorLockState (bool state)
    {
        if (state) {
            Cursor.lockState = CursorLockMode.Confined;
            Cursor.lockState = CursorLockMode.Locked;
        } else {
            Cursor.lockState = CursorLockMode.None;
        }

        if (showCursorStateChangeDebugPrintValue) {
            print ("Set Cursor Lock State " + state);
        }
    }

    public static void setCursorVisibleState (bool state)
    {
        Cursor.visible = state;

        if (showCursorStateChangeDebugPrintValue) {
            print ("Set Cursor Visible State " + state);
        }
    }

    public static bool isCursorVisible ()
    {
        return Cursor.visible;
    }

    public void setIgnoreHideCursorOnClickActiveState (bool state)
    {
        ignoreHideCursorOnClickActive = state;
    }

    public void showOrHideMouseCursorAndPauseHideCheckOnClickExternally (bool state)
    {
        if (ignoreShowOrHideMouseCursorAndPauseHideCheckOnClickExternally) {
            return;
        }

        showOrHideCursor (state);

        showOrHideMouseCursorController (state);

        setIgnoreHideCursorOnClickActiveState (state);
    }

    //check if the touch controls have to be enable and disable and change the cursor visibility according to that
    public void checkTouchControls (bool state)
    {
        if (!state) {
            usingTouchControlsPreviously = useTouchControls;
        }

        if (usingTouchControlsPreviously) {
            enableOrDisableTouchControls (state);

            if (state && usingTouchControlsPreviously) {
                showOrHideCursor (true);
            }
        }
    }

    public void updateTouchButtonsComponents ()
    {
        editControlPositionManager.getTouchButtonsComponents ();
    }

    bool ignoreCheckCloseIngameMenusIfOpened;

    public void setIgnoreCheckCloseIngameMenusIfOpenedState (bool state)
    {
        ignoreCheckCloseIngameMenusIfOpened = state;
    }

    public void checkCloseIngameMenusIfOpened ()
    {
        if (ignoreCheckCloseIngameMenusIfOpened) {
            return;
        }

        if (playerMenuActive) {
            for (int i = 0; i < ingameMenuInfoList.Count; i++) {
                if (ingameMenuInfoList [i].menuOpened) {
                    ingameMenuInfoList [i].closeMenuEvent.Invoke ();

                    if (ingameMenuInfoList [i].mainIngameMenuPanelAssigned) {
                        ingameMenuInfoList [i].mainIngameMenuPanel.closeMenuFromEscapeInput ();
                    }
                }
            }
        }
    }

    public void setMenuOpenedWithActionSystemState (bool state, string menuName)
    {
        menuOpenedWithActionSystemActive = state;

        menuOpenedWithActionSystemName = menuName;
    }

    public void checkCloseMenuOpenedByActionSystem ()
    {
        if (playerMenuActive && menuOpenedWithActionSystemActive) {
            if (playerControllerManager.isActionActive ()) {
                playerControllerManager.playCurrentAnimationOnPlayerActionSystem ();
            }
        }
    }

    bool ignoreDeathStateOnMenuPauseState;

    public void setIgnoreDeathStateOnMenuPauseState (bool state)
    {
        ignoreDeathStateOnMenuPauseState = state;
    }

    //the player dies, so enable the death menu to ask the player to play again
    public void death ()
    {
        if (ignoreDeathStateOnMenuPauseState) {
            return;
        }

        dead = true;

        if (activateDeathMenuWhenPlayeDies) {
            openOrClosePauseMenuByName (mainDeathMenuName, true);

            showOrHideCursor (true);
        }

        changeCameraState (false);

        if (isUsingGamepad ()) {
            if (ignoreSetSelectedGameObjectOnUIByGamepad) {
                mouseCursorControllerManager.setIgnoreSetSelectedGameObjectOnUIByGamepadState (true);

                ignoreSetSelectedGameObjectOnUIByGamepad = false;

                bool lockCursorResult = true;

                if (useTouchControls && input.isAllowGamepadInTouchDevice () && !input.isTouchPlatform ()) {
                    lockCursorResult = false;
                }

                if (lockCursorResult) {
                    setCursorLockState (true);

                    setCursorVisibleState (false);
                }
            }

            showOrHideMouseCursorController (playerInput, true, false);
        } else {
            showOrHideMouseCursorController (true);
        }
    }

    //the player chooses to play again
    public void getUp ()
    {
        dead = false;

        openOrClosePauseMenuByName (mainDeathMenuName, false);

        showOrHideMouseCursorController (false);

        if (!useTouchControls) {
            showOrHideCursor (false);
        }

        changeCameraState (true);
    }

    //restart the scene
    public void restart ()
    {
        pause ();

        int sceneIndexToUse = 0;

        if (useCustomSceneIndexOnRestartGame) {
            sceneIndexToUse = customSceneIndexOnRestartGame;
        } else {
            sceneIndexToUse = SceneManager.GetActiveScene ().buildIndex;
        }

        GKC_Utils.loadScene (sceneIndexToUse, useLoadScreen, loadScreenScene, sceneToLoadAsyncPrefsName,
            useLastSceneIndexAsLoadScreen, checkLoadingScreenSceneConfigured, loadingScreenSceneName);
    }

    public static UnityEngine.SceneManagement.Scene getCurrentActiveScene ()
    {
        return SceneManager.GetActiveScene ();
    }


    public static int getCurrentActiveSceneIndex ()
    {
        return SceneManager.GetActiveScene ().buildIndex;
    }

    //change the camera state according to if the player pauses the game or uses a device, etc... so the camera is enabled correctly according to every situation
    public void changeCameraState (bool state)
    {
        if (playerCameraManager != null) {
            //if the player paused the game using a device, then resume again with the camera disable to keep using that device
            if (!cursorState.currentCameraEnabled && !cursorState.previousCameraEnabled) {
                setPreviousCameraState (true);
            }
            //else save the current and previous state of the camera and set the state of the camera according to the current situation
            else {
                setPreviousCameraState (playerCameraManager.cameraCanBeUsed);
                playerCameraManager.pauseOrPlayCamera (state);
                setCurrentCameraState (playerCameraManager.cameraCanBeUsed);
            }
        }
    }

    public void changeCursorState (bool state)
    {
        if (playerCameraManager != null) {
            //if the player paused the game using a device, then resume again with the camera disable to keep using that device
            if (!cursorState.currentCameraEnabled && !cursorState.previousCameraEnabled) {
                setPreviousCameraState (true);
            }
            //else save the current and previous state of the camera and set the state of the camera according to the current situation
            else {
                setPreviousCameraState (state);
                setCurrentCameraState (!state);
            }
        }
    }

    public void openOrClosePlayerMenu (bool state, Transform blurUIParent, bool useBlurUIPanel)
    {
        playerMenuActive = state;

        playerControllerManager.setPlayerMenuActiveState (playerMenuActive);

        playerControllerManager.setLastTimeMoved ();

        mainWeaponsManager.setLastTimeMoved ();

        if (useBlurUIPanelOnPlayerMenu && useBlurUIPanel) {
            changeBlurUIPanelValue (playerMenuActive, blurUIParent, useBlurUIPanel);
        }

        lastTimeIngameMenuOpened = Time.unscaledTime;
    }

    public void openPlayerMenuWithBlurPanel (Transform blurUIParent)
    {
        playerControllerManager.setLastTimeMoved ();

        if (useBlurUIPanelOnPlayerMenu) {
            changeBlurUIPanelValue (playerMenuActive, blurUIParent, true);
        }
    }

    //the player is in a submenu, so disable the main menu
    public void enterSubMenu ()
    {
        showGUI = false;
        subMenuActive = true;
    }

    //the player backs from a submenu, so enable the main menu
    public void exitSubMenu ()
    {
        showGUI = true;
        subMenuActive = false;
    }

    //switch between touch controls and the keyboard
    public void switchControls ()
    {
        setUseTouchControlsState (!useTouchControls);

        enableOrDisableTouchControls (useTouchControls);

        pause ();

        mainGameManager.setUseTouchControlsState (useTouchControls);

        input.setUseTouchControlsState (useTouchControls);
    }

    public void enableOrDisableTouchControlsOutOfPauseMenu (bool state)
    {
        setUseTouchControlsState (state);

        enableOrDisableTouchControls (useTouchControls);

        mainGameManager.setUseTouchControlsState (useTouchControls);

        input.setUseTouchControlsState (useTouchControls);
    }

    public void setUseTouchControlsState (bool state)
    {
        useTouchControls = state;

        //print ("setUseTouchControlsState " + state);

        //print (input.isAllowGamepadInTouchDevice () + " " + GKC_Utils.isApplicationPlaying () + " " + isUsingGamepad () + " " + !input.isTouchPlatform ());

        if (input.isAllowGamepadInTouchDevice () && GKC_Utils.isApplicationPlaying () && isUsingGamepad () && !input.isTouchPlatform ()) {
            if (useTouchControls) {
                showOrHideCursor (true);
            } else {
                showOrHideCursor (false);
            }
        }
    }

    public void setUseTouchControlsStateFromEditor (bool state)
    {
        useTouchControls = state;

        updateComponent ();
    }

    public void setBlurPanelEnabledState (bool state)
    {
        blurPanelEnabled = state;
    }

    public void setBlurPanelEnabledStateFromEditor (bool state)
    {
        setBlurPanelEnabledState (state);

        updateComponent ();
    }

    public bool isUsingTouchControls ()
    {
        return useTouchControls;
    }

    //exit from the game
    public void confirmExit ()
    {
        Application.Quit ();
    }

    public void confirmGoToHomeMenu ()
    {
        if (saveGameAutomaticallyOnReturnToHome) {
            mainGameManager.saveGameWhenReturningHomeMenu ();
        }

        if (Time.timeScale != 1) {
            setTimeScale (1);
        }

        GKC_Utils.loadScene (sceneNumberForHomeMenu, useLoadScreen, loadScreenScene, sceneToLoadAsyncPrefsName,
            useLastSceneIndexAsLoadScreen, checkLoadingScreenSceneConfigured, loadingScreenSceneName);
    }

    //enable or disable the joysticks and the touch buttons in the HUD
    public void enableOrDisableTouchControls (bool state)
    {
        input.setKeyboardControls (!state);
    }

    public void reloadStart ()
    {
        Start ();
    }

    public void setIgnoreBlurPanelActiveState (bool state)
    {
        ignoreBlurPanelActive = state;
    }

    public void resetPauseMenuBlurPanel ()
    {
        changeBlurUIPanelValue (true, pauseMenuParent, true);
    }

    public void changeBlurUIPanelValue (bool state, Transform blurUIParent, bool useBlurUIPanel)
    {
        if (!blurPanelEnabled) {
            return;
        }

        if (ignoreBlurPanelActive) {
            return;
        }

        if (!useBlurUIPanel) {
            return;
        }

        if (blurUIPanel == null || blurUIPanelParent == null) {
            return;
        }

        if (state) {
            if (blurUIParent != null) {
                blurUIPanel.transform.SetParent (blurUIParent);
                blurUIPanel.transform.SetSiblingIndex (0);
            }
        } else {
            blurUIPanel.transform.SetParent (blurUIPanelParent);
        }

        if (blurUIPanelCoroutine != null) {
            StopCoroutine (blurUIPanelCoroutine);
        }

        if (gameObject.activeInHierarchy) {
            blurUIPanelCoroutine = StartCoroutine (changeBlurUIPanelValueCoroutine (state));
        }
    }

    IEnumerator changeBlurUIPanelValueCoroutine (bool state)
    {
        blurUIPanelActive = state;

        if (blurUIPanelActive) {
            blurUIPanel.SetActive (true);
        }

        float blurUIPanelValueTarget = 0;
        float blurUIPanelAlphaTarget = 0;

        if (blurUIPanelActive) {
            blurUIPanelValueTarget = blurUIPanelValue;
            blurUIPanelAlphaTarget = blurUIPanelAlphaValue;
        }

        checkBlurSizeID ();

        float currentBlurUiPanelValue = blurUIPanelMaterial.GetFloat (blurSizeID);
        float currentBlurUIPanelAlphaValue = blurUIPanelImage.color.a * 255;
        Color currentColor = blurUIPanelImage.color;

        if (Time.timeScale > 0) {
            while (currentBlurUiPanelValue != blurUIPanelValueTarget || currentBlurUIPanelAlphaValue != blurUIPanelAlphaTarget) {
                currentBlurUiPanelValue = Mathf.MoveTowards (currentBlurUiPanelValue, blurUIPanelValueTarget, getDeltaTime () * blurUIPanelSpeed);
                blurUIPanelMaterial.SetFloat (blurSizeID, currentBlurUiPanelValue);

                currentBlurUIPanelAlphaValue = Mathf.MoveTowards (currentBlurUIPanelAlphaValue, blurUIPanelAlphaTarget, getDeltaTime () * blurUIPanelAlphaSpeed);

                currentColor.a = currentBlurUIPanelAlphaValue / 255;

                blurUIPanelImage.color = currentColor;

                yield return null;
            }
        } else {
            blurUIPanelMaterial.SetFloat (blurSizeID, blurUIPanelValueTarget);

            currentColor.a = blurUIPanelAlphaTarget / 255;

            blurUIPanelImage.color = currentColor;

            yield return null;
        }

        if (!blurUIPanelActive) {

            blurUIPanelMaterial.SetFloat (blurSizeID, 0);

            blurUIPanel.SetActive (false);
        }
    }

    public void checkBlurSizeID ()
    {
        if (blurSizeID == -1) {
            blurSizeID = Shader.PropertyToID ("_Size");
        }
    }

    public void setIgnoreChangeHUDElementsState (bool state)
    {
        ignoreChangeHUDElements = state;
    }

    public void enableOrDisableDynamicElementsOnScreen (bool state)
    {
        pauseOrResumeShowHealthSliders (!state);

        pauseOrResumeShowObjectives (!state);

        pauseOrResumeShowIcons (!state);

        enableOrDisableDynamicUIElementsParent (state);
    }

    public void pauseOrResumeShowHealthSliders (bool state)
    {
        mainPlayerHealthBarManager.pauseOrResumeShowHealthSliders (state);
    }

    public void pauseOrResumeShowObjectives (bool state)
    {
        playerScreenObjectivesManager.pauseOrResumeShowObjectives (state);
    }

    public void pauseOrResumeShowIcons (bool state)
    {
        mainPlayerPickupIconManager.pauseOrResumeShowIcons (state);
    }

    public void enableOrDisableDynamicUIElementsParent (bool state)
    {
        if (dynamicUIElementsParent.activeSelf != state) {
            dynamicUIElementsParent.SetActive (state);
        }
    }

    public void enableOrDisableDynamicElementsOnScreenFromEditor (bool state)
    {
        pauseOrResumeShowHealthSlidersFromEditor (!state);

        pauseOrResumeShowObjectivesFromEditor (!state);

        pauseOrResumeShowIconsFromEditor (!state);

        enableOrDisableDynamicUIElementsParent (state);
    }

    public void pauseOrResumeShowHealthSlidersFromEditor (bool state)
    {
        mainPlayerHealthBarManager.pauseOrResumeShowHealthSlidersFromEditor (state);
    }

    public void pauseOrResumeShowObjectivesFromEditor (bool state)
    {
        playerScreenObjectivesManager.pauseOrResumeShowObjectivesFromEditor (state);
    }

    public void pauseOrResumeShowIconsFromEditor (bool state)
    {
        mainPlayerPickupIconManager.pauseOrResumeShowIconsFromEditor (state);
    }

    //INPUT FUNCTIONS
    public void inputPauseGame ()
    {
        if (pauseGameInputPaused) {
            return;
        }

        if (pauseCalledFromGameManager) {
            return;
        }

        if (openMenuSystemAssigned) {
            currentOpenMenuSystem.closeMenu ();

            return;
        }

        pauseGame ();
    }

    float lastTimeIngameMenuOpened = -1;

    float minWaitTimeToReturnPauseGameFromGamepad = -1;

    public void setMinWaitTimeToReturnPauseGameFromGamepad ()
    {
        minWaitTimeToReturnPauseGameFromGamepad = Time.unscaledTime;
    }

    public void inputReturnPauseGameFromGamepad ()
    {
        if (pauseGameInputPaused) {
            return;
        }

        if (pauseCalledFromGameManager) {
            return;
        }

        if (minWaitTimeToReturnPauseGameFromGamepad > -1) {
            if (Time.unscaledTime < minWaitTimeToReturnPauseGameFromGamepad + 0.3f) {
                //print ("cancelling return gamepad");

                return;
            } else {
                minWaitTimeToReturnPauseGameFromGamepad = -1;

                //print ("cancelling finished, continuing");
            }
        }

        if (openMenuSystemAssigned) {
            currentOpenMenuSystem.closeMenu ();

            return;
        }

        if (gamePaused) {

            if (!playerMenuActive && menuUsingUINavigationActive && mainUIRouterLocated) {
                //print ("UI navigation active");

                if (mainUIRouter.HasPopupOpen ()) {
                    mainUIRouter.processCancelEventOnCurrentPage ();

                    //print ("closing pop up");

                    return;
                }
            }

            pauseGame ();
        }
    }

    public void inputCloseIngameMenuFromGamepad ()
    {
        if (pauseGameInputPaused) {
            return;
        }

        if (pauseCalledFromGameManager) {
            return;
        }

        if (minWaitTimeToReturnPauseGameFromGamepad > -1) {
            if (Time.unscaledTime < minWaitTimeToReturnPauseGameFromGamepad + 0.3f) {
                //print ("cancelling return gamepad");

                return;
            } else {
                minWaitTimeToReturnPauseGameFromGamepad = -1;

                //print ("cancelling finished, continuing");
            }
        }

        if (Time.unscaledTime < lastTimeIngameMenuOpened + 0.3f) {
            //print ("cancelling return gamepad");

            return;
        }

        if (playerMenuActive) {

            if (menuUsingUINavigationActive && mainUIRouterLocated) {
                //print ("UI navigation active");

                if (mainUIRouter.HasPopupOpen ()) {
                    mainUIRouter.processCancelEventOnCurrentPage ();

                    //print ("closing pop up");

                    return;
                }
            }

            //print ("using device " + playerControllerManager.isUsingDevice ());

            //if (playerControllerManager.isUsingDevice ()) {
            //    print ("player is using device");
            //}

            //call to the close current ingame menu function, maybe by using currentIngameMenuIndex


            if (currentIngameMenuIndex > -1) {
                ingameMenuInfo currentIngameMenuInfo = ingameMenuInfoList [currentIngameMenuIndex];

                if (currentIngameMenuInfo.menuOpened) {
                    checkCloseIngameMenusIfOpened ();
                }
            }
        }
    }

    public void inputOpenPreviousPlayerOpenMenu ()
    {
        if (changeBetweenMenusInputEnabled) {
            openPreviousPlayerOpenMenu ();
        }
    }

    public void inputOpenNextPlayerOpenMenu ()
    {
        if (changeBetweenMenusInputEnabled) {
            openNextPlayerOpenMenu ();
        }
    }

    public void inputOpenOrClosePlayerMenu ()
    {
        if (changeBetweenIngameMenuPaused) {
            return;
        }

        if (!defaultPlayerMenuInputEnabled) {
            return;
        }

        openOrClosePlayerMenu ();
    }

    public void inputShowOrHideCursorOnCurrentPauseMenu ()
    {
        if (toggleCursorOrUINaivgationOnPauseMenusEnabled) {
            showOrHideCursorOnCurrentPauseMenu ();
        }
    }

    public void inputShowOrHideCursorOnCurrentIngameMenu ()
    {
        if (toggleCursorOrUINaivgationOnIngameMenusEnabled) {
            showOrHideCursorOnCurrentIngameMenu ();
        }
    }

    public void setPauseGameInputPausedState (bool state)
    {
        pauseGameInputPaused = state;
    }

    public void openOrClosePlayerMenu ()
    {
        if (!playerMenuActive) {

            if (playerControllerManager.isUsingDevice ()) {
                return;
            }

            bool indexFound = false;

            int playerMenuInfoListCount = playerMenuInfoList.Count;

            for (int i = 0; i < playerMenuInfoListCount; i++) {
                if (!indexFound && playerMenuInfoList [i].menuCanBeUsed) {
                    if (useDefaultPlayerMenuNameToOpen) {
                        if (playerMenuInfoList [i].Name.Equals (defaultPlayerMenuNameToOpen)) {
                            currentPlayerMenuInfoIndex = i;

                            indexFound = true;
                        }
                    } else {
                        currentPlayerMenuInfoIndex = i;

                        indexFound = true;
                    }
                }
            }

            if (!indexFound) {
                if (showDebugPrint) {
                    print ("All menus are disabled on the optioon menu can be used, cancelling opening");
                }

                return;
            }
        }

        activatePlayerMenuInfoEvent (currentPlayerMenuInfoIndex);
    }

    public void openNextPlayerOpenMenu ()
    {
        if (changeBetweenIngameMenuPaused) {
            return;
        }

        if (!playerMenuActive) {
            return;
        }

        if (playerControllerManager.isPlayerDriving ()) {
            return;
        }

        for (int i = 0; i < ingameMenuInfoList.Count; i++) {
            if (ingameMenuInfoList [i].menuOpened && !ingameMenuInfoList [i].canUsePreviousNextMenuButtons) {
                return;
            }
        }

        activatePlayerMenuInfoEvent (currentPlayerMenuInfoIndex);

        bool exit = false;

        int max = 0;

        int playerMenuInfoListCount = playerMenuInfoList.Count;

        while (!exit) {
            max++;
            if (max > 100) {
                return;
            }

            currentPlayerMenuInfoIndex++;

            if (currentPlayerMenuInfoIndex > playerMenuInfoListCount - 1) {
                currentPlayerMenuInfoIndex = 0;
            }

            if (playerMenuInfoList [currentPlayerMenuInfoIndex].menuCanBeUsed &&
                playerMenuInfoList [currentPlayerMenuInfoIndex].menuCanBeOpenedByPreviousNextMenuButton) {
                exit = true;
            }
        }

        activatePlayerMenuInfoEvent (currentPlayerMenuInfoIndex);
    }

    public void openPreviousPlayerOpenMenu ()
    {
        if (changeBetweenIngameMenuPaused) {
            return;
        }

        if (!playerMenuActive) {
            return;
        }

        if (playerControllerManager.isPlayerDriving ()) {
            return;
        }

        for (int i = 0; i < ingameMenuInfoList.Count; i++) {
            if (ingameMenuInfoList [i].menuOpened && !ingameMenuInfoList [i].canUsePreviousNextMenuButtons) {
                return;
            }
        }

        activatePlayerMenuInfoEvent (currentPlayerMenuInfoIndex);

        bool exit = false;

        int max = 0;

        int playerMenuInfoListCount = playerMenuInfoList.Count;

        while (!exit) {
            max++;
            if (max > 100) {
                return;
            }

            currentPlayerMenuInfoIndex--;

            if (currentPlayerMenuInfoIndex < 0) {
                currentPlayerMenuInfoIndex = playerMenuInfoListCount - 1;
            }

            if (playerMenuInfoList [currentPlayerMenuInfoIndex].menuCanBeUsed &&
                playerMenuInfoList [currentPlayerMenuInfoIndex].menuCanBeOpenedByPreviousNextMenuButton) {
                exit = true;
            }
        }

        activatePlayerMenuInfoEvent (currentPlayerMenuInfoIndex);
    }

    public void openPlayerOpenMenuByName (string menuName)
    {
        if (openOrClosePlayerOpenMenuByNamePaused) {
            return;
        }

        if (showDebugPrint) {
            print ("selecting menu " + menuName);
        }

        if (playerMenuActive) {
            activatePlayerMenuInfoEvent (currentPlayerMenuInfoIndex);

            if (menuName.Equals (playerMenuInfoList [currentPlayerMenuInfoIndex].Name)) {
                return;
            }
        }

        int playerMenuInfoListCount = playerMenuInfoList.Count;

        for (int i = 0; i < playerMenuInfoListCount; i++) {

            if (playerMenuInfoList [i].Name.Equals (menuName) && playerMenuInfoList [i].menuCanBeUsed) {
                currentPlayerMenuInfoIndex = i;

                activatePlayerMenuInfoEvent (i);
            }
        }
    }

    public void closePlayerMenuByName (string menuName)
    {
        if (openOrClosePlayerOpenMenuByNamePaused) {
            if (menuOpenedWithActionSystemActive && menuOpenedWithActionSystemName.Equals (menuName)) {
                checkCloseMenuOpenedByActionSystem ();
            }

            if (allowToExitMenuWithTitleMenuTextButtonActive) {
                if (openMenuSystemAssigned) {
                    currentOpenMenuSystem.closeMenu ();
                }
            }

            return;
        }

        int playerMenuInfoListCount = playerMenuInfoList.Count;

        for (int i = 0; i < playerMenuInfoListCount; i++) {

            if (playerMenuInfoList [i].Name.Equals (menuName) && playerMenuInfoList [i].menuCanBeUsed) {

                activatePlayerMenuInfoEvent (i);

                return;
            }
        }
    }

    public void enableCanUsePreviousNextMenuButtonsState (bool state, string menuName)
    {
        enableOrDisableCanUsePreviousNextMenuButtonsState (true, menuName);
    }

    public void disableCanUsePreviousNextMenuButtonsState (bool state, string menuName)
    {
        enableOrDisableCanUsePreviousNextMenuButtonsState (false, menuName);
    }

    public void enableOrDisableCanUsePreviousNextMenuButtonsState (bool state, string menuName)
    {
        for (int i = 0; i < ingameMenuInfoList.Count; i++) {
            if (ingameMenuInfoList [i].Name.Equals (menuName)) {
                ingameMenuInfoList [i].canUsePreviousNextMenuButtons = state;


                return;
            }
        }
    }

    void activatePlayerMenuInfoEvent (int menuInfoIndex)
    {
        playerMenuInfoList [menuInfoIndex].currentMenuEvent.Invoke ();

        if (playerMenuInfoList [menuInfoIndex].mainIngameMenuPanelAssigned) {
            playerMenuInfoList [menuInfoIndex].mainIngameMenuPanel.openOrCloseMenuFromTouch ();
        }
    }

    void setIngameMenuPanelOnPlayerMenuInfoList (ingameMenuPanel newIngameMenuPanel)
    {
        int playerMenuInfoListCount = playerMenuInfoList.Count;

        for (int i = 0; i < playerMenuInfoListCount; i++) {
            if (playerMenuInfoList [i].Name.Equals (newIngameMenuPanel.menuPanelName)) {
                playerMenuInfoList [i].mainIngameMenuPanel = newIngameMenuPanel;

                playerMenuInfoList [i].mainIngameMenuPanelAssigned = true;

                playerMenuInfoList [i].closeMenuIfGamePaused = newIngameMenuPanel.closeMenuIfGamePaused;

                return;
            }
        }
    }

    public void checkcreateIngameMenuPanel (string menuName)
    {
        int currentIndex = ingameMenuInfoList.FindIndex (s => s.Name.Equals (menuName));

        if (currentIndex > -1) {
            createIngameMenuPanel (currentIndex);
        }
    }

    bool inoreCreateIngameMenuPanel;

    public void setIgnoreCreateIngameMenuPanelState (bool state)
    {
        inoreCreateIngameMenuPanel = state;
    }

    void createIngameMenuPanel (int menuInfoIndex)
    {
        if (inoreCreateIngameMenuPanel) {
            return;
        }

        ingameMenuInfo currentIngameMenuInfo = ingameMenuInfoList [menuInfoIndex];

        if (!currentIngameMenuInfo.mainIngameMenuPanelAssigned && currentIngameMenuInfo.menuPanelPrefab != null && currentIngameMenuInfo.menuPanelGameObject == null) {
            GameObject newPanel = (GameObject)Instantiate (currentIngameMenuInfo.menuPanelPrefab, Vector3.zero, Quaternion.identity);

            if (!newPanel.activeSelf) {
                newPanel.SetActive (true);
            }

            if (currentIngameMenuInfo.parentForMenuPanelInstantiated != null) {
                newPanel.transform.SetParent (currentIngameMenuInfo.parentForMenuPanelInstantiated);
            }

            ingameMenuPanel currentIngameMenuPanel = newPanel.GetComponent<ingameMenuPanel> ();

            if (currentIngameMenuPanel != null) {

                currentIngameMenuInfo.mainIngameMenuPanel = currentIngameMenuPanel;

                currentIngameMenuInfo.mainIngameMenuPanelAssigned = true;

                currentIngameMenuInfo.closeMenuIfGamePaused = currentIngameMenuPanel.closeMenuIfGamePaused;

                currentIngameMenuPanel.setPauseManager (this);

                setIngameMenuPanelOnPlayerMenuInfoList (currentIngameMenuPanel);
            }

            currentIngameMenuInfo.menuCanvasGroup = newPanel.GetComponentInChildren<CanvasGroup> ();

            if (currentIngameMenuInfo.menuCanvasGroup != null) {
                currentIngameMenuInfo.menuPanelGameObject = currentIngameMenuInfo.menuCanvasGroup.gameObject;

                currentIngameMenuInfo.menuPanelGameObject.SetActive (false);
            }

            currentIngameMenuInfo.mainUIPage = currentIngameMenuPanel.mainUIPage;

        }
    }

    public void setEnabledMenuCanBeUsedState (string menuNameToSearch)
    {
        setMenuCanBeUsedState (menuNameToSearch, true);
    }

    public void setDisabledMenuCanBeUsedState (string menuNameToSearch)
    {
        setMenuCanBeUsedState (menuNameToSearch, false);
    }

    public void setMenuCanBeUsedState (string menuNameToSearch, bool state)
    {
        int playerMenuInfoListCount = playerMenuInfoList.Count;

        for (int i = 0; i < playerMenuInfoListCount; i++) {

            if (playerMenuInfoList [i].Name.Equals (menuNameToSearch)) {
                playerMenuInfoList [i].menuCanBeUsed = state;

                if (!Application.isPlaying) {
                    updateComponent ();
                }

                return;
            }
        }
    }

    public bool checkIfMenuCanBeUsedByName (string menuNameToSearch)
    {
        int playerMenuInfoListCount = playerMenuInfoList.Count;

        for (int i = 0; i < playerMenuInfoListCount; i++) {
            if (playerMenuInfoList [i].Name.Equals (menuNameToSearch)) {
                return playerMenuInfoList [i].menuCanBeUsed;
            }
        }

        return true;
    }

    public void setOpenOrClosePlayerOpenMenuByNamePausedState (bool state)
    {
        openOrClosePlayerOpenMenuByNamePaused = state;
    }

    public bool isOpenOrClosePlayerOpenMenuByNamePaused ()
    {
        return openOrClosePlayerOpenMenuByNamePaused;
    }

    public void setAllowToExitMenuWithTitleMenuTextButtonActiveState (bool state)
    {
        allowToExitMenuWithTitleMenuTextButtonActive = state;
    }

    public void setChangeBetweenIngameMenuPausedState (bool state)
    {
        changeBetweenIngameMenuPaused = state;
    }

    public void enableOrDisablePlayerHUD (bool state)
    {
        eventToEnableOrDisableAllPlayerHUD.Invoke (state);
    }

    public void enableOrDisableSecondaryPlayerHUD (bool state)
    {
        eventToEnableOrDisableSecondaryPlayerHUD.Invoke (state);
    }

    public void enableOrDisableVehicleHUD (bool state)
    {
        eventToEnableOrDisableAllVehicleHUD.Invoke (state);
    }

    public Vector2 getMainCanvasSizeDelta ()
    {
        return mainCanvasRectTransform.sizeDelta;
    }

    public Canvas getMainCanvas ()
    {
        return mainCanvas;
    }

    public GraphicRaycaster getMainGraphicRaycaster ()
    {
        if (!mainGraphicRaycasterLocated) {
            mainGraphicRaycaster = mainCanvas.GetComponent<GraphicRaycaster> ();

            mainGraphicRaycasterLocated = mainGraphicRaycaster != null;
        }

        return mainGraphicRaycaster;
    }

    public RectTransform getMainCanvasRectTransform ()
    {
        return mainCanvasRectTransform;
    }

    public List<canvasInfo> getCanvasInfoList ()
    {
        return canvasInfoList;
    }

    public void addNewCanvasToList (Canvas newCanvas)
    {
        if (!isCanvasAlreadyOnInfoList (newCanvas)) {
            canvasInfo newCanvasInfo = new canvasInfo ();

            newCanvasInfo.Name = newCanvas.name;
            newCanvasInfo.mainCanvas = newCanvas;
            newCanvasInfo.mainCanvasScaler = newCanvas.GetComponent<CanvasScaler> ();

            canvasInfoList.Add (newCanvasInfo);
        }

        if (!Application.isPlaying) {
            updateComponent ();
        }
    }

    public canvasInfo getCanvasInfoListByCanvasObject (Canvas newCanvas)
    {
        if (newCanvas == null) {
            return null;
        }

        for (int k = 0; k < canvasInfoList.Count; k++) {
            if (canvasInfoList [k].mainCanvas == newCanvas) {
                return canvasInfoList [k];
            }
        }

        return null;
    }

    public bool isCanvasAlreadyOnInfoList (Canvas newCanvas)
    {
        if (newCanvas == null) {
            return false;
        }

        for (int k = 0; k < canvasInfoList.Count; k++) {
            if (canvasInfoList [k].mainCanvas == newCanvas) {
                return true;
            }
        }

        return false;
    }

    public bool isUsingScreenSpaceCamera ()
    {
        return mainCanvas.renderMode == RenderMode.ScreenSpaceCamera;
    }

    public CanvasScaler getMainCanvasScaler ()
    {
        return mainCanvasScaler;
    }

    public Camera getMainCanvasCamera ()
    {
        return mainCanvasCamera;
    }

    public playerInputManager getPlayerInput ()
    {
        return playerInput;
    }

    public void setCanvasInfo (bool creatingCharactersOnEditor)
    {
        playerCameraManager.setCanvasInfo (getMainCanvasSizeDelta (), isUsingScreenSpaceCamera (), creatingCharactersOnEditor);
    }

    public Transform getMainCameraTransform ()
    {
        return playerCameraManager.getMainCamera ().transform;
    }

    public void checkUpdateReticleActiveState (bool state)
    {
        playerCameraManager.checkUpdateReticleActiveState (state);
    }

    public bool isMenuPaused ()
    {
        return gamePaused;
    }

    public bool isGamePaused ()
    {
        return mainGameManager.isGamePaused ();
    }

    public bool isLoadGameEnabled ()
    {
        return mainGameManager.isLoadGameEnabled ();
    }

    public bool isPlayerMenuActive ()
    {
        return playerMenuActive;
    }

    public void saveGameInfoFromEditor (string saveInfoName)
    {
        mainGameManager.saveGameInfoFromEditor (saveInfoName);
    }

    public void setGameVolume (float newValue)
    {
        AudioListener.volume = newValue;
    }

    public bool isUsingGamepad ()
    {
        return input.isUsingGamepad ();
    }

    public void setCurrentOpenMenuSystem (openMenuSystem newOpenMenuSystem)
    {
        currentOpenMenuSystem = newOpenMenuSystem;

        openMenuSystemAssigned = currentOpenMenuSystem != null;
    }


    //Player controller component function calls
    public void setHeadBobPausedState (bool state)
    {
        playerControllerManager.setHeadBobPausedState (state);
    }

    public GameObject getPlayerControllerGameObject ()
    {
        return playerControllerManager.gameObject;
    }

    public GameObject getHudAndMenus ()
    {
        return hudAndMenus;
    }

    public void setAnimatorUnscaledTimeState (bool state)
    {
        playerControllerManager.setAnimatorUnscaledTimeState (state);
    }

    public float getCurrentIdleID ()
    {
        return playerControllerManager.getCurrentIdleID ();
    }

    public void setCurrentIdleIDValue (float animatorIdle)
    {
        playerControllerManager.setCurrentIdleIDValueFloat (animatorIdle);
    }

    public void updateIdleIDOnAnimator ()
    {
        playerControllerManager.updateIdleIDOnAnimator ();
    }

    public void setUsingDeviceState (bool state)
    {
        playerControllerManager.setUsingDeviceState (state);
    }

    public void stopEditorOrGame ()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
		confirmExit();
#endif
    }

    public void updateComponent ()
    {
        GKC_Utils.updateComponent (this);

        GKC_Utils.updateDirtyScene ("Update Menu System", gameObject);
    }

    //a class to save the current and previous state of the mouse visibility and the state of the camera, to enable and disable them correctly according to every
    //type of situation
    [System.Serializable]
    public class cursorStateInfo
    {
        public bool currentVisible;
        public bool previousVisible;
        public bool currentCameraEnabled;
        public bool previousCameraEnabled;
    }

    [System.Serializable]
    public class submenuInfo
    {
        [Header ("Main Settings")]
        [Space]

        public string Name;
        public GameObject menuGameObject;

        public bool isSubMenu;

        [Space]
        [Header ("UI Navigation Settings")]
        [Space]

        public bool useCursorOnMenu;
        public UIPage mainUIPage;

        [Space]
        [Header ("Debug")]
        [Space]

        public bool menuOpened;

        public bool menuUsingUINavigationActive;

        [Space]
        [Header ("Events Settings")]
        [Space]

        public bool useEventOnOpen;
        public UnityEvent eventOnOpen;

        public bool useEventOnClose;
        public UnityEvent eventOnClose;
    }

    [System.Serializable]
    public class playerMenuInfo
    {
        [Header ("Main Settings")]
        [Space]

        public string Name;
        public bool menuCanBeUsed = true;

        public bool menuCanBeOpenedByPreviousNextMenuButton = true;

        [Space]
        [Header ("Panel Settings")]
        [Space]

        public ingameMenuPanel mainIngameMenuPanel;

        [Space]
        [Header ("Debug")]
        [Space]

        public bool mainIngameMenuPanelAssigned;

        public bool closeMenuIfGamePaused;

        [Space]
        [Header ("Event Settings")]
        [Space]

        public UnityEvent currentMenuEvent;
    }

    [System.Serializable]
    public class ingameMenuInfo
    {
        [Header ("Main Settings")]
        [Space]

        public string Name;

        public bool canUsePreviousNextMenuButtons = true;

        public bool menuOpened;

        [Space]
        [Header ("Debug")]
        [Space]

        public bool mainIngameMenuPanelAssigned;
        public bool closeMenuIfGamePaused;

        public bool menuUsingUINavigationActive;

        [Space]
        [Header ("Canvas Settings")]
        [Space]

        public bool useMenuCanvasGroup;
        public CanvasGroup menuCanvasGroup;
        public GameObject menuPanelGameObject;

        [Space]
        [Header ("Panel Settings")]
        [Space]

        public GameObject menuPanelPrefab;
        public Transform parentForMenuPanelInstantiated;

        public ingameMenuPanel mainIngameMenuPanel;

        [Space]
        [Header ("UI Navigation Settings")]
        [Space]

        public bool useCursorOnMenu;
        public UIPage mainUIPage;

        public bool refreshItemListOnOpenMenu;

        [Space]
        [Header ("Time Scale Settings")]
        [Space]

        public bool setCustomTimeScale;
        public float customTimeScale;

        public bool pauseAIWhenOpenMenu;

        public int pauseCharacterPriority = 1;

        [Space]
        [Header ("Mouse Cursor Speed Settings")]
        [Space]

        public bool setNewMouseCursorControllerSpeed;
        public float newMouseCursroControllerSpeed;

        [Space]
        [Header ("Event Settings")]
        [Space]

        public bool useEventOnOpenClose;
        public UnityEvent eventOnOpen;
        public UnityEvent eventOnClose;

        [Space]

        public UnityEvent closeMenuEvent;

        public Coroutine canvasGroupCoroutine;
    }

    [System.Serializable]
    public class canvasInfo
    {
        public string Name;
        public Canvas mainCanvas;
        public CanvasScaler mainCanvasScaler;
    }
}