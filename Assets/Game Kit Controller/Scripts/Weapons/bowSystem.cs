using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class bowSystem : MonoBehaviour
{
    [Header ("Main Settings")]
    [Space]

    public int regularBowMovementID;
    public int loadedBowMovementID;

    public bool activateStrafeOnlyWhenAimingBow;

    [Space]

    public float targetRotation;

    public float minTimeToShootArrows = 2;

    public float minTimeToAimBow = 1.5f;

    public bool arrowsManagedByInventory;

    public bool activateBowCollisionWhenAiming;

    public bool stopAimBowOnLanding;

    public bool resetArrowIndexOnBowStateChangeEnabled = true;

    public bool activateAimingStateOnPlayerControllerEnabled = true;

    [Space]
    [Header ("Animation Settings")]
    [Space]

    public bool useDrawHolsterActionAnimations = true;
    public string drawBowActionName = "Draw Bow";
    public string holsterBowActionName = "Holster Bow";

    public string loadArrowActionName = "Load Arrow";
    public string fireArrowActionName = "Fire Arrow";

    [Space]
    [Header ("Damage Settings")]
    [Space]

    public bool useCustomIgnoreTags;

    public List<string> customTagsToIgnoreList = new List<string> ();

    [Space]
    [Header ("Third Person Camera State Settings")]
    [Space]

    public bool setNewCameraStateOnThirdPerson;

    public string newCameraStateOnThirdPerson;

    public bool setNewCameraStateOnFBA;

    public string newCameraStateOnFBA = "Full Body Awareness Aim Bow";

    [Space]
    [Header ("Fire Bow Without Aim Settings")]
    [Space]

    public bool canFireBowWithoutAimEnabled;

    public float delayToFireBowWithoutAim;

    public bool autoFireArrowsWithoutAim;
    public float waitTimeToStopAutoFire = 0.5f;

    public bool setCameraStateIfNoAimInputUsedEnabled;

    [Space]
    [Header ("Bow Weapon Types List")]
    [Space]

    public List<bowWeaponSystemInfo> bowWeaponSystemInfoList = new List<bowWeaponSystemInfo> ();

    [Space]

    public int currentBowWeaponID;

    [Space]
    [Header ("Bullet Time Settings")]
    [Space]

    public bool activateBulletTimeOnAir;
    public float bulletTimeScale;
    public float animationSpeedOnBulletTime = 1;

    public bool reduceGravityMultiplerOnBulletTimeOnAir;
    public float gravityMultiplerOnBulletTimeOnAir;

    public bool useBulletTimeOnBowExternallyEnabled;

    [Space]

    public bool useEventOnBulletTime;

    [Space]

    public UnityEvent eventOnBulletTimeStart;
    public UnityEvent eventOnBulletTimeEnd;

    [Space]
    [Header ("Other Settings")]
    [Space]

    public bool useUpperBodyRotationSystemToAim = true;

    public bool setNewUpperBodyRotationValues;

    public float horizontalBendingMultiplier = 1;
    public float verticalBendingMultiplier = 1;

    public float extraUpperBodyRotation = 90;

    public float headTrackBodyWeightOnAim = 1;

    public bool canMoveWhileUsingBowEnabled = true;

    public float cancelBowStateOnEmptyArrowsWaitDelay = 0.5f;

    [Space]
    [Header ("Durability Settings")]
    [Space]

    public bool ignoreDurability;
    public float generalAttackDurabilityMultiplier = 1;

    [Space]
    [Header ("Debug")]
    [Space]

    public bool showDebugPrint;

    public bool bowActive;

    public bool bowLoadedActive;

    public bool arrowIsFired;

    public bool aimingBowActive;

    public bool arrowsAvailableToFire = true;

    public bool checkArrowsOnInventory;

    public bool aimInputPressedDown;

    public bool bulletTimeOnAirActive;

    public bool aimInputPressedOnFireArrowWithoutAim;

    public bool fireArrowWithoutAimActive;

    public bool cancelBowCoroutineActive;

    public float lastTimePullBow;

    [Space]

    public int currentMultipleArrowTypeIndex;

    [Space]
    [Header ("Events Settings")]
    [Space]

    public UnityEvent eventOnBowEnabled;
    public UnityEvent eventOnBowDisabled;

    [Space]

    public UnityEvent eventOnStartAim;
    public UnityEvent eventOnEndAim;

    [Space]

    public UnityEvent eventOnFireArrow;

    [Space]

    public UnityEvent eventOnCancelLoadBowOnStopAim;

    [Space]
    [Header ("Components")]
    [Space]

    public playerActionSystem mainPlayerActionSystem;
    public playerController mainPlayerController;
    public playerCamera mainPlayerCamera;

    public upperBodyRotationSystem mainUpperBodyRotationSystem;

    public headTrack mainHeadTrack;

    public simpleWeaponSystem currentSimpleWeaponSystem;

    public Transform positionToPlaceArrow;
    public Transform arrowPositionReference;

    public grabbedObjectMeleeAttackSystem mainGrabbedObjectMeleeAttackSystem;

    public bowHolderSystem currentBowHolderSystem;

    public GameObject arrowMeshPrefab;
    public GameObject arrowMesh;

    public arrowManager mainArrowManager;

    float lastTimeShootArrow = 0;

    bowWeaponSystemInfo currentBowWeaponSystemInfo;

    durabilityInfo currentBowDurabilityInfo;

    float lastTimeAimBow;

    Coroutine checOnGrounCoroutine;

    Coroutine changeExtraRotation;

    Coroutine updateBowCoroutine;

    bool onGroundState;

    string previousCameraState = "";

    float lastTimeCheckFireArrowWithoutAim;

    bool pauseMoveInputActive;

    Coroutine cancelBowStateCoroutine;

    bool ignoreUseDrawHolsterActionAnimations;

    bool mainPlayerCameraLocated;

    bool originalArrowsManagedByInventory;


    void Start ()
    {
        originalArrowsManagedByInventory = arrowsManagedByInventory;
    }

    public void stopUpdateBowStateCoroutine ()
    {
        if (updateBowCoroutine != null) {
            StopCoroutine (updateBowCoroutine);
        }

        onGroundState = false;
    }

    IEnumerator updateBowStateCoroutine ()
    {
        var waitTime = new WaitForFixedUpdate ();

        while (true) {
            yield return waitTime;

            if (onGroundState) {
                if (!mainPlayerController.isPlayerOnGround ()) {
                    onGroundState = false;
                }

                if (!canMoveWhileUsingBowEnabled) {
                    if (aimingBowActive || fireArrowWithoutAimActive) {
                        if (!pauseMoveInputActive) {
                            mainPlayerController.setMoveInputPausedState (true);
                            mainPlayerController.resetPlayerControllerInput ();

                            pauseMoveInputActive = true;
                        }
                    } else {
                        if (pauseMoveInputActive) {
                            mainPlayerController.setMoveInputPausedState (false);

                            pauseMoveInputActive = false;
                        }
                    }
                }

                if (onGroundState) {
                    if (mainPlayerController.isCrouching ()) {
                        cancelBowState ();

                        aimInputPressedDown = false;
                    }
                }
            } else {
                if (mainPlayerController.isPlayerOnGround ()) {
                    onGroundState = true;

                    if (stopAimBowOnLanding) {
                        if (aimingBowActive) {
                            cancelBowState ();

                            aimInputPressedDown = false;
                        }
                    }
                }

                if (pauseMoveInputActive) {
                    mainPlayerController.setMoveInputPausedState (false);

                    pauseMoveInputActive = false;
                }
            }

            if (fireArrowWithoutAimActive) {
                if (Time.unscaledTime > lastTimeCheckFireArrowWithoutAim + delayToFireBowWithoutAim) {
                    inputFireArrow ();

                    if (!autoFireArrowsWithoutAim) {
                        stopAimAfterFiringArrowWithoutAimActive = true;
                    }

                    lastTimeCheckFireArrowWithoutAim = Time.unscaledTime;
                } else {
                    if (stopAimAfterFiringArrowWithoutAimActive) {
                        if (Time.unscaledTime > lastTimeCheckFireArrowWithoutAim + waitTimeToStopAutoFire) {
                            fireArrowWithoutAimActive = false;

                            setAimState (false);

                            stopAimAfterFiringArrowWithoutAimActive = false;
                        }
                    }
                }
            }

            if (bowLoadedActive && lastTimePullBow != 0) {
                if (currentBowWeaponSystemInfo.usePullBowMultipliers &&
                    currentBowWeaponSystemInfo.useEventOnMaxPullBowMultiplier &&
                    !currentBowWeaponSystemInfo.eventOnMaxPullBowMultiplierActivated) {

                    bool checkPullBowMultipliersResult = true;

                    if (currentBowWeaponSystemInfo.ignorePullMultiplierIfShootingArrowWithoutAim && fireArrowWithoutAimActive) {
                        checkPullBowMultipliersResult = false;
                    }

                    if (checkPullBowMultipliersResult) {
                        checkEventOnMaxPullBowMultiplierStartOrEnd (true);
                    }
                }
            }
        }
    }

    bool stopAimAfterFiringArrowWithoutAimActive;

    public void setBowLoadedState (bool state)
    {
        bowLoadedActive = state;

        if (bowLoadedActive) {
            mainPlayerController.setCurrentStrafeIDValue (loadedBowMovementID);

            if (activateStrafeOnlyWhenAimingBow || (mainPlayerCameraLocated && mainPlayerCamera.is2_5ViewActive ())) {
                mainPlayerController.activateOrDeactivateStrafeMode (true);
            }

            mainPlayerActionSystem.activateCustomAction (loadArrowActionName);

            arrowIsFired = false;

            checkSpawnArrowInPlayerHand ();
        } else {
            mainPlayerController.setCurrentStrafeIDValue (regularBowMovementID);

            if (activateStrafeOnlyWhenAimingBow || (mainPlayerCameraLocated && mainPlayerCamera.is2_5ViewActive ())) {
                mainPlayerController.activateOrDeactivateStrafeMode (false);
            }

            setPositionToPlaceArrowActiveState (false);

            fireArrowWithoutAimActive = false;
        }

        if (useUpperBodyRotationSystemToAim) {
            mainUpperBodyRotationSystem.enableOrDisableIKUpperBody (bowLoadedActive);

            checkUpperBodyRotationSystemValues (bowLoadedActive);
        } else {
            if (bowLoadedActive) {
                mainHeadTrack.setCameraBodyWeightValue (headTrackBodyWeightOnAim);
            } else {
                mainHeadTrack.setOriginalCameraBodyWeightValue ();
            }
        }

        mainHeadTrack.setHeadTrackActiveWhileAimingState (bowLoadedActive);

        mainHeadTrack.setCanUseHeadTrackOnLockedCameraActiveState (bowLoadedActive);

        if (activateAimingStateOnPlayerControllerEnabled) {
            mainPlayerController.enableOrDisableAiminig (bowLoadedActive);
        }

        if (useUpperBodyRotationSystemToAim) {
            checkSetExtraRotationCoroutine (bowLoadedActive);
        }

        setAimBowState (bowLoadedActive);

        stopCancelBowStateCoroutine ();

        cancelBowCoroutineActive = false;

        checkEventOnMaxPullBowMultiplierStartOrEnd (false);
    }

    void checkUpperBodyRotationSystemValues (bool state)
    {
        if (setNewUpperBodyRotationValues) {
            if (state) {
                mainUpperBodyRotationSystem.setHorizontalBendingMultiplierValue (horizontalBendingMultiplier);

                mainUpperBodyRotationSystem.setVerticalBendingMultiplierValue (verticalBendingMultiplier);
            } else {
                mainUpperBodyRotationSystem.setOriginalHorizontalBendingMultiplier ();

                mainUpperBodyRotationSystem.setOriginalVerticalBendingMultiplier ();
            }
        }
    }

    int lastAmountOfFiredProjectiles;

    public void resetLastAmountOfFiredProjectiles ()
    {
        lastAmountOfFiredProjectiles = 0;
    }

    public int getLastAmountOfFiredProjectiles ()
    {
        return lastAmountOfFiredProjectiles;
    }

    public void fireLoadedArrow ()
    {
        if (bowActive && bowLoadedActive) {
            mainPlayerActionSystem.activateCustomAction (fireArrowActionName);

            arrowIsFired = true;

            lastTimeShootArrow = Time.unscaledTime;

            eventOnFireArrow.Invoke ();

            //			setPullBowState (false);

            if (arrowsManagedByInventory) {
                if (checkArrowsOnInventory) {
                    mainArrowManager.useArrowFromInventory (1);

                    checkIfArrowsFoundOnInventoryToCancelBowState ();
                }
            }

            mainGrabbedObjectMeleeAttackSystem.checkDurabilityOnAttackOnCurrentWeapon (ignoreDurability, generalAttackDurabilityMultiplier);

            lastAmountOfFiredProjectiles++;
        }
    }

    public void checkIfArrowsFoundOnInventoryToCancelBowState ()
    {
        if (arrowsManagedByInventory) {
            if (checkArrowsOnInventory) {
                mainArrowManager.checkIfArrowsFoundOnInventory ();

                if (!arrowsAvailableToFire) {
                    //					cancelBowState ();

                    stopCancelBowStateCoroutine ();

                    cancelBowStateCoroutine = StartCoroutine (activatCancelBowStateCoroutine ());
                }
            }
        }
    }

    void stopCancelBowStateCoroutine ()
    {
        if (cancelBowStateCoroutine != null) {
            StopCoroutine (cancelBowStateCoroutine);
        }

        cancelBowCoroutineActive = false;
    }

    IEnumerator activatCancelBowStateCoroutine ()
    {
        cancelBowCoroutineActive = true;

        yield return new WaitForSecondsRealtime (cancelBowStateOnEmptyArrowsWaitDelay);

        cancelBowCoroutineActive = false;

        cancelBowState ();
    }

    public void activateLoadBowAction ()
    {
        if (!aimingBowActive) {
            if (showDebugPrint) {
                print ("no aiming active, cancelling load bow action");
            }

            return;
        }

        if (arrowsManagedByInventory) {
            if (checkArrowsOnInventory) {
                mainArrowManager.checkIfArrowsFoundOnInventory ();

                if (!arrowsAvailableToFire) {

                    return;
                }
            }
        }

        mainPlayerActionSystem.activateCustomAction (loadArrowActionName);
    }

    public void setAimBowState (bool state)
    {
        if (bowActive) {
            aimingBowActive = state;

            mainGrabbedObjectMeleeAttackSystem.setAimBowState (state);

            if (aimingBowActive) {
                eventOnStartAim.Invoke ();

                checkBowEventOnStartAim ();
            } else {
                eventOnEndAim.Invoke ();

                checkBowEventOnEndAim ();
            }

            if (!fireArrowWithoutAimActive) {
                checkCameraState (state);
            }

            if (activateBowCollisionWhenAiming) {
                mainGrabbedObjectMeleeAttackSystem.setGrabbedObjectClonnedColliderEnabledState (aimingBowActive);
            }

            if (activateBulletTimeOnAir) {
                setTimeBulletOnBowSystem (aimingBowActive);
            } else {
                stopCheckIfPlayerOnGroundCoroutine ();

                checOnGrounCoroutine = StartCoroutine (checkIfPlayerOnGroundCoroutine (false));
            }
        }
    }

    bool ignorePlayerOnGroundCheckOnTimeBulletActive;

    public void setTimeBulletOnBowSystemExternally (bool state)
    {
        if (useBulletTimeOnBowExternallyEnabled) {
            if (bowActive || (bulletTimeOnAirActive && !state)) {
                ignorePlayerOnGroundCheckOnTimeBulletActive = true;

                setTimeBulletOnBowSystem (state);

                ignorePlayerOnGroundCheckOnTimeBulletActive = false;
            }
        }
    }

    void setTimeBulletOnBowSystem (bool state)
    {
        if (state) {
            if (!mainPlayerController.isPlayerOnGround () || ignorePlayerOnGroundCheckOnTimeBulletActive) {
                GKC_Utils.activateTimeBulletXSeconds (0, bulletTimeScale);

                bulletTimeOnAirActive = true;

                if (!ignorePlayerOnGroundCheckOnTimeBulletActive) {
                    stopCheckIfPlayerOnGroundCoroutine ();

                    checOnGrounCoroutine = StartCoroutine (checkIfPlayerOnGroundCoroutine (true));
                }

                if (animationSpeedOnBulletTime != 1) {
                    mainPlayerController.setReducedVelocity (animationSpeedOnBulletTime);
                }

                checkEventOnBulletTimeState (true);
            }
        } else {
            if (bulletTimeOnAirActive) {
                GKC_Utils.activateTimeBulletXSeconds (0, 1);

                bulletTimeOnAirActive = false;

                if (animationSpeedOnBulletTime != 1) {
                    mainPlayerController.setNormalVelocity ();
                }

                if (reduceGravityMultiplerOnBulletTimeOnAir) {
                    mainPlayerController.setGravityMultiplierValue (true, 0);
                }

                checkEventOnBulletTimeState (false);
            }
        }
    }

    void checkEventOnBulletTimeState (bool state)
    {
        if (useEventOnBulletTime) {
            if (state) {
                eventOnBulletTimeStart.Invoke ();
            } else {
                eventOnBulletTimeEnd.Invoke ();
            }
        }
    }
    public void setBowActiveState (bool state)
    {
        if (bowActive == state) {
            return;
        }

        bowActive = state;

        if (bowActive) {
            mainPlayerCameraLocated = mainPlayerCamera != null;

            if (useDrawHolsterActionAnimations && !ignoreUseDrawHolsterActionAnimations) {
                if (Time.unscaledTime > mainGrabbedObjectMeleeAttackSystem.getLastTimeObjectReturn () + 0.7f &&
                    !mainGrabbedObjectMeleeAttackSystem.isCurrentWeaponThrown ()) {

                    mainPlayerActionSystem.activateCustomAction (drawBowActionName);
                }
            }

            eventOnBowEnabled.Invoke ();

            Transform currentGrabbedObjectTransform = mainGrabbedObjectMeleeAttackSystem.getCurrentGrabbedObjectTransform ();

            if (currentGrabbedObjectTransform == null) {

                return;
            }

            currentBowHolderSystem = currentGrabbedObjectTransform.GetComponent<bowHolderSystem> ();

            currentBowDurabilityInfo = currentGrabbedObjectTransform.GetComponent<durabilityInfo> ();

            currentBowWeaponID = currentBowHolderSystem.getBowWeaponID ();

            if (resetArrowIndexOnBowStateChangeEnabled) {
                currentMultipleArrowTypeIndex = 0;
            } else {
                if (currentBowHolderSystem.canUseMultipleArrowType) {
                    if (currentBowHolderSystem.bowWeaponIDList.Count > 0) {
                        if (currentMultipleArrowTypeIndex >= currentBowHolderSystem.bowWeaponIDList.Count) {
                            currentMultipleArrowTypeIndex = 0;
                        }

                        currentBowWeaponID = currentBowHolderSystem.bowWeaponIDList [currentMultipleArrowTypeIndex];
                    } else {
                        currentMultipleArrowTypeIndex = 0;
                    }
                } else {
                    currentMultipleArrowTypeIndex = 0;
                }
            }

            setCurrentBowWeaponSystemByID (currentBowWeaponID);

            if (arrowsManagedByInventory) {
                if (checkArrowsOnInventory) {
                    mainArrowManager.updateArrowAmountText ();

                    mainArrowManager.checkIfArrowsFoundOnInventory ();
                }
            }
        } else {
            bool canPlayHolsterAction = true;

            if (mainPlayerController.isActionActive ()) {
                if (!mainPlayerController.canPlayerMove ()) {
                    canPlayHolsterAction = false;
                }
            } else {
                if (!mainPlayerController.canPlayerMove ()) {
                    canPlayHolsterAction = false;
                }
            }

            //			print (canPlayHolsterAction);

            if (currentBowDurabilityInfo != null) {
                if (currentBowDurabilityInfo.isDurabilityEmpty ()) {
                    canPlayHolsterAction = false;
                }
            }

            if (canPlayHolsterAction) {
                if (useDrawHolsterActionAnimations && !ignoreUseDrawHolsterActionAnimations) {
                    if (Time.unscaledTime > mainGrabbedObjectMeleeAttackSystem.getLastTimeObjectThrown () + 0.7f &&
                        !mainGrabbedObjectMeleeAttackSystem.isCurrentWeaponThrown ()) {

                        mainPlayerActionSystem.activateCustomAction (holsterBowActionName);
                    }
                }
            }

            eventOnBowDisabled.Invoke ();

            if (bowLoadedActive) {
                if (arrowIsFired) {
                    mainPlayerActionSystem.stopCustomAction (loadArrowActionName);
                }

                if (useUpperBodyRotationSystemToAim) {
                    mainUpperBodyRotationSystem.enableOrDisableIKUpperBody (false);

                    checkUpperBodyRotationSystemValues (false);
                } else {
                    mainHeadTrack.setOriginalCameraBodyWeightValue ();
                }

                mainHeadTrack.setHeadTrackActiveWhileAimingState (false);

                mainHeadTrack.setCanUseHeadTrackOnLockedCameraActiveState (false);

                if (activateAimingStateOnPlayerControllerEnabled) {
                    mainPlayerController.enableOrDisableAiminig (false);
                }

                if (useUpperBodyRotationSystemToAim) {
                    checkSetExtraRotationCoroutine (false);
                }

                if (aimingBowActive && activateBowCollisionWhenAiming) {
                    mainGrabbedObjectMeleeAttackSystem.setGrabbedObjectClonnedColliderEnabledState (false);
                }

                if (activateBulletTimeOnAir) {
                    setTimeBulletOnBowSystem (false);
                }

                aimingBowActive = false;

                mainGrabbedObjectMeleeAttackSystem.setAimBowState (false);

                bowLoadedActive = false;

                if (activateStrafeOnlyWhenAimingBow || (mainPlayerCameraLocated && mainPlayerCamera.is2_5ViewActive ())) {
                    mainPlayerController.activateOrDeactivateStrafeMode (false);
                }

                eventOnEndAim.Invoke ();

                checkBowEventOnEndAim ();

                checkCameraState (false);

                if (lastTimePullBow == 0 || !mainGrabbedObjectMeleeAttackSystem.isCarryingObject ()) {
                    if (showDebugPrint) {
                        print ("cancel bow action");
                    }

                    checkEventOnCancelBowAction ();
                }

                checkEventOnMaxPullBowMultiplierStartOrEnd (false);
            }

            setPositionToPlaceArrowActiveState (false);

            currentBowHolderSystem = null;

            currentBowDurabilityInfo = null;

            if (arrowsManagedByInventory) {
                if (checkArrowsOnInventory) {
                    mainArrowManager.enableOrDisableArrowInfoPanel (bowActive);
                }
            }

            if (currentSimpleWeaponSystem != null) {
                currentSimpleWeaponSystem.enabled = false;
            }
        }

        aimInputPressedDown = false;

        stopUpdateBowStateCoroutine ();

        if (bowActive) {
            updateBowCoroutine = StartCoroutine (updateBowStateCoroutine ());
        }

        if (!bowActive) {
            if (pauseMoveInputActive) {
                mainPlayerController.setMoveInputPausedState (false);

                pauseMoveInputActive = false;
            }
        }
    }

    public void cancelBowLoadedStateIfActive ()
    {
        if (bowActive) {
            cancelBowState ();

            aimInputPressedDown = false;
        }
    }

    public void cancelBowState ()
    {
        if (bowLoadedActive) {
            setBowLoadedState (false);
        }
    }

    //if the player is in aim mode, enable the upper body to rotate with the camera movement
    public void checkSetExtraRotationCoroutine (bool state)
    {
        if (changeExtraRotation != null) {
            StopCoroutine (changeExtraRotation);
        }

        changeExtraRotation = StartCoroutine (setExtraRotation (state));
    }

    IEnumerator setExtraRotation (bool state)
    {
        if (extraUpperBodyRotation != 0 || (!state && targetRotation != extraUpperBodyRotation)) {
            for (float t = 0; t < 1;) {
                t += Time.unscaledDeltaTime;

                if (state) {
                    targetRotation = Mathf.Lerp (targetRotation, extraUpperBodyRotation, t);
                } else {
                    targetRotation = Mathf.Lerp (targetRotation, 0, t);
                }

                mainUpperBodyRotationSystem.setCurrentBodyRotation (targetRotation);

                yield return null;
            }
        }
    }

    public void stopCheckIfPlayerOnGroundCoroutine ()
    {
        if (checOnGrounCoroutine != null) {
            StopCoroutine (checOnGrounCoroutine);
        }
    }

    IEnumerator checkIfPlayerOnGroundCoroutine (bool checkBulletTime)
    {
        bool targetReached = false;

        bool checkGravityMultiplier = false;

        float lastTimeGravityMultiplierChecked = Time.unscaledTime;

        while (!targetReached) {

            if (checkBulletTime) {
                if (reduceGravityMultiplerOnBulletTimeOnAir) {
                    if (!checkGravityMultiplier) {
                        if (Time.unscaledTime > lastTimeGravityMultiplierChecked + 0.3f) {
                            if (reduceGravityMultiplerOnBulletTimeOnAir) {
                                mainPlayerController.setGravityMultiplierValue (false, gravityMultiplerOnBulletTimeOnAir);

                                checkGravityMultiplier = true;
                            }
                        }
                    }
                }
            }

            if (mainPlayerController.isPlayerOnGround ()) {
                targetReached = true;
            }

            yield return null;
        }

        if (checkBulletTime) {
            if (activateBulletTimeOnAir) {
                setTimeBulletOnBowSystem (false);
            }
        }
    }

    public void checkSpawnArrowInPlayerHand ()
    {
        if (arrowMesh == null) {
            arrowMesh = (GameObject)Instantiate (arrowMeshPrefab);
            arrowMesh.transform.SetParent (positionToPlaceArrow);
            arrowMesh.transform.localPosition = arrowPositionReference.localPosition;
            arrowMesh.transform.localRotation = arrowPositionReference.localRotation;
        }
    }

    public void checkBowHolderLoadArrow ()
    {
        if (!aimingBowActive) {
            if (showDebugPrint) {
                print ("trying to load arrow when the bow is not being aimed, cancelling");
            }

            setPositionToPlaceArrowActiveState (false);

            return;
        }

        getCurrentBowHolderSystem ();

        if (currentBowHolderSystem != null) {
            currentBowHolderSystem.checkEventOnLoadArrow ();
        }
    }

    public void checkBowHolderFireArrow ()
    {
        getCurrentBowHolderSystem ();

        if (currentBowHolderSystem != null) {
            currentBowHolderSystem.checkEventOnFireArrow ();
        }
    }

    public void checkUsePullBowMultipliersOnFireArrow ()
    {
        bool checkPullBowMultipliersResult = false;

        if (currentBowWeaponSystemInfo != null && currentBowWeaponSystemInfo.usePullBowMultipliers) {
            checkPullBowMultipliersResult = true;
        }

        if (checkPullBowMultipliersResult) {
            if (currentBowWeaponSystemInfo.ignorePullMultiplierIfShootingArrowWithoutAim && fireArrowWithoutAimActive) {
                checkPullBowMultipliersResult = false;
            }
        }

        if (checkPullBowMultipliersResult) {
            if (lastTimePullBow > 0 && currentSimpleWeaponSystem != null) {
                float timeTime = Time.unscaledTime;

                if (showDebugPrint) {
                    print ("bow hold time " + (timeTime - lastTimePullBow));
                }

                float totalPullBowForce = (timeTime - lastTimePullBow) / currentBowWeaponSystemInfo.pullBowForceRateMultiplier;

                totalPullBowForce = currentBowWeaponSystemInfo.initialPullBowForceRateMultiplier - totalPullBowForce;

                if (totalPullBowForce <= 0) {
                    totalPullBowForce = 1;
                }


                float totalPullBowDamageMultiplier = (timeTime - lastTimePullBow) * currentBowWeaponSystemInfo.pullBowDamageRateMultiplier;

                totalPullBowDamageMultiplier += 1;

                totalPullBowDamageMultiplier = Mathf.Clamp (totalPullBowDamageMultiplier, 1, currentBowWeaponSystemInfo.maxPullBowDamageRateMultiplier);

                if (showDebugPrint) {
                    print ("total pull down force " + totalPullBowForce + " total damage multiplier " + totalPullBowDamageMultiplier);
                }

                List<projectileSystem> lastProjectilesSystemListFired = currentSimpleWeaponSystem.getLastProjectilesSystemListFired ();

                if (lastProjectilesSystemListFired.Count > 0) {
                    for (int i = 0; i < lastProjectilesSystemListFired.Count; i++) {

                        if (lastProjectilesSystemListFired [i] != null) {
                            if (showDebugPrint) {
                                print ("setting projectile info");
                            }

                            lastProjectilesSystemListFired [i].setProjectileDamageMultiplier (totalPullBowDamageMultiplier);

                            arrowProjectile currentArrowProjectile = lastProjectilesSystemListFired [i].gameObject.GetComponent<arrowProjectile> ();

                            if (currentArrowProjectile != null) {

                                currentArrowProjectile.setNewArrowDownForce (totalPullBowForce);

                                if (currentBowWeaponSystemInfo.checkSurfaceTypeDetected) {
                                    currentArrowProjectile.setArrowSurfaceTypeInfoList (currentBowWeaponSystemInfo.arrowSurfaceTypeInfoList);
                                }
                            }
                        }
                    }
                }
            }

            checkEventOnMaxPullBowMultiplierStartOrEnd (false);
        }
    }

    void checkEventOnMaxPullBowMultiplierStartOrEnd (bool state)
    {
        if (state) {
            if (!currentBowWeaponSystemInfo.eventOnMaxPullBowMultiplierActivated) {
                if (Time.unscaledTime > lastTimePullBow + currentBowWeaponSystemInfo.pullBowMultiplierDelay) {
                    currentBowWeaponSystemInfo.eventOnMaxPullBowMultiplierStart.Invoke ();

                    currentBowHolderSystem.checkEventOnMaxPullBowMultiplierStart ();

                    currentBowWeaponSystemInfo.eventOnMaxPullBowMultiplierActivated = true;

                    if (showDebugPrint) {
                        print ("checkEventOnMaxPullBowMultiplierStartOrEnd true");
                    }
                }
            }
        } else {
            if (currentBowWeaponSystemInfo.eventOnMaxPullBowMultiplierActivated) {
                currentBowWeaponSystemInfo.eventOnMaxPullBowMultiplierEnd.Invoke ();

                currentBowHolderSystem.checkEventOnMaxPullBowMultiplierEnd ();

                currentBowWeaponSystemInfo.eventOnMaxPullBowMultiplierActivated = false;

                if (showDebugPrint) {
                    print ("checkEventOnMaxPullBowMultiplierStartOrEnd false");
                }
            }
        }
    }

    public void checkBowEventOnStartAim ()
    {
        getCurrentBowHolderSystem ();

        if (currentBowHolderSystem != null) {
            currentBowHolderSystem.checkEventOnStartAim ();
        }
    }

    public void checkBowEventOnEndAim ()
    {
        getCurrentBowHolderSystem ();

        if (currentBowHolderSystem != null) {
            currentBowHolderSystem.checkEventOnEndAim ();
        }
    }

    public void checkEventOnCancelBowAction ()
    {
        getCurrentBowHolderSystem ();

        if (currentBowHolderSystem != null) {
            currentBowHolderSystem.checkEventOnCancelBowAction ();
        }
    }

    void getCurrentBowHolderSystem ()
    {
        if (currentBowHolderSystem == null) {
            currentBowHolderSystem = mainPlayerController.GetComponentInChildren<bowHolderSystem> ();

            if (currentBowHolderSystem != null) {
                currentBowDurabilityInfo = currentBowHolderSystem.gameObject.GetComponent<durabilityInfo> ();
            }
        }
    }

    public void setCurrentBowWeaponSystemByID (int bowWeaponID)
    {
        for (int i = 0; i < bowWeaponSystemInfoList.Count; i++) {
            if (bowWeaponSystemInfoList [i].bowWeaponID == bowWeaponID) {
                currentBowWeaponSystemInfo = bowWeaponSystemInfoList [i];

                currentBowWeaponSystemInfo.isCurrentBowWeapon = true;

                currentSimpleWeaponSystem = currentBowWeaponSystemInfo.mainSimpleWeaponSystem;

                currentSimpleWeaponSystem.enabled = true;

                if (useCustomIgnoreTags) {
                    currentSimpleWeaponSystem.setCustomTagsToIgnore (customTagsToIgnoreList);
                }

                currentBowWeaponID = bowWeaponID;

                currentMultipleArrowTypeIndex = currentBowHolderSystem.bowWeaponIDList.IndexOf (currentBowWeaponID);

                if (currentMultipleArrowTypeIndex < 0) {
                    currentMultipleArrowTypeIndex = currentBowWeaponID;
                }

                mainArrowManager.enableOrDisableArrowInfoPanel (currentBowWeaponSystemInfo.showArrowTypeIcon);

                mainArrowManager.setArrowTypeIcon (currentBowWeaponSystemInfo.bowIcon, !currentBowWeaponSystemInfo.checkArrowsOnInventory);

                if (arrowsManagedByInventory) {
                    checkArrowsOnInventory = currentBowWeaponSystemInfo.checkArrowsOnInventory;

                    if (checkArrowsOnInventory) {
                        mainArrowManager.setCurrentArrowInventoryObjectName (currentBowWeaponSystemInfo.arrowInventoryObjectName);
                    }
                }

                if (currentBowWeaponSystemInfo.useEventOnBowSelected) {
                    currentBowWeaponSystemInfo.eventOnBowSelected.Invoke ();
                }
            } else {
                if (bowWeaponSystemInfoList [i].isCurrentBowWeapon) {
                    if (bowWeaponSystemInfoList [i].useEventOnBowSelected) {
                        bowWeaponSystemInfoList [i].eventOnBowUnselected.Invoke ();
                    }

                    bowWeaponSystemInfoList [i].mainSimpleWeaponSystem.enabled = false;
                }

                bowWeaponSystemInfoList [i].isCurrentBowWeapon = false;
            }
        }
    }

    public void fireCurrentBowWeapon ()
    {
        if (bowActive) {
            currentSimpleWeaponSystem.inputShootWeaponOnPressDown ();

            currentSimpleWeaponSystem.inputShootWeaponOnPressUp ();
        }
    }

    public void setPullBowState (bool state)
    {
        bool checkPullBowMultipliersResult = false;

        if (currentBowWeaponSystemInfo != null && currentBowWeaponSystemInfo.usePullBowMultipliers) {
            checkPullBowMultipliersResult = true;
        }

        if (checkPullBowMultipliersResult) {
            if (currentBowWeaponSystemInfo.ignorePullMultiplierIfShootingArrowWithoutAim && fireArrowWithoutAimActive) {
                checkPullBowMultipliersResult = false;
            }
        }

        if (checkPullBowMultipliersResult) {
            if (state) {
                if (showDebugPrint) {
                    print ("start to pull");
                }

                lastTimePullBow = Time.unscaledTime;

                if (showDebugPrint) {
                    print ("set pull bow state true");
                }

                checkEventOnMaxPullBowMultiplierStartOrEnd (false);
            } else {
                lastTimePullBow = 0;
            }
        } else {
            if (state) {
                lastTimePullBow = Time.unscaledTime;
            } else {
                lastTimePullBow = 0;
            }
        }
    }

    public void setArrowsAvailableToFireState (bool state)
    {
        arrowsAvailableToFire = state;
    }

    public void setPositionToPlaceArrowActiveState (bool state)
    {
        if (positionToPlaceArrow.gameObject.activeSelf != state) {
            positionToPlaceArrow.gameObject.SetActive (state);
        }
    }

    public bool canUseWeaponsInput ()
    {
        if (mainPlayerController.canPlayerMove () && !mainPlayerController.playerIsBusy ()) {
            return true;
        }

        return false;
    }

    void checkCameraState (bool state)
    {
        if (mainPlayerCamera == null) {
            return;
        }

        bool isFirstPersonActive = mainPlayerController.isPlayerOnFirstPerson ();

        bool isFullBodyAwarenessActive = mainPlayerCamera.isFullBodyAwarenessActive ();

        bool canChangeViewResult = false;

        if (!isFirstPersonActive) {
            if (isFullBodyAwarenessActive) {
                if (setNewCameraStateOnFBA) {
                    canChangeViewResult = true;
                }
            } else {
                if (setNewCameraStateOnThirdPerson) {
                    canChangeViewResult = true;
                }
            }
        }

        if (canChangeViewResult) {
            if (state) {
                previousCameraState = mainPlayerCamera.getCurrentStateName ();

                if (isFullBodyAwarenessActive) {
                    if (setNewCameraStateOnFBA) {
                        mainPlayerCamera.setCameraState (newCameraStateOnFBA);
                    }
                } else {
                    mainPlayerCamera.setCameraStateOnlyOnThirdPerson (newCameraStateOnThirdPerson);
                }
            } else {

                if (previousCameraState != "") {
                    if (isFullBodyAwarenessActive) {
                        if (previousCameraState != newCameraStateOnFBA) {
                            mainPlayerCamera.setCameraState (previousCameraState);
                        }
                    } else {
                        if (previousCameraState != newCameraStateOnThirdPerson) {
                            mainPlayerCamera.setCameraStateOnlyOnThirdPerson (previousCameraState);
                        }
                    }

                    previousCameraState = "";
                }
            }
        }
    }

    void setAimState (bool state)
    {
        if (bowActive) {
            if (showDebugPrint) {
                print ("input aim bow " + state + " " + bowActive);
            }

            if (!canUseWeaponsInput ()) {
                if (state) {
                    return;
                } else {
                    if (!aimInputPressedDown) {
                        print ("no input down pressed before, cancelling");

                        return;
                    }
                }
            }

            if (!bowLoadedActive && state) {
                if (arrowsManagedByInventory) {
                    mainArrowManager.checkIfArrowsFoundOnInventory ();

                    if (!arrowsAvailableToFire) {
                        if (currentSimpleWeaponSystem != null && bowWeaponSystemInfoList [currentMultipleArrowTypeIndex].checkArrowsOnInventory) {
                            return;
                        }
                    }
                }
            } else {
                if (!state && !bowLoadedActive) {
                    if (arrowsManagedByInventory) {
                        mainArrowManager.checkIfArrowsFoundOnInventory ();

                        if (!arrowsAvailableToFire) {
                            if (showDebugPrint) {
                                print ("no arrows available when releasing the aim button");
                            }

                            return;
                        }
                    }
                }
            }

            aimInputPressedDown = state;

            bool bowLoadedButNotFired = bowLoadedActive && !arrowIsFired;

            if (bowLoadedButNotFired) {
                if (showDebugPrint) {
                    print ("bow loaded but not fired");
                }

                mainPlayerActionSystem.stopCustomActionIfCurrentlyActive (loadArrowActionName);

                eventOnCancelLoadBowOnStopAim.Invoke ();
            }

            setBowLoadedState (state);

            lastTimeAimBow = Time.unscaledTime;

            lastTimePullBow = 0;

            if (!state) {
                setPositionToPlaceArrowActiveState (false);
            }

            if (showDebugPrint) {
                print ("aim " + state);
            }
        }
    }

    public void setIgnoreUseDrawHolsterActionAnimationsState (bool state)
    {
        ignoreUseDrawHolsterActionAnimations = state;
    }

    public void setUseDrawHolsterActionAnimationsState (bool state)
    {
        useDrawHolsterActionAnimations = state;
    }

    public void setArrowsManagedByInventoryState (bool state)
    {
        arrowsManagedByInventory = state;
    }

    public void setOriginalArrowsManagedByInventoryState ()
    {
        setArrowsManagedByInventoryState (originalArrowsManagedByInventory);
    }

    public bool isAimingBowActive ()
    {
        return aimingBowActive;
    }

    //INPUT FUNCTIONS
    public void inputSetAimBowState (bool state)
    {
        if (!bowActive) {
            return;
        }

        if (state) {

            if (mainPlayerController.isCrouching ()) {
                mainPlayerController.crouch ();

                if (mainPlayerController.isCrouching ()) {
                    return;
                }
            }
        }

        if (fireArrowWithoutAimActive) {
            checkCameraState (true);

            if (state) {
                aimInputPressedOnFireArrowWithoutAim = true;
            }
        } else {
            setAimState (state);
        }

        fireArrowWithoutAimActive = false;

        stopAimAfterFiringArrowWithoutAimActive = false;

        if (!state) {
            aimInputPressedOnFireArrowWithoutAim = false;
        }
    }

    public void inputFireArrow ()
    {
        if (bowActive) {
            if (!canUseWeaponsInput ()) {
                return;
            }

            if (Time.unscaledTime > minTimeToShootArrows + lastTimeShootArrow &&
                Time.unscaledTime > lastTimeAimBow + minTimeToAimBow && lastTimePullBow > 0 && bowLoadedActive) {

                fireLoadedArrow ();

                if (showDebugPrint) {
                    print ("fire");
                }

                if (showDebugPrint) {
                    print ("bowLoadedActive " + bowLoadedActive);
                }

                return;
            }

            if (canFireBowWithoutAimEnabled && !bowLoadedActive) {
                fireArrowWithoutAimActive = true;

                lastTimeCheckFireArrowWithoutAim = Time.unscaledTime;

                setAimState (true);

                if (setCameraStateIfNoAimInputUsedEnabled) {
                    checkCameraState (true);
                }
            }
        }
    }

    public void inputStopFireArrowWithoutAim ()
    {
        if (bowActive) {
            if (fireArrowWithoutAimActive) {
                if (!aimInputPressedOnFireArrowWithoutAim) {
                    setAimState (false);
                }

                lastTimeCheckFireArrowWithoutAim = 0;

                fireArrowWithoutAimActive = false;

                stopAimAfterFiringArrowWithoutAimActive = false;
            }
        }
    }

    public void inputChangeArrowType ()
    {
        if (bowActive) {
            if (!canUseWeaponsInput ()) {
                return;
            }

            if (aimingBowActive) {
                return;
            }

            if (currentBowHolderSystem.canUseMultipleArrowType) {

                currentMultipleArrowTypeIndex++;

                if (currentMultipleArrowTypeIndex >= currentBowHolderSystem.bowWeaponIDList.Count) {
                    currentMultipleArrowTypeIndex = 0;
                }

                setCurrentBowWeaponSystemByID (currentBowHolderSystem.bowWeaponIDList [currentMultipleArrowTypeIndex]);

                if (arrowsManagedByInventory) {
                    if (checkArrowsOnInventory) {
                        mainArrowManager.updateArrowAmountText ();

                        mainArrowManager.checkIfArrowsFoundOnInventory ();
                    }
                }
            }
        }
    }

    public void inputSetTimeBulletOnBowSystem (bool state)
    {
        if (bowActive) {
            if (!canUseWeaponsInput ()) {
                return;
            }

            setTimeBulletOnBowSystemExternally (state);
        }
    }

    public void inputToggleTimeBulletOnBowSystem ()
    {
        inputSetTimeBulletOnBowSystem (!bulletTimeOnAirActive);
    }

    public void changeArrowTypeByIndex (int newIndex)
    {
        if (currentBowHolderSystem != null) {
            if (currentBowHolderSystem.canUseMultipleArrowType) {

                currentMultipleArrowTypeIndex = newIndex;

                if (currentMultipleArrowTypeIndex >= currentBowHolderSystem.bowWeaponIDList.Count) {
                    currentMultipleArrowTypeIndex = 0;
                }

                setCurrentBowWeaponSystemByID (currentBowHolderSystem.bowWeaponIDList [currentMultipleArrowTypeIndex]);

                if (arrowsManagedByInventory) {
                    if (checkArrowsOnInventory) {
                        mainArrowManager.updateArrowAmountText ();

                        mainArrowManager.checkIfArrowsFoundOnInventory ();
                    }
                }
            }
        }
    }

    public void setUseUpperBodyRotationSystemToAimState (bool state)
    {
        useUpperBodyRotationSystemToAim = state;
    }


    //EDITOR FUNCTIONS
    public void setUseDrawHolsterActionAnimationsStateFromEditor (bool state)
    {
        setUseDrawHolsterActionAnimationsState (state);

        updateComponent ();
    }

    public void setUseUpperBodyRotationSystemToAimStateFromEditor (bool state)
    {
        setUseUpperBodyRotationSystemToAimState (state);

        updateComponent ();
    }

    public void updateComponent ()
    {
        GKC_Utils.updateComponent (this);

        GKC_Utils.updateDirtyScene ("Update Info " + gameObject.name, gameObject);
    }


    [System.Serializable]
    public class bowWeaponSystemInfo
    {
        [Header ("Main Settings")]
        [Space]

        public string Name;

        public int bowWeaponID;

        public bool isCurrentBowWeapon;

        [Space]
        [Header ("Inventory Settings")]
        [Space]

        public bool checkArrowsOnInventory;

        public string arrowInventoryObjectName;

        [Space]
        [Header ("Surface Settings")]
        [Space]

        public bool checkSurfaceTypeDetected;

        public List<arrowSurfaceTypeInfo> arrowSurfaceTypeInfoList = new List<arrowSurfaceTypeInfo> ();

        [Space]
        [Header ("Other Settings")]
        [Space]

        public simpleWeaponSystem mainSimpleWeaponSystem;

        public bool showArrowTypeIcon = true;

        public Texture bowIcon;

        [Space]
        [Header ("Arrow Force And Damage Settings")]
        [Space]

        public bool usePullBowMultipliers;

        public float pullBowForceRateMultiplier = 0.1f;

        public float initialPullBowForceRateMultiplier = 30;

        public float pullBowDamageRateMultiplier = 0.1f;

        public float maxPullBowDamageRateMultiplier = 10;

        public float pullBowMultiplierDelay;

        public bool ignorePullMultiplierIfShootingArrowWithoutAim;

        [Space]
        [Header ("Events Settings")]
        [Space]

        public bool useEventOnBowSelected;

        [Space]

        public UnityEvent eventOnBowSelected;

        public UnityEvent eventOnBowUnselected;

        [Space]

        public bool useEventOnMaxPullBowMultiplier;

        [Space]

        public UnityEvent eventOnMaxPullBowMultiplierStart;

        public UnityEvent eventOnMaxPullBowMultiplierEnd;

        [HideInInspector] public bool eventOnMaxPullBowMultiplierActivated;
    }

    [System.Serializable]
    public class arrowSurfaceTypeInfo
    {
        public string Name;

        public bool isObstacle;

        public bool arrowBounceOnSurface;

        public bool dropArrowPickupOnBounce;

        public bool addExtraForceOnBounce;

        public float extraForceOnBounce;
    }
}
