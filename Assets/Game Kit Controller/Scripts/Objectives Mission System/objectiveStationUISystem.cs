using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using GKC.Localization;

public class objectiveStationUISystem : MonoBehaviour
{
    [Header ("Main Settings")]
    [Space]

    public bool objectiveStationUIEnabled = true;
    public bool menuOpened;

    public Color buttonUsable;
    public Color buttonNotUsable;

    public bool onlyActiveNewMissionIfNoPreviousInProcess;
    public bool onlyAddNewMissionsToPlayerLogMenuWithoutActivateThem;

    public bool useBlurUIPanel;

    [Space]
    [Header ("Mission Panel Settings")]
    [Space]

    public float dissableObjectiveAcceptedPanelDelay;

    public float delayToDissableCharacterPanelAfterAcceptMission;

    public string noMissionsAvailableText = "NO MISSIONS AVAILABLE";

    [Space]

    public bool disableMissionCompletePanelAfterTime;
    public float delayToDisableMissionPanel;

    [Space]
    [Header ("Events Settings")]
    [Space]

    public bool showMissionsWithHigherLevel = true;
    public UnityEvent eventOnMissionWithHigherLevel;

    public UnityEvent eventOnMissionAccepted;

    [Space]
    [Header ("Debug")]
    [Space]

    public bool showDebugPrint;

    public List<objectiveStationSystem.objectiveInfo> currentObjectiveInfoList = new List<objectiveStationSystem.objectiveInfo> ();

    public List<objectiveLogSystem.objectiveSlotInfo> objectiveSlotInfoList = new List<objectiveLogSystem.objectiveSlotInfo> ();

    public objectiveStationSystem currentObjectiveStationSystem;

    [Space]
    [Header ("Objective UI Elements")]
    [Space]

    public GameObject objectiveSlotPrefab;
    public Transform objectiveListContent;

    public Text objectiveNameText;
    public Text objectiveDescriptionText;
    public Text objectiveFullDescriptionText;
    public Text objectiveRewardsText;

    public Image acceptObjectiveButtonImage;

    public GameObject missionCompletePanel;
    public Text missionCompleteNameText;
    public Text missionCompleteRewardsText;
    public GameObject objectiveStationMenu;

    public GameObject missionAcceptedPanel;
    public Text missionAcceptedNameText;

    [Space]

    public UIPage mainUIPage;

    [Space]
    [Header ("Character Objective UI Elements")]
    [Space]
    public GameObject characterObjectivePanelGameObject;
    public Text characterObjectiveNameText;
    public Text characterObjectiveDescriptionText;
    public Text characterObjectiveFullDescriptionText;
    public Text characterObjectiveRewardsText;

    public GameObject characterAcceptMissionButton;
    public GameObject chracterGetMissionRewardButton;

    [Space]
    [Header ("Other Elements")]
    [Space]

    public GameObject playerControllerGameObject;

    public menuPause pauseManager;
    public usingDevicesSystem usingDevicesManager;

    public playerExperienceSystem mainPlayerExperienceSystem;

    public playerController playerControllerManager;

    public objectiveLogSystem mainObjectiveLogSystem;

    public objectiveManager mainObjectiveManager;



    objectiveLogSystem.objectiveSlotInfo currentObjectiveSlot;

    objectiveStationSystem.objectiveInfo currentObjectiveInfo;

    Coroutine missionCompleteCoroutine;

    bool useCharacterObjectivePanel;

    int currentCharacterObjectiveIndex;

    Coroutine acceptMissionFromCharacterCoroutine;

    Coroutine missionAcceptedCoroutine;

    bool componentInitialized;

    objectiveEventSystem currentObjectiveEventSystem;

    void Start ()
    {
        if (!objectiveStationUIEnabled) {
            return;
        }

        bool mainObjectiveManagerLocated = mainObjectiveManager != null;

        if (!mainObjectiveManagerLocated) {
            mainObjectiveManager = objectiveManager.Instance;

            mainObjectiveManagerLocated = mainObjectiveManager != null;
        }

        if (!mainObjectiveManagerLocated) {
            GKC_Utils.instantiateMainManagerOnSceneWithTypeOnApplicationPlaying (objectiveManager.getMainManagerName (), typeof (objectiveManager), true);

            mainObjectiveManager = objectiveManager.Instance;

            mainObjectiveManagerLocated = mainObjectiveManager != null;
        }

        if (!mainObjectiveManagerLocated) {

            mainObjectiveManager = GKC_Utils.findObjectByComponent<objectiveManager> ();
        }

        updateObjectiveTextContent ("", "", "", "");

        componentInitialized = true;
    }

