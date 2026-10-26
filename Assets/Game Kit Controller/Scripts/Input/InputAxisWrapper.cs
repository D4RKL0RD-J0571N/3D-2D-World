using System.Reflection;
using UnityEngine;
using System;

using oldTouchPhase = UnityEngine.TouchPhase;
using oldTouch = UnityEngine.Touch;

using System.Collections.Generic;

using static inputManager;
using static System.Runtime.CompilerServices.RuntimeHelpers;






#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.LowLevel;

using newMouseButton = UnityEngine.InputSystem.LowLevel.MouseButton;

using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.EnhancedTouch;
using TouchPhase = UnityEngine.InputSystem.TouchPhase;
using enhancedTouch = UnityEngine.InputSystem.EnhancedTouch.Touch;
#endif

public static class InputAxisWrapper
{
#if ENABLE_INPUT_SYSTEM
    private static InputAction moveAction;

    private static InputAction lookAction;
#endif

    private static Vector2 smoothedMove = Vector2.zero;

    private static Vector2 smoothedLook = Vector2.zero;

    private static float sensitivity = 0.1f;
    private static Vector2 moveVelocity;

    private static Vector2 lookVelocity;

    private static bool useNewInputSystem;

#if ENABLE_INPUT_SYSTEM
    private static Keyboard mainKeyboard;

    private static bool mainKeyboardFound;

    private static Mouse mainMouse;

    private static bool mainMouseFound;

    private static Touchscreen mainTouchscreen;

    private static bool mainTouchscreenFound;

    private static Accelerometer mainAccelerometer;

    private static bool mainAccelerometerFound;
#endif

    private static bool isTouchPlatform;

    private static bool checkIfTouchPlatformInitialized;

    private static bool inputManagerLocated;

    private static inputManager mainInputManager;

    public enum TouchPressType
    {
        None,
        Began,
        Moved,
        Stationary,
        Ended,
        Canceled
    }


#if ENABLE_INPUT_SYSTEM
    public static void InitializeMove (InputAction move)
    {
        if (move == null) {
            Debug.LogError ("InputAxisWrapper: move action not found");
            return;
        }

        moveAction = move;
        moveAction.Enable ();
    }

    public static void InitializeLook (InputAction look)
    {
        if (look == null) {
            Debug.LogError ("InputAxisWrapper: look action not found");
            return;
        }

        lookAction = look;
        lookAction.Enable ();
    }
#endif

    public static void initializeInputSources ()
    {
#if ENABLE_INPUT_SYSTEM
        mainKeyboard = Keyboard.current;

        mainKeyboardFound = mainKeyboard != null;


        mainMouse = Mouse.current;

        mainMouseFound = mainMouse != null;


        mainTouchscreen = Touchscreen.current;

        mainTouchscreenFound = mainTouchscreen != null;


        mainAccelerometer = Accelerometer.current;

        mainAccelerometerFound = mainAccelerometer != null;

        mainInputManager = inputManager.Instance;

        inputManagerLocated = mainInputManager != null;

        EnhancedTouchSupport.Enable ();
#endif
    }

    public static void setUseNewInputSystemState (bool state)
    {
        useNewInputSystem = state;
    }


    //GET MOVE INPUT INFO
    public static float GetAxisHorizontal ()
    {
#if ENABLE_INPUT_SYSTEM
        if (moveAction == null) return 0f;

        return moveAction.ReadValue<Vector2> ().x;
#else
        return 0;
#endif
    }

    public static float GetAxisVertical ()
    {
#if ENABLE_INPUT_SYSTEM
        if (moveAction == null) return 0f;

        return moveAction.ReadValue<Vector2> ().y;

#else
        return 0;
#endif
    }

