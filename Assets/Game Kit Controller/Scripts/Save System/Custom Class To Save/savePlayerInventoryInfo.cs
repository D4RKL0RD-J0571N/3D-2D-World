using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;
using System;

public class savePlayerInventoryInfo : saveGameInfo
{
    [Space]
    [Header ("Other Options")]
    [Space]

    public bool ignoreAssignLoadedInventoryIfEmpty;

    [Space]
    [Header ("Debug")]
    [Space]

    public List<inventoryListElement> persistanceInfoList;

    [Space]
    [Header ("Components")]
    [Space]

    public inventoryManagerInfo mainInventoryManager;

    bool lastGameSavedOnHomeMenuValue;


    public override void saveGame (int saveNumber, int playerID, string currentSaveDataPath, bool showDebugInfo, bool savingGameToChangeScene)
    {
        saveGameContent (saveNumber, playerID, currentSaveDataPath, showDebugInfo, savingGameToChangeScene);
    }

    public override void loadGame (int saveNumberToLoad, int playerID, string currentSaveDataPath, bool showDebugInfo)
    {
        loadGameContent (saveNumberToLoad, playerID, currentSaveDataPath, showDebugInfo);
    }

    public override void saveGameFromEditor (int saveNumber, int playerID, string currentSaveDataPath, bool showDebugInfo)
    {
        saveGameContentFromEditor (saveNumber, playerID, currentSaveDataPath, showDebugInfo);
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
                        saveData.infoList = new List<persistanceInventoryListBySaveSlotInfo> ();
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
                    saveData.infoList = new List<persistanceInventoryListBySaveSlotInfo> ();
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
        if (mainInventoryManager == null) {
            return;
        }

        if (!mainInventoryManager.isSaveCurrentPlayerInventoryToSaveFileActive ()) {
            return;
        }

        if (showDebugInfo) {
            print ("\n\n");

            print ("Saving player inventory");
        }

        bool saveLocated = false;
        bool playerLocated = false;

        int saveSlotIndex = -1;
        int listIndex = -1;

        persistanceInventoryListByPlayerInfo inventoryListToSave = getPersistanceList (playerID, showDebugInfo);

        persistanceInventoryListBySaveSlotInfo newPersistanceInventoryListBySaveSlotInfo = new persistanceInventoryListBySaveSlotInfo ();

        List<persistanceInventoryListBySaveSlotInfo> infoListToSave = new List<persistanceInventoryListBySaveSlotInfo> ();

        SaveSlotListWrapper saveData = new SaveSlotListWrapper ();

        if (getFileContent (currentSaveDataPath, out saveData, showDebugInfo)) {
            infoListToSave = saveData.infoList;
        }

        if (showDebugInfo) {
            print ("Number of save list for player inventory " + infoListToSave.Count);
        }

        int infoListToSaveCount = infoListToSave.Count;

        for (int j = 0; j < infoListToSaveCount; j++) {
            if (saveSlotIndex == -1) {
                persistanceInventoryListBySaveSlotInfo currentSlot = infoListToSave [j];

                if (currentSlot.saveNumber == currentSaveNumber) {
                    newPersistanceInventoryListBySaveSlotInfo = currentSlot;
                    saveLocated = true;
                    saveSlotIndex = j;
                }
            }
        }

        if (saveLocated) {
            int playerInventoryListCount = newPersistanceInventoryListBySaveSlotInfo.playerInventoryList.Count;

            int playerIDToSearch = inventoryListToSave.playerID;

            for (int j = 0; j < playerInventoryListCount; j++) {
                if (listIndex == -1) {
                    if (newPersistanceInventoryListBySaveSlotInfo.playerInventoryList [j].playerID == playerIDToSearch) {
                        playerLocated = true;
                        listIndex = j;
                    }
                }
            }
        }

        if (showDebugInfo) {
            print ("\n\n");

            print ("EXTRA INFO\n");
            print ("PLAYER INVENTORY");
            print ("Number of objects: " + inventoryListToSave.inventoryObjectList.Count);
            print ("Current Save Number " + currentSaveNumber);
            print ("Save Located " + saveLocated);
            print ("Player Located " + playerLocated);
            print ("Player ID " + inventoryListToSave.playerID);

            print ("Save List Index " + listIndex);
        }

        //if the save is located, check if the player id exists
        if (saveLocated) {
            //if player id exists, overwrite it
            if (playerLocated) {
                //				infoListToSave [saveSlotIndex].playerInventoryList [listIndex].inventoryObjectList = inventoryListToSave.inventoryObjectList;

                infoListToSave [saveSlotIndex].playerInventoryList [listIndex] = inventoryListToSave;
            } else {
                infoListToSave [saveSlotIndex].playerInventoryList.Add (inventoryListToSave);
            }
        } else {
            newPersistanceInventoryListBySaveSlotInfo.saveNumber = currentSaveNumber;
            newPersistanceInventoryListBySaveSlotInfo.gameSavedOnHomeMenu = gameSavedOnHomeMenu;
            newPersistanceInventoryListBySaveSlotInfo.playerInventoryList.Add (inventoryListToSave);
            infoListToSave.Add (newPersistanceInventoryListBySaveSlotInfo);

            if (showDebugInfo) {
                if (gameSavedOnHomeMenu) {
                    print ("Game saved on home menu");

                    print ("\n\n");
                }
            }
        }

        saveData.infoList = infoListToSave;

        string jsonToSave = JsonUtility.ToJson (saveData, true);

        saveFileContent (currentSaveDataPath, jsonToSave);
    }

