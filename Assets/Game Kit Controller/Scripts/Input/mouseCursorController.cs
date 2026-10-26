using UnityEngine;
using System.Collections;
using System.Runtime.InteropServices;
using System;

public class mouseCursorController : MonoBehaviour
{
    [Header ("Main Settings")]
    [Space]

    public bool useMouseCursorEnabled = true;

    [Space]

    public bool enableGamepadCursorOnPause = true;
    public float timeToResetCursorAfterDisable = 1;

    public bool activateCursorOnStart;

    public bool useFixedTimeEnabled;

    [Space]

    public bool useScreenClamp = true;

    public float cursorMargin = 15;

    [Space]
    [Header ("Cursor Speed Settings")]
    [Space]

    public float cursorSpeed = 7;

    public float cursorLerpSpeed = 10;

    [Space]

    public bool useSmoothCursorSpeed;

    public float maxSmoothSpeed = 200;
    public float acceleration = 2000f;

    public float smoothing = 15f;
    public float deadzone = 0.15f;

    [Space]

    public float mouseCursorSmoothSpeedOnHomeMenu = 0.05f;

    [Space]
    [Header ("Gamepad Input Settings")]
    [Space]

    public bool useRightJoystickAxis = true;
    public bool useBothJoysticks;

    public float rightJoystickSpeedMultiplier = 1;

    [Space]
    [Header ("Old Input Settings")]
    [Space]

    public string horizontalXString = "Horizontal X";
    public string verticalYString = "Vertical Y";

    public string mouseXString = "Mouse X";
    public string mouseYString = "Mouse Y";

    public int buttonIndex = 0;
    public int joystickNumber = 1;

    [Space]
    [Header ("Debug")]
    [Space]

    public bool showDebugPrint;

    public bool ignoreMouseCursorUpdateActive;

    public bool useTryCatchOnUpdateCursorActive;

    [Space]

    public float currentMouseSpeed;
    public Vector2 currentResolution;

    [Space]
    [Space]

    public Vector2 cursorPosition;
    float newX, newY;

    Point cursorPos = new Point ();

    [Space]
    [Space]

    public bool usingActualMouse;
    public bool screenFocused;

    [Space]
    [Space]

    public bool cursorCurrentlyActive;
    public bool cursorPreviouslyActive;

    [Space]
    [Space]

    public bool virtualCursorCurrentlyActive;

    public bool UINavigationCurrentlyActive;

    [Space]
    [Space]

    public Vector2 mouseLimits;

    public Vector2 realMouseAxisInput;

    public Vector2 axisInput;

    public bool ignoreSetSelectedGameObjectOnUIByGamepad;

    [Space]
    [Space]

    public bool playerInputLocated;

    public bool inputLocated;

    public bool useNewInputSystem;

    [Space]
    [Header ("Components")]
    [Space]

    public playerInputManager playerInput;
    public inputManager input;


    //#if UNITY_EDITOR || UNITY_STANDALONE_WIN || UNITY_STANDALONE || UNITY_ANDROID || UNITY_IOS || UNITY_STANDALONE_OSX
    [DllImport ("user32.dll")]
    static extern bool SetCursorPos (int X, int Y);

    [DllImport ("user32.dll")]
    static extern bool GetCursorPos (out Point pos);
    //#endif

    float lastTimeCursorEnabled;
    float lastTimeCursorDisabled;

    bool mouseLimitsAssigned;

    float lastTimeRealMouseUsed;
    float inputDifference;

    string joystickString;

    bool canUseGamepad;

    string mouseXPlayerIDString;
    string mouseYPlayerIDString;
    string horizontalXPlayerIDString;
    string vertircalYPlayerIDString;

    float customCursorSpeed;
    bool customCursorSpeedActive;

    Vector2 velocity;

    private static mouseCursorController _mouseCursorControllerInstance;

    public static mouseCursorController Instance { get { return _mouseCursorControllerInstance; } }

    bool instanceInitialized;

    float currentUnscaledTime;