    public void setButtonsColor (bool activeObjectiveColor)
    {
        if (activeObjectiveColor) {
            acceptObjectiveButtonImage.color = buttonUsable;
        } else {
            acceptObjectiveButtonImage.color = buttonNotUsable;
        }
    }

    //Assign a new station system, to get access to the missions configured on it
    public void setCurrentObjectiveStationSystem (objectiveStationSystem newObjectiveStationSystem)
    {
        currentObjectiveStationSystem = newObjectiveStationSystem;

        useCharacterObjectivePanel = currentObjectiveStationSystem.useCharacterObjectivePanel;
    }

    //Open or close the objective station UI
    public void openOrCloseObjectiveStationMenu (bool state)
    {
        menuOpened = state;

        if (menuOpened) {
            initializeMissionCompletePanelUIPage ();
        }

        if (useCharacterObjectivePanel) {
            pauseManager.openOrClosePlayerMenu (menuOpened, null, false);
        } else {
            pauseManager.openOrClosePlayerMenu (menuOpened, objectiveStationMenu.transform, useBlurUIPanel);
        }

        if (useCharacterObjectivePanel) {
            characterObjectivePanelGameObject.SetActive (menuOpened);
        } else {
            objectiveStationMenu.SetActive (menuOpened);
        }

        if (useCharacterObjectivePanel) {
            pauseManager.setIngameMenuOpenedState ("Character Objective Panel System", menuOpened, true);
        } else {
            pauseManager.setIngameMenuOpenedState ("Objective Station System", menuOpened, true);
        }

        //set to visible the cursor
        pauseManager.showOrHideCursor (menuOpened);

        //disable the touch controls
        pauseManager.checkTouchControls (!menuOpened);

        //disable the camera rotation
        pauseManager.changeCameraState (!menuOpened);

        playerControllerManager.changeScriptState (!menuOpened);

        pauseManager.usingSubMenuState (menuOpened);

        pauseManager.enableOrDisableDynamicElementsOnScreen (!menuOpened);

        currentObjectiveSlot = null;

        if (currentObjectiveSlot != null) {
            currentObjectiveSlot.selectedObjectiveIcon.SetActive (false);
        }

        setButtonsColor (false);

        updateObjectiveTextContent ("", "", "", "");

        disableMissionCompletePanel ();

        disableMissionAcceptedPanel ();

        stopAcceptMissionFromCharacterObjectivePanelCoroutine ();

        if (menuOpened) {

            currentObjectiveInfoList = currentObjectiveStationSystem.getObjectiveInfoList ();

            setObjectiveInfoList ();

            if (objectiveSlotInfoList.Count > 0) {
                bool slotFound = false;

                for (int i = 0; i < objectiveSlotInfoList.Count; i++) {
                    if (!slotFound) {
                        if (objectiveSlotInfoList [i].objectiveSlotGameObject != null &&
                            objectiveSlotInfoList [i].objectiveSlotGameObject.activeSelf) {

                            objectiveSlotInfoList [i].slotSelectedByPlayer = false;

                            checkPressedMission (objectiveSlotInfoList [i].objectiveSlotGameObject);

                            slotFound = true;
                        }
                    }
                }
            }
        } else {
            if (startMissionAfterPressingClosingStationActive) {
                checkStartCurrentMission ();

                startMissionAfterPressingClosingStationActive = false;
            }
        }

        if (showDebugPrint) {
            print ("openOrCloseObjectiveStationMenu " + state);
        }
    }

