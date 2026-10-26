using System.Reflection;
using UnityEngine;
using UnityEngine.UI;
using static InputAxisWrapper;

public class touchJoystick : MonoBehaviour
{
    [Header ("Main Settings")]
    [Space]

    public int fingerId = 666;

    public float dragDistance = 1;
    public bool snapsToFinger = true;
    public bool hideOnRelease = false;
    public bool touchPad;

    public float touchPadHorizontalExtraValue = 1;
    public float touchPadVerticalExtraValue = 1;
    public bool showJoystick;

    [Space]
    [Header ("Other Settings")]
    [Space]

    public bool clampJoystickToScreenSide;
    public bool joystickOnRightScreenSide;

    [Space]

    public bool checkIfPressInsideTouchZone;
    public RectTransform touchZoneRectTransform;

    [Space]
    [Header ("Colors Settings")]
    [Space]

    public bool useChaneColor;
    public float changeColorSpeed;
    public Color regularBaseColor;
    public Color regularStickColor;
    public Color pressedBaseColor;
    public Color pressedStickColor;

    [Space]
    [Header ("Debug")]
    [Space]

    public bool showDebugPrint;
    public Vector2 currentAxisValue;
    public Vector2 currentTouchPosition;
    public bool touching;
    public bool hoveringJoystick;
    public bool touchingPreviously;

    public bool adjustinColors;

    public bool buttonColorVisible = true;

    public bool joystickClampedToScreenSideActive;

    [Space]
    [Header ("Components")]
    [Space]

    public GameObject stickBase;
    public GameObject stick;
    public RectTransform baseRectTransform;
    public RectTransform stickRectTransform;
    public RawImage baseRenderer;
    public RawImage stickRenderer;


    bool touchPlatform;

    bool useNewInputSystem;

    InputAxisWrapper.TouchPressType touchPressType;

    Color currentBaseColor;
    Color currentStickColor;

    Vector3 previousPosition;
    Vector2 originalStickPosition;
    Vector2 originalBasePosition;
    Vector3 globalTouchPosition;
    Vector3 differenceVector;
    Vector3 difference;
    int touchCount;

    Touch currentPressedTouch;
    Touch currentTouch;

    int currentFingerId;

    int currentTouchIndex;

    bool firstTouchActive;

    float screenWidth;


    int currentTouchFingerId;

    Vector2 currentPressedTouchPosition;


    void Start ()
    {
        touchPlatform = InputAxisWrapper.checkTouchPlatform ();

        useNewInputSystem = InputAxisWrapper.isUseNewInputSystem ();

        if (!showJoystick) {
            setSticksState (false, false);
        }

        originalStickPosition = stickRectTransform.anchoredPosition;
        originalBasePosition = baseRectTransform.anchoredPosition;

        if (clampJoystickToScreenSide) {
            screenWidth = GKC_Utils.getScreenResolution ().x / 2;

            if (screenWidth <= 0) {
                screenWidth = Screen.width / 2;
            }
        }
    }

    void Update ()
    {
        setJoystickColors ();

        if (currentFingerId > -1) {
            moveJoystick ();
        }
    }

