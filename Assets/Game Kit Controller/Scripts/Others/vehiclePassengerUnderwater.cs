using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class vehiclePassengerUnderwater : MonoBehaviour {
    [Header ("Main Settings")]
    [Space]

    public bool passengerUnderWaterStateEnabled = true;

    [Space]
    [Header ("Debug")]
    [Space]

    public bool vehiclePassengerUnderwaterActive;

    public bool cameraBelowWater;

    [Space]
    [Header ("Event Settings")]
    [Space]

    public bool useEventOnCameraUnderwaterEffect;
    public UnityEvent eventOnCameraUnderWaterStart;
    public UnityEvent eventOnCameraUnderWaterEnd;

    public bool useEventsOnUnderwaterStateChange;
    public UnityEvent eventOnStartUnderWater;
    public UnityEvent eventOnEndUnderwater;

    [Space]
    [Header ("Components")]
    [Space]

    public Transform mainCameraTransform;

    public BoxCollider currentSwimTrigger;


    Coroutine updateCoroutine;

    bool currentSwimTriggerLocated;


    public void stopUpdateCoroutine ()
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
        if (currentSwimTriggerLocated) {
            bool cameraInsideBoundTrigger = currentSwimTrigger.bounds.Contains (mainCameraTransform.position);

            if (cameraInsideBoundTrigger) {
                if (!cameraBelowWater) {
                    eventOnCameraUnderWaterStart.Invoke ();

                    cameraBelowWater = true;
                }
            } else {
                if (cameraBelowWater) {
                    eventOnCameraUnderWaterEnd.Invoke ();

                    cameraBelowWater = false;
                }
            }
        }
    }

    public void disableVehiclePassengerUnderwaterStateWithoutCallingEvents ()
    {
        if (vehiclePassengerUnderwaterActive) {
            vehiclePassengerUnderwaterActive = false;

            cameraBelowWater = false;

            stopUpdateCoroutine ();
        }
    }

    public void setVehiclePassengerUnderwaterState (bool state)
    {
        if (!passengerUnderWaterStateEnabled) {
            return;
        }

        if (vehiclePassengerUnderwaterActive == state) {
            return;
        }

        vehiclePassengerUnderwaterActive = state;

        if (vehiclePassengerUnderwaterActive) {
            updateCoroutine = StartCoroutine (updateSystemCoroutine ());
        } else {
            stopUpdateCoroutine ();

            if (cameraBelowWater) {
                eventOnCameraUnderWaterEnd.Invoke ();
            }

            cameraBelowWater = false;
        }

        if (useEventsOnUnderwaterStateChange) {
            if (state) {
                eventOnStartUnderWater.Invoke ();
            } else {
                eventOnEndUnderwater.Invoke ();
            }
        }
    }

    public void setCurrentSwimTrigger (BoxCollider newCollider)
    {
        currentSwimTrigger = newCollider;

        currentSwimTriggerLocated = currentSwimTrigger != null;
    }
}