    //Update the info of the current mission selected on the menu
    public void setObjectiveInfoList ()
    {
        if (useCharacterObjectivePanel) {

            currentCharacterObjectiveIndex = currentObjectiveStationSystem.getCurrentCharacterObjectiveIndex ();

            if (currentCharacterObjectiveIndex < currentObjectiveInfoList.Count) {

                currentObjectiveInfo = currentObjectiveInfoList [currentCharacterObjectiveIndex];

                bool characterAcceptMissionButtonResult = false;

                if (currentObjectiveInfo.mainObjectiveEventSystem.isObjectiveComplete ()) {
                    if (currentObjectiveInfo.mainObjectiveEventSystem.getMissionCanBeReplayed ()) {
                        characterAcceptMissionButtonResult = true;
                    }

                    //if (currentObjectiveInfo.mainObjectiveEventSystem.isMissionAccepted ()) {
                    //    characterAcceptMissionButtonResult = false;
                    //}

                } else {
                    characterAcceptMissionButtonResult = true;
                }

                if (characterAcceptMissionButton.activeSelf != characterAcceptMissionButtonResult) {
                    characterAcceptMissionButton.SetActive (characterAcceptMissionButtonResult);
                }

                bool showMissionInfo = false;

                if (currentObjectiveInfo.mainObjectiveEventSystem.isObjectiveComplete ()) {
                    if (!currentObjectiveInfo.mainObjectiveEventSystem.giveRewardOnObjectiveComplete && !currentObjectiveInfo.mainObjectiveEventSystem.isRewardsObtained ()) {
                        chracterGetMissionRewardButton.SetActive (true);

                        showMissionInfo = true;
                    } else {
                        chracterGetMissionRewardButton.SetActive (false);
                    }
                } else {
                    chracterGetMissionRewardButton.SetActive (false);

                    showMissionInfo = true;
                }

                if (showMissionInfo) {
                    updateChracterObjectiveTextContent (currentObjectiveInfo.mainObjectiveEventSystem.generalObjectiveName, currentObjectiveInfo.mainObjectiveEventSystem.generalObjectiveDescription,
                        currentObjectiveInfo.mainObjectiveEventSystem.objectiveFullDescription, currentObjectiveInfo.mainObjectiveEventSystem.objectiveRewards);
                } else {
                    updateChracterObjectiveTextContent (noMissionsAvailableText, "", "", "");
                }
            } else {
                print ("WARNING: index of the mission station is not correct, make sure the index of the current mission index on the station system is managed properly");
            }
        } else {
            for (int i = 0; i < objectiveSlotInfoList.Count; i++) {
                if (objectiveSlotInfoList [i].objectiveSlotGameObject != null && objectiveSlotInfoList [i].objectiveSlotGameObject.activeSelf) {
                    objectiveSlotInfoList [i].objectiveSlotGameObject.SetActive (false);

                    objectiveSlotInfoList [i].objectiveEventManager = null;
                }
            }

            for (int i = 0; i < currentObjectiveInfoList.Count; i++) {

                currentObjectiveInfo = currentObjectiveInfoList [i];

                bool objectiveCanBeAdded = false;

                if (mainObjectiveLogSystem.checkMinLevelOnMissions) {
                    if (!currentObjectiveInfo.mainObjectiveEventSystem.useMinPlayerLevel || showMissionsWithHigherLevel) {
                        objectiveCanBeAdded = true;
                    } else {
                        if (mainPlayerExperienceSystem.getCurrentLevel () >= currentObjectiveInfo.mainObjectiveEventSystem.minPlayerLevel) {
                            objectiveCanBeAdded = true;
                        }
                    }
                } else {
                    objectiveCanBeAdded = true;
                }

                if (objectiveCanBeAdded) {
                    GameObject newObjectiveSlotPrefab = null;

                    objectiveLogSystem.objectiveSlotInfo newObjectiveSlotInfo = null;

                    bool createNewIconResult = true;

                    if (objectiveSlotInfoList.Count > 0) {
                        for (int j = 0; j < objectiveSlotInfoList.Count; j++) {
                            if (createNewIconResult) {
                                if (!objectiveSlotInfoList [j].objectiveSlotGameObject.activeSelf) {
                                    createNewIconResult = false;

                                    newObjectiveSlotPrefab = objectiveSlotInfoList [j].objectiveSlotGameObject;

                                    newObjectiveSlotInfo = objectiveSlotInfoList [j];
                                }
                            }
                        }
                    }

                    if (createNewIconResult) {
                        newObjectiveSlotPrefab = (GameObject)Instantiate (objectiveSlotPrefab, objectiveSlotPrefab.transform.position, Quaternion.identity, objectiveListContent);
                    }

                    if (!newObjectiveSlotPrefab.activeSelf) {
                        newObjectiveSlotPrefab.SetActive (true);
                    }

                    newObjectiveSlotPrefab.transform.localScale = Vector3.one;
                    newObjectiveSlotPrefab.transform.localPosition = Vector3.zero;

                    objectiveMenuIconElement currentobjectiveMenuIconElement = newObjectiveSlotPrefab.GetComponent<objectiveMenuIconElement> ();

                    string generalObjectiveName = currentObjectiveInfo.mainObjectiveEventSystem.generalObjectiveName;
                    string objectiveLocation = currentObjectiveInfo.mainObjectiveEventSystem.objectiveLocation;

                    if (gameLanguageSelector.isCheckLanguageActive ()) {
                        generalObjectiveName = missionLocalizationManager.GetLocalizedValue (generalObjectiveName);

                        objectiveLocation = missionLocalizationManager.GetLocalizedValue (objectiveLocation);
                    }

                    currentobjectiveMenuIconElement.objectiveNameText.text = generalObjectiveName;

                    currentobjectiveMenuIconElement.objectiveLocationText.text = objectiveLocation;

                    if (mainObjectiveLogSystem.checkMinLevelOnMissions) {
                        if (currentObjectiveInfo.mainObjectiveEventSystem.useMinPlayerLevel && showMissionsWithHigherLevel) {
                            currentobjectiveMenuIconElement.objectiveMinLevelText.gameObject.SetActive (true);

                            string levelText = "Level";

                            if (gameLanguageSelector.isCheckLanguageActive ()) {
                                levelText = missionLocalizationManager.GetLocalizedValue (levelText);
                            }

                            currentobjectiveMenuIconElement.objectiveMinLevelText.text = levelText + ": " + currentObjectiveInfo.mainObjectiveEventSystem.minPlayerLevel;
                        }
                    }

                    if (createNewIconResult) {
                        newObjectiveSlotInfo = new objectiveLogSystem.objectiveSlotInfo ();
                    }

                    newObjectiveSlotInfo.objectiveMenuIconElementManager = currentobjectiveMenuIconElement;

                    newObjectiveSlotInfo.objectiveSlotGameObject = newObjectiveSlotPrefab;
                    newObjectiveSlotInfo.objectiveName = currentObjectiveInfo.mainObjectiveEventSystem.generalObjectiveName;
                    newObjectiveSlotInfo.objectiveLocation = currentObjectiveInfo.mainObjectiveEventSystem.objectiveLocation;
                    newObjectiveSlotInfo.objectiveRewards = currentObjectiveInfo.mainObjectiveEventSystem.objectiveRewards;
                    newObjectiveSlotInfo.objectiveDescription = currentObjectiveInfo.mainObjectiveEventSystem.generalObjectiveDescription;
                    newObjectiveSlotInfo.objectiveFullDescription = currentObjectiveInfo.mainObjectiveEventSystem.objectiveFullDescription;

                    newObjectiveSlotInfo.currentObjectiveIcon = currentobjectiveMenuIconElement.currentObjectiveIcon;
                    newObjectiveSlotInfo.objectiveCompletePanel = currentobjectiveMenuIconElement.objectiveCompletePanel;
                    newObjectiveSlotInfo.selectedObjectiveIcon = currentobjectiveMenuIconElement.selectedObjectiveIcon;
                    newObjectiveSlotInfo.objectiveCompleteText = currentobjectiveMenuIconElement.objectiveCompleteText;
                    newObjectiveSlotInfo.objectiveAcceptedText = currentobjectiveMenuIconElement.objectiveAcceptedText;

                    newObjectiveSlotInfo.objectiveEventManager = currentObjectiveInfo.mainObjectiveEventSystem;

                    if (newObjectiveSlotInfo.objectiveEventManager.isObjectiveComplete ()) {

                        newObjectiveSlotInfo.objectiveCompletePanel.SetActive (true);
                        newObjectiveSlotInfo.objectiveCompleteText.SetActive (true);

                        newObjectiveSlotInfo.objectiveAcceptedText.gameObject.SetActive (false);

                        if (!newObjectiveSlotInfo.objectiveEventManager.isRewardsObtained ()) {
                            currentobjectiveMenuIconElement.getRewardsText.SetActive (true);
                        } else {
                            currentobjectiveMenuIconElement.getRewardsText.SetActive (false);
                        }
                    } else {
                        if (newObjectiveSlotInfo.objectiveEventManager.isMissionAccepted ()) {
                            newObjectiveSlotInfo.objectiveAcceptedText.gameObject.SetActive (true);
                            newObjectiveSlotInfo.currentObjectiveIcon.SetActive (true);
                        } else {
                            newObjectiveSlotInfo.objectiveAcceptedText.gameObject.SetActive (false);
                            newObjectiveSlotInfo.currentObjectiveIcon.SetActive (false);
                        }
                    }

                    newObjectiveSlotInfo.slotSelectedByPlayer = true;

                    if (createNewIconResult) {
                        objectiveSlotInfoList.Add (newObjectiveSlotInfo);

                        if (mainUIPage != null) {
                            mainUIPage.registerNewItem (newObjectiveSlotInfo.objectiveSlotGameObject);
                        }
                    }
                }
            }
        }
    }