    public void loadGameContent (int saveNumberToLoad, int playerID, string currentSaveDataPath, bool showDebugInfo)
    {
        if (mainInventoryManager == null) {
            return;
        }

        if (!mainInventoryManager.isLoadCurrentPlayerInventoryFromSaveFileActive ()) {
            initializeValues ();

            return;
        }

        if (showDebugInfo) {
            print ("\n\n");

            print ("Loading player inventory with PLAYER ID " + playerID + " and saveNumberToLoad " + saveNumberToLoad);
        }

        //need to store and check the current slot saved and the player which is saving, to get that concrete info
        persistanceInfoList = new List<inventoryListElement> ();

        List<persistanceInventoryListBySaveSlotInfo> infoListToLoad = new List<persistanceInventoryListBySaveSlotInfo> ();

        SaveSlotListWrapper saveData = new SaveSlotListWrapper ();

        if (getFileContent (currentSaveDataPath, out saveData, showDebugInfo)) {
            infoListToLoad = saveData.infoList;
        }

        lastGameSavedOnHomeMenuValue = false;

        if (saveNumberToLoad > -1) {

            if (showDebugInfo) {
                print (infoListToLoad.Count);
            }

            bool removeSaveInfo = false;

            persistanceInventoryListBySaveSlotInfo newPersistanceInventoryListBySaveSlotInfo = new persistanceInventoryListBySaveSlotInfo ();

            int infoListToLoadCount = infoListToLoad.Count;

            bool slotLocated = false;

            for (int j = 0; j < infoListToLoadCount; j++) {
                if (!slotLocated) {
                    persistanceInventoryListBySaveSlotInfo currentSlot = infoListToLoad [j];

                    bool checkSlot = false;

                    // if (ignoreSaveNumberOnSaveLoadInfo) {
                    // checkSlot = true;
                    // }

                    if (saveNumberToIgnoreSaveLoadInfo == saveNumberToLoad && saveNumberToIgnoreSaveLoadInfo > -1 && currentSlot.gameSavedOnHomeMenu) {
                        removeSaveInfo = true;

                        checkSlot = true;
                    }

                    if (currentSlot.saveNumber == saveNumberToLoad) {
                        checkSlot = true;
                    }

                    if (checkSlot) {
                        newPersistanceInventoryListBySaveSlotInfo = currentSlot;

                        slotLocated = true;
                    }
                }
            }

            //print ("number " + newPersistanceInventoryListBySaveSlotInfo.playerInventoryList.Count);

            int listIndex = -1;

            int playerInventoryListCount = newPersistanceInventoryListBySaveSlotInfo.playerInventoryList.Count;

            for (int j = 0; j < playerInventoryListCount; j++) {
                //				print (newPersistanceInventoryListBySaveSlotInfo.playerInventoryList [j].playerID + " " +
                //				newPersistanceInventoryListBySaveSlotInfo.playerInventoryList [j].inventorySlotAmount);
                //				
                if (listIndex == -1) {
                    bool checkSlot = false;

                    // if (ignorePlayerIDOnSaveLoadInfo) {
                    // checkSlot = true;
                    // }

                    if (saveNumberToIgnoreSaveLoadInfo == saveNumberToLoad && saveNumberToIgnoreSaveLoadInfo > -1) {
                        checkSlot = true;
                    }

                    if (newPersistanceInventoryListBySaveSlotInfo.playerInventoryList [j].playerID == playerID) {
                        checkSlot = true;
                    }

                    if (checkSlot) {
                        listIndex = j;
                    }
                }
            }

            //print ("index " + listIndex);

            if (listIndex > -1) {
                lastGameSavedOnHomeMenuValue = newPersistanceInventoryListBySaveSlotInfo.gameSavedOnHomeMenu;

                persistanceInventoryListByPlayerInfo playerInventoryListToLoad = newPersistanceInventoryListBySaveSlotInfo.playerInventoryList [listIndex];

                int inventoryObjectListCount = playerInventoryListToLoad.inventoryObjectList.Count;

                for (int j = 0; j < inventoryObjectListCount; j++) {
                    inventoryListElement newInventoryListElement = new inventoryListElement ();

                    persistanceInventoryObjectInfo currentInventoryInfo = playerInventoryListToLoad.inventoryObjectList [j];

                    newInventoryListElement.Name = currentInventoryInfo.Name;
                    newInventoryListElement.amount = currentInventoryInfo.amount;
                    newInventoryListElement.infiniteAmount = currentInventoryInfo.infiniteAmount;
                    newInventoryListElement.inventoryObjectName = currentInventoryInfo.inventoryObjectName;

                    newInventoryListElement.categoryIndex = currentInventoryInfo.categoryIndex;
                    newInventoryListElement.elementIndex = currentInventoryInfo.elementIndex;

                    newInventoryListElement.isEquipped = currentInventoryInfo.isEquipped;

                    newInventoryListElement.quickAccessSlotIndex = currentInventoryInfo.quickAccessSlotIndex;

                    newInventoryListElement.useDurability = currentInventoryInfo.useDurability;
                    newInventoryListElement.durabilityAmount = currentInventoryInfo.durabilityAmount;
                    newInventoryListElement.maxDurabilityAmount = currentInventoryInfo.maxDurabilityAmount;
                    newInventoryListElement.objectIsBroken = currentInventoryInfo.objectIsBroken;

                    newInventoryListElement.isWeapon = currentInventoryInfo.isWeapon;
                    newInventoryListElement.isMeleeWeapon = currentInventoryInfo.isMeleeWeapon;

                    newInventoryListElement.projectilesInMagazine = currentInventoryInfo.projectilesInMagazine;

                    persistanceInfoList.Add (newInventoryListElement);
                }

                int inventorySlotAmount = playerInventoryListToLoad.inventorySlotAmount;

                if (inventorySlotAmount > -1) {
                    mainInventoryManager.setInventorySlotAmountValue (inventorySlotAmount);
                }

                mainInventoryManager.setInventoryWasEmptyWhenSavedState (playerInventoryListToLoad.inventoryWasEmptyWhenSaved);

                if (mainInventoryManager.isSetInfiniteSlotValuesOnSaveLoadActive ()) {
                    mainInventoryManager.setInfiniteSlotsState (playerInventoryListToLoad.infiniteSlots);
                }

                if (playerInventoryListToLoad.useOnlyBlueprintsUnlocked) {
                    GKC_Utils.setUseOnlyBlueprintsUnlockedState (mainInventoryManager.gameObject, true);

                    GKC_Utils.setBlueprintsUnlockedListValue (mainInventoryManager.gameObject, playerInventoryListToLoad.blueprintsUnlockedList);
                }

                if (playerInventoryListToLoad.anyObjectToCraftInTimeActive) {
                    GKC_Utils.setCraftObjectInTimeInfoList (mainInventoryManager.gameObject, playerInventoryListToLoad.craftObjectInTimeSimpleInfoList);
                }

                if (playerInventoryListToLoad.objectCategoriesToCraftAvailableAtAnyMoment != null && playerInventoryListToLoad.objectCategoriesToCraftAvailableAtAnyMoment.Count > 0) {
                    GKC_Utils.setObjectCategoriesToCraftAvailableAtAnyMomentValue (mainInventoryManager.gameObject, playerInventoryListToLoad.objectCategoriesToCraftAvailableAtAnyMoment);
                }

                mainInventoryManager.setLastWeaponCarriedOnHandsName (playerInventoryListToLoad.lastWeaponCarriedOnHandsName);


                if (showDebugInfo) {
                    print ("\n\n");
                    print ("Number of inventory slots available: " + playerInventoryListToLoad.inventorySlotAmount);
                    print ("\n\n");

                    if (playerInventoryListToLoad.useOnlyBlueprintsUnlocked) {
                        print ("\n\n");
                        print ("Use only blueprints unlocked active with the amount: " + playerInventoryListToLoad.blueprintsUnlockedList.Count);
                        print ("\n\n");
                    }
                }

                if (removeSaveInfo) {
                    infoListToLoad.Remove (newPersistanceInventoryListBySaveSlotInfo);

                    saveData = new SaveSlotListWrapper ();

                    saveData.infoList = infoListToLoad;

                    string jsonToSave = JsonUtility.ToJson (saveData, true);

                    saveFileContent (currentSaveDataPath, jsonToSave);

                    if (showDebugInfo) {
                        print ("REMOVE SLOT FROM INITIAL INVENTORY ON NEW GAME");
                    }
                }
            }
        }

        if (showDebugInfo) {
            print ("\n\n");

            print ("PLAYER INVENTORY");

            print ("Inventory Loaded in Save Number " + saveNumberToLoad);
            print ("Number of objects: " + persistanceInfoList.Count);

            for (int j = 0; j < persistanceInfoList.Count; j++) {
                inventoryListElement currentElement = persistanceInfoList [j];

                print ("Object Name: " + currentElement.Name + " Amount: " + currentElement.amount);

                if (currentElement.isWeapon && !currentElement.isMeleeWeapon) {
                    print ("Object is weapon with " + currentElement.projectilesInMagazine + " projectiles in the magazine");
                }
            }
        }

        loadInfoOnMainComponent ();
    }


