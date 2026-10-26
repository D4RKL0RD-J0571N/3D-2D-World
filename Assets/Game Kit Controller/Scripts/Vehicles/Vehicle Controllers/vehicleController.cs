using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using GameKitController.Audio;

public class vehicleController : MonoBehaviour
{
    [Header ("Main Settings")]
    [Space]

    public bool updateVehicleStateEnabled;
    public bool callVehicleUpdateEachFrame = true;
    public bool callVehicleFixedUpdateEachFrame = true;

    public bool updatePassengersAngularDirection;

    public bool useHorizontalInputLerp = true;

    [Space]
    [Header ("Vehicle Settings")]
    [Space]

    public vehicleControllerSettingsInfo vehicleControllerSettings;

    [Space]
    [Header ("Other Settings")]
    [Space]

    public bool update2_5dInputEnabled = true;

    public bool ignoreVerticalInputOn2_5d;

    public bool keepVelocityOnChangeOfDirectionOn2_5dView;
    public bool forceVehicleRotationTo2_5dViewDirection = true;

    public bool update2_5dRotationLeftOrRightEnabled = true;

    public bool updateOnlyRightAxisRotationOn2_5d;

    public bool set3dWolrdStateOnGettingOff;

    [Space]

    public bool sendCollisionsToRumbleEnabled;

    [Space]
    [Header ("Hoverboard-Rail Settings")]
    [Space]

    public bool useHoverboardWaypointsEnabled;

    public float hoverboardWaypointsExtraRotation;

    public bool useCustomHoverboardCenterPoint;
    public Vector3 customHoverboardCenterPoint;

    public bool useHoverboardJumpClamp;

    public float hoverboardJumpForce = 500;

    public float hoverboardJumpClamp = 50;

    [Space]

    public bool resetVehicleTransformRotationAfterHoverboard;
    public float resetVehicleTransformRotationSpeed = 10;

    public bool resetVehicleTransformRotationToIdentity;

    [Space]
    [Header ("Debug")]
    [Space]

    public bool showDebugPrint;

    public bool driving;
    public bool isTurnedOn;
    public bool vehicleDestroyed;

    public bool usingGravityControl;

    public bool braking;

    public bool usingImpulse;

    public bool jumpInputPressed;

    public Vector3 currentNormal;

    public float lastTimeJump;

    public float currentSpeed;

    public bool autobrakeActive;

    public float offroadAmount;

    public bool drifting;

    public float driftingAmount;

    [Space]

    public bool usingHoverBoardWaypoint;

    public float lastTimeReleasedFromWaypoint;

    public hoverBoardWayPoints wayPointsManager;

    public bool cameraTypeFree;

    public bool playerMovingOn3dWorld;

    public bool moveInXAxisOn2_5d;

    public bool pauseUpdate2_5dRotationToLeftOrRightByInputDebug;

    public bool pauseUpdate2_5dRotationToLeftOrRightDebug;

    public bool ignoreToTurnOnOrOffInputActive;

    public bool movingBackwards;

    [Space]
    [Header ("Debug Input")]
    [Space]


    public float horizontalAxis = 0;

    public float verticalAxis = 0;

    public Vector2 axisValues;

    public Vector2 rawAxisValues;

    public float motorInput;

    public bool moving;

    public bool usingBoost;

    public float boostInput = 1;

    public bool touchPlatform;

    [Space]
    [Header ("Components")]
    [Space]

    public vehicleHUDManager mainVehicleHUDManager;
    public vehicleCameraController mainVehicleCameraController;
    public vehicleGravityControl mainVehicleGravityControl;
    public Rigidbody mainRigidbody;
    public IKDrivingSystem mainIKDrivingSystem;
    public inputActionManager mainInputActionManager;

    Transform currentLockedCameraTransform;

    [HideInInspector] public bool changingGear;
    [HideInInspector] public float steering;
    [HideInInspector] public Vector3 localLook;

    float lastTimeImpulseUsed;

    bool rotatingVehicleIn2_5dToRight = false;
    bool rotatingVehicleIn2_5dToRLeft = false;

    Vector3 lastLookingInRightDirectionOn2_5d;

    Coroutine hoverboardMovementCoroutine;
    Coroutine rotateVehicleCoroutine;

    bool resetVehicleTransformRotationInProcess;

    float lastTimeRotatedToLeftOrRight = 0;


    public virtual void Awake ()
    {
        touchPlatform = InputAxisWrapper.checkTouchPlatform ();

        InitializeAudioElements ();
    }

    public virtual void Start ()
    {

    }

    protected virtual void InitializeAudioElements ()
    {

    }

    public void checkCameraType ()
    {
        cameraTypeFree = mainVehicleCameraController.isCameraTypeFree ();

        playerMovingOn3dWorld = mainVehicleCameraController.isPlayerMovingOn3dWorld ();

        moveInXAxisOn2_5d = mainVehicleCameraController.isMoveInXAxisOn2_5d ();

    }

