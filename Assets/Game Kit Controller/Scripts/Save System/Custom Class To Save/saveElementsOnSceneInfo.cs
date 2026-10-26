using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;
using System;
using System.Runtime.Serialization;
using System.Linq;

public class saveElementsOnSceneInfo : saveGameInfo
{
    public elementOnSceneManager mainElementOnSceneManager;

    [Space]
    [Header ("Debug")]
    [Space]

    public List<persistanceElementOnSceneInfo> persistanceInfoList;

    public persistancePlayerElementOnSceneInfo temporalPersistancePlayerElementOnSceneInfo;

    bool showCurrentDebugInfo;

    public override void saveGame (int saveNumber, int playerID, string currentSaveDataPath, bool showDebugInfo, bool savingGameToChangeScene)
    {
        saveGameContent (saveNumber, playerID, currentSaveDataPath, showDebugInfo, savingGameToChangeScene);
    }

    public override void loadGame (int saveNumberToLoad, int playerID, string currentSaveDataPath, bool showDebugInfo)
    {
        loadGameContent (saveNumberToLoad, playerID, currentSaveDataPath, showDebugInfo);
    }

    public override void initializeValuesOnComponent ()
    {
        initializeValues ();
    }

    bool getFileContent (string path, out SaveSlotListWrapper saveData, bool showDebugInfo)
    {
        bool fileResult = false;

        saveData = new SaveSlotListWrapper ();

        if (File.Exists (path)) {
            try {
                string json = File.ReadAllText (path);

                if (string.IsNullOrWhiteSpace (json)) {
                    if (showDebugInfo) {
                        Debug.Log ("Save File located but empty" + path);
                        Debug.Log ("\n\n");
                    }
                } else {
                    saveData = JsonUtility.FromJson<SaveSlotListWrapper> (json);

                    if (saveData == null) {
                        saveData = new SaveSlotListWrapper ();
                    }

                    if (saveData.infoList == null) {
                        saveData.infoList = new List<persistanceElementsOnSceneBySaveSlotInfo> ();
                    }

                    fileResult = true;

                    if (showDebugInfo) {
                        Debug.Log ("Save File located " + path);
                        Debug.Log ("\n\n");
                    }
                }
            }

            catch (Exception ex) {
                Debug.LogError (
                    $"Error loading save file {path}\n{ex}"
                );

                saveData = new SaveSlotListWrapper ();

                if (saveData.infoList == null) {
                    saveData.infoList = new List<persistanceElementsOnSceneBySaveSlotInfo> ();
                }
            }
        } else {
            if (showDebugInfo) {
                Debug.Log ("Save File not located " + path);
                Debug.Log ("\n\n");
            }
        }

        return fileResult;
    }

    void saveFileContent (string path, string jsonToSave)
    {
        try {
            string dir = Path.GetDirectoryName (path);

            if (!string.IsNullOrEmpty (dir)) {
                Directory.CreateDirectory (dir);
            }

            File.WriteAllText (path, jsonToSave, System.Text.Encoding.UTF8);
        }
        catch (Exception ex) {
            Debug.LogError (
                $"Error saving file {path}\n{ex}"
            );
        }
    }

    public void saveGameContent (int currentSaveNumber, int playerID, string currentSaveDataPath, bool showDebugInfo, bool savingGameToChangeScene)
    {
        getMainManager ();

        if (mainElementOnSceneManager == null) {
            return;
        }

        if (!mainElementOnSceneManager.isSaveCurrentPlayerElementsOnSceneToSaveFile ()) {
            return;
        }

        if (showDebugInfo) {
            print ("\n\n");

            print ("Saving elements on scene");
        }

        bool saveLocated = false;
        bool playerLocated = false;

        int saveSlotIndex = -1;
        int listIndex = -1;

        persistancePlayerElementOnSceneInfo elementsOnSceneToSave = getPersistanceList (playerID, showDebugInfo);

        persistanceElementsOnSceneBySaveSlotInfo newPersistancePlayerElementsOnSceneListBySaveSlotInfo = new persistanceElementsOnSceneBySaveSlotInfo ();

        List<persistanceElementsOnSceneBySaveSlotInfo> infoListToSave = new List<persistanceElementsOnSceneBySaveSlotInfo> ();

        SaveSlotListWrapper saveData = new SaveSlotListWrapper ();

        if (getFileContent (currentSaveDataPath, out saveData, showDebugInfo)) {
            infoListToSave = saveData.infoList;
        }

        int infoListToSaveCount = infoListToSave.Count;

        for (int j = 0; j < infoListToSaveCount; j++) {
            if (infoListToSave [j].saveNumber == currentSaveNumber) {
                newPersistancePlayerElementsOnSceneListBySaveSlotInfo = infoListToSave [j];
                saveLocated = true;
                saveSlotIndex = j;
            }
        }

        if (saveLocated) {
            int playerElementsOnSceneListCount = newPersistancePlayerElementsOnSceneListBySaveSlotInfo.playerElementsOnSceneList.Count;

            for (int j = 0; j < playerElementsOnSceneListCount; j++) {
                if (newPersistancePlayerElementsOnSceneListBySaveSlotInfo.playerElementsOnSceneList [j].playerID == elementsOnSceneToSave.playerID) {
                    playerLocated = true;
                    listIndex = j;
                }
            }
        }

        if (showDebugInfo) {
            print ("\n\n");

            print ("EXTRA INFO\n");
            print ("Number of elements on scene: " + elementsOnSceneToSave.elementOnSceneList.Count);
            print ("Current Save Number " + currentSaveNumber);
            print ("Save Located " + saveLocated);
            print ("Player Located " + playerLocated);
            print ("Player ID " + elementsOnSceneToSave.playerID);

            print ("Scene Index " + menuPause.getCurrentActiveSceneIndex ());
        }

        int currentSceneIndex = menuPause.getCurrentActiveSceneIndex ();

        //if the save is located, check if the player id exists
        if (saveLocated) {
            //if player id exists, overwrite it
            if (playerLocated) {
                int elementOnSceneListCount = infoListToSave [saveSlotIndex].playerElementsOnSceneList [listIndex].elementOnSceneList.Count;

                for (int i = elementOnSceneListCount - 1; i >= 0; i--) {
                    if (currentSceneIndex == infoListToSave [saveSlotIndex].playerElementsOnSceneList [listIndex].elementOnSceneList [i].elementScene) {
                        infoListToSave [saveSlotIndex].playerElementsOnSceneList [listIndex].elementOnSceneList.RemoveAt (i);
                    }
                }

                infoListToSave [saveSlotIndex].playerElementsOnSceneList [listIndex].elementOnSceneList.AddRange (elementsOnSceneToSave.elementOnSceneList);
            } else {
                infoListToSave [saveSlotIndex].playerElementsOnSceneList.Add (elementsOnSceneToSave);
            }
        } else {
            newPersistancePlayerElementsOnSceneListBySaveSlotInfo.saveNumber = currentSaveNumber;

            newPersistancePlayerElementsOnSceneListBySaveSlotInfo.playerElementsOnSceneList.Add (elementsOnSceneToSave);

            infoListToSave.Add (newPersistancePlayerElementsOnSceneListBySaveSlotInfo);
        }

        if (showDebugInfo) {
            print ("\n\n");

            for (int j = 0; j < elementsOnSceneToSave.elementOnSceneList.Count; j++) {
                persistanceElementOnSceneInfo currentPersistanceElementsOnSceneInfo = elementsOnSceneToSave.elementOnSceneList [j];

                if (currentPersistanceElementsOnSceneInfo.elementScene == currentSceneIndex) {
                    showDetailedDebugInfo (currentPersistanceElementsOnSceneInfo);
                }
            }
        }

        saveData.infoList = infoListToSave;

        string jsonToSave = JsonUtility.ToJson (saveData, true);

        saveFileContent (currentSaveDataPath, jsonToSave);

        if (saveInfoOnRegularTxtFile) {
            writeDetailedDebugInfo (false, currentSaveDataPath);
        }
    }