    public persistanceInventoryListByPlayerInfo getPersistanceList (int playerID, bool showDebugInfo)
    {
        persistanceInventoryListByPlayerInfo newPersistanceInventoryListByPlayerInfo = new persistanceInventoryListByPlayerInfo ();

        newPersistanceInventoryListByPlayerInfo.playerID = playerID;

        mainInventoryManager.updateDurabilityAmountStateOnAllInventoryObjects ();

        int inventorySlotAmount = mainInventoryManager.getInventorySlotAmount ();

        newPersistanceInventoryListByPlayerInfo.inventorySlotAmount = inventorySlotAmount;

        newPersistanceInventoryListByPlayerInfo.infiniteSlots = mainInventoryManager.isInfiniteSlotsActive ();


        //Crafting elements
        newPersistanceInventoryListByPlayerInfo.useOnlyBlueprintsUnlocked = GKC_Utils.isUseOnlyBlueprintsUnlockedActive (mainInventoryManager.gameObject);

        if (newPersistanceInventoryListByPlayerInfo.useOnlyBlueprintsUnlocked) {

            List<string> blueprintsUnlockedList = GKC_Utils.getBlueprintsUnlockedListValue (mainInventoryManager.gameObject);

            if (blueprintsUnlockedList != null) {
                newPersistanceInventoryListByPlayerInfo.blueprintsUnlockedList = new List<string> (blueprintsUnlockedList);
            }
        }

        newPersistanceInventoryListByPlayerInfo.anyObjectToCraftInTimeActive = GKC_Utils.anyObjectToCraftInTimeActive (mainInventoryManager.gameObject);

        if (newPersistanceInventoryListByPlayerInfo.anyObjectToCraftInTimeActive) {
            newPersistanceInventoryListByPlayerInfo.craftObjectInTimeSimpleInfoList = GKC_Utils.getCraftObjectInTimeInfoList (mainInventoryManager.gameObject);
        }

        newPersistanceInventoryListByPlayerInfo.objectCategoriesToCraftAvailableAtAnyMoment = GKC_Utils.getObjectCategoriesToCraftAvailableAtAnyMomentValue (mainInventoryManager.gameObject);

        newPersistanceInventoryListByPlayerInfo.lastWeaponCarriedOnHandsName = mainInventoryManager.getLastWeaponCarriedOnHandsName ();

        if (showDebugInfo) {
            print ("\n\n");
            print ("Number of inventory slots available: " + inventorySlotAmount);
            print ("\n\n");

            if (newPersistanceInventoryListByPlayerInfo.useOnlyBlueprintsUnlocked) {
                print ("\n\n");
                print ("Use only blueprints unlocked active with the amount: " + newPersistanceInventoryListByPlayerInfo.blueprintsUnlockedList.Count);
                print ("\n\n");
            }
        }

        List<persistanceInventoryObjectInfo> newPersistanceInventoryObjectInfoList = new List<persistanceInventoryObjectInfo> ();

        List<inventoryInfo> inventoryList = mainInventoryManager.getInventoryList ();

        int inventoryListCount = inventoryList.Count;

        bool inventoryWasEmptyWhenSaved = true;

        for (int k = 0; k < inventoryListCount; k++) {
            inventoryInfo currentInventoryInfo = inventoryList [k];

            if (currentInventoryInfo.amount > 0) {
                persistanceInventoryObjectInfo newPersistanceInventoryObjectInfo = new persistanceInventoryObjectInfo ();

                newPersistanceInventoryObjectInfo.Name = currentInventoryInfo.Name;
                newPersistanceInventoryObjectInfo.amount = currentInventoryInfo.amount;
                newPersistanceInventoryObjectInfo.infiniteAmount = currentInventoryInfo.infiniteAmount;
                newPersistanceInventoryObjectInfo.inventoryObjectName = currentInventoryInfo.Name;

                newPersistanceInventoryObjectInfo.categoryIndex = currentInventoryInfo.categoryIndex;
                newPersistanceInventoryObjectInfo.elementIndex = currentInventoryInfo.elementIndex;

                newPersistanceInventoryObjectInfo.isEquipped = currentInventoryInfo.isEquipped;

                newPersistanceInventoryObjectInfo.quickAccessSlotIndex = currentInventoryInfo.quickAccessSlotIndex;

                newPersistanceInventoryObjectInfo.useDurability = currentInventoryInfo.useDurability;
                newPersistanceInventoryObjectInfo.durabilityAmount = currentInventoryInfo.durabilityAmount;
                newPersistanceInventoryObjectInfo.maxDurabilityAmount = currentInventoryInfo.maxDurabilityAmount;
                newPersistanceInventoryObjectInfo.objectIsBroken = currentInventoryInfo.objectIsBroken;

                newPersistanceInventoryObjectInfo.isWeapon = currentInventoryInfo.isWeapon;
                newPersistanceInventoryObjectInfo.isMeleeWeapon = currentInventoryInfo.isMeleeWeapon;

                if (newPersistanceInventoryObjectInfo.isWeapon && !newPersistanceInventoryObjectInfo.isMeleeWeapon) {
                    if (currentInventoryInfo.mainWeaponObjectInfo != null) {
                        newPersistanceInventoryObjectInfo.projectilesInMagazine = currentInventoryInfo.mainWeaponObjectInfo.getProjectilesInWeaponMagazine ();
                    }
                }

                newPersistanceInventoryObjectInfoList.Add (newPersistanceInventoryObjectInfo);

                inventoryWasEmptyWhenSaved = false;
            }
        }

        newPersistanceInventoryListByPlayerInfo.inventoryWasEmptyWhenSaved = inventoryWasEmptyWhenSaved;

        if (showDebugInfo) {
            print ("\n\n");

            for (int j = 0; j < newPersistanceInventoryObjectInfoList.Count; j++) {
                persistanceInventoryObjectInfo currentInfo = newPersistanceInventoryObjectInfoList [j];

                print ("Object Name: " + currentInfo.Name + " Amount: " + currentInfo.amount + " category index " +
                currentInfo.categoryIndex + " element index " + currentInfo.elementIndex);

                if (currentInfo.isWeapon && !currentInfo.isMeleeWeapon) {
                    print ("Object is weapon with " + currentInfo.projectilesInMagazine + " projectiles in the magazine");
                }
            }
        }

        newPersistanceInventoryListByPlayerInfo.inventoryObjectList = newPersistanceInventoryObjectInfoList;

        return newPersistanceInventoryListByPlayerInfo;
    }


