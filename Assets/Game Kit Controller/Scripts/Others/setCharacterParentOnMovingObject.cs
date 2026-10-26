using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class setCharacterParentOnMovingObject : MonoBehaviour
{
    [Header ("Main Settings")]
    [Space]

    public string playerTag = "Player";
    public string vehicleTag = "vehicle";

    public Transform platformTransform;

    [Space]
    [Header ("Debug")]
    [Space]

    public List<waypointPlatform.passengersInfo> passengersInfoList = new List<waypointPlatform.passengersInfo> ();

    public List<waypointPlatform.vehiclesInfo> vehiclesInfoList = new List<waypointPlatform.vehiclesInfo> ();


    void OnTriggerEnter (Collider col)
    {
        checkTriggerState (col, true);
    }

    void OnTriggerExit (Collider col)
    {
        checkTriggerState (col, false);
    }

    void checkTriggerState (Collider col, bool isEnter)
    {
        if (isEnter) {
            //if the player enters inside the platform trigger, then
            if (platformTransform == null) {
                platformTransform = transform;
            }

            if (col.gameObject.CompareTag (playerTag)) {
                //store him
                addPassenger (col.gameObject.transform);

                //if he is not driving, then attach the player and the camera inside the platform
                setPlayerParent (platformTransform, col.gameObject.transform);

            } else if (col.gameObject.CompareTag (vehicleTag)) {
                //store him
                addVehicle (col.gameObject.transform);

                //if he is not driving, then attach the player and the camera inside the platform
                setVehicleParent (platformTransform, col.gameObject.transform);

            }
        } else {

            //if the player exits, then disattach the player
            if (col.gameObject.CompareTag (playerTag)) {
                setPlayerParent (null, col.gameObject.transform);

                removePassenger (col.gameObject.transform);
            } else if (col.gameObject.CompareTag (vehicleTag)) {
                setVehicleParent (null, col.gameObject.transform);

                removeVehicle (col.gameObject.transform);

            }
        }
    }

    //attach and disattch the player and the camera inside the elevator
    void setPlayerParent (Transform father, Transform newPassenger)
    {
        bool passengerFound = false;

        waypointPlatform.passengersInfo newPassengersInfo = new waypointPlatform.passengersInfo ();

        for (int i = 0; i < passengersInfoList.Count; i++) {
            if (passengersInfoList [i].playerTransform == newPassenger && !passengerFound) {
                newPassengersInfo = passengersInfoList [i];
                passengerFound = true;
            }
        }

        if (passengerFound) {
            newPassengersInfo.playerControllerManager.setPlayerAndCameraAndFBAPivotTransformParent (father);

            newPassengersInfo.playerControllerManager.setMovingOnPlatformActiveState (father != null);
        }
    }

    void setAllPlayersParent (Transform father)
    {
        for (int i = 0; i < passengersInfoList.Count; i++) {
            passengersInfoList [i].playerControllerManager.setPlayerAndCameraAndFBAPivotTransformParent (father);

            passengersInfoList [i].playerControllerManager.setMovingOnPlatformActiveState (father != null);
        }
    }

    public void addPassenger (Transform newPassenger)
    {
        bool passengerFound = false;

        for (int i = 0; i < passengersInfoList.Count; i++) {
            if (passengersInfoList [i].playerTransform == newPassenger && !passengerFound) {
                passengerFound = true;
            }
        }

        if (!passengerFound) {
            waypointPlatform.passengersInfo newPassengersInfo = new waypointPlatform.passengersInfo ();

            newPassengersInfo.playerTransform = newPassenger;

            newPassengersInfo.playerControllerManager = newPassenger.GetComponent<playerController> ();

            passengersInfoList.Add (newPassengersInfo);
        }
    }

    void removePassenger (Transform newPassenger)
    {
        for (int i = 0; i < passengersInfoList.Count; i++) {
            if (passengersInfoList [i].playerTransform == newPassenger) {
                passengersInfoList.RemoveAt (i);
            }
        }
    }

    void setVehicleParent (Transform father, Transform newVehicle)
    {
        bool vehicleFound = false;

        waypointPlatform.vehiclesInfo newVehiclesInfo = new waypointPlatform.vehiclesInfo ();

        for (int i = 0; i < vehiclesInfoList.Count; i++) {
            if (vehiclesInfoList [i].vehicleTransform == newVehicle && !vehicleFound) {
                newVehiclesInfo = vehiclesInfoList [i];
                vehicleFound = true;
            }
        }

        if (vehicleFound) {
            newVehiclesInfo.HUDManager.setVehicleAndCameraParent (father);
        }
    }

    void setAllVehiclesParent (Transform father)
    {
        for (int i = 0; i < vehiclesInfoList.Count; i++) {
            vehiclesInfoList [i].HUDManager.setVehicleAndCameraParent (father);
        }
    }

    public void addVehicle (Transform newVehicle)
    {
        bool vehicleFound = false;

        for (int i = 0; i < vehiclesInfoList.Count; i++) {
            if (vehiclesInfoList [i].vehicleTransform == newVehicle && !vehicleFound) {
                vehicleFound = true;
            }
        }

        if (!vehicleFound) {
            waypointPlatform.vehiclesInfo newVehiclesInfo = new waypointPlatform.vehiclesInfo ();
            newVehiclesInfo.vehicleTransform = newVehicle;

            newVehiclesInfo.HUDManager = newVehicle.GetComponent<vehicleHUDManager> ();

            vehiclesInfoList.Add (newVehiclesInfo);
        }
    }

    void removeVehicle (Transform newVehicle)
    {
        for (int i = 0; i < vehiclesInfoList.Count; i++) {
            if (vehiclesInfoList [i].vehicleTransform == newVehicle) {
                vehiclesInfoList.RemoveAt (i);
            }
        }
    }
}