    public void loadGameContent (int saveNumberToLoad, int playerID, string currentSaveDataPath, bool showDebugInfo)
    {
        getMainManager ();

        if (mainElementOnSceneManager == null) {
            return;
        }

        if (!mainElementOnSceneManager.isSaveCurrentPlayerElementsOnSceneToSaveFile ()) {
            initializeValues ();

            return;
        }

        if (showDebugInfo) {
            print ("\n\n");

            print ("Loading elements on scene");
        }

        //need to store and check the current slot saved and the player which is saving, to get that concrete info
        persistanceInfoList = new List<persistanceElementOnSceneInfo> ();

        List<persistanceElementsOnSceneBySaveSlotInfo> infoListToLoad = new List<persistanceElementsOnSceneBySaveSlotInfo> ();

        SaveSlotListWrapper saveData = new SaveSlotListWrapper ();

        if (getFileContent (currentSaveDataPath, out saveData, showDebugInfo)) {
            infoListToLoad = saveData.infoList;
        }

        if (saveNumberToLoad > -1) {
            persistanceElementsOnSceneBySaveSlotInfo newPersistancePlayerElementsOnSceneListBySaveSlotInfo = new persistanceElementsOnSceneBySaveSlotInfo ();

            int infoListToLoadCount = infoListToLoad.Count;

            for (int j = 0; j < infoListToLoadCount; j++) {

                if (infoListToLoad [j].saveNumber == saveNumberToLoad) {
                    newPersistancePlayerElementsOnSceneListBySaveSlotInfo = infoListToLoad [j];
                }
            }

            int listIndex = -1;

            int playerElementsOnSceneListCount = newPersistancePlayerElementsOnSceneListBySaveSlotInfo.playerElementsOnSceneList.Count;

            for (int j = 0; j < playerElementsOnSceneListCount; j++) {

                if (newPersistancePlayerElementsOnSceneListBySaveSlotInfo.playerElementsOnSceneList [j].playerID == playerID) {
                    listIndex = j;
                }
            }

            if (listIndex > -1) {
                int currentSceneIndex = menuPause.getCurrentActiveSceneIndex ();

                temporalPersistancePlayerElementOnSceneInfo = newPersistancePlayerElementsOnSceneListBySaveSlotInfo.playerElementsOnSceneList [listIndex];

                int elementOnSceneListCount = temporalPersistancePlayerElementOnSceneInfo.elementOnSceneList.Count;

                if (showDebugInfo) {
                    print ("\n\n");

                    print ("Total number of elements saved in game " + elementOnSceneListCount);
                }

                for (int j = 0; j < elementOnSceneListCount; j++) {
                    if (currentSceneIndex == temporalPersistancePlayerElementOnSceneInfo.elementOnSceneList [j].elementScene) {
                        persistanceInfoList.Add (temporalPersistancePlayerElementOnSceneInfo.elementOnSceneList [j]);
                    }
                }
            }
        }

        if (showDebugInfo) {
            print ("\n\n");

            print ("Elements Loaded in Save Number " + saveNumberToLoad);
            print ("Number of Elements: " + persistanceInfoList.Count);

            print ("Scene Index " + menuPause.getCurrentActiveSceneIndex ());

            print ("\n\n");

            for (int j = 0; j < persistanceInfoList.Count; j++) {
                print ("\n\n");

                persistanceElementOnSceneInfo currentPersistanceElementsOnSceneInfo = persistanceInfoList [j];

                showDetailedDebugInfo (currentPersistanceElementsOnSceneInfo);
            }
        }

        showCurrentDebugInfo = showDebugInfo;

        loadInfoOnMainComponent ();

        if (saveInfoOnRegularTxtFile) {
            writeDetailedDebugInfo (true, currentSaveDataPath);
        }
    }

