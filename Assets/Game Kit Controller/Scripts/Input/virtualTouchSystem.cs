using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

using static InputAxisWrapper;



#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

public class virtualTouchSystem : MonoBehaviour
{
    [Header ("Movement")]
    [Space]

    public bool virtualTouchSystemEnabled = true;

    public bool ignoreActualMousePressEnabled = true;

    public bool ignoreChangeInputModuleStateEnabled;

    public bool allowGamepadInTouchDevice = true;

    [Space]
    [Header ("Debug")]
    [Space]

    public bool showDebugPrint;

    public bool virtualTouchSystemActive;

    public Vector2 currentTouchPosition;

    public bool screenFocused;

    public int playerGamepadId;

    public bool UINavigationCurrentlyActive;

    public bool isUsingTouchControls;

    [Space]
    [Header ("Components")]
    [Space]

    public playerInputManager mainPlayerInputManager;

    public virtualCursorControllerSystem mainVirtualCursorControllerSystem;

    public EventSystem mainEventSystem;

    public VirtualPointerManager mainVirtualPointerManager;


    private VirtualPointer pointer;

    bool pointerInitialized;

    int fingerID;

    VirtualTouch touch;

    bool mainVirtualPointerManagerInitialized;

    Vector3 lastPointerPosition;

    bool mainPlayerInputManagerLocated;


    void Awake ()
    {
        mainPlayerInputManagerLocated = mainPlayerInputManager != null;
    }

    void Start ()
    {
        if (!InputAxisWrapper.isUseNewInputSystem ()) {
            virtualTouchSystemEnabled = false;
        }

        if (!virtualTouchSystemEnabled) {
            enabled = false;
        }
    }

    void Update ()
    {
        if (!virtualTouchSystemEnabled) {
            return;
        }

        if (UINavigationCurrentlyActive) {
            return;
        }

        if (virtualTouchSystemActive) {
            UpdateVirtualTouchSystem ();
        } else {
            if (mainPlayerInputManagerLocated) {
                if (mainPlayerInputManager.isUsingTouchControls ()) {
                    setVirtualTouchSystemActiveState (true);
                }
            } else {

            }

            updateMainVirtualPointerManager ();
        }
    }

    void UpdateVirtualTouchSystem ()
    {
        Vector2 uiCursor = mainVirtualCursorControllerSystem.getCurrentscreenPos ();
        bool useVirtual = mainVirtualCursorControllerSystem.isCursorCurrentlyActive ();

        InputAxisWrapper.updateHasFocusOnGameWindowValue ();

        screenFocused = InputAxisWrapper.hasFocusOnGameWindow;

        isUsingTouchControls = false;

        if (mainPlayerInputManagerLocated) {
            isUsingTouchControls = mainPlayerInputManager.isUsingTouchControls () && !mainPlayerInputManager.isAllowGamepadInTouchDevice ();
        } else {
            isUsingTouchControls = InputAxisWrapper.checkTouchPlatform () && !allowGamepadInTouchDevice;
        }

        pointer.Update (playerGamepadId, uiCursor, useVirtual, screenFocused, ignoreActualMousePressEnabled, isUsingTouchControls);

        touch = TouchAdapter.ToVirtualTouch (pointer, playerGamepadId);

        currentTouchPosition = touch.position;

        fingerID = touch.fingerId;

        updateMainVirtualPointerManager ();

        if (showDebugPrint) {
            if (touch.down) {
                Debug.Log ("down on position");
            }

            if (touch.up) {
                Debug.Log ("up on position");
            }

            if (touch.held) {
                Debug.Log ("held on position");
            }
        }
    }

