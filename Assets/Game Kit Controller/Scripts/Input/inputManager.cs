using GKC.Localization;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using System.Linq;
using static InputAxisWrapper;
using static saveGameSystem;





#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.Controls;
#endif

#if REWIRED
using Rewired;
#endif

[System.Serializable]
public class inputManager : MonoBehaviour
{
    public List<Axes> axes = new List<Axes> ();

    public List<multiAxes> multiAxesList = new List<multiAxes> ();

    public GameObject editInputPanelPrefab;
    public GameObject editInputMenu;
    public Text currentInputPanelListText;

    public loadType loadOption;
    public bool useRelativePath;

    public bool checkInputKeyDuplicatedOnRebindAction;
    public bool checkInputKeyDuplicatedOnlyOnSameInputCategory;

    public List<GameObject> buttonsDisabledAtStart = new List<GameObject> ();

    public List<gamepadInfo> gamepadList = new List<gamepadInfo> ();

    private Dictionary<int, newInputGamepadInfo> newInputGamepadInfoList = new Dictionary<int, newInputGamepadInfo> ();

    public List<newInputGamepadInfo> newInputGamepadInfoListDebug = new List<newInputGamepadInfo> ();

    newInputGamepadInfo currentNewInputGamepadToCheck;


    public List<playerInputManager> playerInputManagerList = new List<playerInputManager> ();

    public bool showActionKeysOnMenu = true;

    public bool touchPlatform;
    public bool gameCurrentlyPaused;

    public bool useTouchControls;

    public playerCharactersManager charactersManager;
    public menuPause pauseManager;
    public gameManager mainGameManager;

    public bool ignoreGamepad;
    public bool allowKeyboardAndGamepad;
    public bool allowGamepadInTouchDevice;
    public float checkConnectedGamepadRate;
    float lastGamepadUpdateCheckTime;

    public List<multiAxesEditButtonInput> multiAxesEditButtonInputList = new List<multiAxesEditButtonInput> ();

    public List<touchButtonListener> touchButtonList = new List<touchButtonListener> ();

    public List<touchButtonsInfo> touchButtonsInfoList = new List<touchButtonsInfo> ();
    public List<touchButtonsStringInfo> touchButtonsStringInfoList = new List<touchButtonsStringInfo> ();

    public List<touchButtonListener> buttonsToIgnoreSetVisibleStateList = new List<touchButtonListener> ();

    bool touchButtonsCurrentlyVisible = true;

    Scrollbar scroller;
    bool mainInputMenuScrollerLocated;

    InputAxisWrapper.TouchPressType touchPressType;

    Touch currentTouch;
    int currentFingerID;

    public bool usingGamepad;
    public bool onlyOnePlayer;
    public bool usingKeyBoard;
    public int numberOfGamepads = -1;

    [Tooltip ("If true any keypress will be logged to the Debug console.")]
    public bool showKeyboardPressed;
    public bool showKeyboardPressedAction;
    public bool showGamepadPressed;
    public bool showGamepadPressedAction;
    public bool showPressedKeyWhenEditingInput;

    public bool showGamepadDebug;

    public bool showDebugKeyPressed;


    public string inputActionCategoryNameToSearch;
    public string inputActionNameToRename;
    public string newInputActionNameToUse;

    public string inputActionCategoryNameToRename;
    public string newInputActionCategoryName;

    public bool editingInput;
    bool newKeyAssigned;

    editButtonInput currentEditButtonInputToRebind;

    KeyCode [] validKeyCodes;

    string [] currentGamepadConnectedNameArray;
    List<string> currentGamepadConnectedNameList = new List<string> ();

    public int currentInputPanelListIndex = 0;

    public gamepadIconsData mainGamepadIconsData;

    bool mainGamepadIconsDataLocated;

    bool previouslyUsingGamepad;
    bool editInputMenuOpened;

    bool usingTouchControls;
    bool gameManagerPaused;

    public enum buttonType
    {
        //type of press of a key
        getKey,
        getKeyDown,
        getKeyUp,
        negMouseWheel,
        posMouseWheel,
    }

    //load the key input in the game from the current configuration in the inspector or load from a file
    public enum loadType
    {
        loadFile,
        loadEditorConfig,
    }

    float currentAxisValue;

    KeyCode currentKeyCodeToCheck;

    public Dictionary<string, Dictionary<JoystickData.ButtonTypes, int>> register;
    bool registerInitialized;
    public string [] joystickNames;
    public string joystickName;

    Axes currentKeyButtonToCheck;

    Dictionary<JoystickData.ButtonTypes, int> buttonConfig;
    int keyIndexPressed;
    string retString;
    bool joystickNamesInitialized;

    bool currentJoystickButtonIsAxis;

    Axes currentJoystickButtonToCheck;

    Axes currentTouchButtonToCheck;

    int currentTouchButtonIndexToCheck;
    touchButtonListener currentTouchButtonListener;

    int numberOfMultiAxes;
    int [] multiAxesIndexList;

    public string [] touchButtonListString;
    string currentJoystickButtonKeyString;

    string saveFileFolderName = "Input";
    string saveFileName = "Input File";

    string defaultSaveFileName = "Default Input File";

    int numberOfTouchControls;

    public List<string> currentInputActionList = new List<string> ();

    public KeyCode actionKeyToFilter;
    public joystickButtons joystickButtonToFilter;

    public bool createRebindInputMenuWithCurrentAxes;
    public int rebindInputMenuPanelsAmount;
    public int rebindInputMenuActionsPerPanelAmount;

    bool joystickFilterActive;
    bool keyFilterActive;

    public bool lastButtonPressedOnGamepad;
    public bool lastKeyPressedOnKeyboard;

    public bool showMissingInputWarningMessagesEnabled = true;

    string emptyGamepadName = "";

    bool inputManagerInitialized;

    public bool useCustomInputFileName;
    public string customInputFileName;

    public bool useCustomInputFilePathName = true;
    public string customInputFilePathName = "Assets/Game Kit Controller/Input";

    public string [] customInputFileList;

    public bool customInputFileListAssigned;



    public string horizontalString = "Horizontal";
    public string verticalString = "Vertical";

    public string mouseXString = "Mouse X";
    public string mouseYString = "Mouse Y";

    public bool useNewInputSystem;

    public bool useNewInputKeys;

    int currentNewInputGamepadInfoIndex = 0;

    int currentTouchCount;
    int currentTouchIndex;

    bool useMouseButtonType;


#if ENABLE_INPUT_SYSTEM
    public InputActionReference moveActionReference;
    public InputActionReference lookActionReference;


    KeyControl [] numberKeys;

    Dictionary<KeyCode, Key> keyCache = new Dictionary<KeyCode, Key> ();

    Keyboard mainKeyboard;

    Mouse mainMouse;

    MouseButton mouseButtonInput;

    Key currentKeyToCheck;

    public Key newInputActionKeyToFilter;

    public MouseButton newInputMouseButtonToFilter;

    public GamepadButton newInputGamepadButtonToFilter;

    public bool filterForMouseButton;

    GamepadButton currentGamepadButtonToCheck;

    public const GamepadButton newGamepadButtonNone = (GamepadButton)(-1);
#endif

    public float newInputSystemMoveAxisSmoothSpeed = 5;
    public float newInputSystemLookAxisSmoothSpeed = 5;



    private static inputManager _inputManagerInstance;

    public static inputManager Instance { get { return _inputManagerInstance; } }

    bool instanceInitialized;

    bool rescanPossibleGamepadsActive = false;




    public void getComponentInstance ()
    {
        if (instanceInitialized) {
            return;
        }

        if (_inputManagerInstance != null && _inputManagerInstance != this) {
            Destroy (this.gameObject);

            return;
        }

        _inputManagerInstance = this;

        instanceInitialized = true;
    }

    public void getComponentInstanceOnApplicationPlaying ()
    {
        if (Application.isPlaying) {
            getComponentInstance ();
        }
    }

    void Awake ()
    {
        getComponentInstance ();

#if !ENABLE_INPUT_SYSTEM
    useNewInputSystem = false;
    useNewInputKeys = false;
#endif

        if (useNewInputSystem) {
#if ENABLE_INPUT_SYSTEM
            mainKeyboard = Keyboard.current;

            mainMouse = Mouse.current;

            InputAxisWrapper.InitializeMove (moveActionReference.action);

            InputAxisWrapper.InitializeLook (lookActionReference.action);

            InputAxisWrapper.initializeInputSources ();

            InputAxisWrapper.setUseNewInputSystemState (true);

            var k = Keyboard.current;

            numberKeys = new KeyControl []
            {
        k.digit0Key, k.digit1Key, k.digit2Key, k.digit3Key, k.digit4Key,
        k.digit5Key, k.digit6Key, k.digit7Key, k.digit8Key, k.digit9Key
        //,
        //k.numpad0Key, k.numpad1Key, k.numpad2Key, k.numpad3Key, k.numpad4Key,
        //k.numpad5Key, k.numpad6Key, k.numpad7Key, k.numpad8Key, k.numpad9Key
            };
#endif
        } else {
            InputAxisWrapper.setUseNewInputSystemState (false);
        }
    }

    string [] GetJoystickNames ()
    {
        return InputAxisWrapper.GetJoystickNames ();
    }


#if REWIRED
	public bool useRewired;
	private IList<Player> _rewiredPlayers;
#endif

    public bool checkInputManagerElementsOnStartEnabled = true;

    void Start ()
    {
        if (checkInputManagerElementsOnStartEnabled) {
            checkInputManagerElementsOnStart ();
        }
    }

    void OnApplicationFocus (bool state)
    {
        //print ("OnApplicationFocus " + state);
    }

    private void OnEnable ()
    {
        if (useNewInputSystem) {
#if ENABLE_INPUT_SYSTEM
            InputSystem.onDeviceChange += OnDeviceChange;
#endif
        }
    }

    private void OnDisable ()
    {
        if (useNewInputSystem) {
#if ENABLE_INPUT_SYSTEM
            stopAllGamepadsVibration ();

            InputSystem.onDeviceChange -= OnDeviceChange;
#endif
        }
    }

#if ENABLE_INPUT_SYSTEM
    private void OnDeviceChange (InputDevice device, InputDeviceChange change)
    {
        if (ignoreGamepad) {
            return;
        }

        if (device is Gamepad gamepad) {
            switch (change) {
                case InputDeviceChange.Added:
                    addOrRemoveNewInputGamepad (gamepad, gamepad.deviceId, true, false);

                    if (showGamepadDebug) {
                        Debug.Log ($"Gamepad connected: {gamepad.displayName}");
                        Debug.Log ($"Gamepads amount: {Gamepad.all.Count}");
                    }

                    break;

                case InputDeviceChange.Removed:
                    addOrRemoveNewInputGamepad (gamepad, gamepad.deviceId, false, false);

                    if (showGamepadDebug) {
                        Debug.Log ($"Gamepad disconnected: {gamepad.displayName}");
                        Debug.Log ($"Gamepads amount: {Gamepad.all.Count}");
                    }

                    break;
            }
        }

        if (allowGamepadInTouchDevice) {
            if (InputAxisWrapper.checkTouchPlatform ()) {
                if (change == InputDeviceChange.Added ||
                change == InputDeviceChange.Disconnected ||
                change == InputDeviceChange.Reconnected) {
                    rescanPossibleGamepadsActive = true;

                    print ("OnDeviceChange function triggered");
                }
            }
        }
    }

    void addOrRemoveNewInputGamepad (Gamepad gamepadToCheck, int deviceID, bool state, bool unregisterGamepad)
    {
        if (!Application.isPlaying) {
            return;
        }

        bool updateGamepadStateResult = false;

        if (state) {
            string currentGamepadDeviceKey = InputAxisWrapper.GetDeviceKey (gamepadToCheck);

            bool gamepadLocated = newInputGamepadInfoList.Any (x => x.Value.deviceKey == currentGamepadDeviceKey && x.Value.connected == false);

            if (gamepadLocated) {
                var currentNewInputGamepadInfo =
                    newInputGamepadInfoList.FirstOrDefault (x => x.Value.deviceKey == currentGamepadDeviceKey && x.Value.connected == false);

                if (newInputGamepadInfoList.ContainsKey (currentNewInputGamepadInfo.Key)) {
                    currentNewInputGamepadInfo.Value.connected = true;

                    currentNewInputGamepadInfo.Value.gamepad = gamepadToCheck;

                    currentNewInputGamepadInfo.Value.deviceId = deviceID;

                    updateGamepadStateResult = true;

                    if (showGamepadDebug) {
                        print ("gamepad connected before, reconnecting");
                    }
                }
            } else {
                newInputGamepadInfo newInputGamepad = new newInputGamepadInfo ();

                currentNewInputGamepadInfoIndex++;

                newInputGamepad.id = currentNewInputGamepadInfoIndex;
                newInputGamepad.gamepad = gamepadToCheck;
                newInputGamepad.connected = true;
                newInputGamepad.playerIndex = currentNewInputGamepadInfoIndex;
                newInputGamepad.deviceName = gamepadToCheck.name;
                newInputGamepad.deviceId = gamepadToCheck.deviceId;
                newInputGamepad.isActive = true;
                newInputGamepad.lastInputTime = Time.time;

                newInputGamepad.deviceKey = currentGamepadDeviceKey;

                newInputGamepadInfoList.Add (currentNewInputGamepadInfoIndex, newInputGamepad);

                newInputGamepadInfoListDebug.Add (newInputGamepad);

                GamepadRegistry.Register (currentNewInputGamepadInfoIndex, gamepadToCheck);

                updateGamepadStateResult = true;

                if (showGamepadDebug) {
                    print ("new gamepad added");
                }
            }
        } else {
            string currentGamepadDeviceKey = InputAxisWrapper.GetDeviceKey (gamepadToCheck);

            var currentNewInputGamepadInfo = newInputGamepadInfoList.FirstOrDefault (x => x.Value.deviceKey == currentGamepadDeviceKey);

            if (newInputGamepadInfoList.ContainsKey (currentNewInputGamepadInfo.Key)) {
                currentNewInputGamepadInfo.Value.connected = false;

                if (currentNewInputGamepadToCheck.rumbleActive) {
                    StopCoroutine (currentNewInputGamepadToCheck.rumbleCoroutine);

                    currentNewInputGamepadToCheck.rumbleActive = false;

                    if (currentNewInputGamepadToCheck.gamepad != null) {
                        currentNewInputGamepadToCheck.gamepad.SetMotorSpeeds (0, 0);
                    }
                }

                currentNewInputGamepadInfo.Value.gamepad = null;

                updateGamepadStateResult = true;

                if (unregisterGamepad) {
                    newInputGamepadInfoList.Remove (currentNewInputGamepadInfo.Key);

                    for (int i = 0; i < newInputGamepadInfoListDebug.Count; ++i) {
                        if (newInputGamepadInfoListDebug [i].id == currentNewInputGamepadInfo.Value.id) {
                            newInputGamepadInfoListDebug.RemoveAt (i);
                        }
                    }

                    currentNewInputGamepadInfoIndex--;

                    if (currentNewInputGamepadInfoIndex < 0) {
                        currentNewInputGamepadInfoIndex = 0;
                    }

                    GamepadRegistry.Unregister (currentNewInputGamepadInfo.Value.id);
                }

                if (showGamepadDebug) {
                    print ("gamepad disconnected");
                }
            }
        }

        if (updateGamepadStateResult) {
            numberOfGamepads = 0;

            foreach (var value in newInputGamepadInfoList.Values) {
                if (value.connected) {
                    numberOfGamepads++;
                }
            }

            if (numberOfGamepads == 0) {
                currentNewInputGamepadInfoIndex = 0;

                newInputGamepadInfoList.Clear ();

                newInputGamepadInfoListDebug.Clear ();

                GamepadRegistry.clearRegister ();
            }

            updateGamepadStateResults ();
        }
    }
#endif

    void stopAllGamepadsVibration ()
    {
#if ENABLE_INPUT_SYSTEM
        InputSystem.ResetHaptics ();
#endif
    }

    public void checkInputManagerElementsOnStart ()
    {
        if (inputManagerInitialized) {
            return;
        }

        inputManagerInitialized = true;
#if REWIRED
		if (useRewired) {
			_rewiredPlayers = ReInput.players.GetPlayers();
			
			foreach (var player in _rewiredPlayers) {
				player.controllers.AddLastActiveControllerChangedDelegate(OnControllerChange);
			}
		}
#endif

        //get if the current platform is a touch device
        touchPlatform = InputAxisWrapper.checkTouchPlatform ();

        checkPlayerInputManagerList ();

        getGameManagerSettings ();

        usingTouchControls = mainGameManager.isUsingTouchControls ();

        pauseManager = charactersManager.getPauseManagerFromPlayerByIndex (0);

        //if the current platform is a mobile, enable the touch controls in case they are not active
        if (touchPlatform && !usingTouchControls) {
            pauseManager.setUseTouchControlsState (true);

            mainGameManager.setUseTouchControlsState (true);

            pauseManager.reloadStart ();

            usingKeyBoard = true;

            usingTouchControls = true;
        }

        useRelativePath = mainGameManager.useRelativePath;

        //load the key input
        loadButtonsInput ();

        getValidKeyCodes ();

        if (usingTouchControls && !allowGamepadInTouchDevice) {
            ignoreGamepad = true;
        }

        if (ignoreGamepad) {
            if (useNewInputSystem) {

            } else {
                currentGamepadConnectedNameArray = new String [0];
            }
        } else {
            if (useNewInputSystem) {

            } else {
                currentGamepadConnectedNameArray = GetJoystickNames ();
            }
        }

        setGamePadList ();

        lastTimeStart = Time.time;

        mainGamepadIconsDataLocated = mainGamepadIconsData != null;
    }

    float lastTimeStart = 0;

    void Update ()
    {
        updateInputManager ();
    }

    void updateInputManager ()
    {
        usingTouchControls = mainGameManager.isUsingTouchControls ();

        gameManagerPaused = mainGameManager.isGamePaused ();

        if (gameManagerPaused && currentNumberOfTouchButtonsPressed > 0) {
            currentNumberOfTouchButtonsPressed = 0;
        }

        updateGamepadsState ();

        updateRebindInputState ();

        if (showDebugKeyPressed) {
            if (useNewInputSystem) {
#if ENABLE_INPUT_SYSTEM
                foreach (KeyControl key in mainKeyboard.allKeys) {
                    if (key.wasPressedThisFrame) {
                        Debug.Log ("KeyCode Pressed: " + key.keyCode.ToString ());
                    }
                }

                foreach (var control in mainMouse.allControls) {
                    if (control is ButtonControl button &&
                        button.wasPressedThisFrame) {

                        Debug.Log ("Mouse Pressed: " + control.name.ToString ());
                    }
                }
#endif
            } else {
                foreach (KeyCode vKey in validKeyCodes) {
                    if (InputAxisWrapper.getOldKeyDown (vKey)) {
                        Debug.Log ("KeyCode Pressed: " + vKey);
                    }
                }
            }
        }
    }

    void updateGamepadsState ()
    {
        if (!ignoreGamepad) {
            if (useNewInputSystem) {
                if (isAllowKeyboardAndGamepad ()) {
                    usingKeyBoard = true;
                }

                if (editInputMenuOpened) {
                    if (usingGamepad) {
                        if (!previouslyUsingGamepad) {
                            previouslyUsingGamepad = true;

                            reloadInputMenu ();
                        }
                    } else {
                        if (previouslyUsingGamepad) {
                            previouslyUsingGamepad = false;

                            reloadInputMenu ();
                        }
                    }
                }

                if (touchPlatform && allowGamepadInTouchDevice) {
                    if (rescanPossibleGamepadsActive && Time.time > lastTimeStart + 0.4f) {
                        print ("checking for possible gamepads conencted");

                        if (numberOfGamepads == 0) {
                            print ("no gamepads stored, checking");

                            setGamePadList ();
                        }

                        rescanPossibleGamepadsActive = false;
                    }
                }
            } else {
                if (usingGamepad) {
                    if (Time.time > checkConnectedGamepadRate + lastGamepadUpdateCheckTime) {
                        joystickNames = GetJoystickNames ();
                    }
                }

                if (Time.time > checkConnectedGamepadRate + lastGamepadUpdateCheckTime || gameManagerPaused) {
                    if (usingGamepad) {
                        currentGamepadConnectedNameArray = joystickNames;
                    } else {
                        currentGamepadConnectedNameArray = GetJoystickNames ();
                    }

                    lastGamepadUpdateCheckTime = Time.time;

                    currentGamepadConnectedNameList.Clear ();

                    int currentGamepadConnectedNameArrayLength = currentGamepadConnectedNameArray.Length;

                    for (int i = 0; i < currentGamepadConnectedNameArrayLength; ++i) {
                        if (!currentGamepadConnectedNameArray [i].Equals (emptyGamepadName)) {
                            currentGamepadConnectedNameList.Add (currentGamepadConnectedNameArray [i]);
                        }
                    }

                    int currentGamepadConnectedNameListCount = currentGamepadConnectedNameList.Count;

                    currentGamepadConnectedNameArray = new string [currentGamepadConnectedNameListCount];

                    for (int i = 0; i < currentGamepadConnectedNameListCount; i++) {
                        currentGamepadConnectedNameArray [i] = currentGamepadConnectedNameList [i];
                    }

                    if (numberOfGamepads != currentGamepadConnectedNameArray.Length) {
                        setGamePadList ();
                    }

                    if (isAllowKeyboardAndGamepad ()) {
                        usingKeyBoard = true;
                    }

                    if (usingGamepad) {
                        if (!previouslyUsingGamepad) {
                            //						print ("Gamepad added");

                            previouslyUsingGamepad = true;

                            if (editInputMenuOpened) {
                                reloadInputMenu ();
                            }
                        }
                    } else {
                        if (previouslyUsingGamepad) {
                            //						print ("Gamepad removed");

                            previouslyUsingGamepad = false;

                            if (editInputMenuOpened) {
                                reloadInputMenu ();
                            }
                        }
                    }
                }
            }
        }

        if (usingGamepad && ignoreGamepad) {
            if (useNewInputSystem) {

            } else {
                currentGamepadConnectedNameArray = new String [0];
            }

            setGamePadList ();
        }

        if (usingTouchControls && usingGamepad && !allowGamepadInTouchDevice) {
            ignoreGamepad = true;
            usingKeyBoard = true;
        }
    }