    public persistancePlayerElementOnSceneInfo getPersistanceList (int playerID, bool showDebugInfo)
    {
        persistancePlayerElementOnSceneInfo newElementsOnSceneList = new persistancePlayerElementOnSceneInfo ();

        newElementsOnSceneList.playerID = playerID;

        List<persistanceElementOnSceneInfo> newPersistanceElementsOnSceneInfoList = new List<persistanceElementOnSceneInfo> ();

        mainElementOnSceneManager.checkForInstantiatedElementsOnSceneOnSave ();

        List<elementOnScene> elementOnSceneList = mainElementOnSceneManager.elementOnSceneList;

        int elementOnSceneListCount = elementOnSceneList.Count;

        for (int k = 0; k < elementOnSceneListCount; k++) {
            persistanceElementOnSceneInfo newPersistanceElementsOnSceneInfo = new persistanceElementOnSceneInfo ();

            elementOnScene currentSlotInfo = elementOnSceneList [k];

            if (currentSlotInfo != null && currentSlotInfo.isSaveElementEnabled ()) {
                newPersistanceElementsOnSceneInfo.elementScene = currentSlotInfo.elementScene;
                newPersistanceElementsOnSceneInfo.elementID = currentSlotInfo.elementID;

                newPersistanceElementsOnSceneInfo.elementActiveState = currentSlotInfo.elementActiveState;

                if (currentSlotInfo.useStats) {

                    currentSlotInfo.checkEventOnStatsSave ();

                    newPersistanceElementsOnSceneInfo.useStats = true;

                    for (int j = 0; j < currentSlotInfo.statInfoList.Count; j++) {
                        if (currentSlotInfo.statInfoList [j].statIsAmount) {
                            newPersistanceElementsOnSceneInfo.floatValueStatList.Add (currentSlotInfo.statInfoList [j].currentFloatValue);
                        } else {
                            newPersistanceElementsOnSceneInfo.boolValueStatList.Add (currentSlotInfo.statInfoList [j].currentBoolState);
                        }
                    }
                }

                newPersistanceElementsOnSceneInfo.savePositionValues = currentSlotInfo.savePositionValues;

                Vector3 currentPosition = currentSlotInfo.getElementPosition ();

                newPersistanceElementsOnSceneInfo.positionX = currentPosition.x;
                newPersistanceElementsOnSceneInfo.positionY = currentPosition.y;
                newPersistanceElementsOnSceneInfo.positionZ = currentPosition.z;

                newPersistanceElementsOnSceneInfo.saveRotationValues = currentSlotInfo.saveRotationValues;

                Vector3 currentRotation = currentSlotInfo.getElementRotation ();

                newPersistanceElementsOnSceneInfo.rotationX = currentRotation.x;
                newPersistanceElementsOnSceneInfo.rotationY = currentRotation.y;
                newPersistanceElementsOnSceneInfo.rotationZ = currentRotation.z;

                newPersistanceElementsOnSceneInfo.elementPrefabID = currentSlotInfo.elementPrefabID;

                newPersistanceElementsOnSceneInfo.useElementPrefabID = currentSlotInfo.useElementPrefabID;

                //				if (showDebugInfo) {
                //					showDetailedDebugInfo (newPersistanceElementsOnSceneInfo);
                //				}

                newPersistanceElementsOnSceneInfoList.Add (newPersistanceElementsOnSceneInfo);
            }
        }


        List<elementOnSceneManager.temporalElementOnSceneInfo> temporalElementOnSceneList = mainElementOnSceneManager.temporalElementOnSceneInfoList;

        int temporalElementOnSceneListCount = temporalElementOnSceneList.Count;

        for (int k = 0; k < temporalElementOnSceneListCount; k++) {
            int elementIndex = getElementOnSceneIndex (newPersistanceElementsOnSceneInfoList, temporalElementOnSceneList [k].elementID,
                                   temporalElementOnSceneList [k].elementScene);

            if (elementIndex > -1) {
                newPersistanceElementsOnSceneInfoList [elementIndex].elementActiveState = temporalElementOnSceneList [k].elementActiveState;
            } else {
                persistanceElementOnSceneInfo newPersistanceElementsOnSceneInfo = new persistanceElementOnSceneInfo ();

                newPersistanceElementsOnSceneInfo.elementScene = temporalElementOnSceneList [k].elementScene;
                newPersistanceElementsOnSceneInfo.elementID = temporalElementOnSceneList [k].elementID;

                newPersistanceElementsOnSceneInfo.elementActiveState = temporalElementOnSceneList [k].elementActiveState;

                newPersistanceElementsOnSceneInfo.useElementPrefabID = true;

                newPersistanceElementsOnSceneInfoList.Add (newPersistanceElementsOnSceneInfo);
            }
        }

        newElementsOnSceneList.elementOnSceneList = newPersistanceElementsOnSceneInfoList;

        return newElementsOnSceneList;
    }

    int getElementOnSceneIndex (List<persistanceElementOnSceneInfo> newPersistanceElementsOnSceneInfoList, int elementID, int elementScene)
    {
        int newPersistanceElementsOnSceneInfoListCount = newPersistanceElementsOnSceneInfoList.Count;

        for (int j = 0; j < newPersistanceElementsOnSceneInfoListCount; j++) {
            if (newPersistanceElementsOnSceneInfoList [j].elementID == elementID &&
                newPersistanceElementsOnSceneInfoList [j].elementScene == elementScene) {
                return j;
            }
        }

        return -1;
    }

