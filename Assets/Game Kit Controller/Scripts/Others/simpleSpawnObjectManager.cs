using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class simpleSpawnObjectManager : MonoBehaviour
{
    [Header ("Main Setting")]
    [Space]

    public bool spawnManagerEnabled = true;

    public string defaultSpawnCategory;

    [Space]
    [Header ("Spawn List Settings")]
    [Space]

    public List<simpleSpawnInfoElement> simpleSpawnInfoElementList = new List<simpleSpawnInfoElement> ();

    [Space]
    [Header ("Debug")]
    [Space]

    public bool showDebugPrint;

    public bool spawnInfoElementListSelected;

    [Space]
    [Header ("Components")]
    [Space]

    public spawnObject mainSpawnObject;


    simpleSpawnInfoElement currentSimpleSpawnInfoElement;



    public void setCurrentSpawnCategoryByName (string elementListName)
    {
        if (!spawnManagerEnabled) {
            return;
        }

        int currentIndex = simpleSpawnInfoElementList.FindIndex (s => s.Name.Equals (elementListName));

        if (currentIndex == -1) {
            setCurrentSpawnCategoryByName (defaultSpawnCategory);

            return;
        }

        if (currentIndex > -1) {
            spawnInfoElementListSelected = true;

            currentSimpleSpawnInfoElement = simpleSpawnInfoElementList [currentIndex];

            if (showDebugPrint) {
                print ("setCurrentSpawnCategoryByName " + currentSimpleSpawnInfoElement.Name);
            }
        }
    }


    public void activateSpawnObjectByName (string objectName)
    {
        if (!spawnManagerEnabled) {
            return;
        }

        if (!spawnInfoElementListSelected) {
            setCurrentSpawnCategoryByName (defaultSpawnCategory);
        }

        if (showDebugPrint) {
            print ("activateSpawnObjectByName, checking object to spawn " + objectName);
        }

        int currentIndex = currentSimpleSpawnInfoElement.simpleSpawnInfoList.FindIndex (s => s.Name.Equals (objectName));

        if (currentIndex > -1) {
            simpleSpawnInfo currentSimpleSpawnInfo = currentSimpleSpawnInfoElement.simpleSpawnInfoList [currentIndex];

            if (showDebugPrint) {
                print ("object located " + objectName + " " + currentSimpleSpawnInfo.spawnObjectEnabled);
            }

            if (currentSimpleSpawnInfo.spawnObjectEnabled) {
                if (currentSimpleSpawnInfo.useCustomSpawnObject) {
                    currentSimpleSpawnInfo.customSpawnObject.activateSpawnObject ();
                } else {
                    mainSpawnObject.setObjectToSpawn (currentSimpleSpawnInfo.objectToSpawn);

                    if (currentSimpleSpawnInfo.spawnPosition != null) {
                        mainSpawnObject.setSpawnPosition (currentSimpleSpawnInfo.spawnPosition);
                    }

                    mainSpawnObject.activateSpawnObject ();
                }
            }
        }
    }

    public void setSpawnObjectEnabledStateByName (string objectName)
    {
        setSpawnObjectEnabledStateByName (true, objectName);
    }

    public void setSpawnObjectDisabledStateByName (string objectName)
    {
        setSpawnObjectEnabledStateByName (false, objectName);
    }

    public void setSpawnObjectEnabledStateByName (bool state, string objectName)
    {
        if (!spawnManagerEnabled) {
            return;
        }

        if (!spawnInfoElementListSelected) {
            setCurrentSpawnCategoryByName (defaultSpawnCategory);
        }

        int currentIndex = currentSimpleSpawnInfoElement.simpleSpawnInfoList.FindIndex (s => s.Name.Equals (objectName));

        if (currentIndex > -1) {
            simpleSpawnInfo currentSimpleSpawnInfo = currentSimpleSpawnInfoElement.simpleSpawnInfoList [currentIndex];

            currentSimpleSpawnInfo.spawnObjectEnabled = state;

            if (showDebugPrint) {
                print ("setSpawnObjectEnabledStateByName " + state + " " + objectName);
            }
        }
    }

    public void setSpawnManagerEnabledState (bool state)
    {
        spawnManagerEnabled = state;
    }

    public void enableRemoteEventState (string remoteEventName)
    {
        enableOrDisableRemoteEventState (remoteEventName, true);
    }

    public void disableRemoteEventState (string remoteEventName)
    {
        enableOrDisableRemoteEventState (remoteEventName, false);
    }

    void enableOrDisableRemoteEventState (string remoteEventName, bool state)
    {
        if (!spawnManagerEnabled) {
            return;
        }

        if (!spawnInfoElementListSelected) {
            setCurrentSpawnCategoryByName (defaultSpawnCategory);
        }

        if (remoteEventName == null || remoteEventName == "") {
            return;
        }

        string eventName = "";

        string spawnName = "";

        bool splitSymbolLocated = remoteEventName.Contains (";");

        if (splitSymbolLocated) {
            string [] spawnNameAndEvent = remoteEventName.Split (';');

            if (spawnNameAndEvent.Length > 1) {
                spawnName = spawnNameAndEvent [0];

                eventName = spawnNameAndEvent [1];

                if (showDebugPrint) {
                    print ("enableOrDisableRemoteEventState " + spawnName + " " + eventName);
                }
            }
        }

        if (eventName == "" || spawnName == "") {
            if (showDebugPrint) {
                print ("enableOrDisableRemoteEventState spawn name or event name not found " + remoteEventName);
            }

            return;
        }

        int currentIndex = currentSimpleSpawnInfoElement.simpleSpawnInfoList.FindIndex (s => s.Name.Equals (spawnName));

        if (currentIndex > -1) {
            simpleSpawnInfo currentSimpleSpawnInfo = currentSimpleSpawnInfoElement.simpleSpawnInfoList [currentIndex];

            if (currentSimpleSpawnInfo.useCustomSpawnObject) {
                if (state) {
                    currentSimpleSpawnInfo.customSpawnObject.enableRemoteEventState (eventName);
                } else {
                    currentSimpleSpawnInfo.customSpawnObject.disableRemoteEventState (eventName);
                }
            }
        }
    }


    public void enableAllRemoteEventStates (string spawnInfoNameToSearch)
    {
        enableOrDisableAllRemoteEventStates (spawnInfoNameToSearch, true);
    }

    public void disableAllRemoteEventStates (string spawnInfoNameToSearch)
    {
        enableOrDisableAllRemoteEventStates (spawnInfoNameToSearch, false);
    }

    void enableOrDisableAllRemoteEventStates (string spawnInfoNameToSearch, bool state)
    {
        if (!spawnManagerEnabled) {
            return;
        }

        if (!spawnInfoElementListSelected) {
            setCurrentSpawnCategoryByName (defaultSpawnCategory);
        }

        if (spawnInfoNameToSearch == null || spawnInfoNameToSearch == "") {
            return;
        }

        int currentIndex = currentSimpleSpawnInfoElement.simpleSpawnInfoList.FindIndex (s => s.Name.Equals (spawnInfoNameToSearch));

        if (currentIndex > -1) {
            simpleSpawnInfo currentSimpleSpawnInfo = currentSimpleSpawnInfoElement.simpleSpawnInfoList [currentIndex];

            if (currentSimpleSpawnInfo.useCustomSpawnObject) {

                currentSimpleSpawnInfo.customSpawnObject.enableOrDisableAllRemoteEventStates (state);
            }
        }
    }

    [System.Serializable]
    public class simpleSpawnInfoElement
    {
        public string Name;
        public List<simpleSpawnInfo> simpleSpawnInfoList = new List<simpleSpawnInfo> ();
    }

    [System.Serializable]
    public class simpleSpawnInfo
    {
        public string Name;
        public bool spawnObjectEnabled = true;

        [Space]

        public GameObject objectToSpawn;

        public Transform spawnPosition;

        [Space]

        public bool useCustomSpawnObject;
        public spawnObject customSpawnObject;
    }
}
