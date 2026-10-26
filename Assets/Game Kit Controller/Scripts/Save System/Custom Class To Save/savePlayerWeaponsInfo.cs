using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;
using System;

public class savePlayerWeaponsInfo : saveGameInfo
{
    public playerWeaponsManager mainPlayerWeaponsManager;

    List<persistanceWeaponInfo> persistanceInfoList;


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
                        saveData.infoList = new List<persistanceWeaponListBySaveSlotInfo> ();
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
                    saveData.infoList = new List<persistanceWeaponListBySaveSlotInfo> ();
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
        if (mainPlayerWeaponsManager == null) {
            return;
        }

        if (!mainPlayerWeaponsManager.saveCurrentPlayerWeaponsToSaveFile) {
            return;
        }

        if (showDebugInfo) {
            print ("\n\n");

            print ("Saving player weapons");
        }

        bool saveLocated = false;
        bool playerLocated = false;

        int saveSlotIndex = -1;
        int listIndex = -1;

        persistanceWeaponListByPlayerInfo weaponsToSave = getPersistanceList (playerID, showDebugInfo);

        persistanceWeaponListBySaveSlotInfo newPersistanceWeaponListBySaveSlotInfo = new persistanceWeaponListBySaveSlotInfo ();

        List<persistanceWeaponListBySaveSlotInfo> infoListToSave = new List<persistanceWeaponListBySaveSlotInfo> ();

        SaveSlotListWrapper saveData = new SaveSlotListWrapper ();

        if (getFileContent (currentSaveDataPath, out saveData, showDebugInfo)) {
            infoListToSave = saveData.infoList;
        }

        int infoListToSaveCount = infoListToSave.Count;

        for (int j = 0; j < infoListToSaveCount; j++) {
            if (infoListToSave [j].saveNumber == currentSaveNumber) {
                newPersistanceWeaponListBySaveSlotInfo = infoListToSave [j];
                saveLocated = true;
                saveSlotIndex = j;
            }
        }

        if (saveLocated) {
            int playerWeaponListCount = newPersistanceWeaponListBySaveSlotInfo.playerWeaponList.Count;

            for (int j = 0; j < playerWeaponListCount; j++) {
                if (newPersistanceWeaponListBySaveSlotInfo.playerWeaponList [j].playerID == weaponsToSave.playerID) {
                    playerLocated = true;
                    listIndex = j;
                }
            }
        }

        if (showDebugInfo) {
            print ("\n\n");

            print ("Number of weapons: " + weaponsToSave.weaponList.Count);

            print ("Current Save Number " + currentSaveNumber);
            print ("Save Located " + saveLocated);
            print ("Player Located " + playerLocated);
        }

        //if the save is located, check if the player id exists
        if (saveLocated) {
            //if player id exists, overwrite it
            if (playerLocated) {
                infoListToSave [saveSlotIndex].playerWeaponList [listIndex].weaponList = weaponsToSave.weaponList;
            } else {
                infoListToSave [saveSlotIndex].playerWeaponList.Add (weaponsToSave);
            }
        } else {
            newPersistanceWeaponListBySaveSlotInfo.saveNumber = currentSaveNumber;
            newPersistanceWeaponListBySaveSlotInfo.playerWeaponList.Add (weaponsToSave);
            infoListToSave.Add (newPersistanceWeaponListBySaveSlotInfo);
        }

        saveData.infoList = infoListToSave;

        string jsonToSave = JsonUtility.ToJson (saveData, true);

