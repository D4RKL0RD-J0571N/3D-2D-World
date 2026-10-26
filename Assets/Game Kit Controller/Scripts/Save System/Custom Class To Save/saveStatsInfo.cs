using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;
using System;

public class saveStatsInfo : saveGameInfo
{
	public playerStatsSystem mainPlayerStatsSystem;

	List<persistanceStatInfo> persistanceInfoList;

	bool valuesInitializedOnLoad;


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
                        saveData.infoList = new List<persistancePlayerStatsListBySaveSlotInfo> ();
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
                    saveData.infoList = new List<persistancePlayerStatsListBySaveSlotInfo> ();
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

		if (mainPlayerStatsSystem == null) {
			return;
		}

		if (!mainPlayerStatsSystem.playerStatsActive) {
			return;
		}

		if (!mainPlayerStatsSystem.saveCurrentPlayerStatsToSaveFile) {
			return;
		}

		if (showDebugInfo) {
			print ("\n\n");

			print ("Saving stats");
		}

		bool saveLocated = false;
		bool playerLocated = false;

		int saveSlotIndex = -1;
		int listIndex = -1;

		persistancePlayerStatInfo statsToSave = getPersistanceList (playerID, showDebugInfo);

		persistancePlayerStatsListBySaveSlotInfo newPersistancePlayerStatsListBySaveSlotInfo = new persistancePlayerStatsListBySaveSlotInfo ();
	
		List<persistancePlayerStatsListBySaveSlotInfo> infoListToSave = new List<persistancePlayerStatsListBySaveSlotInfo> ();

        SaveSlotListWrapper saveData = new SaveSlotListWrapper ();

        if (getFileContent (currentSaveDataPath, out saveData, showDebugInfo)) {
            infoListToSave = saveData.infoList;
        }

        int infoListToSaveCount = infoListToSave.Count;

		for (int j = 0; j < infoListToSaveCount; j++) {
			if (infoListToSave [j].saveNumber == currentSaveNumber) {
				newPersistancePlayerStatsListBySaveSlotInfo = infoListToSave [j];
				saveLocated = true;
				saveSlotIndex = j;
			}
		}

		if (saveLocated) {
			int playerStatsListCount = newPersistancePlayerStatsListBySaveSlotInfo.playerStatsList.Count;

			for (int j = 0; j < playerStatsListCount; j++) {
				if (newPersistancePlayerStatsListBySaveSlotInfo.playerStatsList [j].playerID == statsToSave.playerID) {
					playerLocated = true;
					listIndex = j;
				}
			}
		}

		if (showDebugInfo) {
			print ("\n\n");

			print ("EXTRA INFO\n");
			print ("Number of stats: " + statsToSave.statsList.Count);
			print ("Current Save Number " + currentSaveNumber);
			print ("Save Located " + saveLocated);
			print ("Player Located " + playerLocated);
			print ("Player ID " + statsToSave.playerID);
		}

		//if the save is located, check if the player id exists
		if (saveLocated) {
			//if player id exists, overwrite it
			if (playerLocated) {
				infoListToSave [saveSlotIndex].playerStatsList [listIndex].statsList = statsToSave.statsList;
			} else {
				infoListToSave [saveSlotIndex].playerStatsList.Add (statsToSave);
			}
		} else {
			newPersistancePlayerStatsListBySaveSlotInfo.saveNumber = currentSaveNumber;
			newPersistancePlayerStatsListBySaveSlotInfo.playerStatsList.Add (statsToSave);
			infoListToSave.Add (newPersistancePlayerStatsListBySaveSlotInfo);
		}

        saveData.infoList = infoListToSave;

        string jsonToSave = JsonUtility.ToJson (saveData, true);

