using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Events;

public class objectToPlaceSystem : MonoBehaviour
{
    [Header ("Main Settings")]
    [Space]

    public bool objectToPlacedEnabled = true;

    public bool objectCanCallPlacedEvent = true;

    public bool objectCanCallRemovedEvent = true;

    public string objectName;

    [Space]
    [Header ("Secondary Object To Place Settings")]
    [Space]

    public bool useSecondaryObjectToPlaceConnected;

    public objectToPlaceSystem secondaryObjectToPlaceSystem;

    [Space]
    [Header ("Connect To Others Objects To Place Settings")]
    [Space]

    public bool searchOtherObjectsToPlaceOnDropEnabled;

    public string otherObjectsToPlaceOnDropName;

    public Transform customPlaceToPutOtherObjectToPlaceTransform;

    [Space]

    public bool useOtherObjectsToPlaceOnDropNameList;

    public List<otherObjectsToPlaceOnDropInfo> otherObjectsToPlaceOnDropInfoList = new List<otherObjectsToPlaceOnDropInfo> ();

    [Space]

    public float maxDistanceToSearchOtherObjectsToPlaceAround;

    public bool useFindObjectsOnSceneForOtherObjectsToPlace;

    public LayerMask layerToSearchOtherObjectsToPlaceAround;

    public float radiusToSearchOtherObjectsToPlaceAround;

    [Space]
    [Header ("Search Objects On Drop Settings")]
    [Space]

    public bool searchPutObjectSystemsAroundOnDropEnabled;

    public float maxDistanceToSearchPutObjectsSystemsAround;

    public bool useFindObjectsOnScene;

    public LayerMask layerToSearchPutObjectSystemsAround;

    public float radiusToSearchPutObjectSystemsAround;

    [Space]
    [Header ("Other Settings")]
    [Space]

    public bool useCustomPlaceToPutObjectTransform;

    public Transform customPlaceToPutObjectTransform;

    [Space]

    public bool checkNameOnPutObjectSystemToCallEvent;
    public string nameOnPutObjectSystemToCallEvent;

    [Space]
    [Header ("Debug")]
    [Space]

    public bool showDebugPrint;

    public bool objectInGrabbedState;

    public bool objectPlaced;

    [Space]

    public bool secondaryObjectToPlaceConnectedState;

    public bool connectedToOtherObjectToPlace;

    public bool adjustingObjectToPlaceInProcess;

    [Space]

    public putObjectSystem currentPutObjectSystem;

    public objectToPlaceSystem otherObjectToPlaceConnected;

    [Space]
    [Header ("Event Settings")]
    [Space]

    public bool useEventOnObjectPlacedStateChanged;
    public UnityEvent eventOnObjectPlaced;
    public UnityEvent eventOnObjectRemoved;

    [Space]

    public bool useEventOnConnectedToOtherObjectStateChanged;
    public UnityEvent eventOnConnectedToOtherObject;
    public UnityEvent eventOnDisconnectedToOtherObject;


    public string getObjectName ()
    {
        return objectName;
    }

    public void assignPutObjectSystem (putObjectSystem putObjectSystemToAssign)
    {
        currentPutObjectSystem = putObjectSystemToAssign;
    }

    public void setObjectPlaceState (bool state)
    {
        objectPlaced = state;
    }

    public bool isObjectPlaced ()
    {
        return objectPlaced;
    }

    public bool isObjectInGrabbedState ()
    {
        return objectInGrabbedState;
    }

    public void setObjectToPlacedEnabledState (bool state)
    {
        objectToPlacedEnabled = state;
    }

    public bool isobjectToPlacedEnabled ()
    {
        return objectToPlacedEnabled;
    }

    public bool canObjectCanCallPlacedEvent ()
    {
        return objectCanCallPlacedEvent;
    }

    public bool canObjectCanCallRemovedEvent ()
    {
        return objectCanCallRemovedEvent;
    }

    public void setObjectCanCallPlacedEventState (bool state)
    {
        objectCanCallPlacedEvent = state;
    }