    public virtual void vehicleUpdate ()
    {
        checkCameraType ();

        //if the player is driving this vehicle and the gravity control is not being used, then
        if (driving && !usingGravityControl) {
            axisValues = mainInputActionManager.getPlayerMovementAxis ();

            if (vehicleControllerSettings.canUseBoostWithoutVerticalInput) {
                if (usingBoost) {
                    axisValues.y = 1;
                }
            }

            horizontalAxis = axisValues.x;

            if (!playerMovingOn3dWorld && update2_5dInputEnabled) {
                horizontalAxis = 0;
            }

            rawAxisValues = mainInputActionManager.getPlayerRawMovementAxis ();

            if (vehicleControllerSettings.canUseBoostWithoutVerticalInput) {
                if (usingBoost) {
                    rawAxisValues.y = 1;

                    if (!playerMovingOn3dWorld && update2_5dInputEnabled) {
                        rawAxisValues.x = 1;
                    }
                }
            }

            if (!useHorizontalInputLerp && !touchPlatform) {
                horizontalAxis = rawAxisValues.x;

                if (!playerMovingOn3dWorld && update2_5dInputEnabled) {
                    horizontalAxis = 0;
                }
            }

            if (mainVehicleCameraController.currentState.useCameraSteer && horizontalAxis == 0) {
                localLook = transform.InverseTransformDirection (mainVehicleCameraController.getLookDirection ());

                updateCameraSteerState ();
            }

            if (isTurnedOn) {

                //get the current values from the input manager, keyboard and touch controls
                verticalAxis = axisValues.y;

                if (!playerMovingOn3dWorld) {
                    if (update2_5dInputEnabled) {
                        verticalAxis = axisValues.x;
                    }

                    if (ignoreVerticalInputOn2_5d) {
                        axisValues.y = 0;

                        verticalAxis = 0;

                        rawAxisValues.y = 0;
                    }
                }

                if (vehicleControllerSettings.canImpulseHoldingJump) {
                    if (usingImpulse) {
                        if (vehicleControllerSettings.impulseUseEnergy) {
                            if (Time.time > lastTimeImpulseUsed + vehicleControllerSettings.impulseUseEnergyRate) {
                                mainVehicleHUDManager.removeEnergy (vehicleControllerSettings.impulseUseEnergyAmount);
                                lastTimeImpulseUsed = Time.time;
                            }
                        }

                        mainRigidbody.AddForce ((mainRigidbody.mass * vehicleControllerSettings.impulseForce) * transform.up);
                    }
                }
            }

            //if the boost input is enabled, check if there is energy enough to use it
            if (usingBoost) {
                //if there is enough energy, enable the boost
                updateMovingState ();

                if (mainVehicleHUDManager.useBoost (moving)) {
                    boostInput = vehicleControllerSettings.maxBoostMultiplier;

                    usingBoosting ();
                } else {

                    //else, disable the boost
                    usingBoost = false;

                    //if the vehicle is not using the gravity control system, disable the camera move away action
                    if (!mainVehicleGravityControl.isGravityPowerActive ()) {
                        mainVehicleCameraController.usingBoost (false, vehicleControllerSettings.boostCameraShakeStateName,
                            vehicleControllerSettings.useBoostCameraShake, vehicleControllerSettings.moveCameraAwayOnBoost);
                    }

                    usingBoosting ();

                    boostInput = 1;
                }
            }

            //set the current speed in the HUD of the vehicle
            mainVehicleHUDManager.getSpeed (currentSpeed, vehicleControllerSettings.maxForwardSpeed);

            if (updatePassengersAngularDirection) {
                mainIKDrivingSystem.setNewAngularDirection (verticalAxis * Vector3.forward + horizontalAxis * Vector3.right);
            }
        } else {
            //else, set the input values to 0
            horizontalAxis = 0;
            verticalAxis = 0;

            rawAxisValues = Vector2.zero;
        }

        if (!changingGear) {
            motorInput = verticalAxis;
        } else {
            motorInput = Mathf.Clamp (verticalAxis, -1, 0);
        }

        if (!playerMovingOn3dWorld && update2_5dInputEnabled) {
            if (motorInput < -0.01f) {
                motorInput *= (-1);
            }
        }

        updateMovingState ();

        //if the vehicle has fuel, allow to move it
        if (moving) {
            if (!mainVehicleHUDManager.useFuel ()) {
                motorInput = 0;

                if (isTurnedOn) {
                    turnOnOrOff (false, isTurnedOn);
                }
            }
        }

        checkAutoBrakeOnGetOffState ();

        if (!playerMovingOn3dWorld) {
            update2_5DState ();
        }

        isMovingBackwards ();
    }