    void loadInfoOnMainComponent ()
    {
        initializeValues ();

        if (persistanceInfoList != null && persistanceInfoList.Count > 0) {

            int persistanceInfoListCount = persistanceInfoList.Count;

            for (int k = 0; k < persistanceInfoListCount; k++) {

                persistanceElementOnSceneInfo currentPersistanceElementsOnSceneInfo = persistanceInfoList [k];

                elementOnScene currentElementOnScene =
                    mainElementOnSceneManager.getElementOnSceneInfo (currentPersistanceElementsOnSceneInfo.elementID, currentPersistanceElementsOnSceneInfo.elementScene);

                if (currentElementOnScene == null) {
                    if (mainElementOnSceneManager.useElementsOnSceneData) {
                        if (currentPersistanceElementsOnSceneInfo.useElementPrefabID) {
                            if (currentPersistanceElementsOnSceneInfo.elementActiveState) {
                                int elementPrefabID = currentPersistanceElementsOnSceneInfo.elementPrefabID;

                                GameObject elementPrefab = mainElementOnSceneManager.mainElementsOnSceneData.getElementScenePrefabById (elementPrefabID);

                                if (elementPrefab != null) {
                                    GameObject newObject = Instantiate (elementPrefab);

                                    newObject.name = elementPrefab.name;

                                    currentElementOnScene = newObject.GetComponentInChildren<elementOnScene> ();

                                    currentElementOnScene.saveElementEnabled = true;

                                    mainElementOnSceneManager.setNewInstantiatedElementOnSceneManagerIngameWithInfo (currentElementOnScene);

                                    if (showCurrentDebugInfo) {
                                        print ("INSTANTIATING ELEMENT ON SCENE FROM PREFAB " + newObject.name + " " + currentElementOnScene.elementID);
                                    }
                                } else {
                                    if (showCurrentDebugInfo) {
                                        print ("INSTANTIATING ERROR ON ELEMENT ON SCENE FROM PREFAB ID" + elementPrefabID);
                                    }
                                }
                            }
                        }
                    }
                }

                if (showCurrentDebugInfo) {
                    if (currentElementOnScene != null) {
                        print ("Element On Scene found " + currentElementOnScene.elementID);
                    } else {
                        print ("Element On Scene Not Found");
                    }
                }

                if (currentElementOnScene != null) {
                    if (showCurrentDebugInfo) {
                        showDetailedDebugInfo (currentPersistanceElementsOnSceneInfo);
                    }

                    if (currentElementOnScene.saveElementEnabled) {
                        if (currentElementOnScene.savePositionValues) {
                            Vector3 newPosition = new Vector3 (currentPersistanceElementsOnSceneInfo.positionX,
                                                      currentPersistanceElementsOnSceneInfo.positionY,
                                                      currentPersistanceElementsOnSceneInfo.positionZ);

                            currentElementOnScene.setNewPositionValues (newPosition);
                        }

                        if (currentElementOnScene.saveRotationValues) {
                            Vector3 newRotation = new Vector3 (currentPersistanceElementsOnSceneInfo.rotationX,
                                                      currentPersistanceElementsOnSceneInfo.rotationY,
                                                      currentPersistanceElementsOnSceneInfo.rotationZ);

                            currentElementOnScene.setNewRotationValues (newRotation);
                        }

                        currentElementOnScene.setElementActiveState (currentPersistanceElementsOnSceneInfo.elementActiveState);

                        currentElementOnScene.checkStateOnLoad ();

                        if (currentPersistanceElementsOnSceneInfo.elementActiveState) {
                            bool canCheckStats = true;

                            if (mainElementOnSceneManager.ignoreLoadStatsOnObjectIDList) {
                                if (mainElementOnSceneManager.ignoreLoadStatsOnObjectIDListInfo.Contains (currentElementOnScene.elementID)) {
                                    canCheckStats = false;
                                }
                            }

                            if (mainElementOnSceneManager.ignoreLoadStatsOnObjectPrefabIDList) {
                                if (mainElementOnSceneManager.ignoreLoadStatsOnObjectPrefabIDListInfo.Contains (currentElementOnScene.elementPrefabID)) {
                                    canCheckStats = false;
                                }
                            }

                            if (currentElementOnScene.useStats && canCheckStats) {
                                int boolValueStatListCount = 0;
                                int floatValueStatListCount = 0;

                                for (int j = 0; j < currentElementOnScene.statInfoList.Count; j++) {
                                    if (currentElementOnScene.statInfoList [j].statIsAmount) {
                                        if (floatValueStatListCount < currentPersistanceElementsOnSceneInfo.floatValueStatList.Count) {
                                            currentElementOnScene.statInfoList [j].currentFloatValue = currentPersistanceElementsOnSceneInfo.floatValueStatList [floatValueStatListCount];
                                        }

                                        floatValueStatListCount++;
                                    } else {
                                        if (boolValueStatListCount < currentPersistanceElementsOnSceneInfo.boolValueStatList.Count) {
                                            currentElementOnScene.statInfoList [j].currentBoolState = currentPersistanceElementsOnSceneInfo.boolValueStatList [boolValueStatListCount];
                                        }

                                        boolValueStatListCount++;
                                    }
                                }

                                currentElementOnScene.setStatsOnLoad ();
                            }
                        }
                    }
                }
            }
        }
    }

    void getMainManager ()
    {
        if (mainElementOnSceneManager == null) {
            mainElementOnSceneManager = elementOnSceneManager.Instance;

            bool mainElementOnSceneManagerLocated = mainElementOnSceneManager != null;

            if (!mainElementOnSceneManagerLocated) {
                GKC_Utils.instantiateMainManagerOnSceneWithTypeOnApplicationPlaying (elementOnSceneManager.getMainManagerName (), typeof (elementOnSceneManager), true);

                mainElementOnSceneManager = elementOnSceneManager.Instance;

                mainElementOnSceneManagerLocated = mainElementOnSceneManager != null;
            }

            if (!mainElementOnSceneManagerLocated) {

                mainElementOnSceneManager = GKC_Utils.findObjectByComponent<elementOnSceneManager> ();
            }
        }
    }

    void initializeValues ()
    {
        getMainManager ();

        if (mainElementOnSceneManager != null) {
            mainElementOnSceneManager.initializeValues ();
        }
    }

