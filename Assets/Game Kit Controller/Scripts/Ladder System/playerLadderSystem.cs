using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class playerLadderSystem : MonoBehaviour
{
    [Header ("Main Settings")]
    [Space]

    public float ladderMoovementSpeed = 5;
    public float ladderVerticalMovementAmount = 0.3f;
    public float ladderHorizontalMovementAmount = 0.1f;

    public float minAngleToInverseDirection = 100;

    public bool useAlwaysHorizontalMovementOnLadder;

    public bool useAlwaysLocalMovementDirection;

    public float minAngleVerticalDirection = 60;
    public float maxAngleVerticalDirection = 120;

    public LayerMask layerToCheckLadderEnd;

    public string climbLadderFootStepStateName = "Climb Ladders";

    [Space]
    [Header ("FBA Settings")]
    [Space]

    public bool canUseSimpleLaddersOnFBA;

    [Space]
    [Header ("Jump Settings")]
    [Space]

    public bool jumpOnThirdPersonEnabled;

    public bool jumpOnFirstPersonEnabled;

    public Vector3 impulseOnJump;

    public Vector3 impulseOnJump2_5d = new Vector3 (0, 15, -30);

    public float jumpRotationSpeedThirdPerson = 1;
    public float jumpRotationSpeedFirstPerson = 0.5f;

    [Space]
    [Header ("Other Settings")]
    [Space]

    public bool pauseInputOnUseLaddersEnabled;
    public List<playerActionSystem.inputToPauseOnActionIfo> customInputToPauseOnActionInfoList = new List<playerActionSystem.inputToPauseOnActionIfo> ();

    [Space]
    [Header ("Debug")]
    [Space]

    public bool showDebugPrint;

    public bool ladderLocated;

    public bool ladderFoundFirstPerson;

    public bool ladderFoundThirdPerson;

    public bool ladderEndDetected;
    public bool ladderStartDetected;

    [Space]

    public int movementDirection;
    public float ladderVerticalInput;
    public float ladderHorizontalInput;
    public float ladderAngle;
    public float ladderSignedAngle;

    public float currentVerticalInput;
    public float currentHorizontalInput;

    public Vector3 ladderMovementDirection;

    [Space]

    public bool movingOnLadder;
    public bool movingOnLadderPreviously;

    public Transform ladderDirectionTransform;
    public Transform ladderRaycastDirectionTransform;

    [Space]

    public bool movingOnLadderOnFBAActive;

    public bool changeViewToFBAActive;

    [Space]
    [Header ("Events Settings")]
    [Space]

    public bool useEventsOnThirdPerson;
    public UnityEvent eventOnLocatedLadderOnThirdPerson;
    public UnityEvent eventOnRemovedLadderOnThirdPerson;

    [Space]

    public bool useEventsOnJump;

    public UnityEvent eventOnJump;

    [Space]
    [Header ("Components")]
    [Space]

    public playerController playerControllerManager;
    public playerCamera playerCameraManager;
    public Transform playerCameraTransform;
    public Transform playerPivotTransform;
    public Rigidbody mainRigidbody;
    public playerWeaponsManager mainPlayerWeaponsManager;
    public meleeWeaponsGrabbedManager mainMeleeWeaponsGrabbedManager;
    public playerInputManager mainPlayerInputManager;


    bool useLadderHorizontalMovement;
    bool moveInLadderCenter;
    bool useLocalMovementDirection;

    ladderSystem currentLadderSystem;
    ladderSystem previousLadderSystem;

    float verticalInput;
    float horizontalInput;

    Vector3 currentPlayerPosition;

    Vector3 currentPositionOffset;
    Vector3 lastPosition;

    Coroutine jumpCoroutine;


    void FixedUpdate ()
    {
        if (ladderFoundFirstPerson) {

            verticalInput = playerControllerManager.getVerticalInput ();
            horizontalInput = playerControllerManager.getHorizontalInput ();

            movementDirection = 1;

            ladderAngle = Vector3.Angle (playerCameraTransform.forward, ladderDirectionTransform.forward);

            if (ladderAngle > minAngleToInverseDirection) {
                movementDirection = (-1);
            }

            if (useLocalMovementDirection || useAlwaysLocalMovementDirection) {

                ladderSignedAngle = Vector3.SignedAngle (playerCameraTransform.forward, ladderDirectionTransform.forward, playerCameraTransform.up);

                if (ladderAngle < minAngleVerticalDirection || ladderAngle > maxAngleVerticalDirection) {
                    currentVerticalInput = verticalInput;
                    currentHorizontalInput = horizontalInput;
                } else {
                    if (ladderSignedAngle < 0) {
                        movementDirection = (-1);
                    } else {
                        movementDirection = 1;
                    }

                    currentVerticalInput = horizontalInput;
                    currentHorizontalInput = -verticalInput;
                }
            } else {
                currentVerticalInput = verticalInput;
                currentHorizontalInput = horizontalInput;
            }

            ladderVerticalInput = currentVerticalInput * movementDirection;

            ladderMovementDirection = Vector3.zero;

            currentPlayerPosition = mainRigidbody.position;

            if (moveInLadderCenter) {
                //if (useLadderHorizontalMovement || useAlwaysHorizontalMovementOnLadder) {
                if (Mathf.Abs (currentHorizontalInput) < 0.01f) {
                    currentPlayerPosition = new Vector3 (ladderDirectionTransform.position.x, mainRigidbody.position.y, ladderDirectionTransform.position.z);
                }
                //}
            }

            ladderMovementDirection += currentPlayerPosition + ladderDirectionTransform.up * (ladderVerticalMovementAmount * ladderVerticalInput);

            ladderEndDetected = !Physics.Raycast (mainRigidbody.position, ladderRaycastDirectionTransform.forward, 2, layerToCheckLadderEnd);

            ladderStartDetected = Physics.Raycast (mainRigidbody.position + playerCameraTransform.up * 0.1f,
                -playerCameraTransform.up, 0.13f, layerToCheckLadderEnd);

            bool moveHorizontallyResult = false;

            if (ladderEndDetected || (ladderStartDetected && ladderVerticalInput < 0)) {
                moveHorizontallyResult = true;

                if (ladderEndDetected) {
                    //avoid horizontal movement to move verticaly properly according to the player facing direction and relative input 

                    if (useLocalMovementDirection || useAlwaysLocalMovementDirection) {
                        if (movementDirection == 1) {
                            if (ladderVerticalInput > 0) {

                            } else if (ladderVerticalInput < 0) {
                                if (moveInLadderCenter || !ladderStartDetected) {
                                    moveHorizontallyResult = false;
                                }
                            }
                        } else {
                            if (ladderVerticalInput > 0) {

                            } else if (ladderVerticalInput < 0) {

                                if (moveInLadderCenter || !ladderStartDetected) {
                                    moveHorizontallyResult = false;
                                }
                            }
                        }
                    }
                }
            }

            if (moveHorizontallyResult) {
                ladderMovementDirection = currentPlayerPosition + ladderRaycastDirectionTransform.forward * ladderVerticalInput;
            }

            if (useLadderHorizontalMovement || useAlwaysHorizontalMovementOnLadder) {

                ladderHorizontalInput = currentHorizontalInput * movementDirection;

                ladderMovementDirection += ladderDirectionTransform.right * (ladderHorizontalInput * ladderHorizontalMovementAmount);
            }

            mainRigidbody.position = Vector3.MoveTowards (mainRigidbody.position, ladderMovementDirection, Time.deltaTime * ladderMoovementSpeed);

            currentPositionOffset = mainRigidbody.position - lastPosition;

            if (currentPositionOffset.sqrMagnitude > 0.0001f) {
                lastPosition = mainRigidbody.position;
                movingOnLadder = true;
            } else {
                movingOnLadder = false;
            }

            if (movingOnLadder != movingOnLadderPreviously) {
                movingOnLadderPreviously = movingOnLadder;

                if (movingOnLadder) {
                    playerControllerManager.stepManager.setFootStepState (climbLadderFootStepStateName);
                } else {
                    playerControllerManager.stepManager.setDefaultGroundFootStepState ();
                }
            }
        }
    }

    void checkStatesOnMovingOnLadderOnFBA (bool state)
    {
        if (currentLadderSystem.changeViewToFBADuringLadderUsage) {
            if (state) {
                if (!playerCameraManager.isFullBodyAwarenessActive ()) {
                    playerCameraManager.changeCameraToThirdOrFirstView ();

                    changeViewToFBAActive = true;
                }
            }
        } else {
            changeViewToFBAActive = false;
        }

        playerControllerManager.setCharacterMeshGameObjectState (!state);

        playerControllerManager.setCharacterMeshesListToDisableOnEventState (!state);

        if (mainPlayerWeaponsManager != null) {
            mainPlayerWeaponsManager.enableOrDisableWeaponsMesh (!state);

            mainPlayerWeaponsManager.enableOrDisableEnabledWeaponsMesh (!state);

            mainPlayerWeaponsManager.pauseExtraColliderOnFireWeaponIfActive (state);
        }

        if (mainMeleeWeaponsGrabbedManager != null) {
            mainMeleeWeaponsGrabbedManager.enableOrDisableAllMeleeWeaponMeshesOnCharacterBodyCheckingIfHiddingMeshes (!state);

            mainMeleeWeaponsGrabbedManager.enableOrDisableAllMeleeWeaponShieldMeshesOnCharacterBodyCheckingIfHiddingMeshes (!state);
        }

        if (currentLadderSystem.changeViewToFBADuringLadderUsage) {
            if (!state) {
                if (changeViewToFBAActive) {
                    playerCameraManager.changeCameraToThirdOrFirstView ();
                }

                changeViewToFBAActive = false;
            }
        }

        //add the extra camera to move the camera backward a little
        if (currentLadderSystem.useExtraFollowTransformPositionOffsetActiveFBA) {
            if (state) {
                playerCameraManager.setExtraFollowTransformPositionOffsetFBA (currentLadderSystem.extraFollowTransformPositionOffsetFBA);
            } else {
                playerCameraManager.setExtraFollowTransformPositionOffsetFBA (Vector3.zero);
            }
        }
    }

    float lastTimeEnterLadder = 0;

    public void setLadderFoundState (bool state, ladderSystem newLadderSystem)
    {
        if (playerControllerManager.isIgnoreExternalActionsActiveState () && state) {
            return;
        }

        if (state == ladderLocated) {
            return;
        }

        if (lastTimeEnterLadder > 0) {
            if (state) {
                if (Time.time < lastTimeEnterLadder + 0.1f) {
                    if (currentLadderSystem != null && currentLadderSystem == newLadderSystem) {
                        if (canUseSimpleLaddersOnFBA && currentLadderSystem.canUseSimpleLaddersOnFBA) {
                            if (showDebugPrint) {
                                print ("cancel ladder");
                            }

                            return;
                        }
                    }
                }
            }
        }

        bool cancelCheckLadderStatesResult = false;

        if (!playerControllerManager.isPlayerOnFirstPerson ()) {
            if (state) {
                checkLadderEventsOnThirdPerson (true);

                ladderFoundThirdPerson = true;

                cancelCheckLadderStatesResult = true;
            } else {
                checkLadderEventsOnThirdPerson (false);

                ladderFoundThirdPerson = false;
            }
        }

        if (previousLadderSystem != currentLadderSystem) {

            previousLadderSystem = currentLadderSystem;
        }

        ladderLocated = state;

        currentLadderSystem = newLadderSystem;

        movingOnLadderOnFBAActive = false;

        bool checkIfMoveOnFBAResult = false;

        if (canUseSimpleLaddersOnFBA && currentLadderSystem.canUseSimpleLaddersOnFBA) {
            if (playerControllerManager.isFullBodyAwarenessActive ()) {
                checkIfMoveOnFBAResult = true;
            } else {
                if (currentLadderSystem.changeViewToFBADuringLadderUsage) {
                    if (!playerControllerManager.isLockedCameraStateActive ()) {
                        checkIfMoveOnFBAResult = true;
                    }
                }
            }
        }

        if (cancelCheckLadderStatesResult) {
            if (!checkIfMoveOnFBAResult) {
                return;
            }
        }

        if (checkIfMoveOnFBAResult) {
            if (ladderLocated) {
                movingOnLadderOnFBAActive = true;

                checkStatesOnMovingOnLadderOnFBA (true);
            } else {
                movingOnLadderOnFBAActive = false;

                checkStatesOnMovingOnLadderOnFBA (false);
            }
        }

        ladderFoundFirstPerson = state;

        lastTimeEnterLadder = Time.time;

        playerControllerManager.setGravityForcePuase (ladderFoundFirstPerson);

        playerControllerManager.setPhysicMaterialAssigmentPausedState (ladderFoundFirstPerson);

        playerCameraManager.setCameraPositionMouseWheelEnabledState (!ladderFoundFirstPerson);

        playerControllerManager.setUpdate2_5dClampedPositionPausedState (ladderFoundFirstPerson);

        if (ladderFoundFirstPerson) {
            playerControllerManager.setRigidbodyVelocityToZero ();

            playerControllerManager.setCheckOnGroungPausedState (true);

            playerControllerManager.setOnGroundState (false);

            playerControllerManager.setZeroFrictionMaterial ();

            playerControllerManager.headBobManager.stopAllHeadbobMovements ();
            playerControllerManager.headBobManager.playOrPauseHeadBob (false);

            playerControllerManager.stepManager.setFootStepState (climbLadderFootStepStateName);

            playerControllerManager.setPauseAllPlayerDownForces (true);

            playerCameraManager.enableOrDisableChangeCameraView (false);

            playerControllerManager.setLadderFoundState (true);

            playerControllerManager.setIgnoreExternalActionsActiveState (true);

            playerControllerManager.setIgnoreInputOnAirControlActiveState (true);

            if (playerControllerManager.isFullBodyAwarenessActive ()) {
                playerControllerManager.setAnimatorState (false);
            }
        } else {
            playerControllerManager.setCheckOnGroungPausedState (false);

            playerControllerManager.headBobManager.pauseHeadBodWithDelay (0.5f);
            playerControllerManager.headBobManager.playOrPauseHeadBob (true);

            playerControllerManager.setPauseAllPlayerDownForces (false);

            playerCameraManager.setOriginalchangeCameraViewEnabledValue ();

            playerControllerManager.setLadderFoundState (false);

            playerControllerManager.stepManager.setDefaultGroundFootStepState ();

            playerControllerManager.setIgnoreExternalActionsActiveState (false);

            playerControllerManager.setIgnoreInputOnAirControlActiveState (false);

            if (playerControllerManager.getCurrentSurfaceBelowPlayer () != null || playerControllerManager.checkIfPlayerOnGroundWithRaycast ()) {
                playerControllerManager.setPlayerOnGroundState (true);

                playerControllerManager.setOnGroundAnimatorIDValue (true);
            }

            if (playerControllerManager.isFullBodyAwarenessActive ()) {
                playerControllerManager.setAnimatorState (true);
            }
        }

        if (showDebugPrint) {
            print ("setLadderFoundState " + state);
        }

        if (pauseInputOnUseLaddersEnabled) {
            checkInputListToPauseDuringAction (state);
        }
    }

    public void setLadderDirectionTransform (Transform newLadderDirectionTransform, Transform newLadderRaycastDirectionTransform)
    {
        ladderDirectionTransform = newLadderDirectionTransform;
        ladderRaycastDirectionTransform = newLadderRaycastDirectionTransform;
    }

    public void setLadderHorizontalMovementState (bool state)
    {
        useLadderHorizontalMovement = state;
    }

    public void setMoveInLadderCenterState (bool state)
    {
        moveInLadderCenter = state;
    }

    public bool isLadderFound ()
    {
        return ladderFoundFirstPerson;
    }

    public void setUseLocalMovementDirectionState (bool state)
    {
        useLocalMovementDirection = state;
    }

    public void checkLadderEventsOnThirdPerson (bool state)
    {
        if (useEventsOnThirdPerson) {
            if (state) {
                eventOnLocatedLadderOnThirdPerson.Invoke ();
            } else {
                eventOnRemovedLadderOnThirdPerson.Invoke ();
            }
        }
    }

    public void rotateCharacterOnJump ()
    {
        stopRotateCharacterOnJumpCoroutine ();

        jumpCoroutine = StartCoroutine (rotateCharacterOnJumpCoroutine ());
    }

    void stopRotateCharacterOnJumpCoroutine ()
    {
        if (jumpCoroutine != null) {
            StopCoroutine (jumpCoroutine);
        }
    }

    public IEnumerator rotateCharacterOnJumpCoroutine ()
    {
        bool targetReached = false;

        float movementTimer = 0;

        float t = 0;

        float duration = 0;

        bool isFirstPersonActive = playerControllerManager.isPlayerOnFirstPerson ();

        if (isFirstPersonActive) {
            duration = 0.5f / jumpRotationSpeedFirstPerson;
        } else {
            duration = 0.5f / jumpRotationSpeedThirdPerson;
        }

        float angleDifference = 0;

        bool isFullBodyAwarenessActive = playerControllerManager.isFullBodyAwarenessActive ();

        Transform objectToRotate = playerControllerManager.transform;

        if (isFirstPersonActive || isFullBodyAwarenessActive) {
            objectToRotate = playerCameraTransform;
        }

        Quaternion targetRotation = Quaternion.LookRotation (-objectToRotate.forward, objectToRotate.up);

        while (!targetReached) {
            t += Time.deltaTime / duration;

            objectToRotate.rotation = Quaternion.Slerp (objectToRotate.rotation, targetRotation, t);

            angleDifference = Quaternion.Angle (objectToRotate.rotation, targetRotation);

            movementTimer += Time.deltaTime;

            if (angleDifference < 0.2f || movementTimer > (duration + 1)) {
                targetReached = true;
            }
            yield return null;
        }
    }

    public void checkEventOnJump ()
    {
        if (useEventsOnJump) {
            eventOnJump.Invoke ();

            if (currentLadderSystem != null) {
                currentLadderSystem.checkEventOnJump ();
            }
        }
    }

    void checkInputListToPauseDuringAction (bool state)
    {
        if (mainPlayerInputManager == null) {
            return;
        }

        int customInputtCount = customInputToPauseOnActionInfoList.Count;

        for (int i = 0; i < customInputtCount; i++) {
            playerActionSystem.inputToPauseOnActionIfo inputList = customInputToPauseOnActionInfoList [i];

            if (state) {
                inputList.previousActiveState = mainPlayerInputManager.setPlayerInputMultiAxesStateAndGetPreviousState (false, inputList.inputName);
            } else {
                if (inputList.previousActiveState) {
                    mainPlayerInputManager.setPlayerInputMultiAxesState (inputList.previousActiveState, inputList.inputName);
                }
            }
        }
    }

    //INPUT FUNCTIONS
    public void inputJumpFromLadder ()
    {
        if (!ladderLocated) {
            return;
        }

        activeJumpOnLadder ();
    }

    public void activeJumpOnLadder ()
    {
        bool canJumpResult = false;

        if (ladderFoundThirdPerson && jumpOnThirdPersonEnabled) {
            if (currentLadderSystem.isJumpOnThirdPersonEnabled ()) {
                canJumpResult = true;
            }
        }

        if (ladderFoundFirstPerson && jumpOnFirstPersonEnabled) {
            if (currentLadderSystem.isJumpOnFirstPersonEnabled ()) {
                canJumpResult = true;
            }
        }

        if (canJumpResult) {
            if (showDebugPrint) {
                print ("activate jump on ladder");
            }

            Vector3 newImpulseOnJump = impulseOnJump;

            if (!playerControllerManager.isPlayerMovingOn3dWorld ()) {
                newImpulseOnJump = impulseOnJump2_5d;
            }

            currentLadderSystem.checkEventOnJump ();

            currentLadderSystem.checkTriggerInfo (playerControllerManager.getMainCollider (), false);

            playerControllerManager.stopAllActionsOnActionSystem ();

            Vector3 totalForce = newImpulseOnJump.y * playerControllerManager.transform.up + newImpulseOnJump.z * playerControllerManager.transform.forward;

            playerControllerManager.resetLastMoveInputOnJumpValue ();

            playerControllerManager.useJumpPlatform (totalForce, ForceMode.Impulse);

            rotateCharacterOnJump ();

            if (!playerControllerManager.isPlayerMovingOn3dWorld ()) {
                playerControllerManager.resetPlayerControllerInput ();

                playerControllerManager.setMoveInputPausedWithDuration (true, 0.2f);

                playerControllerManager.setCurrentVelocityValue (Vector3.zero);
            }
        }
    }
}