    public void update2_5DState ()
    {
        if (moveInXAxisOn2_5d) {
            float distance = Mathf.Abs (transform.position.z) - Mathf.Abs (mainVehicleCameraController.getOriginalLockedCameraPivotPosition ().z);

            if (Mathf.Abs (distance) > 0.3f) {
                mainRigidbody.MovePosition (new Vector3 (mainRigidbody.position.x, mainRigidbody.position.y,
                    mainVehicleCameraController.getOriginalLockedCameraPivotPosition ().z));
            }
        } else {
            float distance = Mathf.Abs (transform.position.x) - Mathf.Abs (mainVehicleCameraController.getOriginalLockedCameraPivotPosition ().x);

            if (Mathf.Abs (distance) > 0.3f) {
                mainRigidbody.MovePosition (new Vector3 (mainVehicleCameraController.getOriginalLockedCameraPivotPosition ().x,
                    transform.position.y, transform.position.z));
            }
        }

        currentLockedCameraTransform = mainVehicleCameraController.getLockedCameraTransform ();

        if (pauseUpdate2_5dRotationToLeftOrRightByInputDebug) {
            return;
        }

        if (forceVehicleRotationTo2_5dViewDirection) {
            //adjust player orientation to lef or right according to the input direction pressed just once
            if (rawAxisValues.x == 1) {
                if (!rotatingVehicleIn2_5dToRight) {
                    float lastVelocity = mainRigidbody.linearVelocity.magnitude;

                    Quaternion targetRotation = Quaternion.LookRotation (currentLockedCameraTransform.right);

                    if (updateOnlyRightAxisRotationOn2_5d) {
                        transform.rotation = targetRotation;
                    } else {
                        float angle = Vector3.SignedAngle (transform.forward, -currentLockedCameraTransform.right, transform.right);

                        if (Mathf.Abs (angle) > 90) {
                            bool angleIsNegative = angle < 0;

                            angle = Mathf.Abs (180 - Mathf.Abs (angle));

                            if (angleIsNegative) {
                                angle *= (-1);
                            }
                        }

                        Vector3 rightRotation = currentLockedCameraTransform.right * angle;

                        Vector3 targetEulerAngles = targetRotation.eulerAngles - rightRotation;

                        transform.eulerAngles = targetEulerAngles;
                    }

                    if (keepVelocityOnChangeOfDirectionOn2_5dView) {
                        mainRigidbody.linearVelocity = lastVelocity * transform.forward;
                    }

                    rotatingVehicleIn2_5dToRight = false;

                    if (showDebugPrint) {
                        print ("rotatingVehicleIn2_5dToRight " + transform.eulerAngles);
                    }

                    lastTimeRotatedToLeftOrRight = Time.time;
                }

                rotatingVehicleIn2_5dToRight = true;
                rotatingVehicleIn2_5dToRLeft = false;

                lastLookingInRightDirectionOn2_5d = currentLockedCameraTransform.right;
            }

            if (rawAxisValues.x == -1) {
                if (!rotatingVehicleIn2_5dToRLeft) {
                    float lastVelocity = mainRigidbody.linearVelocity.magnitude;

                    Quaternion targetRotation = Quaternion.LookRotation (-currentLockedCameraTransform.right);

                    if (updateOnlyRightAxisRotationOn2_5d) {
                        transform.rotation = targetRotation;
                    } else {
                        float angle = Vector3.SignedAngle (transform.forward, currentLockedCameraTransform.right, transform.right);

                        if (Mathf.Abs (angle) > 90) {
                            bool angleIsNegative = angle < 0;

                            angle = Mathf.Abs (180 - Mathf.Abs (angle));

                            if (angleIsNegative) {
                                angle *= (-1);
                            }
                        }


                        Vector3 rightRotation = currentLockedCameraTransform.right * angle;

                        Vector3 targetEulerAngles = targetRotation.eulerAngles - rightRotation;

                        transform.eulerAngles = targetEulerAngles;
                    }

                    if (keepVelocityOnChangeOfDirectionOn2_5dView) {
                        mainRigidbody.linearVelocity = lastVelocity * transform.forward;
                    }

                    rotatingVehicleIn2_5dToRLeft = false;

                    if (showDebugPrint) {
                        print ("rotatingVehicleIn2_5dToRLeft " + transform.eulerAngles);
                    }

                    lastTimeRotatedToLeftOrRight = Time.time;
                }

                rotatingVehicleIn2_5dToRight = false;
                rotatingVehicleIn2_5dToRLeft = true;

                lastLookingInRightDirectionOn2_5d = -currentLockedCameraTransform.right;
            }

            if (pauseUpdate2_5dRotationToLeftOrRightDebug) {
                return;
            }

            if (update2_5dRotationLeftOrRightEnabled) {
                if (lastTimeRotatedToLeftOrRight == 0 || Time.time < lastTimeRotatedToLeftOrRight + 0.6f) {
                    return;
                }

                float currentAngle = Vector3.SignedAngle (transform.right,
                                         currentLockedCameraTransform.forward, mainVehicleCameraController.transform.up);

                float angleABS = Mathf.Abs (currentAngle);

                if (angleABS < 100) {
                    if (angleABS > 3) {
                        transform.Rotate (0, currentAngle, 0);
                    }
                } else {
                    if (Mathf.Abs (180 - angleABS) > 3) {
                        currentAngle = 180 - angleABS;

                        transform.Rotate (0, currentAngle, 0);
                    }
                }
            }
        } else {
            if (update2_5dRotationLeftOrRightEnabled) {
                //adjust player orientation to lef or right according to the input direction pressed just once
                if (rawAxisValues.x == 1) {
                    if (!rotatingVehicleIn2_5dToRight) {
                        float lastVelocity = mainRigidbody.linearVelocity.magnitude;

                        rotatingVehicleIn2_5dToRight = false;

                        if (keepVelocityOnChangeOfDirectionOn2_5dView) {
                            mainRigidbody.linearVelocity = lastVelocity * currentLockedCameraTransform.right;
                        }
                    }

                    rotatingVehicleIn2_5dToRight = true;
                    rotatingVehicleIn2_5dToRLeft = false;

                    lastLookingInRightDirectionOn2_5d = currentLockedCameraTransform.right;
                }

                if (rawAxisValues.x == -1) {
                    if (!rotatingVehicleIn2_5dToRLeft) {
                        float lastVelocity = mainRigidbody.linearVelocity.magnitude;

                        rotatingVehicleIn2_5dToRLeft = false;

                        if (keepVelocityOnChangeOfDirectionOn2_5dView) {
                            mainRigidbody.linearVelocity = lastVelocity * (-currentLockedCameraTransform.right);
                        }
                    }

                    rotatingVehicleIn2_5dToRight = false;
                    rotatingVehicleIn2_5dToRLeft = true;

                    lastLookingInRightDirectionOn2_5d = -currentLockedCameraTransform.right;
                }
            }
        }
    }