    public void setStatsSearchingByInfo (int currentElementScene, int currentElementID, elementOnScene currentElementOnScene)
    {
        if (temporalPersistancePlayerElementOnSceneInfo != null) {
            print ("searching stats on " + currentElementScene + " " + currentElementID + " " + currentElementOnScene.gameObject.name);

            int elementOnSceneListCount = temporalPersistancePlayerElementOnSceneInfo.elementOnSceneList.Count;

            for (int k = 0; k < elementOnSceneListCount; k++) {
                if (currentElementScene == temporalPersistancePlayerElementOnSceneInfo.elementOnSceneList [k].elementScene) {

                    persistanceElementOnSceneInfo currentPersistanceElementsOnSceneInfo = temporalPersistancePlayerElementOnSceneInfo.elementOnSceneList [k];

                    if (currentPersistanceElementsOnSceneInfo.elementScene == currentElementScene && currentPersistanceElementsOnSceneInfo.elementID == currentElementID) {
                        bool canCheckStats = true;

                        if (mainElementOnSceneManager.ignoreLoadStatsOnObjectIDList) {
                            if (mainElementOnSceneManager.ignoreLoadStatsOnObjectIDListInfo.Contains (currentElementOnScene.elementID)) {
                                canCheckStats = false;
                            }
                        }

                        if (mainElementOnSceneManager.ignoreLoadStatsOnObjectPrefabIDList) {
                            if (mainElementOnSceneManager.ignoreLoadStatsOnObjectPrefabIDListInfo.Contains (currentElementOnScene.elementPrefabID)) {
                                canCheckStats = false;
                            }
                        }

                        if (currentElementOnScene.useStats && canCheckStats) {
                            int boolValueStatListCount = 0;
                            int floatValueStatListCount = 0;

                            for (int j = 0; j < currentElementOnScene.statInfoList.Count; j++) {
                                if (currentElementOnScene.statInfoList [j].statIsAmount) {
                                    currentElementOnScene.statInfoList [j].currentFloatValue = currentPersistanceElementsOnSceneInfo.floatValueStatList [floatValueStatListCount];

                                    floatValueStatListCount++;
                                } else {
                                    currentElementOnScene.statInfoList [j].currentBoolState = currentPersistanceElementsOnSceneInfo.boolValueStatList [boolValueStatListCount];

                                    boolValueStatListCount++;
                                }
                            }

                            currentElementOnScene.setStatsOnLoad ();

                            return;
                        }
                    }
                }
            }
        }
    }

    public override void deleteGameInfo (int saveNumberToDelete, int playerID, string currentSaveDataPath, bool showDebugInfo)
    {
        getMainManager ();

        if (mainElementOnSceneManager == null) {
            return;
        }

        if (!mainElementOnSceneManager.isSaveCurrentPlayerElementsOnSceneToSaveFile ()) {
            initializeValues ();

            return;
        }

        if (showDebugInfo) {
            print ("\n\n");

            print ("Deleting elements on scene");
        }

        List<persistanceElementsOnSceneBySaveSlotInfo> infoListToLoad = new List<persistanceElementsOnSceneBySaveSlotInfo> ();

        persistanceElementsOnSceneBySaveSlotInfo newPersistancePlayerElementsOnSceneListBySaveSlotInfo = new persistanceElementsOnSceneBySaveSlotInfo ();

        bool saveLocated = false;
        bool playerLocated = false;

        int saveSlotIndex = -1;
        int listIndex = -1;

        SaveSlotListWrapper saveData = new SaveSlotListWrapper ();

        if (getFileContent (currentSaveDataPath, out saveData, showDebugInfo)) {
            infoListToLoad = saveData.infoList;
        }

        int infoListToLoadCount = infoListToLoad.Count;

        if (showDebugInfo) {
            print ("info list to load amount " + infoListToLoadCount);
        }

        for (int j = 0; j < infoListToLoadCount; j++) {
            if (showDebugInfo) {
                print ("checking save number " + infoListToLoad [j].saveNumber);
            }

            if (infoListToLoad [j].saveNumber == saveNumberToDelete) {
                newPersistancePlayerElementsOnSceneListBySaveSlotInfo = infoListToLoad [j];
                saveLocated = true;
                saveSlotIndex = j;
            }
        }

        if (saveLocated) {
            int playerElementsOnSceneListCount = newPersistancePlayerElementsOnSceneListBySaveSlotInfo.playerElementsOnSceneList.Count;

            for (int j = 0; j < playerElementsOnSceneListCount; j++) {

                if (newPersistancePlayerElementsOnSceneListBySaveSlotInfo.playerElementsOnSceneList [j].playerID == playerID) {
                    playerLocated = true;
                    listIndex = j;
                }
            }
        }

        if (showDebugInfo) {
            print (saveLocated + " " + playerLocated + " " + playerID + " " + saveNumberToDelete);
        }

        //if the save is located, check if the player id exists
        if (saveLocated) {
            //if player id exists, overwrite it
            if (playerLocated) {

                if (showDebugInfo) {
                    print ("number of elements " +
                        infoListToLoad [saveSlotIndex].playerElementsOnSceneList [listIndex].elementOnSceneList.Count);

                    print ("\n\n");

                    int elementOnSceneListCount = infoListToLoad [saveSlotIndex].playerElementsOnSceneList [listIndex].elementOnSceneList.Count;

                    for (int j = 0; j < elementOnSceneListCount; j++) {
                        persistanceElementOnSceneInfo currentPersistanceElementsOnSceneInfo =
                            infoListToLoad [saveSlotIndex].playerElementsOnSceneList [listIndex].elementOnSceneList [j];

                        showDetailedDebugInfo (currentPersistanceElementsOnSceneInfo);
                    }
                }

                infoListToLoad [saveSlotIndex].playerElementsOnSceneList [listIndex].elementOnSceneList.Clear ();

                saveData.infoList = infoListToLoad;

                string jsonToSave = JsonUtility.ToJson (saveData, true);

                saveFileContent (currentSaveDataPath, jsonToSave);
            }
        }
    }