    public void setHoverState (bool state)
    {
        hoveringJoystick = state;

        if (hoveringJoystick && !touching) {
            touchCount = 0;

            if (touchPlatform && useNewInputSystem) {
                touchCount = InputAxisWrapper.getNewTouchCount ();
            } else {
                touchCount = InputAxisWrapper.getTouchCount ();
            }

            if (!touchPlatform) {
                touchCount++;
            }

            for (currentTouchIndex = 0; currentTouchIndex < touchCount; currentTouchIndex++) {
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

                //if the touch action has started, check if the finger is inside the touch zone rect, visible in the editor
                if (touchPressType == InputAxisWrapper.TouchPressType.Began) {
                    if (touchPlatform && useNewInputSystem) {
                        currentTouchPosition = InputAxisWrapper.GetTouchPosition (currentTouchIndex);
                    } else {
                        currentTouchPosition = currentTouch.position;
                    }

                    if (clampJoystickToScreenSide) {
                        joystickClampedToScreenSideActive = false;

                        bool isScreenRightSide = currentTouchPosition.x > screenWidth;

                        if (joystickOnRightScreenSide) {
                            if (!isScreenRightSide) {
                                joystickClampedToScreenSideActive = true;
                            }
                        } else {
                            if (isScreenRightSide) {
                                joystickClampedToScreenSideActive = true;
                            }
                        }

                        if (showDebugPrint) {
                            print ("joystick Clamped " + joystickClampedToScreenSideActive +
                                " isScreenRightSide " + isScreenRightSide + " " + currentTouchPosition.x +
                                " " + screenWidth);
                        }

                        if (joystickClampedToScreenSideActive) {
                            return;
                        }
                    }

                    if (checkIfPressInsideTouchZone) {
                        if (!RectTransformUtility.RectangleContainsScreenPoint (touchZoneRectTransform, currentTouchPosition)) {
                            if (showDebugPrint) {
                                print ("outside touch press " + fingerId);
                            }

                            return;
                        }
                    }

                    if (touchPlatform && useNewInputSystem) {
                        currentFingerId = InputAxisWrapper.getEnhancedTouchhControlTouchId (currentTouchIndex);
                    } else {
                        currentFingerId = currentTouch.fingerId;
                    }

                    fingerTouching (true);

                    if (snapsToFinger) {
                        stickRectTransform.position = currentTouchPosition;
                        baseRectTransform.position = stickRectTransform.position;
                    }

                    if (touchPad) {
                        previousPosition = currentTouchPosition;
                    }


                }
            }
        }
    }

    // set the value of touching to activate of deactivate the icons of the joystick
    void fingerTouching (bool state)
    {
        touching = state;

        if (showJoystick) {
            if (hideOnRelease) {
                setSticksState (state, state);
            } else if ((!stickBase.gameObject.activeSelf || !stick.gameObject.activeSelf)) {
                setSticksState (true, true);
            }
        } else {
            setSticksState (false, false);
        }
    }

    void setSticksState (bool stickBaseState, bool stickState)
    {
        if (stickBase.activeSelf != stickBaseState) {
            stickBase.SetActive (stickBaseState);
        }

        if (stick.activeSelf != stickState) {
            stick.SetActive (stickState);
        }
    }

    //if the joystick is released, the icons back to their default positions
    void resetJoystickPosition ()
    {
        stickRectTransform.anchoredPosition = originalStickPosition;
        baseRectTransform.anchoredPosition = originalBasePosition;

        currentAxisValue = Vector2.zero;

        fingerTouching (false);
    }

    //check if the joystick is being used and set the icons position according to the finger or mouse movement
    void moveJoystick ()
    {
        if (touching) {
            touchCount = 0;

            if (touchPlatform && useNewInputSystem) {
                touchCount = InputAxisWrapper.getNewTouchCount ();
            } else {
                touchCount = InputAxisWrapper.getTouchCount ();
            }

            if (!touchPlatform) {
                touchCount++;
            }

            if (touchPlatform && useNewInputSystem) {
                currentTouchFingerId = fingerId;
            } else {
                currentPressedTouch.fingerId = fingerId;

                currentTouchFingerId = fingerId;
            }

            for (currentTouchIndex = 0; currentTouchIndex < touchCount; currentTouchIndex++) {
                if (!touchPlatform) {
                    currentTouch = InputAxisWrapper.convertMouseIntoFinger ();
                } else {
                    if (!useNewInputSystem) {
                        currentTouch = InputAxisWrapper.getTouch (currentTouchIndex);
                    }
                }

                bool touchFoundResult = false;

                if (touchPlatform && useNewInputSystem) {
                    if (InputAxisWrapper.getEnhancedTouchhControlTouchId (currentTouchIndex) == currentFingerId) {
                        currentTouchFingerId = currentFingerId;

                        touchFoundResult = true;
                    }
                } else {
                    if (currentTouch.fingerId == currentFingerId) {
                        currentPressedTouch = currentTouch;

                        currentTouchFingerId = currentFingerId;

                        touchFoundResult = true;
                    }
                }

                if (touchFoundResult) {
                    if (touchPlatform && useNewInputSystem) {
                        touchPressType = InputAxisWrapper.GetTouchPressType (currentTouchIndex);

                        currentPressedTouchPosition = InputAxisWrapper.GetTouchPosition (currentTouchIndex);
                    } else {
                        touchPressType = InputAxisWrapper.GetTouchPressType (currentTouch);

                        currentPressedTouchPosition = currentPressedTouch.position;
                    }
                }
            }

            if (currentTouchFingerId == fingerId || touchPressType == InputAxisWrapper.TouchPressType.Ended) {
                resetJoystickPosition ();

                currentFingerId = -1;

                return;
            }

            globalTouchPosition = currentPressedTouchPosition;

            differenceVector = globalTouchPosition - stickBase.transform.position;

            if (differenceVector.sqrMagnitude > dragDistance * dragDistance) {
                differenceVector.Normalize ();

                differenceVector *= dragDistance;

                stickRectTransform.position = baseRectTransform.position + differenceVector;
            } else {
                stickRectTransform.position = globalTouchPosition;
            }

            if (!touchPad) {
                currentAxisValue = differenceVector / dragDistance;
            } else {
                difference = globalTouchPosition - previousPosition;

                if (differenceVector.sqrMagnitude > dragDistance * dragDistance) {
                    difference.Normalize ();
                }

                currentAxisValue = (difference / dragDistance);

                currentAxisValue.x *= touchPadHorizontalExtraValue;
                currentAxisValue.y *= touchPadVerticalExtraValue;

                previousPosition = globalTouchPosition;
            }
        }
    }