    public virtual void updateCameraSteerState ()
    {
        if (localLook.z < 0f) {
            localLook.x = Mathf.Sign (localLook.x);
        }

        steering = localLook.x;
        steering = Mathf.Clamp (steering, -1f, 1f);

        if (axisValues.y < 0) {
            steering *= (-1);
        }

        horizontalAxis = steering;
    }

    public virtual void updateMovingState ()
    {
        moving = verticalAxis != 0;
    }

    public virtual void vehicleFixedUpdate ()
    {

    }

    public virtual void setCollisionDetected (Collision currentCollision)
    {
        if (sendCollisionsToRumbleEnabled) {
            mainInputActionManager.setCollisionDetected (currentCollision);
        }
    }

    public virtual void setDamageReceived (float damageAmount)
    {
        mainInputActionManager.setDamageReceived (damageAmount); 
    }

    public virtual void startBrakeVehicleToStopCompletely ()
    {

    }

    public virtual void endBrakeVehicleToStopCompletely ()
    {

    }

    public virtual void changeVehicleState ()
    {
        driving = !driving;

        //set the audio values if the player is getting on or off from the vehicle
        if (driving) {
            if (mainVehicleHUDManager.autoTurnOnWhenGetOn) {
                turnOnOrOff (true, isTurnedOn);
            }
        } else {
            turnOnOrOff (false, isTurnedOn);
        }

        //set the same state in the gravity control components

        if (mainVehicleGravityControl != null) {
            mainVehicleGravityControl.changeGravityControlState (driving);
        }

        if (driving) {
            autobrakeActive = false;
        }

        rotatingVehicleIn2_5dToRight = false;
        rotatingVehicleIn2_5dToRLeft = false;
    }

    public virtual void passengerGettingOnOff ()
    {

    }

    public virtual void checkVehicleStateOnPassengerGettingOnOrOff (bool state)
    {

    }

    public virtual void fixDestroyedVehicle ()
    {
        if (vehicleDestroyed) {
            vehicleDestroyed = false;

            this.enabled = true;
        }
    }

    public virtual void disableVehicle ()
    {
        vehicleDestroyed = true;

        setTurnOffState (false);

        //disable the controller
        this.enabled = false;
    }

    public virtual float getCurrentSpeed ()
    {
        return 100;
    }

    public virtual float getMaxForwardSpeed ()
    {
        return vehicleControllerSettings.maxForwardSpeed;
    }

    public virtual float getCurrentRPM ()
    {
        return 0;
    }

    public virtual float getCurrentLean ()
    {
        return 0;
    }

    public virtual float getCurrentSuspension ()
    {
        return 0;
    }

    public virtual float getHoverHeight ()
    {
        return 0;
    }