    public static Vector2 GetMoveAxis (float smoothSpeed)
    {
        Vector2 currentValue = Vector2.zero;

#if ENABLE_INPUT_SYSTEM
        currentValue = moveAction.ReadValue<Vector2> ();
#endif

        if (smoothSpeed > 0) {
            float dt = Time.unscaledDeltaTime;

            smoothedMove = Vector2.SmoothDamp (smoothedMove, currentValue, ref moveVelocity, smoothSpeed, Mathf.Infinity, dt);
        } else {
            smoothedMove = currentValue;
        }

        smoothedMove = Vector2.ClampMagnitude (smoothedMove, 1f);

        if (currentValue.x == 0) {
            if (Mathf.Abs (smoothedMove.x) < 0.001f) {
                smoothedMove.x = 0;
            }
        }

        if (currentValue.y == 0) {
            if (Mathf.Abs (smoothedMove.y) < 0.001f) {
                smoothedMove.y = 0;
            }
        }

        return smoothedMove;
    }

    public static void resetSmoothedGetMoveAxis ()
    {
        smoothedMove = Vector2.zero;
    }


    //GET MOUSE INPUT INFO
    public static Vector2 GetMoveRawAxis ()
    {
#if ENABLE_INPUT_SYSTEM
        return moveAction.ReadValue<Vector2> ();
#else
        return Vector2.zero;
#endif
    }

    public static Vector2 GetLookAxis (float smoothSpeed)
    {
        Vector2 currentValue = Vector2.zero;

#if ENABLE_INPUT_SYSTEM
        currentValue = lookAction.ReadValue<Vector2> ();
#endif

        if (smoothSpeed > 0) {
            float dt = Time.unscaledDeltaTime;

            currentValue *= sensitivity;

            smoothedLook = Vector2.SmoothDamp (smoothedLook, currentValue, ref lookVelocity, smoothSpeed, Mathf.Infinity, dt);
        } else {
            smoothedLook = currentValue;
        }

        smoothedLook = Vector2.ClampMagnitude (smoothedLook, 1f);

        if (currentValue.x == 0) {
            if (Mathf.Abs (smoothedLook.x) < 0.001f) {
                smoothedLook.x = 0;
            }
        }

        if (currentValue.y == 0) {
            if (Mathf.Abs (smoothedLook.y) < 0.001f) {
                smoothedLook.y = 0;
            }
        }

        return smoothedLook;
    }

    public static void resetSmoothedGetLookAxis ()
    {
        smoothedLook = Vector2.zero;
    }


    //GET OLD INPUT KEYBOARD AXIS

    public static float getOldInputAxisValue (string axisName)
    {
        return Input.GetAxis (axisName);
    }

    public static float getOldInputAxisRawValue (string axisName)
    {
        return Input.GetAxisRaw (axisName);
    }

    public static bool getOldKeyUp (string keyName)
    {
        return Input.GetKeyUp (keyName);
    }

    public static bool getOldKeyDown (string keyName)
    {
        return Input.GetKeyDown (keyName);
    }

    public static bool getOldKey (string keyName)
    {
        return Input.GetKey (keyName);
    }

    public static bool getOldKeyUp (KeyCode keycode)
    {
        return Input.GetKeyUp (keycode);
    }

    public static bool getOldKeyDown (KeyCode keycode)
    {
        return Input.GetKeyDown (keycode);
    }

    public static bool getOldKey (KeyCode keycode)
    {
        return Input.GetKey (keycode);
    }

    public static bool getOldMouseButtonDown (int mouseButtomNumber)
    {
        return Input.GetMouseButtonDown (mouseButtomNumber);
    }

    public static bool getOldMouseButtonUp (int mouseButtomNumber)
    {
        return Input.GetMouseButtonUp (mouseButtomNumber);
    }


    public static bool GetMouseButtonDown (int mouseButtomNumber)
    {
        if (useNewInputSystem) {
#if ENABLE_INPUT_SYSTEM
            if (mainMouseFound) {
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
            } else {
                return false;
            }
#else
            return false;
#endif
        } else {
            return Input.GetMouseButtonDown (mouseButtomNumber);
        }
    }

    public static bool GetMouseButtonUp (int mouseButtomNumber)
    {
        if (useNewInputSystem) {
#if ENABLE_INPUT_SYSTEM
            if (mainMouseFound) {
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
            } else {
                return false;
            }
#else
            return false;
#endif
        } else {
            return Input.GetMouseButtonUp (mouseButtomNumber);
        }
    }