    public void setObjectCanCallRemovedEventState (bool state)
    {
        objectCanCallRemovedEvent = state;
    }

    public bool isUseCustomPlaceToPutObjectTransformActive ()
    {
        return useCustomPlaceToPutObjectTransform;
    }

    public Transform getCustomPlaceToPutObjectTransform ()
    {
        return customPlaceToPutObjectTransform;
    }

    public bool isUseSecondaryObjectToPlaceConnectedEnabled ()
    {
        return useSecondaryObjectToPlaceConnected;
    }

    public bool isSecondaryObjectToPlaceConnected ()
    {
        return secondaryObjectToPlaceConnectedState;
    }

    public string getOtherObjectsToPlaceOnDropName ()
    {
        return otherObjectsToPlaceOnDropName;
    }

    public bool isConnectedToOtherObjectToPlace ()
    {
        return connectedToOtherObjectToPlace;
    }

    public void setAdjustingObjectToPlaceInProcessState (bool state)
    {
        adjustingObjectToPlaceInProcess = state;
    }

    public bool isAdjustingObjectToPlaceInProcess ()
    {
        return adjustingObjectToPlaceInProcess;
    }

    public void removeObjectIfPlaced ()
    {
        if (objectPlaced) {
            setObjectInGrabbedState (true);

            objectInGrabbedState = false;

            Rigidbody mainRigidbody = gameObject.GetComponent<Rigidbody> ();

            if (mainRigidbody != null) {
                mainRigidbody.isKinematic = false;

                mainRigidbody.useGravity = true;
            }
        }
    }

    public void removeObjectIfConnectedToOtherObject ()
    {
        if (connectedToOtherObjectToPlace) {
            setObjectInGrabbedState (true);

            objectInGrabbedState = false;

            Rigidbody mainRigidbody = gameObject.GetComponent<Rigidbody> ();

            if (mainRigidbody != null) {
                mainRigidbody.isKinematic = false;

                mainRigidbody.useGravity = true;
            }
        }
    }

    public void setObjectInGrabbedState (bool state)
    {
        if (!objectToPlacedEnabled) {
            return;
        }

        objectInGrabbedState = state;

        if (objectInGrabbedState && objectPlaced) {
            objectPlaced = false;

            if (currentPutObjectSystem != null) {
                currentPutObjectSystem.removePlacedObject ();

                currentPutObjectSystem = null;
            }

            checkEventOnObjectPlacedStateChanged (false);
        }

        if (showDebugPrint) {
            print (objectPlaced + " " + searchOtherObjectsToPlaceOnDropEnabled + " " +
                objectInGrabbedState + " " + connectedToOtherObjectToPlace);
        }

        if (!objectPlaced) {
            if (searchOtherObjectsToPlaceOnDropEnabled) {
                if (objectInGrabbedState) {
                    loopCounter = 0;

                    if (connectedToOtherObjectToPlace) {
                        print ("connected to other object to place, disconnecting to grab");

                        setConnectedToOtherObjectToPlaceState (false, null);

                        connectedToOtherObjectToPlace = false;
                    }

                    //if (secondaryObjectToPlaceConnectedState) {
                    //print ("extreme is connected to something");

                    checkToSetStateOnSecondaryObjectToPlaceOnObjectPlaceStateChange (false, this);
                    //}
                }
            }
        }
    }

    int loopCounter = 0;

    public void checkToSetStateOnSecondaryObjectToPlaceOnObjectPlaceStateChange (bool state, objectToPlaceSystem originalObjectToPlace)
    {
        //if (showDebugPrint) {
        //print ("checking state on secondary object on " + gameObject.name);
        // }

        loopCounter++;

        if (loopCounter > 30) {
            print ("infinite loop, stopping");

            return;
        }

        //set the state on the extreme of this object and if that extreme has any device connnected
        if (secondaryObjectToPlaceSystem.otherObjectToPlaceConnected != null) {
            if (originalObjectToPlace != null && secondaryObjectToPlaceSystem.otherObjectToPlaceConnected == originalObjectToPlace) {
                print ("loop detected, cable connected to itself, cancelling check");
            } else {
                secondaryObjectToPlaceSystem.otherObjectToPlaceConnected.checkEventsOnCurrentPutObjectSystemState (state);

                secondaryObjectToPlaceSystem.otherObjectToPlaceConnected.
                    checkToSetStateOnSecondaryObjectToPlaceOnObjectPlaceStateChange (state, originalObjectToPlace);
            }
        } else {
            if (secondaryObjectToPlaceSystem.isObjectPlaced ()) {
                secondaryObjectToPlaceSystem.checkEventsOnCurrentPutObjectSystemState (state);
            }
        }
    }