    public virtual float getHoverInstability ()
    {
        return 0;
    }

    //INPUT FUNCTIONS
    public virtual void inputJump ()
    {
        if (driving && !usingGravityControl && isTurnedOn) {
            //jump input
            if (vehicleControllerSettings.canJump) {
                jumpInputPressed = true;
            }
        }
    }

    public virtual void inputHoldOrReleaseJump (bool holdingButton)
    {
        if (driving && !usingGravityControl && isTurnedOn) {
            if (vehicleControllerSettings.canImpulseHoldingJump) {
                if (holdingButton) {
                    if (Time.time > lastTimeJump + 0.2f) {
                        usingImpulse = true;
                    }
                } else {
                    usingImpulse = false;
                }
            }
        }
    }

    public virtual void inputHoldOrReleaseTurbo (bool holdingButton)
    {
        if (driving && !usingGravityControl && isTurnedOn) {
            if (holdingButton) {
                //boost input
                if (vehicleControllerSettings.canUseBoost) {
                    usingBoost = true;

                    //set the camera move away action

                    mainVehicleCameraController.usingBoost (true, vehicleControllerSettings.boostCameraShakeStateName,
                        vehicleControllerSettings.useBoostCameraShake, vehicleControllerSettings.moveCameraAwayOnBoost);
                }
            } else {
                //stop boost input
                usingBoost = false;

                //disable the camera move away action
                mainVehicleCameraController.usingBoost (false, vehicleControllerSettings.boostCameraShakeStateName,
                    vehicleControllerSettings.useBoostCameraShake, vehicleControllerSettings.moveCameraAwayOnBoost);

                //disable the boost particles
                usingBoosting ();

                boostInput = 1;
            }
        }
    }

    public virtual void inputSetTurnOnState ()
    {
        if (driving && !usingGravityControl) {
            if (mainVehicleHUDManager.canSetTurnOnState) {
                if (ignoreToTurnOnOrOffInputActive) {
                    return;
                }

                setEngineOnOrOffState ();
            }
        }
    }

    public virtual void inputHorn ()
    {
        if (driving && !usingGravityControl) {
            pressHorn ();
        }
    }

    public virtual void inputHoldOrReleaseBrake (bool holdingButton)
    {
        if (driving && !usingGravityControl) {
            braking = holdingButton;
        }
    }

    public virtual bool isBrakeActive ()
    {
        return braking || mainInputActionManager.getHandBrakeValue () > 0;
    }


    public virtual void pressHorn ()
    {
        mainVehicleHUDManager.activateHorn ();
    }

    //play or stop every audio component in the vehicle, like engine, skid, etc.., configuring also volume and loop according to the movement of the vehicle
    public void setAudioState (AudioElement source, float distance, float volume, bool loop, bool play, bool stop)
    {
        source.audioSource.minDistance = distance;
        source.audioSource.volume = volume;
        //source.clip = audioClip;
        source.audioSource.loop = loop;
        source.audioSource.spatialBlend = 1;

        if (play) {
            AudioPlayer.Play (source, gameObject);
        }

        if (stop) {
            AudioPlayer.Stop (source, gameObject);
        }
    }
    public void setIgnoreToTurnOnOrOffInputActiveState (bool state)
    {
        ignoreToTurnOnOrOffInputActive = state;
    }

    public virtual void setEngineOnOrOffState ()
    {
        if (mainVehicleHUDManager.hasFuel ()) {
            turnOnOrOff (!isTurnedOn, isTurnedOn);
        }
    }

    public virtual void turnOnOrOff (bool state, bool previouslyTurnedOn)
    {
        if (vehicleDestroyed) {
            if (state && !isTurnedOn) {

                return;
            }
        }

        isTurnedOn = state;

        if (isTurnedOn) {
            setTurnOnState ();
        } else {
            setTurnOffState (previouslyTurnedOn);
        }
    }

    public virtual void setTurnOnState ()
    {

    }

    public virtual void setTurnOffState (bool previouslyTurnedOn)
    {
        motorInput = 0;

        boostInput = 1;

        horizontalAxis = 0;

        verticalAxis = 0;

        //stop the boost
        if (usingBoost) {
            usingBoost = false;

            mainVehicleCameraController.usingBoost (false, vehicleControllerSettings.boostCameraShakeStateName,
                vehicleControllerSettings.useBoostCameraShake, vehicleControllerSettings.moveCameraAwayOnBoost);

            usingBoosting ();
        }

        usingImpulse = false;
    }

    //if the vehicle is using the gravity control, set the state in this component
    public virtual void changeGravityControlUse (bool state)
    {
        usingGravityControl = state;

        usingImpulse = false;
    }

    //get the current normal in the gravity control component
    public virtual void setNormal (Vector3 normalValue)
    {
        currentNormal = normalValue;
    }