    public static bool GetMouseButton (int mouseButtomNumber)
    {
        if (useNewInputSystem) {
#if ENABLE_INPUT_SYSTEM
            if (mainMouseFound) {
                switch (mouseButtomNumber) {
                    case 0:
                        return mainMouse.leftButton.isPressed;
                    case 1:
                        return mainMouse.rightButton.isPressed;
                    case 2:
                        return mainMouse.middleButton.isPressed;
                    default:
                        return false;
                }
            } else {
                return false;
            }
#else
            return false;
#endif
        } else {
            return Input.GetMouseButton (mouseButtomNumber);
        }
    }

    public static bool isUseNewInputSystem ()
    {
        return useNewInputSystem;
    }


    //TOUCH ELEMENTS

    //check the if the current platform is a touch device
    public static bool checkTouchPlatform ()
    {
        if (!checkIfTouchPlatformInitialized) {
            isTouchPlatform = (Application.platform == RuntimePlatform.Android || Application.platform == RuntimePlatform.IPhonePlayer);

            checkIfTouchPlatformInitialized = true;
        }

        return isTouchPlatform;
    }

    //it simulates touch control in the game with the mouse position, using left button as tap finger with press, hold and release actions
    public static oldTouch convertMouseIntoFinger ()
    {
        object mouseFinger = new oldTouch ();

        FieldInfo phase = mouseFinger.GetType ().GetField ("m_Phase", BindingFlags.NonPublic | BindingFlags.Instance);

        FieldInfo fingerId = mouseFinger.GetType ().GetField ("m_FingerId", BindingFlags.NonPublic | BindingFlags.Instance);

        FieldInfo position = mouseFinger.GetType ().GetField ("m_Position", BindingFlags.NonPublic | BindingFlags.Instance);

        if (GetMouseButtonDown (0)) {
            phase.SetValue (mouseFinger, oldTouchPhase.Began);
        } else if (GetMouseButtonUp (0)) {
            phase.SetValue (mouseFinger, oldTouchPhase.Ended);
        } else {
            phase.SetValue (mouseFinger, oldTouchPhase.Moved);
        }

        fingerId.SetValue (mouseFinger, 11);

        position.SetValue (mouseFinger, getMousePosition ());

        return (oldTouch)mouseFinger;
    }

    public static Vector2 getMousePosition ()
    {
        if (useNewInputSystem) {
#if ENABLE_INPUT_SYSTEM
            if (mainMouseFound) {
                return mainMouse.position.ReadValue ();
            } else {
                return Vector2.zero;
            }
#else
            return Vector2.zero;
#endif
        } else {
            return new Vector2 (Input.mousePosition.x, Input.mousePosition.y);
        }
    }

    public static void warpCursorPosition (Vector2 newPosition)
    {
        if (useNewInputSystem) {
#if ENABLE_INPUT_SYSTEM
            if (mainMouseFound) {
                mainMouse.WarpCursorPosition (newPosition);

                InputState.Change (mainMouse.position, newPosition);
            }
#endif
        }
    }

    //NEW TOUCH INPUT
    //public static TouchControl getTouchControl (int touchIndex)
    //{
    //    return mainTouchscreen.touches [touchIndex];

    //    //CAMBIAR POR ESTO
    //    //return UnityEngine.InputSystem.EnhancedTouch.Touch.activeTouches [touchIndex];
    //}

#if ENABLE_INPUT_SYSTEM
    public static enhancedTouch getEnhancedTouchhControl (int touchIndex)
    {
        return enhancedTouch.activeTouches [touchIndex];
    }

    public static bool isTouchPhaseBegan (TouchControl touchToCheck)
    {
        return (touchToCheck.phase.ReadValue () == TouchPhase.Began);
    }

    public static bool isTouchPhaseStationary (TouchControl touchToCheck)
    {
        return (touchToCheck.phase.ReadValue () == TouchPhase.Stationary);
    }