    public override void copyGameInfo (int saveNumberToStoreDuplicate, int saveNumberToDuplicate,
        int playerID, string currentSaveDataPath, bool copyAllInfoWithoutChecks, bool showDebugInfo)
    {
        //showDebugInfo = true;

        if (showDebugInfo) {
            print ("\n\n");

            print ("sending signal to copy game info from one slot to another ");

            print ("copying from save number " + saveNumberToDuplicate + " to " + saveNumberToStoreDuplicate);

            print ("copyAllInfoWithoutChecks value " + copyAllInfoWithoutChecks);
        }

        getMainManager ();

        if (mainElementOnSceneManager == null) {
            return;
        }

        if (!mainElementOnSceneManager.isSaveCurrentPlayerElementsOnSceneToSaveFile ()) {
            initializeValues ();

            return;
        }

        List<persistanceElementsOnSceneBySaveSlotInfo> infoListToLoad = new List<persistanceElementsOnSceneBySaveSlotInfo> ();

        persistanceElementsOnSceneBySaveSlotInfo persistanceSaveInfoToDuplicate = new persistanceElementsOnSceneBySaveSlotInfo ();

        persistanceElementsOnSceneBySaveSlotInfo persistanceSaveInfoToStoreDuplicate = new persistanceElementsOnSceneBySaveSlotInfo ();

        int currentSceneIndex = menuPause.getCurrentActiveSceneIndex ();

        bool playerLocatedToDuplicate = false;
        bool playerLocatedToStoreDuplicate = false;

        int saveSlotIndexToDuplicate = -1;
        int saveSlotIndexToStoreDuplicate = -1;

        int listIndexToDuplicate = -1;
        int listIndexToStoreDuplicate = -1;


        bool saveToDuplicateFound = false;
        bool saveToStoreDuplicateFound = false;

        SaveSlotListWrapper saveData = new SaveSlotListWrapper ();

        if (getFileContent (currentSaveDataPath, out saveData, showDebugInfo)) {
            infoListToLoad = saveData.infoList;
        }

        int infoListToLoadCount = infoListToLoad.Count;

        if (showDebugInfo) {
            print ("info list to load amount " + infoListToLoadCount);
        }

        for (int j = 0; j < infoListToLoadCount; j++) {
            if (infoListToLoad [j].saveNumber == saveNumberToDuplicate) {
                persistanceSaveInfoToDuplicate = infoListToLoad [j];

                saveSlotIndexToDuplicate = j;

                saveToDuplicateFound = true;
            }

            if (infoListToLoad [j].saveNumber == saveNumberToStoreDuplicate) {
                persistanceSaveInfoToStoreDuplicate = infoListToLoad [j];

                saveSlotIndexToStoreDuplicate = j;

                saveToStoreDuplicateFound = true;
            }
        }

        if (saveToStoreDuplicateFound) {
            int playerElementsOnSceneListCount = persistanceSaveInfoToStoreDuplicate.playerElementsOnSceneList.Count;

            for (int j = 0; j < playerElementsOnSceneListCount; j++) {
                if (persistanceSaveInfoToStoreDuplicate.playerElementsOnSceneList [j].playerID == playerID) {
                    playerLocatedToStoreDuplicate = true;

                    listIndexToStoreDuplicate = j;
                }
            }
        }

        if (saveToDuplicateFound) {
            int playerElementsOnSceneListCount = persistanceSaveInfoToDuplicate.playerElementsOnSceneList.Count;

            for (int j = 0; j < playerElementsOnSceneListCount; j++) {
                if (persistanceSaveInfoToDuplicate.playerElementsOnSceneList [j].playerID == playerID) {
                    playerLocatedToDuplicate = true;

                    listIndexToDuplicate = j;
                }
            }
        }

        if (showDebugInfo) {
            print (saveToStoreDuplicateFound + " " + playerLocatedToStoreDuplicate + " " + playerID + " " + saveNumberToStoreDuplicate);

            print (saveToDuplicateFound + " " + playerLocatedToDuplicate + " " + playerID + " " + saveNumberToDuplicate);
        }

        //if the save is located, check if the player id exists
        if (saveToStoreDuplicateFound && saveToDuplicateFound) {
            //if player id exists, overwrite it
            if (playerLocatedToStoreDuplicate && playerLocatedToDuplicate) {

                List<persistanceElementOnSceneInfo> elementOnSceneListToStoreDuplicate =
                    infoListToLoad [saveSlotIndexToStoreDuplicate].playerElementsOnSceneList [listIndexToStoreDuplicate].elementOnSceneList;

                if (showDebugInfo) {
                    print ("number of elements on list to store duplicated " + elementOnSceneListToStoreDuplicate.Count);
                }

                List<persistanceElementOnSceneInfo> elementOnSceneListToDuplicate =
                    infoListToLoad [saveSlotIndexToDuplicate].playerElementsOnSceneList [listIndexToDuplicate].elementOnSceneList;

                if (showDebugInfo) {
                    print ("number of elements on list to duplicate " + elementOnSceneListToDuplicate.Count);
                }

                //if the option to copy the list from one save slot to another is active, just override the list
                //of the one to store with the list to duplicate

                //this is used when loading the game, to copy the info from the loaded slot to the temporal save used
                //to auto save and load in between scenes
                if (copyAllInfoWithoutChecks) {
                    if (showDebugInfo) {
                        int elementOnSceneListToDuplicateCount = elementOnSceneListToDuplicate.Count;

                        print ("\n\n");
                        print ("FINAL LIST COUNT " + elementOnSceneListToDuplicateCount);
                        print ("\n\n");

                        for (int j = 0; j < elementOnSceneListToDuplicateCount; j++) {
                            persistanceElementOnSceneInfo currentPersistanceElementsOnSceneInfo =
                                elementOnSceneListToDuplicate [j];

                            showDetailedDebugInfo (currentPersistanceElementsOnSceneInfo);
                        }

                        print ("\n\n");
                        print ("save number where duplicate is stored " + infoListToLoad [saveSlotIndexToStoreDuplicate].saveNumber);
                    }

                    infoListToLoad [saveSlotIndexToStoreDuplicate].playerElementsOnSceneList [listIndexToStoreDuplicate].elementOnSceneList =
                        elementOnSceneListToDuplicate;
                } else {
                    List<persistanceElementOnSceneInfo> newList = new List<persistanceElementOnSceneInfo> ();


                    int elementOnSceneListToStoreDuplicateCount = elementOnSceneListToStoreDuplicate.Count;

                    //for (int j = 0; j < elementOnSceneListToStoreDuplicateCount; j++) {
                    //    persistanceElementOnSceneInfo currentPersistanceElementsOnSceneInfo =
                    //        elementOnSceneListToStoreDuplicate [j];

                    //    showDetailedDebugInfo (currentPersistanceElementsOnSceneInfo);
                    //}

                    int elementOnSceneListToDuplicateCount = elementOnSceneListToDuplicate.Count;

                    for (int j = 0; j < elementOnSceneListToDuplicateCount; j++) {
                        persistanceElementOnSceneInfo currentPersistanceElementsOnSceneInfo =
                            elementOnSceneListToDuplicate [j];

                        //showDetailedDebugInfo (currentPersistanceElementsOnSceneInfo);

                        if (currentPersistanceElementsOnSceneInfo.elementScene != currentSceneIndex) {
                            newList.Add (currentPersistanceElementsOnSceneInfo);
                        }
                    }

                    if (showDebugInfo) {
                        print ("\n\n");

                        print ("new list to add count " + newList.Count);
                    }

                    //we have now the list of elements stored from other scenes that are not the current scene
                    //where the manual save has been made, so from those, we need to compare that list
                    //with the list of elements from the current save to store, and remove the elements
                    //from the save to store which match on the same scene and id of the new list to add
                    //that avoids duplicates and adds the elements info up to date

                    //if the new list is empty, it means it has no info about elements, so the copy is cancelled

                    if (newList.Count > 0) {
                        List<persistanceElementOnSceneInfo> matchingItems =
                                elementOnSceneListToStoreDuplicate.Where (item1 =>
                        newList.Any (item2 => item1.elementScene == item2.elementScene &&
                        item1.elementID == item2.elementID)).ToList ();

                        elementOnSceneListToStoreDuplicate = elementOnSceneListToStoreDuplicate.Except (matchingItems).ToList ();

                        elementOnSceneListToStoreDuplicate.AddRange (newList);

                        elementOnSceneListToStoreDuplicateCount = elementOnSceneListToStoreDuplicate.Count;

                        if (showDebugInfo) {
                            print ("\n\n");
                            print ("FINAL LIST COUNT " + elementOnSceneListToStoreDuplicateCount);
                            print ("\n\n");

                            for (int j = 0; j < elementOnSceneListToStoreDuplicateCount; j++) {
                                persistanceElementOnSceneInfo currentPersistanceElementsOnSceneInfo =
                                    elementOnSceneListToStoreDuplicate [j];

                                showDetailedDebugInfo (currentPersistanceElementsOnSceneInfo);
                            }
                        }

                        infoListToLoad [saveSlotIndexToStoreDuplicate].playerElementsOnSceneList [listIndexToStoreDuplicate].elementOnSceneList = elementOnSceneListToStoreDuplicate;
                    }
                }

                saveData.infoList = infoListToLoad;

                string jsonToSave = JsonUtility.ToJson (saveData, true);

                saveFileContent (currentSaveDataPath, jsonToSave);
            }
        } else {
            if (saveToDuplicateFound) {
                if (copyAllInfoWithoutChecks) {
                    //if the option to copy all the list is active and only the list to copy was found, 
                    //add the info for the list to store as new


                    persistanceElementsOnSceneBySaveSlotInfo newPersistancePlayerElementsOnSceneListBySaveSlotInfo
                        = new persistanceElementsOnSceneBySaveSlotInfo ();

                    newPersistancePlayerElementsOnSceneListBySaveSlotInfo.saveNumber = saveNumberToStoreDuplicate;

                    persistancePlayerElementOnSceneInfo newPersistancePlayerElementOnSceneInfo = new persistancePlayerElementOnSceneInfo ();

                    newPersistancePlayerElementOnSceneInfo.playerID = playerID;

                    newPersistancePlayerElementOnSceneInfo.elementOnSceneList =
                        infoListToLoad [saveSlotIndexToDuplicate].playerElementsOnSceneList [listIndexToDuplicate].elementOnSceneList;

                    newPersistancePlayerElementsOnSceneListBySaveSlotInfo.playerElementsOnSceneList.Add (newPersistancePlayerElementOnSceneInfo);

                    infoListToLoad.Add (newPersistancePlayerElementsOnSceneListBySaveSlotInfo);

                    if (showDebugInfo) {
                        List<persistanceElementOnSceneInfo> elementOnSceneListToDuplicate =
                            newPersistancePlayerElementOnSceneInfo.elementOnSceneList;

                        int elementOnSceneListToDuplicateCount = elementOnSceneListToDuplicate.Count;

                        print ("\n\n");
                        print ("FINAL LIST COUNT " + elementOnSceneListToDuplicateCount);
                        print ("\n\n");

                        for (int j = 0; j < elementOnSceneListToDuplicateCount; j++) {
                            persistanceElementOnSceneInfo currentPersistanceElementsOnSceneInfo =
                                elementOnSceneListToDuplicate [j];

                            showDetailedDebugInfo (currentPersistanceElementsOnSceneInfo);
                        }

                        print ("\n\n");
                        print ("save number where duplicate is stored " + newPersistancePlayerElementsOnSceneListBySaveSlotInfo.saveNumber);

                    }

                    saveData.infoList = infoListToLoad;

                    string jsonToSave = JsonUtility.ToJson (saveData, true);

                    saveFileContent (currentSaveDataPath, jsonToSave);
                }
            }
        }
    }