    void updateMainVirtualPointerManager ()
    {
        if (mainVirtualPointerManagerInitialized) {

            if (mainVirtualPointerManager.pointers.TryGetValue (playerGamepadId, out var pointer)) {

                bool isUsingGamepad = false;

                if (mainPlayerInputManagerLocated) {
                    isUsingGamepad = mainPlayerInputManager.isUsingGamepad ();
                } else {
                    isUsingGamepad = GamepadRegistry.anyGamepadFound ();
                }

                if (isUsingGamepad) {
                    lastPointerPosition = touch.position;

                    pointer.position = lastPointerPosition;

                    pointer.down = touch.down;

                    pointer.held = touch.held;

                    pointer.up = touch.up;
                } else {
                    pointer.position = lastPointerPosition;

                    pointer.down = false;

                    pointer.held = false;

                    pointer.up = false;

                    pointer.dragging = false;
                }
            }

            mainVirtualPointerManager.ProcessAll ();
        } else {
            if (mainPlayerInputManagerLocated) {
                playerGamepadId = mainPlayerInputManager.getPlayerID ();
            }

            if (playerGamepadId > 0) {

                mainVirtualPointerManager.Register (playerGamepadId);

                mainVirtualPointerManagerInitialized = true;
            }
        }
    }

    public void setVirtualTouchSystemActiveState (bool state)
    {
        if (!virtualTouchSystemEnabled) {
            return;
        }

        virtualTouchSystemActive = state;

        if (virtualTouchSystemActive) {
            if (!pointerInitialized) {
                pointer = new VirtualPointer ();

                pointerInitialized = true;

                setInputModuleState (false);
            }

            if (mainPlayerInputManagerLocated) {
                playerGamepadId = mainPlayerInputManager.getPlayerID ();
            }
        } else {

        }
    }

    public Vector2 getCurrentTouchPosition ()
    {
        return currentTouchPosition;
    }

    public TouchPressType GetTouchPressType ()
    {
        if (pointerInitialized) {
            return InputAxisWrapper.GetTouchPressType (pointer.Down, pointer.Held, pointer.Up);
        }

        return TouchPressType.None;
    }

    public int getFingerID ()
    {
        return fingerID;
    }

    public void setVirtualTouchSystemEnabledState (bool state)
    {
        virtualTouchSystemEnabled = state;
    }

    void setInputModuleState (bool state)
    {
        if (ignoreChangeInputModuleStateEnabled) {
            return;
        }

        if (mainEventSystem == null) {
            mainEventSystem = EventSystem.current;
        }

#if ENABLE_INPUT_SYSTEM
        InputSystemUIInputModule mainInputSystemUIInputModule = mainEventSystem.gameObject.GetComponent<InputSystemUIInputModule> ();

        if (mainInputSystemUIInputModule != null) {
            if (state) {
                //disable move with joystick
                mainInputSystemUIInputModule.move.action.Enable ();

                //disable submit (A / Cross) with joystick
                mainInputSystemUIInputModule.submit.action.Enable ();

                ///disable cancel with joystick
                mainInputSystemUIInputModule.cancel.action.Enable ();
            } else {
                //disable move with joystick
                mainInputSystemUIInputModule.move.action.Disable ();

                //disable submit (A / Cross) with joystick
                mainInputSystemUIInputModule.submit.action.Disable ();

                ///disable cancel with joystick
                mainInputSystemUIInputModule.cancel.action.Disable ();
            }
        }
#endif
    }

    public void ResetPointerOnUIPanelChange ()
    {
        if (pointerInitialized) {
            mainVirtualPointerManager.ResetPointer (playerGamepadId);
        }
    }

    public void setUINavigationCurrentlyActive (bool state)
    {
        UINavigationCurrentlyActive = state;

        mainVirtualCursorControllerSystem.setUINavigationCurrentlyActive (state);
    }

    public bool isUINavigationCurrentlyActive ()
    {
        return UINavigationCurrentlyActive;
    }

    public void moveCursorToCornerNextVisibleTime ()
    {
        mainVirtualCursorControllerSystem.moveCursorToCornerNextVisibleTime ();
    }

    public void setForceResetCursorPositionActiveState (bool state)
    {
        mainVirtualCursorControllerSystem.setForceResetCursorPositionActiveState (state);
    }

    public bool isVirtualCursorCurrentlyActive ()
    {
        return mainVirtualCursorControllerSystem.isCursorCurrentlyActive ();
    }

    //EDITOR FUNCTIONS
    public void setVirtualTouchSystemEnabledStateFromEditor (bool state)
    {
        setVirtualTouchSystemEnabledState (state);

        updateComponent ();
    }

    void updateComponent ()
    {
        GKC_Utils.updateComponent (this);

        GKC_Utils.updateDirtyScene ("Update Virtual Touch System Inspector", gameObject);
    }
}