    public static bool isTouchPhaseMoved (TouchControl touchToCheck)
    {
        return (touchToCheck.phase.ReadValue () == TouchPhase.Moved);
    }

    public static bool isTouchPhaseEnded (TouchControl touchToCheck)
    {
        return (touchToCheck.phase.ReadValue () == TouchPhase.Ended);
    }

    public static bool isTouchPhaseBegan (enhancedTouch touchToCheck)
    {
        return (touchToCheck.phase == TouchPhase.Began);
    }

    public static bool isTouchPhaseStationary (enhancedTouch touchToCheck)
    {
        return (touchToCheck.phase == TouchPhase.Stationary);
    }

    public static bool isTouchPhaseMoved (enhancedTouch touchToCheck)
    {
        return (touchToCheck.phase == TouchPhase.Moved);
    }

    public static bool isTouchPhaseEnded (enhancedTouch touchToCheck)
    {
        return (touchToCheck.phase == TouchPhase.Ended);
    }
#endif

    public static int getEnhancedTouchhControlTouchId (int touchIndex)
    {
        if (getNewTouchCount () <= touchIndex) {
            return -1;
        }

#if ENABLE_INPUT_SYSTEM
        return enhancedTouch.activeTouches [touchIndex].touchId;
#else
        return -1;
#endif
    }

    public static int getNewTouchCount ()
    {
#if ENABLE_INPUT_SYSTEM
        return enhancedTouch.activeTouches.Count;
#else
        return -1;
#endif
    }

    public static TouchPressType GetTouchPressType (int touchIndex)
    {
#if ENABLE_INPUT_SYSTEM
        if (getNewTouchCount () <= touchIndex) {
            return TouchPressType.None;
        }

        var touchToCheck = enhancedTouch.activeTouches [touchIndex];

        switch (touchToCheck.phase) {
            case TouchPhase.Began:
                return TouchPressType.Began;

            case TouchPhase.Moved:
                if (touchToCheck.delta.sqrMagnitude > 0.0001f) {
                    return TouchPressType.Moved;
                }

                return TouchPressType.Stationary;

            case TouchPhase.Stationary:
                return TouchPressType.Stationary;

            case TouchPhase.Ended:
                return TouchPressType.Ended;

            case TouchPhase.Canceled:
                return TouchPressType.Canceled;
        }
#endif

        return TouchPressType.None;
    }

    public static Vector2 GetTouchPosition (int touchIndex)
    {
#if ENABLE_INPUT_SYSTEM
        if (getNewTouchCount () <= touchIndex) {
            return Vector2.zero;
        }

        var touchToCheck = enhancedTouch.activeTouches [touchIndex];

        return touchToCheck.screenPosition;
#else
        return Vector2.zero;
#endif
    }

    //OLD TOUCH INPUT

    public static TouchPressType GetTouchPressType (oldTouch touch)
    {
        switch (touch.phase) {
            case oldTouchPhase.Began:
                return TouchPressType.Began;

            case oldTouchPhase.Moved:
                if (touch.deltaPosition.sqrMagnitude > 0.0001f) {
                    return TouchPressType.Moved;
                }

                return TouchPressType.Stationary;

            case oldTouchPhase.Stationary:
                return TouchPressType.Stationary;

            case oldTouchPhase.Ended:
                return TouchPressType.Ended;

            case oldTouchPhase.Canceled:
                return TouchPressType.Canceled;
        }

        return TouchPressType.None;
    }

    public static TouchPressType GetTouchPressType (bool down, bool hold, bool up)
    {
        if (down) {
            return TouchPressType.Began;
        }

        if (up) {
            return TouchPressType.Ended;
        }

        if (hold) {
            return TouchPressType.Stationary;
        }

        return TouchPressType.None;
    }

    public static oldTouch getTouch (int touchIndex)
    {
        return Input.GetTouch (touchIndex);
    }

    public static int getTouchCount ()
    {
        return Input.touchCount;
    }

