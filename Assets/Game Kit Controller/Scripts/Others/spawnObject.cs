using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.Events;

public class spawnObject : MonoBehaviour
{
    [Header ("Main Settings")]
    [Space]

    public bool spawnObjectEnabled = true;
    public GameObject objectToSpawn;
    public Transform spawnPosition;
    public float radiusToSpawn;

    public bool setObjectRotation = true;

    public bool setObjectScale;
    public Vector3 newObjectScale;

    public bool spawnAmountOfSameObject;
    public int amountOfSameObjectToSpawn;

    public bool spawnObjectsOnStart;

    [Space]
    [Header ("Pooling Settings")]
    [Space]

    public bool spawnObjectFromPoolingSystem;
    public bool checkGKC_PoolingElementOnSpawnedObject;

    [Space]
    [Header ("Spawn Local Position And Rotation Settings")]
    [Space]

    public bool useSpawnReferencePosition;
    public Transform spawnReferencePosition;
    public bool setSpawnedObjectParent;
    public Transform spawnedObjectParent;

    [Space]
    [Header ("Spawn Raycast Settings")]
    [Space]

    public bool useRaycastToSpawnObject;
    public LayerMask layerToSpawnObject;
    public bool useInfiniteRaycastDistance;
    public float raycastDistance;
    public Vector3 raycastSpawnObjectOffset;
    public bool launchRaycastToDownDirection;

    public bool adjustSpawnedObjectToSurfaceNormal;

    public bool ignoreRigidbodiesOnRaycast;

    [Space]
    [Header ("Spawn List Of Objects Settings")]
    [Space]

    public bool spawnObjectList;
    public List<GameObject> objectListToSpawn = new List<GameObject> ();

    [Space]

    public bool useCustomSpawnObjectList;

    public List<spawnableObjectInfo> spawnableObjectInfoList = new List<spawnableObjectInfo> ();

    [Space]
    [Header ("Store Objects Settings")]
    [Space]

    public bool storeSpawnedObjects;
    public List<GameObject> spawnedObjectList = new List<GameObject> ();

    [Space]
    [Header ("Spawn Limit Settings")]
    [Space]

    public bool useSpawnLimitAmount;
    public int spawnLimitAmount;

    public bool checkIfObjectsSpawnedAreDead;

    [Space]

    public UnityEvent eventOnLimitReached;
    public UnityEvent eventOnLimitNotReached;

    [Space]
    [Header ("Spawn Possibility Settings")]
    [Space]

    public bool useRandomPossibilityToSpawn;

    [Range (0, 100)] public float possibilityToSpawn;

    [Space]
    [Header ("Events Settings")]
    [Space]

    public bool sendSpawnedObjectOnEvent;
    public eventParameters.eventToCallWithGameObject eventToSendSPawnedObject;

    [Space]
    [Header ("Remote Events Settings")]
    [Space]

    public bool sendSpawnedObjectOnRemoteEvent;
    public string remoteEventToSendObject;

    public bool useRemoveEventList;
    public List<remoteEventInfo> removeEventNameList = new List<remoteEventInfo> ();

    [Space]
    [Header ("Gizmo Settings")]
    [Space]

    public bool showGizmo;

    [Space]
    [Header ("Debug")]
    [Space]

    public int currentNumberOfSpawnObjects;

    GameObject lastObjectSpawned;

    Vector3 raycastNormal;

    Vector3 customPositionToSpawn = Vector3.zero;

    float customPositionOffsetYToSpawn = 0;

    void Start ()
    {
        if (spawnObjectsOnStart) {
            activateSpawnObject ();
        }
    }

    public void setCustomPositionToSpawn (Vector3 newPosition)
    {
        customPositionToSpawn = newPosition;
    }

    public void setCustomPositionOffsetYToSpawn (float newOffset)
    {
        customPositionOffsetYToSpawn = newOffset;
    }