    void updateRebindInputState ()
    {
        //if the player is changin this input field, search for any keyboard press
        if (editingInput) {
            if (usingGamepad) {
                bool checkPressedKey = false;

                string currentPressedKey = "";

                if (useNewInputSystem) {
#if ENABLE_INPUT_SYSTEM
                    foreach (var control in Gamepad.current.allControls) {
                        if (control is ButtonControl button) {
                            if (button.wasPressedThisFrame) {
                                if (!checkPressedKey) {
                                    if (InputAxisWrapper.TryGetGamepadButtonString (button, out currentPressedKey)) {

                                        if (currentPressedKey != "") {
                                            checkPressedKey = true;
                                        }

                                        Debug.Log ("Pulsado: " + button.name + " " + currentPressedKey);
                                    }
                                }
                            }
                        }
                    }

                    if (checkPressedKey) {
                        currentEditButtonInputToRebind.currentActionKeyString = currentPressedKey;

                        print ("currentPressedKey " + currentPressedKey);

                        currentEditButtonInputToRebind.actionKeyText.text = getLocalizedText (currentPressedKey);

                        if (showPressedKeyWhenEditingInput) {
                            print (currentPressedKey);
                        }

                        newKeyAssigned = true;

                        //stop the checking of the keyboard
                        setEditingInputState (false);
                    }
#endif
                } else {
                    foreach (KeyCode vKey in validKeyCodes) {
                        //set the value of the key pressed in the input field
                        if (InputAxisWrapper.getOldKeyDown (vKey)) {
                            currentPressedKey = vKey.ToString ();
                            if (!currentPressedKey.Contains ("Alpha")) {
                                checkPressedKey = true;
                            }
                        }
                    }

                    currentAxisValue = InputAxisWrapper.getOldInputAxisValue ("Left Trigger " + "1");

                    if (currentAxisValue != 0) {
                        currentPressedKey = "14";
                        checkPressedKey = true;
                    }

                    currentAxisValue = InputAxisWrapper.getOldInputAxisValue ("Right Trigger " + "1");

                    if (currentAxisValue != 0) {
                        currentPressedKey = "15";
                        checkPressedKey = true;
                    }

                    currentAxisValue = InputAxisWrapper.getOldInputAxisValue ("DPad X" + "1");

                    if (currentAxisValue != 0) {
                        if (currentAxisValue < 0) {
                            currentPressedKey = "10";
                        } else {
                            currentPressedKey = "11";
                        }

                        checkPressedKey = true;
                    }

                    currentAxisValue = InputAxisWrapper.getOldInputAxisValue ("DPad Y" + "1");

                    if (currentAxisValue != 0) {
                        if (currentAxisValue < 0) {
                            currentPressedKey = "12";
                        } else {
                            currentPressedKey = "13";
                        }

                        checkPressedKey = true;
                    }


                    if (checkPressedKey) {
                        string keyName = "";
                        int keyNumber = -1;

                        string [] numbers = Regex.Split (currentPressedKey, @"\D+");
                        foreach (string value in numbers) {
                            int number = 0;
                            if (int.TryParse (value, out number)) {
                                //							print (value + " " + value.Length);

                                keyNumber = number;
                            }
                        }

                        if (keyNumber > -1) {
                            keyName = Enum.GetName (typeof (joystickButtons), keyNumber);

                            currentEditButtonInputToRebind.currentActionKeyString = keyName;

                            currentEditButtonInputToRebind.actionKeyText.text = getLocalizedText (keyName);

                            if (showPressedKeyWhenEditingInput) {
                                print (keyName);
                            }

                            newKeyAssigned = true;

                            //stop the checking of the keyboard
                            setEditingInputState (false);
                        }

                        return;
                    }
                }
            } else {
                bool pressedKeyFound = false;

                string pressedKeyName = "";

                bool pressedKeyIsMouseButton = false;

                if (useNewInputSystem) {
#if ENABLE_INPUT_SYSTEM
                    foreach (KeyControl key in mainKeyboard.allKeys) {
                        if (key.wasPressedThisFrame) {
                            pressedKeyFound = true;

                            pressedKeyName = key.keyCode.ToString ();
                        }
                    }

                    foreach (var control in mainMouse.allControls) {
                        if (control is ButtonControl button &&
                            button.wasPressedThisFrame) {
                            pressedKeyFound = true;

                            pressedKeyName = control.name.ToString ();

                            pressedKeyIsMouseButton = true;
                        }
                    }

                    if (pressedKeyIsMouseButton) {
                        string temporalButtonName = InputAxisWrapper.convertControlButtonToMouseButtonName (pressedKeyName);

                        if (temporalButtonName != "") {
                            pressedKeyName = temporalButtonName;
                        }
                    }
#endif
                } else {
                    foreach (KeyCode vKey in validKeyCodes) {
                        //set the value of the key pressed in the input field
                        if (InputAxisWrapper.getOldKeyDown (vKey)) {
                            pressedKeyFound = true;

                            pressedKeyName = vKey.ToString ();

                        }
                    }
                }

                if (pressedKeyFound) {

                    if (showPressedKeyWhenEditingInput) {
                        print (pressedKeyName);
                    }

                    bool canAssignNewInput = true;

                    if (checkInputKeyDuplicatedOnRebindAction) {

                        string currentActionKey = pressedKeyName;

                        string currentActionName =
                            multiAxesList [currentEditButtonInputToRebind.multiAxesIndex].axes [currentEditButtonInputToRebind.axesIndex].Name;

                        for (int i = 0; i < multiAxesList.Count; i++) {
                            if (canAssignNewInput &&
                                (!checkInputKeyDuplicatedOnlyOnSameInputCategory || currentEditButtonInputToRebind.multiAxesIndex == i)) {
                                List<Axes> currentAxesList = multiAxesList [i].axes;

                                for (int j = 0; j < currentAxesList.Count; j++) {

                                    Axes currentAxes = currentAxesList [j];

                                    if (canAssignNewInput) {
                                        bool sameKeyFoundResult = currentAxes.keyButton.Equals (currentActionKey);

                                        if (sameKeyFoundResult) {

                                            if (currentAxes.Name != currentActionName) {
                                                canAssignNewInput = false;

                                                print ("Key " + currentActionKey + " already assigned on the action " + currentAxes.Name);
                                            }
                                        }
                                    }
                                }
                            }
                        }

                        if (canAssignNewInput && checkInputKeyDuplicatedOnlyOnSameInputCategory) {
                            print ("No key " + currentActionKey + " assigned to other action on the category " +
                                multiAxesList [currentEditButtonInputToRebind.multiAxesIndex].axesName + ".\n" +
                            "New key assigned to action " + currentActionName);
                        }
                    }

                    if (canAssignNewInput) {
                        if (useNewInputSystem) {
#if ENABLE_INPUT_SYSTEM
                            Axes currentAxes = multiAxesList [currentEditButtonInputToRebind.multiAxesIndex].axes [currentEditButtonInputToRebind.axesIndex];

                            //print ("current key use mouse value " + currentAxes.useMouseButtonType);

                            //currentAxes.useMouseButtonType = pressedKeyIsMouseButton;

                            currentEditButtonInputToRebind.useMouseButtonType = pressedKeyIsMouseButton;
#endif
                        }

                        currentEditButtonInputToRebind.currentActionKeyString = pressedKeyName;

                        string actionKeyTextString = pressedKeyName;

                        if (currentEditButtonInputToRebind.useMouseButtonType) {
                            actionKeyTextString = InputAxisWrapper.convertMouseButtonToFullName (actionKeyTextString);
                        }

                        currentEditButtonInputToRebind.actionKeyText.text = getLocalizedText (actionKeyTextString);

                        //stop the checking of the keyboard
                        setEditingInputState (false);

                        newKeyAssigned = true;

                        return;
                    }

                }
            }
        }
    }

    public void updatePlayerInputManagerList ()
    {
        for (int i = 0; i < playerInputManagerList.Count; i++) {
            if (playerInputManagerList [i] != null) {
                playerInputManagerList [i].findInputManagerAndUpdateComponent ();
            }
        }

        if (pauseManager == null && playerInputManagerList.Count > 0) {
            menuPause temporalMenuPause = charactersManager.getPauseManagerFromPlayerByIndex (0);

            if (temporalMenuPause != null) {
                setMainPlayerInfo (playerInputManagerList [0], temporalMenuPause);

                updateComponent ();
            }
        }
    }

    public void updatePlayerInputManagerListFromPlayerCharactersManager ()
    {
        playerInputManagerList.Clear ();

        for (int i = 0; i < charactersManager.playerList.Count; i++) {
            playerInputManagerList.Add (charactersManager.playerList [i].playerInput);

            charactersManager.playerList [i].playerInput.findInputManagerAndUpdateComponent ();
        }

        updateComponent ();
    }


    void checkPlayerInputManagerList ()
    {
        bool checkIfEmptyInputElements = checkNullElementsOnPlayerInputManagerList ();

        if (checkIfEmptyInputElements) {
            print ("checking");

            charactersManager.checkPlayerElementsOnScene ();

            playerInputManagerList.Clear ();

            for (int i = 0; i < charactersManager.playerList.Count; i++) {
                playerInputManagerList.Add (charactersManager.playerList [i].playerInput);
            }

            pauseManager = charactersManager.getPauseManagerFromPlayerByIndex (0);

            if (playerInputManagerList.Count >= 1) {
                playerInputManager currentPlayerInputManager = playerInputManagerList [0];

                if (currentPlayerInputManager != null) {
                    GameObject currentInputPanelListTextGameObject = currentPlayerInputManager.getCurrentInputPanelListText ();

                    if (currentInputPanelListTextGameObject != null) {
                        currentInputPanelListText = currentInputPanelListTextGameObject.GetComponent<Text> ();
                    }

                    editInputPanelPrefab = currentPlayerInputManager.getEditInputPanelPrefab ();

                    editInputMenu = currentPlayerInputManager.getEditInputMenu ();
                }
            }
        }
    }

    public void setMainPlayerInfo (playerInputManager newPlayerInputManager, menuPause newMenuPause)
    {
        if (!playerInputManagerList.Contains (newPlayerInputManager)) {

            playerInputManagerList.Add (newPlayerInputManager);
        }

        pauseManager = newMenuPause;

        GameObject currentInputPanelListTextGameObject = newPlayerInputManager.getCurrentInputPanelListText ();

        if (currentInputPanelListTextGameObject != null) {
            currentInputPanelListText = currentInputPanelListTextGameObject.GetComponent<Text> ();
        }

        editInputPanelPrefab = newPlayerInputManager.getEditInputPanelPrefab ();

        editInputMenu = newPlayerInputManager.getEditInputMenu ();
    }

    public bool isUseNewInputSystemActive ()
    {
        return useNewInputSystem;
    }

    public bool isUsingGamepad ()
    {
        return usingGamepad;
    }

    public bool isUsingKeyBoard ()
    {
        return usingKeyBoard;
    }

    public bool isUsingTouchControls ()
    {
        return usingTouchControls;
    }

    public bool isUsingTouchControlsOnGameManager ()
    {
        return mainGameManager.isUsingTouchControls ();
    }

    public bool isGameManagerPaused ()
    {
        return gameManagerPaused;
    }

    public bool isAllowKeyboardAndGamepad ()
    {
        if (usingGamepad) {
            if (getNumberOfGamepads () > 1) {
                return false;
            }
        }

        return allowKeyboardAndGamepad;
    }

    public int getNumberOfGamepads ()
    {
        return numberOfGamepads;
    }

    public bool isAllowGamepadInTouchDevice ()
    {
        return allowGamepadInTouchDevice;
    }

    public void setEditInputMenuOpenedState (bool state)
    {
        editInputMenuOpened = state;

        if (state) {
            newKeyAssigned = false;
        }
    }

    bool inputListMenuPanelInstantiated;

    //Update current input panel to edit
    public void openInputMenu ()
    {
        if (!showActionKeysOnMenu) {
            return;
        }

        if (!inputListMenuPanelInstantiated) {
            //get the current list of axes defined in the inspector
            getCurrentAxesListFromInspector ();

            inputListMenuPanelInstantiated = true;
        }

        currentInputPanelListIndex = -1;

        setNextInputPanel ();

        updateCurrentInputPanel (currentInputPanelListIndex);

        setKeyNamesInActionsList ();
    }

    public void reloadInputMenu ()
    {
        updateCurrentInputPanel (currentInputPanelListIndex);

        setKeyNamesInActionsList ();
    }

    public void setKeyNamesInActionsList ()
    {
        for (int i = 0; i < multiAxesEditButtonInputList.Count; i++) {

            multiAxesEditButtonInput currentEditButton = multiAxesEditButtonInputList [i];

            if (currentEditButton.multiAxesEditPanelActive) {
                for (int j = 0; j < currentEditButton.buttonsList.Count; j++) {

                    editButtonInput currentEditButtonInput = currentEditButton.buttonsList [j];

                    if (currentEditButtonInput.editButtonInputActive) {
                        string currentActionName =
                            multiAxesList [currentEditButtonInput.multiAxesIndex].axes [currentEditButtonInput.axesIndex].Name;

                        currentEditButtonInput.actionNameText.text = getLocalizedText (currentActionName);

                        if (usingGamepad) {
                            currentEditButtonInput.currentActionKeyString = currentEditButtonInput.gamepadActionKey;

                            currentEditButtonInput.actionKeyText.text = getLocalizedText (currentEditButtonInput.gamepadActionKey);
                        } else {
                            currentEditButtonInput.currentActionKeyString = currentEditButtonInput.keyboardActionKey;

                            string actionKeyTextString = currentEditButtonInput.keyboardActionKey;

                            if (currentEditButtonInput.useMouseButtonType) {
                                actionKeyTextString = InputAxisWrapper.convertMouseButtonToFullName (actionKeyTextString);
                            }

                            currentEditButtonInput.actionKeyText.text = getLocalizedText (actionKeyTextString);
                        }
                    }
                }
            }
        }
    }

    void reloadInputActionTextOnAllInputPanels ()
    {
        for (int i = 0; i < multiAxesEditButtonInputList.Count; i++) {

            multiAxesEditButtonInput currentEditButton = multiAxesEditButtonInputList [i];

            if (currentEditButton.multiAxesEditPanelActive) {
                for (int j = 0; j < currentEditButton.buttonsList.Count; j++) {

                    editButtonInput currentEditButtonInput = currentEditButton.buttonsList [j];

                    if (currentEditButtonInput.editButtonInputActive) {

                        Axes currentAxes = multiAxesList [currentEditButtonInput.multiAxesIndex].axes [currentEditButtonInput.axesIndex];


                        currentEditButtonInput.keyboardActionKey = currentAxes.keyButton;

                        if (useNewInputSystem) {
#if ENABLE_INPUT_SYSTEM
                            if (currentAxes.gamepadButton != newGamepadButtonNone) {
                                currentEditButtonInput.gamepadActionKey = currentAxes.gamepadButton.ToString ();
                            } else {
                                currentEditButtonInput.gamepadActionKey = "None";
                            }
#endif
                        } else {
                            if (currentAxes.joystickButton != joystickButtons.None) {
                                currentEditButtonInput.gamepadActionKey = currentAxes.joystickButton.ToString ();
                            }
                        }

                        currentEditButtonInput.useMouseButtonType = currentAxes.useMouseButtonType;

                        if (usingGamepad) {
                            currentEditButtonInput.currentActionKeyString = currentEditButtonInput.gamepadActionKey;

                            currentEditButtonInput.actionKeyText.text = getLocalizedText (currentEditButtonInput.gamepadActionKey);
                        } else {
                            currentEditButtonInput.currentActionKeyString = currentEditButtonInput.keyboardActionKey;

                            string actionKeyTextString = currentEditButtonInput.keyboardActionKey;

                            if (currentEditButtonInput.useMouseButtonType) {
                                actionKeyTextString = InputAxisWrapper.convertMouseButtonToFullName (actionKeyTextString);
                            }

                            currentEditButtonInput.actionKeyText.text = getLocalizedText (actionKeyTextString);
                        }
                    }
                }
            }
        }
    }

    public void updateCurrentInputPanel (int index)
    {
        ScrollRect currentScrollRect = null;

        for (int i = 0; i < multiAxesEditButtonInputList.Count; i++) {
            if (i == index) {
                if (!multiAxesEditButtonInputList [i].multiAxesEditPanel.activeSelf) {
                    multiAxesEditButtonInputList [i].multiAxesEditPanel.SetActive (true);
                }

                currentScrollRect = multiAxesEditButtonInputList [i].multiAxesEditPanelScrollRect;
            } else {
                if (multiAxesEditButtonInputList [i].multiAxesEditPanel.activeSelf) {
                    multiAxesEditButtonInputList [i].multiAxesEditPanel.SetActive (false);
                }
            }
        }

        if (currentInputPanelListText != null) {
            currentInputPanelListText.text = getLocalizedText (multiAxesEditButtonInputList [index].multiAxesEditPanel.name);
        }

        //get the scroller in the edit input menu
        if (editInputMenu != null) {
            mainInputMenuScrollerLocated = scroller != null;

            if (mainInputMenuScrollerLocated) {
                scroller = editInputMenu.GetComponentInChildren<Scrollbar> ();

                mainInputMenuScrollerLocated = scroller != null;
            }

            if (mainInputMenuScrollerLocated) {
                resetScroll (scroller);
            }

            ////set the scroller in the top position
            //scroller.value = 1;

            if (currentScrollRect != null) {
                resetPanelSlotsRectTransform (currentScrollRect.transform, currentScrollRect);
            }
        }
    }

    public void resetScroll (Scrollbar scrollBarToReset)
    {
        StartCoroutine (resetScrollCoroutine (scrollBarToReset));
    }

    IEnumerator resetScrollCoroutine (Scrollbar scrollBarToReset)
    {
        yield return new WaitForEndOfFrame ();
        yield return new WaitForEndOfFrame ();

        scrollBarToReset.value = 1;
    }

    public void resetPanelSlotsRectTransform (Transform panelTransform, ScrollRect panelScrollRect)
    {
        StartCoroutine (resetRectTransformCoroutine (panelTransform, panelScrollRect));
    }

    IEnumerator resetRectTransformCoroutine (Transform panelTransform, ScrollRect panelScrollRect)
    {
        LayoutRebuilder.ForceRebuildLayoutImmediate (panelTransform.GetComponent<RectTransform> ());

        yield return new WaitForEndOfFrame ();
        yield return new WaitForEndOfFrame ();

        if (panelScrollRect != null) {
            panelScrollRect.verticalNormalizedPosition = 1;
        }
    }

    public void setNextInputPanel ()
    {
        currentInputPanelListIndex++;
        if (currentInputPanelListIndex > multiAxesList.Count - 1) {
            currentInputPanelListIndex = 0;
        }

        int max = 0;
        bool exit = false;
        while (!exit) {
            if (multiAxesEditButtonInputList [currentInputPanelListIndex].multiAxesEditPanelActive) {
                exit = true;
            } else {

                max++;
                if (max > 100) {
                    exit = true;
                }

                currentInputPanelListIndex++;
                if (currentInputPanelListIndex > multiAxesList.Count - 1) {
                    currentInputPanelListIndex = 0;
                }
            }
        }

        updateCurrentInputPanel (currentInputPanelListIndex);
    }

    public void setPreviousInputPanel ()
    {
        currentInputPanelListIndex--;
        if (currentInputPanelListIndex < 0) {
            currentInputPanelListIndex = multiAxesList.Count - 1;
        }

        int max = 0;
        bool exit = false;
        while (!exit) {
            if (multiAxesEditButtonInputList [currentInputPanelListIndex].multiAxesEditPanelActive) {
                exit = true;
            } else {

                max++;
                if (max > 100) {
                    exit = true;
                }

                currentInputPanelListIndex--;
                if (currentInputPanelListIndex < 0) {
                    currentInputPanelListIndex = multiAxesList.Count - 1;
                }
            }
        }

        updateCurrentInputPanel (currentInputPanelListIndex);
    }

    public void destroyRebindMenusAndActionsPanels ()
    {
        if (!Application.isPlaying) {
            GKC_Utils.unpackPrefabObjectByCheckingTransformRoot (gameObject);
        }

        for (int i = 0; i < multiAxesEditButtonInputList.Count; i++) {
            if (multiAxesEditButtonInputList [i].multiAxesEditPanel != null) {
                DestroyImmediate (multiAxesEditButtonInputList [i].multiAxesEditPanel);
            }
        }

        multiAxesEditButtonInputList.Clear ();

        updateComponent ();
    }

    public void instantiateRebindMenusAndActionsPanels (bool updateComponentActive)
    {
        if (multiAxesEditButtonInputList.Count > 0) {
            destroyRebindMenusAndActionsPanels ();
        }

        int amountOfMenuPanels = multiAxesList.Count;
        int amountOfMenuActionsPerPanel = 0;

        if (!createRebindInputMenuWithCurrentAxes) {
            amountOfMenuPanels = rebindInputMenuPanelsAmount;
            amountOfMenuActionsPerPanel = rebindInputMenuActionsPerPanelAmount;
        }

        if (amountOfMenuPanels == 0) {
            print ("WARNING: Make sure to configure an amount of panels or use the current multi axes list amount");

            return;
        }

        if (editInputPanelPrefab == null) {
            return;
        }

        for (int i = 0; i < amountOfMenuPanels; i++) {
            GameObject newEditInputPanel = (GameObject)Instantiate (editInputPanelPrefab, editInputPanelPrefab.transform.position, Quaternion.identity, editInputPanelPrefab.transform.parent);

            newEditInputPanel.transform.localScale = Vector3.one;

            RectTransform newRectTransform = newEditInputPanel.GetComponent<RectTransform> ();
            RectTransform rectTransformPrefab = editInputPanelPrefab.GetComponent<RectTransform> ();

            newRectTransform.anchorMin = rectTransformPrefab.anchorMin;
            newRectTransform.anchorMax = rectTransformPrefab.anchorMax;
            newRectTransform.anchoredPosition = rectTransformPrefab.anchoredPosition;
            newRectTransform.sizeDelta = rectTransformPrefab.sizeDelta;

            if (!newEditInputPanel.activeSelf) {
                newEditInputPanel.SetActive (true);
            }

            newEditInputPanel.name = "Input Panel " + (i + 1);

            editInputPanelInfo currentEditInputPanelInfo = newEditInputPanel.GetComponent<editInputPanelInfo> ();

            List<editButtonInput> buttonsList = new List<editButtonInput> ();

            GameObject bottom = currentEditInputPanelInfo.bottomGameObject;

            if (createRebindInputMenuWithCurrentAxes) {
                amountOfMenuActionsPerPanel = multiAxesList [i].axes.Count;
            }

            //get all the keys field inside the edit input menu
            for (int j = 0; j < amountOfMenuActionsPerPanel; j++) {
                //every key field in the edit input button has a editButtonInput component, so create every of them

                GameObject newInputButton = (GameObject)Instantiate (currentEditInputPanelInfo.editButtonInputGameObject,
                                                currentEditInputPanelInfo.editButtonInputGameObject.transform.position,
                                                Quaternion.identity, currentEditInputPanelInfo.buttonsParent);

                if (newInputButton.activeSelf) {
                    newInputButton.SetActive (false);
                }

                newInputButton.name = "Input Button " + (j + 1);

                editButtonInput currentEditButtonInput = newInputButton.GetComponent<editButtonInput> ();

                currentEditButtonInput.editButtonGameObject = newInputButton;

                newInputButton.transform.localScale = Vector3.one;

                buttonsList.Add (currentEditButtonInput);

                if (currentEditInputPanelInfo.editButtonInputGameObject.activeSelf) {
                    currentEditInputPanelInfo.editButtonInputGameObject.SetActive (false);
                }
            }

            multiAxesEditButtonInput newMultiAxesEditButtonInput = new multiAxesEditButtonInput ();

            newMultiAxesEditButtonInput.multiAxesEditPanel = newEditInputPanel;

            newMultiAxesEditButtonInput.multiAxesEditPanelScrollRect = currentEditInputPanelInfo.editInputPanelGameObject.GetComponent<ScrollRect> ();

            newMultiAxesEditButtonInput.buttonsList = buttonsList;

            multiAxesEditButtonInputList.Add (newMultiAxesEditButtonInput);

            if (newEditInputPanel.activeSelf) {
                newEditInputPanel.SetActive (false);
            }

            //set the empty element of the list in the bottom of the list
            bottom.transform.SetAsLastSibling ();

            bottom.SetActive (false);
        }

        if (updateComponentActive) {
            print ("Rebind Input Panels and actions buttons created in editor");

            updateComponent ();
        }
    }

