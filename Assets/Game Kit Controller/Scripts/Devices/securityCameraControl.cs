using UnityEngine;
using System.Collections;
using UnityEngine.Events;

public class securityCameraControl : MonoBehaviour
{
    [Header ("Main Settings")]
    [Space]

    public LayerMask layer;
    public float dragDistance = 1;
    public bool controlEnabled;
    public Vector2 clampTiltX;
    public Vector2 clampTiltZ;

    [Space]
    [Header ("Debug")]
    [Space]

    public bool securityCameraActive;

    [Space]
    [Header ("Events Settings")]
    [Space]

    public bool useEventOnEnableDisableObject;
    public UnityEvent useEventOnEnableObject;
    public UnityEvent useEventOnDisablebject;

    [Space]
    [Header ("Components")]
    [Space]

    public GameObject securityCameraGameObject;
    public GameObject joystick;
    public Transform joystickAxisX;
    public Transform joystickAxisZ;
    public GameObject zoomOutButton;
    public GameObject zoomInButton;

    public electronicDevice deviceManager;
    public securityCamera securityCameraManager;


    Touch currentTouch;

    InputAxisWrapper.TouchPressType touchPressType;

    bool useNewInputSystem;

    Vector2 currentTouchPosition;

    int i;
    bool touchPlatform;

    bool touching;
    RaycastHit hit;
    Vector2 currentAxisValue;
    Vector3 beginTouchPosition;

    Vector2 lookAngle;
    bool zoomIn;
    bool zoomOut;
    Coroutine inCoroutine;
    Coroutine outCoroutine;
    Camera mainCamera;

    playerComponentsManager mainPlayerComponentsManager;
    playerInputManager playerInput;
    usingDevicesSystem mainUsingDevicesSystem;
    playerController mainPlayerController;

    bool playerInputLocated;


    GameObject currentPlayer;

    bool securityCameraManagerLocated;

    float lastTimeDeviceUsed;

    Coroutine mainCoroutine;


    public void stopUpdateCoroutine ()
    {
        if (mainCoroutine != null) {
            StopCoroutine (mainCoroutine);
        }
    }

    IEnumerator updateCoroutine ()
    {
        var waitTime = new WaitForSecondsRealtime (0.00001f);

        while (true) {
            updateState ();

            yield return waitTime;
        }
    }