    void loadInfoOnMainComponent ()
    {
        mainInventoryManager.setIsLoadingGameState (true);

        initializeValues ();

        if (persistanceInfoList != null) {
            bool sendNewInventoryResult = true;

            //when loading, it can be from a new game or from a continue/load action, so if the loaded inventory is an
            //empty list, then:
            //-when doing a new game, it should allow to use the initial inventory if configured
            //-when loading/continuing a game, it should load the inventory list saved, even if empty

            //check the state also when the player options menu is used on home menu

            if (ignoreAssignLoadedInventoryIfEmpty) {
                if (persistanceInfoList.Count == 0 && !mainInventoryManager.getInventoryWasEmptyWhenSaved ()) {
                    sendNewInventoryResult = false;
                }

                if (persistanceInfoList.Count == 0 && mainInventoryManager.getInventoryWasEmptyWhenSaved () && lastGameSavedOnHomeMenuValue) {
                    sendNewInventoryResult = false;
                }
            }

            //print (persistanceInfoList.Count + " " + mainInventoryManager.getInventoryWasEmptyWhenSaved () + " " + lastGameSavedOnHomeMenuValue + " " +
            //sendNewInventoryResult);

            if (sendNewInventoryResult) {
                mainInventoryManager.setNewInventoryListManagerList (persistanceInfoList);
            }
        }
    }