    int virtualCursorCurrentlyActiveCounter = 0;

    int UINavigationCurrentlyActiveCounter = 0;

    bool moveCursorToCornerNextVisibleTimeActive;


    public void getComponentInstance ()
    {
        if (instanceInitialized) {
            return;
        }

        if (_mouseCursorControllerInstance != null && _mouseCursorControllerInstance != this) {
            Destroy (this.gameObject);

            return;
        }

        _mouseCursorControllerInstance = this;

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
    }

    void Start ()
    {
        updateJoystickString ();

        if (!playerInputLocated) {
            playerInputLocated = playerInput != null;
        }

        if (!inputLocated) {
            inputLocated = input != null;
        }

        mouseXPlayerIDString = mouseXString + joystickNumber;
        mouseYPlayerIDString = mouseYString + joystickNumber;

        horizontalXPlayerIDString = horizontalXString + joystickNumber;
        vertircalYPlayerIDString = verticalYString + joystickNumber;

        if (activateCursorOnStart) {
            setCursorVisibleStateExternally (true);
        }

        useNewInputSystem = InputAxisWrapper.isUseNewInputSystem ();
    }

    public void setCursorVisibleStateExternally (bool state)
    {
        menuPause.setCursorVisibleState (state);

        menuPause.setCursorLockState (!state);

        showOrHideMouseController (playerInput, state, joystickNumber, false);

        resetCursorPosition ();
    }

    void updateJoystickString ()
    {
        joystickString = "joystick " + joystickNumber + " button " + buttonIndex;
    }

    void Update ()
    {
        updateCursorController ();
    }