    public void activateSpawnObject ()
    {
        if (!spawnObjectEnabled) {
            return;
        }

        if (spawnObjectList) {
            if (useCustomSpawnObjectList) {
                for (int i = 0; i < spawnableObjectInfoList.Count; i++) {
                    spawnableObjectInfo currentSpawnableObjectInfo = spawnableObjectInfoList [i];

                    if (currentSpawnableObjectInfo.objectToSpawn != null) {
                        for (int j = 0; j < currentSpawnableObjectInfo.amountToSpawn; j++) {
                            createObject (currentSpawnableObjectInfo.objectToSpawn);
                        }
                    } else {
                        print ("WARNING: There is no any object to spawn configured in the spawn objects component, make " +
                        "sure to apply the proper settings to spawn the object.");
                    }
                }
            } else {
                for (int i = 0; i < objectListToSpawn.Count; i++) {
                    if (objectListToSpawn [i] != null) {
                        createObject (objectListToSpawn [i]);
                    } else {
                        print ("WARNING: There is no any object to spawn configured in the spawn objects component, make " +
                        "sure to apply the proper settings to spawn the object.");
                    }
                }
            }
        } else {
            if (objectToSpawn != null) {
                if (spawnAmountOfSameObject) {
                    for (int i = 0; i < amountOfSameObjectToSpawn; i++) {
                        createObject (objectToSpawn);
                    }
                } else {
                    createObject (objectToSpawn);
                }
            } else {
                print ("WARNING: There is no any object to spawn configured in the spawn objects component, make " +
                "sure to apply the proper settings to spawn the object.");
            }
        }
    }

    public void createObject (GameObject newObject)
    {
        if (useRandomPossibilityToSpawn) {
            float randomProbability = Random.Range (0, 101);

            if (randomProbability < possibilityToSpawn) {
                return;
            }
        }

        if (useSpawnLimitAmount) {
            checkIfSpawnObjectsListNull ();

            if (currentNumberOfSpawnObjects >= spawnLimitAmount) {
                return;
            }
        }

        if (spawnPosition == null) {
            spawnPosition = transform;
        }

        Vector3 positionToSpawn = getPositionToSpawnObject ();

        Quaternion objectRotation = getRotationToSpawnObject ();

        GameObject objectToSpawnClone = null;

        if (spawnObjectFromPoolingSystem) {
            objectToSpawnClone = GKC_PoolingSystem.Spawn (newObject, positionToSpawn, objectRotation);

            if (checkGKC_PoolingElementOnSpawnedObject) {
                GKC_PoolingElement currentGKC_PoolingElement = objectToSpawnClone.GetComponent<GKC_PoolingElement> ();

                if (currentGKC_PoolingElement != null) {
                    currentGKC_PoolingElement.spawnPoolObject ();
                }
            }
        } else {
            objectToSpawnClone = (GameObject)Instantiate (newObject, positionToSpawn, objectRotation);
        }

        objectToSpawnClone.name = newObject.name;

        if (setSpawnedObjectParent) {
            objectToSpawnClone.transform.SetParent (spawnedObjectParent);
        }

        if (useSpawnReferencePosition) {
            objectToSpawnClone.transform.localPosition = spawnReferencePosition.localPosition;
            objectToSpawnClone.transform.localRotation = spawnReferencePosition.localRotation;
        }

        if (setObjectScale) {
            objectToSpawnClone.transform.localScale = newObjectScale;
        }

        if (!objectToSpawnClone.activeSelf) {
            objectToSpawnClone.SetActive (true);
        }

        if (adjustSpawnedObjectToSurfaceNormal) {

            if (raycastNormal != Vector3.zero) {

                Vector3 myForward = Vector3.Cross (objectToSpawnClone.transform.right, raycastNormal);
                Quaternion targetRotation = Quaternion.LookRotation (myForward, raycastNormal);

                objectToSpawnClone.transform.rotation = targetRotation;
            }
        }

        if (storeSpawnedObjects) {
            spawnedObjectList.Add (objectToSpawnClone);

            lastObjectSpawned = objectToSpawnClone;
        }

        if (useSpawnLimitAmount) {
            checkIfSpawnObjectsListNull ();

            checkEventsOnSpawnLimits ();
        }

        if (sendSpawnedObjectOnEvent) {
            eventToSendSPawnedObject.Invoke (objectToSpawnClone);
        }

        checkRemoteEventsOnObject (objectToSpawnClone);

        raycastNormal = Vector3.zero;
    }

