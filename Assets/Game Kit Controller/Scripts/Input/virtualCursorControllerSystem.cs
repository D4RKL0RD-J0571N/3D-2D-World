using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.UI;


public class virtualCursorControllerSystem : MonoBehaviour
{
    [Header ("Movement")]
    [Space]

    public bool virtualCursorControllerEnabled;

    public float cursorSpeed = 7;

    public bool virtualCursorVisibleEnabled = true;

    [Space]

    public bool useSmoothCursorSpeed;

    public float maxSmoothSpeed = 200;
    public float acceleration = 2000f;

    public float smoothing = 15f;
    public float deadzone = 0.15f;

    public int joystickNumber = 1;

    [Space]
    [Header ("Bounds")]
    [Space]

    public float edgePadding = 8f;

    public bool useBothJoysticks;
    public bool useRightJoystickAxis;

    public float rightJoystickSpeedMultiplier = 1;

    public bool ignoreMoveVirtualCursorWithActualMouse;

    [Space]
    [Header ("Debug")]
    [Space]

    public bool useNewInputSystem;

    public bool cursorCurrentlyActive;

    public bool canUseGamepad;

    public bool screenFocused;

    public bool usingActualMouse;

    public bool isPressing;

    public Vector2 screenPos;

    public bool UINavigationCurrentlyActive;

    [Space]
    [Header ("Components")]
    [Space]

    public RectTransform cursorRect;
    public Canvas canvas;
    public Camera uiCamera;
    public RectTransform canvasRect;

    public playerInputManager playerInput;


    Vector2 axisInput;

    Vector2 realMouseAxisInput;

    float inputDifference;

    float currentUnscaledTime;

    float lastTimeRealMouseUsed;

    bool playerInputLocated;

    float lastTimeActive = 0;

    bool moveCursorToCornerNextVisibleTimeActive;
    bool forceResetCursorPositionActive;

    bool touchPlatform;


    void Awake ()
    {
        touchPlatform = InputAxisWrapper.checkTouchPlatform ();

        if (touchPlatform) {
            virtualCursorControllerEnabled = true;
        }
    }

    void Start ()
    {
        playerInputLocated = playerInput != null;
    }


    void Update ()
    {
        if (cursorCurrentlyActive) {
            if (UINavigationCurrentlyActive) {
                return;
            }

            canUseGamepad = false;

            if (playerInputLocated) {
                if (playerInput.isUsingGamepad () && (!playerInput.isUsingTouchControls () || playerInput.isAllowGamepadInTouchDevice ())) {
                    canUseGamepad = true;
                }
            } else {
                canUseGamepad = true;
            }

            if (canUseGamepad) {

                HandleMovement ();
                ApplyPosition ();
            }
        }
    }

    void HandleMovement ()
    {
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
            realMouseAxisInput = InputAxisWrapper.GetLookAxis (0.05f);
        }

        inputDifference = Mathf.Abs (realMouseAxisInput.sqrMagnitude);


        currentUnscaledTime = Time.unscaledTime;


        if (inputDifference > 0.01f) {
            lastTimeRealMouseUsed = currentUnscaledTime;

            axisInput = realMouseAxisInput;
        }