    void updateCursorController ()
    {
        if (virtualCursorCurrentlyActive) {
            return;
        }

        if (UINavigationCurrentlyActive) {
            return;
        }

        if (cursorCurrentlyActive) {

            canUseGamepad = false;

            if (inputLocated) {
                if (input.isUsingGamepad () && (!input.isUsingTouchControls () || input.isAllowGamepadInTouchDevice ())) {
                    canUseGamepad = true;
                }
            } else {
                canUseGamepad = true;
            }

            if (canUseGamepad) {
                if (inputLocated) {
                    if (!useNewInputSystem) {
                        if (input.checkJoystickButton (joystickString, inputManager.buttonType.getKeyDown)) {
                            MouseOperations.MouseEvent (MouseOperations.MouseEventFlags.LeftDown);
                        }

                        if (input.checkJoystickButton (joystickString, inputManager.buttonType.getKeyUp)) {
                            MouseOperations.MouseEvent (MouseOperations.MouseEventFlags.LeftUp);
                        }
                    }
                } else {
                    if (useNewInputSystem) {

                    } else {
                        if (InputAxisWrapper.getOldKeyDown (joystickString)) {
                            MouseOperations.MouseEvent (MouseOperations.MouseEventFlags.LeftDown);
                        }

                        if (InputAxisWrapper.getOldKeyUp (joystickString)) {
                            MouseOperations.MouseEvent (MouseOperations.MouseEventFlags.LeftUp);
                        }
                    }
                }

                if (useNewInputSystem) {
                    InputAxisWrapper.updateHasFocusOnGameWindowValue ();

                    screenFocused = InputAxisWrapper.hasFocusOnGameWindow;
                } else {
                    //Check if the actual mouse is being used
                    screenFocused = Application.isFocused;
                }

                if (playerInputLocated) {
                    realMouseAxisInput = playerInput.getRealMouseAxisInput ();
                } else {
                    if (useNewInputSystem) {
                        realMouseAxisInput = InputAxisWrapper.GetLookAxis (mouseCursorSmoothSpeedOnHomeMenu);
                    } else {
                        realMouseAxisInput = new Vector2 (
                            InputAxisWrapper.getOldInputAxisValue (mouseXString),
                            InputAxisWrapper.getOldInputAxisValue (mouseYString));
                    }
                }

                inputDifference = Mathf.Abs (realMouseAxisInput.sqrMagnitude);

                if (useFixedTimeEnabled) {
                    currentUnscaledTime = Time.fixedUnscaledTime;
                } else {
                    currentUnscaledTime = Time.unscaledTime;
                }

                if (inputDifference > 0.01f) {
                    lastTimeRealMouseUsed = currentUnscaledTime;

                    axisInput = realMouseAxisInput;
                }

                bool isUsingGamepad = GamepadRegistry.anyGamepadFound ();

                if (currentUnscaledTime > lastTimeRealMouseUsed + 1 && screenFocused) {
                    if (usingActualMouse && isUsingGamepad) {
                        cursorPosition.x = cursorPos.X;
                        cursorPosition.y = cursorPos.Y;

                        newX = cursorPosition.x;
                        newY = cursorPosition.y;

                        usingActualMouse = false;
                    }
                } else {
                    usingActualMouse = true;
                }

                //Assign the input values according to if the gamepad or the actual mouse is being used
                if (!usingActualMouse) {
                    if (playerInputLocated) {
                        if (playerInput.isInputPaused () || playerInput.isPlayerDead () || playerInput.isOverrideInputValuesActive ()) {
                            if (useBothJoysticks) {
                                axisInput = (rightJoystickSpeedMultiplier * playerInput.getRealMouseGamepadAxisInput ())
                                    + playerInput.getRealMovementGamepadAxisInput ();
                            } else {
                                if (useRightJoystickAxis) {
                                    axisInput = playerInput.getRealMouseGamepadAxisInput ();
                                } else {
                                    axisInput = playerInput.getRealMovementGamepadAxisInput ();
                                }
                            }
                        } else {
                            if (useBothJoysticks) {
                                axisInput = (rightJoystickSpeedMultiplier * playerInput.getPlayerMouseAxis ()) +
                                    playerInput.getPlayerMovementAxis ();
                            } else {
                                if (useRightJoystickAxis) {
                                    axisInput = (rightJoystickSpeedMultiplier * playerInput.getPlayerMouseAxis ());
                                } else {
                                    axisInput = playerInput.getPlayerMovementAxis ();
                                }
                            }
                        }
                    } else {
                        if (useNewInputSystem) {
                            if (useBothJoysticks) {
                                axisInput = (rightJoystickSpeedMultiplier * GamepadRegistry.getNewInputRightJoysticksAxisInputValue (joystickNumber))
                                    + GamepadRegistry.getNewInputLeftJoysticksAxisInputValue (joystickNumber);
                            } else {
                                if (useRightJoystickAxis) {
                                    axisInput = GamepadRegistry.getNewInputRightJoysticksAxisInputValue (joystickNumber);
                                } else {
                                    axisInput = GamepadRegistry.getNewInputLeftJoysticksAxisInputValue (joystickNumber);
                                }
                            }
                        } else {
                            if (useBothJoysticks) {
                                axisInput = new Vector2 (InputAxisWrapper.getOldInputAxisValue (mouseXPlayerIDString),
                                    InputAxisWrapper.getOldInputAxisValue (mouseYPlayerIDString)) +

                                    new Vector2 (InputAxisWrapper.getOldInputAxisValue (horizontalXPlayerIDString),
                                    InputAxisWrapper.getOldInputAxisValue (vertircalYPlayerIDString));
                            } else {
                                if (useRightJoystickAxis) {
                                    axisInput = new Vector2 (InputAxisWrapper.getOldInputAxisValue (mouseXPlayerIDString),
                                        InputAxisWrapper.getOldInputAxisValue (mouseYPlayerIDString));
                                } else {
                                    axisInput = new Vector2 (InputAxisWrapper.getOldInputAxisValue (horizontalXPlayerIDString),
                                        InputAxisWrapper.getOldInputAxisValue (vertircalYPlayerIDString));
                                }
                            }
                        }
                    }
                }

                if (!ignoreMouseCursorUpdateActive) {
                    updateGetCursorPos ();
                }

                if (Time.deltaTime > 0) {
                    currentMouseSpeed = cursorSpeed;
                } else {
                    currentMouseSpeed = cursorSpeed;

                    if (!enableGamepadCursorOnPause) {
                        return;
                    }
                }

                if (customCursorSpeedActive) {
                    currentMouseSpeed = customCursorSpeed;
                }

                if (!usingActualMouse) {
                    if (useNewInputSystem) {
                        if (useSmoothCursorSpeed) {
                            if (axisInput.magnitude < deadzone) {
                                axisInput = Vector2.zero;
                            }

                            if (axisInput != Vector2.zero) {
                                velocity += axisInput * acceleration * currentUnscaledTime;
                                velocity = Vector2.ClampMagnitude (velocity, maxSmoothSpeed);
                            } else {
                                velocity = Vector2.Lerp (velocity, Vector2.zero, currentUnscaledTime * smoothing);
                            }

                            cursorPosition += velocity;
                        } else {
                            cursorPosition += new Vector2 (axisInput.x, axisInput.y) * currentMouseSpeed;
                        }
                    } else {
                        if (axisInput.x > 0) {
                            cursorPosition.x += currentMouseSpeed;
                        } else if (axisInput.x < 0) {
                            cursorPosition.x -= currentMouseSpeed;
                        }

                        if (axisInput.y > 0) {
                            cursorPosition.y -= currentMouseSpeed;
                        } else if (axisInput.y < 0) {
                            cursorPosition.y += currentMouseSpeed;
                        }
                    }
                }

                if (useScreenClamp && isUsingGamepad) {
                    cursorPosition = clampMouseToScreen (cursorPosition);
                }

                if (Time.deltaTime > 0 && !usingActualMouse) {
                    newX = Mathf.Lerp (newX, cursorPosition.x, currentUnscaledTime * cursorLerpSpeed);
                    newY = Mathf.Lerp (newY, cursorPosition.y, currentUnscaledTime * cursorLerpSpeed);
                } else {
                    newX = cursorPosition.x;
                    newY = cursorPosition.y;
                }

                if (!usingActualMouse) {
                    if (!ignoreMouseCursorUpdateActive) {
                        updateSetCursorPos ((int)newX, (int)newY);
                    }
                }
            }
        }
    }