    public static bool isTouchPhaseBegan (oldTouch touchToCheck)
    {
        return (touchToCheck.phase == oldTouchPhase.Began);
    }

    public static bool isTouchPhaseStationary (oldTouch touchToCheck)
    {
        return (touchToCheck.phase == oldTouchPhase.Stationary);
    }

    public static bool isTouchPhaseMoved (oldTouch touchToCheck)
    {
        return (touchToCheck.phase == oldTouchPhase.Moved);
    }

    public static bool isTouchPhaseEnded (oldTouch touchToCheck)
    {
        return (touchToCheck.phase == oldTouchPhase.Ended);
    }


    //OTHER TOUCH STUFF

    public static Vector3 getAcceleration ()
    {
        if (useNewInputSystem) {
#if ENABLE_INPUT_SYSTEM
            return mainAccelerometer.acceleration.ReadValue ();
#else
            return Vector3.zero;
#endif
        } else {
            return Input.acceleration;
        }
    }

    //END OF TOUCH FUNCTIONS

    public static bool GetKeyDown (KeyCode keyToCheck)
    {
        if (useNewInputSystem) {
#if ENABLE_INPUT_SYSTEM
            Key currentkey = convertKeyCodeToKey (keyToCheck);

            if (currentkey != Key.None) {
                return mainKeyboard [currentkey].wasPressedThisFrame;
            }

            return false;
#else
            return false;
#endif
        } else {
            return Input.GetKeyDown (keyToCheck);
        }
    }

#if ENABLE_INPUT_SYSTEM
    public static Key convertKeyCodeToKey (KeyCode keyCode)
    {
        switch (keyCode) {
            case KeyCode.A: return Key.A;
            case KeyCode.B: return Key.B;
            case KeyCode.C: return Key.C;
            case KeyCode.Space: return Key.Space;
            case KeyCode.Return: return Key.Enter;
            case KeyCode.Escape: return Key.Escape;

            case KeyCode.Alpha0: return Key.Digit0;
            case KeyCode.Alpha1: return Key.Digit1;
            case KeyCode.Alpha2: return Key.Digit2;

            case KeyCode.UpArrow: return Key.UpArrow;
            case KeyCode.DownArrow: return Key.DownArrow;
            case KeyCode.LeftArrow: return Key.LeftArrow;
            case KeyCode.RightArrow: return Key.RightArrow;

            case KeyCode.F1: return Key.F1;
            case KeyCode.F2: return Key.F2;

            default: return Key.None;
        }
    }
#endif

    public static bool isAnyKeyDown ()
    {
        if (useNewInputSystem) {
#if ENABLE_INPUT_SYSTEM

            if (mainKeyboardFound) {
                if (mainKeyboard.anyKey.wasPressedThisFrame) {
                    return true;
                }
            }

            if (mainMouseFound) {
                foreach (var control in mainMouse.allControls) {
                    if (control is ButtonControl button && button.wasPressedThisFrame) {
                        return true;
                    }
                }
            }

            if (mainTouchscreenFound) {
                if (getNewTouchCount () > 0) {
                    return true;
                }
            }

            return false;
#else
            return false;
#endif
        } else {
            return Input.anyKeyDown;
        }
    }

    public static bool isAnyGamepadButtonDown ()
    {
        if (useNewInputSystem) {
#if ENABLE_INPUT_SYSTEM
            foreach (var pad in Gamepad.all) {
                foreach (var control in pad.allControls) {
                    if (control is ButtonControl btn && btn.wasPressedThisFrame) {
                        return true;
                    }
                }
            }

#endif
        }

        return false;
    }

    public static void setGamepadVibrationByPlayerID (int playerID, float low, float high, float duration)
    {
        if (inputManagerLocated) {
            mainInputManager.setGamepadVibrationByPlayerID (playerID, low, high, duration);
        }
    }

    public static void setGamepadVibrationByPlayerID (int playerID, float low, float high)
    {
        if (inputManagerLocated) {
            mainInputManager.setGamepadVibrationByPlayerID (playerID, low, high);
        }
    }