    void getCurrentAxesListFromInspector ()
    {
        if (multiAxesEditButtonInputList.Count == 0) {
            createRebindInputMenuWithCurrentAxes = true;

            instantiateRebindMenusAndActionsPanels (false);
        }

        if (editInputMenu != null && !editInputMenu.activeSelf) {
            editInputMenu.SetActive (true);
        }

        if (editInputPanelPrefab != null) {
            editInputPanelPrefab.SetActive (false);
        }

        if (multiAxesEditButtonInputList.Count > 0) {
            for (int i = 0; i < multiAxesList.Count; i++) {
                if (multiAxesList [i].currentlyActive) {
                    multiAxesEditButtonInput currentMultiAxesEditButtonInput = multiAxesEditButtonInputList [i];

                    currentMultiAxesEditButtonInput.multiAxesEditPanelActive = multiAxesList [i].multiAxesEditPanelActive;

                    currentMultiAxesEditButtonInput.multiAxesEditPanel.name = multiAxesList [i].axesName;

                    if (currentMultiAxesEditButtonInput.multiAxesEditPanel.activeSelf != currentMultiAxesEditButtonInput.multiAxesEditPanelActive) {
                        currentMultiAxesEditButtonInput.multiAxesEditPanel.SetActive (currentMultiAxesEditButtonInput.multiAxesEditPanelActive);
                    }

                    List<Axes> currentAxesList = multiAxesList [i].axes;

                    //get all the keys field inside the edit input menu
                    for (int j = 0; j < currentAxesList.Count; j++) {
                        Axes currentAxes = currentAxesList [j];

                        if (useNewInputSystem) {
#if ENABLE_INPUT_SYSTEM
                            if (currentAxes.useMouseButtonType) {
                                currentAxes.keyButton = currentAxes.mouseButtonInput.ToString ();
                            } else {
                                currentAxes.keyButton = currentAxes.keyInput.ToString ();
                            }
#endif
                        } else {
                            currentAxes.keyButton = currentAxes.key.ToString ();
                        }

                        if (currentAxes.actionEnabled) {
                            //every key field in the edit input button has a editButtonInput component, so create every of them

                            currentMultiAxesEditButtonInput.buttonsList [j].name = currentAxes.Name;

                            editButtonInput currentEditButtonInput = currentMultiAxesEditButtonInput.buttonsList [j];

                            currentEditButtonInput.editButtonInputActive = true;

                            currentEditButtonInput.useMouseButtonType = currentAxes.useMouseButtonType;

                            currentEditButtonInput.actionNameText.text = getLocalizedText (currentAxes.Name);

                            string actionKeyTextString = currentAxes.keyButton;

                            if (currentEditButtonInput.useMouseButtonType) {
                                actionKeyTextString = InputAxisWrapper.convertMouseButtonToFullName (actionKeyTextString);
                            }

                            currentEditButtonInput.actionKeyText.text = getLocalizedText (actionKeyTextString);

                            currentEditButtonInput.currentActionKeyString = currentAxes.keyButton;

                            currentEditButtonInput.keyboardActionKey = currentAxes.keyButton;

                            if (useNewInputSystem) {
#if ENABLE_INPUT_SYSTEM
                                if (currentAxes.gamepadButton != newGamepadButtonNone) {
                                    currentEditButtonInput.gamepadActionKey = currentAxes.gamepadButton.ToString ();
                                } else {
                                    currentEditButtonInput.gamepadActionKey = "None";
                                }
#endif
                            } else {
                                if (currentAxes.joystickButton != joystickButtons.None) {
                                    currentEditButtonInput.gamepadActionKey = currentAxes.joystickButton.ToString ();
                                }
                            }

                            currentEditButtonInput.multiAxesIndex = i;
                            currentEditButtonInput.axesIndex = j;

                            if (currentAxes.showActionInRebindPanel) {
                                if (!currentEditButtonInput.editButtonGameObject.activeSelf) {
                                    currentEditButtonInput.editButtonGameObject.SetActive (true);
                                }
                            }
                        }
                    }
                }
            }
        }

        if (editInputMenu != null && editInputMenu.activeSelf) {
            editInputMenu.SetActive (false);
        }

        //set in every key field in the edit input menu with the stored key input for every field
        for (int i = 0; i < multiAxesEditButtonInputList.Count; i++) {
            multiAxesEditButtonInput currentEditButton = multiAxesEditButtonInputList [i];

            if (currentEditButton.multiAxesEditPanelActive) {
                for (int j = 0; j < multiAxesEditButtonInputList [i].buttonsList.Count; j++) {

                    editButtonInput currentEditButtonInput = currentEditButton.buttonsList [j];

                    if (currentEditButtonInput.editButtonInputActive) {
                        if (i <= multiAxesList.Count - 1 && j < multiAxesList [i].axes.Count - 1) {
                            currentEditButtonInput.currentActionKeyString = multiAxesList [i].axes [j].keyButton;

                            string actionKeyTextString = multiAxesList [i].axes [j].keyButton;

                            if (currentEditButtonInput.useMouseButtonType) {
                                actionKeyTextString = InputAxisWrapper.convertMouseButtonToFullName (actionKeyTextString);
                            }

                            currentEditButtonInput.actionKeyText.text = getLocalizedText (actionKeyTextString);
                        }
                    }
                }
            }
        }
    }

    public void saveButtonsInput ()
    {
        //for every key field in the edit input menu, save its value and change them in the inputManager inspector aswell
        if (Application.isPlaying) {
            for (int i = 0; i < multiAxesEditButtonInputList.Count; i++) {

                multiAxesEditButtonInput currentEditButton = multiAxesEditButtonInputList [i];

                if (currentEditButton.multiAxesEditPanelActive) {
                    for (int j = 0; j < currentEditButton.buttonsList.Count; j++) {

                        editButtonInput currentEditButtonInput = currentEditButton.buttonsList [j];

                        if (currentEditButtonInput.editButtonInputActive) {
                            string currentKeyAction = currentEditButtonInput.currentActionKeyString;

                            if (usingGamepad) {
                                if (!currentKeyAction.Equals ("")) {
                                    currentEditButtonInput.gamepadActionKey = currentKeyAction;

                                    changeGamepadKeyValue (currentKeyAction, currentEditButtonInput.multiAxesIndex, currentEditButtonInput.axesIndex);
                                }
                            } else {
                                currentEditButtonInput.keyboardActionKey = currentKeyAction;

                                changeKeyValue (currentKeyAction, currentEditButtonInput.multiAxesIndex, currentEditButtonInput.axesIndex,
                                    currentEditButtonInput.useMouseButtonType);
                            }
                        }
                    }
                }
            }
        }

        //create a list of axes to store it
        List<multiAxes> temporalMultiAxesList = new List<multiAxes> ();

        for (int i = 0; i < multiAxesList.Count; i++) {
            List<Axes> temporalAxesList = new List<Axes> ();
            List<Axes> currentAxesList = multiAxesList [i].axes;

            for (int j = 0; j < currentAxesList.Count; j++) {
                Axes currentAxes = currentAxesList [j];

                Axes axe = new Axes ();
                axe.Name = currentAxes.Name;
                axe.actionEnabled = currentAxes.actionEnabled;

                axe.key = currentAxes.key;

                if (useNewInputSystem) {
#if ENABLE_INPUT_SYSTEM
                    if (currentAxes.useMouseButtonType) {
                        axe.keyButton = currentAxes.mouseButtonInput.ToString ();
                    } else {
                        axe.keyButton = currentAxes.keyInput.ToString ();
                    }
#endif
                } else {
                    axe.keyButton = currentAxes.key.ToString ();
                }

                axe.joystickButton = currentAxes.joystickButton;

                axe.touchButtonIndex = currentAxes.touchButtonIndex;
                axe.touchButtonPanelIndex = currentAxes.touchButtonPanelIndex;

                axe.showActionInRebindPanel = currentAxes.showActionInRebindPanel;

#if ENABLE_INPUT_SYSTEM
                axe.keyInput = currentAxes.keyInput;
                axe.useMouseButtonType = currentAxes.useMouseButtonType;
                axe.mouseButtonInput = currentAxes.mouseButtonInput;

                axe.gamepadButton = currentAxes.gamepadButton;
#endif

                temporalAxesList.Add (axe);
            }

            multiAxes newMultiAxes = new multiAxes (multiAxesList [i]);
            newMultiAxes.axesName = multiAxesList [i].axesName;
            newMultiAxes.axes = temporalAxesList;
            temporalMultiAxesList.Add (newMultiAxes);
        }

        //save the input list
        string path = getDataPath (saveFileName);

        SaveSlotListWrapper saveData = new SaveSlotListWrapper ();

        saveData.infoList = temporalMultiAxesList;

        string jsonToSave = JsonUtility.ToJson (saveData, true);

        saveFileContent (path, jsonToSave);

        print ("Input Saved in path" + getDataPath (saveFileName));
    }

    public string getDataPath (string fileNameToUse)
    {
        string dataPath = "";

        if (useRelativePath) {
            dataPath = saveFileFolderName;
        } else {
            dataPath = Application.persistentDataPath + "/" + saveFileFolderName;
        }

        if (useCustomInputFilePathName) {
            if (!Application.isPlaying) {
                dataPath = customInputFilePathName;
            }
        }

        if (!Directory.Exists (dataPath)) {
            Directory.CreateDirectory (dataPath);
        }

        dataPath += "/" + fileNameToUse;

        return dataPath;
    }

    public void loadButtonsInput ()
    {
        List<multiAxes> temporalMultiAxesList = new List<multiAxes> ();

        //if the configuration is loaded from a file, get a new axes list with the stored values
        if (loadOption == loadType.loadFile) {
            //print (saveFileName);

            //print (File.Exists (getDataPath (saveFileName)));

            //if the file of buttons exists, get that list
            string path = getDataPath (saveFileName);

            if (!touchPlatform && File.Exists (path)) {

                SaveSlotListWrapper saveData = new SaveSlotListWrapper ();

                if (getFileContent (path, out saveData, false)) {
                    temporalMultiAxesList = saveData.infoList;
                }

                multiAxesList.Clear ();

                for (int i = 0; i < temporalMultiAxesList.Count; i++) {
                    List<Axes> temporalAxesList = new List<Axes> ();

                    for (int j = 0; j < temporalMultiAxesList [i].axes.Count; j++) {
                        temporalAxesList.Add (temporalMultiAxesList [i].axes [j]);
                    }

                    // print (temporalMultiAxesList [i].axesName + " " + temporalMultiAxesList [i].multiAxesEditPanelActive);

                    multiAxes newMultiAxes = new multiAxes (temporalMultiAxesList [i]);

                    // print (newMultiAxes.axesName + " " + newMultiAxes.multiAxesEditPanelActive);

                    newMultiAxes.axesName = temporalMultiAxesList [i].axesName;

                    newMultiAxes.axes = temporalAxesList;

                    multiAxesList.Add (newMultiAxes);
                }
            }

            //else, get the list created in the inspector
            else {
                for (int i = 0; i < multiAxesList.Count; i++) {
                    List<Axes> temporalAxesList = new List<Axes> ();

                    List<Axes> currentAxesList = multiAxesList [i].axes;

                    for (int j = 0; j < currentAxesList.Count; j++) {
                        temporalAxesList.Add (currentAxesList [j]);
                    }

                    multiAxes newMultiAxes = new multiAxes (multiAxesList [i]);
                    newMultiAxes.axesName = multiAxesList [i].axesName;
                    newMultiAxes.axes = temporalAxesList;

                    temporalMultiAxesList.Add (newMultiAxes);
                }

                saveButtonsInputFromInspector (saveFileName);
            }
        }

        //else the new axes list is the axes in the input manager inspector
        else {
            for (int i = 0; i < multiAxesList.Count; i++) {
                List<Axes> currentAxesList = multiAxesList [i].axes;

                multiAxes newMultiAxes = new multiAxes (multiAxesList [i]);
                newMultiAxes.axesName = multiAxesList [i].axesName;
                newMultiAxes.axes = currentAxesList;

                temporalMultiAxesList.Add (newMultiAxes);
            }
        }

        for (int i = 0; i < multiAxesList.Count; i++) {
            List<Axes> currentAxesList = multiAxesList [i].axes;

            for (int j = 0; j < currentAxesList.Count; j++) {

                Axes currentAxes = currentAxesList [j];

                int currentPanelIndex = currentAxes.touchButtonPanelIndex;

                int currentButtonIndex = currentAxes.touchButtonIndex;

                if (currentPanelIndex > 0) {

                    if (touchButtonsInfoList.Count > currentPanelIndex - 1) {
                        if (touchButtonsInfoList [currentPanelIndex - 1].touchButtonList.Count > currentButtonIndex) {
                            touchButtonListener currentTouchButtonListener = touchButtonsInfoList [currentPanelIndex - 1].touchButtonList [currentButtonIndex];

                            currentAxes.currentTouchButtonIndex = touchButtonList.IndexOf (currentTouchButtonListener);

                            touchButtonListener touchButtonListenerToCheck = touchButtonList [currentAxes.currentTouchButtonIndex];

                            if (touchButtonListenerToCheck != null) {

                                GameObject currentTouchButtonToCheck = touchButtonListenerToCheck.gameObject;

                                if (currentTouchButtonToCheck != null && isInTouchButtonToDisableList (currentTouchButtonToCheck)) {
                                    if (currentTouchButtonToCheck.activeSelf) {
                                        currentTouchButtonToCheck.SetActive (false);
                                    }
                                }
                            } else {
                                currentAxes.touchButtonPanelIndex = 0;
                            }
                        } else {
                            print ("WARNING: The axes list " + multiAxesList [i].axesName + " is trying to use a touch button which is not correct, " +
                            "in the touch panel " + touchButtonsInfoList [currentPanelIndex - 1].Name
                            + ", make sure to check the touch button input option in the action" + currentAxes.Name);
                        }
                    } else {
                        print ("WARNING: The axes list " + multiAxesList [i].axesName + " is trying to use a touch panel which is not correct," +
                        " make sure to check the touch button input option in the action" + currentAxes.Name);
                    }
                }

                if (useNewInputSystem) {
#if ENABLE_INPUT_SYSTEM
                    if (currentAxes.useMouseButtonType) {
                        currentAxes.keyButton = currentAxes.mouseButtonInput.ToString ();
                    } else {
                        currentAxes.keyButton = currentAxes.keyInput.ToString ();
                    }
#endif
                } else {
                    currentAxes.keyButton = currentAxes.key.ToString ();
                }
            }
        }

        numberOfMultiAxes = multiAxesList.Count;

        multiAxesIndexList = new int [numberOfMultiAxes];

        for (int i = 0; i < numberOfMultiAxes; i++) {
            multiAxesIndexList [i] = multiAxesList [i].axes.Count;
        }

        numberOfTouchControls = touchButtonList.Count;
    }

    //save the input list in the inspector to a file
    public void saveButtonsInputFromInspector (string fileNameToUse)
    {
        //create a list of axes to store it
        List<multiAxes> temporalMultiAxesList = new List<multiAxes> ();

        for (int i = 0; i < multiAxesList.Count; i++) {
            List<Axes> temporalAxesList = new List<Axes> ();
            List<Axes> currentAxesList = multiAxesList [i].axes;

            for (int j = 0; j < currentAxesList.Count; j++) {
                Axes currentAxes = currentAxesList [j];

                Axes axe = new Axes ();
                axe.Name = currentAxes.Name;
                axe.actionEnabled = currentAxes.actionEnabled;

                axe.key = currentAxes.key;

                if (useNewInputSystem) {
#if ENABLE_INPUT_SYSTEM
                    if (currentAxes.useMouseButtonType) {
                        axe.keyButton = currentAxes.mouseButtonInput.ToString ();
                    } else {
                        axe.keyButton = currentAxes.keyInput.ToString ();
                    }
#endif
                } else {
                    axe.keyButton = currentAxes.key.ToString ();
                }

                axe.joystickButton = currentAxes.joystickButton;

                axe.touchButtonIndex = currentAxes.touchButtonIndex;
                axe.touchButtonPanelIndex = currentAxes.touchButtonPanelIndex;

                axe.showActionInRebindPanel = currentAxes.showActionInRebindPanel;

#if ENABLE_INPUT_SYSTEM
                axe.keyInput = currentAxes.keyInput;
                axe.useMouseButtonType = currentAxes.useMouseButtonType;
                axe.mouseButtonInput = currentAxes.mouseButtonInput;
                axe.gamepadButton = currentAxes.gamepadButton;
#endif

                temporalAxesList.Add (axe);
            }

            //print (multiAxesList [i].axesName + " " + multiAxesList [i].multiAxesEditPanelActive);

            multiAxes newMultiAxes = new multiAxes (multiAxesList [i]);

            //print (newMultiAxes.axesName + " " + newMultiAxes.multiAxesEditPanelActive);

            newMultiAxes.axesName = multiAxesList [i].axesName;
            newMultiAxes.axes = temporalAxesList;

            temporalMultiAxesList.Add (newMultiAxes);
        }

        //save the input list
        string path = getDataPath (fileNameToUse);

        SaveSlotListWrapper saveData = new SaveSlotListWrapper ();

        saveData.infoList = temporalMultiAxesList;

        string jsonToSave = JsonUtility.ToJson (saveData, true);

        saveFileContent (path, jsonToSave);

        print ("Current Input Saved in path" + getDataPath (fileNameToUse));
    }

    //load the input list from the file to the inspector
    public void loadButtonsInputFromInspector (string fileNameToUse)
    {
        List<multiAxes> temporalMultiAxesList = new List<multiAxes> ();

        string path = getDataPath (fileNameToUse);

        if (File.Exists (path)) {
            SaveSlotListWrapper saveData = new SaveSlotListWrapper ();

            if (getFileContent (path, out saveData, false)) {
                temporalMultiAxesList = saveData.infoList;
            }
        } else {
            print ("File located in " + path + " doesn't exist, make sure the file is created previously to load it in the editor");

            return;
        }

        //print (axesList.Count);
        multiAxesList.Clear ();

        //set the touch button for every axes, if it had it
        for (int i = 0; i < temporalMultiAxesList.Count; i++) {

            List<Axes> temporalAxesList = new List<Axes> ();

            for (int j = 0; j < temporalMultiAxesList [i].axes.Count; j++) {
                temporalAxesList.Add (temporalMultiAxesList [i].axes [j]);
            }

            multiAxes newMultiAxes = new multiAxes (temporalMultiAxesList [i]);
            newMultiAxes.axesName = temporalMultiAxesList [i].axesName;
            newMultiAxes.axes = temporalAxesList;

            multiAxesList.Add (newMultiAxes);
        }

        print ("Input File Loaded from " + fileNameToUse);
        print ("Path used for this file is " + path);
    }

    public void getGameManagerSettings ()
    {
        saveFileFolderName = mainGameManager.getSaveInputFileFolderName ();

        saveFileName = mainGameManager.getSaveInputFileName () + " " + mainGameManager.getVersionNumber () + mainGameManager.getFileExtension ();

        defaultSaveFileName = mainGameManager.getDefaultInputSaveFileName () + " " + mainGameManager.getVersionNumber () + mainGameManager.getFileExtension ();

        useRelativePath = mainGameManager.useRelativePath;

        mainGameManager.setCurrentPersistentDataPath ();

        updateComponent ();
    }

    public void setCurrentInputToDefault ()
    {
        getGameManagerSettings ();

        loadButtonsInputFromInspector (defaultSaveFileName);

        updateInputInspector ();
    }

    public void loadDefaultInputSettingsOnRuntime ()
    {
        getGameManagerSettings ();

        loadButtonsInputFromInspector (defaultSaveFileName);

        reloadInputActionTextOnAllInputPanels ();

        if (saveInputValuesOnLoadDefaultEnabled) {
            saveButtonsInput ();
        }
    }

    public bool saveInputValuesOnLoadDefaultEnabled;

    public void loadButtonsInspectorFromSaveFile ()
    {
        getGameManagerSettings ();

        if (useCustomInputFileName && !Application.isPlaying) {
            string newNameFile = saveFileName;

            if (customInputFileName != null && customInputFileName != "") {
                newNameFile = customInputFileName + mainGameManager.getFileExtension ();
            }

            loadButtonsInputFromInspector (newNameFile);
        } else {
            loadButtonsInputFromInspector (saveFileName);
        }

        updateInputInspector ();
    }

    public void saveCurrentInputAsDefault ()
    {
        getGameManagerSettings ();

        saveButtonsInputFromInspector (defaultSaveFileName);

        updateComponent ();
    }

    public void saveButtonsInputToSaveFile ()
    {
        getGameManagerSettings ();

        if (useCustomInputFileName && !Application.isPlaying) {
            string newNameFile = saveFileName;

            if (customInputFileName != null && customInputFileName != "") {
                newNameFile = customInputFileName + mainGameManager.getFileExtension ();
            }

            saveButtonsInputFromInspector (newNameFile);
        } else {
            saveButtonsInputFromInspector (saveFileName);
        }

        updateComponent ();
    }

    public void showCustomInputFileList ()
    {
        if (customInputFileListAssigned) {
            customInputFileListAssigned = false;

            customInputFileList = new string [0];

            return;
        }

        if (!Directory.Exists (customInputFilePathName)) {
            Debug.Log ("WARNING: " + customInputFilePathName + " path doesn't exist, make sure the path is from an existing folder in the project");

            return;
        }

        string [] search_results = null;

        customInputFileListAssigned = false;

        search_results = System.IO.Directory.GetFiles (customInputFilePathName, "*.txt");

        print (search_results.Length);

        if (search_results.Length > 0) {
            customInputFileList = new string [search_results.Length];

            int currentInputFileIndex = 0;

            foreach (string file in search_results) {
                string nameFile = file;

                if (nameFile.Contains (customInputFilePathName)) {
                    nameFile = nameFile.Replace (customInputFilePathName, "");
                }

                if (nameFile.Contains (mainGameManager.getFileExtension ())) {
                    nameFile = nameFile.Replace (mainGameManager.getFileExtension (), "");
                }

                nameFile = Regex.Replace (nameFile, "[^0-9A-Za-z ,]", "");

                customInputFileList [currentInputFileIndex] = nameFile;

                print (nameFile);

                currentInputFileIndex++;
            }

            customInputFileListAssigned = true;
        } else {
            Debug.Log ("Input File not found in path " + customInputFilePathName);

            customInputFileList = new string [0];
        }
    }

    void changeKeyValue (string keyButton, int multiAxesIndex, int axesIndex, bool useMouseButtonType)
    {
        Axes currentAxes = multiAxesList [multiAxesIndex].axes [axesIndex];

        //print (currentAxes.Name + " " + keyButton);

        if (useNewInputSystem) {
#if ENABLE_INPUT_SYSTEM
            currentAxes.useMouseButtonType = useMouseButtonType;

            if (currentAxes.useMouseButtonType) {
                if (InputAxisWrapper.GetMouseButtonByName (keyButton, out var mouseButton)) {

                    currentAxes.mouseButtonInput = mouseButton;
                    currentAxes.keyButton = currentAxes.mouseButtonInput.ToString ();
                } else {
                    print ("changeKeyValue not found for mouse on new input " + keyButton);
                }

            } else {
                //replace with tryparse

                if (Enum.TryParse<Key> (keyButton, out Key key)) {
                    //Key key = (Key)System.Enum.Parse (typeof (Key), keyButton);

                    currentAxes.keyInput = key;
                    currentAxes.keyButton = currentAxes.keyInput.ToString ();
                } else {
                    print ("changeKeyValue not found for key on new input " + keyButton);
                }
            }
#endif
        } else {
            if (Enum.TryParse<KeyCode> (keyButton, out KeyCode newKeyCode)) {
                //KeyCode newKeyCode = (KeyCode)System.Enum.Parse (typeof (KeyCode), keyButton);

                currentAxes.key = newKeyCode;
                currentAxes.keyButton = currentAxes.key.ToString ();
            } else {
                print ("changeKeyValue not found for key on new input " + keyButton);
            }
        }
    }