    public void checkStateOnExtremesDisconnected ()
    {
        if (showDebugPrint) {
            print ("connection between extremes broken, disabling connected state and can call events disabled");
        }

        if (objectPlaced) {
            checkEventsOnCurrentPutObjectSystemState (false);

            setObjectCanCallPlacedEventState (false);

            setObjectCanCallRemovedEventState (false);
        }

        if (searchOtherObjectsToPlaceOnDropEnabled) {
            checkToSetStateOnSecondaryObjectToPlaceOnObjectPlaceStateChange (false, null);

            if (otherObjectToPlaceConnected != null) {
                otherObjectToPlaceConnected.checkToSetStateOnSecondaryObjectToPlaceOnObjectPlaceStateChange (false, null);
            }

            if (secondaryObjectToPlaceSystem != null) {
                secondaryObjectToPlaceSystem.setObjectCanCallPlacedEventState (false);

                secondaryObjectToPlaceSystem.setObjectCanCallRemovedEventState (false);
            }
        }
    }

    public void checkStateOnExtremesOnObjectPlaced ()
    {
        if (objectPlaced && searchOtherObjectsToPlaceOnDropEnabled && objectCanCallPlacedEvent) {
            if (showDebugPrint) {
                print ("check extremes on object placed");
            }

            bool extremeOnecheckNameOnPutObjectSystemResult = false;

            checkNameOnPutObjectSystemToCallEventResult (ref extremeOnecheckNameOnPutObjectSystemResult, null);

            if (showDebugPrint) {
                print ("extremes names check result " + extremeOnecheckNameOnPutObjectSystemResult);
            }

            if (extremeOnecheckNameOnPutObjectSystemResult) {
                checkToSetStateOnSecondaryObjectToPlaceOnObjectPlaceStateChange (true, null);
            }
        }
    }

    public void checkNameOnPutObjectSystemToCallEventResult (ref bool result, objectToPlaceSystem originalObjectToPlace)
    {
        if (checkNameOnPutObjectSystemToCallEvent) {
            if (objectPlaced) {
                if (currentPutObjectSystem.getObjectName ().Equals (nameOnPutObjectSystemToCallEvent)) {
                    result = true;
                }
            }

            if (!objectPlaced || !result) {
                if (secondaryObjectToPlaceSystem.otherObjectToPlaceConnected != null) {
                    if (originalObjectToPlace != null && secondaryObjectToPlaceSystem.otherObjectToPlaceConnected == originalObjectToPlace) {
                        print ("loop detected, cable connected to itself, cancelling check");

                        result = false;
                    } else {
                        secondaryObjectToPlaceSystem.
                        otherObjectToPlaceConnected.checkNameOnPutObjectSystemToCallEventResult (ref result, originalObjectToPlace);
                    }
                } else {
                    if (secondaryObjectToPlaceSystem.isObjectPlaced ()) {
                        if (secondaryObjectToPlaceSystem.currentPutObjectSystem.getObjectName ().Equals (secondaryObjectToPlaceSystem.nameOnPutObjectSystemToCallEvent)) {
                            result = true;
                        }
                    }
                }
            }
        } else {
            result = true;
        }

        if (showDebugPrint) {
            print ("checking extreme on " + gameObject.name + " " + result);
        }
    }

