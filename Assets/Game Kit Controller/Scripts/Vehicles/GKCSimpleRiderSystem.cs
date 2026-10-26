using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class GKCSimpleRiderSystem : mainRiderSystem
{
    [Header ("Main Settings")]
    [Space]

    public string vehicleName;

    public bool playerIsAlwaysDriver = true;

    public float vehicleRadius = 3;

    public bool playerVisibleInVehicle = true;

    public bool resetCameraRotationWhenGetOff;

    public int resetAnimatorDrivingStateID = 666777;

    [Space]

    public LayerMask obstacleLayer;

    public bool checkIfDriverIsOnGroundOnGetOff;

    public bool ignoreObstacleCheckOnGetOffEnabled;

    [Space]
    [Header ("Weapon Settings")]
    [Space]

    public bool hidePlayerWeaponsWhileDriving;

    public bool drawFireWeaponIfCarryingPreviously = true;
    public bool drawMeleeWeaponIfCarryingPreviously = true;

    [Space]
    [Header ("Vehicle Weapon Settings")]
    [Space]

    public bool useVehicleWeaponsEnabled;

    public bool vehicleWeaponSystemUseRotationInput;
    public Transform defaultVehicleWeaponSystemCameraTransform;

    [Space]
    [Header ("UI Settings")]
    [Space]

    public bool disablePlayerHUDEnabled = true;

    public bool showVehicleHUD;

    [Space]
    [Header ("GameObject Settings")]
    [Space]

    public GameObject vehicleGameObject;

    public Transform passengerParent;

    public GameObject customVehicleGameObject;

    public Transform vehicleCameraDirection;

    [Space]
    [Header ("Ride Object On Start Settings")]
    [Space]

    public bool startGameInThisVehicle;

    public bool setCurrentPlayerManually;

    public bool searchPlayerOnSceneIfNotAssigned = true;

    public GameObject playerForVehicle;

    [Space]
    [Header ("Rider Single Driver Settings")]
    [Space]

    public IKDrivingSystem.IKDrivingInformation IKRiderInfo;

    [Space]
    [Header ("Rider Driver And Passengers Settings")]
    [Space]

    public List<IKDrivingSystem.IKDrivingInformation> IKVehiclePassengersList = new List<IKDrivingSystem.IKDrivingInformation> ();

    [Space]
    [Header ("Override System Settings")]
    [Space]

    public bool objectUseOverrideSystem;

    public objectToOverrideSystem mainObjectToOverrideSystem;

    [Space]
    [Header ("Gravity Settings")]
    [Space]

    public bool changeDriverGravityWhenGetsOff = true;
    public Vector3 regularGravity = new Vector3 (0, 1, 0);
    public Vector3 currentNormal = new Vector3 (0, 1, 0);

    public float minAngleDifferenceToRegularGravityToChangeWhenGetOff = 40;

    public bool checkVehicleUpDirectionForGravityOnGetOff;

    [Space]
    [Header ("Other Settings")]
    [Space]

    public List<Collider> colliderParts = new List<Collider> ();

    [Space]
    [Header ("Debug")]
    [Space]

    public bool showDebugPrint;

    public bool riderActive;

    public bool destroyed;

    public GameObject currentRiderGameObject;

    public bool isBeingDriven;

    public bool passengerIsGettingOn;

    public List<GameObject> passengerGameObjectList = new List<GameObject> ();

    public List<IKDrivingSystem.passengerComponents> passengerComponentsList = new List<IKDrivingSystem.passengerComponents> ();

    [Space]
    [Header ("Event Settings")]
    [Space]

    public bool useEventOnDriverGetOn;
    public eventParameters.eventToCallWithGameObject eventOnDriverGetOn;

    public bool useEventOnDriverGetOff;
    public eventParameters.eventToCallWithGameObject eventOnDriverGetOff;

    public bool useEventOnStartGame;
    public float delayToActivateEventsOnStart;
    public UnityEvent eventOnStartGame;

    [Space]
    [Header ("Remote Event Settings")]
    [Space]

    public bool useRemoteEventsOnPassengers;
    public List<string> remoteEventNameListGetOn = new List<string> ();
    public List<string> remoteEventNameListGetOff = new List<string> ();

    [Space]
    [Header ("Components")]
    [Space]

    public inputActionManager mainInputActionManager;

    public Rigidbody mainVehicleRigidbody;
    public vehicleWeaponSystem mainVehicleWeaponSystem;


    IKDrivingSystem.passengerComponents currentPassengerDriverComponents;

    bool checkIfPlayerStartInVehicle;

    bool playerAssignedProperly;

    Vector3 customPassengerPosition = Vector3.zero;

    bool enteringOnVehicleFromDistance;

    bool getDriverPosition;

    bool getOnlyPassengerSeatActive;

    bool mainVehicleWeaponSystemInitialized;



    void Start ()
    {
        if (useEventOnStartGame) {
            if (delayToActivateEventsOnStart > 0) {
                StartCoroutine (activateEventOnStartCoroutine ());
            } else {
                eventOnStartGame.Invoke ();
            }
        }

        StartCoroutine (checkStartGameInVehicleCoroutine ());
    }

    IEnumerator activateEventOnStartCoroutine ()
    {
        yield return new WaitForSeconds (delayToActivateEventsOnStart);

        eventOnStartGame.Invoke ();
    }

    IEnumerator checkStartGameInVehicleCoroutine ()
    {
        yield return new WaitForSeconds (0.01f);

        checkStartGameInVehicle ();
    }

    void checkStartGameInVehicle ()
    {
        if (!checkIfPlayerStartInVehicle) {
            if (startGameInThisVehicle) {
                if (setCurrentPlayerManually || playerForVehicle == null) {
                    if (currentRiderGameObject == null) {
                        if (!playerAssignedProperly) {
                            findPlayerOnScene ();
                        }
                    }

                    playerForVehicle = currentRiderGameObject;
                }

                if (playerForVehicle != null) {
                    setDriverExternally (playerForVehicle);
                } else {
                    print ("Warning: assign the player to drive this car in the field Player For Vehicle in IK Driving System inspector");
                }
            }

            checkIfPlayerStartInVehicle = true;
        }

    }

    public void setPlayerToStartGameOnThisVehicle (GameObject newPlayer)
    {
        startGameInThisVehicle = true;

        playerForVehicle = newPlayer;

        checkIfPlayerStartInVehicle = false;
    }

    public void setDriverExternally (GameObject currentDriver)
    {
        currentRiderGameObject = currentDriver;

        if (currentRiderGameObject == null) {
            return;
        }

        usingDevicesSystem usingDevicesManager = currentDriver.GetComponent<usingDevicesSystem> ();

        if (usingDevicesManager != null) {
            if (usingDevicesManager.usedByAI) {
                getDriverPosition = true;

                GKC_Utils.setAICharacterOnVehicle (vehicleGameObject, currentDriver);

                getDriverPosition = false;
            } else {
                usingDevicesManager.clearDeviceList ();

                usingDevicesManager.addDeviceToList (vehicleGameObject);

                usingDevicesManager.updateClosestDeviceList ();

                usingDevicesManager.setCurrentVehicle (vehicleGameObject);

                getDriverPosition = true;

                usingDevicesManager.useCurrentDevice (vehicleGameObject);

                getDriverPosition = false;
            }
        }
    }

    public void setPassengerExternally (GameObject currentDriver)
    {
        usingDevicesSystem usingDevicesManager = currentDriver.GetComponent<usingDevicesSystem> ();

        if (usingDevicesManager != null) {
            if (usingDevicesManager.usedByAI) {
                getDriverPosition = false;

                getOnlyPassengerSeatActive = true;

                GKC_Utils.setAICharacterOnVehicle (vehicleGameObject, currentDriver);

                getOnlyPassengerSeatActive = false;
            } else {
                usingDevicesManager.clearDeviceList ();

                usingDevicesManager.addDeviceToList (vehicleGameObject);

                usingDevicesManager.updateClosestDeviceList ();

                usingDevicesManager.setCurrentVehicle (vehicleGameObject);

                getDriverPosition = false;

                getOnlyPassengerSeatActive = true;

                bool originalPlayerIsAlwaysDriverValue = playerIsAlwaysDriver;

                playerIsAlwaysDriver = false;

                usingDevicesManager.useCurrentDevice (vehicleGameObject);

                playerIsAlwaysDriver = originalPlayerIsAlwaysDriverValue;

                getOnlyPassengerSeatActive = false;
            }
        }
    }

    public void stopDriveVehicleExternally ()
    {
        if (isBeingDriven) {
            if (currentPassengerDriverComponents != null) {
                if (currentPassengerDriverComponents.usingDevicesManager != null) {
                    currentPassengerDriverComponents.usingDevicesManager.clearDeviceList ();

                    currentPassengerDriverComponents.usingDevicesManager.addDeviceToList (vehicleGameObject);

                    currentPassengerDriverComponents.usingDevicesManager.setCurrentVehicle (vehicleGameObject);

                    currentPassengerDriverComponents.usingDevicesManager.useCurrentDevice (vehicleGameObject);
                }
            }
        }
    }

    public void activateDevice ()
    {
        if (isVehicleOnlyForOnePlayerActive ()) {
            return;
        }

        addOrRemovePassengerToVehicle ();
    }

    void addOrRemovePassengerToVehicle ()
    {
        if (isVehicleFull () && !passengerGameObjectList.Contains (currentRiderGameObject)) {
            return;
        }


        startOrStopVehicle (true);

        riderActive = isBeingDriven;
    }

    public void setRiderState (bool state)
    {
        if (riderActive == state) {
            return;
        }

        if (currentRiderGameObject == null) {
            if (!playerAssignedProperly) {
                findPlayerOnScene ();

                if (currentRiderGameObject == null) {
                    print ("WARNING: no player controller has been assigned to the mission." +
                    " Make sure to use a trigger to activate the mission or assign the player manually");

                    return;
                }
            }

            return;
        }

        riderActive = state;

        if (showDebugPrint) {
            print ("setting rider active state" + state);
        }

        if (IKVehiclePassengersList == null || IKVehiclePassengersList.Count == 0) {
            if (!IKVehiclePassengersList.Contains (IKRiderInfo)) {
                IKVehiclePassengersList.Add (IKRiderInfo);
            }
        }

        if (objectUseOverrideSystem) {
            startOrStopObjectToOverride (riderActive);
        } else {
            startOrStopVehicle (riderActive);
        }
    }

    public void startOrStopObjectToOverride (bool state)
    {
        setPassengerInfo (IKRiderInfo, currentRiderGameObject);

        passengerIsGettingOn = false;

        IKDrivingSystem.IKDrivingInformation currentIKVehiclePassengerInfo = getIKVehiclePassengerInfo (currentRiderGameObject);

        int currentPassengerIndex = passengerGameObjectList.IndexOf (currentRiderGameObject);
        IKDrivingSystem.passengerComponents currentPassengerComponents = passengerComponentsList [currentPassengerIndex];

        passengerIsGettingOn = currentIKVehiclePassengerInfo.vehicleSeatInfo.seatIsFree;

        currentIKVehiclePassengerInfo.vehicleSeatInfo.seatIsFree = !passengerIsGettingOn;

        currentIKVehiclePassengerInfo.adjustPassengerPositionActive = true;
        currentIKVehiclePassengerInfo.currentDriveIkWeightValue = 1;
        currentIKVehiclePassengerInfo.disableIKOnPassengerSmoothly = false;

        currentPassengerComponents.playerControllerManager.setActionToGetOnVehicleActiveState (false);
        currentPassengerComponents.playerControllerManager.setActionToGetOffFromVehicleActiveState (false);

        if (showDebugPrint) {
            print ("passenger is getting on " + passengerIsGettingOn);
        }

        bool passengerOnDriverSeat = false;

        if (currentIKVehiclePassengerInfo.vehicleSeatInfo.isDriverSeat) {
            passengerOnDriverSeat = true;

            currentPassengerDriverComponents = currentPassengerComponents;
        }

        if (showDebugPrint) {
            print ("is driver seat " + passengerOnDriverSeat);
        }

        currentPassengerComponents.passengerOnDriverSeat = passengerOnDriverSeat;

        isBeingDriven = false;

        if (passengerOnDriverSeat) {
            isBeingDriven = passengerIsGettingOn;
        } else {
            isBeingDriven = false;
        }

        if (showDebugPrint) {
            print (passengerIsGettingOn + " " + passengerOnDriverSeat + " " + isBeingDriven);
        }

        bool isPlayer = false;

        if (!currentPassengerComponents.playerControllerManager.usedByAI) {
            isPlayer = true;
        }

        if (vehicleCameraDirection == null) {
            vehicleCameraDirection = vehicleGameObject.transform;
        }

        //set the state driving as the current state of the player
        currentPassengerComponents.playerControllerManager.setDrivingState (passengerIsGettingOn, vehicleGameObject, vehicleName, vehicleCameraDirection);

        currentPassengerComponents.passengerPhysicallyOnVehicle = true;

        currentPassengerComponents.playerControllerManager.setUsingDeviceState (passengerIsGettingOn);

        currentPassengerComponents.playerControllerManager.setUsingGeneralDriveRiderSystem (passengerIsGettingOn);

        if (passengerIsGettingOn) {
            if (currentPassengerComponents.carryingMeleeWeaponsPreviously) {
                GKC_Utils.checkIfDropThrownWeaponWhenUsingDevice (currentPassengerComponents.currentPassenger);

                if (currentPassengerComponents.playerControllerManager.isPlayerUsingMeleeWeapons ()) {
                    currentPassengerComponents.statesManager.disableGrabMode ();
                } else {
                    currentPassengerComponents.carryingMeleeWeaponsPreviously = false;
                }
            }
        }

        if (isPlayer) {
            currentPassengerComponents.pauseManager.usingDeviceState (passengerIsGettingOn);

            currentPassengerComponents.mainInventoryManager.enableOrDisableWeaponSlotsParentOutOfInventory (!passengerIsGettingOn);
        }

        if (isPlayer) {
            if (disablePlayerHUDEnabled) {
                //enable and disable the player's HUD and the vehicle's HUD
                currentPassengerComponents.mainPlayerHUDManager.enableOrDisableHUD (!passengerIsGettingOn);
            }

            if (showVehicleHUD) {
                currentPassengerComponents.mainPlayerHUDManager.enableOrDisableVehicleHUD (isBeingDriven);
            }

            if (passengerOnDriverSeat) {
                if (passengerIsGettingOn) {
                    if (useEventOnDriverGetOn) {
                        eventOnDriverGetOn.Invoke (currentPassengerComponents.currentPassenger);
                    }
                } else {
                    if (useEventOnDriverGetOff) {
                        eventOnDriverGetOff.Invoke (currentPassengerComponents.currentPassenger);
                    }
                }
            }
        }

        currentPassengerComponents.mainFootStepManager.enableOrDisableFootSteps (!passengerIsGettingOn);

        currentPassengerComponents.healthManager.setSliderVisibleState (!passengerIsGettingOn);

        currentPassengerComponents.healthManager.setDrivingState (passengerIsGettingOn);

        currentPassengerComponents.usingDevicesManager.setUseDeviceButtonEnabledState (true);

        //if the player is driving it
        if (passengerIsGettingOn) {
            //check the current state of the player, to check if he is carrying an object, aiming, etc... to disable that state
            currentPassengerComponents.statesManager.checkPlayerStates ();

            if (currentPassengerComponents.mainHideCharacterFixedPlaceSystemPlayerManagement != null) {
                currentPassengerComponents.mainHideCharacterFixedPlaceSystemPlayerManagement.removeCurrentHideFromEnemiesSystem ();
            }

            //check the current state of the player, to check if he is carrying an object, aiming, etc... to disable that state
            currentPassengerComponents.usingDevicesManager.setDrivingState (passengerIsGettingOn);

            currentPassengerComponents.usingDevicesManager.clearDeviceListButOne (vehicleGameObject);

            currentPassengerComponents.usingDevicesManager.checkIfSetOriginalShaderToPreviousDeviceFound (vehicleGameObject);

            if (currentIKVehiclePassengerInfo.useAnimationActionIDOnDriveIdle) {
                currentPassengerComponents.mainPlayerActionSystem.setNewActionIDExternally (currentIKVehiclePassengerInfo.animationActionID);
            }
        }

        //the player gets off from the vehicle
        else {
            currentPassengerComponents.playerControllerManager.setTemporalVehicle (null);

            if (isPlayer && passengerOnDriverSeat) {
                currentPassengerComponents.mainPlayerHUDManager.setCurrentVehicleHUD (null);
            }

            if (currentIKVehiclePassengerInfo.useAnimationActionIDOnDriveIdle) {
                currentPassengerComponents.mainPlayerActionSystem.setNewActionIDExternally (resetAnimatorDrivingStateID);
            } else {
                currentPassengerComponents.mainPlayerActionSystem.setNewActionIDExternally (0);
            }

            if (isPlayer) {
                currentPassengerComponents.playerCameraManager.setFBAPivotCameraTransformParent (null);

                if (currentPassengerComponents.playerCameraManager.isUpdateFBAPivotCameraTransformInsideHeadPositionActive ()) {
                    currentPassengerComponents.playerCameraManager.setUpdateFBAPivotCameraTransformInsideHeadPositionActiveState (true);
                }
            }

            currentPassengerComponents.usingDevicesManager.setDrivingState (passengerIsGettingOn);
        }

        if (hidePlayerWeaponsWhileDriving) {
            currentPassengerComponents.playerWeaponManager.enableOrDisableEnabledWeaponsMesh (!passengerIsGettingOn);

            if (currentPassengerComponents.mainMeleeWeaponsGrabbedManager != null) {
                currentPassengerComponents.mainMeleeWeaponsGrabbedManager.enableOrDisableAllMeleeWeaponMeshesOnCharacterBodyCheckingIfHiddingMeshes (!passengerIsGettingOn);

                currentPassengerComponents.mainMeleeWeaponsGrabbedManager.enableOrDisableAllMeleeWeaponShieldMeshesOnCharacterBodyCheckingIfHiddingMeshes (!passengerIsGettingOn);
            }
        }

        if (!passengerIsGettingOn) {
            removePassengerInfo (currentIKVehiclePassengerInfo);
        }

        if (!passengerIsGettingOn) {
            currentPassengerComponents.playerCameraManager.updateFBAPivotCameraTransformToPivotCameraTransform ();
        }

        if (useRemoteEventsOnPassengers) {
            if (currentPassengerComponents.mainRemoteEventSystem != null) {
                if (passengerIsGettingOn) {
                    for (int i = 0; i < remoteEventNameListGetOn.Count; i++) {
                        currentPassengerComponents.mainRemoteEventSystem.callRemoteEvent (remoteEventNameListGetOn [i]);
                    }
                } else {
                    for (int i = 0; i < remoteEventNameListGetOff.Count; i++) {
                        currentPassengerComponents.mainRemoteEventSystem.callRemoteEvent (remoteEventNameListGetOff [i]);
                    }
                }
            }
        }

        if (mainInputActionManager != null && isPlayer && passengerOnDriverSeat) {
            mainInputActionManager.enableOrDisableInput (isBeingDriven, currentPassengerComponents.currentPassenger);
        }

        mainObjectToOverrideSystem.setCurrentPlayer (currentPassengerComponents.currentPassenger);

        mainObjectToOverrideSystem.setOverrideControlExternally (isBeingDriven);

        if (isPlayer) {
            checkVehicleWeaponsElements (currentPassengerComponents);
        }
    }


    //the player is getting in or off from the vehicle
    public void startOrStopVehicle (bool state)
    {
        Vector3 nextPlayerPos = getPassengerGetOffPosition (currentRiderGameObject);

        IKDrivingSystem.IKDrivingInformation currentIKVehiclePassengerInfo = getIKVehiclePassengerInfo (currentRiderGameObject);

        int currentPassengerIndex = passengerGameObjectList.IndexOf (currentRiderGameObject);

        if (currentPassengerIndex < 0) {
            if (showDebugPrint) {
                print ("Warning: the passenger wasn't found on the vehicle when trying to enter and get its index info");
            }

            return;
        }

        IKDrivingSystem.passengerComponents currentPassengerComponents = passengerComponentsList [currentPassengerIndex];

        if (nextPlayerPos == -Vector3.one || nextPlayerPos == Vector3.zero) {
            if (currentIKVehiclePassengerInfo.vehicleSeatInfo.getOffPlace == IKDrivingSystem.seatInfo.getOffSide.right) {
                nextPlayerPos = currentIKVehiclePassengerInfo.vehicleSeatInfo.rightGetOffPosition.position;
            } else {
                nextPlayerPos = currentIKVehiclePassengerInfo.vehicleSeatInfo.leftGetOffPosition.position;
            }
        }

        if (customPassengerPosition != Vector3.zero) {
            nextPlayerPos = customPassengerPosition;

            customPassengerPosition = Vector3.zero;
        }

        passengerIsGettingOn = false;

        passengerIsGettingOn = currentIKVehiclePassengerInfo.vehicleSeatInfo.seatIsFree;

        currentIKVehiclePassengerInfo.vehicleSeatInfo.seatIsFree = !passengerIsGettingOn;

        currentIKVehiclePassengerInfo.adjustPassengerPositionActive = true;
        currentIKVehiclePassengerInfo.currentDriveIkWeightValue = 1;
        currentIKVehiclePassengerInfo.disableIKOnPassengerSmoothly = false;

        currentPassengerComponents.playerControllerManager.setActionToGetOnVehicleActiveState (false);
        currentPassengerComponents.playerControllerManager.setActionToGetOffFromVehicleActiveState (false);

        if (showDebugPrint) {
            print ("passenger is getting on " + passengerIsGettingOn);
        }

        bool passengerOnDriverSeat = false;

        if (currentIKVehiclePassengerInfo.vehicleSeatInfo.isDriverSeat) {
            passengerOnDriverSeat = true;

            currentPassengerDriverComponents = currentPassengerComponents;
        }

        if (showDebugPrint) {
            print ("is driver seat " + passengerOnDriverSeat);
        }

        currentPassengerComponents.passengerOnDriverSeat = passengerOnDriverSeat;

        if (passengerOnDriverSeat) {
            isBeingDriven = passengerIsGettingOn;
        }

        if (showDebugPrint) {
            print (passengerIsGettingOn + " " + passengerOnDriverSeat + " " + isBeingDriven);
        }

        bool isPlayer = false;

        if (!currentPassengerComponents.playerControllerManager.usedByAI) {
            isPlayer = true;
        }

        //CHECK IN THE ACTION SYSTEM IS USED TO PLAY AN ANIMATION TO ENTER-EXIT THE VEHICLE

        bool useActionSystemToEnterExitSeat = currentIKVehiclePassengerInfo.vehicleSeatInfo.useActionSystemToEnterExitSeat;

        if (enteringOnVehicleFromDistance) {
            useActionSystemToEnterExitSeat = false;

            enteringOnVehicleFromDistance = false;
        }

        bool firstCameraEnabled = currentPassengerComponents.playerCameraManager.isFirstPersonActive ();

        bool canUseActionSystem = false;

        if (useActionSystemToEnterExitSeat) {
            canUseActionSystem = true;
        }

        if (passengerIsGettingOn) {
            if (firstCameraEnabled) {
                canUseActionSystem = false;
            }
        }

        if (canUseActionSystem) {
            stopEnterExitActionSystemOnPassengerCoroutine (currentPassengerComponents.actionSystemCoroutine);

            bool enterExitActionInProcess = currentIKVehiclePassengerInfo.vehicleSeatInfo.enterExitActionInProcess;

            currentPassengerComponents.actionSystemCoroutine =
                StartCoroutine (enterExitActionSystemOnPassengerCoroutine (currentPassengerComponents.playerControllerManager.gameObject, passengerParent, nextPlayerPos,
                currentPassengerComponents, currentIKVehiclePassengerInfo, passengerIsGettingOn, isPlayer, passengerOnDriverSeat, isBeingDriven));

            if (passengerIsGettingOn) {
                return;
            } else {
                if (enterExitActionInProcess) {
                    return;
                }

                return;
            }
        } else {
            stopEnterExitActionSystemOnPassengerCoroutine (currentPassengerComponents.actionSystemCoroutine);

            setPassengerOnVehicleState (currentPassengerComponents.playerControllerManager.gameObject, passengerParent, nextPlayerPos, currentPassengerComponents,
                currentIKVehiclePassengerInfo, passengerIsGettingOn, isPlayer, passengerOnDriverSeat, false, isBeingDriven);
        }

        //		return passengerOnDriverSeat;
    }

    public void stopEnterExitActionSystemOnPassengerCoroutine (Coroutine passengerCoroutine)
    {
        if (passengerCoroutine != null) {
            StopCoroutine (passengerCoroutine);
        }
    }

    IEnumerator enterExitActionSystemOnPassengerCoroutine (GameObject currentPassenger, Transform passengerParent, Vector3 nextPlayerPos,
                                                           IKDrivingSystem.passengerComponents currentPassengerComponents, IKDrivingSystem.IKDrivingInformation currentIKVehiclePassengerInfo,
                                                           bool passengerIsGettingOn, bool isPlayer, bool passengerOnDriverSeat, bool isBeingDriven)
    {
        bool cancelingEntering = false;

        bool jumpOffFromVehicleActive = false;

        if (currentIKVehiclePassengerInfo.vehicleSeatInfo.enterExitActionInProcess) {
            if (passengerIsGettingOn) {
                print ("character entering from vehicle already in process");
            } else {
                print ("character was entering on vehicle, but that is going to be cancelled");

                cancelingEntering = true;
            }
        }

        currentIKVehiclePassengerInfo.vehicleSeatInfo.enterExitActionInProcess = true;

        bool actionActive = true;

        GameObject vehicle = getVehicleGameObject ();

        if (passengerIsGettingOn) {
            currentPassengerComponents.playerControllerManager.setTemporalVehicle (vehicle);
            currentIKVehiclePassengerInfo.vehicleSeatInfo.eventOnActionToEnter.Invoke ();

            currentIKVehiclePassengerInfo.vehicleSeatInfo.actionSystemToEnterVehicle.setPlayerActionActive (currentPassengerComponents.currentPassenger);

            currentPassengerComponents.playerControllerManager.setActionToGetOnVehicleActiveState (true);
        } else {
            if (!cancelingEntering) {
                if (!passengerIsGettingOn) {

                    float currentVehicleSpeed = 0;

                    if (mainVehicleRigidbody != null) {
                        currentVehicleSpeed = Mathf.Abs (mainVehicleRigidbody.linearVelocity.magnitude);

                    }

                    if (currentVehicleSpeed > currentIKVehiclePassengerInfo.vehicleSeatInfo.minSpeedToJumpOffFromVehicle) {
                        jumpOffFromVehicleActive = true;
                    }
                }
            }


            currentPassengerComponents.playerControllerManager.setTemporalVehicle (null);

            if (cancelingEntering) {
                currentPassengerComponents.mainPlayerActionSystem.stopAllActions ();
            } else {
                currentIKVehiclePassengerInfo.vehicleSeatInfo.eventOnActionToExit.Invoke ();

                if (jumpOffFromVehicleActive) {
                    currentIKVehiclePassengerInfo.vehicleSeatInfo.actionSystemToJumpOffFromVehicle.setPlayerActionActive (currentPassengerComponents.currentPassenger);
                } else {
                    currentIKVehiclePassengerInfo.vehicleSeatInfo.actionSystemToExitVehicle.setPlayerActionActive (currentPassengerComponents.currentPassenger);
                }
            }

            currentIKVehiclePassengerInfo.adjustPassengerPositionActive = false;
            currentIKVehiclePassengerInfo.disableIKOnPassengerSmoothly = true;

            currentPassengerComponents.playerControllerManager.enabled = true;

            currentPassengerComponents.playerControllerManager.setUsingDeviceState (false);

            currentPassengerComponents.playerControllerManager.setUsingGeneralDriveRiderSystem (false);

            currentPassengerComponents.playerControllerManager.setAnimatorState (true);

            currentPassengerComponents.mainRigidbody.isKinematic = false;

            currentPassengerComponents.playerControllerManager.setActionToGetOffFromVehicleActiveState (true);
        }

        bool cancelAction = false;

        if (!cancelingEntering) {
            if (!passengerIsGettingOn) {
                currentPassengerComponents.usingDevicesManager.setUseDeviceButtonEnabledState (false);
            }

            currentPassengerComponents.mainPlayerActionSystem.playCurrentAnimation ();

            bool passengerParentAdjusted = false;

            float currentDistanceToVehicle = 0;

            bool actionCanBeCancelled = false;

            bool cancelActionOutsideVehicle = false;

            if (jumpOffFromVehicleActive) {
                bool isCurrentPassengerCameraLocked = !currentPassengerComponents.playerCameraManager.isCameraTypeFree ();

                if (isPlayer && passengerOnDriverSeat) {
                    if (!isCurrentPassengerCameraLocked) {

                        float delayToStartJumpOff = currentIKVehiclePassengerInfo.vehicleSeatInfo.delayToStartJumpOff;

                        float timer = 0;

                        bool delayComplete = false;

                        while (!delayComplete) {

                            if (mainVehicleRigidbody != null) {
                                currentPassengerComponents.playerControllerManager.setExtraCharacterVelocity (mainVehicleRigidbody.linearVelocity);
                            }

                            timer += Time.deltaTime;

                            if (timer >= delayToStartJumpOff) {
                                delayComplete = true;
                            }

                            yield return null;
                        }

                        currentPassengerComponents.playerControllerManager.setExtraCharacterVelocity (Vector3.zero);

                        currentPassenger.transform.SetParent (null);

                        //change the main camera parent to player's camera

                        Transform playerCameraTransform = currentPassengerComponents.playerCameraGameObject.transform;

                        if (resetCameraRotationWhenGetOff) {
                            float angleY = Vector3.Angle (vehicle.transform.forward, playerCameraTransform.forward);

                            angleY *= Mathf.Sign (playerCameraTransform.InverseTransformDirection (
                                Vector3.Cross (vehicle.transform.forward, playerCameraTransform.forward)).y);

                            playerCameraTransform.Rotate (0, -angleY, 0);
                        } else {
                        }

                        currentPassengerComponents.playerCameraManager.setMainCameraParent (currentPassengerComponents.originalCameraParentTransform);
                    }
                }
            }

            while (actionActive) {
                //			print ("checking for action " + currentPassengerComponents.playerControllerManager.isActionActive () + " " + currentPassengerComponents.mainPlayerActionSystem.isPlayerWalkingToDirectionActive ());

                if (!currentPassengerComponents.playerControllerManager.isActionActive () && !currentPassengerComponents.mainPlayerActionSystem.isPlayerWalkingToDirectionActive ()) {
                    actionActive = false;
                } else {
                    if (currentPassenger != null && passengerParent != null) {
                        currentDistanceToVehicle = GKC_Utils.distance (currentPassenger.transform.position, passengerParent.position);
                    }

                    if (passengerIsGettingOn) {
                        if (!passengerParentAdjusted) {
                            if (!currentPassengerComponents.mainPlayerActionSystem.isPlayerWalkingToDirectionActive () &&
                                !currentPassengerComponents.mainPlayerActionSystem.isRotatingTowardFacingDirection ()) {
                                currentPassenger.transform.SetParent (vehicle.transform);

                                passengerParentAdjusted = true;

                                currentPassengerComponents.usingDevicesManager.setUseDeviceButtonEnabledState (false);
                            } else {
                                if (currentDistanceToVehicle > 20) {
                                    cancelActionOutsideVehicle = true;
                                    actionCanBeCancelled = true;
                                }
                            }
                        }
                    }

                    if (passengerParentAdjusted) {
                        if (mainVehicleRigidbody != null) {
                            currentPassengerComponents.playerControllerManager.setExtraCharacterVelocity (mainVehicleRigidbody.linearVelocity);
                        }
                    }

                    if (currentIKVehiclePassengerInfo.vehicleSeatInfo.cancelActionEnterExitVehicleIfSpeedTooHigh) {

                        float currentVehicleVelocity = 0;

                        if (mainVehicleRigidbody != null) {
                            currentVehicleVelocity = mainVehicleRigidbody.linearVelocity.magnitude;
                        }

                        if (Mathf.Abs (currentVehicleVelocity) > currentIKVehiclePassengerInfo.vehicleSeatInfo.minSpeedToCancelActionEnterExitVehicle) {
                            actionCanBeCancelled = true;

                            print ("Vehicle too fast, stopping the action");
                        }

                        if (passengerParentAdjusted) {
                            if (currentDistanceToVehicle > 5) {
                                print ("Too far from vehicle, cancelling");
                                actionCanBeCancelled = true;
                            }

                            cancelActionOutsideVehicle = false;
                        } else {
                            cancelActionOutsideVehicle = true;
                        }

                        if (actionCanBeCancelled) {
                            if (passengerParentAdjusted) {
                                currentPassenger.transform.SetParent (null);
                            }
                            print ("CANCELLING");

                            cancelAction = true;

                            actionActive = false;

                            currentPassengerComponents.passengerOnDriverSeat = false;

                            currentIKVehiclePassengerInfo.vehicleSeatInfo.seatIsFree = true;

                            currentIKVehiclePassengerInfo.vehicleSeatInfo.enterExitActionInProcess = false;

                            currentPassengerComponents.playerControllerManager.setActionToGetOnVehicleActiveState (false);
                            currentPassengerComponents.playerControllerManager.setActionToGetOffFromVehicleActiveState (false);

                            currentIKVehiclePassengerInfo.vehicleSeatInfo.actionSystemToEnterVehicle.setPlayerActionDeactivate (currentPassengerComponents.currentPassenger);

                            currentPassengerComponents.mainPlayerActionSystem.stopAllActions ();

                            print ("Cancelling action outside of vehicle " + cancelActionOutsideVehicle);

                            if (cancelActionOutsideVehicle) {
                                currentPassengerComponents.mainRemoteEventSystem.callRemoteEvent (currentIKVehiclePassengerInfo.vehicleSeatInfo.remoteEventToCancelActionEnterExitOutsideVehicle);
                            } else {
                                currentPassengerComponents.mainRemoteEventSystem.callRemoteEvent (currentIKVehiclePassengerInfo.vehicleSeatInfo.remoteEventToCancelActionEnterExitInsideVehicle);
                            }

                            currentIKVehiclePassengerInfo.vehicleSeatInfo.eventToCancelActionEnterAndExit.Invoke ();

                            removePassengerInfo (currentIKVehiclePassengerInfo);

                            stopEnterExitActionSystemOnPassengerCoroutine (currentPassengerComponents.actionSystemCoroutine);
                        }
                    }
                }

                yield return null;
            }
        }

        if (!cancelAction) {
            if (passengerIsGettingOn) {
                currentIKVehiclePassengerInfo.vehicleSeatInfo.actionSystemToEnterVehicle.setPlayerActionDeactivate (currentPassengerComponents.currentPassenger);
            } else {
                if (jumpOffFromVehicleActive) {
                    currentIKVehiclePassengerInfo.vehicleSeatInfo.actionSystemToJumpOffFromVehicle.setPlayerActionDeactivate (currentPassengerComponents.currentPassenger);
                } else {
                    currentIKVehiclePassengerInfo.vehicleSeatInfo.actionSystemToExitVehicle.setPlayerActionDeactivate (currentPassengerComponents.currentPassenger);
                }
            }

            currentPassengerComponents.playerControllerManager.setActionToGetOnVehicleActiveState (false);
            currentPassengerComponents.playerControllerManager.setActionToGetOffFromVehicleActiveState (false);

            currentIKVehiclePassengerInfo.vehicleSeatInfo.enterExitActionInProcess = false;

            if (cancelingEntering) {
                //				print ("Cancelling passenger entering on vehicle properly");

                currentPassenger.transform.SetParent (null);

                currentPassengerComponents.passengerOnDriverSeat = false;

                currentIKVehiclePassengerInfo.vehicleSeatInfo.seatIsFree = true;

                currentIKVehiclePassengerInfo.vehicleSeatInfo.eventToCancelActionEnterAndExit.Invoke ();

                removePassengerInfo (currentIKVehiclePassengerInfo);
            } else {
                //				print ("confirm to enter ");

                setPassengerOnVehicleState (currentPassenger, passengerParent, nextPlayerPos, currentPassengerComponents,
                    currentIKVehiclePassengerInfo, passengerIsGettingOn, isPlayer, passengerOnDriverSeat, true, isBeingDriven);
            }
        }
    }

    void setPassengerOnVehicleState (GameObject currentPassenger, Transform passengerParent, Vector3 nextPlayerPos,
                                     IKDrivingSystem.passengerComponents currentPassengerComponents, IKDrivingSystem.IKDrivingInformation currentIKVehiclePassengerInfo,
                                     bool passengerIsGettingOn, bool isPlayer, bool passengerOnDriverSeat, bool useActionSystemToEnterExitSeat,
                                     bool isBeingDriven)
    {
        if (vehicleCameraDirection == null) {
            vehicleCameraDirection = vehicleGameObject.transform;
        }

        //set the state driving as the current state of the player
        currentPassengerComponents.playerControllerManager.setDrivingState (passengerIsGettingOn, vehicleGameObject, vehicleName, vehicleCameraDirection);

        currentPassengerComponents.playerControllerManager.enabled = !passengerIsGettingOn;
        currentPassengerComponents.passengerPhysicallyOnVehicle = true;

        currentPassengerComponents.playerControllerManager.changeScriptState (!passengerIsGettingOn);

        currentPassengerComponents.playerControllerManager.setPlayerSetAsChildOfParentState (passengerIsGettingOn);

        currentPassengerComponents.playerControllerManager.setUsingDeviceState (passengerIsGettingOn);

        currentPassengerComponents.playerControllerManager.setUsingGeneralDriveRiderSystem (passengerIsGettingOn);

        if (isPlayer) {
            currentPassengerComponents.pauseManager.usingDeviceState (passengerIsGettingOn);

            currentPassengerComponents.mainInventoryManager.enableOrDisableWeaponSlotsParentOutOfInventory (!passengerIsGettingOn);
        }

        //enable or disable the collider and the rigidbody of the player

        currentPassengerComponents.playerControllerManager.setMainColliderState (!passengerIsGettingOn);

        currentPassengerComponents.mainRigidbody.isKinematic = passengerIsGettingOn;

        //get the IK positions of the car to use them in the player
        currentPassengerComponents.IKManager.setDrivingState (passengerIsGettingOn, currentIKVehiclePassengerInfo);

        //check if the camera in the player is in first or third view, to set the current view in the vehicle
        bool firstCameraEnabled = currentPassengerComponents.playerCameraManager.isFirstPersonActive ();

        if (passengerIsGettingOn) {
            if (currentPassengerComponents.carryingMeleeWeaponsPreviously) {
                GKC_Utils.checkIfDropThrownWeaponWhenUsingDevice (currentPassengerComponents.currentPassenger);

                if (currentPassengerComponents.playerControllerManager.isPlayerUsingMeleeWeapons ()) {
                    currentPassengerComponents.statesManager.disableGrabMode ();
                } else {
                    currentPassengerComponents.carryingMeleeWeaponsPreviously = false;
                }
            }
        }

        if (isPlayer) {
            if (disablePlayerHUDEnabled) {
                //enable and disable the player's HUD and the vehicle's HUD
                currentPassengerComponents.mainPlayerHUDManager.enableOrDisableHUD (!passengerIsGettingOn);
            }

            if (showVehicleHUD) {
                currentPassengerComponents.mainPlayerHUDManager.enableOrDisableVehicleHUD (isBeingDriven);
            }

            if (passengerOnDriverSeat) {
                if (passengerIsGettingOn) {
                    if (useEventOnDriverGetOn) {
                        eventOnDriverGetOn.Invoke (currentPassengerComponents.currentPassenger);
                    }
                } else {
                    if (useEventOnDriverGetOff) {
                        eventOnDriverGetOff.Invoke (currentPassengerComponents.currentPassenger);
                    }
                }
            }
        }

        currentPassengerComponents.mainFootStepManager.enableOrDisableFootSteps (!passengerIsGettingOn);

        currentPassengerComponents.healthManager.setSliderVisibleState (!passengerIsGettingOn);

        currentPassengerComponents.healthManager.setDrivingState (passengerIsGettingOn);

        if (passengerIsGettingOn) {
            currentPassengerComponents.playerCameraManager.playOrPauseHeadBob (!passengerIsGettingOn);
            currentPassengerComponents.playerCameraManager.stopAllHeadbobMovements ();
        } else {
            currentPassengerComponents.playerCameraManager.pauseHeadBodWithDelay (0.5f);
            currentPassengerComponents.playerCameraManager.playOrPauseHeadBob (!passengerIsGettingOn);
        }

        if (passengerIsGettingOn) {
            if (currentIKVehiclePassengerInfo.setNewScaleOnPassenger) {
                currentPassengerComponents.currentPassenger.transform.localScale = currentIKVehiclePassengerInfo.newScaleOnPassenger;
            }
        } else {
            if (currentIKVehiclePassengerInfo.setNewScaleOnPassenger) {
                currentPassengerComponents.currentPassenger.transform.localScale = Vector3.one;
            }
        }

        //if the first camera was enabled, set the current main camera position in the first camera position of the vehicle
        if (firstCameraEnabled) {
            //enable the player's body to see it
            if (passengerIsGettingOn) {
                currentPassengerComponents.playerControllerManager.setCharacterMeshGameObjectState (true);
                //if the first person was actived, disable the player's body
            } else {
                currentPassengerComponents.playerControllerManager.setCharacterMeshGameObjectState (false);
            }

            currentPassengerComponents.playerControllerManager.setAnimatorState (passengerIsGettingOn);
        }

        currentPassengerComponents.usingDevicesManager.setUseDeviceButtonEnabledState (true);

        //if the player is driving it
        if (passengerIsGettingOn) {
            //check the current state of the player, to check if he is carrying an object, aiming, etc... to disable that state
            currentPassengerComponents.statesManager.checkPlayerStates ();

            if (currentPassengerComponents.mainHideCharacterFixedPlaceSystemPlayerManagement != null) {
                currentPassengerComponents.mainHideCharacterFixedPlaceSystemPlayerManagement.removeCurrentHideFromEnemiesSystem ();
            }

            //set the player's position and parent inside the car

            currentRiderGameObject.transform.SetParent (passengerParent.transform);
            currentRiderGameObject.transform.localPosition = Vector3.zero;
            currentRiderGameObject.transform.localRotation = Quaternion.identity;
            currentRiderGameObject.transform.position = currentIKVehiclePassengerInfo.vehicleSeatInfo.seatTransform.position;
            currentRiderGameObject.transform.rotation = currentIKVehiclePassengerInfo.vehicleSeatInfo.seatTransform.rotation;

            //get the vehicle camera rotation

            if (passengerOnDriverSeat) {
                //reset the player's camera rotation input values
                currentPassengerComponents.playerCameraManager.setLookAngleValue (Vector2.zero);

                //set the player's camera rotation as the same in the vehicle
                currentPassengerComponents.playerCameraGameObject.transform.rotation = transform.rotation;
            }

            if (isPlayer) {
                if (passengerOnDriverSeat) {
                    currentPassengerComponents.playerCameraManager.getPivotCameraTransform ().localRotation = Quaternion.identity;

                    currentPassengerComponents.playerCameraManager.resetMainCameraTransformLocalPosition ();

                    currentPassengerComponents.playerCameraManager.resetPivotCameraTransformLocalPosition ();

                    currentPassengerComponents.playerCameraManager.resetCurrentCameraStateAtOnce ();
                }

                if (!useActionSystemToEnterExitSeat) {
                    currentPassengerComponents.playerCameraManager.setFBAPivotCameraTransformParent (currentPassengerComponents.playerCameraManager.getPivotCameraTransform ());
                }
            }

            currentPassengerComponents.usingDevicesManager.setDrivingState (passengerIsGettingOn);

            currentPassengerComponents.usingDevicesManager.clearDeviceListButOne (vehicleGameObject);

            currentPassengerComponents.usingDevicesManager.checkIfSetOriginalShaderToPreviousDeviceFound (vehicleGameObject);

            if (!useActionSystemToEnterExitSeat) {
                if (currentIKVehiclePassengerInfo.useAnimationActionIDOnDriveIdle) {
                    currentPassengerComponents.mainPlayerActionSystem.setNewActionIDExternally (currentIKVehiclePassengerInfo.animationActionID);
                }
            }
        }

        //the player gets off from the vehicle
        else {
            currentPassengerComponents.playerControllerManager.setTemporalVehicle (null);

            if (isPlayer && passengerOnDriverSeat) {
                currentPassengerComponents.mainPlayerHUDManager.setCurrentVehicleHUD (null);
            }

            //set the parent of the player as null
            currentRiderGameObject.transform.SetParent (null);

            if (!useActionSystemToEnterExitSeat) {
                //set the player's position at the correct side of the car
                currentRiderGameObject.transform.position = nextPlayerPos;
            }

            if (currentIKVehiclePassengerInfo.useAnimationActionIDOnDriveIdle) {
                currentPassengerComponents.mainPlayerActionSystem.setNewActionIDExternally (resetAnimatorDrivingStateID);
            } else {
                currentPassengerComponents.mainPlayerActionSystem.setNewActionIDExternally (0);
            }

            //Set passenger rotation

            Vector3 targetRotation = vehicleGameObject.transform.eulerAngles;

            if (!useActionSystemToEnterExitSeat) {
                currentRiderGameObject.transform.eulerAngles = targetRotation;
            }

            Vector3 currentNormal = currentPassengerComponents.playerGravityManager.getCurrentNormal ();

            Quaternion currentPlayerRotation = currentRiderGameObject.transform.rotation;
            Vector3 currentPlayerForward = Vector3.Cross (currentRiderGameObject.transform.right, currentNormal);
            Quaternion playerTargetRotation = Quaternion.LookRotation (currentPlayerForward, currentNormal);

            if (!useActionSystemToEnterExitSeat) {
                currentRiderGameObject.transform.rotation = playerTargetRotation;
            }

            //set the player's camera position in the correct place

            Transform playerCameraTransform = currentPassengerComponents.playerCameraGameObject.transform;

            if (!useActionSystemToEnterExitSeat) {
                playerCameraTransform.position = nextPlayerPos;

                playerCameraTransform.rotation = currentRiderGameObject.transform.rotation;

                if (resetCameraRotationWhenGetOff) {
                    float angleY = Vector3.Angle (vehicleGameObject.transform.forward, playerCameraTransform.forward);

                    angleY *= Mathf.Sign (playerCameraTransform.InverseTransformDirection (
                        Vector3.Cross (vehicleGameObject.transform.forward, playerCameraTransform.forward)).y);

                    playerCameraTransform.Rotate (0, -angleY, 0);
                }
            }

            if (isPlayer) {
                if (!useActionSystemToEnterExitSeat) {
                    currentPassengerComponents.playerCameraManager.setFBAPivotCameraTransformParent (null);

                    if (currentPassengerComponents.playerCameraManager.isUpdateFBAPivotCameraTransformInsideHeadPositionActive ()) {
                        currentPassengerComponents.playerCameraManager.setUpdateFBAPivotCameraTransformInsideHeadPositionActiveState (true);
                    }
                }
            }

            if (changeDriverGravityWhenGetsOff) {
                setPassengerGravityState (currentPassengerComponents);
            }

            if (drawFireWeaponIfCarryingPreviously) {
                if (currentPassengerComponents.carryingFireWeaponsPrevioulsy) {
                    currentPassengerComponents.playerWeaponManager.checkIfDrawSingleOrDualWeapon ();
                }
            }

            if (drawMeleeWeaponIfCarryingPreviously) {
                if (currentPassengerComponents.carryingMeleeWeaponsPreviously) {
                    GKC_Utils.drawMeleeWeaponGrabbed (currentPassengerComponents.currentPassenger);
                }
            }

            currentPassengerComponents.usingDevicesManager.setDrivingState (passengerIsGettingOn);
        }

        if (!playerVisibleInVehicle) {
            currentPassengerComponents.playerControllerManager.setCharacterMeshGameObjectState (!passengerIsGettingOn);
            enableOrDisablePlayerVisibleInVehicle (passengerIsGettingOn, currentPassengerComponents);
        }

        if (hidePlayerWeaponsWhileDriving) {
            currentPassengerComponents.playerWeaponManager.enableOrDisableEnabledWeaponsMesh (!passengerIsGettingOn);

            if (currentPassengerComponents.mainMeleeWeaponsGrabbedManager != null) {
                currentPassengerComponents.mainMeleeWeaponsGrabbedManager.enableOrDisableAllMeleeWeaponMeshesOnCharacterBodyCheckingIfHiddingMeshes (!passengerIsGettingOn);

                currentPassengerComponents.mainMeleeWeaponsGrabbedManager.enableOrDisableAllMeleeWeaponShieldMeshesOnCharacterBodyCheckingIfHiddingMeshes (!passengerIsGettingOn);
            }
        }

        if (!passengerIsGettingOn) {
            removePassengerInfo (currentIKVehiclePassengerInfo);
        }

        if (passengerIsGettingOn) {
            if (!useActionSystemToEnterExitSeat) {
                currentPassengerComponents.playerControllerManager.updateAnimatorExternally ();

                currentPassengerComponents.playerControllerManager.resetAnimatorEvenIfFullBodyAwarenessActive ();
            }
        } else {
            if (!useActionSystemToEnterExitSeat) {
                currentPassengerComponents.playerCameraManager.updateFBAPivotCameraTransformToPivotCameraTransform ();
            }
        }

        if (useRemoteEventsOnPassengers) {
            if (currentPassengerComponents.mainRemoteEventSystem != null) {
                if (passengerIsGettingOn) {
                    for (int i = 0; i < remoteEventNameListGetOn.Count; i++) {
                        currentPassengerComponents.mainRemoteEventSystem.callRemoteEvent (remoteEventNameListGetOn [i]);
                    }
                } else {
                    for (int i = 0; i < remoteEventNameListGetOff.Count; i++) {
                        currentPassengerComponents.mainRemoteEventSystem.callRemoteEvent (remoteEventNameListGetOff [i]);
                    }
                }
            }
        }

        if (mainInputActionManager != null && isPlayer && passengerOnDriverSeat) {
            mainInputActionManager.enableOrDisableInput (isBeingDriven, currentPassengerComponents.currentPassenger);
        }

        if (isPlayer) {
            checkVehicleWeaponsElements (currentPassengerComponents);
        }
    }

    public void enableOrDisablePlayerVisibleInVehicle (bool state, IKDrivingSystem.passengerComponents newPassengerComponents)
    {
        if (!newPassengerComponents.playerCameraManager.isFirstPersonActive ()) {
            newPassengerComponents.playerControllerManager.setCharacterMeshesListToDisableOnEventState (!state);

            newPassengerComponents.playerWeaponManager.enableOrDisableWeaponsMesh (!state);
        } else {
            newPassengerComponents.playerControllerManager.setCharacterMeshGameObjectState (false);
        }
    }

    public Vector3 getGetOffPosition (IKDrivingSystem.IKDrivingInformation passengerInfo)
    {
        if (passengerInfo.vehicleSeatInfo.getOffPlace == IKDrivingSystem.seatInfo.getOffSide.right) {
            return passengerInfo.vehicleSeatInfo.rightGetOffPosition.position;
        } else {
            return passengerInfo.vehicleSeatInfo.leftGetOffPosition.position;
        }
    }

    public void removePassengerInfo (IKDrivingSystem.IKDrivingInformation passengerInfo)
    {
        if (passengerGameObjectList.Contains (passengerInfo.vehicleSeatInfo.currentPassenger)) {
            int passengerGameObjecToRemoveIndex = passengerGameObjectList.IndexOf (passengerInfo.vehicleSeatInfo.currentPassenger);

            IKDrivingSystem.passengerComponents currentPassengerComponents = passengerComponentsList [passengerGameObjecToRemoveIndex];

            playerController currentPlayerController = currentPassengerComponents.playerControllerManager;

            currentPlayerController.setPlayerStatusIDValue (currentPassengerComponents.previousPlayerStatusID);

            currentPlayerController.setCurrentIdleIDValueFloat (currentPassengerComponents.previousIdleID);

            currentPlayerController.setCurrentStrafeIDValue (currentPassengerComponents.previousStrafeID);

            currentPlayerController.updatePlayerStatusIDOnAnimator ();

            currentPlayerController.updateIdleIDOnAnimator ();

            currentPlayerController.updateStrafeIDOnAnimator ();

            currentPlayerController.setLastTimeFalling ();

            if (checkIfDriverIsOnGroundOnGetOff) {
                bool isPlayerOnGrond = currentPlayerController.checkIfPlayerOnGroundWithRaycast ();

                currentPlayerController.setCheckOnGroungPausedState (false);

                if (!isPlayerOnGrond) {
                    currentPlayerController.setPlayerOnGroundState (false);
                }

                currentPlayerController.setPlayerOnGroundAnimatorStateOnOverrideOnGroundWithTime (true);

                currentPlayerController.disableOverrideOnGroundAnimatorValue ();

                currentPlayerController.setPauseResetAnimatorStateFOrGroundAnimatorState (true);

                if (isPlayerOnGrond) {
                    currentPlayerController.setPlayerOnGroundState (true);

                    currentPlayerController.setOnGroundAnimatorIDValue (true);
                } else {
                    currentPlayerController.setOnGroundAnimatorIDValue (false);
                }
            }

            passengerComponentsList.RemoveAt (passengerGameObjecToRemoveIndex);

            passengerGameObjectList.Remove (passengerInfo.vehicleSeatInfo.currentPassenger);

            passengerInfo.vehicleSeatInfo.currentPassenger = null;
        }
    }

    public Vector3 getPassengerGetOffPosition (GameObject currentPassenger)
    {
        //		bool isGettingOn = setPassengerInfo (IKRiderInfo, currentRiderGameObject);
        //
        //		if (isGettingOn) {
        //			return Vector3.zero;
        //		}

        //search if the current passenger is in the list, to avoid get the closest seat
        int currentPassengerGameObjectIndex = -1;

        if (passengerGameObjectList.Contains (currentPassenger)) {
            int IKVehiclePassengersListCount = IKVehiclePassengersList.Count;

            for (int i = 0; i < IKVehiclePassengersListCount; i++) {
                if (IKVehiclePassengersList [i].vehicleSeatInfo.currentPassenger == currentPassenger) {
                    currentPassengerGameObjectIndex = i;
                }
            }
        }

        IKDrivingSystem.IKDrivingInformation currentIKVehiclePassengerInfo;

        if (currentPassengerGameObjectIndex == -1) {

            int currentIKVehiclePassengerIndex = -1;

            //print (currentPassenger.name);

            bool setDriverPosition = false;

            playerController currentPlayerController = currentPassenger.GetComponent<playerController> ();

            if (currentPlayerController != null) {
                if (!currentPlayerController.usedByAI) {
                    if (playerIsAlwaysDriver) {
                        if (isBeingDriven) {
                            if (showDebugPrint) {
                                print ("vehicle is already being driven, setting new character as passenger");
                            }
                        } else {
                            if (showDebugPrint) {
                                print ("vehicle is not being driven, setting new character as driver");
                            }

                            if (!currentPlayerController.canCharacterGetOnVehicles ()) {
                                return -Vector3.one;
                            }

                            setDriverPosition = true;
                        }
                    }
                }
            } else {
                print ("WARNING: player not found");
            }

            if (getDriverPosition || setDriverPosition) {
                currentIKVehiclePassengerIndex = getDriverSeatPassengerIndex ();
            } else {
                currentIKVehiclePassengerIndex = getClosestSeatToPassengerIndex (currentPassenger);
            }

            if (currentIKVehiclePassengerIndex > -1) {
                currentIKVehiclePassengerInfo = IKVehiclePassengersList [currentIKVehiclePassengerIndex];
            } else {
                return -Vector3.one;
            }

            bool isGettingOn = setPassengerInfo (currentIKVehiclePassengerInfo, currentPassenger);

            if (isGettingOn) {
                return Vector3.zero;
            }
        } else {
            currentIKVehiclePassengerInfo = IKVehiclePassengersList [currentPassengerGameObjectIndex];
        }

        Transform seatTransform = currentIKVehiclePassengerInfo.vehicleSeatInfo.seatTransform;
        Vector3 getOffPosition = Vector3.zero;
        Vector3 rayDirection = Vector3.zero;
        Vector3 passengerPosition = Vector3.zero;
        Vector3 rayPosition = Vector3.zero;
        float rayDistance = 0;

        Ray ray = new Ray ();

        bool canGetOff = false;

        RaycastHit [] hits;

        if (currentIKVehiclePassengerInfo.vehicleSeatInfo.getOffPlace == IKDrivingSystem.seatInfo.getOffSide.right) {
            getOffPosition = currentIKVehiclePassengerInfo.vehicleSeatInfo.rightGetOffPosition.position;
            rayDirection = seatTransform.right;
        } else {
            getOffPosition = currentIKVehiclePassengerInfo.vehicleSeatInfo.leftGetOffPosition.position;
            rayDirection = -seatTransform.right;
        }

        rayDistance = GKC_Utils.distance (seatTransform.position, getOffPosition);

        rayPosition = getOffPosition - rayDirection * rayDistance;

        //set the ray origin at the vehicle position with a little offset set in the inspector
        ray.origin = rayPosition;
        //set the ray direction to the left

        ray.direction = rayDirection;
        //get all the colliders in that direction where the yellow sphere is placed

        hits = Physics.SphereCastAll (ray, 0.1f, rayDistance, obstacleLayer);
        //get the position where the player will be placed

        passengerPosition = getOffPosition;

        if (ignoreObstacleCheckOnGetOffEnabled) {
            canGetOff = true;
        } else {
            if (hits.Length == 0) {
                //any obstacle detected, so the player can get off
                canGetOff = true;
            } else {
                if (showDebugPrint) {
                    if (hits.Length > 0) {
                        print ("Obstacle detected at the " + currentIKVehiclePassengerInfo.vehicleSeatInfo.getOffPlace + " side");
                    }
                }
            }

            //some obstacles found
            for (int i = 0; i < hits.Length; i++) {
                //check the distance to that obstacles, if they are lower that the rayDistance, the player can get off
                if (hits [i].distance > rayDistance) {
                    canGetOff = true;
                } else {
                    canGetOff = false;
                }

                if (showDebugPrint) {
                    print ("Obstacle detected is " + hits [i].collider.name + " at a distance of " + hits [i].distance);
                }
            }
        }

        //if the left side is blocked, then check the right side in the same way that previously
        if (!canGetOff) {
            if (showDebugPrint) {
                print ("Checking the other side to get off");
            }

            if (currentIKVehiclePassengerInfo.vehicleSeatInfo.getOffPlace == IKDrivingSystem.seatInfo.getOffSide.right) {
                getOffPosition = currentIKVehiclePassengerInfo.vehicleSeatInfo.leftGetOffPosition.position;
                rayDirection = -seatTransform.right;
            } else {
                getOffPosition = currentIKVehiclePassengerInfo.vehicleSeatInfo.rightGetOffPosition.position;
                rayDirection = seatTransform.right;
            }

            rayDistance = GKC_Utils.distance (seatTransform.position, getOffPosition);

            rayPosition = getOffPosition - rayDirection * rayDistance;

            ray.origin = rayPosition;

            ray.direction = rayDirection;

            hits = Physics.SphereCastAll (ray, 0.1f, rayDistance, obstacleLayer);

            passengerPosition = getOffPosition;

            if (hits.Length == 0) {
                canGetOff = true;
            } else {
                if (showDebugPrint) {
                    if (hits.Length > 0) {
                        print ("Obstacle detected at the other side");
                    }
                }
            }

            for (int i = 0; i < hits.Length; i++) {
                if (hits [i].distance > rayDistance) {
                    canGetOff = true;
                } else {
                    canGetOff = false;
                }

                if (showDebugPrint) {
                    print ("Obstacle detected is " + hits [i].collider.name + " at a distance of " + hits [i].distance);
                }
            }
        }

        //if both sides are blocked, exit the function and the player can't get off
        if (!canGetOff) {
            return -Vector3.one;
        }

        //if any side is avaliable then check a ray in down direction, to place the player above the ground
        RaycastHit hit;

        if (Physics.Raycast (getOffPosition, -vehicleGameObject.transform.up, out hit, currentIKVehiclePassengerInfo.vehicleSeatInfo.getOffDistance, obstacleLayer)) {
            Debug.DrawRay (getOffPosition, -vehicleGameObject.transform.up * hit.distance, Color.yellow);
            passengerPosition = hit.point;
        }

        return passengerPosition;
    }

    public void setPassengerGravityState (IKDrivingSystem.passengerComponents currentPassengerComponents)
    {
        //set the current gravity of the player's as the same in the vehicle
        if (changeDriverGravityWhenGetsOff) {
            Vector3 gravityDirection = currentNormal;

            if (checkVehicleUpDirectionForGravityOnGetOff) {
                if (customVehicleGameObject != null) {
                    gravityDirection = customVehicleGameObject.transform.up;
                } else {
                    gravityDirection = vehicleGameObject.transform.up;
                }
            }

            if (gravityDirection != regularGravity) {
                Vector3 currentNormal = currentPassengerComponents.playerGravityManager.getCurrentNormal ();

                if (currentNormal != gravityDirection) {
                    float currentAngle = Vector3.Angle (gravityDirection, currentNormal);

                    if (currentAngle > minAngleDifferenceToRegularGravityToChangeWhenGetOff) {
                        //if the option to change player's gravity is on, change his gravity direction

                        currentPassengerComponents.playerGravityManager.setNormal (gravityDirection);
                    }
                }
            }
        }
    }

    public void setChangeDriverGravityWhenGetsOffState (bool state)
    {
        changeDriverGravityWhenGetsOff = state;
    }

    public void setCurrentNormal (Vector3 newValue)
    {
        currentNormal = newValue;
    }

    playerHUDManager.vehicleHUDElements currentHUDElements;
    playerHUDManager currentVehicleHUDInfo;

    Slider vehicleAmmo;
    Text ammoAmountText;
    Text weaponNameText;

    bool ammoAmountTextAssigned;
    bool weaponNameTextAssigned;

    public void getHUDBars (playerHUDManager newVehicleHUDInfo, playerHUDManager.vehicleHUDElements hudElements, bool drivingState)
    {
        currentVehicleHUDInfo = newVehicleHUDInfo;

        currentHUDElements = hudElements;

        vehicleAmmo = currentHUDElements.vehicleAmmo;

        ammoAmountText = currentHUDElements.ammoInfo;

        ammoAmountTextAssigned = ammoAmountText != null;

        weaponNameText = currentHUDElements.weaponName;

        weaponNameTextAssigned = weaponNameText != null;


        //check also if the vehicle has a weapon system attached, to enable or disable it
        //if the vehicle has not a weapon system, the weapon info of the HUD is disabled
        if (currentHUDElements.ammoContent != null && currentHUDElements.vehicleCursor != null) {
            bool hudElementState = false;

            if (mainVehicleWeaponSystem != null && mainVehicleWeaponSystem.weaponsEnabled) {
                if (!destroyed) {
                    mainVehicleWeaponSystem.changeWeaponState (drivingState);

                    if (drivingState) {
                        mainVehicleWeaponSystem.getCameraInfo (defaultVehicleWeaponSystemCameraTransform, vehicleWeaponSystemUseRotationInput);
                    }
                }

                hudElementState = true;
            }

            if (currentHUDElements.ammoContent.activeSelf != hudElementState) {
                currentHUDElements.ammoContent.SetActive (hudElementState);
            }

            if (currentHUDElements.vehicleCursor.activeSelf != hudElementState) {
                currentHUDElements.vehicleCursor.SetActive (hudElementState);
            }
        }
    }

    public void setWeaponState (bool state)
    {
        if (mainVehicleWeaponSystem != null && mainVehicleWeaponSystem.isWeaponsEnabled ()) {
            if (!destroyed) {
                mainVehicleWeaponSystem.changeWeaponState (state);
            }

            if (currentHUDElements != null) {
                if (currentHUDElements.ammoContent != null) {

                    if (currentHUDElements.ammoContent.activeSelf != state) {
                        currentHUDElements.ammoContent.SetActive (state);
                    }

                    if (currentHUDElements.vehicleCursor.activeSelf != state) {
                        currentHUDElements.vehicleCursor.SetActive (state);
                    }
                }
            }
        }
    }

    void checkVehicleWeaponsElements (IKDrivingSystem.passengerComponents currentPassengerComponents)
    {
        if (!useVehicleWeaponsEnabled) {
            return;
        }

        if (mainVehicleWeaponSystem != null) {
            mainVehicleWeaponSystem.setVehicleWeaponsComponents ();

            mainVehicleWeaponSystem.setCurrentDriver (currentPassengerComponents.currentPassenger);

            mainVehicleWeaponSystem.setVehicleHUDInfoManager (currentPassengerComponents.mainPlayerHUDManager);

            mainVehicleWeaponSystem.setGKCSimpleRiderSystem (this);

            getHUDBars (currentPassengerComponents.mainPlayerHUDManager, currentPassengerComponents.mainPlayerHUDManager.getHudElements (), isBeingDriven);
        }
    }

    public void setVehicleWeaponsComponents ()
    {
        if (mainVehicleWeaponSystemInitialized) {
            return;
        }

        if (mainVehicleWeaponSystem != null) {
            mainVehicleWeaponSystem.setVehicleWeaponsComponents ();

            mainVehicleWeaponSystemInitialized = true;
        }
    }

    public void getAmmo (string ammoName, int amount)
    {
        if (mainVehicleWeaponSystem != null && mainVehicleWeaponSystem.isWeaponsEnabled ()) {
            mainVehicleWeaponSystem.getAmmo (ammoName, amount);
        }
    }

    public void setDestroyedState (bool state)
    {
        destroyed = state;

    }

    public void setWeaponName (string name, int ammoPerClip, int clipSize)
    {
        if (weaponNameTextAssigned) {
            weaponNameText.text = name;

            vehicleAmmo.maxValue = ammoPerClip;

            vehicleAmmo.value = clipSize;
        }
    }

    //the player is shooting while he is driving, so use ammo of the vehicle weapon
    public void useAmmo (int clipSize, int remainAmmo)
    {
        if (ammoAmountTextAssigned) {
            if (remainAmmo == -1) {
                ammoAmountText.text = clipSize + "/Inf";
            } else {
                ammoAmountText.text = clipSize + "/" + remainAmmo;
            }

            vehicleAmmo.value = clipSize;
        }
    }

    public void reactivateCollidersIfPossible ()
    {
        int colliderPartsCount = colliderParts.Count;

        for (int i = 0; i < colliderPartsCount; i++) {
            if (colliderParts [i] != null) {
                colliderParts [i].enabled = false;

                colliderParts [i].enabled = true;
            }
        }
    }

    public IKDrivingSystem.IKDrivingInformation getIKVehiclePassengerInfo (GameObject passenger)
    {
        int IKVehiclePassengersListCount = IKVehiclePassengersList.Count;

        for (int i = 0; i < IKVehiclePassengersListCount; i++) {
            if (IKVehiclePassengersList [i].vehicleSeatInfo.currentPassenger == passenger) {
                return IKVehiclePassengersList [i];
            }
        }

        return null;
    }

    public bool setPassengerInfo (IKDrivingSystem.IKDrivingInformation passengerInfo, GameObject passenger)
    {
        bool isGettingOn = false;

        if (showDebugPrint) {
            print ("ADD PASSENGER " + passenger.name);
        }

        if (!passengerGameObjectList.Contains (passenger)) {
            passengerGameObjectList.Add (passenger);

            passengerInfo.vehicleSeatInfo.currentPassenger = passenger;

            isGettingOn = true;

            setPassengerComponents (passenger);
        }

        return isGettingOn;
    }

    public void getRiderGameObject (GameObject newRiderGameObject)
    {
        currentRiderGameObject = newRiderGameObject;
    }

    public void setPassengerComponents (GameObject currentPassenger)
    {
        IKDrivingSystem.passengerComponents newPassengerComponentsList = new IKDrivingSystem.passengerComponents ();

        newPassengerComponentsList.currentPassenger = currentPassenger;

        playerComponentsManager mainPlayerComponentsManager = currentPassenger.GetComponent<playerComponentsManager> ();

        newPassengerComponentsList.mainPlayerComponentsManager = mainPlayerComponentsManager;

        newPassengerComponentsList.playerControllerManager = mainPlayerComponentsManager.getPlayerController ();

        newPassengerComponentsList.usingDevicesManager = mainPlayerComponentsManager.getUsingDevicesSystem ();

        newPassengerComponentsList.playerCameraManager = mainPlayerComponentsManager.getPlayerCamera ();

        newPassengerComponentsList.playerCameraGameObject = newPassengerComponentsList.playerCameraManager.gameObject;

        newPassengerComponentsList.playerGravityManager = mainPlayerComponentsManager.getGravitySystem ();

        newPassengerComponentsList.playerWeaponManager = mainPlayerComponentsManager.getPlayerWeaponsManager ();

        newPassengerComponentsList.mainMeleeWeaponsGrabbedManager = mainPlayerComponentsManager.getMeleeWeaponsGrabbedManager ();

        newPassengerComponentsList.mainCamera = newPassengerComponentsList.playerCameraManager.getMainCamera ();
        newPassengerComponentsList.mainCameraTransform = newPassengerComponentsList.mainCamera.transform;

        newPassengerComponentsList.mainRigidbody = newPassengerComponentsList.playerControllerManager.getRigidbody ();

        newPassengerComponentsList.IKManager = mainPlayerComponentsManager.getIKSystem ();

        newPassengerComponentsList.mainFootStepManager = mainPlayerComponentsManager.getFootStepManager ();

        newPassengerComponentsList.healthManager = mainPlayerComponentsManager.getHealth ();

        newPassengerComponentsList.statesManager = mainPlayerComponentsManager.getPlayerStatesManager ();

        newPassengerComponentsList.mainHideCharacterFixedPlaceSystemPlayerManagement = newPassengerComponentsList.mainPlayerComponentsManager.getHideCharacterFixedPlaceSystemPlayerManagement ();

        newPassengerComponentsList.pauseManager = mainPlayerComponentsManager.getPauseManager ();

        newPassengerComponentsList.mainInventoryManager = mainPlayerComponentsManager.getInventoryManager ();

        newPassengerComponentsList.mainPlayerHUDManager = mainPlayerComponentsManager.getPlayerHUDManager ();

        newPassengerComponentsList.mapManager = mainPlayerComponentsManager.getMapSystem ();

        newPassengerComponentsList.mainRagdollActivator = mainPlayerComponentsManager.getRagdollActivator ();

        newPassengerComponentsList.carryingFireWeaponsPrevioulsy = newPassengerComponentsList.playerWeaponManager.isUsingWeapons ();

        newPassengerComponentsList.carryingMeleeWeaponsPreviously = newPassengerComponentsList.playerControllerManager.isPlayerUsingMeleeWeapons ();

        newPassengerComponentsList.mainRemoteEventSystem = mainPlayerComponentsManager.getRemoteEventSystem ();

        newPassengerComponentsList.mainPlayerActionSystem = mainPlayerComponentsManager.getPlayerActionSystem ();

        newPassengerComponentsList.mainVehiclePassengerUnderwater = mainPlayerComponentsManager.getVehiclePassengerUnderwater ();

        newPassengerComponentsList.previousPlayerStatusID = newPassengerComponentsList.playerControllerManager.playerStatusID;

        newPassengerComponentsList.previousIdleID = newPassengerComponentsList.playerControllerManager.getCurrentIdleID ();

        newPassengerComponentsList.previousStrafeID = newPassengerComponentsList.playerControllerManager.currentStrafeID;

        newPassengerComponentsList.playerControllerManager.setPlayerStatusIDValue (0);

        newPassengerComponentsList.playerControllerManager.setCurrentIdleIDValue (0);

        newPassengerComponentsList.playerControllerManager.setCurrentStrafeIDValue (0);

        newPassengerComponentsList.playerControllerManager.setOriginalAirIDValue ();

        newPassengerComponentsList.playerControllerManager.updatePlayerStatusIDOnAnimator ();

        newPassengerComponentsList.playerControllerManager.updateIdleIDOnAnimator ();

        newPassengerComponentsList.playerControllerManager.updateStrafeIDOnAnimator ();

        newPassengerComponentsList.playerControllerManager.updateAirIDAnimatorID ();

        newPassengerComponentsList.playerControllerManager.setOnGroundAnimatorIDValue (true);

        passengerComponentsList.Add (newPassengerComponentsList);
    }

    public void checkDriverHeadMeshOnCameraViewChange (bool isFirstPersonCamera)
    {
        if (isBeingDriven) {
            if (currentPassengerDriverComponents != null) {
                currentPassengerDriverComponents.playerControllerManager.changeHeadScale (isFirstPersonCamera);
            }
        }
    }

    public void findPlayerOnScene ()
    {
        if (searchPlayerOnSceneIfNotAssigned) {
            getRiderGameObject (GKC_Utils.findMainPlayerOnScene ());

            if (currentRiderGameObject != null) {
                playerAssignedProperly = true;
            }
        }
    }

    public void killCharacterOnVehicle ()
    {
        if (isBeingDriven) {
            if (currentPassengerDriverComponents != null && currentPassengerDriverComponents.usingDevicesManager != null) {
                customPassengerPosition = passengerParent.position + passengerParent.up * 3;

                health currentHealthManager = currentPassengerDriverComponents.healthManager;

                currentPassengerDriverComponents.usingDevicesManager.useCurrentDevice (vehicleGameObject);

                currentHealthManager.killCharacter ();
            }
        }
    }

    public void ejectCharacterFromVehicle ()
    {
        if (isBeingDriven) {
            if (currentPassengerDriverComponents != null && currentPassengerDriverComponents.usingDevicesManager != null) {
                customPassengerPosition = passengerParent.position + passengerParent.up * 3;

                currentPassengerDriverComponents.usingDevicesManager.useCurrentDevice (vehicleGameObject);
            }
        }
    }

    public void getOffFromVehicle ()
    {
        if (isBeingDriven) {
            if (currentPassengerDriverComponents != null) {
                if (currentPassengerDriverComponents.usingDevicesManager != null) {
                    currentPassengerDriverComponents.usingDevicesManager.useCurrentDevice (vehicleGameObject);

                    if (showDebugPrint) {
                        print ("activating get off input action");
                    }
                }
            }
        }
    }

    public string getVehicleName ()
    {
        return vehicleName;
    }

    public bool isVehicleFull ()
    {
        if (passengerGameObjectList.Count == IKVehiclePassengersList.Count) {
            return true;
        }

        return false;
    }

    public int getDriverSeatPassengerIndex ()
    {
        int IKVehiclePassengersListCount = IKVehiclePassengersList.Count;

        for (int i = 0; i < IKVehiclePassengersListCount; i++) {
            if (IKVehiclePassengersList [i].vehicleSeatInfo.isDriverSeat) {
                return i;
            }
        }

        return -1;
    }

    public int getClosestSeatToPassengerIndex (GameObject currentPassenger)
    {
        bool characterCanDrive = false;

        playerController currentPlayerController = currentPassenger.GetComponent<playerController> ();

        if (currentPlayerController != null) {
            if (currentPlayerController.canCharacterDrive ()) {
                characterCanDrive = true;
            }
        }

        if (getOnlyPassengerSeatActive) {
            characterCanDrive = false;
        }

        int currentIKVehiclePassengerIndex = -1;

        float maxDistance = Mathf.Infinity;

        Vector3 currentPassengerPosition = currentPassenger.transform.position;

        int IKVehiclePassengersListCount = IKVehiclePassengersList.Count;

        for (int i = 0; i < IKVehiclePassengersListCount; i++) {
            if (IKVehiclePassengersList [i].vehicleSeatInfo.seatIsFree) {
                if (characterCanDrive || (!IKVehiclePassengersList [i].vehicleSeatInfo.isDriverSeat && !characterCanDrive)) {

                    float currentDistance = GKC_Utils.distance (currentPassengerPosition, IKVehiclePassengersList [i].vehicleSeatInfo.seatTransform.position);

                    if (currentDistance < maxDistance) {
                        maxDistance = currentDistance;
                        currentIKVehiclePassengerIndex = i;
                    }
                }
            }
        }

        return currentIKVehiclePassengerIndex;
    }

    //Override functions
    public override bool checkIfDetectSurfaceBelongToVehicle (Collider surfaceFound)
    {
        if (colliderParts.Contains (surfaceFound)) {
            return true;
        }

        return false;
    }

    public override Collider getVehicleCollider ()
    {
        int colliderPartsCount = colliderParts.Count;

        if (colliderPartsCount > 0) {
            for (int i = 0; i < colliderPartsCount; i++) {
                if (colliderParts [i] != null) {
                    if (colliderParts [i].GetComponent<characterDamageReceiver> () != null) {
                        return colliderParts [i];
                    }
                }
            }
        }

        return null;
    }

    public override float getVehicleRadius ()
    {
        return vehicleRadius;
    }

    public override bool isBeingDrivenActive ()
    {
        return isBeingDriven;
    }

    public override GameObject getCurrentDriver ()
    {
        if (currentPassengerDriverComponents != null) {
            return currentPassengerDriverComponents.currentPassenger;
        }

        return null;
    }

    public override void setPlayerVisibleInVehicleState (bool state)
    {
        playerVisibleInVehicle = state;
    }

    public override GameObject getVehicleGameObject ()
    {
        return vehicleGameObject;
    }

    public override Transform getCustomVehicleTransform ()
    {
        if (customVehicleGameObject == null) {
            customVehicleGameObject = vehicleGameObject;
        }

        return customVehicleGameObject.transform;
    }

    public override void setEnteringOnVehicleFromDistanceState (bool state)
    {
        enteringOnVehicleFromDistance = state;
    }

    public override void setCurrentPassenger (GameObject passenger)
    {
        currentRiderGameObject = passenger;
    }

    public void setPauseDriverGetOffInputActionState (bool state)
    {
        if (isBeingDriven) {
            if (currentPassengerDriverComponents != null && currentPassengerDriverComponents.usingDevicesManager != null) {

                currentPassengerDriverComponents.usingDevicesManager.setUseDeviceButtonEnabledState (state);
            }
        }
    }

    //INPUT FUNCTIONS
    public void inputGetOffFromVehicle ()
    {
        if (isBeingDriven) {
            getOffFromVehicle ();
        }
    }
}
