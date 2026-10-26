using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class camera2_5dSplineZoneOptions : MonoBehaviour
{
    [Header ("Main Settings")]
    [Space]

    public bool camera2_5dSplineZoneEnabled = true;

    public bool setFollowPlayerRotationDirectionPausedState;

    public bool setFollowPlayerRotationDirectionPausedValue;

    [Space]

    public bool adjustLockedCameraRotationToTransform;

    public float adjustLockedCameraRotationSpeed = 10;

    public Transform transformToAdjustLockedCameraRotation;

    [Space]
    [Header ("Debug")]
    [Space]

    public bool showDebugPrint;


    GameObject currentPlayer;

    playerCamera currentPlayerCamera;


    public void setCurrentPlayerAndActivateCameraState (GameObject newPlayer)
    {
        setCurrentPlayer (newPlayer);

        setCameraStateOnPlayer ();
    }

    public void setCurrentPlayer (GameObject newPlayer)
    {
        if (newPlayer == null) {
            print ("WARNING: the zone system is trying to assign an empty player, make sure the system is properly configured");

            return;
        }

        currentPlayer = newPlayer;

        playerComponentsManager mainPlayerComponentsManager = currentPlayer.GetComponent<playerComponentsManager> ();

        if (mainPlayerComponentsManager != null) {
            currentPlayerCamera = mainPlayerComponentsManager.getPlayerCamera ();
        }
    }

    public void setCameraStateOnPlayer ()
    {
        if (!camera2_5dSplineZoneEnabled) {
            return;
        }

        if (currentPlayerCamera == null) {
            return;
        }

        if (setFollowPlayerRotationDirectionPausedState) {
            currentPlayerCamera.setFollowPlayerRotationDirectionEnabledOnLockedCameraPausedState (setFollowPlayerRotationDirectionPausedValue);
        }

        if (adjustLockedCameraRotationToTransform) {
            currentPlayerCamera.setLockedMainCameraTransformRotationSmoothly (transformToAdjustLockedCameraRotation.eulerAngles, adjustLockedCameraRotationSpeed);
        }

        if (showDebugPrint) {
            print ("player detected, setting camera state");
        }
    }
}