    public override void showSaveInfoDebug (int saveNumberToShow, int playerID, string currentSaveDataPath,
       bool showAllGameInfo, bool showDebugInfo)
    {
        getMainManager ();

        if (mainElementOnSceneManager == null) {
            return;
        }

        if (!mainElementOnSceneManager.isSaveCurrentPlayerElementsOnSceneToSaveFile ()) {
            initializeValues ();

            return;
        }

        print ("\n\n");

        List<persistanceElementsOnSceneBySaveSlotInfo> infoListToLoad = new List<persistanceElementsOnSceneBySaveSlotInfo> ();

        persistanceElementsOnSceneBySaveSlotInfo newPersistancePlayerElementsOnSceneListBySaveSlotInfo = new persistanceElementsOnSceneBySaveSlotInfo ();

        bool saveLocated = false;
        bool playerLocated = false;

        int saveSlotIndex = -1;
        int listIndex = -1;

        SaveSlotListWrapper saveData = new SaveSlotListWrapper ();

        if (getFileContent (currentSaveDataPath, out saveData, showDebugInfo)) {
            infoListToLoad = saveData.infoList;
        }

        int infoListToLoadCount = infoListToLoad.Count;

        print ("ELEMENTS ON SCENE SAVE INFO");

        print ("Info List To Load amount " + infoListToLoadCount);

        print ("\n\n");

        if (showAllGameInfo) {
            for (int i = 0; i < infoListToLoadCount; i++) {

                newPersistancePlayerElementsOnSceneListBySaveSlotInfo = infoListToLoad [i];

                print ("#####################################################################\n\n");

                print ("SAVE NUMBER " + newPersistancePlayerElementsOnSceneListBySaveSlotInfo.saveNumber);

                int playerElementsOnSceneList =
                    newPersistancePlayerElementsOnSceneListBySaveSlotInfo.playerElementsOnSceneList.Count;

                for (int j = 0; j < playerElementsOnSceneList; j++) {
                    persistancePlayerElementOnSceneInfo currentPersistancePlayerElementOnSceneInfo =
                        newPersistancePlayerElementsOnSceneListBySaveSlotInfo.playerElementsOnSceneList [j];

                    print ("Player ID " + currentPersistancePlayerElementOnSceneInfo.playerID);

                    int elementOnSceneListCount = currentPersistancePlayerElementOnSceneInfo.elementOnSceneList.Count;

                    print ("Number of Elements on Scene " + elementOnSceneListCount);

                    print ("\n\n");

                    for (int k = 0; k < elementOnSceneListCount; k++) {
                        persistanceElementOnSceneInfo currentPersistanceElementsOnSceneInfo =
                            currentPersistancePlayerElementOnSceneInfo.elementOnSceneList [k];

                        showDetailedDebugInfo (currentPersistanceElementsOnSceneInfo);
                    }
                }

                print ("\n\n\n");
            }
        } else {
            for (int j = 0; j < infoListToLoadCount; j++) {
                print (infoListToLoad [j].saveNumber);

                if (infoListToLoad [j].saveNumber == saveNumberToShow) {
                    newPersistancePlayerElementsOnSceneListBySaveSlotInfo = infoListToLoad [j];
                    saveLocated = true;
                    saveSlotIndex = j;
                }
            }

            if (saveLocated) {
                int playerElementsOnSceneListCount = newPersistancePlayerElementsOnSceneListBySaveSlotInfo.playerElementsOnSceneList.Count;

                print ("\n\n");

                for (int j = 0; j < playerElementsOnSceneListCount; j++) {

                    print (newPersistancePlayerElementsOnSceneListBySaveSlotInfo.playerElementsOnSceneList [j].playerID);

                    if (newPersistancePlayerElementsOnSceneListBySaveSlotInfo.playerElementsOnSceneList [j].playerID == playerID) {
                        playerLocated = true;
                        listIndex = j;
                    }
                }
            }

            print (saveLocated + " " + playerLocated + " " + playerID + " " + saveNumberToShow);

            //if the save is located, check if the player id exists
            if (saveLocated) {
                //if player id exists, overwrite it
                if (playerLocated) {
                    print ("#####################################################################\n\n");

                    print ("SAVE NUMBER " + infoListToLoad [saveSlotIndex].saveNumber);

                    print ("Number of Elements on Scene " + infoListToLoad [saveSlotIndex].playerElementsOnSceneList [listIndex].elementOnSceneList.Count);

                    print ("\n\n");

                    int elementOnSceneListCount = infoListToLoad [saveSlotIndex].playerElementsOnSceneList [listIndex].elementOnSceneList.Count;

                    for (int j = 0; j < elementOnSceneListCount; j++) {
                        persistanceElementOnSceneInfo currentPersistanceElementsOnSceneInfo =
                            infoListToLoad [saveSlotIndex].playerElementsOnSceneList [listIndex].elementOnSceneList [j];

                        showDetailedDebugInfo (currentPersistanceElementsOnSceneInfo);
                    }
                }
            }
        }
    }