    public void checkIfExtremeConnected (ref bool result, bool ignoreIfThisObjectPlaced, objectToPlaceSystem originalObjectToPlace)
    {
        if (objectPlaced && !ignoreIfThisObjectPlaced) {
            if (objectCanCallPlacedEvent) {
                result = true;
            }
        } else {
            if (secondaryObjectToPlaceSystem.otherObjectToPlaceConnected != null) {
                if (originalObjectToPlace != null && secondaryObjectToPlaceSystem.otherObjectToPlaceConnected == originalObjectToPlace) {
                    print ("loop detected, cable connected to itself, cancelling check");

                    result = false;
                } else {
                    secondaryObjectToPlaceSystem.otherObjectToPlaceConnected.checkIfExtremeConnected (ref result, ignoreIfThisObjectPlaced, originalObjectToPlace);
                }
            } else {
                if (secondaryObjectToPlaceSystem.isObjectPlaced () && secondaryObjectToPlaceSystem.canObjectCanCallPlacedEvent ()) {
                    result = true;
                }
            }
        }

        if (showDebugPrint) {
            print ("checking extreme on " + gameObject.name + " " + result);
        }
    }

    public void setSecondaryObjectToPlaceConnectedState (bool state)
    {
        secondaryObjectToPlaceConnectedState = state;

        checkEventsOnCurrentPutObjectSystemState (state);
    }

    public void checkEventsOnCurrentPutObjectSystemState (bool state)
    {
        if (currentPutObjectSystem != null) {
            if (state) {
                if (objectCanCallPlacedEvent) {
                    currentPutObjectSystem.checkEventsOnObjectPlacedOrRemoved (true);
                }
            } else {
                if (objectCanCallRemovedEvent) {
                    currentPutObjectSystem.checkEventsOnObjectPlacedOrRemoved (false);
                }
            }
        }
    }

    public void checkEventOnObjectPlacedStateChanged (bool state)
    {
        if (useEventOnObjectPlacedStateChanged) {
            if (state) {
                eventOnObjectPlaced.Invoke ();
            } else {
                eventOnObjectRemoved.Invoke ();
            }

            if (showDebugPrint) {
                print ("check Event On Object Placed State Changed " + state + " " + gameObject.name);
            }
        }
    }

    public void checkEventOnConnectedToOtherObjectStateChanged (bool state)
    {
        if (useEventOnConnectedToOtherObjectStateChanged) {
            if (state) {
                eventOnConnectedToOtherObject.Invoke ();
            } else {
                eventOnDisconnectedToOtherObject.Invoke ();
            }

            if (showDebugPrint) {
                print ("check Event On Connected To Ohter Object State Changed " + state + " " + gameObject.name);
            }
        }
    }