    public static void stopGamepadVibrationByPlayerID (int playerID)
    {
        if (inputManagerLocated) {
            mainInputManager.stopGamepadVibrationByPlayerID (playerID);
        }
    }

    public static bool isAnyOldKeyDown ()
    {
        return Input.anyKeyDown;
    }

#if ENABLE_INPUT_SYSTEM
    public static readonly Dictionary<KeyCode, Key> keycodeToKeyDictionary = new Dictionary<KeyCode, Key> ()
    {
        // Letters
        { KeyCode.A, Key.A }, { KeyCode.B, Key.B }, { KeyCode.C, Key.C },
        { KeyCode.D, Key.D }, { KeyCode.E, Key.E }, { KeyCode.F, Key.F },
        { KeyCode.G, Key.G }, { KeyCode.H, Key.H }, { KeyCode.I, Key.I },
        { KeyCode.J, Key.J }, { KeyCode.K, Key.K }, { KeyCode.L, Key.L },
        { KeyCode.M, Key.M }, { KeyCode.N, Key.N }, { KeyCode.O, Key.O },
        { KeyCode.P, Key.P }, { KeyCode.Q, Key.Q }, { KeyCode.R, Key.R },
        { KeyCode.S, Key.S }, { KeyCode.T, Key.T }, { KeyCode.U, Key.U },
        { KeyCode.V, Key.V }, { KeyCode.W, Key.W }, { KeyCode.X, Key.X },
        { KeyCode.Y, Key.Y }, { KeyCode.Z, Key.Z },

        // Numbers
        { KeyCode.Alpha0, Key.Digit0 },
        { KeyCode.Alpha1, Key.Digit1 },
        { KeyCode.Alpha2, Key.Digit2 },
        { KeyCode.Alpha3, Key.Digit3 },
        { KeyCode.Alpha4, Key.Digit4 },
        { KeyCode.Alpha5, Key.Digit5 },
        { KeyCode.Alpha6, Key.Digit6 },
        { KeyCode.Alpha7, Key.Digit7 },
        { KeyCode.Alpha8, Key.Digit8 },
        { KeyCode.Alpha9, Key.Digit9 },

        // Numpad
        { KeyCode.Keypad0, Key.Numpad0 },
        { KeyCode.Keypad1, Key.Numpad1 },
        { KeyCode.Keypad2, Key.Numpad2 },
        { KeyCode.Keypad3, Key.Numpad3 },
        { KeyCode.Keypad4, Key.Numpad4 },
        { KeyCode.Keypad5, Key.Numpad5 },
        { KeyCode.Keypad6, Key.Numpad6 },
        { KeyCode.Keypad7, Key.Numpad7 },
        { KeyCode.Keypad8, Key.Numpad8 },
        { KeyCode.Keypad9, Key.Numpad9 },

        { KeyCode.KeypadEnter, Key.NumpadEnter },
        { KeyCode.KeypadPlus, Key.NumpadPlus },
        { KeyCode.KeypadMinus, Key.NumpadMinus },
        { KeyCode.KeypadMultiply, Key.NumpadMultiply },
        { KeyCode.KeypadDivide, Key.NumpadDivide },
        { KeyCode.KeypadPeriod, Key.NumpadPeriod },

        // Function
        { KeyCode.F1, Key.F1 }, { KeyCode.F2, Key.F2 }, { KeyCode.F3, Key.F3 },
        { KeyCode.F4, Key.F4 }, { KeyCode.F5, Key.F5 }, { KeyCode.F6, Key.F6 },
        { KeyCode.F7, Key.F7 }, { KeyCode.F8, Key.F8 }, { KeyCode.F9, Key.F9 },
        { KeyCode.F10, Key.F10 }, { KeyCode.F11, Key.F11 }, { KeyCode.F12, Key.F12 },

        // Control
        { KeyCode.Space, Key.Space },
        { KeyCode.Return, Key.Enter },
        { KeyCode.Escape, Key.Escape },
        { KeyCode.Backspace, Key.Backspace },
        { KeyCode.Tab, Key.Tab },

        // Modifiers
        { KeyCode.LeftShift, Key.LeftShift },
        { KeyCode.RightShift, Key.RightShift },
        { KeyCode.LeftControl, Key.LeftCtrl },
        { KeyCode.RightControl, Key.RightCtrl },
        { KeyCode.LeftAlt, Key.LeftAlt },
        { KeyCode.RightAlt, Key.RightAlt },

        // Arrows
        { KeyCode.UpArrow, Key.UpArrow },
        { KeyCode.DownArrow, Key.DownArrow },
        { KeyCode.LeftArrow, Key.LeftArrow },
        { KeyCode.RightArrow, Key.RightArrow },

        // Navegation
        { KeyCode.Insert, Key.Insert },
        { KeyCode.Delete, Key.Delete },
        { KeyCode.Home, Key.Home },
        { KeyCode.End, Key.End },
        { KeyCode.PageUp, Key.PageUp },
        { KeyCode.PageDown, Key.PageDown },
        { KeyCode.Menu, Key.ContextMenu },

        // Blocks
        { KeyCode.CapsLock, Key.CapsLock },
        { KeyCode.Numlock, Key.NumLock },
        { KeyCode.ScrollLock, Key.ScrollLock },

        // Simbols
        { KeyCode.Minus, Key.Minus },
        { KeyCode.Equals, Key.Equals },
        { KeyCode.LeftBracket, Key.LeftBracket },
        { KeyCode.RightBracket, Key.RightBracket },
        { KeyCode.Backslash, Key.Backslash },
        { KeyCode.Semicolon, Key.Semicolon },
        { KeyCode.Quote, Key.Quote },
        { KeyCode.Comma, Key.Comma },
        { KeyCode.Period, Key.Period },
        { KeyCode.Slash, Key.Slash },
        { KeyCode.BackQuote, Key.Backquote },
    };

