using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class simpleStrafeAndAimState : MonoBehaviour
{
    [Header ("Main Settings")]
    [Space]

    public bool simpleStrafeAndAimEnabled = true;

    [Space]
    [Header ("Movement Settings")]
    [Space]

    public bool useStrafeMode;
    public int strafeIDUsed;
    public bool setPreviousStrafeModeOnDisableSystem;

    public bool activateStrafeModeOnLockOnTargetActive;

    public bool toggleStrafeModeIfRunningActive;

    public bool setNewCrouchID;
    public int crouchIDUsed;

    [Space]
    [Header ("Aim Settings")]
    [Space]

    public bool setAimStateEnabled;

    [Space]
    [Header ("Third Person Camera State Settings")]
    [Space]

    public bool setNewCameraStateOnThirdPerson;

    public string newCameraStateOnThirdPerson;

    [Space]
    [Header ("Debug")]
    [Space]

    public bool simpleStrafeAndAimActive;

    public bool previousStrafeMode;
    public int previousStrafeID = -1;

    [Space]
    [Header ("Components")]
    [Space]

    public playerController mainPlayerController;
    public playerCamera mainPlayerCamera;

    string previousCameraState;


    public void setSimpleStrafeAndAimState (bool state)
    {
        if (!simpleStrafeAndAimEnabled) {
            return;
        }

        if (simpleStrafeAndAimActive == state) {
            return;
        }

        if (mainPlayerCamera.isFullBodyAwarenessActive ()) {
            return;
        }

        simpleStrafeAndAimActive = state;

        if (simpleStrafeAndAimActive) {
            activateState ();
        } else {
            deactivateState ();
        }
    }


    public void activateState ()
    {
        previousStrafeMode = mainPlayerController.isStrafeModeActive ();

        if (previousStrafeID == -1) {
            previousStrafeID = mainPlayerController.getCurrentStrafeIDValue ();
        }

        if (useStrafeMode) {
            mainPlayerController.activateOrDeactivateStrafeMode (true);

            mainPlayerController.setLookOnlyIfMovingState (false);

        } else {

            previousStrafeMode = false;

            previousStrafeID = -1;

            mainPlayerController.activateOrDeactivateStrafeMode (false);
        }

        if (toggleStrafeModeIfRunningActive) {
            mainPlayerController.setDisableStrafeModeExternallyIfIncreaseWalkSpeedActiveState (true);
        }

        mainPlayerController.setCurrentStrafeIDValue (strafeIDUsed);

        if (setNewCrouchID) {
            mainPlayerController.setCurrentCrouchIDValue (crouchIDUsed);
        }

        if (setAimStateEnabled) {
            mainPlayerCamera.setManualAimStateOnLockedCamera (true);
        }
    }

    public void deactivateState ()
    {
        if (setPreviousStrafeModeOnDisableSystem) {
            mainPlayerController.activateOrDeactivateStrafeMode (previousStrafeMode);
        } else {
            mainPlayerController.setOriginalLookAlwaysInCameraDirectionState ();
        }

        mainPlayerController.setOriginalLookOnlyIfMovingState ();

        if (previousStrafeID != -1) {
            mainPlayerController.setCurrentStrafeIDValue (previousStrafeID);
        }

        if (toggleStrafeModeIfRunningActive) {
            mainPlayerController.setDisableStrafeModeExternallyIfIncreaseWalkSpeedActiveState (false);
        }

        previousStrafeMode = false;

        previousStrafeID = -1;

        if (setNewCrouchID) {
            mainPlayerController.setCurrentCrouchIDValue (0);
        }

        if (setAimStateEnabled) {
            mainPlayerCamera.setManualAimStateOnLockedCamera (false);
        }
    }

    public void checkLookAtTargetActiveState ()
    {
        if (simpleStrafeAndAimActive) {
            bool lookingAtTarget = mainPlayerController.isPlayerLookingAtTarget ();

            if (lookingAtTarget) {
                if (activateStrafeModeOnLockOnTargetActive) {
                    mainPlayerController.activateOrDeactivateStrafeMode (true);

                }
            } else {
                if (activateStrafeModeOnLockOnTargetActive) {
                    mainPlayerController.activateOrDeactivateStrafeMode (false);

                }
            }
        }
    }

    public void checkLookAtTargetDeactivateState ()
    {
        if (simpleStrafeAndAimActive && activateStrafeModeOnLockOnTargetActive) {
            mainPlayerController.activateOrDeactivateStrafeMode (false);
        }
    }

    void checkCameraState (bool state)
    {
        if (setNewCameraStateOnThirdPerson) {
            if (state) {
                previousCameraState = mainPlayerCamera.getCurrentStateName ();

                mainPlayerCamera.setCameraStateOnlyOnThirdPerson (newCameraStateOnThirdPerson);
            } else {

                if (previousCameraState != "") {
                    if (previousCameraState != newCameraStateOnThirdPerson) {
                        mainPlayerCamera.setCameraStateOnlyOnThirdPerson (previousCameraState);
                    }

                    previousCameraState = "";
                }
            }
        }
    }
}