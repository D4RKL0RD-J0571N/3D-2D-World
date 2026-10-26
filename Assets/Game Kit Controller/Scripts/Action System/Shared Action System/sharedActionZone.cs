using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class sharedActionZone : MonoBehaviour
{
    [Header ("Main Settings")]
    [Space]

    public bool sharedActionZoneEnabled = true;

    public string sharedActionName;

    public bool disableSharedActionZoneAfterUse;

    [Space]

    public bool setPlayerAsFirstCharacter;

    public bool setAIAsFirstCharacterIfSignalOwner;

    [Space]

    public bool useSharedActionContent = true;

    public sharedActionContent mainSharedActionContent;

    public GameObject sharedActionContentPrefab;

    [Space]
    [Header ("Range Settings")]
    [Space]

    public bool useSharedZoneInsideRange;

    public bool useZonePositionRange;
    public Vector2 xRange;
    public Vector2 zRange;

    [Space]

    public bool useZoneRotationRange;
    public float rotationRange;

    public bool useCustomSharedZoneRotationCenter;
    public Transform customSharedZoneTransform;

    [Space]
    [Header ("Condition Settings")]
    [Space]

    public float maxDistanceToUseSharedActionZone;

    [Space]

    public bool useMaxDistanceXZToUseSharedActionZone;
    public float maxDistanceXToUseSharedActionZone;
    public float maxDistanceZToUseSharedActionZone;

    [Space]
    [Space]

    public bool useMinDistanceToActivateActionFirstCharacter;
    public float minDistanceToActivateActionFirstCharacter;

    [Space]

    public bool useMinAngleToActivateActionFirstCharacter;
    public float minAngleToActivateActionFirstCharacter;

    [Space]
    [Space]

    public bool useMinDistanceToActivateActionSecondCharacter;
    public float minDistanceToActivateActionSecondCharacter;

    [Space]

    public bool useMinAngleToActivateActionSecondCharacter;
    public float minAngleToActivateActionSecondCharacter;

    [Space]
    [Space]

    public bool useMinAngleToActivateActionOnCharactersDirection;
    public float minAngleToActivateActionOnCharactersDirection;

    [Space]
    [Header ("Debug")]
    [Space]

    public bool mainSharedActionContentFound;


    Vector3 initialPosition = -Vector3.one;
    Vector3 initialEuler = -Vector3.one;



    public void storeInitialPosition ()
    {
        if (!mainSharedActionContentFound) {
            spawnSharedActionObjectOnScene ();
        }

        if (initialPosition == -Vector3.one) {
            if (mainSharedActionContentFound) {
                initialPosition = mainSharedActionContent.transform.position;
            } else {
                initialPosition = transform.position;
            }
        }

        if (initialEuler == -Vector3.one) {
            if (mainSharedActionContentFound) {
                initialEuler = mainSharedActionContent.transform.eulerAngles;
            } else {
                initialEuler = transform.eulerAngles;
            }
        }
    }

    public Vector3 getInitialPosition ()
    {
        return initialPosition;
    }

    public Vector3 getInitialEuler ()
    {
        return initialEuler;
    }

    public void setSharedActionZoneEnabledState (bool state)
    {
        sharedActionZoneEnabled = state;
    }

    public bool isSharedActionZoneEnabled ()
    {
        return sharedActionZoneEnabled;
    }

    public void setDisableSharedActionZoneAfterUseState (bool state)
    {
        disableSharedActionZoneAfterUse = state;
    }

    public string getSharedActionName ()
    {
        return sharedActionName;
    }

    public bool isUseSharedActionContentActive ()
    {
        return useSharedActionContent;
    }

    public sharedActionContent getSharedActionContent ()
    {
        return mainSharedActionContent;
    }

    public bool getSetPlayerAsFirstCharacter ()
    {
        return setPlayerAsFirstCharacter;
    }

    public bool getSetAIAsFirstCharacterIfSignalOwner ()
    {
        return setAIAsFirstCharacterIfSignalOwner;
    }

    public float getMaxDistanceToUseSharedActionZone ()
    {
        return maxDistanceToUseSharedActionZone;
    }

    void spawnSharedActionObjectOnScene ()
    {
        mainSharedActionContentFound = mainSharedActionContent != null;

        if (!mainSharedActionContentFound) {
            if (sharedActionContentPrefab != null) {
                GameObject sharedActionContentGameObject = (GameObject)Instantiate (sharedActionContentPrefab,
                    transform.position, transform.rotation);

                sharedActionContentGameObject.transform.SetParent (transform);

                mainSharedActionContent = sharedActionContentGameObject.GetComponent<sharedActionContent> ();

                mainSharedActionContentFound = true;
            }
        }

        if (mainSharedActionContentFound && mainSharedActionContent != null) {
            GKC_Utils.setActiveGameObjectInEditor (mainSharedActionContent.gameObject);
        }
    }

    public void spawnSharedActionObjectOnSceneFromEditor ()
    {
        spawnSharedActionObjectOnScene ();

        updateComponent ();
    }

    public void getSharedActionManagerrOnScene ()
    {
        sharedActionZoneManager currentSharedActionZoneManager = GKC_Utils.findObjectByComponent<sharedActionZoneManager> ();

        if (currentSharedActionZoneManager == null) {
            GameObject newSharedActionZoneManagerGameObject = new GameObject ();

            currentSharedActionZoneManager = newSharedActionZoneManagerGameObject.AddComponent<sharedActionZoneManager> ();
        }

        if (currentSharedActionZoneManager != null) {
            GKC_Utils.setActiveGameObjectInEditor (currentSharedActionZoneManager.gameObject);
        }
    }

    void updateComponent ()
    {
        GKC_Utils.updateComponent (this);

        GKC_Utils.updateDirtyScene ("Update Shared Action Zone", gameObject);
    }
}
