using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

using static InputAxisWrapper;
using static inputManager;

#if REWIRED
using Rewired;
#endif

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.LowLevel;
#endif

public class playerInputManager : MonoBehaviour
{
    public List<multiAxes> multiAxesList = new List<multiAxes> ();

    public int playerID;
    public bool inputEnabled = true;

    public bool showDebugActions;

    public bool inputCurrentlyActive = true;

    public inputManager input;
    public playerController playerControllerManager;

    public bool ignoreGamepad;
    public bool allowKeyboardAndGamepad;
    public bool allowGamepadInTouchDevice;

    public touchJoystick touchMovementControl;
    public touchJoystick touchCameraControl;
    [Range (0, 10)] public float leftTouchSensitivity;
    [Range (0, 10)] public float rightTouchSensitivity;
    [Range (0, 10)] public float mouseSensitivity;

    [Range (0, 10)] public float leftGamepadJoystickSensitivity = 1;
    [Range (0, 10)] public float rightGamepadJoystickSensitivity = 1;

    public bool useVerticalAndHorizontalGamepadSensitivity;

    [Range (0, 10)] public float leftGamepadJoystickHorizontalSensitivity = 1;
    [Range (0, 10)] public float leftGamepadJoystickVerticalSensitivity = 1;

    [Range (0, 10)] public float rightGamepadJoystickHorizontalSensitivity = 1;
    [Range (0, 10)] public float rightGamepadJoystickVerticalSensitivity = 1;

    public GameObject touchPanel;

    public bool usingTouchMovementJoystick = true;

    public bool useHorizontaTouchMovementButtons;
    public bool useVerticalTouchMovementButtons;

    public GameObject horizontalTouchMovementButtons;
    public GameObject verticalTouchMovementButtons;
    public float inputLerpSpeedTouchMovementButtons = 5;

    public bool pressingTouchHorizontalRightInput;
    public bool pressingTouchHorizontalLeftInput;

    public bool pressingTouchVerticalUpInput;
    public bool pressingTouchVerticalDownInput;

    public bool useOnlyKeyboard;

    public bool usingGamepad;
    public bool usingKeyBoard;
    public bool usingTouchControls;
    public bool gameManagerPaused;

    public bool useNewInputSystem;

    public string horizontalString = "Horizontal";
    public string verticalString = "Vertical";
    public string mouseXString = "Mouse X";
    public string mouseYString = "Mouse Y";
    public string horizontalXString = "Horizontal X";
    public string verticalYString = "Vertical Y";

    multiAxes currentMultiAxes;
    Axes currentAxes;

    public Vector2 movementAxis;
    public Vector2 mouseAxis;
    public Vector2 rawMovementAxis;

    public bool useMovementOrientation;
    [Range (-1, 1)] public int horizontalMovementOrientation = 1;
    [Range (-1, 1)] public int verticalMovementOrientation = 1;
    public bool useCameraOrientation;
    [Range (-1, 1)] public int horizontalCameraOrientation = 1;
    [Range (-1, 1)] public int verticalCameraOrientation = 1;

    public bool manualControlActive;
    [Range (-1, 1)] public float manualMovementHorizontalInput;
    [Range (-1, 1)] public float manualMovementVerticalInput;
    [Range (-1, 1)] public float manualMouseHorizontalInput;
    [Range (-1, 1)] public float manualMouseVerticalInput;

    public bool inputPaused;

    public bool ignoreAllActionsInput;
    public bool ignoreAllAxisInput;

    public bool showSingleMultiAxes;
    public int currentMultiAxesToShow;
    public bool hideSingleMultiAxes;

    public playerInputPanelSystem mainPlayerInputPanelSystem;

    public List<inputCurrentlyActiveInfo> inputCurrentlyActiveInfoList = new List<inputCurrentlyActiveInfo> ();

    public GameObject editInputPanelPrefab;
    public GameObject editInputMenu;
    public GameObject currentInputPanelListText;

    public virtualTouchSystem mainVirtualTouchSystem;

    public bool playerIsNonLocalAvatar;

    public int playerNonLocalAvatarID;

    public simpleGamepadVibrationHelper mainSimpleGamepadVibrationHelper;

    bool mainSimpleGamepadVibrationHelperLocated;


    string playerIDString;

    string mouseXPlayerIDString;
    string mouseYPlayerIDString;
    string horizontalXPlayerIDString;
    string vertircalYPlayerIDString;

    bool keyboardActive;

    bool ignoreKeyboardInputActive;

    public bool overrideInputValuesActive;
    Vector2 overrideInputAxisValue;

    Vector2 auxMovementAxis;
    Vector2 auxRawMovementAxis;

    bool playerIsDead;

    bool originalUsingTouchMovementJoystickValue;

    bool inputPausedForExternalComponents;

    bool ignoreOverrideInput;

    bool avoidInputActive;

    int MAIndex;
    int AIndex;

    int currentMultiAxesCount;
    int currentAxesCount;

    Vector2 vector2Zero = Vector2.zero;

    public bool ignorePlayerActionsInput;

    bool ignorePlayerIsDeadOnCameraAxis;

    bool lastButtonPressedOnGamepad;
    bool lastKeyPressedOnKeyboard;

    Vector2 temporalRawMovementAxis;

    public bool axisInputPaused;

    public bool movementAxisInputPaused;
    public bool cameraAxisInputPaused;


    float lastTimePlayStarted;

    bool initialInputWaitChecked;

    bool inputManagerLocated;

    bool editingInput;

    public List<touchButtonListener> touchButtonList = new List<touchButtonListener> ();

    public List<touchButtonsInfo> touchButtonsInfoList = new List<touchButtonsInfo> ();

#if REWIRED
	private bool _useRewired;
	private IList<Player> _rewiredPlayers;
#endif

    bool ignoreInputOnGameWindowFocusResult;

    Vector2 currentGamepadMovementAxisValue;

    Vector2 currentGamepadCameraAxisValue;

    Vector2 rawCameraAxis;

    bool mainVirtualTouchSystemLocated;

    bool touchPlatform;

    bool useOnlyPhoneVibrationOnMobile;


    void Awake ()
    {
        findInputManager ();

        mainSimpleGamepadVibrationHelperLocated = mainSimpleGamepadVibrationHelper != null;

        mainVirtualTouchSystemLocated = mainVirtualTouchSystem != null;

        touchPlatform = InputAxisWrapper.checkTouchPlatform ();

        if (mainSimpleGamepadVibrationHelperLocated) {
            useOnlyPhoneVibrationOnMobile = mainSimpleGamepadVibrationHelper.isUseOnlyPhoneVibrationOnMobile ();
        }
    }

    void Start ()
    {
        if (useOnlyKeyboard) {
            playerControllerManager.setPlayerID (0);
        }

        playerID = playerControllerManager.getPlayerID ();

        switchPlayerID (playerID);

        originalUsingTouchMovementJoystickValue = usingTouchMovementJoystick;

        if (input != null) {
            usingTouchControls = input.isUsingTouchControls ();

#if REWIRED
			_useRewired = input.useRewired;
#endif
        }

        currentMultiAxesCount = multiAxesList.Count;

        lastTimePlayStarted = Time.time;

        useNewInputSystem = input.isUseNewInputSystemActive ();

#if REWIRED
		if (_useRewired)
			_rewiredPlayers = ReInput.players.GetPlayers();
#endif
    }

    void Update ()
    {
        updatePlayerInputManager ();
    }