    public static bool TryGetGamepadButtonString (ButtonControl control, out string buttonName)
    {
        buttonName = "";

        if (control == null)
            return false;

        switch (control.name) {
            case "buttonSouth":
                buttonName = GamepadButton.South.ToString ();
                return true;

            case "buttonNorth":
                buttonName = GamepadButton.North.ToString ();
                return true;

            case "buttonEast":
                buttonName = GamepadButton.East.ToString ();
                return true;

            case "buttonWest":
                buttonName = GamepadButton.West.ToString ();
                return true;

            case "startButton":
                buttonName = GamepadButton.Start.ToString ();
                return true;

            case "selectButton":
                buttonName = GamepadButton.Select.ToString ();
                return true;

            case "leftShoulder":
                buttonName = GamepadButton.LeftShoulder.ToString ();
                return true;

            case "rightShoulder":
                buttonName = GamepadButton.RightShoulder.ToString ();
                return true;

            case "leftStickPress":
                buttonName = GamepadButton.LeftStick.ToString ();
                return true;

            case "rightStickPress":
                buttonName = GamepadButton.RightStick.ToString ();
                return true;

            case "dpad/up":
                buttonName = GamepadButton.DpadUp.ToString ();
                return true;

            case "dpad/down":
                buttonName = GamepadButton.DpadDown.ToString ();
                return true;

            case "dpad/left":
                buttonName = GamepadButton.DpadLeft.ToString ();
                return true;

            case "dpad/right":
                buttonName = GamepadButton.DpadRight.ToString ();
                return true;
        }

        return false;
    }


    public static bool TryParseGamepadButton (string name, out GamepadButton button)
    {
        button = default;

        if (string.IsNullOrEmpty (name))
            return false;

        name = name.Trim ();

        return System.Enum.TryParse (name, true, out button);
    }
#endif

    public static string convertControlButtonToMouseButtonName (string mouseButtonName)
    {
        switch (mouseButtonName) {
            case "leftButton":
                return "Left";

            case "rightButton":
                return "Right";

            case "middleButton":
                return "Middle";

            case "forwardButton":
                return "Forward";

            case "backButton":
                return "Back";

            default:
                return "";
        }
    }