    //Check the mission button pressed from the list of available missions on the station
    public void checkPressedMission (GameObject objectiveSlot)
    {
        if (currentObjectiveSlot != null) {
            currentObjectiveSlot.selectedObjectiveIcon.SetActive (false);
        }

        for (int i = 0; i < objectiveSlotInfoList.Count; i++) {
            if (objectiveSlotInfoList [i].objectiveSlotGameObject == objectiveSlot) {
                currentObjectiveSlot = objectiveSlotInfoList [i];

                updateObjectiveTextContent (currentObjectiveSlot.objectiveName, currentObjectiveSlot.objectiveDescription,
                    currentObjectiveSlot.objectiveFullDescription, currentObjectiveSlot.objectiveRewards);

                currentObjectiveSlot.selectedObjectiveIcon.SetActive (true);

                for (int j = 0; j < currentObjectiveInfoList.Count; j++) {
                    if (currentObjectiveInfoList [j].mainObjectiveEventSystem == currentObjectiveSlot.objectiveEventManager) {
                        currentObjectiveInfo = currentObjectiveInfoList [j];
                    }
                }

                if (!currentObjectiveSlot.objectiveEventManager.isObjectiveComplete ()) {
                    if (currentObjectiveSlot.objectiveEventManager.isObjectiveInProcess ()) {
                        setButtonsColor (false);
                    } else {
                        if (currentObjectiveSlot.objectiveEventManager.isMissionAccepted ()) {
                            setButtonsColor (false);

                            return;
                        }

                        setButtonsColor (true);
                    }
                } else {
                    if (currentObjectiveSlot.slotSelectedByPlayer) {
                        if (!currentObjectiveSlot.objectiveEventManager.giveRewardOnObjectiveComplete) {

                            if (!currentObjectiveSlot.objectiveEventManager.isRewardsObtained ()) {
                                showMissionCompleteMessage ();
                            }

                            if (!currentObjectiveSlot.objectiveEventManager.isRewardsObtained ()) {
                                currentObjectiveSlot.objectiveEventManager.giveRewardToPlayer ();

                                currentObjectiveSlot.objectiveEventManager.setRewardsObtanedState (true);

                                currentObjectiveSlot.objectiveMenuIconElementManager.getRewardsText.gameObject.SetActive (false);
                            }
                        }
                    }

                    currentObjectiveSlot.slotSelectedByPlayer = true;

                    if (currentObjectiveSlot.objectiveEventManager.getMissionCanBeReplayed ()) {
                        if (currentObjectiveSlot.objectiveEventManager.isRewardsObtained ()) {
                            setButtonsColor (true);
                        } else {
                            setButtonsColor (false);
                        }
                    } else {
                        setButtonsColor (false);
                    }
                }

                return;
            }
        }

        setButtonsColor (false);
    }