    public virtual Vector3 getNormal ()
    {
        return currentNormal;
    }

    //if the vehicle is using the boost, set the boost particles
    public virtual void usingBoosting ()
    {

    }

    public virtual bool isUsingBoosting ()
    {
        return usingBoost;
    }

    public virtual bool isBraking ()
    {
        return braking;
    }

    public virtual float getOffroadAmount ()
    {
        return offroadAmount;
    }

    public virtual bool isDrifting ()
    {
        return drifting;
    }

    public virtual float getDriftingAmount ()
    {
        return driftingAmount;
    }

    public virtual bool isUseOfGravityActive ()
    {
        return mainVehicleGravityControl.useGravity;
    }

    public virtual float getCurrentGravityForceValue ()
    {
        return mainVehicleGravityControl.getGravityForce ();
    }

    public virtual float getCurrentSteeringShake ()
    {
        return 0;
    }

    public virtual bool isDrivingActive ()
    {
        return driving;
    }

    public bool isPlayerUsingInput ()
    {
        if (rawAxisValues.x != 0 || rawAxisValues.y != 0) {
            return true;
        }

        return false;
    }

    public bool isCameraTypeFree ()
    {
        return cameraTypeFree;
    }

    public bool isPlayerMovingOn3dWorld ()
    {
        return playerMovingOn3dWorld;
    }

    public bool isMovingBackwards ()
    {
        movingBackwards = false;

        if (isTurnedOn) {
            Vector3 speedDirection = mainRigidbody.linearVelocity;

            if (speedDirection.magnitude > 1) {
                //float speedAngle = Vector3.SignedAngle (transform.forward, speedDirection, transform.up);

                float speedAngle = Vector3.Angle (transform.forward, speedDirection);

                //movingBackwards = (Mathf.Abs (speedAngle) > 90);

                movingBackwards = speedAngle > 90;
            }
        }

        return movingBackwards;
    }

    public Vector3 getLastLookingInRightDirectionOn2_5d ()
    {
        if (lastLookingInRightDirectionOn2_5d == Vector3.zero) {
            if (moveInXAxisOn2_5d) {
                return Vector3.right;
            } else {
                return Vector3.forward;
            }
        }

        return lastLookingInRightDirectionOn2_5d;
    }

    public virtual Vector3 getCurrentNormal ()
    {
        return mainVehicleGravityControl.getCurrentNormal ();
    }

    public virtual void setNewMainCameraTransform (Transform newTransform)
    {

    }

    public virtual void setNewPlayerCameraTransform (Transform newTransform)
    {

    }

    public virtual void setUseForwardDirectionForCameraDirectionState (bool state)
    {

    }

    public virtual void setUseRightDirectionForCameraDirectionState (bool state)
    {

    }

    public virtual void setAddExtraRotationPausedState (bool state)
    {

    }

    public virtual void activateAutoBrakeOnGetOff ()
    {
        braking = true;

        autobrakeActive = true;
    }

    public virtual void checkAutoBrakeOnGetOffState ()
    {
        if (autobrakeActive) {
            if (Mathf.Abs (mainRigidbody.linearVelocity.magnitude) < 0.5f) {
                autobrakeActive = false;

                braking = false;
            }
        }
    }

    public virtual bool isVehicleOnGround ()
    {

        return false;
    }

    public virtual bool canUseHoverboardWaypoints ()
    {
        return useHoverboardWaypointsEnabled;
    }

    public virtual float getLastTimeReleasedFromWaypoint ()
    {
        return lastTimeReleasedFromWaypoint;
    }

    public virtual bool isUsingHoverBoardWaypoint ()
    {
        return usingHoverBoardWaypoint;
    }

    public virtual void enterOrExitFromWayPoint (bool state)
    {
        usingHoverBoardWaypoint = state;

        mainVehicleGravityControl.enabled = !state;

        mainRigidbody.isKinematic = state;

        if (usingHoverBoardWaypoint) {
            lastTimeReleasedFromWaypoint = 0;
        } else {
            lastTimeReleasedFromWaypoint = Time.time;
        }

        if (resetVehicleTransformRotationAfterHoverboard) {
            if (usingHoverBoardWaypoint) {
                if (resetVehicleTransformRotationInProcess) {
                    stopRotateVehicleCoroutine ();
                }
            } else {
                resetVehicleTransformRotation ();
            }
        }
    }

    public virtual void receiveWayPoints (hoverBoardWayPoints wayPoints)
    {
        wayPointsManager = wayPoints;
    }

    public virtual float getMotorInput ()
    {
        return motorInput;
    }

    public float getHorizontalAxis ()
    {
        return horizontalAxis;
    }

    public float getVerticalAxis ()
    {
        return verticalAxis;
    }

    public Vector2 getPlayerMouseAxis ()
    {
        return mainInputActionManager.getPlayerMouseAxis ();
    }

