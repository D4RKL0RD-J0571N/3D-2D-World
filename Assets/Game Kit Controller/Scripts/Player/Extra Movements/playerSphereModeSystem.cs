using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class playerSphereModeSystem : MonoBehaviour
{
    [Header ("Main Settings")]
    [Space]

    public bool sphereModeEnabled;

    public string defaultVehicleStateName = "Sphere";

    [Space]
    [Header ("Vehicle Info List Settings")]
    [Space]

    public List<vehicleInfo> vehicleInfoList = new List<vehicleInfo> ();

    [Space]
    [Header ("Debug")]
    [Space]

    public bool showDebugPrint;

    public bool usingSphereMode;

    public bool isExternalVehicleController;
    public mainRiderSystem currentRiderSystem;

    public Transform currentRiderSystemTransform;

    [Space]

    public Transform currentObjectToControlTransform;
    public GameObject currentObjectToControl;

    [Space]
    [Header ("Components")]
    [Space]

    public playerController playerManager;
    public gravitySystem gravityManager;
    public usingDevicesSystem usingDevicesManager;
    public Collider playerCollider;

    public overrideElementControlSystem mainOverrideElementControlSystem;


    Collider mainVehicleCollider;

    GameObject vehicleCamera;
    vehicleGravityControl vehicleGravityManager;

    bool initialized;

    Coroutine updateCoroutine;

    vehicleHUDManager currenVehicleHUDManager;

    bool currenVehicleHUDManagerLocated;

    vehicleInfo currentVehicleInfo;


    void stopUpdateCoroutine ()
    {
        if (updateCoroutine != null) {
            StopCoroutine (updateCoroutine);
        }
    }

    IEnumerator updateSystemCoroutine ()
    {
        var waitTime = new WaitForFixedUpdate ();

        while (true) {
            updateSystem ();

            yield return waitTime;
        }
    }

    void updateSystem ()
    {
        if (usingSphereMode && currenVehicleHUDManagerLocated) {
            if (currenVehicleHUDManager.vehicleIsDestroyed ()) {
                stopUpdateCoroutine ();


                //setSphereModeActiveState (false);

                if (currentVehicleInfo.useEventOnVehicleDestroyed) {
                    currentVehicleInfo.eventOnVehicleDestroyed.Invoke ();
                }
            }
        }
    }

    void checkCurrentVehicleInfo ()
    {
        if (currentVehicleInfo == null || !initialized) {
            initialized = true;

            setCurrentVehicleInfo (defaultVehicleStateName);
        }
    }

    public void setCurrentVehicleInfo (string newVehicleInfoName)
    {
        for (int i = 0; i < vehicleInfoList.Count; i++) {
            if (vehicleInfoList [i].Name.Equals (newVehicleInfoName)) {
                if (currentVehicleInfo != null) {
                    if (currentObjectToControl != null) {
                        if (currentVehicleInfo != vehicleInfoList [i]) {
                            currentObjectToControl = null;

                            currentRiderSystemTransform = null;
                        }
                    }
                }

                currentVehicleInfo = vehicleInfoList [i];

                if (!currentVehicleInfo.isCurrentVehicle) {
                    if (currentVehicleInfo.useEventOnVehicleInfoState) {
                        currentVehicleInfo.eventOnVehicleInfoState.Invoke ();
                    }
                }

                currentVehicleInfo.isCurrentVehicle = true;

                if (showDebugPrint) {
                    print ("setCurrentVehicleInfo " + newVehicleInfoName);
                }
            } else {
                if (vehicleInfoList [i].isCurrentVehicle) {
                    setSphereModeActiveState (false);
                }

                vehicleInfoList [i].isCurrentVehicle = false;
            }
        }
    }

    public void toggleSphereModeActiveState ()
    {
        setSphereModeActiveState (!usingSphereMode);
    }

    public void setSphereModeActiveState (bool state)
    {
        if (!sphereModeEnabled) {
            return;
        }

        if (showDebugPrint) {
            print ("calling setVehicleState coroutine " + state);
        }

        if (playerManager.canUseSphereMode) {
            StartCoroutine (setVehicleState (state));
        }
    }

    IEnumerator setVehicleState (bool state)
    {
        checkCurrentVehicleInfo ();

        currenVehicleHUDManagerLocated = false;

        usingSphereMode = state;

        if (showDebugPrint) {
            print ("setVehicleState " + state);
        }

        if (state) {
            if (currentVehicleInfo.controlWithOverrideSystem) {
                if (currentVehicleInfo.currentVehicleObject != null) {
                    if (!mainOverrideElementControlSystem.checkIfTemporalObjectOnList (currentVehicleInfo.currentVehicleObject)) {
                        currentVehicleInfo.currentVehicleObject = null;
                    }
                }
            }

            if (currentObjectToControl != null) {
                currenVehicleHUDManager = currentObjectToControl.GetComponentInChildren<vehicleHUDManager> ();

                currenVehicleHUDManagerLocated = currenVehicleHUDManager != null;

                if (currenVehicleHUDManagerLocated) {
                    if (currenVehicleHUDManager.vehicleIsDestroyed ()) {
                        currentVehicleInfo.currentVehicleObject = null;

                        currentObjectToControl = null;

                        currentRiderSystem = null;

                        if (showDebugPrint) {
                            print ("current vehicle is destroying, removing to instantiate a new one");
                        }

                        currenVehicleHUDManager = null;

                        currenVehicleHUDManagerLocated = false;
                    }
                }
            }

            if (currentVehicleInfo.currentVehicleObject == null) {
                Vector3 vehiclePosition = Vector3.one * 1000;

                if (currentVehicleInfo.adjustVehicleParentToPlayerPosition) {
                    vehiclePosition = playerManager.transform.position + playerManager.transform.up;
                }

                currentVehicleInfo.currentVehicleObject = (GameObject)Instantiate (currentVehicleInfo.vehiclePrefab, vehiclePosition, Quaternion.identity);

                if (!currentVehicleInfo.currentVehicleObject.activeSelf) {
                    currentVehicleInfo.currentVehicleObject.SetActive (true);
                }

                yield return new WaitForSeconds (0.00001f);

                getCurrentVehicleComponents ();

                if (vehicleGravityManager != null) {
                    vehicleGravityManager.pauseDownForce (true);
                }

                currentObjectToControlTransform.gameObject.SetActive (false);

                if (mainVehicleCollider != null) {
                    mainVehicleCollider.enabled = false;
                }

                yield return new WaitForSeconds (0.00001f);

                if (vehicleGravityManager != null) {
                    vehicleGravityManager.pauseDownForce (false);
                }

                yield return null;
            } else {
                getCurrentVehicleComponents ();

                yield return null;
            }

            //			if (currentVehicleInfo.currentVehicleObject != null) {
            //				getCurrentVehicleComponents ();
            //			}

            if (currentObjectToControl != null) {
                if (!currentObjectToControlTransform.gameObject.activeSelf) {
                    if (vehicleGravityManager != null) {
                        vehicleGravityManager.setCustomNormal (gravityManager.getCurrentNormal ());
                    }

                    Vector3 vehiclePosition = playerManager.transform.position + playerManager.transform.up;

                    currentObjectToControlTransform.position = vehiclePosition;

                    if (currentVehicleInfo.setVehicleRotationWheGetOn) {
                        currentObjectToControlTransform.rotation = playerManager.transform.rotation;
                    }

                    if (vehicleCamera != null) {
                        vehicleCamera.transform.position = vehiclePosition;
                    }

                    if (isExternalVehicleController) {
                        if (currentRiderSystemTransform != currentObjectToControlTransform) {
                            print (currentObjectToControlTransform.name + " " + currentRiderSystemTransform.name);
                            currentRiderSystemTransform.SetParent (currentObjectToControlTransform);

                            currentRiderSystemTransform.transform.localPosition = Vector3.zero;

                            currentRiderSystemTransform.transform.localRotation = Quaternion.identity;
                        }
                    }

                    if (!currentObjectToControlTransform.gameObject.activeSelf) {
                        currentObjectToControlTransform.gameObject.SetActive (true);
                    }

                    if (currentRiderSystem != null) {
                        currentRiderSystem.setTriggerToDetect (playerCollider);
                    }

                    yield return null;
                }

                stopUpdateCoroutine ();

                updateCoroutine = StartCoroutine (updateSystemCoroutine ());
            }
        } else {
            stopUpdateCoroutine ();

            if (currentObjectToControl != null) {
                if (currentObjectToControlTransform.gameObject.activeSelf) {
                    if (vehicleCamera != null) {
                        if (currentVehicleInfo.setVehicleRotationWheGetOn) {
                            currentObjectToControlTransform.rotation = vehicleCamera.transform.rotation;
                        }
                    }
                }
            }
        }

        if (currentObjectToControl != null) {
            if (currentVehicleInfo.controlWithOverrideSystem) {
                if (state) {
                    mainOverrideElementControlSystem.overrideElementControl (currentObjectToControl);

                    mainOverrideElementControlSystem.addNewTemporalObject (currentObjectToControl);
                } else {
                    mainOverrideElementControlSystem.inputStopOverrideControl ();

                    if (mainOverrideElementControlSystem.checkIfTemporalObjectOnList (currentObjectToControl)) {
                        currentObjectToControlTransform.gameObject.SetActive (false);
                    }
                }
            } else {
                if (state) {
                    usingDevicesManager.clearDeviceList ();

                    usingDevicesManager.addDeviceToList (currentObjectToControl);

                    usingDevicesManager.setCurrentVehicle (currentObjectToControl);

                    usingDevicesManager.useCurrentDevice (currentObjectToControl);

                    usingDevicesManager.setUseDeviceButtonEnabledState (false);

                    usingDevicesManager.setPauseVehicleGetOffInputState (true);
                } else {
                    if (currentVehicleInfo.currentVehicleObject != null) {
                        usingDevicesManager.useDevice ();

                        usingDevicesManager.checkTriggerInfo (mainVehicleCollider, false);

                        usingDevicesManager.removeCurrentVehicle (currentObjectToControl);
                    }

                    if (isExternalVehicleController) {
                        currentRiderSystemTransform.SetParent (null);
                    }

                    currentObjectToControlTransform.gameObject.SetActive (false);

                    usingDevicesManager.setUseDeviceButtonEnabledState (true);

                    usingDevicesManager.setPauseVehicleGetOffInputState (false);
                }
            }
        }

        if (state) {
            if (currentVehicleInfo != null) {
                playerManager.enableOrDisableSphereMode (true);

                playerManager.setCheckOnGroungPausedState (true);

                playerManager.setPlayerOnGroundState (false);

                playerManager.setPlayerOnGroundAnimatorStateOnOverrideOnGroundWithTime (false);

                playerManager.overrideOnGroundAnimatorValue (0);

                playerManager.setPlayerOnGroundAnimatorStateOnOverrideOnGround (false);

                playerManager.setOnGroundAnimatorIDValue (false);

                currentVehicleInfo.isCurrentVehicle = true;
            }
        } else {
            if (currentVehicleInfo != null) {
                if (currentVehicleInfo.isCurrentVehicle) {

                    currentVehicleInfo.isCurrentVehicle = false;

                    playerManager.enableOrDisableSphereMode (false);

                    playerManager.setCheckOnGroungPausedState (false);

                    playerManager.setPlayerOnGroundState (false);

                    playerManager.setPlayerOnGroundAnimatorStateOnOverrideOnGroundWithTime (true);

                    playerManager.disableOverrideOnGroundAnimatorValue ();

                    playerManager.setPauseResetAnimatorStateFOrGroundAnimatorState (true);

                    if (playerManager.getCurrentSurfaceBelowPlayer () != null) {
                        playerManager.setPlayerOnGroundState (true);

                        playerManager.setOnGroundAnimatorIDValue (true);
                    }

                    if (currentVehicleInfo.destroyVehicleOnStopUse) {
                        StartCoroutine (destroyVehicleObjectCororutine (currentVehicleInfo.currentVehicleObject));

                        currentVehicleInfo.currentVehicleObject = null;
                    }
                }
            }
        }

        if (currentObjectToControl != null) {
            currenVehicleHUDManager = currentObjectToControl.GetComponentInChildren<vehicleHUDManager> ();

            currenVehicleHUDManagerLocated = currenVehicleHUDManager != null;
        }
    }

    void getCurrentVehicleComponents ()
    {
        if (currentVehicleInfo.controlWithOverrideSystem) {
            currentObjectToControl = currentVehicleInfo.currentVehicleObject;

            currentObjectToControlTransform = currentObjectToControl.transform;
        } else {
            currentRiderSystem = currentVehicleInfo.currentVehicleObject.GetComponentInChildren<mainRiderSystem> ();

            if (currentRiderSystem == null) {
                GKCRiderSocketSystem currentGKCRiderSocketSystem = currentVehicleInfo.currentVehicleObject.GetComponentInChildren<GKCRiderSocketSystem> ();

                if (currentGKCRiderSocketSystem != null) {
                    currentRiderSystem = currentGKCRiderSocketSystem.getMainRiderSystem ();
                }
            }

            isExternalVehicleController = currentRiderSystem.isExternalVehicleController;

            if (currentRiderSystem != null) {
                vehicleGravityManager = currentRiderSystem.getVehicleGravityControl ();

                if (isExternalVehicleController) {
                    currentRiderSystemTransform = currentRiderSystem.transform;

                    electronicDevice currentElectronicDevice = currentRiderSystem.gameObject.GetComponentInChildren<electronicDevice> ();

                    if (currentElectronicDevice != null) {
                        currentElectronicDevice.setPlayerManually (playerManager.gameObject);
                    }
                } else {
                    currentRiderSystemTransform = null;
                }
            }

            if (currentRiderSystem != null) {
                currentRiderSystem.setPlayerVisibleInVehicleState (currentVehicleInfo.playerVisibleInVehicle);

                currentRiderSystem.setEjectPlayerWhenDestroyedState (currentVehicleInfo.ejectPlayerWhenDestroyed);

                currentRiderSystem.setResetCameraRotationWhenGetOnState (currentVehicleInfo.resetCameraRotationWhenGetOn);
            }

            if (currentRiderSystem != null) {
                currentObjectToControl = currentRiderSystem.getVehicleGameObject ();

                currentObjectToControlTransform = currentRiderSystem.getCustomVehicleTransform ();

                vehicleCamera = currentRiderSystem.getVehicleCameraGameObject ();
            }

            mainVehicleCollider = currentObjectToControl.GetComponent<Collider> ();

            tutorialActivatorSystem currentTutorialActivatorSystem = currentVehicleInfo.currentVehicleObject.GetComponent<tutorialActivatorSystem> ();

            if (currentTutorialActivatorSystem != null) {
                currentTutorialActivatorSystem.setTutorialEnabledState (false);
            }
        }
    }

    IEnumerator destroyVehicleObjectCororutine (GameObject currentVehicleObject)
    {
        yield return new WaitForSeconds (0.00001f);

        currenVehicleHUDManager = currentVehicleObject.GetComponent<vehicleHUDManager> ();

        if (currenVehicleHUDManager != null) {
            currenVehicleHUDManager.destroyVehicleAtOnce ();
        } else {
            Destroy (currentVehicleInfo.currentVehicleObject);
        }
    }

    [System.Serializable]
    public class vehicleInfo
    {
        [Header ("Main Settings")]
        [Space]

        public string Name;

        [Space]
        [Header ("Other Settings")]
        [Space]

        public bool playerVisibleInVehicle;

        public bool adjustVehicleParentToPlayerPosition;

        public bool ejectPlayerWhenDestroyed = true;

        public bool controlWithOverrideSystem;

        public bool setVehicleRotationWheGetOn = true;

        public bool resetCameraRotationWhenGetOn;

        public bool destroyVehicleOnStopUse;

        [Space]
        [Header ("Debug")]
        [Space]

        public bool isCurrentVehicle;

        public GameObject currentVehicleObject;

        [Space]
        [Header ("Components Settings")]
        [Space]

        public GameObject vehiclePrefab;

        [Space]
        [Header ("Events Settings")]
        [Space]

        public bool useEventOnVehicleInfoState;
        public UnityEvent eventOnVehicleInfoState;

        public bool useEventOnVehicleDestroyed;
        public UnityEvent eventOnVehicleDestroyed;
    }
}