    public void updateChracterObjectiveTextContent (string objectiveName, string objectiveDescription, string objectiveFullDescription, string objectiveRewards)
    {
        if (gameLanguageSelector.isCheckLanguageActive ()) {
            objectiveName = missionLocalizationManager.GetLocalizedValue (objectiveName);

            objectiveRewards = missionLocalizationManager.GetLocalizedValue (objectiveRewards);

            objectiveDescription = missionLocalizationManager.GetLocalizedValue (objectiveDescription);

            objectiveFullDescription = missionLocalizationManager.GetLocalizedValue (objectiveFullDescription);
        }

        characterObjectiveNameText.text = objectiveName;

        characterObjectiveRewardsText.text = objectiveRewards;

        characterObjectiveDescriptionText.text = objectiveDescription;

        characterObjectiveFullDescriptionText.text = objectiveFullDescription;
    }

    public void updateObjectiveTextContent (string objectiveName, string objectiveDescription, string objectiveFullDescription, string objectiveRewards)
    {
        if (gameLanguageSelector.isCheckLanguageActive ()) {
            objectiveName = missionLocalizationManager.GetLocalizedValue (objectiveName);

            objectiveRewards = missionLocalizationManager.GetLocalizedValue (objectiveRewards);

            objectiveDescription = missionLocalizationManager.GetLocalizedValue (objectiveDescription);

            objectiveFullDescription = missionLocalizationManager.GetLocalizedValue (objectiveFullDescription);
        }

        objectiveNameText.text = objectiveName;

        objectiveRewardsText.text = objectiveRewards;

        objectiveDescriptionText.text = objectiveDescription;

        objectiveFullDescriptionText.text = objectiveFullDescription;
    }

