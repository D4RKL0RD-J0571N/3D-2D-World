using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class mainRiderSystem : MonoBehaviour
{
    [Header ("Main Rider Settings")]
    [Space]

    public bool isExternalVehicleController;

    public bool vehicleOnlyForOnePlayer = true;


    public bool isVehicleOnlyForOnePlayerActive ()
    {
        return vehicleOnlyForOnePlayer;
    }

    public virtual GameObject getVehicleCameraGameObject ()
    {
        return null;
    }

    public virtual vehicleGravityControl getVehicleGravityControl ()
    {
        return null;
    }

    public virtual GameObject getVehicleGameObject ()
    {
        return null;
    }

    public virtual void setTriggerToDetect (Collider newCollider)
    {

    }

    public virtual void setPlayerVisibleInVehicleState (bool state)
    {

    }

    public virtual void setResetCameraRotationWhenGetOnState (bool state)
    {

    }

    public virtual void setEjectPlayerWhenDestroyedState (bool state)
    {

    }

    public virtual Transform getCustomVehicleTransform ()
    {
        return null;
    }

    public virtual void setEnteringOnVehicleFromDistanceState (bool state)
    {

    }

    public virtual void setCurrentPassenger (GameObject passenger)
    {

    }

    public virtual GameObject getCurrentDriver ()
    {
        return null;
    }

    public virtual bool isBeingDrivenActive ()
    {
        return false;
    }

    public virtual float getVehicleRadius ()
    {
        return 0;
    }

    public virtual bool checkIfDetectSurfaceBelongToVehicle (Collider surfaceFound)
    {
        return false;
    }

    public virtual Collider getVehicleCollider ()
    {
        return null;
    }
}