    void updateState ()
    {
        //if the security camera control is being used
        if (controlEnabled) {

            //check for touch input from the mouse or the finger
            int touchCount = 0;

            if (touchPlatform && useNewInputSystem) {
                touchCount = InputAxisWrapper.getNewTouchCount ();
            } else {
                touchCount = InputAxisWrapper.getTouchCount ();
            }

            bool usingGamepadOnTouchPlatform = false;

            if (touchPlatform) {
                if (touchCount == 0 && playerInput.isUsingGamepad ()) {
                    touchCount++;

                    usingGamepadOnTouchPlatform = true;
                }
            } else {
                touchCount++;
            }


            for (i = 0; i < touchCount; i++) {
                if (!touchPlatform) {
                    if (!useNewInputSystem) {
                        currentTouch = InputAxisWrapper.convertMouseIntoFinger ();
                    }
                } else {
                    if (!useNewInputSystem) {
                        currentTouch = InputAxisWrapper.getTouch (i);
                    }
                }

                if (touchPlatform && useNewInputSystem) {
                    touchPressType = InputAxisWrapper.GetTouchPressType (i);

                    currentTouchPosition = InputAxisWrapper.GetTouchPosition (i);

                    if (usingGamepadOnTouchPlatform) {
                        touchPressType = playerInput.GetTouchPressType ();

                        currentTouchPosition = playerInput.getCurrentTouchPosition ();
                    }
                } else {
                    if (useNewInputSystem) {
                        touchPressType = playerInput.GetTouchPressType ();

                        currentTouchPosition = playerInput.getCurrentTouchPosition ();
                    } else {
                        touchPressType = InputAxisWrapper.GetTouchPressType (currentTouch);

                        currentTouchPosition = currentTouch.position;
                    }
                }

                //in the touch begin phase
                if (touchPressType == InputAxisWrapper.TouchPressType.Began && !touching) {
                    //check with a ray
                    Ray ray = mainCamera.ScreenPointToRay (currentTouchPosition);

                    if (Physics.Raycast (ray, out hit, Mathf.Infinity, layer)) {
                        //the pressed position is the joystick of the camera
                        if (hit.collider.gameObject == joystick) {
                            touching = true;
                            //store the initial press position
                            beginTouchPosition = new Vector3 (currentTouchPosition.x, currentTouchPosition.y, 0);
                        }

                        //the pressed position is the zoom in button
                        if (hit.collider.gameObject == zoomInButton) {
                            zoomIn = true;
                            //move the button down
                            checkButtonTranslation (true, zoomInButton, true);
                        }

                        //the pressed position is the zoom out button
                        if (hit.collider.gameObject == zoomOutButton) {
                            zoomOut = true;
                            //move the button down
                            checkButtonTranslation (false, zoomOutButton, true);
                        }
                    }
                }

                //the current touch press is being moved
                if ((touchPressType == InputAxisWrapper.TouchPressType.Moved || touchPressType == InputAxisWrapper.TouchPressType.Stationary) && touching) {
                    //like in the joystick script, get the amount of movement between the inital press position and the current touch position
                    Vector3 globalTouchPosition = new Vector3 (currentTouchPosition.x, currentTouchPosition.y, 0);
                    Vector3 differenceVector = globalTouchPosition - beginTouchPosition;

                    if (differenceVector.sqrMagnitude > dragDistance * dragDistance) {
                        differenceVector.Normalize ();
                    }

                    //get the axis from the touch movement
                    currentAxisValue = differenceVector;

                    rotateCamera (currentAxisValue);
                }

                //if the touch ends, reset the rotation of the joystick, the current axis values and the zoom buttons positions
                if (touchPressType == InputAxisWrapper.TouchPressType.Ended) {
                    touching = false;

                    currentAxisValue = Vector2.zero;

                    lookAngle = Vector2.zero;

                    StartCoroutine (resetJoystick ());

                    if (zoomIn) {
                        zoomIn = false;
                        checkButtonTranslation (true, zoomInButton, false);
                    }

                    if (zoomOut) {
                        zoomOut = false;
                        checkButtonTranslation (false, zoomOutButton, false);
                    }
                }
            }

            if (!touching && playerInputLocated) {
                bool canUseInputResult = true;

                if (lastTimeDeviceUsed > 0) {
                    if (Time.time > lastTimeDeviceUsed + 0.3f) {
                        if (mainUsingDevicesSystem != null) {
                            if (!mainUsingDevicesSystem.anyDeviceDetected ()) {
                                canUseInputResult = false;
                            }
                        }

                        if (mainPlayerController != null) {
                            if (!mainPlayerController.isUsingDevice ()) {
                                canUseInputResult = false;
                            }
                        }
                    }
                }

                if (canUseInputResult) {
                    currentAxisValue = playerInput.getPlayerMovementAxis ();

                    rotateCamera (currentAxisValue);
                }
            }

            if (securityCameraManagerLocated) {
                //send the current axis value to the security camera to rotate it
                securityCameraManager.getLookValue (currentAxisValue);
            }
        }

        if (securityCameraManagerLocated) {
            //set the zoom in the camera
            if (zoomIn) {
                securityCameraManager.setZoom (-1);
            }

            if (zoomOut) {
                securityCameraManager.setZoom (1);
            }
        }
    }

    public void rotateCamera (Vector2 values)
    {
        if (securityCameraManagerLocated) {
            //rotate the camera joystick too
            lookAngle.x += values.x * securityCameraManager.sensitivity;
            lookAngle.y += values.y * securityCameraManager.sensitivity;
        }

        //clamp these values to limit the joystick rotation
        lookAngle.y = Mathf.Clamp (lookAngle.y, -clampTiltX.x, clampTiltX.y);
        lookAngle.x = Mathf.Clamp (lookAngle.x, -clampTiltZ.x, clampTiltZ.y);

        //apply the rotation to every component in the X and Z axis
        joystickAxisX.transform.localRotation = Quaternion.Euler (0, 0, -lookAngle.x);
        joystickAxisZ.transform.localRotation = Quaternion.Euler (lookAngle.y, 0, 0);
    }