    void changeGamepadKeyValue (string keyButton, int multiAxesIndex, int axesIndex)
    {
        Axes currentAxes = multiAxesList [multiAxesIndex].axes [axesIndex];

        //print (currentAxes.Name + " " + keyButton);

        if (useNewInputSystem) {
#if ENABLE_INPUT_SYSTEM
            if (InputAxisWrapper.TryParseGamepadButton (keyButton, out var gamepadButton)) {
                currentAxes.gamepadButton = gamepadButton;

                print ("gamepad button found for " + keyButton + " for action " + currentAxes.Name + " set now as " + currentAxes.gamepadButton.ToString ());
            } else {
                //print ("warning, no gamepad button found for " + keyButton + " for action " + currentAxes.Name);
            }
#endif
        } else {
            currentAxes.joystickButton = (joystickButtons)Enum.Parse (typeof (joystickButtons), keyButton);
        }
    }

    //get the key button value for an input field, using the action of the button
    public string getButtonKey (string actionName)
    {
        //print (actionName);

        for (int i = 0; i < multiAxesList.Count; i++) {
            List<Axes> currentAxesList = multiAxesList [i].axes;

            for (int j = 0; j < currentAxesList.Count; j++) {

                Axes currentAxes = currentAxesList [j];

                if (currentAxes.Name.Equals (actionName)) {
                    if ((usingGamepad && !usingKeyBoard) || lastButtonPressedOnGamepad) {
                        if (useNewInputSystem) {
#if ENABLE_INPUT_SYSTEM
                            if (currentAxes.gamepadButton != newGamepadButtonNone) {
                                return currentAxes.gamepadButton.ToString ();
                            }
#endif
                        } else {
                            if (currentAxes.joystickButton != joystickButtons.None) {
                                return currentAxes.joystickButton.ToString ();
                            }
                        }
                    }

                    return currentAxes.keyButton;
                }
            }
        }

        print ("WARNING: no input key found for the action called " + actionName);

        return "";
    }

#if ENABLE_INPUT_SYSTEM
    GamepadButton getGamepadButton (string actionName)
    {
        if (!useNewInputSystem) {
            return newGamepadButtonNone;
        }

        for (int i = 0; i < multiAxesList.Count; i++) {
            List<Axes> currentAxesList = multiAxesList [i].axes;

            for (int j = 0; j < currentAxesList.Count; j++) {

                Axes currentAxes = currentAxesList [j];

                if (currentAxes.Name.Equals (actionName)) {

                    return currentAxes.gamepadButton;
                }
            }
        }

        return newGamepadButtonNone;
    }
#endif

    public Sprite getButtonSprite (string actionName, int playerID)
    {
        if (!mainGamepadIconsDataLocated) {
            return null;
        }

#if ENABLE_INPUT_SYSTEM
        GamepadButton currentGamepadButton = getGamepadButton (actionName);

        if (currentGamepadButton == newGamepadButtonNone) {
            return null;
        }

        Gamepad currentGamapad = getGamepad (playerID);

        if (currentGamapad == null) {
            return null;
        }

        GamepadType currentGamepadType = mainGamepadIconsData.GetGamepadType (currentGamapad);

        var profile = mainGamepadIconsData.gamepads.Find (p => p.gamepadType == currentGamepadType);

        if (profile == null)
            return null;

        var button = profile.buttons.Find (b => b.button == currentGamepadButton);

        return button != null ? button.icon : null;
#else
        return null;
#endif
    }

    public string getButtonKeyStringByIndex (int multiAxesIndex, int axesIndex)
    {
        if (multiAxesList.Count < multiAxesIndex) {
            return "";
        }

        if (multiAxesList [multiAxesIndex].axes.Count < axesIndex) {
            return "";
        }

        Axes currentAxes = multiAxesList [multiAxesIndex].axes [axesIndex];

        if ((usingGamepad && !usingKeyBoard) || lastButtonPressedOnGamepad) {
            if (useNewInputSystem) {
#if ENABLE_INPUT_SYSTEM
                if (currentAxes.gamepadButton != newGamepadButtonNone) {
                    return currentAxes.gamepadButton.ToString ();
                }
#endif
            } else {
                if (currentAxes.joystickButton != joystickButtons.None) {
                    return currentAxes.joystickButton.ToString ();
                }
            }
        }

        if (useNewInputSystem) {
#if ENABLE_INPUT_SYSTEM
            if (currentAxes.useMouseButtonType) {
                return currentAxes.mouseButtonInput.ToString ();
            } else {
                return currentAxes.keyInput.ToString ();
            }
#else
            return "";
#endif
        } else {
            return currentAxes.key.ToString ();
        }
    }

    //if the input field has been pressed, call a coroutine, to avoid the input field get the mouse press as new value
    public void startEditingInput (GameObject button)
    {
        if (!editingInput) {
            StartCoroutine (startEditingInputCoroutine (button));
        }
    }

    //set the text of the input field to ... and start to check the keyboard press
    IEnumerator startEditingInputCoroutine (GameObject button)
    {
        //yield return null;

        //WaitForSeconds delay = new WaitForSeconds (0.3f);

        //yield return delay;


        yield return new WaitForEndOfFrame ();

        currentEditButtonInputToRebind = button.GetComponent<editButtonInput> ();

        currentEditButtonInputToRebind.actionKeyText.text = "...";

        setEditingInputState (true);

        //newKeyAssigned = false;
    }

    void setEditingInputState (bool state)
    {
        editingInput = state;

        if (editingInput) {

        } else {

        }

        for (int i = 0; i < playerInputManagerList.Count; i++) {
            if (playerInputManagerList [i] != null) {
                playerInputManagerList [i].setEditingInputState (state);
            }
        }

        charactersManager.setMinWaitTimeToReturnPauseGameFromGamepad ();
    }

    public bool isEditingInput ()
    {
        return editingInput;
    }

    //any change done in the input field is undone
    public void cancelEditingInput ()
    {
        setEditingInputState (false);

        newKeyAssigned = false;

        if (currentEditButtonInputToRebind != null) {
            currentEditButtonInputToRebind = null;

            reloadInputActionTextOnAllInputPanels ();
        }
    }

    string getLocalizedText (string textContent)
    {
        if (gameLanguageSelector.isCheckLanguageActive ()) {
            textContent = UIElementsLocalizationManager.GetLocalizedValue (textContent);
        }

        return textContent;
    }

    public void checkIfSaveInputAfterRebind ()
    {
        if (newKeyAssigned) {
            saveButtonsInput ();

            newKeyAssigned = false;
        }
    }

    public void getValidKeyCodes ()
    {
        validKeyCodes = (KeyCode [])System.Enum.GetValues (typeof (KeyCode));
    }

    public string getKeyPressed (buttonType type, bool canBeUsedOnGamePaused)
    {
        if ((!gameManagerPaused || canBeUsedOnGamePaused) && (!usingTouchControls || allowGamepadInTouchDevice)) {
            if (useNewInputSystem) {
#if ENABLE_INPUT_SYSTEM
                foreach (KeyControl key in mainKeyboard.allKeys) {
                    switch (type) {
                        //this key is for holding
                        case buttonType.getKey:
                            if (key.isPressed) {
                                return key.name.ToString ();
                            }
                            break;

                        case buttonType.getKeyDown:
                            //this key is for press once
                            if (key.wasPressedThisFrame) {
                                return key.name.ToString ();
                            }
                            break;

                        case buttonType.getKeyUp:
                            //this key is for release 
                            if (key.wasReleasedThisFrame) {
                                return key.name.ToString ();
                            }
                            break;
                    }
                }
#endif
            } else {
                foreach (KeyCode vKey in validKeyCodes) {
                    switch (type) {
                        //this key is for holding
                        case buttonType.getKey:
                            if (InputAxisWrapper.getOldKey (vKey)) {
                                return vKey.ToString ();
                            }
                            break;

                        case buttonType.getKeyDown:
                            //this key is for press once
                            if (InputAxisWrapper.getOldKeyDown (vKey)) {
                                return vKey.ToString ();
                            }
                            break;

                        case buttonType.getKeyUp:
                            //this key is for release 
                            if (InputAxisWrapper.getOldKeyUp (vKey)) {
                                return vKey.ToString ();
                            }
                            break;
                    }
                }
            }
        }

        return "";
    }

    public bool checkJoystickButton (string joystickString, buttonType type)
    {
        if (!usingTouchControls || allowGamepadInTouchDevice) {
            if ((!usingKeyBoard || isAllowKeyboardAndGamepad ())) {
                if (usingGamepad) {
                    switch (type) {
                        //this key is for holding
                        case buttonType.getKeyDown:
                            if (InputAxisWrapper.getOldKeyDown (joystickString)) {
                                return true;
                            }
                            break;

                        case buttonType.getKeyUp:
                            if (InputAxisWrapper.getOldKeyUp (joystickString)) {
                                return true;
                            }
                            break;
                    }
                }
            }
        }

        return false;
    }

    public bool checkPlayerInputButtonFromMultiAxesList (int multiAxesIndex, int axesIndex, buttonType type, int controllerNumber, bool canBeUsedOnPausedGame,
        bool useOnlyKeyboard, bool ignoreKeyboardInputActive)
    {
        if (usingTouchControls) {
            if (allowGamepadInTouchDevice && usingGamepad) {
                if (getTouchButtonFromMultiAxesList (multiAxesIndex, axesIndex, type)) {
                    return true;
                }
            } else {
                return getTouchButtonFromMultiAxesList (multiAxesIndex, axesIndex, type);
            }
        }

#if REWIRED
		if (useRewired) {
			return getJoystickButtonFromMultiAxesList(multiAxesIndex, axesIndex, type, controllerNumber, canBeUsedOnPausedGame);
		}
#endif

        if ((!usingKeyBoard || isAllowKeyboardAndGamepad ()) && !useOnlyKeyboard) {
            if (usingGamepad) {
                if (getJoystickButtonFromMultiAxesList (multiAxesIndex, axesIndex, type, controllerNumber, canBeUsedOnPausedGame)) {

                    if (!lastButtonPressedOnGamepad) {
                        //						print ("pressing gamepad");

                        GKC_Utils.eventOnPressingGamepadInput (controllerNumber);

                        lastButtonPressedOnGamepad = true;
                    }

                    lastKeyPressedOnKeyboard = false;

                    return true;
                }
            }
        }

        if (usingTouchControls && allowGamepadInTouchDevice && usingGamepad) {
            return false;
        }

        if (ignoreKeyboardInputActive) {
            return false;
        }

        if (usingKeyBoard || isAllowKeyboardAndGamepad () || useOnlyKeyboard) {
            if (getKeyboardButtonFromMultiAxesList (multiAxesIndex, axesIndex, type, canBeUsedOnPausedGame)) {

                if (!lastKeyPressedOnKeyboard) {
                    //					print ("pressing keyboard");

                    GKC_Utils.eventOnPressingKeyboardInput (controllerNumber);

                    lastKeyPressedOnKeyboard = true;
                }

                lastButtonPressedOnGamepad = false;

                return true;
            }
        }

        return false;
    }

    public bool GetMouseButtonDown (int mouseButtomNumber)
    {
        if (useNewInputSystem) {
#if ENABLE_INPUT_SYSTEM
            switch (mouseButtomNumber) {
                case 0:
                    return mainMouse.leftButton.wasPressedThisFrame;
                case 1:
                    return mainMouse.rightButton.wasPressedThisFrame;
                case 2:
                    return mainMouse.middleButton.wasPressedThisFrame;
                default:
                    return false;
            }
#else
            return false;
#endif
        } else {
            return InputAxisWrapper.getOldMouseButtonDown (mouseButtomNumber);
        }
    }

    public bool GetMouseButtonUp (int mouseButtomNumber)
    {
        if (useNewInputSystem) {
#if ENABLE_INPUT_SYSTEM
            switch (mouseButtomNumber) {
                case 0:
                    return mainMouse.leftButton.wasReleasedThisFrame;
                case 1:
                    return mainMouse.rightButton.wasReleasedThisFrame;
                case 2:
                    return mainMouse.middleButton.wasReleasedThisFrame;
                default:
                    return false;
            }
#else
            return false;
#endif
        } else {
            return InputAxisWrapper.getOldMouseButtonUp (mouseButtomNumber);
        }
    }

    string [] keyNumberListString = new string [] { "0", "1", "2", "3", "4", "5", "6", "7", "8", "9" };

    public int checkNumberInput (int numberAmount)
    {
        if (numberAmount > 10) {
            numberAmount = 10;
        }

        if (isAnyKeyDown ()) {
            for (int i = 0; i < numberAmount; i++) {
                if (useNewInputSystem) {
#if ENABLE_INPUT_SYSTEM
                    if (numberKeys [i].wasPressedThisFrame) {
                        return i;
                    }
#endif
                } else {
                    if (InputAxisWrapper.getOldKeyDown (keyNumberListString [i])) {
                        return i;
                    }
                }
            }
        }

        return -1;
    }

    public bool isAnyKeyDown ()
    {
        if (useNewInputSystem) {
#if ENABLE_INPUT_SYSTEM
            return mainKeyboard.anyKey.wasPressedThisFrame;
#else
            return false;
#endif
        } else {
            return InputAxisWrapper.isAnyOldKeyDown ();
        }
    }

#if ENABLE_INPUT_SYSTEM
    bool getMouseButtonKeyDown (MouseButton buttonToCheck)
    {
        switch (buttonToCheck) {
            case MouseButton.Left:
                return mainMouse.leftButton.wasPressedThisFrame;
            case MouseButton.Right:
                return mainMouse.rightButton.wasPressedThisFrame;
            case MouseButton.Middle:
                return mainMouse.middleButton.wasPressedThisFrame;
            default:
                return false;
        }
    }

    bool getMouseButtonKeyUp (MouseButton buttonToCheck)
    {
        switch (buttonToCheck) {
            case MouseButton.Left:
                return mainMouse.leftButton.wasReleasedThisFrame;
            case MouseButton.Right:
                return mainMouse.rightButton.wasReleasedThisFrame;
            case MouseButton.Middle:
                return mainMouse.middleButton.wasReleasedThisFrame;
            default:
                return false;
        }
    }

    bool getMouseButtonKey (MouseButton buttonToCheck)
    {
        switch (buttonToCheck) {
            case MouseButton.Left:
                return mainMouse.leftButton.isPressed;
            case MouseButton.Right:
                return mainMouse.rightButton.isPressed;
            case MouseButton.Middle:
                return mainMouse.middleButton.isPressed;
            default:
                return false;
        }
    }

#endif

#if ENABLE_INPUT_SYSTEM
    bool getKey (Key newKey)
    {
        //if (newKey == Key.None) {
        //    return false;
        //}

        return mainKeyboard [newKey].isPressed;
    }

    bool getKeyDown (Key newKey)
    {
        //if (newKey == Key.None) {
        //    return false;
        //}

        return mainKeyboard [newKey].wasPressedThisFrame;
    }

    bool getKeyUp (Key newKey)
    {
        //if (newKey == Key.None) {
        //    return false;
        //}

        return mainKeyboard [newKey].wasReleasedThisFrame;
    }
#endif


    bool getKey (KeyCode keyCode)
    {
        if (keyCode == KeyCode.None) {
            return false;
        }

#if ENABLE_INPUT_SYSTEM
        if (!keyCache.TryGetValue (keyCode, out var key)) {
            if (!System.Enum.TryParse<Key> (keyCode.ToString (), out key))
                return false;

            keyCache [keyCode] = key;
        }

        if (mainKeyboard [key].isPressed) {
            return true;
        }
#endif

        return false;
    }

    bool getKeyDown (KeyCode keyCode)
    {
        if (keyCode == KeyCode.None) {
            return false;
        }

#if ENABLE_INPUT_SYSTEM
        if (!keyCache.TryGetValue (keyCode, out var key)) {
            if (!System.Enum.TryParse<Key> (keyCode.ToString (), out key))
                return false;

            keyCache [keyCode] = key;
        }

        if (mainKeyboard [key].wasPressedThisFrame) {
            return true;
        }
#endif

        return false;
    }

    bool getKeyUp (KeyCode keyCode)
    {
        if (keyCode == KeyCode.None) {
            return false;
        }

#if ENABLE_INPUT_SYSTEM
        if (!keyCache.TryGetValue (keyCode, out var key)) {
            if (!System.Enum.TryParse<Key> (keyCode.ToString (), out key))
                return false;

            keyCache [keyCode] = key;
        }

        if (mainKeyboard [key].wasReleasedThisFrame) {
            return true;
        }
#endif

        return false;
    }

    public Vector2 getKeyboardAxisInputValue ()
    {
        if (useNewInputSystem) {
            return InputAxisWrapper.GetMoveAxis (newInputSystemMoveAxisSmoothSpeed);
        } else {
            return new Vector2 (InputAxisWrapper.getOldInputAxisValue (horizontalString), InputAxisWrapper.getOldInputAxisValue (verticalString));
        }
    }

    public Vector2 getKeyboardRawAxisInputValue ()
    {
        if (useNewInputSystem) {
            return InputAxisWrapper.GetMoveRawAxis ();
        } else {
            return new Vector2 (InputAxisWrapper.getOldInputAxisRawValue (horizontalString), InputAxisWrapper.getOldInputAxisRawValue (verticalString));
        }
    }

    public Vector2 getMouseAxisInputValue ()
    {
        if (useNewInputSystem) {
            return InputAxisWrapper.GetLookAxis (newInputSystemLookAxisSmoothSpeed);
        } else {
            return new Vector2 (InputAxisWrapper.getOldInputAxisValue (mouseXString), InputAxisWrapper.getOldInputAxisValue (mouseYString));
        }
    }

    public Vector2 getNewInputLeftJoysticksAxisInputValue (int controllerNumber)
    {
#if ENABLE_INPUT_SYSTEM
        if (usingGamepad) {
            if (newInputGamepadInfoList.TryGetValue (controllerNumber, out newInputGamepadInfo currentGamepad)) {
                if (currentGamepad.connected) {
                    return currentGamepad.gamepad.leftStick.ReadValue ();
                }
            }
        }
#endif

        return Vector2.zero;
    }

    public Vector2 getNewInputRightJoysticksAxisInputValue (int controllerNumber)
    {
#if ENABLE_INPUT_SYSTEM
        if (usingGamepad) {
            if (newInputGamepadInfoList.TryGetValue (controllerNumber, out newInputGamepadInfo currentGamepad)) {
                if (currentGamepad.connected) {
                    return currentGamepad.gamepad.rightStick.ReadValue ();
                }
            }
        }
#endif

        return Vector2.zero;
    }

#if ENABLE_INPUT_SYSTEM
    public bool checkNewGamepadButton (int controllerNumber, GamepadButton buttonToCheck, buttonType type)
    {
        if (usingGamepad) {
            if (newInputGamepadInfoList.TryGetValue (controllerNumber, out newInputGamepadInfo currentGamepad)) {
                if (currentGamepad.connected) {
                    switch (type) {

                        case buttonType.getKey:
                            //this key is for holding

                            return currentGamepad.gamepad [buttonToCheck].isPressed;

                        case buttonType.getKeyDown:
                            //this key is for press once

                            return currentGamepad.gamepad [buttonToCheck].wasPressedThisFrame;

                        case buttonType.getKeyUp:
                            //this key is for release 

                            return currentGamepad.gamepad [buttonToCheck].wasReleasedThisFrame;
                    }
                }
            }
        }

        return false;
    }

    public Gamepad getGamepad (int controllerNumber)
    {
        if (usingGamepad) {
            return newInputGamepadInfoList.TryGetValue (controllerNumber, out var pad) ? pad.gamepad : null;
        }

        return null;
    }
#endif

    public bool IsValidGamepad (int id)
    {
        if (usingGamepad) {
#if ENABLE_INPUT_SYSTEM
            return newInputGamepadInfoList.ContainsKey (id);
#else
        return false;
#endif
        }

        return false;
    }

    public void setGamepadVibrationByPlayerID (int playerID, float low, float high)
    {
        if (!newInputGamepadInfoList.TryGetValue (playerID, out var currentNewInputGamepadToCheck)) {
            return;
        }

#if ENABLE_INPUT_SYSTEM
        currentNewInputGamepadToCheck.gamepad.SetMotorSpeeds (low, high);
#endif
    }

    public void setGamepadVibrationByPlayerID (int playerID, float low, float high, float duration)
    {
        if (!newInputGamepadInfoList.TryGetValue (playerID, out var currentNewInputGamepadToCheck)) {
            return;
        }

        // cancel previous vibration
        if (currentNewInputGamepadToCheck.rumbleActive) {
            StopCoroutine (currentNewInputGamepadToCheck.rumbleCoroutine);

            currentNewInputGamepadToCheck.rumbleActive = false;
        }

        currentNewInputGamepadToCheck.rumbleCoroutine =
            StartCoroutine (setGamepadVibrationByPlayerIDCoroutine (currentNewInputGamepadToCheck, low, high, duration));
    }

    IEnumerator setGamepadVibrationByPlayerIDCoroutine (newInputGamepadInfo gamepadInfo, float low, float high, float duration)
    {
        gamepadInfo.rumbleActive = true;

#if ENABLE_INPUT_SYSTEM
        gamepadInfo.gamepad.SetMotorSpeeds (low, high);
#endif

        float timer = duration;

        bool wasPaused = false;

        while (timer > 0f) {
            if (gamepadInfo.gameIsPaused) {
                // stop motors once
                if (!wasPaused) {
#if ENABLE_INPUT_SYSTEM
                    gamepadInfo.gamepad.SetMotorSpeeds (0f, 0f);
#endif

                    wasPaused = true;
                }
            } else {
                // restore motors once
                if (wasPaused) {
#if ENABLE_INPUT_SYSTEM
                    gamepadInfo.gamepad.SetMotorSpeeds (low, high);
#endif

                    wasPaused = false;
                }

                timer -= Time.unscaledDeltaTime;
            }

            yield return null;
        }

#if ENABLE_INPUT_SYSTEM
        gamepadInfo.gamepad.SetMotorSpeeds (0f, 0f);
#endif
        gamepadInfo.rumbleCoroutine = null;

        gamepadInfo.rumbleActive = false;
    }

    public void stopGamepadVibrationByPlayerID (int playerID)
    {
        if (!newInputGamepadInfoList.TryGetValue (playerID, out var currentNewInputGamepadToCheck)) {
            return;
        }

        if (currentNewInputGamepadToCheck.rumbleActive) {
            StopCoroutine (currentNewInputGamepadToCheck.rumbleCoroutine);

            currentNewInputGamepadToCheck.rumbleActive = false;

#if ENABLE_INPUT_SYSTEM
            currentNewInputGamepadToCheck.gamepad.SetMotorSpeeds (0, 0);
#endif
        }
    }

    public void setGamePausedStateOnAllGamepads (bool state)
    {
        foreach (var value in newInputGamepadInfoList.Values) {
            value.gameIsPaused = state;
        }
    }

