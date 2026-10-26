using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using static InputAxisWrapper;

public class touchScreenPinchSystem : MonoBehaviour
{
    [Header ("Main Settings")]
    [Space]

    public bool pinchEnabled = true;

    public float minDistanceToUpdatePinch = 5;

    [Space]

    public bool checkIfPressInsideRectTransform;
    public RectTransform pressZoneRectTransform;

    [Space]
    [Header ("Debug")]
    [Space]

    public bool showDebugPrint;

    public bool firstMouseTouchActive;
    public bool secondaryMouseTouchActive;

    public int currentNumberOfTouchButtonsPressed;

    public float initialDistance;

    public float currentDistance;

    public bool firstTouchInsidePressZoneResult = false;

    public bool secondTouchInsidePressZoneResult = false;

    [Space]
    [Header ("Events Settings")]
    [Space]

    public UnityEvent eventOnPinchIn;
    public UnityEvent eventOnPinchOut;

    [Space]
    [Header ("Components")]
    [Space]

    public inputManager input;
    public playerInputManager playerInput;


    bool touchPlatform;

    Touch firstTouch;

    Touch secondTouch;

    float distanceDifference;

    bool inputManagerLocated;

    InputAxisWrapper.TouchPressType firstTouchPressType;
    InputAxisWrapper.TouchPressType secondTouchPressType;

    bool useNewInputSystem;

    public Vector2 firstTouchPosition;
    public Vector2 secondTouchPosition;


    void Start ()
    {
        touchPlatform = InputAxisWrapper.checkTouchPlatform ();

        useNewInputSystem = InputAxisWrapper.isUseNewInputSystem ();

        inputManagerLocated = input != null;

        if (!inputManagerLocated) {
            input = inputManager.Instance;

            inputManagerLocated = input != null;
        }

        if (!inputManagerLocated) {
            input = GKC_Utils.findObjectByComponent<inputManager> ();

            input.getComponentInstanceOnApplicationPlaying ();

            inputManagerLocated = input != null;
        }
    }

    void Update ()
    {
        if (pinchEnabled && inputManagerLocated && input.isUsingTouchControls ()) {
            updateTouchPinchScreen ();
        }
    }

