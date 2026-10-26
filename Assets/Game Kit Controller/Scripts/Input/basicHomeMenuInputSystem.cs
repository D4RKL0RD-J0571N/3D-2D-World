using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using UnityEngine.Events;
using static inputManager;


#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class basicHomeMenuInputSystem : MonoBehaviour
{
    [Header ("Movement")]
    [Space]

    public bool useNewInputSystem = true;

    public bool ignoreGamepad;

    [Space]
    [Header ("Other Settings")]
    [Space]

    public int joystickNumber = 1;

    public bool vibrationEnabled = true;

    [Space]
    [Header ("Event Settings")]
    [Space]

    public bool useEventOnGamepadBackButtonPressedDown;

    public UnityEvent eventOnGamepadBackButtonPressedDown;

    [Space]

    public UnityEvent eventOnGamepadRightTriggerClick;

    [Space]

    public UnityEvent eventOnGamepadLeftTriggerClick;

    [Space]

    public UnityEvent eventOnGamepadRightJoystickPressClick;

    [Space]
    [Header ("Debug")]
    [Space]

    public bool showGamepadDebug;

    public int numberOfGamepads;

    public List<newInputGamepadInfo> newInputGamepadInfoListDebug = new List<newInputGamepadInfo> ();

    private Dictionary<int, newInputGamepadInfo> newInputGamepadInfoList = new Dictionary<int, newInputGamepadInfo> ();


#if ENABLE_INPUT_SYSTEM

    [Space]
    [Header ("Components")]
    [Space]

    public InputActionReference moveActionReference;
    public InputActionReference lookActionReference;

#endif

    [Space]
    [Header ("Event Settings")]
    [Space]

    public bool useEventOnGamepadStateChange;

    public UnityEvent eventOnGamepadConnected;
    public UnityEvent eventOnGamepadRemoved;


    bool rescanPossibleGamepadsActive = false;

    int currentNewInputGamepadInfoIndex = 0;

    bool touchPlatform;

    float lastTimeStart = 0;



    void Awake ()
    {
        initializeInputElements ();
    }

    void Start ()
    {
        setGamePadList ();
    }

    void Update ()
    {
        if (useEventOnGamepadBackButtonPressedDown) {
            if (joystickNumber > 0 && GamepadRegistry.IsValid (joystickNumber)) {
                if (GamepadRegistry.GetReturnClickDown (joystickNumber)) {
                    if (showGamepadDebug) {
                        print ("gamepad back button pressed");
                    }

                    eventOnGamepadBackButtonPressedDown.Invoke ();
                }

                if (GamepadRegistry.GetRightTriggerClick (joystickNumber)) {
                    if (showGamepadDebug) {
                        print ("gamepad right trigger button pressed");
                    }

                    eventOnGamepadRightTriggerClick.Invoke ();
                }

                if (GamepadRegistry.GetLeftTriggerClick (joystickNumber)) {
                    if (showGamepadDebug) {
                        print ("gamepad left trigger button pressed");
                    }

                    eventOnGamepadLeftTriggerClick.Invoke ();
                }

                if (GamepadRegistry.GetRightJoystickPressClick (joystickNumber)) {
                    if (showGamepadDebug) {
                        print ("gamepad right joystick press button pressed");
                    }

                    eventOnGamepadRightJoystickPressClick.Invoke ();
                }
            }
        }

        if (useNewInputSystem && !ignoreGamepad) {
            if (touchPlatform) {
                if (rescanPossibleGamepadsActive && Time.time > lastTimeStart + 0.4f) {
                    if (showGamepadDebug) {
                        print ("checking for possible gamepads conencted");
                    }

                    rescanPossibleGamepadsActive = false;

                    if (numberOfGamepads == 0) {
                        if (showGamepadDebug) {
                            print ("no gamepads stored, checking");
                        }

                        setGamePadList ();

                        if (showGamepadDebug) {
                            print ("numberOfGamepads " + numberOfGamepads);
                        }

                        if (numberOfGamepads == 0) {
                            checkEventsOnGamepadStateChange (false);
                        } else {
                            checkEventsOnGamepadStateChange (true);
                        }
                    } else {
                        checkEventsOnGamepadStateChange (true);

                        if (showGamepadDebug) {
                            print ("gamepads found, checking");
                        }
                    }
                }
            }
        }
    }

    void initializeInputElements ()
    {
#if !ENABLE_INPUT_SYSTEM
    useNewInputSystem = false;
#endif

        if (useNewInputSystem) {
#if ENABLE_INPUT_SYSTEM
            InputAxisWrapper.InitializeMove (moveActionReference.action);

            InputAxisWrapper.InitializeLook (lookActionReference.action);

            InputAxisWrapper.initializeInputSources ();

            InputAxisWrapper.setUseNewInputSystemState (true);

            touchPlatform = InputAxisWrapper.checkTouchPlatform ();

            lastTimeStart = Time.time;
#endif
        } else {
            InputAxisWrapper.setUseNewInputSystemState (false);
        }
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
                    addOrRemoveNewInputGamepad (gamepad, gamepad.deviceId, true);

                    if (showGamepadDebug) {
                        Debug.Log ($"Gamepad connected: {gamepad.displayName}");
                        Debug.Log ($"Gamepads amount: {Gamepad.all.Count}");
                    }

                    break;

                case InputDeviceChange.Removed:
                    addOrRemoveNewInputGamepad (gamepad, gamepad.deviceId, false);

                    if (showGamepadDebug) {
                        Debug.Log ($"Gamepad disconnected: {gamepad.displayName}");
                        Debug.Log ($"Gamepads amount: {Gamepad.all.Count}");
                    }

                    break;
            }
        }

        if (InputAxisWrapper.checkTouchPlatform ()) {
            if (change == InputDeviceChange.Added ||
            change == InputDeviceChange.Disconnected ||
            change == InputDeviceChange.Reconnected) {
                rescanPossibleGamepadsActive = true;

                print ("OnDeviceChange function triggered");
            }
        }
    }

    void addOrRemoveNewInputGamepad (Gamepad gamepadToCheck, int deviceID, bool state)
    {
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

                currentNewInputGamepadInfo.Value.gamepad = null;

                updateGamepadStateResult = true;

                if (showGamepadDebug) {
                    print ("gamepad disconnected");
                }
            }
        }

        if (showGamepadDebug) {
            print ("updateGamepadStateResult " + updateGamepadStateResult);
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

                checkEventsOnGamepadStateChange (false);
            } else {
                checkEventsOnGamepadStateChange (true);
            }
        }
    }