    public void setGamePausedStateOnGamepadByID (bool state, int playerID)
    {
        if (newInputGamepadInfoList.TryGetValue (playerID, out currentNewInputGamepadToCheck)) {
            currentNewInputGamepadToCheck.gameIsPaused = state;
        }
    }

    public void setVibrationMultiplier (float newValue)
    {
        for (int i = 0; i < playerInputManagerList.Count; i++) {
            if (playerInputManagerList [i] != null) {
                playerInputManagerList [i].setVibrationMultiplierOnMainSimpleGamepadVibrationHelper (newValue);
            }
        }
    }

    //function called in the script where pressing that button will make an action in the game, for example jump, crouch, shoot, etc...
    //every button sends its action and the type of pressing
    public bool getKeyboardButtonFromMultiAxesList (int multiAxesIndex, int axesIndex, buttonType type, bool canBeUsedOnPausedGame)
    {
        //if the game is not paused, and the current control is the keyboard
        if ((canBeUsedOnPausedGame || !gameManagerPaused) && (!usingTouchControls || allowGamepadInTouchDevice)) {

            if (multiAxesIndex >= numberOfMultiAxes) {
                if (showMissingInputWarningMessagesEnabled) {
                    print ("WARNING: The input manager is trying to access to an action which has changed or has been removed.  " +
                    "\n Please, check the Player Input Manager to configure properly this group of actions according to the " +
                    "settings in the main custom Input Manager of GKC." +
                    "\n Use the Update Input List button on Player Input Manager and make sure to remove any removed action from it where the Axe field appears as empty.\n\n");
                }

                return false;
            }

            if (multiAxesIndex < 0 || axesIndex < 0) {
                if (showMissingInputWarningMessagesEnabled) {
                    if (multiAxesIndex >= 0) {
                        print ("WARNING: The input manager is trying to access to an action which has changed or has been removed in the Group of actions" +
                        " called " + multiAxesList [multiAxesIndex].axesName + " \n Please, check the Player Input Manager to configure properly this group of actions according to the " +
                        "settings in the main custom Input Manager of GKC" +
                        "\n Use the Update Input List button on Player Input Manager and make sure to remove any removed action from it where the Axe field appears as empty.\n\n");
                    } else {
                        print ("WARNING: The input manager is trying to access to a multi axes which has changed or has been removed.  " +
                        "\n Please, check the Player Input Manager to configure properly this group of actions according to the " +
                        "settings in the main custom Input Manager of GKC" +
                        "\n Use the Update Input List button on Player Input Manager and make sure to remove any removed action from it where the Axe field appears as empty.\n\n");
                    }
                }

                return false;
            }

            if (axesIndex >= multiAxesIndexList [multiAxesIndex]) {
                if (showMissingInputWarningMessagesEnabled) {
                    print ("WARNING: The input manager is trying to access to an action which has changed or has been removed in the Group of actions" +
                    " called " + multiAxesList [multiAxesIndex].axesName + " \n Please, check the Player Input Manager to configure properly this group of actions according to the " +
                    "settings in the main custom Input Manager of GKC" +
                    "\n Use the Update Input List button on Player Input Manager and make sure to remove any removed action from it where the Axe field appears as empty.\n\n");
                }

                return false;
            }

            if (!multiAxesList [multiAxesIndex].currentlyActive) {
                return false;
            }

            currentKeyButtonToCheck = multiAxesList [multiAxesIndex].axes [axesIndex];

            //check that the key pressed has being defined as an action
            if (!currentKeyButtonToCheck.actionEnabled) {
                return false;
            }

            if (useNewInputSystem) {
#if ENABLE_INPUT_SYSTEM
                useMouseButtonType = currentKeyButtonToCheck.useMouseButtonType;

                if (useMouseButtonType) {
                    mouseButtonInput = currentKeyButtonToCheck.mouseButtonInput;
                }

                if (useNewInputKeys) {
                    currentKeyToCheck = currentKeyButtonToCheck.keyInput;

                    if (!useMouseButtonType && currentKeyToCheck == Key.None) {
                        return false;
                    }
                } else {
                    currentKeyCodeToCheck = currentKeyButtonToCheck.key;
                }

                switch (type) {
                    //this key is for holding
                    case buttonType.getKey:
                        if (useMouseButtonType) {
                            if (getMouseButtonKey (mouseButtonInput)) {
                                if (showKeyboardPressed) {
                                    print ("Get Mouse Button: " + mouseButtonInput.ToString ());
                                }

                                if (showKeyboardPressedAction) {
                                    print ("Action Name On Mouse Button: " + currentKeyButtonToCheck.Name);
                                }

                                return true;
                            }
                        } else {
                            if (useNewInputKeys) {
                                if (getKey (currentKeyToCheck)) {
                                    if (showKeyboardPressed) {
                                        print ("Get Key: " + currentKeyToCheck.ToString ());
                                    }

                                    if (showKeyboardPressedAction) {
                                        print ("Action Name On Key: " + currentKeyButtonToCheck.Name);
                                    }

                                    return true;
                                }
                            } else {
                                if (getKey (currentKeyCodeToCheck)) {
                                    if (showKeyboardPressed) {
                                        print ("Get Key: " + currentKeyButtonToCheck.keyButton);
                                    }

                                    if (showKeyboardPressedAction) {
                                        print ("Action Name On Key: " + currentKeyButtonToCheck.Name);
                                    }

                                    return true;
                                }
                            }
                        }

                        break;

                    //this key is for press once
                    case buttonType.getKeyDown:
                        if (useMouseButtonType) {
                            if (getMouseButtonKeyDown (mouseButtonInput)) {
                                if (showKeyboardPressed) {
                                    print ("Get Mouse Button Down: " + mouseButtonInput.ToString ());
                                }

                                if (showKeyboardPressedAction) {
                                    print ("Action Name On Mouse Button Down: " + currentKeyButtonToCheck.Name);
                                }

                                return true;
                            }
                        } else {
                            if (useNewInputKeys) {
                                if (getKeyDown (currentKeyToCheck)) {
                                    if (showKeyboardPressed) {
                                        print ("Get Key Down: " + currentKeyToCheck.ToString ());
                                    }

                                    if (showKeyboardPressedAction) {
                                        print ("Action Name On Key: " + currentKeyButtonToCheck.Name);
                                    }

                                    return true;
                                }
                            } else {
                                if (getKeyDown (currentKeyCodeToCheck)) {
                                    if (showKeyboardPressed) {
                                        print ("Get Key Down: " + currentKeyButtonToCheck.keyButton);
                                    }

                                    if (showKeyboardPressedAction) {
                                        print ("Action Name On Key Down: " + currentKeyButtonToCheck.Name);
                                    }

                                    return true;
                                }
                            }
                        }

                        break;

                    //this key is for release
                    case buttonType.getKeyUp:

                        if (useMouseButtonType) {
                            if (getMouseButtonKeyUp (mouseButtonInput)) {
                                if (showKeyboardPressed) {
                                    print ("Get Mouse Button up: " + mouseButtonInput.ToString ());
                                }

                                if (showKeyboardPressedAction) {
                                    print ("Action Name On Mouse Button Up: " + currentKeyButtonToCheck.Name);
                                }

                                return true;
                            }
                        } else {
                            if (useNewInputKeys) {
                                if (getKeyUp (currentKeyToCheck)) {
                                    if (showKeyboardPressed) {
                                        print ("Get Key Up: " + currentKeyToCheck.ToString ());
                                    }

                                    if (showKeyboardPressedAction) {
                                        print ("Action Name On Key: " + currentKeyButtonToCheck.Name);
                                    }

                                    return true;
                                }
                            } else {
                                if (getKeyUp (currentKeyCodeToCheck)) {
                                    if (showKeyboardPressed) {
                                        print ("Get Key Up: " + currentKeyButtonToCheck.keyButton);
                                    }

                                    if (showKeyboardPressedAction) {
                                        print ("Action Name On Key Up: " + currentKeyButtonToCheck.Name);
                                    }

                                    return true;
                                }
                            }
                        }

                        break;

                    //mouse wheel
                    case buttonType.negMouseWheel:
                        //check if the wheel of the mouse has been used, and in what direction
                        if (mainMouse.scroll.y.ReadValue () < 0) {
                            return true;
                        }

                        break;

                    case buttonType.posMouseWheel:
                        //check if the wheel of the mouse has been used, and in what direction
                        if (mainMouse.scroll.y.ReadValue () > 0) {
                            return true;
                        }
                        break;
                }
#endif
            } else {
                currentKeyCodeToCheck = currentKeyButtonToCheck.key;

                switch (type) {
                    //this key is for holding
                    case buttonType.getKey:
                        if (InputAxisWrapper.getOldKey (currentKeyCodeToCheck)) {
                            if (showKeyboardPressed) {
                                print ("Get Key: " + currentKeyButtonToCheck.keyButton);
                            }

                            if (showKeyboardPressedAction) {
                                print ("Action Name On Key: " + currentKeyButtonToCheck.Name);
                            }

                            return true;
                        }

                        break;

                    //this key is for press once
                    case buttonType.getKeyDown:
                        if (InputAxisWrapper.getOldKeyDown (currentKeyCodeToCheck)) {

                            if (showKeyboardPressed) {
                                print ("Get Key Down: " + currentKeyButtonToCheck.keyButton);
                            }

                            if (showKeyboardPressedAction) {
                                print ("Action Name On Key Down: " + currentKeyButtonToCheck.Name);
                            }

                            return true;
                        }

                        break;

                    //this key is for release
                    case buttonType.getKeyUp:
                        if (InputAxisWrapper.getOldKeyUp (currentKeyCodeToCheck)) {

                            if (showKeyboardPressed) {
                                print ("Get Key Up: " + currentKeyButtonToCheck.keyButton);
                            }

                            if (showKeyboardPressedAction) {
                                print ("Action Name On Key Up: " + currentKeyButtonToCheck.Name);
                            }

                            return true;
                        }

                        break;

                    //mouse wheel
                    case buttonType.negMouseWheel:
                        //check if the wheel of the mouse has been used, and in what direction
                        if (InputAxisWrapper.getOldInputAxisValue ("Mouse ScrollWheel") < 0) {
                            return true;
                        }

                        break;

                    case buttonType.posMouseWheel:
                        //check if the wheel of the mouse has been used, and in what direction
                        if (InputAxisWrapper.getOldInputAxisValue ("Mouse ScrollWheel") > 0) {
                            return true;
                        }

                        break;
                }
            }
        }

        return false;
    }

    public bool isKeyboardButtonPressed (int controllerNumber, string multiAxesName, string axesName)
    {
        int multiAxesIndex = multiAxesList.FindIndex (s => s.axesName.ToLower ().Equals (multiAxesName.ToLower ()));

        if (multiAxesIndex > -1) {

            int axesIndex = multiAxesList [multiAxesIndex].axes.FindIndex (s => s.Name.ToLower ().Equals (axesName.ToLower ()));

            if (axesIndex > -1) {
                currentKeyButtonToCheck = multiAxesList [multiAxesIndex].axes [axesIndex];

                if (usingTouchControls) {
                    if (currentKeyButtonToCheck.touchButtonPanelIndex > 0) {
                        currentTouchButtonIndexToCheck = currentKeyButtonToCheck.currentTouchButtonIndex;

                        if (numberOfTouchControls > currentTouchButtonIndexToCheck) {
                            currentTouchButtonListener = touchButtonList [currentTouchButtonIndexToCheck];

                            if (currentTouchButtonListener.isPressed ()) {
                                return true;
                            }
                        }
                    }
                } else {

                    if (useNewInputKeys) {
#if ENABLE_INPUT_SYSTEM
                        currentKeyToCheck = currentKeyButtonToCheck.keyInput;

                        if (!useMouseButtonType && currentKeyToCheck == Key.None) {
                            return false;
                        }
#endif
                    } else {
                        currentKeyCodeToCheck = currentKeyButtonToCheck.key;
                    }

                    if (useNewInputKeys) {
#if ENABLE_INPUT_SYSTEM
                        if (getKey (currentKeyToCheck)) {
                            return true;
                        }
#endif
                    } else {
                        if (InputAxisWrapper.getOldKey (currentKeyCodeToCheck)) {

                            return true;
                        }
                    }

                    if ((!usingKeyBoard || isAllowKeyboardAndGamepad ()) && usingGamepad) {
                        if (useNewInputSystem) {
#if ENABLE_INPUT_SYSTEM
                            currentGamepadButtonToCheck = currentJoystickButtonToCheck.gamepadButton;

                            if (currentGamepadButtonToCheck == newGamepadButtonNone) {
                                return false;
                            }

                            if (newInputGamepadInfoList.TryGetValue (controllerNumber, out currentNewInputGamepadToCheck)) {
                                if (!currentNewInputGamepadToCheck.connected) {
                                    return false;
                                }
                            } else {
                                return false;
                            }

                            if (currentNewInputGamepadToCheck.gamepad [currentGamepadButtonToCheck].isPressed) {
                                return true;
                            }
#endif
                        } else {
                            keyIndexPressed = (int)currentKeyButtonToCheck.joystickButton;

                            if (keyIndexPressed == -1) {
                                return false;
                            }

                            currentJoystickButtonIsAxis = (keyIndexPressed >= 10 && keyIndexPressed <= 15);

                            if (currentJoystickButtonIsAxis) {
                                return getAxisValue (controllerNumber, keyIndexPressed, multiAxesIndex, axesIndex, (int)buttonType.getKey);
                            }

                            if (controllerNumber <= 0) {
                                return false;
                            }

                            currentJoystickButtonKeyString = gamepadList [controllerNumber - 1].joystickButtonStringList [keyIndexPressed];

                            if (InputAxisWrapper.getOldKey (currentJoystickButtonKeyString)) {
                                return true;
                            }
                        }
                    }
                }
            }
        }

        return false;
    }

    public bool getTouchButtonFromMultiAxesList (int multiAxesIndex, int axesIndex, buttonType type)
    {
        //if the game is not paused, and the current control is a touch device
        if (!gameManagerPaused && usingTouchControls) {

            if (multiAxesIndex >= numberOfMultiAxes) {
                if (showMissingInputWarningMessagesEnabled) {
                    print ("WARNING: The input manager is trying to access to an action which has changed or has been removed.  " +
                    "\n Please, check the Player Input Manager to configure properly this group of actions according to the " +
                    "settings in the main custom Input Manager of GKC." +
                    "\n Use the Update Input List button on Player Input Manager and make sure to remove any removed action from it where the Axe field appears as empty.\n\n");
                }

                return false;
            }

            if (multiAxesIndex < 0 || axesIndex < 0) {
                if (showMissingInputWarningMessagesEnabled) {
                    if (multiAxesIndex >= 0) {
                        print ("WARNING: The input manager is trying to access to an action which has changed or has been removed in the Group of actions" +
                        " called " + multiAxesList [multiAxesIndex].axesName + " \n Please, check the Player Input Manager to configure properly this group of actions according to the " +
                        "settings in the main custom Input Manager of GKC" +
                        "\n Use the Update Input List button on Player Input Manager and make sure to remove any removed action from it where the Axe field appears as empty.\n\n");
                    } else {
                        print ("WARNING: The input manager is trying to access to a multi axes which has changed or has been removed.  " +
                        "\n Please, check the Player Input Manager to configure properly this group of actions according to the " +
                        "settings in the main custom Input Manager of GKC" +
                        "\n Use the Update Input List button on Player Input Manager and make sure to remove any removed action from it where the Axe field appears as empty.\n\n");
                    }
                }

                return false;
            }

            if (axesIndex >= multiAxesIndexList [multiAxesIndex]) {
                if (showMissingInputWarningMessagesEnabled) {
                    print ("WARNING: The input manager is trying to access to an action which has changed or has been removed in the Group of actions" +
                    " called " + multiAxesList [multiAxesIndex].axesName + " \n Please, check the Player Input Manager to configure properly this group of actions according to the " +
                    "settings in the main custom Input Manager of GKC" +
                    "\n Use the Update Input List button on Player Input Manager and make sure to remove any removed action from it where the Axe field appears as empty.\n\n");
                }

                return false;
            }

            if (!multiAxesList [multiAxesIndex].currentlyActive) {
                return false;
            }

            currentTouchButtonToCheck = multiAxesList [multiAxesIndex].axes [axesIndex];

            if (!currentTouchButtonToCheck.actionEnabled) {
                return false;
            }

            if (currentTouchButtonToCheck.touchButtonPanelIndex > 0) {
                currentTouchButtonIndexToCheck = currentTouchButtonToCheck.currentTouchButtonIndex;

                if (numberOfTouchControls > currentTouchButtonIndexToCheck) {
                    currentTouchButtonListener = touchButtonList [currentTouchButtonIndexToCheck];

                    //check for a began touch
                    if (type == buttonType.getKeyDown) {
                        if (currentTouchButtonListener.isPressedDown ()) {

                            currentTouchCount = 0;

                            if (touchPlatform && useNewInputSystem) {
                                currentTouchCount = InputAxisWrapper.getNewTouchCount ();
                            } else {
                                currentTouchCount = InputAxisWrapper.getTouchCount ();
                            }

                            if (!touchPlatform) {
                                currentTouchCount++;
                            }

                            for (currentTouchIndex = 0; currentTouchIndex < currentTouchCount; currentTouchIndex++) {
                                if (!touchPlatform) {
                                    currentTouch = InputAxisWrapper.convertMouseIntoFinger ();
                                } else {
                                    if (!useNewInputSystem) {
                                        currentTouch = InputAxisWrapper.getTouch (currentTouchIndex);
                                    }
                                }

                                if (touchPlatform && useNewInputSystem) {
                                    touchPressType = InputAxisWrapper.GetTouchPressType (currentTouchIndex);
                                } else {
                                    touchPressType = InputAxisWrapper.GetTouchPressType (currentTouch);
                                }

                                if (touchPressType == InputAxisWrapper.TouchPressType.Began) {
                                    //if the button is pressed (OnPointerDown), return true
                                    //print ("getKeyDown");
                                    return true;
                                }
                            }
                        }
                    }

                    //check for a hold touch
                    if (type == buttonType.getKey) {
                        if (currentTouchButtonListener.isPressed ()) {

                            currentTouchCount = 0;

                            if (touchPlatform && useNewInputSystem) {
                                currentTouchCount = InputAxisWrapper.getNewTouchCount ();
                            } else {
                                currentTouchCount = InputAxisWrapper.getTouchCount ();
                            }

                            if (!touchPlatform) {
                                currentTouchCount++;
                            }

                            for (currentTouchIndex = 0; currentTouchIndex < currentTouchCount; currentTouchIndex++) {
                                if (!touchPlatform) {
                                    currentTouch = InputAxisWrapper.convertMouseIntoFinger ();
                                } else {
                                    if (!useNewInputSystem) {
                                        currentTouch = InputAxisWrapper.getTouch (currentTouchIndex);
                                    }
                                }

                                if (touchPlatform && useNewInputSystem) {
                                    touchPressType = InputAxisWrapper.GetTouchPressType (currentTouchIndex);
                                } else {
                                    touchPressType = InputAxisWrapper.GetTouchPressType (currentTouch);
                                }

                                if (touchPressType == InputAxisWrapper.TouchPressType.Stationary ||
                                    touchPressType == InputAxisWrapper.TouchPressType.Moved) {
                                    //if the button is pressed OnPointerDown, and is not released yet (OnPointerUp), return true
                                    //print ("getKey");
                                    return true;
                                }
                            }
                        }
                    }

                    //check for a release touch
                    if (type == buttonType.getKeyUp) {
                        if (currentTouchButtonListener.isPressedUp ()) {

                            currentTouchCount = 0;

                            if (touchPlatform && useNewInputSystem) {
                                currentTouchCount = InputAxisWrapper.getNewTouchCount ();
                            } else {
                                currentTouchCount = InputAxisWrapper.getTouchCount ();
                            }

                            if (!touchPlatform) {
                                currentTouchCount++;
                            }

                            for (currentTouchIndex = 0; currentTouchIndex < currentTouchCount; currentTouchIndex++) {
                                if (!touchPlatform) {
                                    currentTouch = InputAxisWrapper.convertMouseIntoFinger ();
                                } else {
                                    if (!useNewInputSystem) {
                                        currentTouch = InputAxisWrapper.getTouch (currentTouchIndex);
                                    }
                                }

                                if (touchPlatform && useNewInputSystem) {
                                    touchPressType = InputAxisWrapper.GetTouchPressType (currentTouchIndex);
                                } else {
                                    touchPressType = InputAxisWrapper.GetTouchPressType (currentTouch);
                                }

                                if (touchPressType == InputAxisWrapper.TouchPressType.Ended) {
                                    //if the button is released (OnPointerUp), return true
                                    //print ("getKeyUp");
                                    return true;
                                }
                            }
                        }
                    }
                }
            }
        }

        return false;
    }

    public bool useMultipleGamepadDictionary;