    Vector2 clampMouseToScreen (Vector2 position)
    {
        position.x = Mathf.Clamp (position.x, cursorMargin, currentResolution.x - cursorMargin);
        position.y = Mathf.Clamp (position.y, cursorMargin, currentResolution.y - cursorMargin);

        return position;
    }

    public void setVirtualCursorCurrentlyActive (bool state)
    {
        if (state) {
            virtualCursorCurrentlyActiveCounter++;
        } else {
            virtualCursorCurrentlyActiveCounter--;

            if (virtualCursorCurrentlyActiveCounter < 0) {
                virtualCursorCurrentlyActiveCounter = 0;
            }
        }

        virtualCursorCurrentlyActive = virtualCursorCurrentlyActiveCounter != 0;
    }

    public void setUINavigationCurrentlyActive (bool state)
    {
        if (state) {
            UINavigationCurrentlyActiveCounter++;
        } else {
            UINavigationCurrentlyActiveCounter--;

            if (UINavigationCurrentlyActiveCounter < 0) {
                UINavigationCurrentlyActiveCounter = 0;
            }
        }

        UINavigationCurrentlyActive = UINavigationCurrentlyActiveCounter != 0;
    }

    public bool isUINavigationCurrentlyActive ()
    {
        return UINavigationCurrentlyActive;
    }

    public bool isCursorCurrentlyActive ()
    {
        return cursorCurrentlyActive;
    }

    public void showOrHideMouseController (playerInputManager newInput, bool state, int newJoystickNumber, bool pausingGame)
    {
        if (newInput == null || !newInput.useOnlyKeyboard) {
            playerInput = newInput;

            joystickNumber = newJoystickNumber;

            updateJoystickString ();

            showOrHideMouseController (state, pausingGame);
        }
    }

