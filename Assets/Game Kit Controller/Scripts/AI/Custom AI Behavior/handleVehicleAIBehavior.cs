using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class handleVehicleAIBehavior : AIBehaviorInfo
{
    [Space]
    [Header ("Debug")]
    [Space]

    public bool showDebugPrint;

    public bool characterOnVehicle;

    [Space]
    [Header ("Custom Driver Settings")]
    [Space]

    public bool searchVehicleEnabled = true;

    public bool startGameSearchingForVehicle;

    public bool setVehicleToStart;
    public GameObject vehicleToStart;

    [Space]

    public bool useMaxDistanceToGetVehicle;

    public float maxDistanceToGetVehicle;

    public float minDistanceToGetOnCurrentVehicle = 3.5f;

    public bool ignoreCheckVehicleIfTargetToAttackFound;

    [Space]

    public bool stopBehaviorUpdateOnGetOffFromVehicle = true;
    public bool stopBehaviorUpdateOnVehicleReached = true;

    [Space]
    [Header ("AI Driver Debug Info")]
    [Space]

    public bool searchingVehicle;
    public bool vehicleToReachFound;

    public bool currentVehicleAINavmeshLocated;

    public bool getOffFromVehicleOnDestinyReached;

    [Space]

    public Transform currentVehicleToGet;
    public vehicleAINavMesh currentVehicleAINavMesh;

    [Space]
    [Header ("Events Settings")]
    [Space]

    public bool useEventOnNoVehicleToPickFromScene;
    public UnityEvent eventOnNoVehicleToPickFromScene;

    [Space]
    [Space]

    [Space]
    [Header ("Custom Passenger Settings")]
    [Space]

    public bool checkAIPassengerStateEnabled = true;

    public bool ignoreCheckPartnerOnVehicle;

    [Space]
    [Header ("AI Passenger Debug Info")]
    [Space]

    public vehicleHUDManager currentVehicleHUDManager;
    public vehicleHUDManager currentVehicleToAttack;
    public GameObject currentVehicle;
    public playerController playerControllerPartner;

    public bool AICharacterWasntAbleToEnterOnPartnerVehicle;
    public bool partnerFound;

    public bool followingPartnerDrivingOnFoot;

    [Space]
    [Header ("Components")]
    [Space]

    public Transform AITransform;
    public findObjectivesSystem mainFindObjectivesSystem;
    public AINavMesh mainAINavmeshManager;
    public playerController mainPlayerController;
    public usingDevicesSystem usingDevicesManager;


    Coroutine updateCoroutine;

    bool followingTargetPreviously;

    bool isVehicleFull = false;


    void Start ()
    {
        if (searchVehicleEnabled) {
            if (startGameSearchingForVehicle) {
                StartCoroutine (startGameSearchingForVehicleCoroutine ());
            }
        }
    }

    //------------------------------------------------------------
    //DRIVER FUNCTIONS
    //------------------------------------------------------------

    IEnumerator startGameSearchingForVehicleCoroutine ()
    {
        yield return new WaitForSeconds (0.3f);

        activateAIBehavior ();
    }

    public override void activateAIBehavior ()
    {
        if (searchVehicleEnabled) {
            updateCoroutine = StartCoroutine (updateSystemCoroutine ());
        }
    }

    public override void deactivateAIBehavior ()
    {
        if (searchVehicleEnabled) {
            stopUpdateCoroutine ();
        }
    }

    public void stopUpdateCoroutine ()
    {
        if (updateCoroutine != null) {
            StopCoroutine (updateCoroutine);
        }
    }

    IEnumerator updateSystemCoroutine ()
    {
        var waitTime = new WaitForSecondsRealtime (0.0001f);

        while (true) {
            updateSystem ();

            yield return waitTime;
        }
    }

    public void setGetOffFromVehicleOnDestinyReachedState (bool state)
    {
        getOffFromVehicleOnDestinyReached = state;
    }

    void updateSystem ()
    {
        if (characterOnVehicle) {
            if (currentVehicleAINavmeshLocated) {
                if (currentVehicleAINavMesh.isFollowingTarget ()) {
                    followingTargetPreviously = true;
                } else {
                    if (followingTargetPreviously) {
                        getOffFromVehicle ();

                        if (stopBehaviorUpdateOnGetOffFromVehicle) {
                            stopUpdateCoroutine ();
                        }

                        setCharacterOnVehicleState (false);

                        return;
                    }
                }
            }
        } else {
            if (searchingVehicle) {
                bool vehicleReached = false;

                if (currentVehicleToGet != null) {
                    if (GKC_Utils.distance (mainAINavmeshManager.transform.position, currentVehicleToGet.position) < minDistanceToGetOnCurrentVehicle) {
                        if (showDebugPrint) {
                            print ("picking vehicle " + currentVehicleToGet.name);
                        }

                        vehicleHUDManager currentVehicleHUDManager = currentVehicleToGet.GetComponent<vehicleHUDManager> ();

                        if (currentVehicleHUDManager != null) {
                            IKDrivingSystem currentIKDrivingSystem = currentVehicleHUDManager.getIKDrivingSystem ();

                            if (currentIKDrivingSystem != null) {
                                currentIKDrivingSystem.setDriverExternally (AITransform.gameObject);

                                if (showDebugPrint) {
                                    print ("sending character to vehicle");
                                }

                                mainAINavmeshManager.removeTarget ();
                            }
                        } else {
                            GKCSimpleRiderSystem currentGKCSimpleRiderSystem = currentVehicleToGet.GetComponent<GKCSimpleRiderSystem> ();

                            if (currentGKCSimpleRiderSystem != null) {
                                currentGKCSimpleRiderSystem.setDriverExternally (AITransform.gameObject);

                                if (showDebugPrint) {
                                    print ("sending character to vehicle");
                                }
                            }
                        }

                        vehicleReached = true;
                    }
                } else {
                    vehicleReached = true;
                }

                if (vehicleReached) {
                    mainFindObjectivesSystem.setSearchingObjectState (false);

                    searchingVehicle = false;

                    currentVehicleToGet = null;

                    mainFindObjectivesSystem.setIgnoreVisionRangeActiveState (true);

                    mainFindObjectivesSystem.resetAITargets ();

                    mainFindObjectivesSystem.setIgnoreVisionRangeActiveState (false);

                    mainFindObjectivesSystem.setOriginalWanderEnabled ();

                    setCharacterOnVehicleState (true);

                    if (showDebugPrint) {
                        print ("vehicle picked, resuming state on AI");
                    }

                    if (stopBehaviorUpdateOnVehicleReached) {
                        stopUpdateCoroutine ();
                    }
                }
            } else {
                checkToFindVehicle ();
            }
        }
    }

    void checkToFindVehicle ()
    {
        if (characterOnVehicle) {

            return;
        }

        if (ignoreCheckVehicleIfTargetToAttackFound) {
            if (mainFindObjectivesSystem.isOnSpotted ()) {
                return;
            }
        }

        setCharacterOnVehicleState (false);

        bool vehicleFound = false;

        bool ignoreSearchVehicleResult = false;

        if (setVehicleToStart) {
            if (vehicleToStart != null) {
                Transform vehicleToStartTransform = null;

                vehicleHUDManager currentVehicleHUDManager = vehicleToStart.GetComponentInChildren<vehicleHUDManager> ();

                if (currentVehicleHUDManager != null) {
                    vehicleToStartTransform = currentVehicleHUDManager.transform;

                    if (mainAINavmeshManager.updateCurrentNavMeshPath (vehicleToStartTransform.position)) {
                        setVehicleToReachToDrive (vehicleToStartTransform);

                        ignoreSearchVehicleResult = true;
                    }
                }
            }
        }

        if (!ignoreSearchVehicleResult) {
            vehicleHUDManager [] vehicleHUDManagerList = GKC_Utils.findObjectsByComponent<vehicleHUDManager> ();

            List<Transform> vehiclesDetected = new List<Transform> ();

            if (vehicleHUDManagerList.Length > 0) {
                for (int i = 0; i < vehicleHUDManagerList.Length; i++) {
                    bool checkObjectResult = true;

                    if (vehicleHUDManagerList [i].getCurrentDriver () != null) {
                        checkObjectResult = false;
                    }

                    if (useMaxDistanceToGetVehicle) {
                        float distance = GKC_Utils.distance (vehicleHUDManagerList [i].transform.position, AITransform.position);

                        if (distance > maxDistanceToGetVehicle) {
                            checkObjectResult = false;
                        }
                    }

                    if (checkObjectResult) {
                        vehiclesDetected.Add (vehicleHUDManagerList [i].transform);
                    }
                }
            }

            GKCSimpleRiderSystem [] GKCSimpleRiderSystemList = GKC_Utils.findObjectsByComponent<GKCSimpleRiderSystem> ();

            if (GKCSimpleRiderSystemList.Length > 0) {
                for (int i = 0; i < GKCSimpleRiderSystemList.Length; i++) {
                    bool checkObjectResult = true;

                    if (GKCSimpleRiderSystemList [i].getCurrentDriver () != null) {
                        checkObjectResult = false;
                    }

                    if (useMaxDistanceToGetVehicle) {
                        float distance = GKC_Utils.distance (GKCSimpleRiderSystemList [i].transform.position, AITransform.position);

                        if (distance > maxDistanceToGetVehicle) {
                            checkObjectResult = false;
                        }
                    }

                    if (checkObjectResult) {
                        vehiclesDetected.Add (GKCSimpleRiderSystemList [i].transform);
                    }
                }
            }

            if (vehiclesDetected.Count == 0) {
                if (showDebugPrint) {
                    print ("no vehicles detected");
                }

                stopUpdateCoroutine ();
            }

            if (vehiclesDetected.Count > 0) {

                vehiclesDetected.Sort (delegate (Transform a, Transform b) {
                    return Vector3.Distance (AITransform.position, a.transform.position).CompareTo (Vector3.Distance (AITransform.position, b.transform.position));
                });

                if (showDebugPrint) {
                    print ("vehicles found on scene " + vehiclesDetected.Count);
                }

                for (int i = 0; i < vehiclesDetected.Count; i++) {
                    if (!vehicleFound) {

                        if (showDebugPrint) {
                            vehicleHUDManager currentVehicleHUDManager = vehiclesDetected [i].GetComponent<vehicleHUDManager> ();

                            if (currentVehicleHUDManager != null) {
                                print ("checking if vehicle with name " + currentVehicleHUDManager.getVehicleName () + " can be used");
                            } else {
                                GKCSimpleRiderSystem currentGKCSimpleRiderSystem = vehiclesDetected [i].GetComponent<GKCSimpleRiderSystem> ();

                                if (currentGKCSimpleRiderSystem != null) {
                                    print ("checking if vehicle with name " + currentGKCSimpleRiderSystem.getVehicleName () + " can be used");
                                }
                            }
                        }

                        if (mainAINavmeshManager.updateCurrentNavMeshPath (vehiclesDetected [i].transform.position)) {
                            setVehicleToReachToDrive (vehiclesDetected [i]);

                            vehicleFound = true;
                        }
                    }
                }
            } else {
                if (showDebugPrint) {
                    print ("no vehicles found");
                }
            }
        }

        if (!vehicleFound) {
            if (showDebugPrint) {
                print ("vehicles found can't be reached, cancelling search");
            }

            stopUpdateCoroutine ();

            if (useEventOnNoVehicleToPickFromScene) {
                eventOnNoVehicleToPickFromScene.Invoke ();
            }
        }
    }

    void setVehicleToReachToDrive (Transform vehicleTransform)
    {
        if (showDebugPrint) {
            print ("vehicle to reach found ");
        }

        mainFindObjectivesSystem.setDrawOrHolsterWeaponState (false);

        mainFindObjectivesSystem.setSearchingObjectState (true);

        mainFindObjectivesSystem.setWanderEnabledState (false);

        currentVehicleToGet = vehicleTransform;

        vehicleHUDManager currentVehicleHUDManager = currentVehicleToGet.GetComponent<vehicleHUDManager> ();

        if (currentVehicleHUDManager != null) {
            currentVehicleAINavMesh = currentVehicleHUDManager.getAIVehicleNavmesh ();

            currentVehicleAINavmeshLocated = currentVehicleAINavMesh != null;
        }

        mainAINavmeshManager.setTarget (vehicleTransform);

        mainAINavmeshManager.setTargetType (false, true);

        followingTargetPreviously = false;

        mainAINavmeshManager.lookAtTaget (false);

        if (showDebugPrint) {
            print ("vehicle to use located, setting searching vehicle state on AI");
        }

        searchingVehicle = true;
    }

    public override void updateAI ()
    {
        if (!behaviorEnabled) {
            return;
        }


    }

    public override void updateAIBehaviorState ()
    {
        if (!behaviorEnabled) {
            return;
        }


    }

    public override void updateAIAttackState (bool canUseAttack)
    {
        if (!behaviorEnabled) {
            return;
        }


    }

    public override void setSystemActiveState (bool state)
    {
        if (!behaviorEnabled) {
            return;
        }


    }

    public override void setWaitToActivateAttackActiveState (bool state)
    {

    }


    //------------------------------------------------------------
    //PASSENGER FUNCTIONS
    //------------------------------------------------------------

    public override void updateAIPassengerBehavior ()
    {
        if (!checkAIPassengerStateEnabled) {
            return;
        }

        if (!partnerFound) {
            return;
        }

        if (playerControllerPartner == null) {
            return;
        }

        if (!mainAINavmeshManager.isCharacterWaiting ()) {
            if (ignoreCheckPartnerOnVehicle) {
                if (playerControllerPartner.isPlayerDriving ()) {
                    if (!followingPartnerDrivingOnFoot) {
                        currentVehicle = playerControllerPartner.getCurrentVehicle ();

                        vehicleHUDManager currentVehicleHUDManagerToGetOn = currentVehicle.GetComponent<vehicleHUDManager> ();

                        if (currentVehicleHUDManagerToGetOn != null) {
                            mainFindObjectivesSystem.setExtraMinDistanceState (true, currentVehicleHUDManagerToGetOn.getVehicleRadius ());

                            mainAINavmeshManager.setExtraMinDistanceState (true, currentVehicleHUDManagerToGetOn.getVehicleRadius ());
                        } else {
                            GKCSimpleRiderSystem currentGKCSimpleRiderSystemToGetOn = currentVehicle.GetComponent<GKCSimpleRiderSystem> ();

                            if (currentGKCSimpleRiderSystemToGetOn != null) {

                                mainFindObjectivesSystem.setExtraMinDistanceState (true, currentGKCSimpleRiderSystemToGetOn.getVehicleRadius ());

                                mainAINavmeshManager.setExtraMinDistanceState (true, currentGKCSimpleRiderSystemToGetOn.getVehicleRadius ());
                            }
                        }

                        followingPartnerDrivingOnFoot = true;
                    } else {
                        if (mainAINavmeshManager.isCharacterAttacking ()) {
                            removeStateOnVehicle ();
                        }
                    }
                } else {
                    if (followingPartnerDrivingOnFoot) {
                        removeStateOnVehicle ();
                    }
                }
            } else {
                if (playerControllerPartner.isPlayerDriving ()) {
                    if (!characterOnVehicle && !mainAINavmeshManager.isCharacterAttacking ()) {

                        if (currentVehicle == null) {

                            isVehicleFull = false;

                            currentVehicle = playerControllerPartner.getCurrentVehicle ();

                            vehicleHUDManager currentVehicleHUDManagerToGetOn = currentVehicle.GetComponent<vehicleHUDManager> ();

                            if (currentVehicleHUDManagerToGetOn != null) {

                                mainFindObjectivesSystem.setExtraMinDistanceState (true, currentVehicleHUDManagerToGetOn.getVehicleRadius ());

                                mainAINavmeshManager.setExtraMinDistanceState (true, currentVehicleHUDManagerToGetOn.getVehicleRadius ());

                                mainFindObjectivesSystem.addNotEnemy (playerControllerPartner.gameObject);

                                isVehicleFull = currentVehicleHUDManagerToGetOn.isVehicleFull ();

                                if (showDebugPrint) {
                                    print ("partner is driving a GKC vehicle and the vehicle full result is " + isVehicleFull);
                                }
                            } else {
                                GKCSimpleRiderSystem currentGKCSimpleRiderSystemToGetOn = currentVehicle.GetComponent<GKCSimpleRiderSystem> ();

                                print (currentGKCSimpleRiderSystemToGetOn != null);

                                if (currentGKCSimpleRiderSystemToGetOn != null) {

                                    mainFindObjectivesSystem.setExtraMinDistanceState (true, currentGKCSimpleRiderSystemToGetOn.getVehicleRadius ());

                                    mainAINavmeshManager.setExtraMinDistanceState (true, currentGKCSimpleRiderSystemToGetOn.getVehicleRadius ());

                                    mainFindObjectivesSystem.addNotEnemy (playerControllerPartner.gameObject);

                                    isVehicleFull = currentGKCSimpleRiderSystemToGetOn.isVehicleFull () ||
                                    currentGKCSimpleRiderSystemToGetOn.isVehicleOnlyForOnePlayerActive ();

                                    if (showDebugPrint) {
                                        print ("partner is driving a GKC rider and the vehicle full result is " + isVehicleFull);
                                    }
                                }
                            }
                        }

                        if (!mainAINavmeshManager.isFollowingTarget ()) {
                            if (!AICharacterWasntAbleToEnterOnPartnerVehicle) {
                                if (mainPlayerController.canCharacterGetOnVehicles ()) {
                                    if (isVehicleFull) {
                                        AICharacterWasntAbleToEnterOnPartnerVehicle = true;
                                    } else {
                                        enterOnVehicle (currentVehicle);
                                    }
                                }
                            }
                        }
                    } else {
                        if (mainAINavmeshManager.isCharacterAttacking ()) {
                            getOffFromVehicle ();
                        }
                    }
                } else {
                    if (characterOnVehicle) {
                        getOffFromVehicle ();
                    } else if (AICharacterWasntAbleToEnterOnPartnerVehicle) {
                        removeStateOnVehicle ();
                    }
                }
            }
        }
    }

    public void setIgnoreCheckPartnerOnVehicleState (bool state)
    {
        ignoreCheckPartnerOnVehicle = state;
    }

    public void setCheckAIPassengerStateEnabledState (bool state)
    {
        checkAIPassengerStateEnabled = state;
    }

    public void enterOnVehicle (GameObject newVehicleObject)
    {
        if (AICharacterWasntAbleToEnterOnPartnerVehicle) {
            if (showDebugPrint) {
                print ("AI wasn't able to enter on vehicle, cancelling");
            }

            return;
        }

        if (showDebugPrint) {
            print ("characterOnVehicle " + true);
        }

        mainAINavmeshManager.pauseAI (true);

        usingDevicesManager.addDeviceToList (newVehicleObject);

        usingDevicesManager.getclosestDevice (false, false);

        usingDevicesManager.setCurrentVehicle (newVehicleObject);

        usingDevicesManager.useCurrentDevice (newVehicleObject);

        if (mainPlayerController.isPlayerDriving ()) {
            setCharacterOnVehicleState (true);

            AICharacterWasntAbleToEnterOnPartnerVehicle = false;
        } else {
            setCharacterOnVehicleState (false);

            AICharacterWasntAbleToEnterOnPartnerVehicle = true;

            mainAINavmeshManager.pauseAI (false);
        }

        if (showDebugPrint) {
            print (gameObject.name + " " + mainPlayerController.isPlayerDriving () + " " + characterOnVehicle);
        }
    }

    public override void getOffFromVehicle ()
    {
        if (showDebugPrint) {
            print ("getOffFromVehicle " + gameObject.name);
        }

        if (characterOnVehicle) {
            if (mainPlayerController.canCharacterGetOnVehicles ()) {
                if (currentVehicle != null) {
                    usingDevicesManager.clearDeviceList ();

                    usingDevicesManager.addDeviceToList (currentVehicle);

                    usingDevicesManager.getclosestDevice (false, false);

                    usingDevicesManager.setCurrentVehicle (currentVehicle);

                    usingDevicesManager.useCurrentDevice (currentVehicle);
                } else {
                    usingDevicesManager.useDevice ();
                }

                mainAINavmeshManager.pauseAI (false);
            }

            removeStateOnVehicle ();

            if (showDebugPrint) {
                print ("remove AI from vehicle " + gameObject.name);
            }
        } else {
            if (AICharacterWasntAbleToEnterOnPartnerVehicle) {
                removeStateOnVehicle ();

                if (showDebugPrint) {
                    print ("remove state on vehicle " + gameObject.name);
                }
            }
        }
    }

    void setCharacterOnVehicleState (bool state)
    {
        characterOnVehicle = state;

        if (showDebugPrint) {
            print (gameObject.name + " " + mainPlayerController.isPlayerDriving () + " " + characterOnVehicle);
        }
    }

    public override void removeStateOnVehicle ()
    {
        currentVehicle = null;

        setCharacterOnVehicleState (false);

        followingPartnerDrivingOnFoot = false;

        mainFindObjectivesSystem.setExtraMinDistanceState (false, 0);

        mainAINavmeshManager.setExtraMinDistanceState (false, 0);

        AICharacterWasntAbleToEnterOnPartnerVehicle = false;

        if (showDebugPrint) {
            print ("removeStateOnVehicle " + gameObject.name);
        }
    }

    public override void setAICharacterOnVehicle (GameObject newVehicle)
    {
        if (characterOnVehicle) {
            return;
        }

        currentVehicle = newVehicle;

        bool isVehicleFull = false;

        float vehicleRadius = 0;

        vehicleHUDManager currentVehicleHUDManagerToGetOn = currentVehicle.GetComponent<vehicleHUDManager> ();

        if (currentVehicleHUDManagerToGetOn != null) {
            isVehicleFull = currentVehicleHUDManagerToGetOn.isVehicleFull ();

            vehicleRadius = currentVehicleHUDManagerToGetOn.getVehicleRadius ();
        } else {
            GKCSimpleRiderSystem currentGKCSimpleRiderSystem = currentVehicle.GetComponent<GKCSimpleRiderSystem> ();

            if (currentGKCSimpleRiderSystem != null) {
                isVehicleFull = currentGKCSimpleRiderSystem.isVehicleFull () ||
                currentGKCSimpleRiderSystem.isVehicleOnlyForOnePlayerActive ();

                vehicleRadius = currentGKCSimpleRiderSystem.getVehicleRadius ();
            }
        }

        mainFindObjectivesSystem.setExtraMinDistanceState (true, vehicleRadius);

        mainAINavmeshManager.setExtraMinDistanceState (true, vehicleRadius);

        if (!isVehicleFull) {
            if (mainPlayerController.canCharacterGetOnVehicles ()) {
                enterOnVehicle (currentVehicle);
            }
        }
    }

    public bool isCharacterOnVehicle ()
    {
        return characterOnVehicle;
    }

    public override void setCurrentPlayerControllerPartner (playerController newPlayerController)
    {
        playerControllerPartner = newPlayerController;

        partnerFound = playerControllerPartner != null;
    }
}