    void checkRemoteEventsOnObject (GameObject objectToCheck)
    {
        if (sendSpawnedObjectOnRemoteEvent) {
            remoteEventSystem currentRemoteEventSystem = objectToCheck.GetComponent<remoteEventSystem> ();

            if (currentRemoteEventSystem != null) {
                if (useRemoveEventList) {
                    for (int i = 0; i < removeEventNameList.Count; i++) {
                        if (removeEventNameList [i].remoteEventEnabled) {
                            currentRemoteEventSystem.callRemoteEvent (removeEventNameList [i].remoteEventName);
                        }
                    }
                } else {
                    currentRemoteEventSystem.callRemoteEventWithGameObject (remoteEventToSendObject, gameObject);
                }
            }
        }
    }

    Vector3 getPositionToSpawnObject ()
    {
        Vector3 positionToSpawn = spawnPosition.position;

        if (customPositionToSpawn != Vector3.zero) {
            positionToSpawn = customPositionToSpawn;

            if (customPositionOffsetYToSpawn != 0) {
                positionToSpawn += Vector3.up * customPositionOffsetYToSpawn;
            }
        }

        if (radiusToSpawn > 0) {
            Vector2 circlePosition = Random.insideUnitCircle * radiusToSpawn;
            Vector3 newSpawnPosition = new Vector3 (circlePosition.x, 0, circlePosition.y);
            positionToSpawn += newSpawnPosition;
        }

        raycastNormal = Vector3.zero;

        if (useRaycastToSpawnObject) {
            RaycastHit hit = new RaycastHit ();

            float newRaycastDistance = raycastDistance;

            if (useInfiniteRaycastDistance) {
                newRaycastDistance = Mathf.Infinity;
            }

            Vector3 raycastDirection = -spawnPosition.up;

            if (launchRaycastToDownDirection) {
                raycastDirection = -Vector3.up;
            }

            bool surfaceLocated = false;

            Vector3 raycastPosition = positionToSpawn;

            if (Physics.Raycast (raycastPosition, raycastDirection, out hit, newRaycastDistance, layerToSpawnObject)) {
                if (ignoreRigidbodiesOnRaycast) {
                    if (hit.rigidbody != null) {

                        raycastPosition = hit.point;

                        if (Physics.Raycast (raycastPosition, raycastDirection, out hit, newRaycastDistance, layerToSpawnObject)) {
                            surfaceLocated = true;
                        }
                    } else {
                        surfaceLocated = true;
                    }
                } else {
                    surfaceLocated = true;
                }
            }

            if (surfaceLocated) {
                positionToSpawn = hit.point + hit.normal * 0.1f + raycastSpawnObjectOffset;

                raycastNormal = hit.normal;
            }
        }


        return positionToSpawn;
    }

    Quaternion getRotationToSpawnObject ()
    {
        Quaternion objectRotation = Quaternion.identity;

        if (setObjectRotation) {
            objectRotation = spawnPosition.rotation;
        }

        return objectRotation;
    }

    public void checkIfSpawnObjectsListNull ()
    {
        for (int i = spawnedObjectList.Count - 1; i >= 0; i--) {
            if (spawnedObjectList [i] == null) {
                spawnedObjectList.RemoveAt (i);
            } else {
                if (checkIfObjectsSpawnedAreDead) {
                    if (applyDamage.checkIfDeadOnObjectChilds (spawnedObjectList [i])) {
                        spawnedObjectList.RemoveAt (i);
                    }
                }
            }
        }

        currentNumberOfSpawnObjects = spawnedObjectList.Count;
    }