    public Vector2 getPlayerMovementAxis ()
    {
        return mainInputActionManager.getPlayerMovementAxis ();
    }


    //Vehicle hoverboard movement functions
    public void resetVehicleTransformRotation ()
    {
        stopRotateVehicleCoroutine ();

        rotateVehicleCoroutine = StartCoroutine (resetVehicleTransformRotationCoroutine ());
    }

    void stopRotateVehicleCoroutine ()
    {
        if (rotateVehicleCoroutine != null) {
            StopCoroutine (rotateVehicleCoroutine);
        }

        resetVehicleTransformRotationInProcess = false;
    }

    public IEnumerator resetVehicleTransformRotationCoroutine ()
    {
        resetVehicleTransformRotationInProcess = true;

        Quaternion currentVehicleRotation = transform.rotation;

        Vector3 currentVehicleForward = Vector3.Cross (transform.right, getNormal ());
        Quaternion targetVehicleRotation = Quaternion.LookRotation (currentVehicleForward, getNormal ());

        if (resetVehicleTransformRotationToIdentity) {
            targetVehicleRotation = Quaternion.identity;
        }

        for (float t = 0; t < 1;) {
            t += Time.deltaTime * resetVehicleTransformRotationSpeed;

            transform.rotation = Quaternion.Slerp (currentVehicleRotation, targetVehicleRotation, t);

            yield return null;
        }

        resetVehicleTransformRotationInProcess = false;
    }

    public void pickOrReleaseHoverboardVehicle (bool state, bool auto)
    {
        enterOrExitFromWayPoint (state);

        mainVehicleCameraController.startOrStopFollowVehiclePosition (!state);

        if (!state) {
            stopMoveThroughWayPointsCoroutine ();

            if (auto) {
                mainRigidbody.AddForce ((mainRigidbody.mass * wayPointsManager.forceAtEnd) * transform.forward, ForceMode.Impulse);
            }
        }

        if (state) {
            activeHoverboardMovement ();
        }
    }

    public void activeHoverboardMovement ()
    {
        stopMoveThroughWayPointsCoroutine ();

        hoverboardMovementCoroutine = StartCoroutine (moveThroughWayPointsCoroutine ());
    }

    public void stopMoveThroughWayPointsCoroutine ()
    {
        if (hoverboardMovementCoroutine != null) {
            StopCoroutine (hoverboardMovementCoroutine);
        }
    }