    public void setIgnoreSetSelectedGameObjectOnUIByGamepadState (bool state)
    {
        ignoreSetSelectedGameObjectOnUIByGamepad = state;
    }

    public void showOrHideMouseController (bool state, bool pausingGame)
    {
        if (input != null && !input.isUsingGamepad ()) {
            return;
        }

        if (!useMouseCursorEnabled) {
            return;
        }

        if (pausingGame) {
            cursorPreviouslyActive = false;
        } else {
            if (!state) {
                if (cursorCurrentlyActive) {
                    cursorPreviouslyActive = true;
                }
            }
        }

        if (state) {
            if (Time.time > lastTimeCursorDisabled + timeToResetCursorAfterDisable) {
                cursorPreviouslyActive = false;
            }
        }

        if (state) {
            if (pausingGame || !cursorPreviouslyActive || forceResetCursorPositionActive) {
                resetCursorPosition ();
            }
        }

        forceResetCursorPositionActive = false;

        cursorCurrentlyActive = state;

        if (showDebugPrint) {
            print ("Setting cursor active state as " + cursorCurrentlyActive);
        }

        if (!pausingGame && state) {
            if (!cursorCurrentlyActive) {
                cursorPreviouslyActive = false;
            }
        }

        if (!ignoreSetSelectedGameObjectOnUIByGamepad) {
            if (state) {
                StartCoroutine (FocusButton ());
            }
        }

        ignoreSetSelectedGameObjectOnUIByGamepad = false;

        if (state) {
            lastTimeCursorEnabled = Time.time;
        } else {
            lastTimeCursorDisabled = Time.time;
        }
    }

    IEnumerator FocusButton ()
    {
        yield return null;

        GKC_Utils.setSelectedGameObjectOnUI (true, false, null, showDebugPrint);
    }

    public void resetCursorPosition ()
    {
        if (!ignoreMouseCursorUpdateActive) {
            updateGetCursorPos ();
        }

        cursorPosition.x = cursorPos.X;
        cursorPosition.y = cursorPos.Y;

        if (!mouseLimitsAssigned) {
            currentResolution = new Vector2 (Screen.width, Screen.height);

            mouseLimitsAssigned = true;
        }

        if (showDebugPrint) {
            print ("resetCursorPosition " + moveCursorToCornerNextVisibleTimeActive);
        }

        if (moveCursorToCornerNextVisibleTimeActive) {
            cursorPosition = Vector2.zero;

            moveCursorToCornerNextVisibleTimeActive = false;
        } else {
            cursorPosition = currentResolution / 2;
        }

        newX = cursorPosition.x;
        newY = cursorPosition.y;

        if (!ignoreMouseCursorUpdateActive) {
            updateSetCursorPos ((int)cursorPosition.x, (int)cursorPosition.y);
            updateGetCursorPos ();
        }
    }


    public void moveCursorToCornerNextVisibleTime ()
    {
        moveCursorToCornerNextVisibleTimeActive = true;
    }

    bool forceResetCursorPositionActive;

    public void setForceResetCursorPositionActiveState (bool state)
    {
        forceResetCursorPositionActive = state;
    }

    void updateSetCursorPos (int cursorPositionX, int cursorPositionY)
    {
        if (useTryCatchOnUpdateCursorActive) {
            try {
                if (useNewInputSystem) {
                    Vector2 newPosition = new Vector2 (cursorPositionX, cursorPositionY);

                    InputAxisWrapper.warpCursorPosition (newPosition);
                } else {
                    //#if UNITY_EDITOR || UNITY_STANDALONE_WIN || UNITY_STANDALONE || UNITY_ANDROID || UNITY_IOS || UNITY_STANDALONE_OSX
                    SetCursorPos (cursorPositionX, cursorPositionY);
                    //#endif
                }
            }
            catch (Exception e) {
                print (e + " updateSetCursorPos error");

                throw;
            }
        } else {
            if (useNewInputSystem) {
                Vector2 newPosition = new Vector2 (cursorPositionX, cursorPositionY);

                InputAxisWrapper.warpCursorPosition (newPosition);
            } else {
                //#if UNITY_EDITOR || UNITY_STANDALONE_WIN || UNITY_STANDALONE || UNITY_ANDROID || UNITY_IOS || UNITY_STANDALONE_OSX
                SetCursorPos (cursorPositionX, cursorPositionY);
                //#endif
            }
        }
    }