    public static string convertMouseButtonToFullName (string mouseButtonName)
    {
        switch (mouseButtonName) {
            case "Left":
                return "Left Mouse";

            case "Right":
                return "Right Mouse";

            case "Middle":
                return "Middle Mouse";

            case "Forward":
                return "Forward Mouse";

            case "Back":
                return "Back Mouse";

            default:
                return "";
        }
    }

#if ENABLE_INPUT_SYSTEM
    public static GamepadButton convertOldJoystickToNewInputButton (joystickButtons button)
    {
        switch (button) {
            case joystickButtons.A:
                return GamepadButton.South;

            case joystickButtons.B:
                return GamepadButton.East;

            case joystickButtons.X:
                return GamepadButton.West;

            case joystickButtons.Y:
                return GamepadButton.North;

            case joystickButtons.LeftBumper:
                return GamepadButton.LeftShoulder;

            case joystickButtons.RightBumper:
                return GamepadButton.RightShoulder;

            case joystickButtons.Back:
                return GamepadButton.Select;

            case joystickButtons.Start:
                return GamepadButton.Start;

            case joystickButtons.LeftStickClick:
                return GamepadButton.LeftStick;

            case joystickButtons.RightStickClick:
                return GamepadButton.RightStick;

            case joystickButtons.LeftDPadX:
                return GamepadButton.DpadLeft;

            case joystickButtons.RightDPadX:
                return GamepadButton.DpadRight;

            case joystickButtons.TopDPadY:
                return GamepadButton.DpadUp;

            case joystickButtons.BottomDPadY:
                return GamepadButton.DpadDown;

            case joystickButtons.LeftTrigger:
                return GamepadButton.LeftTrigger;

            case joystickButtons.RightTrigger:
                return GamepadButton.RightTrigger;

            case joystickButtons.None:
            default:
                return newGamepadButtonNone;
        }
    }

    public static bool GetMouseButtonByName (string name, out newMouseButton button)
    {
        switch (name) {
            case "Left":
                button = newMouseButton.Left;
                return true;

            case "Right":
                button = newMouseButton.Right;
                return true;

            case "Middle":
                button = newMouseButton.Middle;
                return true;

            case "Forward":
                button = newMouseButton.Forward;
                return true;

            case "Back":
                button = newMouseButton.Back;
                return true;

            default:
                button = default;
                return false;
        }
    }

    public static bool TryGetMouseButton (KeyCode keyCode, out newMouseButton button)
    {
        switch (keyCode) {
            case KeyCode.Mouse0:
                button = newMouseButton.Left;
                return true;

            case KeyCode.Mouse1:
                button = newMouseButton.Right;
                return true;

            case KeyCode.Mouse2:
                button = newMouseButton.Middle;
                return true;

            case KeyCode.Mouse3:
                button = newMouseButton.Forward;
                return true;

            case KeyCode.Mouse4:
                button = newMouseButton.Back;
                return true;

            default:
                button = default;
                return false;
        }
    }

    public static string GetDeviceKey (Gamepad g)
    {
        var d = g.description;

        return string.Join ("|", new []
        {
        g.layout,
        g.name,
        d.interfaceName,
        d.product,
        d.manufacturer
        });
    }
#endif

    public static string [] GetJoystickNames ()
    {
        return Input.GetJoystickNames ();
    }


    //CHECK UNITY WINDOW EDITOR AND GAME BUILD FOCUS STATE FOR INPUT IGNORE

    public static bool hasFocusOnGameWindow { get; private set; }

    public static void updateHasFocusOnGameWindowValue ()
    {
#if UNITY_EDITOR
        hasFocusOnGameWindow =
            UnityEditor.EditorWindow.focusedWindow != null &&
            UnityEditor.EditorWindow.focusedWindow.GetType () == GameViewType;
#else
        hasFocusOnGameWindow = Application.isFocused;
#endif
    }

#if UNITY_EDITOR
    static readonly Type GameViewType =
        Type.GetType ("UnityEditor.GameView,UnityEditor");
#endif
}