    void updatePlayerInputManager ()
    {
        if (!initialInputWaitChecked) {

            if (Time.time > lastTimePlayStarted + 0.4f) {
                initialInputWaitChecked = true;
            } else {
                return;
            }
        }

        playerIsDead = playerControllerManager.isPlayerDead ();

        if (useNewInputSystem) {
            InputAxisWrapper.updateHasFocusOnGameWindowValue ();

            ignoreInputOnGameWindowFocusResult = !InputAxisWrapper.hasFocusOnGameWindow;
        }

        if (inputEnabled && !ignorePlayerActionsInput && !ignoreInputOnGameWindowFocusResult && !editingInput) {
            gameManagerPaused = input.isGameManagerPaused ();

            if (!inputPaused && !ignoreAllActionsInput) {
                for (MAIndex = 0; MAIndex < currentMultiAxesCount; MAIndex++) {

                    currentMultiAxes = multiAxesList [MAIndex];

                    if (!avoidInputActive || currentMultiAxes.canUseInputOnPlayerPaused) {
                        if (currentMultiAxes.currentlyActive && !currentMultiAxes.ignoreCheckInputActive) {
                            currentAxesCount = currentMultiAxes.axesCount;

                            for (AIndex = 0; AIndex < currentAxesCount; AIndex++) {

                                currentAxes = currentMultiAxes.axes [AIndex];

                                if (currentAxes.actionEnabled) {
                                    if (inputCurrentlyActive || currentAxes.canBeUsedOnPausedGame) {
                                        if (input.checkPlayerInputButtonFromMultiAxesList (currentMultiAxes.multiAxesStringIndex, currentAxes.axesStringIndex,
                                                currentAxes.buttonPressType, playerID, currentAxes.canBeUsedOnPausedGame, useOnlyKeyboard, ignoreKeyboardInputActive)) {

                                            if (showDebugActions) {
                                                print (currentAxes.Name);
                                            }

                                            currentAxes.buttonEvent.Invoke ();

                                            if (playerIsNonLocalAvatar) {
                                                sendInputSignalExternally (MAIndex, AIndex);
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }

            //convert the input from keyboard or a touch screen into values to move the player, given the camera direction
            //also, it checks in the player is using a device, like a vehicle
            //convert the mouse input in the tilt angle for the camera or the input from the touch screen depending of the settings
            //also, it checks in the player is using a device, like a vehicle

            usingTouchControls = input.isUsingTouchControls ();

            usingKeyBoard = input.isUsingKeyBoard ();

            allowKeyboardAndGamepad = input.isAllowKeyboardAndGamepad ();

            allowGamepadInTouchDevice = input.isAllowGamepadInTouchDevice ();

            usingGamepad = input.isUsingGamepad ();

            if (usingKeyBoard || allowKeyboardAndGamepad || useOnlyKeyboard) {
                if (!usingTouchControls) {
#if REWIRED
					if (_useRewired) {
						movementAxis.x = _rewiredPlayers[playerID - 1].GetAxis (horizontalString);
						movementAxis.y = _rewiredPlayers[playerID - 1].GetAxis (verticalString);

						mouseAxis.x = _rewiredPlayers[playerID - 1].GetAxis (mouseXString);
						mouseAxis.y = _rewiredPlayers[playerID - 1].GetAxis (mouseYString);

						rawMovementAxis.x = _rewiredPlayers[playerID - 1].GetAxisRaw (horizontalString);
						rawMovementAxis.y = _rewiredPlayers[playerID - 1].GetAxisRaw (verticalString);
					}
					else {
#endif

                    if (useNewInputSystem) {
                        movementAxis = input.getKeyboardAxisInputValue ();

                        mouseAxis = input.getMouseAxisInputValue ();

                        rawMovementAxis = input.getKeyboardRawAxisInputValue ();
                    } else {
                        movementAxis.x = InputAxisWrapper.getOldInputAxisValue (horizontalString);
                        movementAxis.y = InputAxisWrapper.getOldInputAxisValue (verticalString);

                        mouseAxis.x = InputAxisWrapper.getOldInputAxisValue (mouseXString);
                        mouseAxis.y = InputAxisWrapper.getOldInputAxisValue (mouseYString);

                        rawMovementAxis.x = InputAxisWrapper.getOldInputAxisRawValue (horizontalString);
                        rawMovementAxis.y = InputAxisWrapper.getOldInputAxisRawValue (verticalString);
                    }

#if REWIRED
					}
#endif

                    mouseAxis *= mouseSensitivity;

#if REWIRED
					if (!_useRewired) {
#endif

                    keyboardActive = true;

                    if (!lastKeyPressedOnKeyboard && rawMovementAxis != vector2Zero) {
                        //						print ("pressing keyboard");

                        GKC_Utils.eventOnPressingKeyboardInput (playerID);

                        lastKeyPressedOnKeyboard = true;

                        lastButtonPressedOnGamepad = false;

                        input.checkButtonPressedOnGamepadOrKeyboard (true);
                    }

#if REWIRED
					}
#endif

                } else {
                    if (usingTouchMovementJoystick) {
                        movementAxis = leftTouchSensitivity * touchMovementControl.GetAxis ();
                    } else {
                        float movementHorizontalValue = 0;
                        float movementVerticalValue = 0;

                        if (pressingTouchHorizontalRightInput) {
                            movementHorizontalValue = 1;
                        }

                        if (pressingTouchHorizontalLeftInput) {
                            movementHorizontalValue = -1;
                        }

                        if ((pressingTouchHorizontalRightInput && pressingTouchHorizontalLeftInput) || (!pressingTouchHorizontalRightInput && !pressingTouchHorizontalLeftInput)) {
                            movementHorizontalValue = 0;
                        }

                        if (pressingTouchVerticalUpInput) {
                            movementVerticalValue = 1;
                        }

                        if (pressingTouchVerticalDownInput) {
                            movementVerticalValue = -1;
                        }

                        if ((pressingTouchVerticalUpInput && pressingTouchVerticalDownInput) || (!pressingTouchVerticalUpInput && !pressingTouchVerticalDownInput)) {
                            movementVerticalValue = 0;
                        }

                        movementAxis = Vector2.MoveTowards (movementAxis, leftTouchSensitivity * new Vector2 (movementHorizontalValue, movementVerticalValue),
                            Time.deltaTime * inputLerpSpeedTouchMovementButtons);
                    }


                    mouseAxis = rightTouchSensitivity * touchCameraControl.GetAxis ();

                    if (usingTouchMovementJoystick) {
                        rawMovementAxis = touchMovementControl.getRawAxis ();
                    } else {
                        if (movementAxis.x > 0) {
                            rawMovementAxis.x = 1;
                        } else if (movementAxis.x < 0) {
                            rawMovementAxis.x = -1;
                        } else {
                            rawMovementAxis.x = 0;
                        }

                        if (movementAxis.y > 0) {
                            rawMovementAxis.y = 1;
                        } else if (movementAxis.y < 0) {
                            rawMovementAxis.y = -1;
                        } else {
                            rawMovementAxis.y = 0;
                        }
                    }

                    if (allowGamepadInTouchDevice) {
                        if (touchPlatform) {
                            keyboardActive = true;
                        }
                    } else {
                        keyboardActive = false;
                    }
                }
            } else {
                keyboardActive = false;
            }

#if REWIRED
			if (!_useRewired && (!usingKeyBoard || allowKeyboardAndGamepad) && !useOnlyKeyboard) {
#else
            if ((!usingKeyBoard || allowKeyboardAndGamepad) && !useOnlyKeyboard) {
#endif
                if (usingGamepad && (!usingTouchControls || allowGamepadInTouchDevice)) {
                    if (!keyboardActive) {
                        movementAxis = vector2Zero;
                        mouseAxis = vector2Zero;
                        rawMovementAxis = vector2Zero;
                    }

                    if (useNewInputSystem) {
                        if (allowKeyboardAndGamepad) {
                            rawMovementAxis += input.getNewInputLeftJoysticksAxisInputValue (playerID);
                        } else {
                            rawMovementAxis = input.getNewInputLeftJoysticksAxisInputValue (playerID);
                        }

                        updateSmoothNewAxis (rawMovementAxis, ref currentGamepadMovementAxisValue);

                        if (allowKeyboardAndGamepad) {
                            movementAxis += currentGamepadMovementAxisValue;
                        } else {
                            movementAxis = currentGamepadMovementAxisValue;
                        }

                        rawCameraAxis = input.getNewInputRightJoysticksAxisInputValue (playerID);

                        updateSmoothNewAxis (rawCameraAxis, ref currentGamepadCameraAxisValue,
                            sensitivity: 20f, gravity: 20f, deadZone: 0.08f, applyCurve: false);

                        if (allowKeyboardAndGamepad) {
                            mouseAxis += currentGamepadCameraAxisValue;
                        } else {
                            mouseAxis = currentGamepadCameraAxisValue;
                        }
                    } else {
                        movementAxis.x += InputAxisWrapper.getOldInputAxisValue (horizontalXPlayerIDString);
                        movementAxis.y += InputAxisWrapper.getOldInputAxisValue (vertircalYPlayerIDString);

                        mouseAxis.x += InputAxisWrapper.getOldInputAxisValue (mouseXPlayerIDString);
                        mouseAxis.y += InputAxisWrapper.getOldInputAxisValue (mouseYPlayerIDString);
                    }

                    if (useVerticalAndHorizontalGamepadSensitivity) {
                        movementAxis.x *= leftGamepadJoystickHorizontalSensitivity;
                        movementAxis.y *= leftGamepadJoystickVerticalSensitivity;

                        mouseAxis.x *= rightGamepadJoystickHorizontalSensitivity;
                        mouseAxis.y *= rightGamepadJoystickVerticalSensitivity;
                    } else {
                        movementAxis *= leftGamepadJoystickSensitivity;

                        mouseAxis *= rightGamepadJoystickSensitivity;
                    }


                    if (allowKeyboardAndGamepad && usingKeyBoard) {
                        temporalRawMovementAxis = rawMovementAxis;
                    }

                    if (!useNewInputSystem) {
                        rawMovementAxis.x = InputAxisWrapper.getOldInputAxisRawValue (horizontalXPlayerIDString);
                        rawMovementAxis.y = InputAxisWrapper.getOldInputAxisRawValue (vertircalYPlayerIDString);
                    }

                    if (!lastButtonPressedOnGamepad && rawMovementAxis != vector2Zero) {
                        //						print ("pressing gamepad");

                        GKC_Utils.eventOnPressingGamepadInput (playerID);

                        lastButtonPressedOnGamepad = true;

                        lastKeyPressedOnKeyboard = false;

                        input.checkButtonPressedOnGamepadOrKeyboard (false);
                    }

                    if (allowKeyboardAndGamepad && usingKeyBoard && temporalRawMovementAxis != vector2Zero) {
                        rawMovementAxis = temporalRawMovementAxis;
                    }
                }
            }

            if (useMovementOrientation) {
                if (!isAnyMenuActive ()) {
                    if (horizontalMovementOrientation == -1) {
                        movementAxis.x *= horizontalMovementOrientation;
                        rawMovementAxis.x *= horizontalMovementOrientation;
                    }

                    if (verticalMovementOrientation == -1) {
                        movementAxis.y *= verticalMovementOrientation;
                        rawMovementAxis.y *= verticalMovementOrientation;
                    }
                }
            }

            if (useCameraOrientation) {
                if (!isAnyMenuActive ()) {
                    if (horizontalCameraOrientation == -1) {
                        mouseAxis.x *= horizontalCameraOrientation;
                    }

                    if (verticalCameraOrientation == -1) {
                        mouseAxis.y *= verticalCameraOrientation;
                    }
                }
            }
        }

        if (ignoreOverrideInput) {
            return;
        }

        if (overrideInputValuesActive) {

            auxMovementAxis = movementAxis;
            auxRawMovementAxis = rawMovementAxis;

            movementAxis = overrideInputAxisValue;

            if (movementAxis.x > 0) {
                rawMovementAxis.x = 1;
            } else if (movementAxis.x < 0) {
                rawMovementAxis.x = -1;
            } else {
                rawMovementAxis.x = 0;
            }

            if (movementAxis.y > 0) {
                rawMovementAxis.y = 1;
            } else if (movementAxis.y < 0) {
                rawMovementAxis.y = -1;
            } else {
                rawMovementAxis.y = 0;
            }
        }

        if (manualControlActive) {
            auxMovementAxis = movementAxis;
            auxRawMovementAxis = rawMovementAxis;

            movementAxis = new Vector2 (manualMovementHorizontalInput, manualMovementVerticalInput);

            if (movementAxis.x > 0) {
                rawMovementAxis.x = 1;
            } else if (movementAxis.x < 0) {
                rawMovementAxis.x = -1;
            } else {
                rawMovementAxis.x = 0;
            }

            if (movementAxis.y > 0) {
                rawMovementAxis.y = 1;
            } else if (movementAxis.y < 0) {
                rawMovementAxis.y = -1;
            } else {
                rawMovementAxis.y = 0;
            }

            mouseAxis.x = manualMouseHorizontalInput;
            mouseAxis.y = manualMouseVerticalInput;
        }

        if (axisInputPaused || ignoreInputOnGameWindowFocusResult || editingInput) {
            movementAxis = vector2Zero;

            mouseAxis = vector2Zero;

            rawMovementAxis = vector2Zero;
        } else {
            if (movementAxisInputPaused) {
                movementAxis = vector2Zero;

                rawMovementAxis = vector2Zero;
            }

            if (cameraAxisInputPaused) {
                mouseAxis = vector2Zero;
            }
        }
    }

    Vector2 updateSmoothNewAxis (Vector2 rawInput, ref Vector2 currentValue, float sensitivity = 12f,
        float gravity = 15f, float deadZone = 0.1f, bool applyCurve = false)
    {
        if (rawInput.magnitude < deadZone) {
            rawInput = Vector2.zero;
        }

        Vector2 target = rawInput;

        float rate = (target == Vector2.zero) ? gravity : sensitivity;

        currentValue = Vector2.MoveTowards (currentValue, target, rate * Time.unscaledDeltaTime);

        currentValue = Vector2.ClampMagnitude (currentValue, 1f);

        if (applyCurve) {
            currentValue = new Vector2 (
                Mathf.Sign (currentValue.x) * currentValue.x * currentValue.x,
                Mathf.Sign (currentValue.y) * currentValue.y * currentValue.y
            );
        }

        return currentValue;
    }

    public bool isUseNewInputSystem ()
    {
        return useNewInputSystem;
    }

    public bool isAnyMenuActive ()
    {
        if (gameManagerPaused || playerControllerManager.isPlayerMenuActive () || playerControllerManager.isUsingSubMenu ()) {
            return true;
        }

        return false;
    }

    public Vector2 getAuxMovementAxis ()
    {
        return auxMovementAxis;
    }

    public Vector2 getAuxRawMovementAxis ()
    {
        return auxRawMovementAxis;
    }

    //get if the touch controls are enabled, so any other component can check it
    public void changeControlsType (bool state)
    {
        enableOrDisableTouchControls (state);

        enableOrDisableScreenActionParent (!state);
    }

    public void enableOrDisableTouchControls (bool state)
    {
        setTouchPanelActiveState (state);

        enableOrDisableTouchCameraControl (state);

        enableOrDisableTouchMovementJoystickForButtons (state);
    }

    public void enableOrDisableTouchMovementControl (bool state)
    {
        if (touchMovementControl.gameObject.activeSelf != state) {
            touchMovementControl.gameObject.SetActive (state);
        }
    }

    public void enableOrDisableTouchCameraControl (bool state)
    {
        if (touchCameraControl.gameObject.activeSelf != state) {
            touchCameraControl.gameObject.SetActive (state);
        }
    }

    public void setTouchMovementControl (touchJoystick newTouchJoystick)
    {
        touchMovementControl = newTouchJoystick;
    }

    public void setTouchCameraControl (touchJoystick newTouchJoystick)
    {
        touchCameraControl = newTouchJoystick;
    }

    public touchJoystick getTouchMovementControl ()
    {
        return touchMovementControl;
    }

    public touchJoystick getTouchCameraControl ()
    {
        return touchCameraControl;
    }

    public void enableOrDisableTouchMovementJoystickForButtons (bool state)
    {
        if (usingTouchMovementJoystick) {
            enableOrDisableTouchMovementControl (state);

            if (horizontalTouchMovementButtons.activeSelf) {
                horizontalTouchMovementButtons.SetActive (false);
            }

            if (verticalTouchMovementButtons.activeSelf) {
                verticalTouchMovementButtons.SetActive (false);
            }
        } else {

            enableOrDisableTouchMovementControl (false);

            bool isTouchButtonsCurrentlyVisible = input.isTouchButtonsCurrentlyVisible ();

            if (!isTouchButtonsCurrentlyVisible) {
                state = false;
            }

            if (useHorizontaTouchMovementButtons) {
                if (horizontalTouchMovementButtons.activeSelf != state) {
                    horizontalTouchMovementButtons.SetActive (state);
                }
            } else {
                if (horizontalTouchMovementButtons.activeSelf) {
                    horizontalTouchMovementButtons.SetActive (false);
                }
            }

            if (useVerticalTouchMovementButtons) {
                if (verticalTouchMovementButtons.activeSelf != state) {
                    verticalTouchMovementButtons.SetActive (state);
                }
            } else {
                if (verticalTouchMovementButtons.activeSelf) {
                    verticalTouchMovementButtons.SetActive (false);
                }
            }
        }
    }

    public void setJoystickColorVisibleState (bool state)
    {
        touchMovementControl.setJoystickColorVisibleState (state);
        touchCameraControl.setJoystickColorVisibleState (state);
    }

    public void setOriginalTouchMovementInputState ()
    {
        setUsingTouchMovementJoystickState (originalUsingTouchMovementJoystickValue);

        enableOrDisableTouchMovementJoystickForButtons (true);
    }

    public void setUsingTouchMovementJoystickState (bool state)
    {
        usingTouchMovementJoystick = state;
    }

    public Vector2 getPlayerRawMovementAxis ()
    {
        if (!inputEnabled) {
            return vector2Zero;
        }

        if (inputPaused) {
            return vector2Zero;
        }

        if (ignoreAllAxisInput) {
            return vector2Zero;
        }

        if (playerIsDead) {
            return vector2Zero;
        }

        return rawMovementAxis;
    }

    public Vector2 getPlayerRawMovementAxisWithoutCheckingEnabled ()
    {
        if (inputPaused) {
            return vector2Zero;
        }

        if (ignoreAllAxisInput) {
            return vector2Zero;
        }

        if (playerIsDead) {
            return vector2Zero;
        }

        return rawMovementAxis;
    }

    //get the current values of the axis keys or the mouse in the input manager
    public Vector2 getPlayerMovementAxis ()
    {
        if (!inputEnabled) {
            return vector2Zero;
        }

        if (inputPaused) {
            return vector2Zero;
        }

        if (ignoreAllAxisInput) {
            return vector2Zero;
        }

        if (playerIsDead) {
            return vector2Zero;
        }

        return movementAxis;
    }

    public Vector2 getPlayerMovementAxisWithoutCheckingEnabled ()
    {
        if (inputPaused) {
            return vector2Zero;
        }

        if (playerIsDead) {
            return vector2Zero;
        }

        return movementAxis;
    }

    public Vector2 getPlayerMouseAxis ()
    {
        if (!inputEnabled) {
            return vector2Zero;
        }

        if (inputPaused) {
            return vector2Zero;
        }

        if (ignoreAllAxisInput) {
            return vector2Zero;
        }

        if (playerIsDead && !ignorePlayerIsDeadOnCameraAxis) {
            return vector2Zero;
        }

        return mouseAxis;
    }

    public Vector2 getRealMovementAxisInput ()
    {
#if REWIRED
		if (_useRewired)
			return new Vector2 (_rewiredPlayers[playerID - 1].GetAxis (horizontalString), _rewiredPlayers[playerID - 1].GetAxis (verticalString));
#endif
        if (useNewInputSystem) {
            return input.getKeyboardAxisInputValue ();
        } else {
            return new Vector2 (InputAxisWrapper.getOldInputAxisValue (horizontalString), InputAxisWrapper.getOldInputAxisValue (verticalString));
        }
    }

    public Vector2 getRealMouseAxisInput ()
    {
#if REWIRED
		if (_useRewired)
			return new Vector2 (_rewiredPlayers[playerID - 1].GetAxis (mouseXString), _rewiredPlayers[playerID - 1].GetAxis (mouseYString));
#endif
        if (useNewInputSystem) {
            return input.getMouseAxisInputValue ();
        } else {
            return new Vector2 (InputAxisWrapper.getOldInputAxisValue (mouseXString), InputAxisWrapper.getOldInputAxisValue (mouseYString));
        }
    }

    public Vector2 getRealMouseRawAxisInput ()
    {
#if REWIRED
		if (_useRewired)
			return new Vector2 (_rewiredPlayers[playerID - 1].GetAxisRaw (mouseXString), _rewiredPlayers[playerID - 1].GetAxisRaw (mouseYString));
#endif
        if (useNewInputSystem) {
            return input.getKeyboardRawAxisInputValue ();
        } else {
            return new Vector2 (InputAxisWrapper.getOldInputAxisRawValue (mouseXString), InputAxisWrapper.getOldInputAxisRawValue (mouseYString));
        }
    }

    public Vector2 getRealMouseGamepadAxisInput ()
    {
        if (useNewInputSystem) {
            return input.getNewInputRightJoysticksAxisInputValue (playerID);
        } else {
            return new Vector2 (InputAxisWrapper.getOldInputAxisValue (mouseXPlayerIDString), InputAxisWrapper.getOldInputAxisValue (mouseYPlayerIDString));
        }
    }

    public Vector2 getRealMovementGamepadAxisInput ()
    {
        if (useNewInputSystem) {
            return input.getNewInputLeftJoysticksAxisInputValue (playerID);
        } else {
            return new Vector2 (InputAxisWrapper.getOldInputAxisValue (horizontalXPlayerIDString), InputAxisWrapper.getOldInputAxisValue (vertircalYPlayerIDString));
        }
    }

    public Vector2 getRealMovementAxisInputAnyType ()
    {
        Vector2 currentValues = Vector2.zero;

        if (usingTouchControls && !allowGamepadInTouchDevice) {
            currentValues = touchMovementControl.GetAxis ();
        } else {
#if REWIRED
			if (!_useRewired && usingGamepad) {
#else
            if (usingGamepad) {
#endif
                if (allowKeyboardAndGamepad) {
                    currentValues = getRealMovementGamepadAxisInput () + getRealMovementAxisInput ();
                } else {
                    currentValues = getRealMovementGamepadAxisInput ();
                }

                if (usingTouchControls && allowGamepadInTouchDevice) {
                    currentValues += touchMovementControl.GetAxis ();
                }
            } else {
                if (usingTouchControls) {
                    currentValues = touchMovementControl.GetAxis ();
                } else {
                    currentValues = getRealMovementAxisInput ();
                }
            }
        }

        return currentValues;
    }

#if ENABLE_INPUT_SYSTEM
    public bool checkNewGamepadButton (GamepadButton buttonToCheck, buttonType type)
    {
        return input.checkNewGamepadButton (playerID, buttonToCheck, type);
    }
#endif

    public bool checkJoystickButton (string joystickString, buttonType type)
    {
        return input.checkJoystickButton (joystickString, type);
    }

    public void sendInputSignalExternally (int multiAxesIndex, int axesIndex)
    {
        //manager.sendInputSignalExternally( multiAxesIndex,  axesIndex,playerNonLocalAvatarID);
    }

    public void getInputSignalExternally (int multiAxesIndex, int axesIndex)
    {
        if (!inputEnabled) {
            return;
        }

        if (inputPaused) {
            return;
        }

        if (ignoreAllActionsInput) {
            return;
        }

        if (playerIsDead) {
            return;
        }

        //		inputManager.buttonType type = (inputManager.buttonType)buttonTypeIndex;

        //		if (input.checkPlayerInputButtonFromMultiAxesList (multiAxesIndex, axesIndex, type, playerID, false, useOnlyKeyboard)) {
        multiAxesList [multiAxesIndex].axes [axesIndex].buttonEvent.Invoke ();
        //		}
    }

    public void sendAxisInputSignalExternally ()
    {
        //manager.sendAxisInputSignalExternally( movementAxis,  rawMovementAxis, mouseAxis,playerNonLocalAvatarID);
    }

    public void getAxisInputSignalExternally (Vector2 newMovementAxis, Vector2 newRawMovementAxis, Vector2 newMouseAxis)
    {
        movementAxis = newMovementAxis;

        rawMovementAxis = newRawMovementAxis;

        mouseAxis = newMouseAxis;
    }

    public bool checkPlayerInputButtonFromMultiAxesList (int multiAxesIndex, int axesIndex, inputManager.buttonType type, bool canBeUsedOnPausedGame)
    {
        if (!inputEnabled) {
            return false;
        }

        if (inputPaused) {
            return false;
        }

        if (ignoreAllActionsInput) {
            return false;
        }

        if (playerIsDead) {
            return false;
        }

        return input.checkPlayerInputButtonFromMultiAxesList (multiAxesIndex, axesIndex, type, playerID, canBeUsedOnPausedGame, useOnlyKeyboard, ignoreKeyboardInputActive);
    }

    public bool checkPlayerInputButtonWithoutEvents (int multiAxesStringIndex, int axesStringIndex, inputManager.buttonType buttonType)
    {
        if (!inputEnabled) {
            return false;
        }

        if (inputPaused) {
            return false;
        }

        if (ignoreAllActionsInput) {
            return false;
        }

        if (playerIsDead) {
            return false;
        }

        return input.checkPlayerInputButtonFromMultiAxesList (multiAxesStringIndex, axesStringIndex, buttonType, playerID, false, useOnlyKeyboard, ignoreKeyboardInputActive);
    }

    public bool checkIfInputPressedByName (string inputName, inputManager.buttonType buttonType)
    {
        int axesStringIndex = -1;

        for (int i = 0; i < currentMultiAxesCount; i++) {

            currentMultiAxes = multiAxesList [i];

            axesStringIndex = currentMultiAxes.axes.FindIndex (s => s.actionName.Equals (inputName));

            if (axesStringIndex != -1) {
                return input.checkPlayerInputButtonFromMultiAxesList (
                    currentMultiAxes.multiAxesStringIndex,
                    currentMultiAxes.axes [axesStringIndex].axesStringIndex,
                    buttonType, playerID, false, useOnlyKeyboard, ignoreKeyboardInputActive);
            }
        }

        return false;
    }

    public void setGamepadVibrationByPlayerID (float low, float high, float duration)
    {
        bool ignoreGamepadResult = false;

        if (useOnlyPhoneVibrationOnMobile && touchPlatform) {
            ignoreGamepadResult = true;
        }

        if (usingGamepad && !ignoreGamepadResult) {
            input.setGamepadVibrationByPlayerID (playerID, low, high, duration);
        } else {
            if (usingTouchControls) {
                if (mainSimpleGamepadVibrationHelperLocated) {
                    mainSimpleGamepadVibrationHelper.setMobileVibration (low, high, duration);
                }
            }
        }
    }

    public void setGamepadVibrationByPlayerID (float low, float high)
    {
        bool ignoreGamepadResult = false;

        if (useOnlyPhoneVibrationOnMobile && touchPlatform) {
            ignoreGamepadResult = true;
        }

        if (usingGamepad && !ignoreGamepadResult) {
            input.setGamepadVibrationByPlayerID (playerID, low, high);
        } else {
            if (usingTouchControls) {
                if (mainSimpleGamepadVibrationHelperLocated) {
                    mainSimpleGamepadVibrationHelper.setMobileVibration (low, high);
                }
            }
        }
    }

    public void stopGamepadVibrationByPlayerID ()
    {
        bool ignoreGamepadResult = false;

        if (useOnlyPhoneVibrationOnMobile && touchPlatform) {
            ignoreGamepadResult = true;
        }

        if (usingGamepad && !ignoreGamepadResult) {
            input.stopGamepadVibrationByPlayerID (playerID);
        } else {
            if (usingTouchControls) {
                if (mainSimpleGamepadVibrationHelperLocated) {
                    mainSimpleGamepadVibrationHelper.stopMobileVibration ();
                }
            }
        }
    }

    public void sendSignalToGamepadVibrationHelper (int vibrationID)
    {
        if (mainSimpleGamepadVibrationHelperLocated) {
            if (usingGamepad || usingTouchControls) {
                mainSimpleGamepadVibrationHelper.setGamepadVibrationByID (vibrationID);
            }
        }
    }

    public void sendSignalToGamepadVibrationHelper (int vibrationID, float intensity)
    {
        if (mainSimpleGamepadVibrationHelperLocated) {
            if (usingGamepad || usingTouchControls) {
                mainSimpleGamepadVibrationHelper.setGamepadVibrationByID (vibrationID, intensity);
            }
        }
    }

    public void updateUsingGamepadState (bool state)
    {
        usingGamepad = state;

        if (mainSimpleGamepadVibrationHelperLocated) {
            mainSimpleGamepadVibrationHelper.setUsingGamepadState (state);

            mainSimpleGamepadVibrationHelper.setTouchPlatformState (InputAxisWrapper.checkTouchPlatform ());
        }
    }

    public simpleGamepadVibrationHelper getMainSimpleGamepadVibrationHelper ()
    {
        return mainSimpleGamepadVibrationHelper;
    }

    public void setVibrationMultiplierOnMainSimpleGamepadVibrationHelper (float newValue)
    {
        if (mainSimpleGamepadVibrationHelperLocated) {
            mainSimpleGamepadVibrationHelper.setVibrationMultiplierValue (newValue);
        }
    }

    public void setVibrationMultiplier (float newValue)
    {
        input.setVibrationMultiplier (newValue);
    }

    public void setGamePausedStateOnGamepadByID (bool state)
    {
        input.setGamePausedStateOnGamepadByID (state, playerID);

        if (usingTouchControls) {
            if (mainSimpleGamepadVibrationHelperLocated) {
                mainSimpleGamepadVibrationHelper.setGameIsPausedState (state);
            }
        }
    }

    bool UINavigationCurrentlyActive;

    public void setUINavigationCurrentlyActive (bool state)
    {
        UINavigationCurrentlyActive = state;

        if (mainVirtualTouchSystemLocated) {
            mainVirtualTouchSystem.setUINavigationCurrentlyActive (state);
        }
    }

    public void moveCursorToCornerNextVisibleTime ()
    {
        if (mainVirtualTouchSystemLocated) {
            mainVirtualTouchSystem.moveCursorToCornerNextVisibleTime ();
        }
    }

    public void setForceResetCursorPositionActiveState (bool state)
    {
        if (mainVirtualTouchSystemLocated) {
            mainVirtualTouchSystem.setForceResetCursorPositionActiveState (state);
        }
    }

    public bool isVirtualCursorCurrentlyActive ()
    {
        if (mainVirtualTouchSystemLocated) {
            return mainVirtualTouchSystem.isVirtualCursorCurrentlyActive ();
        }

        return false;
    }

    public bool isUINavigationCurrentlyActive ()
    {
        return UINavigationCurrentlyActive;
    }

    public int checkNumberInput (int numberAmount)
    {
        return input.checkNumberInput (numberAmount);
    }

    public void setPlayerInputEnabledState (bool state)
    {
        inputEnabled = state;
    }

    public void setIgnorePlayerActionsInputState (bool state)
    {
        ignorePlayerActionsInput = state;
    }

    Coroutine enablePlayerInputCoroutine;

    public void setPlayerInputEnabledStateWithDelay (bool state)
    {
        if (state) {
            if (enablePlayerInputCoroutine != null) {
                StopCoroutine (enablePlayerInputCoroutine);
            }
            enablePlayerInputCoroutine = StartCoroutine (setPlayerInputEnabledStateWithDelayCoroutine ());

        } else {
            inputEnabled = false;
        }
    }

    IEnumerator setPlayerInputEnabledStateWithDelayCoroutine ()
    {
        WaitForSeconds delay = new WaitForSeconds (0.3f);

        yield return delay;

        inputEnabled = true;
    }

    public void setInputCurrentlyActiveState (bool state)
    {
        inputCurrentlyActive = state;
    }

    public void toggleInputCurrentlyActiveState ()
    {
        setInputCurrentlyActiveState (!inputCurrentlyActive);
    }

    public void setPlayerID (int newID)
    {
        playerID = newID;
    }

    public int getPlayerID ()
    {
        return playerID;
    }

    public void switchPlayerID (int newID)
    {
        playerID = newID;
        playerIDString = playerID.ToString ();

        horizontalXPlayerIDString = horizontalXString + playerIDString;
        vertircalYPlayerIDString = verticalYString + playerIDString;

        mouseXPlayerIDString = mouseXString + playerIDString;
        mouseYPlayerIDString = mouseYString + playerIDString;
    }

    public bool isPlayerNonLocalAvatar ()
    {
        return playerIsNonLocalAvatar;
    }

    public void setPlayerIsNonLocalAvatarState (bool state)
    {
        playerIsNonLocalAvatar = state;
    }

    public int getPlayerNonLocalAvatarID ()
    {
        return playerNonLocalAvatarID;
    }

    public void setPlayerNonLocalAvatarID (int newID)
    {
        playerNonLocalAvatarID = newID;
    }

    public void setPlayerInputActionState (bool playerInputActionState, string multiAxesInputName, string axesInputName)
    {
        int multiAxesListCount = multiAxesList.Count;

        for (int i = 0; i < multiAxesListCount; i++) {
            multiAxes temporalMultiAxes = multiAxesList [i];

            if (temporalMultiAxes.axesName.Equals (multiAxesInputName)) {

                int axesCount = temporalMultiAxes.axes.Count;

                for (int j = 0; j < axesCount; j++) {
                    if (temporalMultiAxes.axes [j].Name.Equals (axesInputName)) {
                        temporalMultiAxes.axes [j].actionEnabled = playerInputActionState;
                    }
                }
            }
        }
    }

    public bool getPlayerInputMultiAxesState (string multiAxesInputName)
    {
        int multiAxesListCount = multiAxesList.Count;

        for (int i = 0; i < multiAxesListCount; i++) {
            multiAxes temporalMultiAxes = multiAxesList [i];

            if (temporalMultiAxes.axesName.Equals (multiAxesInputName)) {
                return temporalMultiAxes.currentlyActive;
            }
        }

        return false;
    }

    public void updateCurrentActiveInputList ()
    {
        if (playerControllerManager.checkIfPlayerDeadFromHealthComponent ()) {
            return;
        }

        int multiAxesListCount = multiAxesList.Count;

        if (inputCurrentlyActiveInfoList.Count == 0) {
            for (int i = 0; i < multiAxesListCount; i++) {
                inputCurrentlyActiveInfo newInputCurrentlyActiveInfo = new inputCurrentlyActiveInfo ();

                multiAxes temporalMultiAxes = multiAxesList [i];

                newInputCurrentlyActiveInfo.inputName = temporalMultiAxes.axesName;

                newInputCurrentlyActiveInfo.currentState = temporalMultiAxes.currentlyActive;

                inputCurrentlyActiveInfoList.Add (newInputCurrentlyActiveInfo);
            }
        }

        for (int i = 0; i < multiAxesListCount; i++) {
            inputCurrentlyActiveInfoList [i].currentState = multiAxesList [i].currentlyActive;
        }

        //		for (int i = 0; i < multiAxesList.Count; i++) {
        //			print (inputCurrentlyActiveInfoList [i].inputName + " " + inputCurrentlyActiveInfoList [i].currentState);
        //		}
        //
        //		print ("UPDATE INPUT---------------------------------------------------------------------");
    }

    public void setCurrentActiveInputList ()
    {
        //		print ("SET ACTIVE INPUT------------------------------------------------------------");

        int multiAxesListCount = multiAxesList.Count;

        int inputCurrentlyActiveInfoListCount = inputCurrentlyActiveInfoList.Count;

        for (int i = 0; i < multiAxesListCount; i++) {
            if (i < inputCurrentlyActiveInfoListCount) {
                multiAxesList [i].currentlyActive = inputCurrentlyActiveInfoList [i].currentState;
            }

            //			print (multiAxesList [i].axesName + " " + multiAxesList [i].currentlyActive);
        }
    }

    public void setPlayerInputMultiAxesState (bool playerInputMultiAxesState, string multiAxesInputName)
    {
        int multiAxesListCount = multiAxesList.Count;

        for (int i = 0; i < multiAxesListCount; i++) {
            multiAxes temporalMultiAxes = multiAxesList [i];

            if (temporalMultiAxes.axesName.Equals (multiAxesInputName)) {
                temporalMultiAxes.currentlyActive = playerInputMultiAxesState;

                return;
            }
        }
    }

    public bool setPlayerInputMultiAxesStateAndGetPreviousState (bool playerInputMultiAxesState, string multiAxesInputName)
    {
        bool previousState = false;

        int multiAxesListCount = multiAxesList.Count;

        for (int i = 0; i < multiAxesListCount; i++) {
            multiAxes temporalMultiAxes = multiAxesList [i];

            if (temporalMultiAxes.axesName.Equals (multiAxesInputName)) {
                previousState = temporalMultiAxes.currentlyActive;

                temporalMultiAxes.currentlyActive = playerInputMultiAxesState;

                //				print (playerInputMultiAxesState);

                return previousState;
            }
        }

        return false;
    }

    public void setIgnoreCheckInputActiveStateOnList (bool ignoreCheckInputActiveValue, List<string> multiAxesInputNameList)
    {
        int multiAxesInputNameListCount = multiAxesInputNameList.Count;

        for (int i = 0; i < multiAxesInputNameListCount; i++) {
            int multiAxesIndex = multiAxesList.FindIndex (s => s.axesName.Equals (multiAxesInputNameList [i]));

            if (multiAxesIndex > -1) {
                multiAxesList [multiAxesIndex].ignoreCheckInputActive = ignoreCheckInputActiveValue;
            }
        }
    }

    public void setIgnoreEnablePlayerInputMultiAxesActiveState (bool state)
    {
        ignoreEnablePlayerInputMultiAxesActive = state;
    }

    bool ignoreEnablePlayerInputMultiAxesActive;

    public void enablePlayerInputMultiAxes (string multiAxesInputName)
    {
        if (ignoreEnablePlayerInputMultiAxesActive) {
            return;
        }

        int multiAxesListCount = multiAxesList.Count;

        for (int i = 0; i < multiAxesListCount; i++) {
            multiAxes temporalMultiAxes = multiAxesList [i];

            if (temporalMultiAxes.axesName.Equals (multiAxesInputName)) {
                temporalMultiAxes.currentlyActive = true;

                return;
            }
        }
    }

    public void disablePlayerInputMultiAxes (string multiAxesInputName)
    {
        int multiAxesListCount = multiAxesList.Count;

        for (int i = 0; i < multiAxesListCount; i++) {
            multiAxes temporalMultiAxes = multiAxesList [i];

            if (temporalMultiAxes.axesName.Equals (multiAxesInputName)) {
                temporalMultiAxes.currentlyActive = false;

                return;
            }
        }
    }

    public void overrideInputValues (Vector2 newInputValues, bool overrideState)
    {
        overrideInputAxisValue = newInputValues;

        overrideInputValuesActive = overrideState;
    }

    public bool isOverrideInputValuesActive ()
    {
        return overrideInputValuesActive;
    }

    public void setManuaInputValues (Vector2 newMovementInputValues, Vector2 newMouseInputValues)
    {
        manualControlActive = true;

        manualMovementHorizontalInput = newMovementInputValues.x;

        manualMovementVerticalInput = newMovementInputValues.y;

        manualMouseHorizontalInput = newMouseInputValues.x;

        manualMouseVerticalInput = newMouseInputValues.y;
    }

    public void disableManuaInput ()
    {
        manualControlActive = false;
    }

    public void setIgnoreOverrideInputState (bool state)
    {
        ignoreOverrideInput = state;
    }

    public inputManager getInputManager ()
    {
        return input;
    }

    public bool isUsingTouchControls ()
    {
        return input.isUsingTouchControls ();
    }

    public bool isAllowGamepadInTouchDevice ()
    {
        return allowGamepadInTouchDevice;
    }

    public bool isUsingTouchControlsOnGameManager ()
    {
        return input.isUsingTouchControlsOnGameManager ();
    }

    public void updateIsUsingTouchControlsState ()
    {
        usingTouchControls = input.isUsingTouchControls ();
    }

    public string getButtonKey (string buttonName)
    {
        return input.getButtonKey (buttonName);
    }

    public Sprite getButtonSprite (string actionName)
    {
        return input.getButtonSprite (actionName, playerID);
    }

    public void setAvoidInputActiveState (bool state)
    {
        avoidInputActive = state;
    }

    public GameObject getTouchPanel ()
    {
        return touchPanel;
    }

    public void setTouchPanelActiveState (bool state)
    {
        if (touchPanel.activeSelf != state) {
            touchPanel.SetActive (state);
        }
    }

    public GameObject getEditInputPanelPrefab ()
    {
        return editInputPanelPrefab;
    }

    public GameObject getEditInputMenu ()
    {
        return editInputMenu;
    }

    public GameObject getCurrentInputPanelListText ()
    {
        return currentInputPanelListText;
    }

    //Start of functions to enable or disable the reverse direction of movement and camera input
    public void setHorizontalMovementOrientationValue (int newValue)
    {
        horizontalMovementOrientation = newValue;
    }

    public void setVerticalMovementOrientationValue (int newValue)
    {
        verticalMovementOrientation = newValue;
    }

    public void setHorizontalCameraOrientationValue (int newValue)
    {
        horizontalCameraOrientation = newValue;
    }

    public void setVerticalCameratOrientationValue (int newValue)
    {
        verticalCameraOrientation = newValue;
    }

    public void setHorizontalMovementOrientationValue (bool state)
    {
        setHorizontalMovementOrientationValue (state ? 1 : -1);
    }

    public void setVerticalMovementOrientationValue (bool state)
    {
        setVerticalMovementOrientationValue (state ? 1 : -1);
    }

    public void setHorizontalCameraOrientationValue (bool state)
    {
        setHorizontalCameraOrientationValue (state ? 1 : -1);
    }

    public void setVerticalCameratOrientationValue (bool state)
    {
        setVerticalCameratOrientationValue (state ? 1 : -1);
    }
    //End of functions to enable or disable the reverse direction of movement and camera input


    public void setPressingTouchHorizontalRightInputState (bool state)
    {
        pressingTouchHorizontalRightInput = state;
    }

    public void setPressingTouchHorizontalLeftInputState (bool state)
    {
        pressingTouchHorizontalLeftInput = state;
    }

    public void setPressingTouchVerticalUpInputState (bool state)
    {
        pressingTouchVerticalUpInput = state;
    }

    public void setPressingTouchVerticalDownInputState (bool state)
    {
        pressingTouchVerticalDownInput = state;
    }

    public void pauseOrResumeInput (bool state)
    {
        inputPaused = state;
    }

    public void setAxisInputPausedState (bool state)
    {
        axisInputPaused = state;
    }

    public void setIgnoreAllActionsInputState (bool state)
    {
        ignoreAllActionsInput = state;
    }

    public void setMovementAxisInputPausedState (bool state)
    {
        movementAxisInputPaused = state;
    }

    public void setCameraAxisInputPausedState (bool state)
    {
        cameraAxisInputPaused = state;
    }

    public void toggleAxisInputPausedState ()
    {
        setAxisInputPausedState (!axisInputPaused);
    }

    public void setIgnoreKeyboardInputActiveState (bool state)
    {
        ignoreKeyboardInputActive = state;
    }

    public void setIgnorePlayerIsDeadOnCameraAxisState (bool state)
    {
        ignorePlayerIsDeadOnCameraAxis = state;
    }

    public void setInputPausedForExternalComponentsState (bool state)
    {
        inputPausedForExternalComponents = state;
    }

    public bool isInputPausedForExternalComponents ()
    {
        return inputPausedForExternalComponents;
    }

    public List<touchPanelsInfo> getTouchPanelsList ()
    {
        return mainPlayerInputPanelSystem.getTouchPanelsList ();
    }

    public void storeTouchButtonList ()
    {
        bool isPlaying = Application.isPlaying;

        touchButtonList.Clear ();

        setTouchPanelActiveState (true);

        touchButtonsInfoList.Clear ();

        setTouchPanelActiveState (true);

        List<touchPanelsInfo> touchPanelsInfoList = getTouchPanelsList ();

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

        if (!isPlaying) {
            print ("total touch Button List count is " + touchButtonList.Count);
        }

        if (!usingTouchControls) {
            setTouchPanelActiveState (false);
        }

        if (!isPlaying) {
            updateComponent ();
        }
    }

    public void renameInputAction (string inputActionCategoryNameToSearch, string inputActionNameToRename, string newInputActionNameToUse)
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
            multiAxes currentMultiAxes = multiAxesList [multiAxesIndex];

            int axesIndex = currentMultiAxes.axes.FindIndex (s => s.Name.Equals (inputActionNameToRename));

            if (axesIndex > -1) {
                Axes currentAxes = currentMultiAxes.axes [axesIndex];

                currentAxes.Name = newInputActionNameToUse;
                currentAxes.actionName = newInputActionNameToUse;


                int multiAxesStringIndex = currentMultiAxes.multiAxesStringIndex;

                List<inputManager.Axes> currentTemporalAxes = input.multiAxesList [multiAxesStringIndex].axes;

                for (int i = 0; i < currentMultiAxes.axes.Count; i++) {

                    currentMultiAxes.axes [i].axesStringList = new string [currentTemporalAxes.Count];

                    for (int j = 0; j < currentTemporalAxes.Count; j++) {
                        string actionName = currentTemporalAxes [j].Name;
                        currentMultiAxes.axes [i].axesStringList [j] = actionName;
                    }
                }

                updateComponent ();
            }
        }
    }

    public void renameInputActionCategory (string inputActionCategoryNameToRename, string newInputActionCategoryName)
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

            for (int i = 0; i < multiAxesList.Count; i++) {

                multiAxes temporalMultiAxes = multiAxesList [i];

                temporalMultiAxes.multiAxesStringList = new string [input.multiAxesList.Count];

                for (int j = 0; j < input.multiAxesList.Count; j++) {
                    string axesName = input.multiAxesList [j].axesName;

                    temporalMultiAxes.multiAxesStringList [j] = axesName;
                }
            }

            updateComponent ();
        }
    }

    public List<touchButtonsInfo> getTouchButtonsInfoList ()
    {
        if (touchButtonsInfoList.Count == 0) {
            storeTouchButtonList ();
        }

        return touchButtonsInfoList;
    }

    public List<touchButtonListener> getTouchButtonList ()
    {
        if (touchButtonList.Count == 0) {
            storeTouchButtonList ();
        }

        return touchButtonList;
    }

    public void checkStoreTouchButtonListIfListEmpty ()
    {
        if (touchButtonList.Count == 0 || touchButtonsInfoList.Count == 0) {
            storeTouchButtonList ();
        }
    }

    public void enableTouchPanelByName (string panelName)
    {
        enableOrDisableTouchPanelByName (panelName, true);
    }

    public void disableTouchPanelByName (string panelName)
    {
        enableOrDisableTouchPanelByName (panelName, false);
    }

    public void enableOrDisableTouchPanelByName (string panelName, bool state)
    {
        mainPlayerInputPanelSystem.enableOrDisableTouchPanelByName (panelName, state);
    }

    public void enableTouchPanelSchemeByName (string panelName)
    {
        mainPlayerInputPanelSystem.enableTouchPanelSchemeByName (panelName);
    }

    public void enablePreviousTouchPanelScheme ()
    {
        mainPlayerInputPanelSystem.enablePreviousTouchPanelScheme ();
    }

    public void enableOrDisableActionScreen (string actionScreenName)
    {
        mainPlayerInputPanelSystem.enableOrDisableActionScreen (actionScreenName);
    }

    public void enableOrDisableActionScreen (string actionScreenName, bool state)
    {
        mainPlayerInputPanelSystem.enableOrDisableActionScreen (actionScreenName, state);
    }

    public void enableActionScreen (string actionScreenName)
    {
        mainPlayerInputPanelSystem.enableActionScreen (actionScreenName);
    }

    public void disableActionScreen (string actionScreenName)
    {
        mainPlayerInputPanelSystem.disableActionScreen (actionScreenName);
    }

    public void enablePreviousActionScreen ()
    {
        mainPlayerInputPanelSystem.enablePreviousActionScreen ();
    }

    public void enableSingleActionScreen (string actionScreenName)
    {
        mainPlayerInputPanelSystem.enableSingleActionScreen (actionScreenName);
    }

    public void enableOrDisableScreenActionParent (bool state)
    {
        mainPlayerInputPanelSystem.enableOrDisableScreenActionParent (state);
    }

    public void checkPanelsActiveOnGamepadOrKeyboard (bool checkKeyboard)
    {
        mainPlayerInputPanelSystem.checkPanelsActiveOnGamepadOrKeyboard (checkKeyboard);

        checkButtonPressedOnGamepadOrKeyboard (checkKeyboard);
    }

    public void setScreenActionPanelsEnabledState (bool state)
    {
        mainPlayerInputPanelSystem.setScreenActionPanelsEnabledState (state);
    }

    public void resetButtonStateOnTouchButtonList ()
    {
        input.resetButtonStateOnTouchButtonList ();
    }

    public bool usingPlayerMenu;

    public void setUsingPlayerMenuState (bool state)
    {
        usingPlayerMenu = state;
    }

    public bool isUsingPlayerMenu ()
    {
        return usingPlayerMenu;
    }

    //get the values from the touch joystick sensitivity in the touch options menu when the player adjust the joysticks sensitivity
    public void setRightSensitivityValue (float sensitivityValue)
    {
        //set the values in the input manager
        rightTouchSensitivity = sensitivityValue;
    }

    public void setLeftSensitivityValue (float sensitivityValue)
    {
        //set the values in the input manager
        leftTouchSensitivity = sensitivityValue;
    }

    //get the mouse sensitivity value when the player adjusts it in the edit input menu
    public void setMouseSensitivityValue (float sensitivityValue)
    {
        //set the values in the input manager
        mouseSensitivity = sensitivityValue;
    }

    public void setLeftGamepadJoystickSensitivityValue (float sensitivityValue)
    {
        leftGamepadJoystickSensitivity = sensitivityValue;
    }

    public void setRightGamepadJoystickSensitivityValue (float sensitivityValue)
    {
        rightGamepadJoystickSensitivity = sensitivityValue;
    }


    public void setLeftGamepadJoystickHorizontalSensitivityValue (float sensitivityValue)
    {
        leftGamepadJoystickHorizontalSensitivity = sensitivityValue;
    }

    public void setLeftGamepadJoystickVerticalSensitivityValue (float sensitivityValue)
    {
        leftGamepadJoystickVerticalSensitivity = sensitivityValue;
    }

    public void setRightGamepadJoystickHorizontalSensitivityValue (float sensitivityValue)
    {
        rightGamepadJoystickHorizontalSensitivity = sensitivityValue;
    }

    public void setRightGamepadJoystickVerticalSensitivityValue (float sensitivityValue)
    {
        rightGamepadJoystickVerticalSensitivity = sensitivityValue;
    }

    public bool isKeyboardButtonPressed (string multiAxesName, string axesName)
    {
        return input.isKeyboardButtonPressed (playerID, multiAxesName, axesName);
    }

    public bool isUsingGamepad ()
    {
        return input.isUsingGamepad ();
    }

    public bool playerHasGamepadAssigned ()
    {
        return input.IsValidGamepad (playerID);
    }

    public bool isPlayerDead ()
    {
        return playerIsDead;
    }

    public bool isInputPaused ()
    {
        return inputPaused;
    }

    public bool isGameManagerPaused ()
    {
        return input.isGameManagerPaused ();
    }

    public bool areTouchJoysticksPressed ()
    {
        return touchMovementControl.touchJoystickIsPressed () || touchCameraControl.touchJoystickIsPressed ();
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

    public void openInputMenu ()
    {
        if (!inputManagerLocated) {
            findInputManager ();
        }

        if (inputManagerLocated) {
            input.openInputMenu ();
        }
    }

    public void setEditInputMenuOpenedState (bool state)
    {
        if (!inputManagerLocated) {
            findInputManager ();
        }

        if (inputManagerLocated) {
            input.setEditInputMenuOpenedState (state);
        }
    }

    public void cancelEditingInput ()
    {
        if (!inputManagerLocated) {
            findInputManager ();
        }

        if (inputManagerLocated) {
            input.cancelEditingInput ();
        }
    }

    public void setEditingInputState (bool state)
    {
        editingInput = state;
    }

    public void checkIfSaveInputAfterRebind ()
    {
        if (!inputManagerLocated) {
            findInputManager ();
        }

        if (inputManagerLocated) {
            input.checkIfSaveInputAfterRebind ();
        }
    }

    public void loadDefaultInputSettings ()
    {
        if (inputManagerLocated) {
            input.loadDefaultInputSettingsOnRuntime ();
        }
    }

    public void setNextInputPanel ()
    {
        if (!inputManagerLocated) {
            findInputManager ();
        }

        if (inputManagerLocated) {
            input.setNextInputPanel ();
        }
    }

    public void setPreviousInputPanel ()
    {
        if (!inputManagerLocated) {
            findInputManager ();
        }

        if (inputManagerLocated) {
            input.setPreviousInputPanel ();
        }
    }

    public void startEditingInput (GameObject button)
    {
        if (!inputManagerLocated) {
            findInputManager ();
        }

        if (inputManagerLocated) {
            input.startEditingInput (button);
        }
    }

    public Vector2 getCurrentTouchPosition ()
    {
        if (mainVirtualTouchSystemLocated) {
            return mainVirtualTouchSystem.getCurrentTouchPosition ();
        }

        return Vector2.zero;
    }

    public TouchPressType GetTouchPressType ()
    {
        if (mainVirtualTouchSystemLocated) {
            return mainVirtualTouchSystem.GetTouchPressType ();
        }

        return TouchPressType.None;
    }

    public int getFingerID ()
    {
        if (mainVirtualTouchSystemLocated) {
            return mainVirtualTouchSystem.getFingerID ();
        }

        return -1;
    }


    //Input management through the custom inspector
    public void addNewAxes ()
    {
        findInputManager ();

        if (input != null) {
            multiAxes newMultiAxes = new multiAxes ();

            newMultiAxes.multiAxesStringList = new string [input.multiAxesList.Count];

            for (int i = 0; i < input.multiAxesList.Count; i++) {
                string axesName = input.multiAxesList [i].axesName;
                newMultiAxes.multiAxesStringList [i] = axesName;
            }

            multiAxesList.Add (newMultiAxes);

            updateComponent ();
        }
    }

    public void addNewAction (int multiAxesIndex)
    {
        findInputManager ();

        if (input != null) {
            multiAxes currentMultiAxesList = multiAxesList [multiAxesIndex];

            int multiAxesStringIndex = currentMultiAxesList.multiAxesStringIndex;

            List<inputManager.Axes> currentTemporalAxes = input.multiAxesList [multiAxesStringIndex].axes;

            Axes newAxes = new Axes ();
            newAxes.axesStringList = new string [currentTemporalAxes.Count];

            for (int i = 0; i < currentTemporalAxes.Count; i++) {
                string actionName = currentTemporalAxes [i].Name;
                newAxes.axesStringList [i] = actionName;
            }

            newAxes.multiAxesStringIndex = multiAxesIndex;

            currentMultiAxesList.axes.Add (newAxes);

            currentMultiAxesList.axesCount = currentMultiAxesList.axes.Count;

            updateComponent ();
        }
    }

    public void setAxesActionName (int multiAxesIndex, int axesIndex)
    {
        if (multiAxesList.Count > multiAxesIndex && multiAxesList [multiAxesIndex].axes.Count > axesIndex) {
            Axes currentAxes = multiAxesList [multiAxesIndex].axes [axesIndex];

            int currentAxesStringIndex = currentAxes.axesStringIndex;

            print (currentAxesStringIndex);

            if (currentAxesStringIndex > -1) {
                currentAxes.actionName = currentAxes.axesStringList [currentAxesStringIndex];

                updateComponent ();
            } else {
                print ("WARNING: Action no properly configured, make sure the Multi Axes exist on the Input Manager and inside of it," +
                "there is an action configured for this current element");
            }
        }
    }

    public void updateMultiAxesList ()
    {
        findInputManager ();

        if (input != null) {
            for (int i = 0; i < multiAxesList.Count; i++) {

                multiAxes currentMultiAxesList = multiAxesList [i];

                List<inputManager.multiAxes> currentTemporalMultiAxes = input.multiAxesList;

                currentMultiAxesList.multiAxesStringList = new string [currentTemporalMultiAxes.Count];

                int currentMultiAxesIndex = -1;

                string currentMultiAxesName = currentMultiAxesList.axesName;

                for (int j = 0; j < currentTemporalMultiAxes.Count; j++) {
                    string axesName = currentTemporalMultiAxes [j].axesName;
                    currentMultiAxesList.multiAxesStringList [j] = axesName;

                    if (currentMultiAxesName.Equals (axesName)) {
                        currentMultiAxesIndex = j;
                    }
                }

                if (currentMultiAxesIndex > -1) {
                    if (currentMultiAxesIndex != currentMultiAxesList.multiAxesStringIndex) {
                        currentMultiAxesList.multiAxesStringIndex = currentMultiAxesIndex;

                        print (currentMultiAxesName.ToUpper () + " updated with index " + currentMultiAxesIndex);
                    } else {
                        print (currentMultiAxesName.ToUpper () + " keeps the same index " + currentMultiAxesIndex);
                    }
                } else {
                    print ("WARNING: Multi axes called " + currentMultiAxesName.ToUpper () + " hasn't been found, make sure to configure a multi axes " +
                    "with that name in the main input manager");

                    currentMultiAxesList.multiAxesStringIndex = -1;
                }

                currentMultiAxesList.axesCount = currentMultiAxesList.axes.Count;
            }

            updateComponent ();
        }
    }

    public void updateAxesList (int multiAxesListIndex)
    {
        findInputManager ();

        if (input != null) {
            multiAxes currentMultiAxesList = multiAxesList [multiAxesListIndex];

            for (int i = 0; i < currentMultiAxesList.axes.Count; i++) {

                Axes currentAxes = currentMultiAxesList.axes [i];

                int multiAxesStringIndex = currentMultiAxesList.multiAxesStringIndex;

                List<inputManager.Axes> currentTemporalAxes = input.multiAxesList [multiAxesStringIndex].axes;

                currentAxes.axesStringList = new string [currentTemporalAxes.Count];

                int currentAxesIndex = -1;

                string currentAxesName = currentAxes.Name;

                for (int j = 0; j < currentTemporalAxes.Count; j++) {
                    string actionName = currentTemporalAxes [j].Name;
                    currentAxes.axesStringList [j] = actionName;

                    if (currentAxesName.Equals (actionName)) {
                        currentAxesIndex = j;
                    }
                }

                if (currentAxesIndex > -1) {
                    if (currentAxesIndex != currentAxes.axesStringIndex) {
                        currentAxes.axesStringIndex = currentAxesIndex;

                        print (currentAxes.actionName.ToUpper () + " updated with index " + currentAxesIndex);
                    } else {
                        print (currentAxes.actionName.ToUpper () + " keeps the same index " + currentAxesIndex);
                    }
                } else {
                    print ("WARNING: Action called " + currentAxesName.ToUpper () + " hasn't been found, make sure to configure an action " +
                    "with that name in the main input manager");

                    currentAxes.axesStringIndex = -1;
                }

                currentMultiAxesList.axesCount = currentMultiAxesList.axes.Count;
            }

            updateComponent ();
        }
    }

    public void setAllAxesList (int multiAxesListIndex)
    {
        if (input != null) {
            multiAxes currentMultiAxesList = multiAxesList [multiAxesListIndex];

            currentMultiAxesList.axes.Clear ();

            int multiAxesStringIndex = currentMultiAxesList.multiAxesStringIndex;

            List<inputManager.Axes> currentTemporalAxes = input.multiAxesList [multiAxesStringIndex].axes;

            for (int i = 0; i < currentTemporalAxes.Count; i++) {
                Axes newAxes = new Axes ();
                newAxes.axesStringList = new string [currentTemporalAxes.Count];

                for (int j = 0; j < currentTemporalAxes.Count; j++) {
                    string actionName = currentTemporalAxes [j].Name;
                    newAxes.axesStringList [j] = actionName;
                }

                newAxes.multiAxesStringIndex = multiAxesListIndex;

                newAxes.axesStringIndex = i;

                newAxes.Name = currentTemporalAxes [i].Name;

                newAxes.actionName = newAxes.Name;

                currentMultiAxesList.axes.Add (newAxes);

                currentMultiAxesList.axesCount = currentMultiAxesList.axes.Count;
            }

            updateComponent ();
        }
    }

    void findInputManager ()
    {
        inputManagerLocated = input != null;

        if (!inputManagerLocated) {
            input = inputManager.Instance;

            inputManagerLocated = input != null;
        }

        if (!inputManagerLocated) {
            input = GKC_Utils.findObjectByComponent<inputManager> ();

            inputManagerLocated = input != null;

            if (inputManagerLocated) {
                input.getComponentInstanceOnApplicationPlaying ();
            }
        }
    }

    public void findInputManagerAndUpdateComponent ()
    {
        inputManagerLocated = input != null;

        if (!inputManagerLocated) {
            findInputManager ();

            updateComponent ();
        }
    }

    public void updateCurrentInputValues ()
    {
        print ("Player Input Manager settings updated");

        for (int i = 0; i < multiAxesList.Count; i++) {
            multiAxesList [i].axesCount = multiAxesList [i].axes.Count;
        }

        updateComponent ();
    }

    public void setMultiAxesEnabledState (bool state)
    {
        for (int i = 0; i < multiAxesList.Count; i++) {
            multiAxesList [i].currentlyActive = state;
        }

        updateComponent ();
    }

    public void setAllActionsEnabledState (int multiAxesListIndex, bool state)
    {
        for (int j = 0; j < multiAxesList [multiAxesListIndex].axes.Count; j++) {
            multiAxesList [multiAxesListIndex].axes [j].actionEnabled = state;
        }

        updateComponent ();
    }

    void updateComponent ()
    {
        GKC_Utils.updateComponent (this);

        GKC_Utils.updateDirtyScene ("Update Player Input Manager Values", gameObject);
    }

    [System.Serializable]
    public class multiAxes
    {
        public string axesName;
        public List<Axes> axes = new List<Axes> ();

        public int axesCount;

        public bool currentlyActive = true;
        public bool canUseInputOnPlayerPaused;

        public bool ignoreCheckInputActive;

        public int multiAxesStringIndex;
        public string [] multiAxesStringList;

        public multiAxes (multiAxes newState)
        {
            axesName = newState.axesName;
        }

        public multiAxes ()
        {
        }
    }

    [System.Serializable]
    public class Axes
    {
        public string actionName = "New Action";
        public string Name;
        public bool actionEnabled = true;

        public bool canBeUsedOnPausedGame;

        public inputManager.buttonType buttonPressType;

        public UnityEvent buttonEvent;

        public int axesStringIndex;
        public string [] axesStringList;

        public int multiAxesStringIndex;
    }

    [System.Serializable]
    public class inputCurrentlyActiveInfo
    {
        public string inputName;
        public bool currentState;
    }
}