    void initializeValues ()
    {
        mainInventoryManager.initializeInventoryValues ();
    }


    public void saveGameContentFromEditor (int currentSaveNumber, int playerID, string currentSaveDataPath, bool showDebugInfo)
    {
        if (mainInventoryManager == null) {
            return;
        }

        bool saveLocated = false;
        bool playerLocated = false;

        int saveSlotIndex = -1;
        int listIndex = -1;

        persistanceInventoryListByPlayerInfo inventoryListToSave = getPersistanceInventoryListElement (playerID, showDebugInfo);

        persistanceInventoryListBySaveSlotInfo newPersistanceInventoryListBySaveSlotInfo = new persistanceInventoryListBySaveSlotInfo ();

        List<persistanceInventoryListBySaveSlotInfo> infoListToSave = new List<persistanceInventoryListBySaveSlotInfo> ();

        SaveSlotListWrapper saveData = new SaveSlotListWrapper ();

        if (getFileContent (currentSaveDataPath, out saveData, showDebugInfo)) {
            infoListToSave = saveData.infoList;
        }

        int infoListToSaveCount = infoListToSave.Count;

        for (int j = 0; j < infoListToSaveCount; j++) {
            if (saveSlotIndex == -1) {
                persistanceInventoryListBySaveSlotInfo currentSlot = infoListToSave [j];

                if (currentSlot.saveNumber == currentSaveNumber) {
                    newPersistanceInventoryListBySaveSlotInfo = currentSlot;
                    saveLocated = true;
                    saveSlotIndex = j;
                }
            }
        }

        if (saveLocated) {

            int playerInventoryListCount = newPersistanceInventoryListBySaveSlotInfo.playerInventoryList.Count;

            int playerIDToSearch = inventoryListToSave.playerID;

            for (int j = 0; j < playerInventoryListCount; j++) {
                if (listIndex == -1 && newPersistanceInventoryListBySaveSlotInfo.playerInventoryList [j].playerID == playerIDToSearch) {
                    playerLocated = true;
                    listIndex = j;
                }
            }
        }

        if (showDebugInfo) {
            print ("\n\n");

            print ("EXTRA INFO\n");
            print ("PLAYER INVENTORY");

            print ("Number of objects: " + inventoryListToSave.inventoryObjectList.Count);
            print ("Current Save Number " + currentSaveNumber);
            print ("Save Located " + saveLocated);
            print ("Player Located " + playerLocated);
            print ("Player ID " + inventoryListToSave.playerID);
        }

        //if the save is located, check if the player id exists
        if (saveLocated) {
            //if player id exists, overwrite it
            if (playerLocated) {
                infoListToSave [saveSlotIndex].playerInventoryList [listIndex].inventoryObjectList = inventoryListToSave.inventoryObjectList;
            } else {
                infoListToSave [saveSlotIndex].playerInventoryList.Add (inventoryListToSave);
            }
        } else {
            newPersistanceInventoryListBySaveSlotInfo.saveNumber = currentSaveNumber;
            newPersistanceInventoryListBySaveSlotInfo.playerInventoryList.Add (inventoryListToSave);
            infoListToSave.Add (newPersistanceInventoryListBySaveSlotInfo);
        }

        saveData.infoList = infoListToSave;

        string jsonToSave = JsonUtility.ToJson (saveData, true);

        saveFileContent (currentSaveDataPath, jsonToSave);
    }