    //Accept a mission from the mission station board
    public void acceptMissionFromObjectiveStationPanel ()
    {
        if (currentObjectiveSlot != null) {
            bool missionCanBeReplayed = currentObjectiveSlot.objectiveEventManager.getMissionCanBeReplayed ();

            bool isObjectiveComplete = currentObjectiveSlot.objectiveEventManager.isObjectiveComplete ();

            if ((!isObjectiveComplete || missionCanBeReplayed) &&
                !currentObjectiveInfo.mainObjectiveEventSystem.isObjectiveInProcess ()) {

                if (missionCanBeReplayed) {
                    if (isObjectiveComplete) {
                        if (!currentObjectiveSlot.objectiveEventManager.isRewardsObtained ()) {
                            return;
                        }
                    }
                }

                for (int j = 0; j < currentObjectiveInfoList.Count; j++) {
                    if (currentObjectiveInfoList [j].mainObjectiveEventSystem == currentObjectiveSlot.objectiveEventManager) {
                        currentObjectiveInfo = currentObjectiveInfoList [j];
                    }
                }

                if (mainObjectiveLogSystem.checkMinLevelOnMissions) {
                    bool objectiveCanBeAdded = false;

                    if (!currentObjectiveInfo.mainObjectiveEventSystem.useMinPlayerLevel) {
                        objectiveCanBeAdded = true;
                    } else {
                        if (mainPlayerExperienceSystem.getCurrentLevel () >= currentObjectiveInfo.mainObjectiveEventSystem.minPlayerLevel) {
                            objectiveCanBeAdded = true;
                        }
                    }

                    if (!objectiveCanBeAdded) {
                        eventOnMissionWithHigherLevel.Invoke ();
                        return;
                    }
                }

                currentObjectiveSlot.objectiveEventManager.setCurrentPlayer (playerControllerGameObject);

                if ((!mainObjectiveLogSystem.isObjectiveInProcess () || !onlyActiveNewMissionIfNoPreviousInProcess) && !onlyAddNewMissionsToPlayerLogMenuWithoutActivateThem) {
                    currentObjectiveSlot.objectiveEventManager.startObjective ();
                } else {
                    currentObjectiveSlot.objectiveEventManager.addObjectiveToPlayerLogMenu ();
                }

                currentObjectiveSlot.currentObjectiveIcon.SetActive (true);

                currentObjectiveSlot.objectiveAcceptedText.SetActive (true);

                if (missionCanBeReplayed && isObjectiveComplete) {
                    currentObjectiveSlot.objectiveCompletePanel.SetActive (false);
                    currentObjectiveSlot.objectiveCompleteText.SetActive (false);
                }

                setButtonsColor (false);

                showMissionAcceptedMessage (currentObjectiveInfo.mainObjectiveEventSystem.generalObjectiveName);

                mainObjectiveLogSystem.checkUpdateObjectiveSlotInfo (currentObjectiveInfo.mainObjectiveEventSystem);
            }
        }
    }

    //Accept a mission from a character
    public void acceptMissionFromCharacterObjectivePanel ()
    {
        if (currentObjectiveInfo != null) {
            if ((!currentObjectiveInfo.mainObjectiveEventSystem.isObjectiveComplete () || currentObjectiveInfo.mainObjectiveEventSystem.getMissionCanBeReplayed ()) &&
                !currentObjectiveInfo.mainObjectiveEventSystem.isObjectiveInProcess ()) {

                if (mainObjectiveLogSystem.checkMinLevelOnMissions) {
                    bool objectiveCanBeAdded = false;

                    if (!currentObjectiveInfo.mainObjectiveEventSystem.useMinPlayerLevel) {
                        objectiveCanBeAdded = true;
                    } else {
                        if (mainPlayerExperienceSystem.getCurrentLevel () >= currentObjectiveInfo.mainObjectiveEventSystem.minPlayerLevel) {
                            objectiveCanBeAdded = true;
                        }
                    }

                    if (!objectiveCanBeAdded) {
                        eventOnMissionWithHigherLevel.Invoke ();
                        return;
                    }
                }

                currentObjectiveInfo.mainObjectiveEventSystem.setCurrentPlayer (playerControllerGameObject);

                startMissionAfterPressingClosingStationActive = currentObjectiveStationSystem.startMissionAfterPressingClosingStation;

                if (!startMissionAfterPressingClosingStationActive) {
                    checkStartCurrentMission ();
                }

                showMissionAcceptedMessage (currentObjectiveInfo.mainObjectiveEventSystem.generalObjectiveName);

                stopAcceptMissionFromCharacterObjectivePanelCoroutine ();

                acceptMissionFromCharacterCoroutine = StartCoroutine (acceptMissionFromCharacterObjectivePanelCoroutine ());

                characterAcceptMissionButton.SetActive (false);
            }
        }
    }

    Coroutine startMissionCoroutine;

    public void checkStartCurrentMission ()
    {
        if (currentObjectiveStationSystem != null && currentObjectiveStationSystem.useDelayToStartMission) {
            if (startMissionCoroutine != null) {
                StopCoroutine (startMissionCoroutine);
            }

            startMissionCoroutine = StartCoroutine (startCurrentMissionCoroutine (currentObjectiveStationSystem.delayToStartMission));
        } else {
            startCurrentMission ();
        }
    }

