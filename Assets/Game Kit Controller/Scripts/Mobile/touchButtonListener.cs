using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using static InputAxisWrapper;

[System.Serializable]
//System to use touch buttons to check if the button is being pressing, holding or released
public class touchButtonListener : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
{
    [Header ("Main Settings")]
    [Space]

    public bool changeColorOnPress;
    public Color releaseColor;
    public Color pressColor;
    public float colorChangeSpeed;

    public bool useCurrentColorForRelease;

    [Space]
    [Header ("Debug")]
    [Space]

    public bool pressedDown = false;
    public bool pressedUp = false;
    public bool pressed;

    public bool buttonColorVisible = true;

    public int currentFingerId;

    [Space]
    [Header ("Components")]
    [Space]

    public RawImage buttonIcon;

    bool buttonIconLocated;


    Color currentColorForRelease;

    Coroutine colorTransition;

    Touch currentTouch;

    int touchCount;

    bool touchPlatform;

    int currentTouchIndex;

    bool useNewInputSystem;

    InputAxisWrapper.TouchPressType touchPressType;


    void Start ()
    {
        if (changeColorOnPress) {
            checkbuttonIconLocated ();

            if (useCurrentColorForRelease && buttonIconLocated) {
                currentColorForRelease = buttonIcon.color;
            }
        }

        touchPlatform = InputAxisWrapper.checkTouchPlatform ();

        useNewInputSystem = InputAxisWrapper.isUseNewInputSystem ();
    }

    //if you press the button
    public void OnPointerDown (PointerEventData eventData)
    {
        pressedDown = true;
        pressed = true;

        if (changeColorOnPress && buttonIconLocated) {
            changeColor (true);
        }

        StartCoroutine (disableDown ());

        //		print (eventData.pointerId);

        //checkTouchID ();
    }

    public void checkTouchID ()
    {
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

            if (touchPressType == InputAxisWrapper.TouchPressType.Began) {
                currentFingerId = currentTouch.fingerId;
            }
        }
    }

    public int getCurrentFingerId ()
    {
        return currentFingerId;
    }

    //if you release the button
    public void OnPointerUp (PointerEventData eventData)
    {
        pressedUp = true;

        pressed = false;

        if (changeColorOnPress && buttonIconLocated) {
            changeColor (false);
        }

        StartCoroutine (disableUp ());
    }

    //disable the booleans parameters after press them
    IEnumerator disableDown ()
    {
        WaitForSeconds delay = new WaitForSeconds (0.001f);

        yield return delay;

        pressedDown = false;
    }

    IEnumerator disableUp ()
    {
        WaitForSeconds delay = new WaitForSeconds (0.001f);

        yield return delay;

        pressedUp = false;
    }

    //if the button is disabled, reset the button
    void OnDisable ()
    {
        resetPressedState ();
    }

    void resetPressedState ()
    {
        pressedDown = false;
        pressedUp = false;
        pressed = false;
    }

    void changeColor (bool state)
    {
        if (!buttonColorVisible) {
            return;
        }

        checkbuttonIconLocated ();

        if (colorTransition != null) {
            StopCoroutine (colorTransition);
        }

        colorTransition = StartCoroutine (changeColorCoroutine (state));
    }

    IEnumerator changeColorCoroutine (bool state)
    {
        if (buttonIconLocated) {
            Color targetColor = Color.white;

            if (state) {
                targetColor = pressColor;
            } else {
                if (useCurrentColorForRelease) {
                    targetColor = currentColorForRelease;
                } else {
                    targetColor = releaseColor;
                }
            }

            while (buttonIcon.color != targetColor) {
                buttonIcon.color = Color.Lerp (buttonIcon.color, targetColor, Time.deltaTime * colorChangeSpeed);

                yield return null;
            }
        }

        yield return null;
    }

    public void setButtonColorVisibleState (bool state)
    {
        checkbuttonIconLocated ();

        if (buttonIconLocated) {
            buttonColorVisible = state;

            Color targetColor = buttonIcon.color;

            if (buttonColorVisible) {
                targetColor.a = releaseColor.a;
            } else {
                targetColor.a = 0;
            }

            buttonIcon.color = targetColor;
        }
    }

    public void resetButtonState ()
    {
        if (pressed || pressedDown || pressedUp) {
            resetPressedState ();

            if (buttonColorVisible) {
                Color targetColor = buttonIcon.color;

                targetColor.a = releaseColor.a;

                buttonIcon.color = targetColor;
            }
        }
    }

    void checkbuttonIconLocated ()
    {
        if (!buttonIconLocated) {
            buttonIconLocated = buttonIcon != null;
        }
    }

    public bool isPressedDown ()
    {
        return pressedDown;
    }

    public bool isPressedUp ()
    {
        return pressedUp;
    }

    public bool isPressed ()
    {
        return pressed;
    }

    //EDITOR FUNCTIONS
    public void setButtonIconComponent ()
    {
        buttonIcon = GetComponent<RawImage> ();

        updateComponent ();
    }

    void updateComponent ()
    {
        GKC_Utils.updateComponent (this);
    }
}