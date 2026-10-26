using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class freeFallCharacterActivator : MonoBehaviour
{
    [Header ("Main Settings")]
    [Space]

    public bool checkCharacterFallEnabled = true;

    public float minTimeOnAirToActivateFreeFall = 2;

    public int regularAirID = -1;
    public int freeFallID = 3;

    [Space]

    public bool useMinFallSpeedToActivateState;
    public float minFallSpeedToActivateState;

    public bool avoidFallDamageOnFreeFall;

    public bool avoidFallDamageOnFreeFallOnlyOnTurbo;

    [Space]

    public Vector3 capsuleColliderCenter = new Vector3 (0, 1, 0);

    [Space]
    [Header ("Other Settings")]
    [Space]

    public bool setNewCameraStateOnFreeFallActive;

    public string newCameraStateOnFreeFall;

    [Space]
    [Header ("Turbo Settings")]
    [Space]

    public bool fallTurboEnabled = true;
    public float fallTurboMultiplier = 2;

    public bool useCameraShake;
    public string regularCameraShakeName;

    public bool useMaxFallSpeed;

    public float maxFallSpeed;

    [Space]

    public bool ignoreFreeFallIDIfNotTurboActive;

    [Space]

    public bool useEventOnLandingWithTurbo;
    public UnityEvent eventOnLandingWithTurbo;

    [Space]
    [Header ("Debug")]
    [Space]

    public bool showDebugPrint;

    public bool checkingFreeFall;

    public bool freeFallActive;

    public bool freeFallPaused;

    public bool fallTurboActive;

    [Space]
    [Header ("Event Settings")]
    [Space]

    public bool useEventsOnFreeFallStateChange;
    public UnityEvent eventOnFreeFallActive;
    public UnityEvent eventOnFreeFallDeactivate;

    [Space]

    public bool useEventOnTurboState;
    public UnityEvent eventOnEnableTurbo;
    public UnityEvent eventOnDisableTurbo;

    [Space]
    [Header ("Components")]
    [Space]

    public playerCamera mainPlayerCamera;

    public playerController mainPlayerController;

    float lastTimeFalling;

    float lastTimeJump;

    string previousCameraState;

    bool resetCheckOnFreeFallActive;

    void Update ()
    {
        if (checkCharacterFallEnabled) {
            if (freeFallPaused) {
                return;
            }

            if (!checkingFreeFall) {
                if (!mainPlayerController.isPlayerOnGround () &&
                    mainPlayerController.getCurrentAirID () == regularAirID &&
                    !mainPlayerController.isExternalControlBehaviorForAirTypeActive () &&
                    !mainPlayerController.isPlayerDriving ()) {

                    checkingFreeFall = true;

                    lastTimeFalling = Time.time;
                }
            } else {
                if (!freeFallActive) {
                    if (Time.time > minTimeOnAirToActivateFreeFall + lastTimeFalling && checkFallSpeed ()) {

                        if (mainPlayerController.getCurrentAirID () == regularAirID) {
                            setFreeFallState (true);
                        } else {
                            checkingFreeFall = false;
                        }
                    } else {
                        if (checkIfPlayerBusy ()) {

                            setFreeFallState (false);
                        }
                    }
                } else {
                    if (mainPlayerController.isPlayerOnGround () ||
                        mainPlayerController.isPlayerAiming () ||
                        lastTimeJump != mainPlayerController.getLastDoubleJumpTime () ||
                        mainPlayerController.isExternalControlBehaviorForAirTypeActive () ||
                        mainPlayerController.isActionActive () ||
                        resetCheckOnFreeFallActive) {

                        resetRegularPlayerValues ();

                        disableFreeFallActiveState ();
                    }

                    bool disableFreeFallActiveStateResult = false;

                    if (ignoreFreeFallIDIfNotTurboActive) {
                        if (fallTurboActive) {
                            if (mainPlayerController.getCurrentAirID () != freeFallID) {
                                disableFreeFallActiveStateResult = true;
                            }
                        } else {
                            if (mainPlayerController.getCurrentAirID () != regularAirID) {
                                disableFreeFallActiveStateResult = true;
                            }
                        }
                    } else {
                        if (mainPlayerController.getCurrentAirID () != freeFallID) {
                            disableFreeFallActiveStateResult = true;
                        }
                    }

                    if (disableFreeFallActiveStateResult) {
                        disableFreeFallActiveState ();
                    }
                }
            }
        }
    }

    bool checkIfPlayerBusy ()
    {
        if (mainPlayerController.isPlayerOnGround () ||
                            mainPlayerController.isActionActive () ||
                            mainPlayerController.isGravityPowerActive () ||
                            mainPlayerController.isPlayerOnFFOrZeroGravityModeOn () ||
                            mainPlayerController.isChoosingGravityDirection () ||
                            mainPlayerController.isGravityForcePaused () ||
                            mainPlayerController.isWallRunningActive () ||
                            mainPlayerController.isSwimModeActive () ||
                            mainPlayerController.isSphereModeActive () ||
                            mainPlayerController.isExternalControlBehaviorForAirTypeActive () ||
                            mainPlayerController.isPlayerDriving () ||
                            mainPlayerController.isSlowFallExternallyActive ()) {

            return true;
        }

        return false;
    }

    public void disableFreeFallActiveState ()
    {
        resetCheckOnFreeFallActive = false;

        setFreeFallState (false);
    }

    public void setFreeFallPausedState (bool state)
    {
        if (!state) {
            if (freeFallActive) {
                resetRegularPlayerValues ();

                disableFreeFallActiveState ();
            }
        }

        freeFallPaused = state;
    }

    public bool checkFallSpeed ()
    {
        if (useMinFallSpeedToActivateState) {
            if (Mathf.Abs (mainPlayerController.getVerticalSpeed ()) > minFallSpeedToActivateState) {
                return true;
            } else {
                return false;
            }
        }

        return true;
    }

    public void setResetCheckOnFreeFallActiveState (bool state)
    {
        resetCheckOnFreeFallActive = state;
    }

    public void inputEnableOrDisableFallTurbo (bool state)
    {
        if (!fallTurboEnabled) {
            return;
        }

        if (freeFallActive) {
            setTurboState (state);
        }
    }

    void setTurboState (bool state)
    {
        if (fallTurboActive == state) {
            return;
        }

        fallTurboActive = state;

        if (fallTurboActive) {
            mainPlayerController.setGravityMultiplierValueFromExternalFunction (fallTurboMultiplier);

            mainPlayerController.setCurrentAirSpeedValue (2);

            if (useCameraShake) {
                mainPlayerCamera.setShakeCameraState (true, regularCameraShakeName);
            }
        } else {
            mainPlayerController.setGravityMultiplierValue (true, 0);

            mainPlayerController.setCurrentAirSpeedValue (1);

            if (useCameraShake) {
                mainPlayerCamera.setShakeCameraState (false, "");
            }
        }

        if (avoidFallDamageOnFreeFall && avoidFallDamageOnFreeFallOnlyOnTurbo) {
            mainPlayerController.setFallDamageCheckOnHealthPausedState (state);
        }

        if (useMaxFallSpeed) {
            mainPlayerController.setUseMaxFallSpeedExternallyActiveState (state);

            if (state) {
                mainPlayerController.setCustomMaxFallSpeedExternally (maxFallSpeed);
            } else {
                mainPlayerController.setCustomMaxFallSpeedExternally (0);
            }
        }

        if (ignoreFreeFallIDIfNotTurboActive) {
            if (fallTurboActive) {
                setAirValueAndCapsuleState (true);
            } else {
                setAirValueAndCapsuleState (false);
            }
        }

        checkEventsOnTurboState (state);
    }

    void checkEventsOnTurboState (bool state)
    {
        if (useEventOnTurboState) {
            if (state) {
                eventOnEnableTurbo.Invoke ();
            } else {
                eventOnDisableTurbo.Invoke ();
            }
        }
    }

    void setFreeFallState (bool state)
    {
        if (state) {
            freeFallActive = true;

            if (!ignoreFreeFallIDIfNotTurboActive) {
                setAirValueAndCapsuleState (true);
            }

            lastTimeJump = mainPlayerController.getLastDoubleJumpTime ();

            if (setNewCameraStateOnFreeFallActive) {
                previousCameraState = mainPlayerCamera.getCurrentStateName ();

                mainPlayerCamera.setCameraStateOnlyOnThirdPerson (newCameraStateOnFreeFall);
            }

            if (avoidFallDamageOnFreeFall && !avoidFallDamageOnFreeFallOnlyOnTurbo) {
                mainPlayerController.setFallDamageCheckOnHealthPausedState (true);
            }

        } else {
            if (avoidFallDamageOnFreeFall) {
                mainPlayerController.setFallDamageCheckOnHealthPausedState (false);
            }

            if (fallTurboActive) {
                if (useEventOnLandingWithTurbo) {
                    if (mainPlayerController.checkIfPlayerOnGroundWithRaycast () || mainPlayerController.isPlayerOnGround ()) {
                        eventOnLandingWithTurbo.Invoke ();
                    }
                }

                setTurboState (false);
            }

            checkingFreeFall = false;

            freeFallActive = false;

            if (setNewCameraStateOnFreeFallActive) {
                if (previousCameraState != "") {
                    mainPlayerCamera.setCameraStateOnlyOnThirdPerson (previousCameraState);

                    previousCameraState = "";
                }
            }

            if (useMaxFallSpeed) {
                mainPlayerController.setUseMaxFallSpeedExternallyActiveState (false);

                mainPlayerController.setCustomMaxFallSpeedExternally (0);
            }
        }

        checkEventsOnFreeFallStateChange (state);

        if (showDebugPrint) {
            print ("setFreeFallState " + state);
        }
    }

    public void stopFreeFallStateIfActive ()
    {
        if (freeFallActive) {
            resetRegularPlayerValues ();

            disableFreeFallActiveState ();
        }
    }

    void resetRegularPlayerValues ()
    {
        if (mainPlayerController.getCurrentAirID () == freeFallID) {
            setAirValueAndCapsuleState (false);
        }
    }

    void setAirValueAndCapsuleState (bool state)
    {
        if (state) {
            mainPlayerController.setCurrentAirIDValue (freeFallID);

            mainPlayerController.setPlayerCapsuleColliderDirection (2);

            mainPlayerController.setPlayerColliderCapsuleCenter (capsuleColliderCenter);
        } else {
            mainPlayerController.setCurrentAirIDValue (regularAirID);

            mainPlayerController.setPlayerCapsuleColliderDirection (1);

            mainPlayerController.setOriginalPlayerColliderCapsuleScale ();
        }
    }

    void checkEventsOnFreeFallStateChange (bool state)
    {
        if (useEventsOnFreeFallStateChange) {
            if (state) {
                eventOnFreeFallActive.Invoke ();
            } else {
                eventOnFreeFallDeactivate.Invoke ();
            }
        }
    }
}