    bool startMissionAfterPressingClosingStationActive;

    public void startCurrentMission ()
    {
        if ((!mainObjectiveLogSystem.isObjectiveInProcess () || !onlyActiveNewMissionIfNoPreviousInProcess) && !onlyAddNewMissionsToPlayerLogMenuWithoutActivateThem) {
            currentObjectiveInfo.mainObjectiveEventSystem.startObjective ();
        } else {
            currentObjectiveInfo.mainObjectiveEventSystem.addObjectiveToPlayerLogMenu ();
        }
    }

    IEnumerator startCurrentMissionCoroutine (float delayToStartMission)
    {
        yield return new WaitForSeconds (delayToStartMission);

        startCurrentMission ();
    }

    public void stopAcceptMissionFromCharacterObjectivePanelCoroutine ()
    {
        if (acceptMissionFromCharacterCoroutine != null) {
            StopCoroutine (acceptMissionFromCharacterCoroutine);
        }
    }

    //Close the mission panel after getting a new mission from a character
    IEnumerator acceptMissionFromCharacterObjectivePanelCoroutine ()
    {
        yield return new WaitForSeconds (delayToDissableCharacterPanelAfterAcceptMission);

        if (menuOpened) {
            usingDevicesManager.useDevice ();
        }
    }

    //Get the reward from a character mission
    public void getMissionRewardFromCharacterObjectivePanel ()
    {
        if (!currentObjectiveInfo.mainObjectiveEventSystem.isRewardsObtained ()) {
            showMissionCompleteMessage ();
        }

        if (!currentObjectiveInfo.mainObjectiveEventSystem.isRewardsObtained ()) {
            currentObjectiveInfo.mainObjectiveEventSystem.giveRewardToPlayer ();

            currentObjectiveInfo.mainObjectiveEventSystem.setRewardsObtanedState (true);
        }

        chracterGetMissionRewardButton.SetActive (false);
    }

    //Different functions to open and close this menu
    public void openOrCloseObjectiveMenuFromTouch ()
    {
        openOrCloseObjectiveStationMenu (!menuOpened);
    }

    public void openOrCloseObjectiveStationMenuByButton ()
    {
        usingDevicesManager.useDevice ();

        if (currentObjectiveStationSystem) {
            currentObjectiveStationSystem.setUsingObjectiveStationState (menuOpened);
        }
    }

    //Show a panel from a mission accepted
    public void showMissionCompleteMessage ()
    {
        if (disableMissionCompletePanelAfterTime) {
            showMissionCompleteMessageTemporarily (delayToDisableMissionPanel);
        } else {
            enableAndSetMissionCompletePanelInfo ();
        }
    }

    public void showMissionCompleteMessageTemporarily ()
    {
        showMissionCompleteMessageTemporarily (delayToDisableMissionPanel);
    }

    public void showMissionCompleteMessageTemporarily (float delayToDisablePanel)
    {
        if (missionCompleteCoroutine != null) {
            StopCoroutine (missionCompleteCoroutine);
        }

        missionCompleteCoroutine = StartCoroutine (showMissionCompleteMessageCoroutine (delayToDisablePanel));
    }

    IEnumerator showMissionCompleteMessageCoroutine (float delayToDisablePanel)
    {
        usingDevicesManager.setUseDeviceButtonEnabledState (false);

        enableAndSetMissionCompletePanelInfo ();

        yield return new WaitForSeconds (delayToDisablePanel);

        disableMissionCompletePanel ();

        usingDevicesManager.setUseDeviceButtonEnabledState (true);
    }

    //Check if after complete a mission of a character, that character has more missions available
    public void checkIfMissionsAvailableInCharacter ()
    {
        if (useCharacterObjectivePanel) {
            if (currentObjectiveInfo != null &&
                currentObjectiveInfo.mainObjectiveEventSystem.isObjectiveComplete () &&
                currentObjectiveInfo.mainObjectiveEventSystem.isRewardsObtained ()) {

                currentCharacterObjectiveIndex += 1;

                if (currentObjectiveStationSystem.isThereMissionsAvailableOnStation (currentCharacterObjectiveIndex)) {
                    setObjectiveInfoList ();
                } else {
                    updateChracterObjectiveTextContent (noMissionsAvailableText, "", "", "");
                }
            }
        }
    }