    public bool getJoystickButtonFromMultiAxesList (int multiAxesIndex, int axesIndex, buttonType type, int controllerNumber, bool canBeUsedOnPausedGame)
    {
        //if the game is not paused, and the current control is the keyboard
        if ((canBeUsedOnPausedGame || !gameManagerPaused) && (!usingTouchControls || allowGamepadInTouchDevice)) {

#if REWIRED
			if (!useRewired) {
#endif
            if (controllerNumber == 0) {
                return false;
            }

            if (!useNewInputSystem) {
                if (controllerNumber > numberOfGamepads) {
                    return false;
                }
            }

#if REWIRED
			}
#endif

            if (multiAxesIndex >= numberOfMultiAxes) {
                if (showMissingInputWarningMessagesEnabled) {
                    print ("WARNING: The input manager is trying to access to an action which has changed or has been removed.  " +
                    "\n Please, check the Player Input Manager to configure properly this group of actions according to the " +
                    "settings in the main custom Input Manager of GKC." +
                    "\n Use the Update Input List button on Player Input Manager and make sure to remove any removed action from it where the Axe field appears as empty.\n\n");
                }

                return false;
            }

            if (multiAxesIndex < 0 || axesIndex < 0) {
                if (showMissingInputWarningMessagesEnabled) {
                    if (multiAxesIndex >= 0) {
                        print ("WARNING: The input manager is trying to access to an action which has changed or has been removed in the Group of actions" +
                        " called " + multiAxesList [multiAxesIndex].axesName + " \n Please, check the Player Input Manager to configure properly this group of actions according to the " +
                        "settings in the main custom Input Manager of GKC" +
                        "\n Use the Update Input List button on Player Input Manager and make sure to remove any removed action from it where the Axe field appears as empty.\n\n");
                    } else {
                        print ("WARNING: The input manager is trying to access to a multi axes which has changed or has been removed.  " +
                        "\n Please, check the Player Input Manager to configure properly this group of actions according to the " +
                        "settings in the main custom Input Manager of GKC" +
                        "\n Use the Update Input List button on Player Input Manager and make sure to remove any removed action from it where the Axe field appears as empty.\n\n");
                    }
                }

                return false;
            }

            if (axesIndex >= multiAxesIndexList [multiAxesIndex]) {
                if (showMissingInputWarningMessagesEnabled) {
                    print ("WARNING: The input manager is trying to access to an action which has changed or has been removed in the Group of actions" +
                    " called " + multiAxesList [multiAxesIndex].axesName + " \n Please, check the Player Input Manager to configure properly this group of actions according to the " +
                    "settings in the main custom Input Manager of GKC" +
                    "\n Use the Update Input List button on Player Input Manager and make sure to remove any removed action from it where the Axe field appears as empty.\n\n");
                }

                return false;
            }

            if (!multiAxesList [multiAxesIndex].currentlyActive) {
                return false;
            }

            currentJoystickButtonToCheck = multiAxesList [multiAxesIndex].axes [axesIndex];

            //check that the key pressed has being defined as an action
            if (!currentJoystickButtonToCheck.actionEnabled) {
                return false;
            }

            if (useNewInputSystem) {

            } else {

                keyIndexPressed = (int)currentJoystickButtonToCheck.joystickButton;
            }

#if REWIRED
			if (!useRewired) {
#endif
            if (useNewInputSystem) {
#if ENABLE_INPUT_SYSTEM
                currentGamepadButtonToCheck = currentJoystickButtonToCheck.gamepadButton;

                if (currentGamepadButtonToCheck == newGamepadButtonNone) {
                    return false;
                }

                if (newInputGamepadInfoList.TryGetValue (controllerNumber, out currentNewInputGamepadToCheck)) {
                    if (!currentNewInputGamepadToCheck.connected) {
                        return false;
                    }
                } else {
                    return false;
                }
#endif
            } else {
                if (keyIndexPressed == -1) {
                    return false;
                }

                currentJoystickButtonIsAxis = (keyIndexPressed >= 10 && keyIndexPressed <= 15);

                if (currentJoystickButtonIsAxis) {
                    return getAxisValue (controllerNumber, keyIndexPressed, multiAxesIndex, axesIndex, type);
                }

                if (useMultipleGamepadDictionary) {
                    currentJoystickButtonKeyString = getKeyString (controllerNumber, keyIndexPressed);
                } else {
                    currentJoystickButtonKeyString = gamepadList [controllerNumber - 1].joystickButtonStringList [keyIndexPressed];
                }
            }
#if REWIRED
			}
#endif

            switch (type) {
                //this key is for holding
                case buttonType.getKey:
#if REWIRED
				if (useRewired) {
					if (currentJoystickButtonToCheck.rewiredAction < 0)
						return false;

					if (currentJoystickButtonToCheck.axisContribution == Axes.AxisContribution.Positive
						|| currentJoystickButtonToCheck.axisContribution == Axes.AxisContribution.Both) {
						if (_rewiredPlayers[controllerNumber - 1].GetButton(currentJoystickButtonToCheck.rewiredAction)) {
							if (showGamepadPressed) {
								print (controllerNumber + " Get Key: " + ReInput.mapping.GetAction(currentJoystickButtonToCheck.rewiredAction).name);
							}

							if (showGamepadPressedAction) {
								print (controllerNumber + " Action On Key: " + currentJoystickButtonToCheck.Name);
							}

							return true;
						}
					}
					if (currentJoystickButtonToCheck.axisContribution == Axes.AxisContribution.Negative
							 || currentJoystickButtonToCheck.axisContribution == Axes.AxisContribution.Both) {
						if (_rewiredPlayers[controllerNumber - 1].GetNegativeButton(currentJoystickButtonToCheck.rewiredAction)) {
							if (showGamepadPressed) {
								print (controllerNumber + " Get Key: " + ReInput.mapping.GetAction(currentJoystickButtonToCheck.rewiredAction).name);
							}

							if (showGamepadPressedAction) {
								print (controllerNumber + " Action On Key: " + currentJoystickButtonToCheck.Name);
							}

							return true;
						}
					}

					return false;
				}
#endif

                    if (useNewInputSystem) {
#if ENABLE_INPUT_SYSTEM
                        if (currentNewInputGamepadToCheck.gamepad [currentGamepadButtonToCheck].isPressed) {
                            if (showGamepadPressed) {
                                print (controllerNumber + " Get Key: " + currentGamepadButtonToCheck.ToString ());
                            }

                            if (showGamepadPressedAction) {
                                print (controllerNumber + " Action On Key: " + currentJoystickButtonToCheck.Name);
                            }

                            return true;
                        }
#endif
                    } else {
                        if (InputAxisWrapper.getOldKey (currentJoystickButtonKeyString)) {
                            if (showGamepadPressed) {
                                print (controllerNumber + " Get Key: " + currentJoystickButtonKeyString);
                            }

                            if (showGamepadPressedAction) {
                                print (controllerNumber + " Action On Key: " + currentJoystickButtonToCheck.Name);
                            }

                            return true;
                        }
                    }

                    break;

                case buttonType.getKeyDown:
                    //this key is for press once
#if REWIRED
				if (useRewired) {
					if (currentJoystickButtonToCheck.rewiredAction < 0)
						return false;

					if (currentJoystickButtonToCheck.axisContribution == Axes.AxisContribution.Positive
						|| currentJoystickButtonToCheck.axisContribution == Axes.AxisContribution.Both) {
						if (_rewiredPlayers[controllerNumber - 1].GetButtonDown(currentJoystickButtonToCheck.rewiredAction)) {
							if (showGamepadPressed) {
								print (controllerNumber + " Get Key Down: " + ReInput.mapping.GetAction(currentJoystickButtonToCheck.rewiredAction).name);
							}

							if (showGamepadPressedAction) {
								print (controllerNumber + " Action On Key Down: " + currentJoystickButtonToCheck.Name);
							}

							return true;
						}
					}
					if (currentJoystickButtonToCheck.axisContribution == Axes.AxisContribution.Negative
							 || currentJoystickButtonToCheck.axisContribution == Axes.AxisContribution.Both) {
						if (_rewiredPlayers[controllerNumber - 1].GetNegativeButtonDown(currentJoystickButtonToCheck.rewiredAction)) {
							if (showGamepadPressed) {
								print (controllerNumber + " Get Key Down: " + ReInput.mapping.GetAction(currentJoystickButtonToCheck.rewiredAction).name);
							}

							if (showGamepadPressedAction) {
								print (controllerNumber + " Action On Key Down: " + currentJoystickButtonToCheck.Name);
							}

							return true;
						}
					}

					return false;
				}
#endif
                    if (useNewInputSystem) {
#if ENABLE_INPUT_SYSTEM
                        if (currentNewInputGamepadToCheck.gamepad [currentGamepadButtonToCheck].wasPressedThisFrame) {
                            if (showGamepadPressed) {
                                print (controllerNumber + " Get Key Down: " + currentGamepadButtonToCheck.ToString ());
                            }

                            if (showGamepadPressedAction) {
                                print (controllerNumber + " Action On Key Down: " + currentJoystickButtonToCheck.Name);
                            }

                            return true;
                        }
#endif
                    } else {
                        if (InputAxisWrapper.getOldKeyDown (currentJoystickButtonKeyString)) {
                            if (showGamepadPressed) {
                                print (controllerNumber + " Get Key Down: " + currentJoystickButtonKeyString);
                            }

                            if (showGamepadPressedAction) {
                                print (controllerNumber + " Action On Key Down: " + currentJoystickButtonToCheck.Name);
                            }
                            return true;
                        }
                    }
                    break;

                case buttonType.getKeyUp:
                    //this key is for release 
#if REWIRED
				if (useRewired) {
					if (currentJoystickButtonToCheck.rewiredAction < 0)
						return false;

					if (currentJoystickButtonToCheck.axisContribution == Axes.AxisContribution.Positive
						|| currentJoystickButtonToCheck.axisContribution == Axes.AxisContribution.Both) {
						if (_rewiredPlayers[controllerNumber - 1].GetButtonUp(currentJoystickButtonToCheck.rewiredAction)) {
							if (showGamepadPressed) {
								print (controllerNumber + " Get Key Up: " + ReInput.mapping.GetAction(currentJoystickButtonToCheck.rewiredAction).name);
							}

							if (showGamepadPressedAction) {
								print (controllerNumber + " Action On Key Up: " + currentJoystickButtonToCheck.Name);
							}

							return true;
						}
					}
					if (currentJoystickButtonToCheck.axisContribution == Axes.AxisContribution.Negative
							 || currentJoystickButtonToCheck.axisContribution == Axes.AxisContribution.Both) {
						if (_rewiredPlayers[controllerNumber - 1].GetNegativeButtonUp(currentJoystickButtonToCheck.rewiredAction)) {
							if (showGamepadPressed) {
								print (controllerNumber + " Get Key Up: " + ReInput.mapping.GetAction(currentJoystickButtonToCheck.rewiredAction).name);
							}

							if (showGamepadPressedAction) {
								print (controllerNumber + " Action On Key Up: " + currentJoystickButtonToCheck.Name);
							}

							return true;
						}
					}

					return false;
				}
#endif
                    if (useNewInputSystem) {
#if ENABLE_INPUT_SYSTEM
                        if (currentNewInputGamepadToCheck.gamepad [currentGamepadButtonToCheck].wasReleasedThisFrame) {
                            if (showGamepadPressed) {
                                print (controllerNumber + " Get Key Up: " + currentGamepadButtonToCheck.ToString ());
                            }

                            if (showGamepadPressedAction) {
                                print (controllerNumber + " Action On Key Up: " + currentJoystickButtonToCheck.Name);
                            }

                            return true;
                        }
#endif
                    } else {
                        if (InputAxisWrapper.getOldKeyUp (currentJoystickButtonKeyString)) {
                            if (showGamepadPressed) {
                                print (controllerNumber + " Get Key Up: " + currentJoystickButtonKeyString);
                            }

                            if (showGamepadPressedAction) {
                                print (controllerNumber + " Action On Key Up: " + currentJoystickButtonToCheck.Name);
                            }
                            return true;
                        }
                    }
                    break;
            }
        }

        return false;
    }

    string currentAxisString;

    bool getAxisValue (int numberPlayer, int keyPressed, int multiAxesIndex, int axesIndex, buttonType type)
    {
        //TRIGGERS
        if (keyPressed == 14) {
            currentAxisString = gamepadList [numberPlayer - 1].axisButtonStringList [0];

            currentAxisValue = InputAxisWrapper.getOldInputAxisValue (currentAxisString);

            switch (type) {
                //this key is for holding
                case buttonType.getKey:
                    if (currentAxisValue > 0) {
                        if (showGamepadPressed) {
                            print (keyPressed + "-" + numberPlayer);
                        }

                        if (showGamepadPressedAction) {
                            print (numberPlayer + " Action On Key: " + multiAxesList [multiAxesIndex].axes [axesIndex].Name);
                        }

                        return true;
                    }
                    break;

                case buttonType.getKeyDown:
                    if (currentAxisValue > 0) {
                        if (!getGamePadLeftTriggersInfoDown (numberPlayer, multiAxesIndex, axesIndex)) {
                            setGamepadLeftTriggersInfo (numberPlayer, true, multiAxesIndex, axesIndex);

                            setGamepadLeftTriggersInfoDown (numberPlayer, true, multiAxesIndex, axesIndex);

                            if (showGamepadPressed) {
                                print (keyPressed + "-" + numberPlayer);
                            }

                            if (showGamepadPressedAction) {
                                print (numberPlayer + " Action On Key Down: " + multiAxesList [multiAxesIndex].axes [axesIndex].Name);
                            }

                            return true;
                        }
                    } else {
                        if (getGamePadLeftTriggersInfoDown (numberPlayer, multiAxesIndex, axesIndex)) {
                            setGamepadLeftTriggersInfoDown (numberPlayer, false, multiAxesIndex, axesIndex);
                        }
                    }
                    break;

                case buttonType.getKeyUp:
                    if (currentAxisValue == 0) {
                        if (getGamePadLeftTriggersInfo (numberPlayer, multiAxesIndex, axesIndex)) {

                            setGamepadLeftTriggersInfo (numberPlayer, false, multiAxesIndex, axesIndex);

                            if (showGamepadPressed) {
                                print (keyPressed + "-" + numberPlayer);
                            }

                            if (showGamepadPressedAction) {
                                print (numberPlayer + " Action On Key Up: " + multiAxesList [multiAxesIndex].axes [axesIndex].Name);
                            }

                            return true;
                        }
                    }
                    break;
            }
        }

        if (keyPressed == 15) {
            currentAxisString = gamepadList [numberPlayer - 1].axisButtonStringList [1];

            currentAxisValue = InputAxisWrapper.getOldInputAxisValue (currentAxisString);

            switch (type) {
                //this key is for holding
                case buttonType.getKey:
                    if (currentAxisValue > 0) {
                        if (showGamepadPressed) {
                            print (keyPressed + "-" + numberPlayer);
                        }

                        if (showGamepadPressedAction) {
                            print (numberPlayer + " Action On Key: " + multiAxesList [multiAxesIndex].axes [axesIndex].Name);
                        }

                        return true;
                    }
                    break;

                case buttonType.getKeyDown:
                    if (currentAxisValue > 0) {
                        if (!getGamePadRightTriggersInfoDown (numberPlayer, multiAxesIndex, axesIndex)) {
                            setGamepadRightTriggersInfo (numberPlayer, true, multiAxesIndex, axesIndex);

                            setGamepadRightTriggersInfoDown (numberPlayer, true, multiAxesIndex, axesIndex);

                            if (showGamepadPressed) {
                                print (keyPressed + "-" + numberPlayer);
                            }

                            if (showGamepadPressedAction) {
                                print (numberPlayer + " Action On Key Down: " + multiAxesList [multiAxesIndex].axes [axesIndex].Name);
                            }

                            return true;
                        }
                    } else {
                        if (getGamePadRightTriggersInfoDown (numberPlayer, multiAxesIndex, axesIndex)) {
                            setGamepadRightTriggersInfoDown (numberPlayer, false, multiAxesIndex, axesIndex);
                        }
                    }
                    break;

                case buttonType.getKeyUp:
                    if (currentAxisValue == 0) {
                        if (getGamePadRightTriggersInfo (numberPlayer, multiAxesIndex, axesIndex)) {
                            setGamepadRightTriggersInfo (numberPlayer, false, multiAxesIndex, axesIndex);

                            if (showGamepadPressed) {
                                print (keyPressed + "-" + numberPlayer);
                            }

                            if (showGamepadPressedAction) {
                                print (numberPlayer + " Action On Key: " + multiAxesList [multiAxesIndex].axes [axesIndex].Name);
                            }

                            return true;
                        }
                    }
                    break;
            }
        }


        //DPAD X
        if (keyPressed == 10) {
            currentAxisString = gamepadList [numberPlayer - 1].axisButtonStringList [2];

            currentAxisValue = InputAxisWrapper.getOldInputAxisValue (currentAxisString);

            switch (type) {
                //this key is for holding
                case buttonType.getKey:
                    if (currentAxisValue < 0) {
                        if (showGamepadPressed) {
                            print (keyPressed + "-" + numberPlayer);
                        }

                        if (showGamepadPressedAction) {
                            print (numberPlayer + " Action On Key: " + multiAxesList [multiAxesIndex].axes [axesIndex].Name);
                        }

                        return true;
                    }
                    break;

                case buttonType.getKeyDown:
                    if (currentAxisValue < 0) {
                        if (!getGamePadDPadXInfoNegativeDown (numberPlayer, multiAxesIndex, axesIndex)) {
                            setGamepadDPadXInfoNegative (numberPlayer, true, multiAxesIndex, axesIndex);

                            setGamepadDPadXInfoNegativeDown (numberPlayer, true, multiAxesIndex, axesIndex);

                            if (showGamepadPressed) {
                                print (keyPressed + "-" + numberPlayer);
                            }

                            if (showGamepadPressedAction) {
                                print (numberPlayer + " Action On Key Down: " + multiAxesList [multiAxesIndex].axes [axesIndex].Name);
                            }

                            return true;
                        }
                    } else {
                        if (getGamePadDPadXInfoNegativeDown (numberPlayer, multiAxesIndex, axesIndex)) {
                            setGamepadDPadXInfoNegativeDown (numberPlayer, false, multiAxesIndex, axesIndex);
                        }
                    }
                    break;

                case buttonType.getKeyUp:
                    if (currentAxisValue == 0) {
                        if (getGamePadDPadXInfoNegative (numberPlayer, multiAxesIndex, axesIndex)) {

                            setGamepadDPadXInfoNegative (numberPlayer, false, multiAxesIndex, axesIndex);

                            if (showGamepadPressed) {
                                print (keyPressed + "-" + numberPlayer);
                            }

                            if (showGamepadPressedAction) {
                                print (numberPlayer + " Action On Key Up: " + multiAxesList [multiAxesIndex].axes [axesIndex].Name);
                            }

                            return true;
                        }
                    }
                    break;
            }
        }

        if (keyPressed == 11) {
            currentAxisString = gamepadList [numberPlayer - 1].axisButtonStringList [2];

            currentAxisValue = InputAxisWrapper.getOldInputAxisValue (currentAxisString);

            switch (type) {
                //this key is for holding
                case buttonType.getKey:
                    if (currentAxisValue > 0) {
                        if (showGamepadPressed) {
                            print (keyPressed + "-" + numberPlayer);
                        }

                        if (showGamepadPressedAction) {
                            print (numberPlayer + " Action On Key: " + multiAxesList [multiAxesIndex].axes [axesIndex].Name);
                        }

                        return true;
                    }
                    break;

                case buttonType.getKeyDown:
                    if (currentAxisValue > 0) {
                        if (!getGamePadDPadXInfoPositiveDown (numberPlayer, multiAxesIndex, axesIndex)) {
                            setGamepadDPadXInfoPositive (numberPlayer, true, multiAxesIndex, axesIndex);

                            setGamepadDPadXInfoPositiveDown (numberPlayer, true, multiAxesIndex, axesIndex);

                            if (showGamepadPressed) {
                                print (keyPressed + "-" + numberPlayer);
                            }

                            if (showGamepadPressedAction) {
                                print (numberPlayer + " Action On Key Down: " + multiAxesList [multiAxesIndex].axes [axesIndex].Name);
                            }

                            return true;
                        }
                    } else {
                        if (getGamePadDPadXInfoPositiveDown (numberPlayer, multiAxesIndex, axesIndex)) {
                            setGamepadDPadXInfoPositiveDown (numberPlayer, false, multiAxesIndex, axesIndex);
                        }
                    }
                    break;

                case buttonType.getKeyUp:
                    if (currentAxisValue == 0) {
                        if (getGamePadDPadXInfoPositive (numberPlayer, multiAxesIndex, axesIndex)) {

                            setGamepadDPadXInfoPositive (numberPlayer, false, multiAxesIndex, axesIndex);

                            if (showGamepadPressed) {
                                print (keyPressed + "-" + numberPlayer);
                            }

                            if (showGamepadPressedAction) {
                                print (numberPlayer + " Action On Key Up: " + multiAxesList [multiAxesIndex].axes [axesIndex].Name);
                            }

                            return true;
                        }
                    }
                    break;
            }
        }


        //DPAD Y
        if (keyPressed == 12) {
            currentAxisString = gamepadList [numberPlayer - 1].axisButtonStringList [3];

            currentAxisValue = InputAxisWrapper.getOldInputAxisValue (currentAxisString);

            switch (type) {
                //this key is for holding
                case buttonType.getKey:
                    if (currentAxisValue < 0) {
                        if (showGamepadPressed) {
                            print (keyPressed + "-" + numberPlayer);
                        }

                        if (showGamepadPressedAction) {
                            print (numberPlayer + " Action On Key: " + multiAxesList [multiAxesIndex].axes [axesIndex].Name);
                        }

                        return true;
                    }
                    break;

                case buttonType.getKeyDown:
                    if (currentAxisValue < 0) {
                        if (!getGamePadDPadYInfoNegativeDown (numberPlayer, multiAxesIndex, axesIndex)) {
                            setGamepadDPadYInfoNegative (numberPlayer, true, multiAxesIndex, axesIndex);

                            setGamepadDPadYInfoNegativeDown (numberPlayer, true, multiAxesIndex, axesIndex);

                            if (showGamepadPressed) {
                                print (keyPressed + "-" + numberPlayer);
                            }

                            if (showGamepadPressedAction) {
                                print (numberPlayer + " Action On Key Down: " + multiAxesList [multiAxesIndex].axes [axesIndex].Name);
                            }

                            return true;
                        }
                    } else {
                        if (getGamePadDPadYInfoNegativeDown (numberPlayer, multiAxesIndex, axesIndex)) {
                            setGamepadDPadYInfoNegativeDown (numberPlayer, false, multiAxesIndex, axesIndex);
                        }
                    }
                    break;

                case buttonType.getKeyUp:
                    if (currentAxisValue == 0) {
                        if (getGamePadDPadYInfoNegative (numberPlayer, multiAxesIndex, axesIndex)) {

                            setGamepadDPadYInfoNegative (numberPlayer, false, multiAxesIndex, axesIndex);

                            if (showGamepadPressed) {
                                print (keyPressed + "-" + numberPlayer);
                            }

                            if (showGamepadPressedAction) {
                                print (numberPlayer + " Action On Key Up: " + multiAxesList [multiAxesIndex].axes [axesIndex].Name);
                            }

                            return true;
                        }
                    }
                    break;
            }
        }

        if (keyPressed == 13) {
            currentAxisString = gamepadList [numberPlayer - 1].axisButtonStringList [3];

            currentAxisValue = InputAxisWrapper.getOldInputAxisValue (currentAxisString);

            switch (type) {
                //this key is for holding
                case buttonType.getKey:
                    if (currentAxisValue > 0) {
                        if (showGamepadPressed) {
                            print (keyPressed + "-" + numberPlayer);
                        }

                        if (showGamepadPressedAction) {
                            print (numberPlayer + " Action On Key: " + multiAxesList [multiAxesIndex].axes [axesIndex].Name);
                        }

                        return true;
                    }
                    break;

                case buttonType.getKeyDown:
                    if (currentAxisValue > 0) {
                        if (!getGamePadDPadYInfoPositiveDown (numberPlayer, multiAxesIndex, axesIndex)) {
                            setGamepadDPadYInfoPositive (numberPlayer, true, multiAxesIndex, axesIndex);

                            setGamepadDPadYInfoPositiveDown (numberPlayer, true, multiAxesIndex, axesIndex);

                            if (showGamepadPressed) {
                                print (keyPressed + "-" + numberPlayer);
                            }

                            if (showGamepadPressedAction) {
                                print (numberPlayer + " Action On Key Down: " + multiAxesList [multiAxesIndex].axes [axesIndex].Name);
                            }

                            return true;
                        }
                    } else {
                        if (getGamePadDPadYInfoPositiveDown (numberPlayer, multiAxesIndex, axesIndex)) {
                            setGamepadDPadYInfoPositiveDown (numberPlayer, false, multiAxesIndex, axesIndex);
                        }
                    }
                    break;

                case buttonType.getKeyUp:
                    if (currentAxisValue == 0) {
                        if (getGamePadDPadYInfoPositive (numberPlayer, multiAxesIndex, axesIndex)) {

                            setGamepadDPadYInfoPositive (numberPlayer, false, multiAxesIndex, axesIndex);

                            if (showGamepadPressed) {
                                print (keyPressed + "-" + numberPlayer);
                            }

                            if (showGamepadPressedAction) {
                                print (numberPlayer + " Action On Key Up: " + multiAxesList [multiAxesIndex].axes [axesIndex].Name);
                            }

                            return true;
                        }
                    }
                    break;
            }
        }

        return false;
    }


    //Set and get triggerS state
    public void setGamepadLeftTriggersInfo (int numberPlayer, bool left, int multiAxesIndex, int axesIndex)
    {
        gamepadList [numberPlayer - 1].multiGamepadAxisInfoList [multiAxesIndex].gamepadAxisInfoList [axesIndex].usingLeftTrigger = left;
    }