    IEnumerator moveThroughWayPointsCoroutine ()
    {
        float currentVerticalDirection;
        float speedMultiplier = 1;
        float currentMovementSpeed;

        float closestDistance = Mathf.Infinity;

        int index = -1;

        List<hoverBoardWayPoints.wayPointsInfo> wayPoints = wayPointsManager.wayPoints;

        Vector3 mainObjectPosition = transform.position;

        for (int i = 0; i < wayPoints.Count; i++) {
            float currentDistance = GKC_Utils.distance (wayPoints [i].wayPoint.position, mainObjectPosition);

            if (currentDistance < closestDistance) {
                closestDistance = currentDistance;

                index = i;
            }
        }

        Vector3 heading = mainObjectPosition - wayPoints [index].wayPoint.position;

        float distance = heading.magnitude;

        Vector3 directionToPoint = heading / distance;

        // ("player: "+directionToPoint + "-direction: "+wayPoints [index].direction.forward);
        //check if the vectors point in the same direction or not

        float angle = Vector3.Dot (directionToPoint, wayPoints [index].direction.forward);

        //print (angle);
        //		if (angle < 0) {
        //			print ("different direction");
        //		}

        //if the vectors point in different directions, it means that the player is close 
        //to a waypoint in the opposite forward direction of the hoverboard waypoints,
        //so increase the index in 1 to move the player to the correct waypoint position, 
        //according to the forward direction used to the waypoints


        bool movingInForwardDirectionResult = true;

        if (wayPointsManager.allowMovementOnBothDirections) {
            float vehicleAngle = Vector3.SignedAngle (transform.forward, wayPoints [index].direction.forward, getCurrentNormal ());

            if (Mathf.Abs (vehicleAngle) > 90) {
                index--;

                if (index <= 0) {
                    stopMoveThroughWayPointsCoroutine ();
                }

                movingInForwardDirectionResult = false;
            }
        }

        if (movingInForwardDirectionResult) {
            if (angle > 0) {
                //print ("same direcion");
                index++;

                if (index > wayPoints.Count - 1) {
                    stopMoveThroughWayPointsCoroutine ();
                }
            }
        }

        List<Transform> currentPath = new List<Transform> ();

        if (movingInForwardDirectionResult) {
            for (int i = index; i < wayPoints.Count; i++) {
                currentPath.Add (wayPoints [i].direction);
            }
        } else {
            for (int i = index; i >= 0; i--) {
                currentPath.Add (wayPoints [i].direction);
            }
        }

        if (index - 1 >= 0) {
            index--;
        } else {
            index = 0;
        }

        bool modifyMovementSpeedEnabled = wayPointsManager.modifyMovementSpeedEnabled;

        float maxMovementSpeed = wayPointsManager.maxMovementSpeed;

        float minMovementSpeed = wayPointsManager.minMovementSpeed;

        float modifyMovementSpeed = wayPointsManager.modifyMovementSpeed;

        float movementSpeed = wayPointsManager.movementSpeed;

        Vector3 extraYRotation = Vector3.zero;

        if (movingInForwardDirectionResult) {
            extraYRotation = wayPoints [index].direction.eulerAngles + hoverboardWaypointsExtraRotation * transform.up;
        }

        Quaternion targetRotation = Quaternion.Euler (extraYRotation);

        foreach (Transform transformPath in currentPath) {
            Vector3 targetPosition = transformPath.transform.position;

            if (transformPath == currentPath [currentPath.Count - 1]) {
                targetPosition += 2 * transformPath.forward;
            }

            if (!movingInForwardDirectionResult) {
                extraYRotation = -transformPath.eulerAngles + hoverboardWaypointsExtraRotation * transform.up;

                targetRotation = Quaternion.Euler (extraYRotation);
            }

            bool targetReached = false;

            while (!targetReached) {

                if (modifyMovementSpeedEnabled) {
                    currentVerticalDirection = getVerticalAxis ();

                    if (currentVerticalDirection > 0) {
                        speedMultiplier = Mathf.Lerp (speedMultiplier, maxMovementSpeed, Time.deltaTime * modifyMovementSpeed);
                    } else if (currentVerticalDirection < 0) {
                        speedMultiplier = Mathf.Lerp (speedMultiplier, minMovementSpeed, Time.deltaTime * modifyMovementSpeed);
                    } else {
                        speedMultiplier = Mathf.Lerp (speedMultiplier, 1, Time.deltaTime * modifyMovementSpeed);
                    }
                }

                currentMovementSpeed = speedMultiplier * movementSpeed;

                Vector3 vehicleTransformTargetPosition = targetPosition;

                if (useCustomHoverboardCenterPoint) {
                    vehicleTransformTargetPosition += customHoverboardCenterPoint;
                }

                transform.position =
                    Vector3.MoveTowards (transform.position, vehicleTransformTargetPosition, Time.deltaTime * currentMovementSpeed);

                transform.rotation =
                    Quaternion.Slerp (transform.rotation, targetRotation, Time.deltaTime * currentMovementSpeed);

                mainVehicleCameraController.transform.position =
                    Vector3.MoveTowards (mainVehicleCameraController.transform.position, transform.position, Time.deltaTime * currentMovementSpeed);


                float currentDistance = GKC_Utils.distance (transform.position, vehicleTransformTargetPosition);

                if (currentDistance < .01f) {
                    targetReached = true;
                }

                yield return null;
            }

            if (movingInForwardDirectionResult) {
                extraYRotation = transformPath.eulerAngles + hoverboardWaypointsExtraRotation * transform.up;

                targetRotation = Quaternion.Euler (extraYRotation);
            }
        }

        pickOrReleaseHoverboardVehicle (false, true);
    }

    public void getVehicleComponents (GameObject mainVehicleObject)
    {
        mainVehicleHUDManager = mainVehicleObject.GetComponentInChildren<vehicleHUDManager> ();

        mainVehicleCameraController = mainVehicleObject.GetComponentInChildren<vehicleCameraController> ();

        mainVehicleGravityControl = mainVehicleObject.GetComponentInChildren<vehicleGravityControl> ();

        mainRigidbody = mainVehicleHUDManager.gameObject.GetComponent<Rigidbody> ();

        mainIKDrivingSystem = mainVehicleObject.GetComponentInChildren<IKDrivingSystem> ();

        mainInputActionManager = mainVehicleObject.GetComponentInChildren<inputActionManager> ();

        GKC_Utils.updateComponent (this);
    }

    [System.Serializable]
    public class vehicleControllerSettingsInfo
    {
        public float maxForwardSpeed;

        [Space]

        public bool canUseBoost;

        public bool canUseBoostWithoutVerticalInput;

        public float maxBoostMultiplier;

        public bool useBoostCameraShake = true;
        public bool moveCameraAwayOnBoost = true;
        public string boostCameraShakeStateName = "Boost";

        [Space]

        public bool canJump;

        public float jumpPower;

        [Space]

        public bool canImpulseHoldingJump;
        public float impulseForce;
        public bool impulseUseEnergy;
        public float impulseUseEnergyRate;
        public float impulseUseEnergyAmount;

        [Space]

        public bool autoBrakeOnGetOff;

        public bool autoBrakeIfVehicleDestroyed;
    }
}