    public void setConnectedToOtherObjectToPlaceState (bool state, objectToPlaceSystem newObjectToPlaceSystem)
    {
        if (showDebugPrint) {
            print ("setConnectedToOtherObjectToPlaceState " + connectedToOtherObjectToPlace);
        }

        connectedToOtherObjectToPlace = state;

        if (connectedToOtherObjectToPlace) {
            otherObjectToPlaceConnected = newObjectToPlaceSystem;

            otherObjectToPlaceConnected.connectedToOtherObjectToPlace = true;

            otherObjectToPlaceConnected.otherObjectToPlaceConnected = this;

            Transform newParentTransform = getCustomPlaceToPutOtherObjectToPlaceTransform (otherObjectToPlaceConnected.getObjectName ());

            otherObjectToPlaceConnected.transform.SetParent (newParentTransform);

            otherObjectToPlaceConnected.transform.localPosition = Vector3.zero;
            otherObjectToPlaceConnected.transform.localRotation = Quaternion.identity;

            Rigidbody mainOtherObjectToPlaceConnectedRigidbody = otherObjectToPlaceConnected.gameObject.GetComponent<Rigidbody> ();

            if (mainOtherObjectToPlaceConnectedRigidbody != null) {
                mainOtherObjectToPlaceConnectedRigidbody.isKinematic = true;
            }

            //MAKE BOTH COLLIDERS TO IGNORE EACH OTHER
            Collider mainOtherObjectToPlaceConnectedCollider = otherObjectToPlaceConnected.gameObject.GetComponent<Collider> ();

            Collider mainCollider = gameObject.GetComponent<Collider> ();

            if (mainOtherObjectToPlaceConnectedCollider != null && mainCollider != null) {
                Physics.IgnoreCollision (mainOtherObjectToPlaceConnectedCollider, mainCollider, true);
            }


            //print ("check both extremes of each connection to send the event to call to enable");

            //print ("it is needed to check if both extremes are connected fully");

            bool extremeOneConnectedResult = false;

            checkIfExtremeConnected (ref extremeOneConnectedResult, false, this);

            bool extremeTwoConnectedResult = false;

            otherObjectToPlaceConnected.checkIfExtremeConnected (ref extremeTwoConnectedResult, false, otherObjectToPlaceConnected);

            if (showDebugPrint) {
                print ("extremes connected result " + extremeOneConnectedResult + " " + extremeTwoConnectedResult);
            }

            bool extremeOnecheckNameOnPutObjectSystemResult = false;

            checkNameOnPutObjectSystemToCallEventResult (ref extremeOnecheckNameOnPutObjectSystemResult, this);

            bool extremeTwocheckNameOnPutObjectSystemResult = false;

            otherObjectToPlaceConnected.checkNameOnPutObjectSystemToCallEventResult (ref extremeTwocheckNameOnPutObjectSystemResult
                , otherObjectToPlaceConnected);

            if (showDebugPrint) {
                print ("extremes names check result " + extremeOnecheckNameOnPutObjectSystemResult + " " +
                extremeTwocheckNameOnPutObjectSystemResult);
            }


            if (extremeOneConnectedResult && extremeTwoConnectedResult &&
                (extremeOnecheckNameOnPutObjectSystemResult ||
                extremeTwocheckNameOnPutObjectSystemResult)) {
                checkToSetStateOnSecondaryObjectToPlaceOnObjectPlaceStateChange (true, null);

                otherObjectToPlaceConnected.checkToSetStateOnSecondaryObjectToPlaceOnObjectPlaceStateChange (true, null);

                if (showDebugPrint) {
                    print ("both extremes of loop connected");
                }
            }

            checkEventOnConnectedToOtherObjectStateChanged (true);

            otherObjectToPlaceConnected.checkEventOnConnectedToOtherObjectStateChanged (true);
        } else {
            if (otherObjectToPlaceConnected != null) {
                //MAKE BOTH COLLIDERS TO IGNORE EACH OTHER
                Collider mainOtherObjectToPlaceConnectedCollider = otherObjectToPlaceConnected.gameObject.GetComponent<Collider> ();

                Collider mainCollider = gameObject.GetComponent<Collider> ();

                if (mainOtherObjectToPlaceConnectedCollider != null && mainCollider != null) {
                    Physics.IgnoreCollision (mainOtherObjectToPlaceConnectedCollider, mainCollider, false);
                }

                otherObjectToPlaceConnected.connectedToOtherObjectToPlace = false;

                if (showDebugPrint) {
                    print ("check both extremes of each connection to send the event to call to disable");
                }

                checkToSetStateOnSecondaryObjectToPlaceOnObjectPlaceStateChange (false, this);

                otherObjectToPlaceConnected.checkToSetStateOnSecondaryObjectToPlaceOnObjectPlaceStateChange (false, otherObjectToPlaceConnected);


                checkEventOnConnectedToOtherObjectStateChanged (false);

                otherObjectToPlaceConnected.checkEventOnConnectedToOtherObjectStateChanged (false);


                Rigidbody mainOtherObjectToPlaceConnectedRigidbody = otherObjectToPlaceConnected.gameObject.GetComponent<Rigidbody> ();

                if (mainOtherObjectToPlaceConnectedRigidbody != null) {
                    mainOtherObjectToPlaceConnectedRigidbody.isKinematic = false;
                }

                otherObjectToPlaceConnected.otherObjectToPlaceConnected = null;

                if (otherObjectToPlaceConnected.transform.IsChildOf (transform)) {
                    otherObjectToPlaceConnected.transform.SetParent (null);
                } else if (transform.IsChildOf (otherObjectToPlaceConnected.transform)) {
                    transform.SetParent (null);
                }
            }

            otherObjectToPlaceConnected = null;
        }
    }

