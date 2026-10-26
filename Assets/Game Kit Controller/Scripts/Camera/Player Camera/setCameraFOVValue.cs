using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class setCameraFOVValue : MonoBehaviour
{
    [Header ("Main Settings")]
    [Space]

    public bool cameraFOVChangeEnabled = true;

    public float changeFOVAmount = 0.4f;

    public Vector2 FOVClampValue;

    [Space]

    public bool getMainPlayerCameraOnScene;

    [Space]
    [Header ("Debug")]
    [Space]

    public bool mainCameraLocated;

    public bool showDebugPrint;

    [Space]
    [Header ("Components")]
    [Space]

    public Camera mainCamera;


    public void enableOrDisableCameraFOVChange (bool state)
    {
        cameraFOVChangeEnabled = state;
    }

    public void increaseFov ()
    {
        if (!cameraFOVChangeEnabled) {
            return;
        }

        changeFOV (1);
    }

    public void decreaseFOV ()
    {
        if (!cameraFOVChangeEnabled) {
            return;
        }

        changeFOV (-1);
    }

    void changeFOV (float changeDirection)
    {
        checkMainCameraLocated ();

        if (!mainCameraLocated) {
            return;
        }

        mainCamera.fieldOfView += (changeFOVAmount * changeDirection);

        if (showDebugPrint) {
            print ("setting FOV value to " + mainCamera.fieldOfView);
        }

        clampFOVValue ();
    }

    public void setCameraFOV (float newValue)
    {
        checkMainCameraLocated ();

        if (!mainCameraLocated) {
            return;
        }

        mainCamera.fieldOfView = newValue;

        clampFOVValue ();
    }

    void clampFOVValue ()
    {
        mainCamera.fieldOfView = Mathf.Clamp (mainCamera.fieldOfView, FOVClampValue.x, FOVClampValue.y);
    }

    public void setFOVClampValueX (float newValue)
    {
        FOVClampValue.x = newValue;
    }

    public void setFOVClampValueY (float newValue)
    {
        FOVClampValue.y = newValue;
    }

    void checkMainCameraLocated ()
    {
        if (!mainCameraLocated) {
            mainCameraLocated = mainCamera != null;

            if (!mainCameraLocated && getMainPlayerCameraOnScene) {
                playerCamera mainPlayerCamera = GKC_Utils.findMainPlayerCameraOnScene ();

                if (mainPlayerCamera != null) {
                    mainCamera = mainPlayerCamera.getMainCamera ();
                } else {
                    mainCamera = Camera.main;
                }

                mainCameraLocated = mainCamera != null;
            }
        }
    }
}