        saveFileContent (currentSaveDataPath, jsonToSave);
    }

    public void loadGameContent (int saveNumberToLoad, int playerID, string currentSaveDataPath, bool showDebugInfo)
    {
        if (mainPlayerWeaponsManager == null) {
            return;
        }

        if (!mainPlayerWeaponsManager.loadWeaponAttachmentsInfoFromSaveFile) {
            initializeValues ();

            return;
        }

        if (showDebugInfo) {
            print ("\n\n");

            print ("Loading player weapons");
        }

        persistanceInfoList = new List<persistanceWeaponInfo> ();

        List<persistanceWeaponListBySaveSlotInfo> infoListToLoad = new List<persistanceWeaponListBySaveSlotInfo> ();

        SaveSlotListWrapper saveData = new SaveSlotListWrapper ();

        if (getFileContent (currentSaveDataPath, out saveData, showDebugInfo)) {
            infoListToLoad = saveData.infoList;
        }

        if (saveNumberToLoad > -1) {
            persistanceWeaponListBySaveSlotInfo newPersistanceWeaponListBySaveSlotInfo = new persistanceWeaponListBySaveSlotInfo ();

            int infoListToLoadCount = infoListToLoad.Count;

            for (int j = 0; j < infoListToLoadCount; j++) {
                if (infoListToLoad [j].saveNumber == saveNumberToLoad) {
                    newPersistanceWeaponListBySaveSlotInfo = infoListToLoad [j];
                }
            }

            int listIndex = -1;

            int playerWeaponListCount = newPersistanceWeaponListBySaveSlotInfo.playerWeaponList.Count;

            for (int j = 0; j < playerWeaponListCount; j++) {
                if (newPersistanceWeaponListBySaveSlotInfo.playerWeaponList [j].playerID == playerID) {
                    listIndex = j;
                }
            }

            if (listIndex > -1) {
                persistanceInfoList = newPersistanceWeaponListBySaveSlotInfo.playerWeaponList [listIndex].weaponList;
            }
        }

        if (showDebugInfo) {
            print ("\n\n");

            print ("Weapons Loaded in Save Number " + saveNumberToLoad);
            print ("Weapons amount: " + persistanceInfoList.Count);

            for (int j = 0; j < persistanceInfoList.Count; j++) {
                print ("Weapon Name: " + persistanceInfoList [j].Name + " Is Enabled: " + persistanceInfoList [j].isWeaponEnabled +
                "Is Current Weapon: " + persistanceInfoList [j].isCurrentWeapon + " Remain Ammo: " + persistanceInfoList [j].remainingAmmo);
            }
        }

        loadInfoOnMainComponent ();
    }


    public persistanceWeaponListByPlayerInfo getPersistanceList (int playerID, bool showDebugInfo)
    {
        persistanceWeaponListByPlayerInfo newPersistanceWeaponListByPlayerInfo = new persistanceWeaponListByPlayerInfo ();

        newPersistanceWeaponListByPlayerInfo.playerID = playerID;

        List<persistanceWeaponInfo> newPersistanceWeaponInfoList = new List<persistanceWeaponInfo> ();

        List<IKWeaponSystem> weaponsList = mainPlayerWeaponsManager.weaponsList;

        int weaponsListCount = weaponsList.Count;

        for (int k = 0; k < weaponsListCount; k++) {
            persistanceWeaponInfo newPersistanceWeaponInfo = new persistanceWeaponInfo ();

            IKWeaponSystem currentWeapon = weaponsList [k];

            newPersistanceWeaponInfo.Name = currentWeapon.getWeaponSystemName ();
            newPersistanceWeaponInfo.index = k;
            newPersistanceWeaponInfo.isWeaponEnabled = currentWeapon.isWeaponEnabled ();
            newPersistanceWeaponInfo.isCurrentWeapon = currentWeapon.isCurrentWeapon ();
            newPersistanceWeaponInfo.remainingAmmo = currentWeapon.getWeaponSystemManager ().weaponSettings.remainAmmo + currentWeapon.getWeaponSystemManager ().getWeaponClipSize ();

            if (mainPlayerWeaponsManager.saveWeaponAttachmentsInfoToSaveFile) {
                if (currentWeapon.checkIfWeaponUsesAttachment ()) {
                    newPersistanceWeaponInfo.weaponUsesAttachment = true;

                    weaponAttachmentSystem currentWeaponAttachmentSystem = currentWeapon.getWeaponAttachmentSystem ();

                    if (currentWeaponAttachmentSystem != null) {
                        newPersistanceWeaponInfo.weaponAttachmentPlaceList = currentWeaponAttachmentSystem.getPersistanceAttachmentWeaponInfoList ();
                    }
                }

                newPersistanceWeaponInfoList.Add (newPersistanceWeaponInfo);
            }
        }

        newPersistanceWeaponListByPlayerInfo.weaponList = newPersistanceWeaponInfoList;

        return newPersistanceWeaponListByPlayerInfo;
    }


    void loadInfoOnMainComponent ()
    {
        initializeValues ();

        if (persistanceInfoList != null && persistanceInfoList.Count > 0) {

            List<IKWeaponSystem> weaponsList = mainPlayerWeaponsManager.weaponsList;

            if (mainPlayerWeaponsManager.storePickedWeaponsOnInventory) {

                if (mainPlayerWeaponsManager.loadWeaponAttachmentsInfoFromSaveFile) {
                    if (mainPlayerWeaponsManager.loadCurrentPlayerWeaponsFromSaveFile) {

                        int persistanceInfoListCount = persistanceInfoList.Count;

                        for (int i = 0; i < persistanceInfoListCount; i++) {

                            persistanceWeaponInfo currentPersistanceWeaponInfo = persistanceInfoList [i];

                            int currentIndex = currentPersistanceWeaponInfo.index;

                            if (weaponsList.Count > currentIndex && currentIndex > -1) {

                                IKWeaponSystem currentIKWeaponToCheck = weaponsList [currentIndex];

                                if (currentPersistanceWeaponInfo.weaponUsesAttachment) {
                                    weaponAttachmentSystem currentWeaponAttachmentSystem = currentIKWeaponToCheck.getWeaponAttachmentSystem ();

                                    if (currentWeaponAttachmentSystem != null) {
                                        currentWeaponAttachmentSystem.initializeAttachmentValues (currentPersistanceWeaponInfo.weaponAttachmentPlaceList);
                                    }
                                }
                            }
                        }
                    }
                }

                return;
            }

            if (mainPlayerWeaponsManager.loadCurrentPlayerWeaponsFromSaveFile) {
                bool thereIsCurrentWeapon = false;

                int persistanceInfoListCount = persistanceInfoList.Count;

                for (int i = 0; i < persistanceInfoListCount; i++) {
                    persistanceWeaponInfo currentPersistanceWeaponInfo = persistanceInfoList [i];

                    int currentIndex = currentPersistanceWeaponInfo.index;

                    if (weaponsList.Count > currentIndex && currentIndex > -1) {

                        IKWeaponSystem currentIKWeaponToCheck = weaponsList [currentIndex];

                        currentIKWeaponToCheck.setWeaponEnabledState (currentPersistanceWeaponInfo.isWeaponEnabled);
                        currentIKWeaponToCheck.setCurrentWeaponState (currentPersistanceWeaponInfo.isCurrentWeapon);
                        currentIKWeaponToCheck.weaponSystemManager.weaponSettings.remainAmmo = currentPersistanceWeaponInfo.remainingAmmo
                        - currentIKWeaponToCheck.weaponSystemManager.getWeaponClipSize ();

                        if (currentIKWeaponToCheck.isCurrentWeapon ()) {
                            thereIsCurrentWeapon = true;
                        }

                        if (mainPlayerWeaponsManager.loadWeaponAttachmentsInfoFromSaveFile) {
                            if (currentPersistanceWeaponInfo.weaponUsesAttachment) {
                                weaponAttachmentSystem currentWeaponAttachmentSystem = currentIKWeaponToCheck.getWeaponAttachmentSystem ();

                                if (currentWeaponAttachmentSystem != null) {
                                    currentWeaponAttachmentSystem.initializeAttachmentValues (currentPersistanceWeaponInfo.weaponAttachmentPlaceList);
                                }
                            }
                        }
                    }
                }

                if (!thereIsCurrentWeapon) {
                    int weaponsListCount = weaponsList.Count;

                    for (int k = 0; k < weaponsListCount; k++) {
                        IKWeaponSystem currentIKWeaponToCheck = weaponsList [k];

                        if (!thereIsCurrentWeapon && currentIKWeaponToCheck.isWeaponEnabled ()) {
                            currentIKWeaponToCheck.setCurrentWeaponState (true);
                            thereIsCurrentWeapon = true;
                        }
                    }
                }
            }
        }
    }

    void initializeValues ()
    {
        mainPlayerWeaponsManager.initializePlayerWeaponsValues ();
    }

    [System.Serializable]
    public class SaveSlotListWrapper
    {
        public List<persistanceWeaponListBySaveSlotInfo> infoList = new List<persistanceWeaponListBySaveSlotInfo> ();
    }
}