    public void checkObjectsAround ()
    {
        if (searchPutObjectSystemsAroundOnDropEnabled) {
            float minDistance = Mathf.Infinity;

            int putObjectSystemIndex = -1;

            List<putObjectSystem> objectsLocatedList = new List<putObjectSystem> ();

            if (useFindObjectsOnScene) {
                putObjectSystem [] putObjectSystemList = GKC_Utils.findObjectsByComponent<putObjectSystem> (false);

                foreach (putObjectSystem currentPutObjectSystem in putObjectSystemList) {
                    if (!objectsLocatedList.Contains (currentPutObjectSystem)) {
                        objectsLocatedList.Add (currentPutObjectSystem);
                    }
                }
            } else {
                Collider [] colliders = Physics.OverlapSphere (transform.position,
                    radiusToSearchPutObjectSystemsAround,
                    layerToSearchPutObjectSystemsAround);

                if (colliders.Length == 0) {
                    if (showDebugPrint) {
                        print ("objects not found on radius");
                    }
                } else {
                    if (showDebugPrint) {
                        print ("objects found on radius " + colliders.Length);
                    }
                }

                int collidersLength = colliders.Length;

                for (int i = 0; i < collidersLength; i++) {
                    putObjectSystem temporalputObjectSystem = colliders [i].gameObject.GetComponent<putObjectSystem> ();

                    if (temporalputObjectSystem != null) {
                        objectsLocatedList.Add (temporalputObjectSystem);
                    }
                }
            }

            int objectsLocatedListCount = objectsLocatedList.Count;

            for (int i = 0; i < objectsLocatedListCount; i++) {
                bool putObjectSystemLocatedResult = false;

                if (objectsLocatedList [i].isPutObjectSystemEnabled ()) {

                    float currentDistance = GKC_Utils.distance (transform.position, objectsLocatedList [i].getPlaceToPutObjectPosition ());

                    if (maxDistanceToSearchPutObjectsSystemsAround == 0 || currentDistance < maxDistanceToSearchPutObjectsSystemsAround) {
                        if (currentDistance < minDistance) {
                            putObjectSystemLocatedResult = true;

                            minDistance = currentDistance;
                        }
                    }

                    if (showDebugPrint) {
                        print ("checking state on " + objectsLocatedList [i].name + " " +
                            putObjectSystemLocatedResult + " " + currentDistance);
                    }
                }

                if (putObjectSystemLocatedResult) {
                    putObjectSystemIndex = i;
                }
            }

            if (putObjectSystemIndex > -1) {
                if (showDebugPrint) {
                    print ("object found, connecting " + objectsLocatedList [putObjectSystemIndex].name);
                }

                objectsLocatedList [putObjectSystemIndex].placeObject (gameObject);

                return;
            } else {
                if (showDebugPrint) {
                    print ("no objects found");
                }
            }
        }

        if (searchOtherObjectsToPlaceOnDropEnabled) {
            float minDistance = Mathf.Infinity;

            int otherObjectToPlaceIndex = -1;

            List<objectToPlaceSystem> objectsLocatedList = new List<objectToPlaceSystem> ();

            if (useFindObjectsOnSceneForOtherObjectsToPlace) {
                objectToPlaceSystem [] objectToPlaceSystemList = GKC_Utils.findObjectsByComponent<objectToPlaceSystem> (false);

                foreach (objectToPlaceSystem currentObjectToPlaceSystem in objectToPlaceSystemList) {
                    if (!objectsLocatedList.Contains (currentObjectToPlaceSystem)) {
                        objectsLocatedList.Add (currentObjectToPlaceSystem);
                    }
                }
            } else {
                Collider [] colliders = Physics.OverlapSphere (transform.position,
                    radiusToSearchOtherObjectsToPlaceAround,
                    layerToSearchOtherObjectsToPlaceAround);

                if (colliders.Length == 0) {
                    if (showDebugPrint) {
                        print ("objects not found on radius");
                    }
                } else {
                    if (showDebugPrint) {
                        print ("objects found on radius " + colliders.Length);
                    }
                }

                int collidersLength = colliders.Length;

                for (int i = 0; i < collidersLength; i++) {
                    objectToPlaceSystem temporalObjectToPlaceSystem = colliders [i].gameObject.GetComponent<objectToPlaceSystem> ();

                    if (temporalObjectToPlaceSystem != null) {
                        objectsLocatedList.Add (temporalObjectToPlaceSystem);
                    }
                }
            }

            int objectsLocatedListCount = objectsLocatedList.Count;

            for (int i = 0; i < objectsLocatedListCount; i++) {
                bool objectToPlaceSystemResult = false;

                bool canCheckElementResult = false;

                if (objectsLocatedList [i].isobjectToPlacedEnabled () &&
                    !objectsLocatedList [i].isConnectedToOtherObjectToPlace () &&
                    objectsLocatedList [i] != this) {

                    if (objectsLocatedList [i].useOtherObjectsToPlaceOnDropNameList) {
                        canCheckElementResult = checkIfObjectNameIsCompatible (objectsLocatedList [i].getObjectName ());
                    } else {
                        if (objectsLocatedList [i].getObjectName ().Equals (otherObjectsToPlaceOnDropName)) {
                            canCheckElementResult = true;
                        }
                    }
                }

                if (canCheckElementResult) {

                    float currentDistance = GKC_Utils.distance (transform.position, objectsLocatedList [i].transform.position);

                    if (maxDistanceToSearchOtherObjectsToPlaceAround == 0 || currentDistance < maxDistanceToSearchPutObjectsSystemsAround) {
                        if (currentDistance < minDistance) {
                            objectToPlaceSystemResult = true;

                            minDistance = currentDistance;
                        }
                    }

                    if (showDebugPrint) {
                        print ("checking state on " + objectsLocatedList [i].name + " " +
                            objectToPlaceSystemResult + " " + currentDistance);
                    }
                }

                if (showDebugPrint) {
                    print ("result on " + objectsLocatedList [i].gameObject.name + " " +
                        otherObjectsToPlaceOnDropName + " " +
                        objectToPlaceSystemResult + " " +

                        objectsLocatedList [i].getObjectName ().Equals (otherObjectsToPlaceOnDropName));
                }

                if (objectToPlaceSystemResult) {
                    otherObjectToPlaceIndex = i;
                }
            }

            if (otherObjectToPlaceIndex > -1) {
                if (showDebugPrint) {
                    print ("object found, connecting " + objectsLocatedList [otherObjectToPlaceIndex].name);
                }

                objectsLocatedList [otherObjectToPlaceIndex].setConnectedToOtherObjectToPlaceState (true, this);
            } else {
                if (showDebugPrint) {
                    print ("no objects found");
                }
            }
        }
    }

    public bool checkIfObjectNameIsCompatible (string nameToCheck)
    {
        if (useOtherObjectsToPlaceOnDropNameList) {
            int currentIndex = otherObjectsToPlaceOnDropInfoList.FindIndex (s => s.Name.Equals (nameToCheck));

            if (currentIndex > -1) {
                return true;
            } else {
                return false;
            }
        }

        return true;
    }

    public Transform getCustomPlaceToPutOtherObjectToPlaceTransform (string nameToCheck)
    {
        if (useOtherObjectsToPlaceOnDropNameList) {
            int currentIndex = otherObjectsToPlaceOnDropInfoList.FindIndex (s => s.Name.Equals (nameToCheck));

            if (currentIndex > -1) {

                return otherObjectsToPlaceOnDropInfoList [currentIndex].customPlaceToPutOtherObjectToPlaceTransform;
            }
        }

        return customPlaceToPutOtherObjectToPlaceTransform;
    }

    [System.Serializable]
    public class otherObjectsToPlaceOnDropInfo
    {
        public string Name;

        public Transform customPlaceToPutOtherObjectToPlaceTransform;
    }
}