    public void setGamepadRightTriggersInfo (int numberPlayer, bool right, int multiAxesIndex, int axesIndex)
    {
        gamepadList [numberPlayer - 1].multiGamepadAxisInfoList [multiAxesIndex].gamepadAxisInfoList [axesIndex].usingRightTrigger = right;
    }

    public void setGamepadLeftTriggersInfoDown (int numberPlayer, bool left, int multiAxesIndex, int axesIndex)
    {
        gamepadList [numberPlayer - 1].multiGamepadAxisInfoList [multiAxesIndex].gamepadAxisInfoList [axesIndex].leftTriggerDown = left;
    }

    public void setGamepadRightTriggersInfoDown (int numberPlayer, bool right, int multiAxesIndex, int axesIndex)
    {
        gamepadList [numberPlayer - 1].multiGamepadAxisInfoList [multiAxesIndex].gamepadAxisInfoList [axesIndex].rightTriggerDown = right;
    }

    public bool getGamePadLeftTriggersInfo (int numberPlayer, int multiAxesIndex, int axesIndex)
    {
        return gamepadList [numberPlayer - 1].multiGamepadAxisInfoList [multiAxesIndex].gamepadAxisInfoList [axesIndex].usingLeftTrigger;
    }

    public bool getGamePadRightTriggersInfo (int numberPlayer, int multiAxesIndex, int axesIndex)
    {
        return gamepadList [numberPlayer - 1].multiGamepadAxisInfoList [multiAxesIndex].gamepadAxisInfoList [axesIndex].usingRightTrigger;
    }

    public bool getGamePadLeftTriggersInfoDown (int numberPlayer, int multiAxesIndex, int axesIndex)
    {
        return gamepadList [numberPlayer - 1].multiGamepadAxisInfoList [multiAxesIndex].gamepadAxisInfoList [axesIndex].leftTriggerDown;
    }

    public bool getGamePadRightTriggersInfoDown (int numberPlayer, int multiAxesIndex, int axesIndex)
    {
        return gamepadList [numberPlayer - 1].multiGamepadAxisInfoList [multiAxesIndex].gamepadAxisInfoList [axesIndex].rightTriggerDown;
    }


    //Set and get dpad X state
    public bool getGamePadDPadXInfoPositive (int numberPlayer, int multiAxesIndex, int axesIndex)
    {
        return gamepadList [numberPlayer - 1].multiGamepadAxisInfoList [multiAxesIndex].gamepadAxisInfoList [axesIndex].usingDPadXPositive;
    }

    public void setGamepadDPadXInfoPositive (int numberPlayer, bool DPad, int multiAxesIndex, int axesIndex)
    {
        gamepadList [numberPlayer - 1].multiGamepadAxisInfoList [multiAxesIndex].gamepadAxisInfoList [axesIndex].usingDPadXPositive = DPad;
    }

    public bool getGamePadDPadXInfoPositiveDown (int numberPlayer, int multiAxesIndex, int axesIndex)
    {
        return gamepadList [numberPlayer - 1].multiGamepadAxisInfoList [multiAxesIndex].gamepadAxisInfoList [axesIndex].DPadXPositiveDown;
    }

    public void setGamepadDPadXInfoPositiveDown (int numberPlayer, bool DPad, int multiAxesIndex, int axesIndex)
    {
        gamepadList [numberPlayer - 1].multiGamepadAxisInfoList [multiAxesIndex].gamepadAxisInfoList [axesIndex].DPadXPositiveDown = DPad;
    }

    public bool getGamePadDPadXInfoNegative (int numberPlayer, int multiAxesIndex, int axesIndex)
    {
        return gamepadList [numberPlayer - 1].multiGamepadAxisInfoList [multiAxesIndex].gamepadAxisInfoList [axesIndex].usingDPadXNegative;
    }

    public void setGamepadDPadXInfoNegative (int numberPlayer, bool DPad, int multiAxesIndex, int axesIndex)
    {
        gamepadList [numberPlayer - 1].multiGamepadAxisInfoList [multiAxesIndex].gamepadAxisInfoList [axesIndex].usingDPadXNegative = DPad;
    }

    public bool getGamePadDPadXInfoNegativeDown (int numberPlayer, int multiAxesIndex, int axesIndex)
    {
        return gamepadList [numberPlayer - 1].multiGamepadAxisInfoList [multiAxesIndex].gamepadAxisInfoList [axesIndex].DPadXNegativeDown;
    }

    public void setGamepadDPadXInfoNegativeDown (int numberPlayer, bool DPad, int multiAxesIndex, int axesIndex)
    {
        gamepadList [numberPlayer - 1].multiGamepadAxisInfoList [multiAxesIndex].gamepadAxisInfoList [axesIndex].DPadXNegativeDown = DPad;
    }


    //Set and get dpad Y state
    public bool getGamePadDPadYInfoPositive (int numberPlayer, int multiAxesIndex, int axesIndex)
    {
        return gamepadList [numberPlayer - 1].multiGamepadAxisInfoList [multiAxesIndex].gamepadAxisInfoList [axesIndex].usingDPadYPositive;
    }

    public void setGamepadDPadYInfoPositive (int numberPlayer, bool DPad, int multiAxesIndex, int axesIndex)
    {
        gamepadList [numberPlayer - 1].multiGamepadAxisInfoList [multiAxesIndex].gamepadAxisInfoList [axesIndex].usingDPadYPositive = DPad;
    }

    public bool getGamePadDPadYInfoPositiveDown (int numberPlayer, int multiAxesIndex, int axesIndex)
    {
        return gamepadList [numberPlayer - 1].multiGamepadAxisInfoList [multiAxesIndex].gamepadAxisInfoList [axesIndex].DPadYPositiveDown;
    }

    public void setGamepadDPadYInfoPositiveDown (int numberPlayer, bool DPad, int multiAxesIndex, int axesIndex)
    {
        gamepadList [numberPlayer - 1].multiGamepadAxisInfoList [multiAxesIndex].gamepadAxisInfoList [axesIndex].DPadYPositiveDown = DPad;
    }

    public bool getGamePadDPadYInfoNegative (int numberPlayer, int multiAxesIndex, int axesIndex)
    {
        return gamepadList [numberPlayer - 1].multiGamepadAxisInfoList [multiAxesIndex].gamepadAxisInfoList [axesIndex].usingDPadYNegative;
    }

    public void setGamepadDPadYInfoNegative (int numberPlayer, bool DPad, int multiAxesIndex, int axesIndex)
    {
        gamepadList [numberPlayer - 1].multiGamepadAxisInfoList [multiAxesIndex].gamepadAxisInfoList [axesIndex].usingDPadYNegative = DPad;
    }

    public bool getGamePadDPadYInfoNegativeDown (int numberPlayer, int multiAxesIndex, int axesIndex)
    {
        return gamepadList [numberPlayer - 1].multiGamepadAxisInfoList [multiAxesIndex].gamepadAxisInfoList [axesIndex].DPadYNegativeDown;
    }

    public void setGamepadDPadYInfoNegativeDown (int numberPlayer, bool DPad, int multiAxesIndex, int axesIndex)
    {
        gamepadList [numberPlayer - 1].multiGamepadAxisInfoList [multiAxesIndex].gamepadAxisInfoList [axesIndex].DPadYNegativeDown = DPad;
    }


    public void setRegister ()
    {
        // First use the platform to determine which register to search
        switch (Application.platform) {
            case RuntimePlatform.WindowsPlayer | RuntimePlatform.WindowsEditor:
                register = JoystickData.register_windows;
                break;

            case RuntimePlatform.OSXPlayer | RuntimePlatform.OSXEditor:
                register = JoystickData.register_osx;
                break;

            case RuntimePlatform.LinuxPlayer:
                register = JoystickData.register_linux;
                break;
        }

        register = JoystickData.register_default;
    }

    //	string keyStringFormat = "{0}{1}{2}{3}";

    string getKeyString (int joystickNumber, int keyIndex)
    {
        if (!registerInitialized) {
            setRegister ();
            registerInitialized = true;
        }

        joystickName = "default";

        if (!joystickNamesInitialized) {
            joystickNames = GetJoystickNames ();

            joystickNamesInitialized = true;
        }

        if (joystickNumber > joystickNames.Length) {
            return "";
        }

        joystickName = joystickNames [joystickNumber - 1];

        if (!register.ContainsKey (joystickName)) {
            joystickName = "default";

            // While we are here, make sure there the requested button is in the default joystick config of the default register
            if (!register [joystickName].ContainsKey ((JoystickData.ButtonTypes)keyIndex)) {
                // The requested button doesn't exist on the default joystick configuration! This is bad!
                return "";
            }
        }

        buttonConfig = register [joystickName];

        retString = "joystick " + joystickNumber + " button " + buttonConfig [(JoystickData.ButtonTypes)keyIndex];

        //		retString = string.Format (keyStringFormat, "joystick ", joystickNumber, " button ", buttonConfig [(JoystickData.ButtonTypes)keyIndex]);

        return retString;
    }

    //change the current controls to keyboard or mobile
    public void setKeyboardControls (bool state)
    {
        mainGameManager.setUseTouchControlsState (!state);

        pauseManager.setUseTouchControlsState (!state);

        useTouchControls = !state;

        for (int i = 0; i < playerInputManagerList.Count; i++) {
            playerInputManagerList [i].changeControlsType (!state);
        }
    }

    public void setKeyboardControlsFromEditor (bool state)
    {
        mainGameManager.setUseTouchControlsStateFromEditor (!state);

        pauseManager.setUseTouchControlsStateFromEditor (!state);

        useTouchControls = !state;

        for (int i = 0; i < playerInputManagerList.Count; i++) {
            playerInputManagerList [i].changeControlsType (!state);
        }

        updateInputInspector ();
    }

    public void setUseTouchControlsState (bool state)
    {
        useTouchControls = state;
    }

    public void clearAndObtainGamepadsConnected ()
    {
        if (useNewInputSystem) {
            setGamePadList ();
        }
    }

    public void removeGamepadByID (int deviceID)
    {
        if (!IsValidGamepad (deviceID)) {
            return;
        }

#if ENABLE_INPUT_SYSTEM
        addOrRemoveNewInputGamepad (getGamepad (deviceID), deviceID, false, true);
#endif
    }

    public void updateInputInspector ()
    {
        if (!Application.isPlaying) {
            print ("Input Manager Inspector updated");

            updateComponent ();

            GKC_Utils.updateDirtyScene ("Update Input Manager Inspector", gameObject);
        }
    }

    public bool isInTouchButtonToDisableList (GameObject touchButtonGameObject)
    {
        if (buttonsDisabledAtStart.Contains (touchButtonGameObject)) {
            return true;
        }

        return false;
    }

    public void setGamePadList ()
    {
        if (useNewInputSystem) {
#if ENABLE_INPUT_SYSTEM
            if (ignoreGamepad) {
                numberOfGamepads = 0;
            } else {
                currentNewInputGamepadInfoIndex = 0;

                newInputGamepadInfoList.Clear ();

                GamepadRegistry.clearRegister ();

                newInputGamepadInfoListDebug.Clear ();

                foreach (Gamepad pad in Gamepad.all) {
                    addOrRemoveNewInputGamepad (pad, pad.deviceId, true, false);
                }

                numberOfGamepads = 0;

                foreach (var value in newInputGamepadInfoList.Values) {
                    if (value.connected) {
                        numberOfGamepads++;
                    }
                }
            }
#endif
        } else {
            gamepadList.Clear ();

            int currentGamepadConnectedNameArrayLength = currentGamepadConnectedNameArray.Length;

            int multiAxesListCount = multiAxesList.Count;

            for (int i = 0; i < currentGamepadConnectedNameArrayLength; i++) {
                gamepadInfo newGamepad = new gamepadInfo ();
                newGamepad.gamepadNumber = (i + 1);
                newGamepad.gamepadName = currentGamepadConnectedNameArray [i];

                for (int j = 0; j < multiAxesListCount; j++) {

                    multiGamepadAxisInfo newMultiGamepadAxisInfo = new multiGamepadAxisInfo ();

                    multiAxes temporaMultiAxes = multiAxesList [j];

                    newMultiGamepadAxisInfo.Name = temporaMultiAxes.axesName;

                    int axesCount = temporaMultiAxes.axes.Count;

                    for (int k = 0; k < axesCount; k++) {
                        gamepadAxisInfo newGamepadAxisInfo = new gamepadAxisInfo ();

                        newGamepadAxisInfo.Name = temporaMultiAxes.axes [k].Name;
                        newMultiGamepadAxisInfo.gamepadAxisInfoList.Add (newGamepadAxisInfo);
                    }

                    newGamepad.multiGamepadAxisInfoList.Add (newMultiGamepadAxisInfo);
                }

                for (int j = 0; j < 20; j++) {
                    newGamepad.joystickButtonStringList.Add ("joystick " + (i + 1) + " button " + j);
                }

                newGamepad.axisButtonStringList.Add ("Left Trigger " + (i + 1));
                newGamepad.axisButtonStringList.Add ("Right Trigger " + (i + 1));

                newGamepad.axisButtonStringList.Add ("DPad X" + (i + 1));
                newGamepad.axisButtonStringList.Add ("DPad Y" + (i + 1));

                gamepadList.Add (newGamepad);

                //			Debug.Log (currentGamepadConnectedNameArray [i]);
            }

            numberOfGamepads = gamepadList.Count;
        }

        updateGamepadStateResults ();
    }

    void updateGamepadStateResults ()
    {
        if (numberOfGamepads == 0) {
            usingKeyBoard = true;

            usingGamepad = false;

            //print ("keyboard");
        }

        if (numberOfGamepads >= 1) {
            usingKeyBoard = false;

            usingGamepad = true;

            //print ("gamepad");
        }

        if (numberOfGamepads <= 1) {
            onlyOnePlayer = true;

            //print ("one player");
        }

        for (int i = 0; i < playerInputManagerList.Count; i++) {
            if (playerInputManagerList [i] != null) {
                playerInputManagerList [i].updateUsingGamepadState (usingGamepad);
            }
        }
    }

    //set the current pause state of the game
    public void setPauseState (bool state)
    {
        gameCurrentlyPaused = state;
    }

    public void enableTouchButtonByName (string touchButtonName)
    {
        enableOrDisableTouchButtonByName (true, touchButtonName);
    }

    public void disableTouchButtonByName (string touchButtonName)
    {
        enableOrDisableTouchButtonByName (false, touchButtonName);
    }

    public void enableOrDisableTouchButtonByName (bool state, string touchButtonName)
    {
        for (int i = 0; i < touchButtonList.Count; i++) {
            if (touchButtonList [i].gameObject.name.Equals (touchButtonName)) {
                if (touchButtonList [i].gameObject.activeSelf != state) {
                    touchButtonList [i].gameObject.SetActive (state);
                }

                return;
            }
        }
    }

    public bool isTouchPlatform ()
    {
        return touchPlatform;
    }

    public int currentNumberOfTouchButtonsPressed;

    public void increaseCurrentNumberOfTouchButtonsPressed ()
    {
        currentNumberOfTouchButtonsPressed++;

        //		print ("increase " + currentNumberOfTouchButtonsPressed);
    }

    public void decreaseCurrentNumberOfTouchButtonsPressed ()
    {
        currentNumberOfTouchButtonsPressed--;

        if (currentNumberOfTouchButtonsPressed < 0) {
            currentNumberOfTouchButtonsPressed = 0;
        }

        //		print ("decrease " + currentNumberOfTouchButtonsPressed);
    }

    public int getCurrentNumberOfTouchButtonsPressed ()
    {
        return currentNumberOfTouchButtonsPressed;
    }

    public void checkButtonPressedOnGamepadOrKeyboard (bool checkKeyboard)
    {
        if (checkKeyboard) {
            lastButtonPressedOnGamepad = false;
            lastKeyPressedOnKeyboard = true;
        } else {
            lastButtonPressedOnGamepad = true;
            lastKeyPressedOnKeyboard = false;
        }
    }

#if REWIRED
	private void OnControllerChange(Rewired.Player player, Controller controller)
	{
		switch (controller.type)
		{
			case ControllerType.Joystick:
				GKC_Utils.eventOnPressingGamepadInput (player.id + 1);
				lastButtonPressedOnGamepad = true;
				lastKeyPressedOnKeyboard = false;
				break;

			case ControllerType.Mouse:
			case ControllerType.Keyboard:
				GKC_Utils.eventOnPressingKeyboardInput (player.id + 1);
				lastButtonPressedOnGamepad = false;
				lastKeyPressedOnKeyboard = true;
				break;
		}
	}
#endif

    public void enableMultiAxesInputByName (string multiAxesName)
    {
        enableOrDisableMultiAxesInputByName (true, multiAxesName);
    }

    public void disableMultiAxesInputByName (string multiAxesName)
    {
        enableOrDisableMultiAxesInputByName (false, multiAxesName);
    }

    public void enableOrDisableMultiAxesInputByName (bool state, string multiAxesName)
    {
        int multiAxesIndex = multiAxesList.FindIndex (s => s.axesName.ToLower ().Equals (multiAxesName.ToLower ()));

        if (multiAxesIndex > -1) {
            multiAxesList [multiAxesIndex].currentlyActive = state;
        }
    }

    //EDITOR FUNCTIONS

    //add a new axe to the list
    public void addNewAxe (int axeListIndex)
    {
        Axes newAxe = new Axes ();
        newAxe.Name = "New Action";
        newAxe.joystickButton = joystickButtons.None;

        multiAxesList [axeListIndex].axes.Add (newAxe);

        updateComponent ();
    }

    public void addNewAxesList ()
    {
        multiAxes newMultiAxes = new multiAxes ();
        newMultiAxes.axesName = "New Axes List";

        multiAxesList.Add (newMultiAxes);

        updateComponent ();
    }

    public void removeAxesList (int multiAxesIndex)
    {
        if (multiAxesList.Count > multiAxesIndex) {
            string removeMultiAxesName = multiAxesList [multiAxesIndex].axesName;

            multiAxesList.RemoveAt (multiAxesIndex);

            print ("IMPORTANT: Multi Axes List " + removeMultiAxesName.ToUpper () + " has been removed." +
            "\n Make sure to update the axes list in the Player Input Manager inspector of the main player");

            updateComponent ();
        }
    }

    public int getAxesListIndexByName (string axesName)
    {
        for (int i = 0; i < multiAxesList.Count; i++) {
            if (multiAxesList [i].axesName.Equals (axesName)) {
                return i;
            }
        }

        return -1;
    }

    public void removeAxesElement (int multiAxesIndex, int axesIndex)
    {
        if (multiAxesList.Count > multiAxesIndex && multiAxesList [multiAxesIndex].axes.Count > axesIndex) {
            multiAxesList [multiAxesIndex].axes.RemoveAt (axesIndex);

            print ("IMPORTANT: Axes elements in the group of actions called " + multiAxesList [multiAxesIndex].axesName + " has changed." +
            "\n Make sure to update the axes list in the Player Input Manager inspector of the main player");

            updateComponent ();
        }
    }

    public void removeTouchPanel (int touchPanelIndex)
    {
        string touchPanelName = touchButtonsInfoList [touchPanelIndex].Name;

        touchButtonsInfoList.RemoveAt (touchPanelIndex);

        print ("Touch panel  " + touchPanelName + " removed. Updating all touch panel list\n\n");

        touchPanelIndex++;

        for (int i = 0; i < multiAxesList.Count; i++) {

            List<Axes> currentAxesList = multiAxesList [i].axes;

            for (int j = 0; j < currentAxesList.Count; j++) {
                Axes currentAxes = currentAxesList [j];

                if (currentAxes.touchButtonPanelIndex > 0) {
                    if (currentAxes.touchButtonPanelIndex == touchPanelIndex) {
                        currentAxes.touchButtonPanelIndex = 0;
                        currentAxes.touchButtonIndex = 0;

                        print ("The touch panel and button assigned to the action " + currentAxes.Name + " has been removed, " +
                        "make sure to set a new touch button for that action");
                    } else {
                        int newTouchPanelIndex = getTouchPanelIndexByName (currentAxes.touchButtonPanel);

                        //						print ("panel " + currentAxes.touchButtonPanel + " indice obtenido  " + newTouchPanelIndex);

                        if (newTouchPanelIndex > -1) {
                            newTouchPanelIndex++;

                            if (currentAxes.touchButtonPanelIndex != newTouchPanelIndex) {
                                currentAxes.touchButtonPanelIndex = newTouchPanelIndex;

                                print (currentAxes.Name.ToUpper () + " action updated with touch panel index " + newTouchPanelIndex);
                            }
                        } else {
                            print ("WARNING: Touch Panel called " + currentAxes.touchButtonPanel.ToUpper () + " hasn't been found, make sure to configure an action " +
                            "with that name in the main input manager");

                            currentAxes.touchButtonPanelIndex = 0;
                            currentAxes.touchButtonIndex = 0;
                        }
                    }
                }
            }
        }

        updateTouchButtonListString ();

        updateComponent ();
    }

    public void removeTouchButton (int touchPanelIndex, int touchButtonIndex)
    {
        touchButtonsInfo currentTouchButtonsInfo = touchButtonsInfoList [touchPanelIndex];

        string touchButtonName = currentTouchButtonsInfo.touchButtonList [touchButtonIndex].name;

        currentTouchButtonsInfo.touchButtonList.RemoveAt (touchButtonIndex);

        print ("Touch button  " + touchButtonName + " removed. Updating " + currentTouchButtonsInfo.Name + " touch panel list\n\n");

        touchPanelIndex++;

        for (int i = 0; i < multiAxesList.Count; i++) {

            List<Axes> currentAxesList = multiAxesList [i].axes;

            for (int j = 0; j < currentAxesList.Count; j++) {
                Axes currentAxes = currentAxesList [j];

                //				print (currentAxes.Name + " " + currentAxes.touchButtonPanelIndex + " " + currentAxes.touchButtonIndex);

                if (currentAxes.touchButtonPanelIndex == touchPanelIndex) {

                    if (currentAxes.touchButtonIndex == touchButtonIndex) {
                        currentAxes.touchButtonPanelIndex = 0;
                        currentAxes.touchButtonIndex = 0;

                        print ("The touch button assigned to the action " + currentAxes.Name + " has been removed, make sure to set a new touch" +
                        " button for that action");
                    } else {
                        int newTouchButtonIndex = getTouchButtonIndexByName (currentAxes.touchButtonPanel, currentAxes.touchButtonName);

                        if (newTouchButtonIndex > -1) {
                            if (currentAxes.touchButtonIndex != newTouchButtonIndex) {
                                currentAxes.touchButtonIndex = newTouchButtonIndex;

                                print (currentAxes.Name.ToUpper () + " action updated with touch button index " + newTouchButtonIndex);
                            }
                        } else {
                            print ("WARNING: Touch Button called " + currentAxes.touchButtonName.ToUpper () + " hasn't been found, make sure to configure an action " +
                            "with that name in the main input manager");

                            currentAxes.touchButtonPanelIndex = 0;
                            currentAxes.touchButtonIndex = 0;
                        }
                    }
                }
            }
        }

        updateTouchButtonListString ();

        updateComponent ();
    }

    public void getTouchButtonList ()
    {
        getTouchButtonListByPlayerInputManager (playerInputManagerList [0]);
    }