    void updateGetCursorPos ()
    {
        if (useTryCatchOnUpdateCursorActive) {
            try {
                if (useNewInputSystem) {
                    Vector2 newPosition = InputAxisWrapper.getMousePosition ();

                    cursorPos.X = (int)newPosition.x;
                    cursorPos.Y = (int)newPosition.y;
                } else {
                    //#if UNITY_EDITOR || UNITY_STANDALONE_WIN || UNITY_STANDALONE || UNITY_ANDROID || UNITY_IOS || UNITY_STANDALONE_OSX
                    GetCursorPos (out cursorPos);
                    //#endif
                }
            }
            catch (Exception e) {
                print (e + " updateGetCursorPos error");

                throw;
            }
        } else {
            if (useNewInputSystem) {
                Vector2 newPosition = InputAxisWrapper.getMousePosition ();

                cursorPos.X = (int)newPosition.x;
                cursorPos.Y = (int)newPosition.y;
            } else {
                //#if UNITY_EDITOR || UNITY_STANDALONE_WIN || UNITY_STANDALONE || UNITY_ANDROID || UNITY_IOS || UNITY_STANDALONE_OSX
                GetCursorPos (out cursorPos);
                //#endif
            }
        }
    }

    public Point getCursorPos ()
    {
        return cursorPos;
    }

    public void setUseRightOrLeftJoysticsAxis (bool state)
    {
        useRightJoystickAxis = state;
    }

    public int getCurrentJoystickNumber ()
    {
        return joystickNumber;
    }

    public void setMouseCursorControllerSpeedOnGameValue (float newValue)
    {
        customCursorSpeed = newValue;

        customCursorSpeedActive = true;
    }

    public void setOriginalMouseCursorControllerSpeedOnGameValue ()
    {
        customCursorSpeedActive = false;
    }

    public void setCursorSpeedValue (float newValue)
    {
        cursorSpeed = newValue;
    }

    public void setCursorAccelerationValue (float newValue)
    {
        acceleration = newValue;
    }

    public void setUseSmoothCursorSpeedState (bool state)
    {
        useSmoothCursorSpeed = state;
    }

    [System.Serializable]
    public struct Point
    {
        public int X;
        public int Y;

        public Point (int x, int y)
        {
            this.X = x;
            this.Y = y;
        }
    }

    public Vector2 getMouseAxis ()
    {
        if (playerInputLocated) {
            if (cursorCurrentlyActive) {
                return playerInput.getRealMouseGamepadAxisInput ();
            } else {
                return playerInput.getRealMouseAxisInput ();
            }
        } else {
            return new Vector2 (InputAxisWrapper.getOldInputAxisValue (mouseXPlayerIDString),
                InputAxisWrapper.getOldInputAxisValue (mouseYPlayerIDString)) +

            new Vector2 (InputAxisWrapper.getOldInputAxisValue (mouseXString),
            InputAxisWrapper.getOldInputAxisValue (mouseYString));
        }
    }

    public Vector2 getMovementAxis ()
    {
        if (playerInputLocated) {
            if (cursorCurrentlyActive) {
                return playerInput.getRealMovementGamepadAxisInput ();
            } else {
                return playerInput.getRealMovementAxisInput ();
            }
        } else {
            return new Vector2 (InputAxisWrapper.getOldInputAxisValue (horizontalXPlayerIDString),
                InputAxisWrapper.getOldInputAxisValue (vertircalYPlayerIDString)) +

            new Vector2 (InputAxisWrapper.getOldInputAxisValue (horizontalXString),
            InputAxisWrapper.getOldInputAxisValue (verticalYString));
        }
    }
}
