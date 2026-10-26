using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class regularVehicleOnWater : FloatingObject
{
    [Header ("Custom Settings")]
    [Space]

    public bool checkWaterStateEnabled = true;

    public float minDistanceToSinkVehicleOnWater = 1;

    public float minDistanceToCheckIfVehicleAboveSurface = 1.2f;

    public bool turnOffVehicleOnWaterEnabled;

    [Space]

    public bool applyDamageIfVehicleBelowWaterEnabled;

    public float applyDamageIfVehicleBelowWaterRate;

    public float applyDamageIfVehicleBelowWaterAmount;

    [Space]

    public bool sinkVehicleAfterDelayEnabled = true;

    public float waitTimeBeforeSinking = 3;

    public float sinkingRate = 0.1f;

    public float sinkingAmount = 0.5f;

    public bool enableUnderwaterStateOnPassengersOnVehicleBelowWaterState;

    [Space]
    [Header ("Debug")]
    [Space]

    public bool showDebugPrint;
    public bool isVehicleBelowWaterLevel;

    public float currentDistanceToSurface;

    public bool vehicleIsSinking;

    [Space]
    [Header ("Event Settings")]
    [Space]

    public bool useEventsOnVehicleBelowOrAboveSurface;

    public UnityEvent eventsOnVehicleAboveSurface;

    public UnityEvent eventsOnVehicleBelowSurface;

    [Space]
    [Header ("Components")]
    [Space]

    public GameObject vehicleGameObject;

    public vehicleController mainVehicleController;

    public vehicleHUDManager mainVehicleHUDManager;

    public vehicleGravityControl mainVehicleGravityControl;

    public Transform vehicleMaxWaterLevelTransform;


    float lastTimeBelowWater;

    float lastTimeDamageToVehicleBelowWater;

    float lastTimeDensityChange;

    BoxCollider waterCollider;

    public override void checkObjectStateOnWater ()
    {
        float surfacePosition = mainWaterSurfaceSystem.GetWaterLevel (vehicleGameObject.transform.position);

        currentDistanceToSurface = Mathf.Abs (vehicleMaxWaterLevelTransform.position.y - surfacePosition);

        float currentDistanceToSurfaceABS = Mathf.Abs (currentDistanceToSurface);

        if (isVehicleBelowWaterLevel) {
            if (applyDamageIfVehicleBelowWaterEnabled) {
                if (!mainVehicleHUDManager.vehicleIsDestroyed ()) {
                    if (Time.time > lastTimeDamageToVehicleBelowWater + applyDamageIfVehicleBelowWaterRate) {
                        applyDamage.checkHealth (vehicleGameObject, vehicleGameObject, applyDamageIfVehicleBelowWaterAmount,
                            vehicleGameObject.transform.forward, vehicleGameObject.transform.position, vehicleGameObject,
                            true, false, true, false, false, false,
                            -1, -1);

                        lastTimeDamageToVehicleBelowWater = Time.time;
                    }
                }
            }

            if (sinkVehicleAfterDelayEnabled) {
                if (Time.time > lastTimeBelowWater + waitTimeBeforeSinking) {
                    if (Time.time > lastTimeDensityChange + sinkingRate) {
                        addOrRemoveDensity (sinkingAmount);

                        lastTimeDensityChange = Time.time;
                    }

                    if (!vehicleIsSinking) {
                        setVehiclePassengersState (true);

                        vehicleIsSinking = true;
                    }
                }
            }

            bool vehicleCanResumeRegularStateResult = false;

            if (vehicleMaxWaterLevelTransform.position.y > surfacePosition) {
                if ((vehicleMaxWaterLevelTransform.position.y - surfacePosition) > minDistanceToCheckIfVehicleAboveSurface) {
                    if (mainVehicleController.isVehicleOnGround ()) {
                        vehicleCanResumeRegularStateResult = true;
                    }
                }
            }

            if (vehicleCanResumeRegularStateResult) {
                if (isVehicleBelowWaterLevel) {
                    setVehicleStateBelowOrAboveWater (false);

                    if (showDebugPrint) {
                        print ("vehicle above water, resume regular state");
                    }
                }

                isVehicleBelowWaterLevel = false;
            }
        } else {
            bool vehicleCanStartSinkStateResult = false;

            if (!mainVehicleController.isVehicleOnGround ()) {
                vehicleCanStartSinkStateResult = true;
            }

            if (vehicleMaxWaterLevelTransform.position.y < surfacePosition) {
                if (currentDistanceToSurface > minDistanceToSinkVehicleOnWater) {
                    vehicleCanStartSinkStateResult = true;
                }
            }

            if (currentDistanceToSurfaceABS < minDistanceToSinkVehicleOnWater) {
                vehicleCanStartSinkStateResult = true;
            }

            if (vehicleCanStartSinkStateResult) {
                if (!isVehicleBelowWaterLevel) {

                    lastTimeBelowWater = Time.time;

                    lastTimeDamageToVehicleBelowWater = 0;

                    lastTimeDensityChange = 0;

                    setVehicleStateBelowOrAboveWater (true);

                    if (showDebugPrint) {
                        print ("vehicle below the water, start to sink");
                    }
                }

                isVehicleBelowWaterLevel = true;

                if (enableUnderwaterStateOnPassengersOnVehicleBelowWaterState) {
                    if (mainWaterSurfaceSystem != null) {
                        waterCollider = mainWaterSurfaceSystem.gameObject.GetComponent<BoxCollider> ();
                    }

                    setVehiclePassengersState (true);
                }
            }
        }
    }
    public void disableVehiclePassengerUnderwaterStateWithoutCallingEvents ()
    {
        List<GameObject> passengerGameObjectList = mainVehicleHUDManager.getPassengerGameObjectList ();

        print (passengerGameObjectList.Count);

        for (int i = 0; i < passengerGameObjectList.Count; i++) {
            if (passengerGameObjectList[i] != null) {
                playerComponentsManager mainPlayerComponentsManager = passengerGameObjectList[i].GetComponent<playerComponentsManager> ();

                if (mainPlayerComponentsManager != null) {
                    vehiclePassengerUnderwater currentvehiclePassengerUnderwater =
                        mainPlayerComponentsManager.getVehiclePassengerUnderwater ();

                    if (currentvehiclePassengerUnderwater != null) {
                        currentvehiclePassengerUnderwater.disableVehiclePassengerUnderwaterStateWithoutCallingEvents ();
                    }
                }
            }
        }
    }
    public void checkVehiclePassengersState ()
    {
        if (vehicleIsSinking) {
            setVehiclePassengersState (true);
        }
    }

    void setVehiclePassengersState (bool state)
    {
        List<GameObject> passengerGameObjectList = mainVehicleHUDManager.getPassengerGameObjectList ();

        for (int i = 0; i < passengerGameObjectList.Count; i++) {
            if (passengerGameObjectList[i] != null) {
                playerComponentsManager mainPlayerComponentsManager = passengerGameObjectList[i].GetComponent<playerComponentsManager> ();

                if (mainPlayerComponentsManager != null) {
                    vehiclePassengerUnderwater currentvehiclePassengerUnderwater =
                        mainPlayerComponentsManager.getVehiclePassengerUnderwater ();

                    if (currentvehiclePassengerUnderwater != null) {
                        if (state) {
                            currentvehiclePassengerUnderwater.setCurrentSwimTrigger (waterCollider);
                        }

                        currentvehiclePassengerUnderwater.setVehiclePassengerUnderwaterState (state);
                    }
                }
            }
        }
    }

    void setVehicleStateBelowOrAboveWater (bool state)
    {
        if (state) {
            setApplyBuoyancyActiveState (true);

            mainVehicleGravityControl.pauseDownForce (true);

            mainRigidbody.useGravity = true;

            mainVehicleHUDManager.setExplodeVehicleWhenDestroyedEnabledState (false);

            mainVehicleHUDManager.setFadeVehiclePiecesOnDestroyedState (false);

            if (turnOffVehicleOnWaterEnabled) {
                mainVehicleController.setEngineOnOrOffState ();

                mainVehicleController.setIgnoreToTurnOnOrOffInputActiveState (true);
            }
        } else {
            mainRigidbody.useGravity = false;

            setApplyBuoyancyActiveState (false);

            mainVehicleGravityControl.pauseDownForce (false);

            setOriginalDensity ();

            setVehiclePassengersState (false);

            vehicleIsSinking = false;

            isVehicleBelowWaterLevel = false;

            if (turnOffVehicleOnWaterEnabled) {
                mainVehicleController.setIgnoreToTurnOnOrOffInputActiveState (false);
            }
        }

        checkEventStateOnBeloworAboveSurface (state);
    }

    public override void checkObjectStateOnWaterEnterOrExit (bool state)
    {
        if (state) {
            waterCollider = mainWaterSurfaceSystem.gameObject.GetComponent<BoxCollider> ();
        } else {
            setVehicleStateBelowOrAboveWater (false);
        }

        if (showDebugPrint) {
            if (state) {
                print ("vehicle entering water");
            } else {
                print ("vehicle exiting water");
            }
        }
    }

    public void checkEventStateOnBeloworAboveSurface (bool state)
    {
        if (useEventsOnVehicleBelowOrAboveSurface) {
            if (state) {
                eventsOnVehicleAboveSurface.Invoke ();
            } else {
                eventsOnVehicleBelowSurface.Invoke ();
            }
        }
    }
}