    public persistanceInventoryListByPlayerInfo getPersistanceInventoryListElement (int playerID, bool showDebugInfo)
    {
        persistanceInventoryListByPlayerInfo newPersistanceInventoryListByPlayerInfo = new persistanceInventoryListByPlayerInfo ();

        newPersistanceInventoryListByPlayerInfo.playerID = playerID;

        List<persistanceInventoryObjectInfo> newPersistanceInventoryObjectInfoList = new List<persistanceInventoryObjectInfo> ();

        List<inventoryListElement> inventoryListManagerList = mainInventoryManager.getCurrentInventoryListManagerList ();

        int inventoryListManagerListCount = inventoryListManagerList.Count;

        for (int k = 0; k < inventoryListManagerListCount; k++) {
            persistanceInventoryObjectInfo newPersistanceInventoryObjectInfo = new persistanceInventoryObjectInfo ();

            inventoryListElement currentElement = inventoryListManagerList [k];

            newPersistanceInventoryObjectInfo.Name = currentElement.Name;
            newPersistanceInventoryObjectInfo.amount = currentElement.amount;
            newPersistanceInventoryObjectInfo.infiniteAmount = currentElement.infiniteAmount;
            newPersistanceInventoryObjectInfo.inventoryObjectName = currentElement.inventoryObjectName;

            newPersistanceInventoryObjectInfo.elementIndex = currentElement.elementIndex;
            newPersistanceInventoryObjectInfo.categoryIndex = currentElement.categoryIndex;

            if (showDebugInfo) {
                print (newPersistanceInventoryObjectInfo.Name + " " + newPersistanceInventoryObjectInfo.amount);
            }

            newPersistanceInventoryObjectInfoList.Add (newPersistanceInventoryObjectInfo);
        }

        newPersistanceInventoryListByPlayerInfo.inventoryObjectList = newPersistanceInventoryObjectInfoList;

        return newPersistanceInventoryListByPlayerInfo;
    }


    [System.Serializable]
    public class SaveSlotListWrapper
    {
        public List<persistanceInventoryListBySaveSlotInfo> infoList = new List<persistanceInventoryListBySaveSlotInfo> ();
    }
}