#endif

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
                    addOrRemoveNewInputGamepad (pad, pad.deviceId, true);
                }

                numberOfGamepads = 0;

                foreach (var value in newInputGamepadInfoList.Values) {
                    if (value.connected) {
                        numberOfGamepads++;
                    }
                }
            }
#endif
        }
    }

    void checkEventsOnGamepadStateChange (bool state)
    {
        if (useEventOnGamepadStateChange) {
            if (state) {
                eventOnGamepadConnected.Invoke ();
            } else {
                eventOnGamepadRemoved.Invoke ();

            }
        }
    }

    //VIBRATION FUNCTIONS

    public void setGamepadVibration (string infoString)
    {
        if (!vibrationEnabled) {
            return;
        }

        bool splitSymbolLocated = infoString.Contains (";");

        if (splitSymbolLocated) {
            string [] vibrationFields = infoString.Split (';');

            if (vibrationFields.Length >= 4) {
                int playerID = int.Parse (vibrationFields [0]);

                float lowAmount = float.Parse (vibrationFields [1]);

                float hightAmount = float.Parse (vibrationFields [2]);

                float durationAmount = float.Parse (vibrationFields [3]);


                setGamepadVibrationByPlayerID (playerID, lowAmount, hightAmount, durationAmount);
            }
        }
    }

    void stopAllGamepadsVibration ()
    {
#if ENABLE_INPUT_SYSTEM
        InputSystem.ResetHaptics ();
#endif
    }

    public void setGamepadVibrationByPlayerID (int playerID, float low, float high)
    {
        if (!vibrationEnabled) {
            return;
        }

        if (!newInputGamepadInfoList.TryGetValue (playerID, out var currentNewInputGamepadToCheck)) {
            return;
        }

#if ENABLE_INPUT_SYSTEM
        currentNewInputGamepadToCheck.gamepad.SetMotorSpeeds (low, high);
#endif
    }

    public void setGamepadVibrationByPlayerID (int playerID, float low, float high, float duration)
    {
        if (!vibrationEnabled) {
            return;
        }

        if (!newInputGamepadInfoList.TryGetValue (playerID, out var currentNewInputGamepadToCheck)) {
            return;
        }

        // cancel previous vibration
        if (currentNewInputGamepadToCheck.rumbleActive) {
            StopCoroutine (currentNewInputGamepadToCheck.rumbleCoroutine);

            currentNewInputGamepadToCheck.rumbleActive = false;
        }


#if ENABLE_INPUT_SYSTEM
        currentNewInputGamepadToCheck.rumbleCoroutine =
            StartCoroutine (setGamepadVibrationByPlayerIDCoroutine (currentNewInputGamepadToCheck, low, high, duration));
#endif
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
        if (!vibrationEnabled) {
            return;
        }

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
}