    public void removeSpawnedObjectFromList (GameObject objectToCheck)
    {
        for (int i = 0; i < spawnedObjectList.Count; i++) {
            if (spawnedObjectList [i] == objectToCheck) {
                spawnedObjectList.RemoveAt (i);
            }
        }

        currentNumberOfSpawnObjects = spawnedObjectList.Count;
    }

    public GameObject getLastObjectSpawned ()
    {
        return lastObjectSpawned;
    }

    public void checkEventsOnSpawnLimits ()
    {
        if (currentNumberOfSpawnObjects >= spawnLimitAmount) {
            eventOnLimitReached.Invoke ();
        } else {
            eventOnLimitNotReached.Invoke ();
        }
    }

    public void destroySpawnedObjects ()
    {
        if (storeSpawnedObjects) {
            for (int i = 0; i < spawnedObjectList.Count; i++) {
                if (spawnedObjectList [i] != null) {
                    Destroy (spawnedObjectList [i]);
                }
            }

            spawnedObjectList.Clear ();
        }
    }

    public void setAmountOfSameObjectToSpawn (int newValue)
    {
        amountOfSameObjectToSpawn = newValue;
    }

    public void setSpawnObjectEnabledState (bool state)
    {
        spawnObjectEnabled = state;
    }

    public void enableRemovetEventState (string remoteEventName)
    {
        enableRemoteEventState (remoteEventName);
    }

    public void disableRemovetEventState (string remoteEventName)
    {
        disableRemoteEventState (remoteEventName);
    }

    public void enableOrDisableAllRemovetEventStates (bool state)
    {
        enableOrDisableAllRemoteEventStates (state);
    }

    public void enableRemoteEventState (string remoteEventName)
    {
        enableOrDisableRemoteEventState (remoteEventName, true);
    }

    public void disableRemoteEventState (string remoteEventName)
    {
        enableOrDisableRemoteEventState (remoteEventName, false);
    }

    public void enableOrDisableAllRemoteEventStates (bool state)
    {
        for (int i = 0; i < removeEventNameList.Count; i++) {
            removeEventNameList [i].remoteEventEnabled = state;
        }
    }

    void enableOrDisableRemoteEventState (string remoteEventName, bool state)
    {
        int currentIndex = removeEventNameList.FindIndex (s => s.remoteEventName == remoteEventName);

        if (currentIndex > -1) {
            removeEventNameList [currentIndex].remoteEventEnabled = state;
        }
    }

    public void setObjectToSpawn (GameObject newObject)
    {
        objectToSpawn = newObject;
    }

    public void setSpawnPosition (Transform newTransform)
    {
        spawnPosition = newTransform;
    }

    //EDITOR FUNCTIONS
#if UNITY_EDITOR
    void OnDrawGizmos ()
    {
        if (!showGizmo) {
            return;
        }

        if (GKC_Utils.isCurrentSelectionActiveGameObject (gameObject)) {
            DrawGizmos ();
        }
    }

    void OnDrawGizmosSelected ()
    {
        DrawGizmos ();
    }

    void DrawGizmos ()
    {
        if (showGizmo) {
            Gizmos.color = Color.yellow;

            if (spawnPosition != null) {
                Gizmos.DrawWireSphere (spawnPosition.position, radiusToSpawn);
            } else {
                Gizmos.DrawWireSphere (transform.position, radiusToSpawn);
            }
        }
    }
#endif

    [System.Serializable]
    public class remoteEventInfo
    {
        public string remoteEventName;
        public bool remoteEventEnabled;
    }

    [System.Serializable]
    public class spawnableObjectInfo
    {
        public GameObject objectToSpawn;
        public int amountToSpawn = 1;
    }
}