using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class wolfCharacterController : vehicleController
{
    [Header ("Custom Settings")]
    [Space]

    public customCharacterControllerBase mainCustomCharacterControllerBase;

    public playerController mainPlayerController;

    public Transform vehicleTransform;

    public Transform vehicleCameraTransform;

    [Space]
    [Header ("Other Options")]
    [Space]

    public bool rotateCharacterModelToSurfaceEnabled;

    public float maxCharacterXRotationToSurface = 40;
    public float maxCharacterZRotationToSurface = 40;

    public float characterRotationToSurfaceSpeed = 10;

    public float characterPositionAdjustmentToSurfaceSpeed = 10;

    public float characterRotationToSurfaceRaycastDistance = 2.5f;

    [Space]

    public float minSurfaceRotation = 5;

    public bool useReverseRotation;

    public bool ignoreLocalZAxisRotationEnabled;

    public bool adjustModelPositionToSurfaceEnabled = true;

    [Space]

    public Transform characterPivotTransform;

    public LayerMask surfaceRotationLayermask;


    [Space]
    [Header ("Debug")]
    [Space]

    public float turnAmount;

    public float forwardAmount;

    public Transform currentCameraPivotTransform;
    public Transform currentMainCameraTranform;

    public bool useForwardDirectionForCameraDirection;

    public bool useRightDirectionForCameraDirection;

    public bool addExtraRotationPaused;


    bool playerUsingInput;

    Vector3 currentMoveInput;

    Vector3 currentForwardDirection;

    Vector3 currentRightDirection;


    bool surfaceFound;

    Vector3 targetPosition;

    Quaternion targetRotation;

    RaycastHit hit;

    bool characterPivotTransformLocated;

    Vector3 originalPivotTransformLocalEuler;

    Vector3 originalCharacterPivotTransformPosition;


    public override void Start ()
    {
        base.Start ();

        mainPlayerController.setCustomCharacterControllerActiveState (true, mainCustomCharacterControllerBase);

        currentCameraPivotTransform = vehicleCameraTransform;
        currentMainCameraTranform = vehicleCameraTransform;

        characterPivotTransformLocated = characterPivotTransform != null;

        if (characterPivotTransformLocated) {
            originalPivotTransformLocalEuler = characterPivotTransform.localEulerAngles;

            originalCharacterPivotTransformPosition = characterPivotTransform.localPosition;
        }
    }

    void Update ()
    {
        base.vehicleUpdate ();
    }

    void FixedUpdate ()
    {
        mainPlayerController.setCustomAxisValues (new Vector2 (horizontalAxis, verticalAxis));

        currentForwardDirection = currentCameraPivotTransform.forward;
        currentRightDirection = currentMainCameraTranform.right;

        currentMoveInput = verticalAxis * currentForwardDirection + horizontalAxis * currentRightDirection;

        if (currentMoveInput.magnitude > 1) {
            currentMoveInput.Normalize ();
        }

        Vector3 localMove = vehicleTransform.InverseTransformDirection (currentMoveInput);

        //get the amount of rotation added to the character mecanim
        if (currentMoveInput.magnitude > 0) {
            turnAmount = Mathf.Atan2 (localMove.x, localMove.z);
        } else {
            turnAmount = Mathf.Atan2 (0, 0);
        }

        forwardAmount = localMove.z;

        forwardAmount *= boostInput;

        forwardAmount = Mathf.Clamp (forwardAmount, -boostInput, boostInput);


        mainCustomCharacterControllerBase.updateForwardAmountInputValue (forwardAmount);

        if (addExtraRotationPaused && forwardAmount < 0.0001f && verticalAxis < 0) {
            turnAmount = 0;
        }

        mainCustomCharacterControllerBase.updateTurnAmountInputValue (turnAmount);

        playerUsingInput = isPlayerUsingInput ();

        mainCustomCharacterControllerBase.updatePlayerUsingInputValue (playerUsingInput);

        mainCustomCharacterControllerBase.updateCharacterControllerAnimator ();

        mainCustomCharacterControllerBase.updateCharacterControllerState ();

        if (rotateCharacterModelToSurfaceEnabled && characterPivotTransformLocated) {
            updateCharacterRotationToSurface ();
        }
    }

    void updateCharacterRotationToSurface ()
    {
        Vector3 raycastPosition = vehicleTransform.position + vehicleTransform.up;

        Vector3 raycastDirection = -vehicleTransform.up;

        float hitDistance = 0;

        surfaceFound = false;

        targetRotation = Quaternion.identity;

        targetPosition = originalCharacterPivotTransformPosition;

        if (Physics.Raycast (raycastPosition, raycastDirection, out hit, characterRotationToSurfaceRaycastDistance, surfaceRotationLayermask)) {
            hitDistance = hit.distance - 1;

            targetPosition.y -= hitDistance;

            surfaceFound = true;
        }

        if (surfaceFound) {
            Vector3 surfaceNormal = hit.normal;

            Vector3 forward = vehicleTransform.forward;

            Vector3 projectedForward = Vector3.ProjectOnPlane (forward, surfaceNormal).normalized;

            Quaternion targetWorldRot = Quaternion.LookRotation (projectedForward, surfaceNormal);

            Quaternion targetLocalRot = Quaternion.Inverse (vehicleTransform.rotation) * targetWorldRot;

            Vector3 localEuler = targetLocalRot.eulerAngles;

            float x = normalizeAngle (localEuler.x);
            float z = normalizeAngle (localEuler.z);

            x = Mathf.Clamp (x, -maxCharacterXRotationToSurface, maxCharacterXRotationToSurface);
            z = Mathf.Clamp (z, -maxCharacterZRotationToSurface, maxCharacterZRotationToSurface);

            if (useReverseRotation) {
                x = x * (-1);
                z = z * (-1);
            }

            if (minSurfaceRotation > 0) {
                if (Mathf.Abs (x) < minSurfaceRotation && Mathf.Abs (z) < minSurfaceRotation) {
                    x = 0;
                    z = 0;
                }
            }

            if (ignoreLocalZAxisRotationEnabled) {
                targetRotation = Quaternion.Euler (new Vector3 (x, originalPivotTransformLocalEuler.y, 0));
            } else {
                targetRotation = Quaternion.Euler (new Vector3 (x, originalPivotTransformLocalEuler.y, z));
            }
        } else {
            targetRotation = Quaternion.Euler (originalPivotTransformLocalEuler);
        }

        characterPivotTransform.localRotation = Quaternion.Lerp (characterPivotTransform.localRotation, targetRotation, Time.fixedDeltaTime * characterRotationToSurfaceSpeed);

        if (adjustModelPositionToSurfaceEnabled) {
            if (hitDistance != 0 || Mathf.Abs (characterPivotTransform.localPosition.x) > 0.02f) {
                characterPivotTransform.localPosition = Vector3.Lerp (characterPivotTransform.localPosition, targetPosition, Time.fixedDeltaTime * characterPositionAdjustmentToSurfaceSpeed);
            }
        }
    }

    //convert angle from 0 to 360 to a range of -180 180 degrees

    float normalizeAngle (float angle)
    {
        angle %= 360f;

        if (angle > 180f) {
            angle -= 360f;
        }

        return angle;
    }

    public void setCharacterPivotTransform (Transform newTransform)
    {
        characterPivotTransform = newTransform;

    }


    public override void updateMovingState ()
    {
        moving = verticalAxis != 0 || horizontalAxis != 0;
    }

    //if the vehicle is using the gravity control, set the state in this component
    public override void changeGravityControlUse (bool state)
    {
        base.changeGravityControlUse (state);


    }

    //the player is getting on or off from the vehicle, so
    public override void changeVehicleState ()
    {
        base.changeVehicleState ();


    }

    public override void checkVehicleStateOnPassengerGettingOnOrOff (bool state)
    {
        if (state) {
            if (moveInXAxisOn2_5d) {
                mainPlayerController.setAddExtraRotationPausedState (true);
            }
        } else {
            if (mainIKDrivingSystem.isVehicleEmpty ()) {
                mainPlayerController.setAddExtraRotationPausedState (false);
            }
        }
    }

    public override void setTurnOnState ()
    {

    }

    public override void setTurnOffState (bool previouslyTurnedOn)
    {
        base.setTurnOffState (previouslyTurnedOn);

        if (previouslyTurnedOn) {

        }
    }

    public override bool isDrivingActive ()
    {
        return driving;
    }

    public override void setEngineOnOrOffState ()
    {
        base.setEngineOnOrOffState ();


    }

    public override void turnOnOrOff (bool state, bool previouslyTurnedOn)
    {
        base.turnOnOrOff (state, previouslyTurnedOn);


    }

    //the vehicle has been destroyed, so disabled every component in it
    public override void disableVehicle ()
    {
        base.disableVehicle ();


    }

    //if the vehicle is using the boost, set the boost particles
    public override void usingBoosting ()
    {
        base.usingBoosting ();


    }

    public override void setNewMainCameraTransform (Transform newTransform)
    {
        mainPlayerController.setNewMainCameraTransform (newTransform);

        currentMainCameraTranform = newTransform;
    }

    public override void setNewPlayerCameraTransform (Transform newTransform)
    {
        mainPlayerController.setNewPlayerCameraTransform (newTransform);

        currentCameraPivotTransform = newTransform;
    }

    public override void setUseForwardDirectionForCameraDirectionState (bool state)
    {
        //		mainPlayerController.setUseForwardDirectionForCameraDirectionState (state);

        useForwardDirectionForCameraDirection = state;

        if (useForwardDirectionForCameraDirection) {
            currentCameraPivotTransform = vehicleTransform;
        } else {
            currentCameraPivotTransform = vehicleCameraTransform;
        }
    }

    public override void setUseRightDirectionForCameraDirectionState (bool state)
    {
        //		mainPlayerController.setUseRightDirectionForCameraDirectionState (state);

        useRightDirectionForCameraDirection = state;

        if (useRightDirectionForCameraDirection) {
            currentMainCameraTranform = vehicleTransform;
        } else {
            currentMainCameraTranform = vehicleCameraTransform;
        }
    }

    public override void setAddExtraRotationPausedState (bool state)
    {
        mainPlayerController.setAddExtraRotationPausedState (state);

        addExtraRotationPaused = state;
    }

    //CALL INPUT FUNCTIONS
    public override void inputJump ()
    {
        if (driving && !usingGravityControl && isTurnedOn && vehicleControllerSettings.canJump && mainPlayerController.isPlayerOnGround ()) {

            mainPlayerController.inputJump ();
        }
    }

    public override void inputHoldOrReleaseTurbo (bool holdingButton)
    {
        if (driving && !usingGravityControl && isTurnedOn) {
            //boost input
            if (holdingButton) {
                if (vehicleControllerSettings.canUseBoost) {
                    if (!usingBoost) {
                        usingBoost = true;

                        //set the camera move away action
                        mainVehicleCameraController.usingBoost (true, vehicleControllerSettings.boostCameraShakeStateName,
                            vehicleControllerSettings.useBoostCameraShake, vehicleControllerSettings.moveCameraAwayOnBoost);
                    }

                    mainPlayerController.inputStartToRun ();
                }
            } else {
                //stop boost input
                if (usingBoost) {
                    usingBoost = false;

                    //disable the camera move away action
                    mainVehicleCameraController.usingBoost (false, vehicleControllerSettings.boostCameraShakeStateName,
                        vehicleControllerSettings.useBoostCameraShake, vehicleControllerSettings.moveCameraAwayOnBoost);
                }
                //disable the boost particles

                usingBoosting ();

                boostInput = 1;

                mainPlayerController.inputStopToRun ();
            }
        }
    }

    public override void inputHoldOrReleaseBrake (bool holdingButton)
    {

    }

    public override void inputSetTurnOnState ()
    {
        if (driving && !usingGravityControl) {
            if (mainVehicleHUDManager.canSetTurnOnState) {
                setEngineOnOrOffState ();
            }
        }
    }
}