    //reset the joystick rotation
    IEnumerator resetJoystick ()
    {
        for (float t = 0; t < 1;) {
            t += Time.deltaTime * 3;

            joystickAxisX.transform.localRotation = Quaternion.Slerp (joystickAxisX.transform.localRotation, Quaternion.identity, t);
            joystickAxisZ.transform.localRotation = Quaternion.Slerp (joystickAxisZ.transform.localRotation, Quaternion.identity, t);

            yield return null;
        }
    }

    public void setSecurityCameraActiveState (bool state)
    {
        securityCameraActive = state;

        if (securityCameraActive) {
            if (gameObject.activeInHierarchy) {
                mainCoroutine = StartCoroutine (updateCoroutine ());
            }
        } else {
            stopUpdateCoroutine ();
        }
    }

    public void activateSecurityControl ()
    {
        setSecurityCameraControlState (true);
    }

    public void deactivateSecurityCameraControl ()
    {
        setSecurityCameraControlState (false);
    }

    public void setSecurityCameraControlState (bool state)
    {
        getMainComponents ();

        touchPlatform = InputAxisWrapper.checkTouchPlatform ();

        useNewInputSystem = InputAxisWrapper.isUseNewInputSystem ();

        controlEnabled = state;

        if (controlEnabled) {
            currentPlayer = deviceManager.getCurrentPlayer ();

            if (currentPlayer != null) {
                mainPlayerComponentsManager = currentPlayer.GetComponent<playerComponentsManager> ();
                mainCamera = mainPlayerComponentsManager.getPlayerCamera ().getMainCamera ();
                playerInput = mainPlayerComponentsManager.getPlayerInputManager ();

                playerInputLocated = playerInput != null;

                mainUsingDevicesSystem = mainPlayerComponentsManager.getUsingDevicesSystem ();

                mainPlayerController = mainPlayerComponentsManager.getPlayerController ();
            }
        } else {
            if (gameObject.activeInHierarchy) {
                StartCoroutine (resetJoystick ());
            }
        }

        lastTimeDeviceUsed = Time.time;

        securityCameraManagerLocated = securityCameraManager != null;

        if (securityCameraManagerLocated) {
            securityCameraManager.changeCameraState (controlEnabled);
        }
    }

    void getMainComponents ()
    {
        if (securityCameraManager == null && securityCameraGameObject != null) {
            //get the security camera component of the camera
            securityCamera currentSecurityCamera = securityCameraGameObject.GetComponent<securityCamera> ();

            if (currentSecurityCamera != null) {
                securityCameraManager = currentSecurityCamera;
            }
        }

        securityCameraManagerLocated = securityCameraManager != null;
    }

    void checkEventOnEnableObjectStateChange (bool state)
    {
        if (useEventOnEnableDisableObject) {
            if (state) {
                useEventOnEnableObject.Invoke ();

            } else {
                useEventOnDisablebject.Invoke ();
            }
        }
    }

    //if this control is enabled, enable the camera component too
    void OnEnable ()
    {
        setSecurityCameraControlState (true);

        checkEventOnEnableObjectStateChange (true);
    }

    //else, this control is disabled, so disable the camera component too
    void OnDisable ()
    {
        setSecurityCameraControlState (false);

        checkEventOnEnableObjectStateChange (false);
    }

    //if a zoom button is pressed, check the coroutine, stop it and play it again
    void checkButtonTranslation (bool state, GameObject button, bool value)
    {
        if (state) {
            if (inCoroutine != null) {
                StopCoroutine (inCoroutine);
            }

            inCoroutine = StartCoroutine (pressButton (button, value));
        } else {
            if (outCoroutine != null) {
                StopCoroutine (outCoroutine);
            }

            outCoroutine = StartCoroutine (pressButton (button, value));
        }
    }

    //move the zoom buttons up and down when they are pressed, instead of using animations
    IEnumerator pressButton (GameObject button, bool value)
    {
        int mult = 1;

        if (value) {
            mult = -1;
        }

        Vector3 buttonPos = button.transform.localPosition;
        Vector3 newButtonPos = button.transform.localPosition + Vector3.up * (mult * 0.03f);

        for (float t = 0; t < 1;) {
            t += Time.deltaTime * 3;

            button.transform.localPosition = Vector3.MoveTowards (buttonPos, newButtonPos, t);

            yield return null;
        }
    }
}