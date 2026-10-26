using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class remoteEventSearcher : MonoBehaviour
{
    [Header ("Main Settings")]
    [Space]

    public bool remoteEventsEnabled = true;

    public bool searchForRemoteEventSockedEnabled;

    public Transform mainTransformCenter;

    [Space]
    [Header ("Remote Event Searcher Info List Settings")]
    [Space]

    public List<remoteEventSearcherInfo> remoteEventSearcherInfoList = new List<remoteEventSearcherInfo> ();

    [Space]
    [Header ("Debug")]
    [Space]

    public bool showDebugPrint;

    public List<GameObject> objectsLocatedList = new List<GameObject> ();


    int currentRemoteEventSearcherInfoIndex;

    remoteEventActivator.removeEventInfo currentEventInfo;


    public void activateRemoteEventSearcher (string remoteEventSearcherName)
    {
        if (!remoteEventsEnabled) {
            return;
        }

        currentRemoteEventSearcherInfoIndex = -1;

        for (int i = 0; i < remoteEventSearcherInfoList.Count; i++) {
            if (remoteEventSearcherInfoList [i].Name.Equals (remoteEventSearcherName)) {
                currentRemoteEventSearcherInfoIndex = i;
            }
        }

        if (currentRemoteEventSearcherInfoIndex == -1) {
            if (showDebugPrint) {
                print ("remote event name not found " + remoteEventSearcherName);

            }

            return;
        }


        remoteEventSearcherInfo currentRemoteEventSearcherInfo = remoteEventSearcherInfoList [currentRemoteEventSearcherInfoIndex];

        if (!currentRemoteEventSearcherInfo.remoteEventEnabled) {
            if (showDebugPrint) {
                print ("remote event not enabled");

            }

            return;
        }



        if (currentRemoteEventSearcherInfo.useFindObjectsOnScene) {
            bool useFindObjectsResult = false;

            if (currentRemoteEventSearcherInfo.useFindObjectsOnEachSearch) {
                objectsLocatedList.Clear ();

                useFindObjectsResult = true;
            } else {
                if (objectsLocatedList.Count == 0) {
                    useFindObjectsResult = true;
                }
            }

            if (useFindObjectsResult) {
                remoteEventSystem [] remoteEventSystemList = GKC_Utils.findObjectsByComponent<remoteEventSystem> ();

                foreach (remoteEventSystem currentRemoteEventSystem in remoteEventSystemList) {
                    if (!objectsLocatedList.Contains (currentRemoteEventSystem.gameObject)) {
                        objectsLocatedList.Add (currentRemoteEventSystem.gameObject);
                    }
                }
            }
        } else {
            objectsLocatedList.Clear ();

            if (mainTransformCenter == null) {
                mainTransformCenter = transform;
            }

            Collider [] colliders = Physics.OverlapSphere (mainTransformCenter.position, currentRemoteEventSearcherInfo.radiusToSearch, currentRemoteEventSearcherInfo.radiusLayer);

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
                objectsLocatedList.Add (colliders [i].gameObject);
            }
        }

        int objectsLocatedListCount = objectsLocatedList.Count;

        for (int i = 0; i < objectsLocatedListCount; i++) {
            bool canActivateEvent = true;

            GameObject currentObjectDetected = objectsLocatedList [i];

            if (currentObjectDetected != null) {

                if (currentRemoteEventSearcherInfo.ignoreCharacters) {
                    if (currentRemoteEventSearcherInfo.characterObjectToIgnoreList.Count > 0) {
                        if (currentRemoteEventSearcherInfo.characterObjectToIgnoreList.Contains (currentObjectDetected.gameObject)) {
                            canActivateEvent = false;
                        }
                    }

                    if (currentRemoteEventSearcherInfo.tagsToIgnore.Contains (currentObjectDetected.tag)) {
                        canActivateEvent = false;
                    }
                }

                if (canActivateEvent) {
                    remoteEventSystem currentRemoteEventSystem = 
                        GKC_Utils.getRemoteEventSystemFromObject (currentObjectDetected, searchForRemoteEventSockedEnabled);
                    
                    //currentObjectDetected.GetComponent<remoteEventSystem> ();

                    if (currentRemoteEventSystem != null) {

                        bool checkRemoteEventSystemResult = true;

                        if (currentRemoteEventSearcherInfo.searchRemoteEventByID) {
                            if (currentRemoteEventSearcherInfo.useSearchRemoteEventByIDList) {
                                if (!currentRemoteEventSearcherInfo.remoteEventIDToSearchList.Contains (currentRemoteEventSystem.getRemoteEventSystemID ())) {
                                    checkRemoteEventSystemResult = false;
                                }
                            } else {
                                if (!currentRemoteEventSearcherInfo.remoteEventIDToSearch.Equals (currentRemoteEventSystem.getRemoteEventSystemID ())) {
                                    checkRemoteEventSystemResult = false;
                                }
                            }
                        }

                        if (currentRemoteEventSearcherInfo.searchRemoteEventByName) {
                            if (currentRemoteEventSearcherInfo.useSearchRemoteEventByNameList) {
                                if (!currentRemoteEventSearcherInfo.remoteEventNameToSearchList.Contains (currentRemoteEventSystem.getRemoteEventSystemName ())) {
                                    checkRemoteEventSystemResult = false;
                                }
                            } else {
                                if (!currentRemoteEventSearcherInfo.remoteEventNameToSearch.Equals (currentRemoteEventSystem.getRemoteEventSystemName ())) {
                                    checkRemoteEventSystemResult = false;
                                }
                            }
                        }

                        if (checkRemoteEventSystemResult) {

                            if (showDebugPrint) {
                                print ("remote event system found on object " + currentObjectDetected.name);
                            }

                            if (currentRemoteEventSearcherInfo.useRemoveEventInfoList) {
                                int removeEventInfoListCount = currentRemoteEventSearcherInfo.mainRemoveEventInfoList.Count;

                                string currentRemoteEventToCall = currentRemoteEventSearcherInfo.remoteEventToCall;

                                for (int j = 0; j < removeEventInfoListCount; j++) {
                                    currentEventInfo = currentRemoteEventSearcherInfo.mainRemoveEventInfoList [j];

                                    if (!currentRemoteEventSearcherInfo.useSameRemoteEventToCall) {
                                        currentRemoteEventToCall = currentEventInfo.remoteEventToCall;
                                    }

                                    if (currentEventInfo.useAmount) {
                                        currentRemoteEventSystem.callRemoteEventWithAmount (currentRemoteEventToCall, currentEventInfo.amountValue);
                                    } else if (currentEventInfo.useBool) {
                                        currentRemoteEventSystem.callRemoteEventWithBool (currentRemoteEventToCall, currentEventInfo.boolValue);
                                    } else if (currentEventInfo.useGameObject) {
                                        currentRemoteEventSystem.callRemoteEventWithGameObject (currentRemoteEventToCall, currentEventInfo.gameObjectToUse);
                                    } else if (currentEventInfo.useTransform) {
                                        currentRemoteEventSystem.callRemoteEventWithTransform (currentRemoteEventToCall, currentEventInfo.transformToUse);
                                    } else {
                                        currentRemoteEventSystem.callRemoteEvent (currentRemoteEventToCall);
                                    }
                                }
                            } else {
                                if (currentRemoteEventSearcherInfo.useAmount) {
                                    currentRemoteEventSystem.callRemoteEventWithAmount (currentRemoteEventSearcherInfo.remoteEventToCall, currentRemoteEventSearcherInfo.amountValue);
                                } else if (currentRemoteEventSearcherInfo.useBool) {
                                    currentRemoteEventSystem.callRemoteEventWithBool (currentRemoteEventSearcherInfo.remoteEventToCall, currentRemoteEventSearcherInfo.boolValue);
                                } else if (currentRemoteEventSearcherInfo.useGameObject) {
                                    currentRemoteEventSystem.callRemoteEventWithGameObject (currentRemoteEventSearcherInfo.remoteEventToCall, currentRemoteEventSearcherInfo.gameObjectToUse);
                                } else if (currentRemoteEventSearcherInfo.useTransform) {
                                    currentRemoteEventSystem.callRemoteEventWithTransform (currentRemoteEventSearcherInfo.remoteEventToCall, currentRemoteEventSearcherInfo.transformToUse);
                                } else {
                                    currentRemoteEventSystem.callRemoteEvent (currentRemoteEventSearcherInfo.remoteEventToCall);
                                }
                            }
                        }

                    } else {
                        if (showDebugPrint) {
                            print ("remote event system not found on object " + currentObjectDetected.name);
                        }
                    }
                }
            }
        }
    }

    [System.Serializable]
    public class remoteEventSearcherInfo
    {
        [Header ("Main Settings")]
        [Space]

        public string Name;

        public bool remoteEventEnabled = true;

        [Space]
        [Space]

        public string remoteEventToCall;

        public bool useRemoveEventInfoList;

        public bool useSameRemoteEventToCall = true;

        [Space]
        [Header ("Remote Event Info Settings")]
        [Space]

        public bool useAmount;
        public float amountValue;

        [Space]

        public bool useBool;
        public bool boolValue;

        [Space]

        public bool useGameObject;
        public GameObject gameObjectToUse;

        [Space]

        public bool useTransform;
        public Transform transformToUse;

        [Space]
        [Header ("Ignore Objects Settings")]
        [Space]

        public bool ignoreCharacters;

        public List<string> tagsToIgnore = new List<string> ();

        public List<GameObject> characterObjectToIgnoreList = new List<GameObject> ();

        [Space]
        [Header ("Search Objects By Radius Settings")]
        [Space]

        public float radiusToSearch;
        public LayerMask radiusLayer;

        [Space]
        [Header ("Search Objects By Find Settings")]
        [Space]

        public bool useFindObjectsOnScene;
        public bool useFindObjectsOnEachSearch = true;

        [Space]
        [Space]

        public bool searchRemoteEventByID;
        public int remoteEventIDToSearch;

        [Space]

        public bool useSearchRemoteEventByIDList;
        public List<int> remoteEventIDToSearchList = new List<int> ();

        [Space]
        [Space]

        public bool searchRemoteEventByName;
        public string remoteEventNameToSearch;

        [Space]

        public bool useSearchRemoteEventByNameList;
        public List<string> remoteEventNameToSearchList = new List<string> ();

        [Space]
        [Header ("Remote Event List Settings")]
        [Space]

        public List<remoteEventActivator.removeEventInfo> mainRemoveEventInfoList = new List<remoteEventActivator.removeEventInfo> ();
    }
}
