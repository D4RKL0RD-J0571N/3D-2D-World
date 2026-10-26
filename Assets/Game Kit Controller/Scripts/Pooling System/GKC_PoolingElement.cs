using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class GKC_PoolingElement : MonoBehaviour
{
    [Header ("Main Settings")]
    [Space]

    public bool usePoolingEnabled = true;

    public GameObject mainPoolGameObject;

    [Space]
    [Header ("Other Settings")]
    [Space]

    public Vector3 currentPositionToSpawn;
    public Quaternion currentRotationToSpawn;

    [Space]
    [Header ("Events Settings")]
    [Space]

    public bool useEventOnSpawn;
    public UnityEvent eventOnSpawn;

    [Space]

    public bool useEventOnDespawn;
    public UnityEvent eventOnDespawn;

    [Space]
    [Space]

    public UnityEvent eventToEnablePoolManagement;
    public UnityEvent eventToDisablePoolManagement;


    public void setPositionToSpawn (Vector3 newValue)
    {
        currentPositionToSpawn = newValue;
    }

    public void setRotationToSpawn (Quaternion newValue)
    {
        currentRotationToSpawn = newValue;
    }

    public virtual void spawnPoolObject ()
    {
        if (usePoolingEnabled) {
            //			GKC_PoolingSystem.Spawn (mainPoolGameObject, currentPositionToSpawn, currentRotationToSpawn);

            checkEventOnSpawn ();

            checkObjectStateAfterSpawn ();
        }
    }

    public virtual void despawnPoolObject ()
    {
        if (usePoolingEnabled) {
            checkObjectStateBeforeDespawn ();

            checkEventOnDespawn ();

            GKC_PoolingSystem.Despawn (mainPoolGameObject);
        }
    }

    public virtual void checkObjectStateBeforeDespawn ()
    {

    }

    public virtual void checkObjectStateAfterSpawn ()
    {

    }

    public void checkEventOnSpawn ()
    {
        if (useEventOnSpawn) {
            eventOnSpawn.Invoke ();
        }
    }

    public void checkEventOnDespawn ()
    {
        if (useEventOnDespawn) {
            eventOnDespawn.Invoke ();
        }
    }

    public void setUsePoolingEnabledState (bool state)
    {
        usePoolingEnabled = state;
    }

    public void checkEventsOnEnableOrDisablePoolingManagementOnObject (bool state)
    {
        setUsePoolingEnabledState (state);

        if (state) {
            eventToEnablePoolManagement.Invoke ();
        } else {
            eventToDisablePoolManagement.Invoke ();
        }

        if (!Application.isPlaying) {
            updateComponent ();
        }
    }

    //EDITOR FUNCTIONS
    public void setUsePoolingEnabledStateFromEditor (bool state)
    {
        setUsePoolingEnabledState (state);

        updateComponent ();
    }

    public void checkEventsOnEnableOrDisablePoolingManagementOnObjectFromEditor (bool state)
    {
        checkEventsOnEnableOrDisablePoolingManagementOnObject (state);
    }

    void updateComponent ()
    {
        GKC_Utils.updateComponent (this);

        GKC_Utils.updateDirtyScene ("Update GKC Poolong Element " + gameObject.name, gameObject);
    }
}