    void updateTouchPinchScreen ()
    {
        currentNumberOfTouchButtonsPressed = input.getCurrentNumberOfTouchButtonsPressed ();

        int touchCount = 0;

        if (touchPlatform && useNewInputSystem) {
            touchCount = InputAxisWrapper.getNewTouchCount ();
        } else {
            touchCount = InputAxisWrapper.getTouchCount ();
        }

        if (!touchPlatform) {
            touchCount++;

            if (input.GetMouseButtonDown (1)) {
                secondaryMouseTouchActive = !secondaryMouseTouchActive;

                if (secondaryMouseTouchActive) {
                    secondTouch = InputAxisWrapper.convertMouseIntoFinger ();

                    secondTouchPosition = secondTouch.position;
                } else {
                    firstMouseTouchActive = false;
                }

                if (checkIfPressInsideRectTransform) {
                    if (!IsScreenPositionInsideRect (secondTouchPosition, pressZoneRectTransform)) {
                        secondaryMouseTouchActive = false;

                        initialDistance = 0;

                        if (showDebugPrint) {
                            print ("first press out of the rect transform zone, cancelling");
                        }
                    }
                }

                return;
            }

            if (secondaryMouseTouchActive) {
                touchCount++;
            }
        }

        if (touchCount >= 2) {
            if (!touchPlatform) {
                if (input.GetMouseButtonDown (0)) {
                    firstMouseTouchActive = true;

                    if (checkIfPressInsideRectTransform) {
                        firstTouch = InputAxisWrapper.convertMouseIntoFinger ();

                        if (!IsScreenPositionInsideRect (firstTouch.position, pressZoneRectTransform)) {
                            firstMouseTouchActive = false;

                            if (showDebugPrint) {
                                print ("second press out of the rect transform zone, cancelling");
                            }

                            initialDistance = 0;

                            return;
                        }
                    }
                }

                if (input.GetMouseButtonUp (0)) {
                    firstMouseTouchActive = false;
                }

                if (firstMouseTouchActive) {
                    firstTouch = InputAxisWrapper.convertMouseIntoFinger ();
                }
            } else {
                if (!useNewInputSystem) {
                    firstTouch = InputAxisWrapper.getTouch (0);

                    secondTouch = InputAxisWrapper.getTouch (1);
                }
            }

            if (touchPlatform && useNewInputSystem) {
                firstTouchPressType = InputAxisWrapper.GetTouchPressType (0);

                firstTouchPosition = InputAxisWrapper.GetTouchPosition (0);


                secondTouchPressType = InputAxisWrapper.GetTouchPressType (1);

                secondTouchPosition = InputAxisWrapper.GetTouchPosition (1);
            } else {
                firstTouchPressType = InputAxisWrapper.GetTouchPressType (firstTouch);

                firstTouchPosition = firstTouch.position;

                if (touchPlatform) {
                    secondTouchPressType = InputAxisWrapper.GetTouchPressType (secondTouch);

                    secondTouchPosition = secondTouch.position;
                }
            }

            if (checkIfPressInsideRectTransform && touchPlatform) {
                if (firstTouchPressType == InputAxisWrapper.TouchPressType.Began) {
                    if (IsScreenPositionInsideRect (firstTouchPosition, pressZoneRectTransform)) {
                        firstTouchInsidePressZoneResult = true;
                    } else {
                        firstTouchInsidePressZoneResult = false;

                        if (showDebugPrint) {
                            print ("first press out of the rect transform zone, cancelling");
                        }
                    }
                }

                if (firstTouchPressType == InputAxisWrapper.TouchPressType.Ended) {
                    firstTouchInsidePressZoneResult = false;
                }

                if (secondTouchPressType == InputAxisWrapper.TouchPressType.Began) {
                    if (IsScreenPositionInsideRect (secondTouchPosition, pressZoneRectTransform)) {
                        secondTouchInsidePressZoneResult = true;
                    } else {
                        secondTouchInsidePressZoneResult = false;

                        if (showDebugPrint) {
                            print ("second press out of the rect transform zone, cancelling");
                        }

                    }
                }

                if (secondTouchPressType == InputAxisWrapper.TouchPressType.Ended) {
                    secondTouchInsidePressZoneResult = false;
                }

                if (!firstTouchInsidePressZoneResult || !secondTouchInsidePressZoneResult) {
                    initialDistance = 0;

                    return;
                }
            }

            if (firstTouchPressType == InputAxisWrapper.TouchPressType.Began) {
                if (initialDistance == 0) {
                    initialDistance = GKC_Utils.distance (firstTouchPosition, secondTouchPosition);

                    distanceDifference = 0;
                }
            }

            if (currentNumberOfTouchButtonsPressed == 0 && !playerInput.areTouchJoysticksPressed ()) {

                if (touchPlatform || (firstMouseTouchActive && secondaryMouseTouchActive)) {

                    bool checkDistanceResult = false;

                    if (touchPlatform) {
                        if (firstTouchPressType == InputAxisWrapper.TouchPressType.Moved ||
                            secondTouchPressType == InputAxisWrapper.TouchPressType.Moved) {
                            checkDistanceResult = true;
                        }
                    } else {
                        if (firstTouchPressType == InputAxisWrapper.TouchPressType.Moved || firstTouchPressType == InputAxisWrapper.TouchPressType.Stationary ||
                            secondTouchPressType == InputAxisWrapper.TouchPressType.Moved) {
                            checkDistanceResult = true;
                        }
                    }

                    if (checkDistanceResult) {

                        currentDistance = GKC_Utils.distance (firstTouchPosition, secondTouchPosition);

                        distanceDifference = currentDistance - initialDistance;

                        if (Mathf.Abs (distanceDifference) > minDistanceToUpdatePinch) {

                            initialDistance = currentDistance;

                            if (distanceDifference < 0) {
                                eventOnPinchIn.Invoke ();
                            } else {
                                eventOnPinchOut.Invoke ();
                            }
                        }
                    }
                }
            }

        } else {
            if (initialDistance != 0) {
                initialDistance = 0;
            }
        }
    }

    bool IsScreenPositionInsideRect (Vector2 screenPosition, RectTransform rectTransform, Camera uiCamera = null)
    {
        return RectTransformUtility.RectangleContainsScreenPoint (
            rectTransform,
            screenPosition,
            uiCamera
        );
    }
}