    //Show a mission complete panel
    public void enableAndSetMissionCompletePanelInfo ()
    {
        setMissionCompletePanelActiveState (true);

        currentObjectiveEventSystem = null;

        if (useCharacterObjectivePanel) {
            if (currentObjectiveInfo != null) {
                currentObjectiveEventSystem = currentObjectiveInfo.mainObjectiveEventSystem;
            } else {
                currentObjectiveEventSystem = mainObjectiveLogSystem.getCurrentObjectiveEventSystem ();
            }
        } else {
            if (currentObjectiveSlot != null) {
                currentObjectiveEventSystem = currentObjectiveSlot.objectiveEventManager;
            } else {
                currentObjectiveEventSystem = mainObjectiveLogSystem.getCurrentObjectiveEventSystem ();
            }
        }

        if (currentObjectiveEventSystem != null) {
            string generalObjectiveName = currentObjectiveEventSystem.generalObjectiveName;
            string objectiveRewards = currentObjectiveEventSystem.objectiveRewards;

            if (gameLanguageSelector.isCheckLanguageActive ()) {
                generalObjectiveName = missionLocalizationManager.GetLocalizedValue (generalObjectiveName);
                objectiveRewards = missionLocalizationManager.GetLocalizedValue (objectiveRewards);
            }

            missionCompleteNameText.text = generalObjectiveName;

            missionCompleteRewardsText.text = objectiveRewards;
        }
    }

    //Enable and disable different panels from the mission UI system
    public void disableMissionCompletePanel ()
    {
        setMissionCompletePanelActiveState (false);
    }

    bool missionCompletePanelActive;

    void setMissionCompletePanelActiveState (bool state)
    {
        if (missionCompletePanelActive == state) {
            return;
        }

        if (missionCompletePanel.activeSelf != state) {
            missionCompletePanel.SetActive (state);
        }

        missionCompletePanelActive = state;

        if (missionCompletePanelUIPageCancelEventInitialized) {
            if (missionCompletePanelActive) {
                pauseManager.pushNewUIPageOnCurrentIngamePanel (missionCompletePanel.GetComponent<UIPage> ());
            } else {
                pauseManager.popUIPageOnCurrentIngamePanel (missionCompletePanel.GetComponent<UIPage> ());
            }
        }
    }

    bool missionCompletePanelUIPageCancelEventInitialized;

    void initializeMissionCompletePanelUIPage ()
    {
        if (missionCompletePanelUIPageCancelEventInitialized) {
            return;
        }

        UIPage missionCompletePanelUIPage = missionCompletePanel.GetComponent<UIPage> ();

        if (missionCompletePanelUIPage != null) {
            missionCompletePanelUIPage.ClearCancelEvent ();

            missionCompletePanelUIPage.OnCancelEvent += ProcessMenuCancelEvent;
        }

        missionCompletePanelUIPageCancelEventInitialized = true;
    }

    //function called as an event through the cancel/return gamepad button, for now, when using a pop up UI page here
    bool ProcessMenuCancelEvent ()
    {
        bool result = false;

        if (missionCompletePanelActive) {
            setMissionCompletePanelActiveState (false);

            result = true;
        }

        return result;
    }

    public void showMissionAcceptedMessage (string missionAcceptedMessage, float delayToDisableMissionAcceptedPanelValue)
    {
        if (!componentInitialized) {
            return;
        }

        if (missionAcceptedCoroutine != null) {
            StopCoroutine (missionAcceptedCoroutine);
        }

        missionAcceptedCoroutine = StartCoroutine (showMissionAcceptedMessageCoroutine (missionAcceptedMessage, delayToDisableMissionAcceptedPanelValue));
    }

    public void showMissionAcceptedMessage (string missionAcceptedMessage)
    {
        showMissionAcceptedMessage (missionAcceptedMessage, dissableObjectiveAcceptedPanelDelay);
    }

    IEnumerator showMissionAcceptedMessageCoroutine (string missionAcceptedMessage, float delayToDisableMissionAcceptedPanelValue)
    {
        enableAndSetMissionAcceptedPanelInfo (missionAcceptedMessage);

        eventOnMissionAccepted.Invoke ();

        yield return new WaitForSeconds (delayToDisableMissionAcceptedPanelValue);

        disableMissionAcceptedPanel ();
    }

    public void disableMissionAcceptedPanel ()
    {
        missionAcceptedPanel.SetActive (false);
    }

    public void enableAndSetMissionAcceptedPanelInfo (string missionAcceptedMessage)
    {
        missionAcceptedPanel.SetActive (true);

        if (gameLanguageSelector.isCheckLanguageActive ()) {
            missionAcceptedMessage = missionLocalizationManager.GetLocalizedValue (missionAcceptedMessage);
        }

        missionAcceptedNameText.text = missionAcceptedMessage;
    }
}