        if (!ignoreMoveVirtualCursorWithActualMouse) {
            if (currentUnscaledTime > lastTimeRealMouseUsed + 1 && screenFocused) {
                if (usingActualMouse) {
                    screenPos = InputAxisWrapper.getMousePosition ();

                    usingActualMouse = false;
                }
            } else {
                usingActualMouse = true;
            }
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
            }
        }

        if (usingActualMouse) {
            screenPos = InputAxisWrapper.getMousePosition ();
        } else {
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

                screenPos += velocity;
            } else {
                screenPos += axisInput * cursorSpeed;
            }
        }

        ClampToCanvas ();
    }

    Vector2 velocity;

    void ClampToCanvas ()
    {
        Vector2 localPoint;

        RectTransformUtility.ScreenPointToLocalPointInRectangle (
            canvasRect,
            screenPos,
            canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : uiCamera,
            out localPoint
        );

        Rect rect = canvasRect.rect;

        localPoint.x = Mathf.Clamp (localPoint.x, rect.xMin + edgePadding, rect.xMax - edgePadding);
        localPoint.y = Mathf.Clamp (localPoint.y, rect.yMin + edgePadding, rect.yMax - edgePadding);

        screenPos = RectTransformUtility.WorldToScreenPoint (
            canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : uiCamera,
            canvasRect.TransformPoint (localPoint)
        );
    }


    void ApplyPosition ()
    {
        Vector2 worldPoint;
        RectTransformUtility.ScreenPointToLocalPointInRectangle (
            canvasRect,
            screenPos,
            canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : uiCamera,
            out worldPoint
        );

        cursorRect.anchoredPosition = worldPoint;
    }

    public void setCursorActiveState (bool state)
    {
        playerInputLocated = playerInput != null;

        useNewInputSystem = InputAxisWrapper.isUseNewInputSystem ();

        if (playerInputLocated) {
            if (!playerInput.isUsingGamepad ()) {
                state = false;
            }
        }

        cursorCurrentlyActive = state;

        if (virtualCursorVisibleEnabled) {
            bool setCursorVisibleResult = cursorCurrentlyActive;

            if ((playerInputLocated && playerInput.isUINavigationCurrentlyActive ()) || UINavigationCurrentlyActive) {
                setCursorVisibleResult = false;
            }

            setCursorGameObjectActiveState (setCursorVisibleResult);
        }

        if (state) {
            if (lastTimeActive == 0 || (Time.time > lastTimeActive + 0.2f) || forceResetCursorPositionActive) {
                resetCursorToScreenCenter ();
            }
        }

        forceResetCursorPositionActive = false;

        if (cursorCurrentlyActive) {
            menuPause.setCursorVisibleState (false);

            menuPause.setCursorLockState (true);
        } else {
            lastTimeActive = Time.time;
        }
    }

    void setCursorGameObjectActiveState (bool state)
    {
        if (cursorRect.gameObject.activeSelf != state) {
            cursorRect.gameObject.SetActive (state);
        }
    }

    public void moveVirtualCursorToUIElement (RectTransform newTransform)
    {
        if (newTransform == null) {
            return;
        }

        Camera currentCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : uiCamera;

        Vector3 elementWorldCenter = newTransform.TransformPoint (newTransform.rect.center);

        screenPos = RectTransformUtility.WorldToScreenPoint (
            currentCamera,
            elementWorldCenter
        );

        ClampToCanvas ();

        ApplyPosition ();
    }

    public void setNewCanvasForVirtualCursor (Canvas newCanvas)
    {
        if (newCanvas == null) {
            return;
        }

        Vector2 currentScreenPosition = screenPos;

        canvas = newCanvas;

        canvasRect = canvas.GetComponent<RectTransform> ();

        if (canvas.renderMode == RenderMode.ScreenSpaceOverlay) {
            uiCamera = null;
        } else {
            uiCamera = canvas.worldCamera;

            if (uiCamera == null) {
                uiCamera = Camera.main;
            }
        }

        cursorRect.SetParent (canvasRect, false);

        cursorRect.localScale = Vector3.one;
        cursorRect.localRotation = Quaternion.identity;

        screenPos = currentScreenPosition;

        ClampToCanvas ();
        ApplyPosition ();
    }

    void resetCursorToScreenCenter ()
    {
        Vector2 canvasCenterWorld = canvasRect.TransformPoint (canvasRect.rect.center);

        if (moveCursorToCornerNextVisibleTimeActive) {
            screenPos = Vector2.zero;

            moveCursorToCornerNextVisibleTimeActive = false;
        } else {
            screenPos = RectTransformUtility.WorldToScreenPoint (
                canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : uiCamera,
                canvasCenterWorld
            );
        }
    }

    public Vector2 getCurrentscreenPos ()
    {
        return screenPos;
    }

    public bool isCursorCurrentlyActive ()
    {
        return cursorCurrentlyActive;
    }

    public bool isVirtualCursorControllerEnabled ()
    {
        return virtualCursorControllerEnabled;
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

    public void moveCursorToCornerNextVisibleTime ()
    {
        moveCursorToCornerNextVisibleTimeActive = true;
    }

    public void setForceResetCursorPositionActiveState (bool state)
    {
        forceResetCursorPositionActive = state;
    }

    public void setUINavigationCurrentlyActive (bool state)
    {
        UINavigationCurrentlyActive = state;

        if (UINavigationCurrentlyActive) {
            if (cursorCurrentlyActive) {
                if (virtualCursorVisibleEnabled) {
                    setCursorGameObjectActiveState (false);
                }
            }
        }
    }
}