        saveFileContent (currentSaveDataPath, jsonToSave);
    }

	public void loadGameContent (int saveNumberToLoad, int playerID, string currentSaveDataPath, bool showDebugInfo)
	{
		getMainManager ();

		if (mainPlayerStatsSystem == null) {
			return;
		}

		if (!mainPlayerStatsSystem.playerStatsActive) {
			return;
		}

		if (!mainPlayerStatsSystem.saveCurrentPlayerStatsToSaveFile) {
			initializeValues ();

			return;
		}

		if (showDebugInfo) {
			print ("\n\n");

			print ("Loading stats");
		}

		//need to store and check the current slot saved and the player which is saving, to get that concrete info
		persistanceInfoList = new List<persistanceStatInfo> ();

		List<persistancePlayerStatsListBySaveSlotInfo> infoListToLoad = new List<persistancePlayerStatsListBySaveSlotInfo> ();

        SaveSlotListWrapper saveData = new SaveSlotListWrapper ();

        if (getFileContent (currentSaveDataPath, out saveData, showDebugInfo)) {
            infoListToLoad = saveData.infoList;
        }

        if (saveNumberToLoad > -1) {
			persistancePlayerStatsListBySaveSlotInfo newPersistancePlayerStatsListBySaveSlotInfo = new persistancePlayerStatsListBySaveSlotInfo ();

			int infoListToLoadCount = infoListToLoad.Count;

			for (int j = 0; j < infoListToLoadCount; j++) {

				if (infoListToLoad [j].saveNumber == saveNumberToLoad) {
					newPersistancePlayerStatsListBySaveSlotInfo = infoListToLoad [j];
				}
			}

			int listIndex = -1;

			int playerStatsListCount = newPersistancePlayerStatsListBySaveSlotInfo.playerStatsList.Count;

			for (int j = 0; j < playerStatsListCount; j++) {

				if (newPersistancePlayerStatsListBySaveSlotInfo.playerStatsList [j].playerID == playerID) {
					listIndex = j;
				}
			}

			if (listIndex > -1) {
				persistanceInfoList.AddRange (newPersistancePlayerStatsListBySaveSlotInfo.playerStatsList [listIndex].statsList);
			}
		}

		if (showDebugInfo) {
			print ("\n\n");

			print ("Stats Loaded in Save Number " + saveNumberToLoad);
			print ("Number of stats: " + persistanceInfoList.Count);
		}

		loadInfoOnMainComponent (showDebugInfo);
	}


	public persistancePlayerStatInfo getPersistanceList (int playerID, bool showDebugInfo)
	{
		persistancePlayerStatInfo newStatsList = new persistancePlayerStatInfo ();

		newStatsList.playerID = playerID;

		List<persistanceStatInfo> newPersistanceStatInfoList = new List<persistanceStatInfo> ();

		playerStatsSystem.statInfo currentStatInfo = null;

		List<playerStatsSystem.statInfo> statInfoList = mainPlayerStatsSystem.statInfoList;

		int statInfoListCount = statInfoList.Count;

		for (int k = 0; k < statInfoListCount; k++) {
			currentStatInfo = statInfoList [k];

			persistanceStatInfo newpersistanceStatInfo = new persistanceStatInfo ();

			newpersistanceStatInfo.currentValue = currentStatInfo.currentValue;

			newpersistanceStatInfo.extraCurrentValue = currentStatInfo.extraCurrentValue;

			if (newpersistanceStatInfo.extraCurrentValue > 0) {
				newpersistanceStatInfo.currentValue -= newpersistanceStatInfo.extraCurrentValue;

				newpersistanceStatInfo.currentValue = Mathf.Clamp (newpersistanceStatInfo.currentValue, 0, newpersistanceStatInfo.currentValue);
			}

			newpersistanceStatInfo.currentBoolState = currentStatInfo.currentBoolState;

			newPersistanceStatInfoList.Add (newpersistanceStatInfo);
		}	

		newStatsList.statsList = newPersistanceStatInfoList;

		return newStatsList;
	}


	void loadInfoOnMainComponent (bool showDebugInfo)
	{
		valuesInitializedOnLoad = true;

		initializeValues ();

		if (persistanceInfoList != null && persistanceInfoList.Count > 0) {

			List<playerStatsSystem.statInfo> statInfoList = mainPlayerStatsSystem.statInfoList;

			int statInfoListCount = statInfoList.Count;

			for (int k = 0; k < statInfoListCount; k++) {
				playerStatsSystem.statInfo currentStatInfo = statInfoList [k];

				persistanceStatInfo currentPersistanceStatInfo = persistanceInfoList [k];

				if (!currentStatInfo.ignoreStatInitializationOnLoadGame) {

					currentStatInfo.currentValue = currentPersistanceStatInfo.currentValue;
					currentStatInfo.currentBoolState = currentPersistanceStatInfo.currentBoolState;

					if (showDebugInfo) {
						print (statInfoList [k].Name + " " + statInfoList [k].currentValue + " " + statInfoList [k].currentBoolState);
					}
				}
			}

			mainPlayerStatsSystem.setIsLoadingGameState (true);
		}

		mainPlayerStatsSystem.initializeStatsValues ();
	}


	void getMainManager ()
	{
		if (mainPlayerStatsSystem == null) {
			mainPlayerStatsSystem = GKC_Utils.findObjectByComponent<playerStatsSystem> ();
		}
	}

	void initializeValues ()
	{
		mainPlayerStatsSystem.setIsLoadingGameState (false);

		if (!valuesInitializedOnLoad) {
			mainPlayerStatsSystem.initializeStatsValues ();
		}
	}

    [System.Serializable]
    public class SaveSlotListWrapper
    {
        public List<persistancePlayerStatsListBySaveSlotInfo> infoList = new List<persistancePlayerStatsListBySaveSlotInfo> ();
    }
}