    //get the vertical and horizontal axis values
    public Vector2 GetAxis ()
    {
        return currentAxisValue;
    }

    public Vector2 getRawAxis ()
    {
        Vector2 axisValues = Vector2.zero;

        if (currentAxisValue.x > 0) {
            axisValues.x = 1;
        } else if (currentAxisValue.x < 0) {
            axisValues.x = -1;
        } else {
            axisValues.x = 0;
        }

        if (currentAxisValue.y > 0) {
            axisValues.y = 1;
        } else if (currentAxisValue.y < 0) {
            axisValues.y = -1;
        } else {
            axisValues.y = 0;
        }

        return axisValues;
    }

    void setJoystickColors ()
    {
        if (useChaneColor) {
            if (buttonColorVisible) {
                if (touching != touchingPreviously || !firstTouchActive) {
                    touchingPreviously = touching;

                    adjustinColors = true;

                    firstTouchActive = true;
                }

                if (adjustinColors) {
                    if (touching) {
                        currentBaseColor = pressedBaseColor;
                        currentStickColor = pressedStickColor;
                    } else {
                        currentBaseColor = regularBaseColor;
                        currentStickColor = regularStickColor;
                    }

                    baseRenderer.color = Color.Lerp (baseRenderer.color, currentBaseColor, Time.deltaTime * changeColorSpeed);

                    stickRenderer.color = Color.Lerp (stickRenderer.color, currentStickColor, Time.deltaTime * changeColorSpeed);

                    if (baseRenderer.color == currentBaseColor && stickRenderer.color == currentStickColor) {
                        adjustinColors = false;
                    }
                }
            }
        }
    }

    public void setTouchPadState (bool state)
    {
        touchPad = state;
    }

    public void setShowJoystickState (bool state)
    {
        showJoystick = state;

        setSticksState (showJoystick, showJoystick);
    }

    public void setSnapsToFingerState (bool state)
    {
        snapsToFinger = state;
    }

    public void setHideOnReleaseState (bool state)
    {
        hideOnRelease = state;
    }

    public void setJoystickColorVisibleState (bool state)
    {
        buttonColorVisible = state;

        Color targetColorBase = baseRenderer.color;
        Color targetColorStick = stickRenderer.color;

        if (buttonColorVisible) {
            targetColorBase.a = regularBaseColor.a;
            targetColorStick.a = regularStickColor.a;
        } else {
            targetColorBase.a = 0;
            targetColorStick.a = 0;
        }

        baseRenderer.color = targetColorBase;
        stickRenderer.color = targetColorStick;
    }

    public bool touchJoystickIsPressed ()
    {
        return touching;
    }
}