    void showDetailedDebugInfo (persistanceElementOnSceneInfo currentPersistanceElementsOnSceneInfo)
    {
        print ("Element Scene " + currentPersistanceElementsOnSceneInfo.elementScene);
        print ("Element ID " + currentPersistanceElementsOnSceneInfo.elementID);
        print ("Element Active State " + currentPersistanceElementsOnSceneInfo.elementActiveState);

        print ("Element Save Position Values " + currentPersistanceElementsOnSceneInfo.savePositionValues);
        print ("Element Position X-Y-Z " + currentPersistanceElementsOnSceneInfo.positionX + " " +
        currentPersistanceElementsOnSceneInfo.positionY + " " +
        currentPersistanceElementsOnSceneInfo.positionZ);

        print ("Element Save Rotation Values " + currentPersistanceElementsOnSceneInfo.saveRotationValues);

        print ("Element Rotation X-Y-Z " + currentPersistanceElementsOnSceneInfo.rotationX + " " +
        currentPersistanceElementsOnSceneInfo.rotationY + " " +
        currentPersistanceElementsOnSceneInfo.rotationZ);

        print ("Element Prefab ID " + currentPersistanceElementsOnSceneInfo.elementPrefabID);

        print ("\n\n");
    }

    void writeDetailedDebugInfo (bool loadingFile, string currentSaveDataPath)
    {

    }

    [System.Serializable]
    public class SaveSlotListWrapper
    {
        public List<persistanceElementsOnSceneBySaveSlotInfo> infoList = new List<persistanceElementsOnSceneBySaveSlotInfo> ();
    }
}