    void getTouchButtonListByPlayerInputManager (playerInputManager currentPlayerInputManager)
    {
        bool isPlaying = Application.isPlaying;

        if (!isPlaying) {
            checkPlayerInputManagerList ();
        }

        touchButtonList.Clear ();

        currentPlayerInputManager.setTouchPanelActiveState (true);

        touchButtonsInfoList.Clear ();

        currentPlayerInputManager.setTouchPanelActiveState (true);

        List<touchPanelsInfo> touchPanelsInfoList = currentPlayerInputManager.getTouchPanelsList ();

        for (int i = 0; i < touchPanelsInfoList.Count; i++) {

            touchButtonsInfo newTouchButtonsInfo = new touchButtonsInfo ();

            newTouchButtonsInfo.Name = touchPanelsInfoList [i].Name;

            Component [] components = touchPanelsInfoList [i].touchPanel.GetComponentsInChildren (typeof (touchButtonListener));

            foreach (touchButtonListener child in components) {
                newTouchButtonsInfo.touchButtonList.Add (child);

                child.setButtonIconComponent ();

                touchButtonList.Add (child);
            }

            touchButtonsInfoList.Add (newTouchButtonsInfo);
        }

        if (!usingTouchControls) {
            currentPlayerInputManager.setTouchPanelActiveState (false);
        }

        if (!isPlaying) {
            pauseManager.updateTouchButtonsComponents ();

            updateComponent ();
        }
    }

    public void setNewTouchButtonListByPlayerInputManager (playerInputManager currentPlayerInputManager)
    {
        touchButtonList = currentPlayerInputManager.getTouchButtonList ();

        touchButtonsInfoList = currentPlayerInputManager.getTouchButtonsInfoList ();
    }

    public void setTouchButtonVisibleState (bool state)
    {
        for (int i = 0; i < touchButtonList.Count; i++) {
            if (touchButtonList [i] != null) {
                if (!buttonsToIgnoreSetVisibleStateList.Contains (touchButtonList [i])) {
                    touchButtonList [i].setButtonColorVisibleState (state);
                }
            }
        }

        for (int i = 0; i < playerInputManagerList.Count; i++) {
            playerInputManagerList [i].setJoystickColorVisibleState (state);
        }
    }

    public void resetButtonStateOnTouchButtonList ()
    {
        for (int i = 0; i < touchButtonList.Count; i++) {
            if (touchButtonList [i] != null) {
                touchButtonList [i].resetButtonState ();
            }
        }
    }

    public bool checkNullElementsOnPlayerInputManagerList ()
    {
        if (playerInputManagerList.Count == 0) {
            return true;
        }

        for (int i = 0; i < playerInputManagerList.Count; i++) {
            if (playerInputManagerList [i] == null) {
                return true;
            }
        }

        return false;
    }

    public void toggleTouchButtonsVisibleState ()
    {
        touchButtonsCurrentlyVisible = !touchButtonsCurrentlyVisible;

        setTouchButtonVisibleState (touchButtonsCurrentlyVisible);
    }

    public bool isTouchButtonsCurrentlyVisible ()
    {
        return touchButtonsCurrentlyVisible;
    }

    public void getTouchButtonListString ()
    {
        getTouchButtonList ();

        updateTouchButtonListString ();

        updateTouchButtonList ();

        print ("Touch panels and buttons stored properly");
    }

    public void selectMainTouchButtonPanelOnEditor ()
    {
        if (playerInputManagerList.Count > 0 && playerInputManagerList [0] != null) {
            GKC_Utils.setActiveGameObjectInEditor (playerInputManagerList [0].getTouchPanel ());
        }
    }

    public void updateTouchButtonListString ()
    {
        touchButtonListString = new string [touchButtonsInfoList.Count + 1];
        touchButtonListString [0] = "None";

        for (int i = 0; i < touchButtonsInfoList.Count; i++) {
            string newName = touchButtonsInfoList [i].Name;
            touchButtonListString [i + 1] = newName;
        }

        touchButtonsStringInfoList.Clear ();

        touchButtonsStringInfoList.Add (new touchButtonsStringInfo ());

        for (int i = 0; i < touchButtonsInfoList.Count; i++) {

            touchButtonsStringInfo newTouchButtonsStringInfo = new touchButtonsStringInfo ();
            newTouchButtonsStringInfo.Name = touchButtonsInfoList [i].Name;

            newTouchButtonsStringInfo.touchButtonListString = new string [touchButtonsInfoList [i].touchButtonList.Count];

            for (int j = 0; j < touchButtonsInfoList [i].touchButtonList.Count; j++) {
                string newName = touchButtonsInfoList [i].touchButtonList [j].gameObject.name;
                newTouchButtonsStringInfo.touchButtonListString [j] = newName;
            }

            touchButtonsStringInfoList.Add (newTouchButtonsStringInfo);
        }

        updateComponent ();
    }

    public void updateTouchButtonList ()
    {
        for (int i = 0; i < multiAxesList.Count; i++) {

            List<Axes> currentAxesList = multiAxesList [i].axes;

            for (int j = 0; j < currentAxesList.Count; j++) {
                Axes currentAxes = currentAxesList [j];

                if (currentAxes.touchButtonPanelIndex > 0) {
                    int newTouchPanelIndex = getTouchPanelIndexByName (currentAxes.touchButtonPanel);

                    if (newTouchPanelIndex > -1) {
                        newTouchPanelIndex++;

                        if (currentAxes.touchButtonPanelIndex != newTouchPanelIndex) {
                            currentAxes.touchButtonPanelIndex = newTouchPanelIndex;
                        }

                        int newTouchButtonIndex = getTouchButtonIndexByName (currentAxes.touchButtonPanel, currentAxes.touchButtonName);

                        if (newTouchButtonIndex > -1) {
                            if (currentAxes.touchButtonIndex != newTouchButtonIndex) {
                                currentAxes.touchButtonIndex = newTouchButtonIndex;

                                print ("Multi axes " + multiAxesList [i].axesName + " " + currentAxes.Name.ToUpper () + " updated with index " + newTouchButtonIndex + " for " + currentAxes.touchButtonName);
                            }
                        } else {
                            print ("WARNING: Touch Button called " + currentAxes.touchButtonName.ToUpper () + " hasn't been found, make sure to configure an action " +
                            "with that name in the main input manager");

                            currentAxes.touchButtonPanelIndex = 0;
                            currentAxes.touchButtonIndex = 0;
                        }
                    } else {
                        print ("WARNING: Touch Panel called " + currentAxes.touchButtonPanel.ToUpper () + " hasn't been found, make sure to configure an action " +
                        "with that name in the main input manager");

                        currentAxes.touchButtonPanelIndex = 0;
                        currentAxes.touchButtonIndex = 0;
                    }
                }
            }
        }

        updateComponent ();
    }

    public int getTouchPanelIndexByName (string touchPanelName)
    {
        for (int i = 0; i < touchButtonsInfoList.Count; i++) {
            if (touchButtonsInfoList [i].Name.Equals (touchPanelName)) {
                return i;
            }
        }

        return -1;
    }


    public int getTouchButtonIndexByName (string touchPanelName, string touchButtonName)
    {
        for (int i = 0; i < touchButtonsInfoList.Count; i++) {

            touchButtonsInfo currentTouchButtonsInfo = touchButtonsInfoList [i];

            if (currentTouchButtonsInfo.Name.Equals (touchPanelName)) {
                for (int j = 0; j < currentTouchButtonsInfo.touchButtonList.Count; j++) {
                    if (currentTouchButtonsInfo.touchButtonList [j].name.Equals (touchButtonName)) {
                        return j;
                    }
                }
            }
        }

        return -1;
    }

    public void showCurrentInputActionList (bool showOnlyKeys, bool showOnlyJoystick)
    {
        currentInputActionList.Clear ();

        currentInputActionList.Add ("TOTAL INPUT LIST AMOUNT: " + multiAxesList.Count + "\n\n\n");

        int numberOfActions = 0;

        int numberOfActionsFound = 0;

        for (int i = 0; i < multiAxesList.Count; i++) {
            string newStringToAdd = "";

            newStringToAdd += multiAxesList [i].axesName.ToUpper () + "\n";

            List<Axes> currentAxesList = multiAxesList [i].axes;

            int numberOfActionsFoundInFilter = 0;

            for (int j = 0; j < currentAxesList.Count; j++) {

                Axes currentAxes = currentAxesList [j];

                if (keyFilterActive) {
                    if (useNewInputSystem) {
#if ENABLE_INPUT_SYSTEM
                        bool checkInputResult = false;

                        if (filterForMouseButton) {
                            if (currentAxes.useMouseButtonType) {
                                if (currentAxes.mouseButtonInput == newInputMouseButtonToFilter) {
                                    checkInputResult = true;
                                }
                            }
                        } else {
                            if (!currentAxes.useMouseButtonType) {
                                if (currentAxes.keyInput == newInputActionKeyToFilter) {
                                    checkInputResult = true;
                                }
                            }
                        }

                        if (checkInputResult) {
                            newStringToAdd += "     -" + currentAxes.Name + "\n";

                            numberOfActionsFoundInFilter++;

                            numberOfActionsFound++;
                        }
#endif
                    } else {
                        if (currentAxes.key == actionKeyToFilter) {
                            newStringToAdd += "     -" + currentAxes.Name + "\n";

                            numberOfActionsFoundInFilter++;

                            numberOfActionsFound++;
                        }
                    }
                } else if (joystickFilterActive) {
                    if (useNewInputSystem) {
#if ENABLE_INPUT_SYSTEM
                        if (currentAxes.gamepadButton == newInputGamepadButtonToFilter) {
                            newStringToAdd += "     -" + currentAxes.Name + "\n";

                            numberOfActionsFoundInFilter++;

                            numberOfActionsFound++;
                        }
#endif
                    } else {
                        if (currentAxes.joystickButton == joystickButtonToFilter) {
                            newStringToAdd += "     -" + currentAxes.Name + "\n";

                            numberOfActionsFoundInFilter++;

                            numberOfActionsFound++;
                        }
                    }
                } else {
                    if (showOnlyKeys) {
                        if (useNewInputSystem) {
#if ENABLE_INPUT_SYSTEM
                            newStringToAdd += "     -" + currentAxes.keyInput.ToString () + "\n";
#endif
                        } else {
                            newStringToAdd += "     -" + currentAxes.key.ToString () + "\n";
                        }
                    } else if (showOnlyJoystick) {
                        if (useNewInputSystem) {
#if ENABLE_INPUT_SYSTEM
                            newStringToAdd += "     -" + currentAxes.gamepadButton.ToString () + "\n";
#endif
                        } else {
                            newStringToAdd += "     -" + currentAxes.joystickButton.ToString () + "\n";
                        }
                    } else {
                        newStringToAdd += "     -" + currentAxes.Name + "  ------>  ";

                        if (useNewInputSystem) {
#if ENABLE_INPUT_SYSTEM
                            newStringToAdd += currentAxes.keyInput.ToString ().ToUpper () + "  ------>  ";
#endif
                        } else {
                            newStringToAdd += currentAxes.key.ToString ().ToUpper () + "  ------>  ";
                        }

                        if (useNewInputSystem) {
#if ENABLE_INPUT_SYSTEM
                            newStringToAdd += currentAxes.gamepadButton.ToString ().ToUpper () + " ";
#endif
                        } else {
                            newStringToAdd += currentAxes.joystickButton.ToString ().ToUpper () + " ";
                        }

                        newStringToAdd += "\n";
                    }
                }

                numberOfActions++;
            }

            if ((keyFilterActive || joystickFilterActive) && numberOfActionsFoundInFilter == 0) {
                newStringToAdd = newStringToAdd.Replace ((multiAxesList [i].axesName.ToUpper () + "\n"), "");
            } else {
                newStringToAdd += "\n\n";
            }

            if (newStringToAdd != "") {
                currentInputActionList.Add (newStringToAdd);
            }
        }

        if (keyFilterActive || joystickFilterActive) {
            if (numberOfActionsFound == 0) {
                currentInputActionList.Add ("\n\n NO ACTIONS FOUND\n\n");
            } else {
                currentInputActionList.Add ("\n\n TOTAL ACTIONS FOUND: " + numberOfActionsFound + "\n\n");
            }
        }

        currentInputActionList.Add ("\n\nTOTAL ACTIONS AMOUNT: " + numberOfActions + "\n\n");

        updateComponent ();
    }

    public void showCurrentInputActionListByKeyFilter ()
    {
        keyFilterActive = true;

        joystickFilterActive = false;

        showCurrentInputActionList (false, false);

        keyFilterActive = false;

        updateComponent ();
    }

    public void showCurrentInputActionListByJoystickFilter ()
    {
        joystickFilterActive = true;

        keyFilterActive = false;

        showCurrentInputActionList (false, false);

        joystickFilterActive = false;

        updateComponent ();
    }

    public void clearInputActionListText ()
    {
        currentInputActionList.Clear ();

        updateComponent ();
    }


    public void convertKeyCodeToKeys ()
    {
#if ENABLE_INPUT_SYSTEM
        int multiAxesListCount = multiAxesList.Count;

        for (int i = 0; i < multiAxesListCount; i++) {
            List<Axes> currentAxesList = multiAxesList [i].axes;

            for (int j = 0; j < currentAxesList.Count; j++) {

                Axes currentAxes = currentAxesList [j];

                KeyCode currentKeyCode = currentAxes.key;

                bool keyIsMouseResult = (currentKeyCode >= KeyCode.Mouse0 && currentKeyCode <= KeyCode.Mouse6);

                if (keyIsMouseResult) {
                    currentAxes.useMouseButtonType = true;

                    if (InputAxisWrapper.TryGetMouseButton (currentKeyCode, out var mouseButton)) {
                        Debug.Log ("mouse found " + mouseButton.ToString () + " " + currentAxes.Name);

                        currentAxes.mouseButtonInput = mouseButton;
                    } else {
                        Debug.Log ("Mouse Not convertible " + currentAxes.Name + " " + currentKeyCode.ToString ());
                    }

                } else {
                    if (InputAxisWrapper.keycodeToKeyDictionary.TryGetValue (currentKeyCode, out var newKey)) {
                        Debug.Log ("key found " + newKey + " " + currentAxes.Name);

                        currentAxes.keyInput = newKey;
                    } else {
                        Debug.Log ("Key Not convertible " + currentAxes.Name + " " + currentKeyCode.ToString ());
                    }
                }

                if (currentAxes.joystickButton == joystickButtons.None) {
                    currentAxes.gamepadButton = newGamepadButtonNone;

                    Debug.Log (currentAxes.Name + " not assigned for gamepad, setting as none");
                } else {
                    currentAxes.gamepadButton = InputAxisWrapper.convertOldJoystickToNewInputButton (currentAxes.joystickButton);

                    Debug.Log (currentAxes.Name + " assigned for gamepad from  " + currentAxes.joystickButton
                        + " to " + currentAxes.gamepadButton.ToString ());
                }
            }
        }

        updateInputInspector ();
#endif
    }

    public void confirmInputActionRename ()
    {
        if (inputActionCategoryNameToSearch == "") {
            return;
        }

        if (inputActionNameToRename == "") {
            return;
        }

        if (newInputActionNameToUse == "") {
            return;
        }

        int multiAxesIndex = multiAxesList.FindIndex (s => s.axesName.Equals (inputActionCategoryNameToSearch));

        if (multiAxesIndex > -1) {

            int axesIndex = multiAxesList [multiAxesIndex].axes.FindIndex (s => s.Name.Equals (inputActionNameToRename));

            if (axesIndex > -1) {
                currentKeyButtonToCheck = multiAxesList [multiAxesIndex].axes [axesIndex];


                currentKeyButtonToCheck.Name = newInputActionNameToUse;

                print ("Input Action to rename found, updating info on Input Manager and Player Input Manager from " +
                    inputActionNameToRename + " to " + newInputActionNameToUse);


                for (int i = 0; i < playerInputManagerList.Count; i++) {
                    if (playerInputManagerList [i] != null) {
                        playerInputManagerList [i].renameInputAction (inputActionCategoryNameToSearch, inputActionNameToRename, newInputActionNameToUse);
                    }
                }

                updateInputInspector ();
            } else {
                print ("Input Action to rename not found");
            }
        }
    }

    public void confirmInputActionCategoryRename ()
    {
        if (inputActionCategoryNameToRename == "") {
            return;
        }

        if (newInputActionCategoryName == "") {
            return;
        }

        int multiAxesIndex = multiAxesList.FindIndex (s => s.axesName.Equals (inputActionCategoryNameToRename));

        if (multiAxesIndex > -1) {

            multiAxes currentMultiAxes = multiAxesList [multiAxesIndex];


            currentMultiAxes.axesName = newInputActionCategoryName;

            print ("Input Action Category to rename found, updating info on Input Manager and Player Input Manager from " +
                inputActionCategoryNameToRename + " to " + newInputActionCategoryName);


            for (int i = 0; i < playerInputManagerList.Count; i++) {
                if (playerInputManagerList [i] != null) {
                    playerInputManagerList [i].renameInputActionCategory (inputActionCategoryNameToRename, newInputActionCategoryName);
                }
            }

            updateInputInspector ();

        } else {
            print ("Input Action Category to rename not found");
        }
    }

    public void enableOrDisableNewInputSystemFromEditor (bool state)
    {
        useNewInputSystem = state;
        useNewInputKeys = state;

        print ("Enable Or Disable New Input System From Editor state " + state);

        updateInputInspector ();
    }

    bool getFileContent (string path, out SaveSlotListWrapper saveData, bool showDebugInfo)
    {
        bool fileResult = false;

        saveData = new SaveSlotListWrapper ();

        if (File.Exists (path)) {
            try {
                string json = File.ReadAllText (path);

                if (string.IsNullOrWhiteSpace (json)) {
                    if (showDebugInfo) {
                        Debug.Log ("Save File located but empty" + path);
                        Debug.Log ("\n\n");
                    }
                } else {
                    saveData = JsonUtility.FromJson<SaveSlotListWrapper> (json);

                    if (saveData == null) {
                        saveData = new SaveSlotListWrapper ();
                    }

                    if (saveData.infoList == null) {
                        saveData.infoList = new List<multiAxes> ();
                    }

                    fileResult = true;

                    if (showDebugInfo) {
                        Debug.Log ("Save File located " + path);
                        Debug.Log ("\n\n");
                    }
                }
            }

            catch (Exception ex) {
                Debug.LogError (
                    $"Error loading save file {path}\n{ex}"
                );

                saveData = new SaveSlotListWrapper ();

                if (saveData.infoList == null) {
                    saveData.infoList = new List<multiAxes> ();
                }
            }
        } else {
            if (showDebugInfo) {
                Debug.Log ("Save File not located " + path);
                Debug.Log ("\n\n");
            }
        }

        return fileResult;
    }

    void saveFileContent (string path, string jsonToSave)
    {
        try {
            string dir = Path.GetDirectoryName (path);

            if (!string.IsNullOrEmpty (dir)) {
                Directory.CreateDirectory (dir);
            }

            File.WriteAllText (path, jsonToSave, System.Text.Encoding.UTF8);
        }
        catch (Exception ex) {
            Debug.LogError (
                $"Error saving file {path}\n{ex}"
            );
        }
    }

    [System.Serializable]
    public class SaveSlotListWrapper
    {
        public List<multiAxes> infoList = new List<multiAxes> ();
    }

    void updateComponent ()
    {
        GKC_Utils.updateComponent (this);
    }

    [System.Serializable]
    public class Axes
    {
        public string Name;

        public bool actionEnabled = true;

        public bool showActionInRebindPanel = true;

        public string keyButton;

        public KeyCode key = KeyCode.A;


        public joystickButtons joystickButton;


        public int touchButtonPanelIndex;
        public int touchButtonIndex = 0;
        public string touchButtonName;
        public string touchButtonPanel;

        public int currentTouchButtonIndex;


        public bool useMouseButtonType;

#if ENABLE_INPUT_SYSTEM
        public Key keyInput;

        public MouseButton mouseButtonInput;


        public GamepadButton gamepadButton;
#endif

#if REWIRED
		[ActionIdProperty(typeof(RewiredConsts.Action))]
		public int rewiredAction = -1;

		public enum AxisContribution {
			Positive,
			Negative,
			Both
		}

		public AxisContribution axisContribution;
#endif

        //some constructors for a key input, incluing name, key button and touch button
        public Axes ()
        {
            Name = "";
            keyButton = "";
            actionEnabled = true;
        }

        public Axes (string n, string key)
        {
            Name = n;
            keyButton = key;
            actionEnabled = true;
        }
    }

    public enum joystickButtons
    {
        A = 0,
        B = 1,
        X = 2,
        Y = 3,
        LeftBumper = 4,
        RightBumper = 5,
        Back = 6,
        Start = 7,
        LeftStickClick = 8,
        RightStickClick = 9,
        LeftDPadX = 10,
        RightDPadX = 11,
        TopDPadY = 12,
        BottomDPadY = 13,
        LeftTrigger = 14,
        RightTrigger = 15,
        None = -1
    }

    [System.Serializable]
    public class gamepadInfo
    {
        public int gamepadNumber;
        public string gamepadName;

        public List<multiGamepadAxisInfo> multiGamepadAxisInfoList = new List<multiGamepadAxisInfo> ();

        public List<string> joystickButtonStringList = new List<string> ();

        public List<string> axisButtonStringList = new List<string> ();
    }

    [System.Serializable]
    public class newInputGamepadInfo
    {
        public int id;

#if ENABLE_INPUT_SYSTEM
        public Gamepad gamepad;
#endif
        public bool connected;

        public int playerIndex;

        public string deviceName;

        public int deviceId;

        public bool isActive;

        public float lastInputTime;

        public string deviceKey;

        public Coroutine rumbleCoroutine;

        public bool rumbleActive;

        public bool gameIsPaused;
    }

    [System.Serializable]
    public class multiGamepadAxisInfo
    {
        public string Name;
        public List<gamepadAxisInfo> gamepadAxisInfoList = new List<gamepadAxisInfo> ();
    }

    [System.Serializable]
    public class gamepadAxisInfo
    {
        public string Name;

        public bool usingRightTrigger;
        public bool usingLeftTrigger;
        public bool rightTriggerDown;
        public bool leftTriggerDown;

        public bool usingDPadXPositive;
        public bool DPadXPositiveDown;

        public bool usingDPadXNegative;
        public bool DPadXNegativeDown;

        public bool usingDPadYPositive;
        public bool DPadYPositiveDown;

        public bool usingDPadYNegative;
        public bool DPadYNegativeDown;
    }

    [System.Serializable]
    public class multiAxes
    {
        public string axesName;
        public bool currentlyActive = true;
        public List<Axes> axes = new List<Axes> ();

        public bool multiAxesEditPanelActive = true;

        public multiAxes ()
        {

        }

        public multiAxes (multiAxes newAxes)
        {
            axesName = newAxes.axesName;
            currentlyActive = newAxes.currentlyActive;
            axes = newAxes.axes;
            multiAxesEditPanelActive = newAxes.multiAxesEditPanelActive;
        }
    }

    [System.Serializable]
    public class multiAxesEditButtonInput
    {
        public List<editButtonInput> buttonsList = new List<editButtonInput> ();

        public GameObject multiAxesEditPanel;

        public bool multiAxesEditPanelActive;

        public ScrollRect multiAxesEditPanelScrollRect;
    }

    [System.Serializable]
    public class touchButtonsInfo
    {
        public string Name;
        public List<touchButtonListener> touchButtonList = new List<touchButtonListener> ();
    }

    [System.Serializable]
    public class touchButtonsStringInfo
    {
        public string Name;
        public string [] touchButtonListString;
    }
}
