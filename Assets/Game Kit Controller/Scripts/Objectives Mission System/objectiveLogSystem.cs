using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using GKC.Localization;

public class objectiveLogSystem : MonoBehaviour
{
    [Header ("Main Settings")]
    [Space]

    public bool openMenuEnabled = true;

    public bool objectiveMenuActive = true;

    public bool checkMinLevelOnMissions;

    public Color buttonUsable;
    public Color buttonNotUsable;

    public bool saveCurrentPlayerMissionsToSaveFile;

    public string menuPanelName = "Objective Log System";

    public string mainMissionManagerName = "Mission Manager";

    [Space]
    [Header ("Debug")]
    [Space]

    public bool showDebugPrint;

    public bool objectiveMenuOpened;

    public bool objectiveInProcess;

    public List<objectiveSlotInfo> objectiveSlotInfoList = new List<objectiveSlotInfo> ();

    [Space]
    [Header ("Events Settings")]
    [Space]

    public bool useEventIfSystemDisabled;
    public UnityEvent eventIfSystemDisabled;

    [Space]
    [Header ("UI Components")]
    [Space]

    public GameObject objectiveMenuGameObject;
    public GameObject objectiveSlotPrefab;

    public Text objectiveNameText;
    public Text objectiveDescriptionText;
    public Text objectiveFullDescriptionText;
    public Text objectiveRewardsText;

    public Image activeObjectiveButtonImage;
    public Image cancelObjectiveButtonImage;

    public Transform objectiveListContent;

    public Scrollbar missionListScrollbar;
    public Scrollbar missionInfoScrollbar;
    public Scrollbar missionRewardScrollbar;

    [Space]

    public UIPage mainUIPage;

    Coroutine missionListScrollbarCoroutine;
    Coroutine missionInfoScrollbarCoroutine;
    Coroutine missionRewardScrollbarCoroutine;

    [Space]
    [Header ("Components")]
    [Space]

    public GameObject playerControllerGameObject;
    public menuPause pauseManager;
    public playerController playerControllerManager;

    public objectiveManager mainObjectiveManager;


    objectiveSlotInfo currentObjectiveSlot;
    objectiveEventSystem currentObjectiveEventManager;

    bool mainObjectiveManagerLocated;


    public void initializeMissionValues ()
    {
        //Load the missions saved previously with those missions found by the player or activated in some way, setting their state or complete or not complete
        if (!objectiveMenuActive) {
            checkEventOnSystemDisabled ();

            return;
        }

        //Search for an objectives manager in the level, if no one is present, add one
        mainObjectiveManagerLocated = mainObjectiveManager != null;

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

            mainObjectiveManagerLocated = mainObjectiveManager != null;
        }

        updateObjectiveTextContent ("", "", "", "");
    }

    public objectiveEventSystem getObjectiveEventSystem (int missionID, int missionSceneToSearch)
    {
        if (mainObjectiveManagerLocated) {
            return mainObjectiveManager.getObjectiveEventSystem (missionID, missionSceneToSearch);
        }

        return null;
    }

    public void checkEventOnSystemDisabled ()
    {
        if (useEventIfSystemDisabled) {
            eventIfSystemDisabled.Invoke ();
        }
    }

    public void setButtonsColor (bool activeObjectiveColor, bool cancelObjectiveColor)
    {
        if (activeObjectiveColor) {
            activeObjectiveButtonImage.color = buttonUsable;
        } else {
            activeObjectiveButtonImage.color = buttonNotUsable;
        }

        if (cancelObjectiveColor) {
            cancelObjectiveButtonImage.color = buttonUsable;
        } else {
            cancelObjectiveButtonImage.color = buttonNotUsable;
        }
    }

    public void activeObjective ()
    {
        if (currentObjectiveSlot != null && currentObjectiveSlot.objectiveEventManager != null) {
            bool canActivateObjectiveResult = false;

            canActivateObjectiveResult = (!currentObjectiveSlot.objectiveEventManager.isObjectiveComplete () ||
                currentObjectiveSlot.objectiveEventManager.getMissionCanBeReplayed ()) &&
                !currentObjectiveSlot.objectiveEventManager.isObjectiveInProcess ();

            if (canActivateObjectiveResult) {
                currentObjectiveSlot.objectiveEventManager.startObjective ();

                if (!currentObjectiveSlot.currentObjectiveIcon.activeSelf) {
                    currentObjectiveSlot.currentObjectiveIcon.SetActive (true);
                }

                bool isMissionCanBeCancelledEnabled = currentObjectiveSlot.objectiveEventManager.isMissionCanBeCancelledEnabled ();

                setButtonsColor (false, isMissionCanBeCancelledEnabled);

                currentObjectiveEventManager = currentObjectiveSlot.objectiveEventManager;
            }
        }
    }

    public void cancelObjective ()
    {
        if (currentObjectiveSlot != null && currentObjectiveSlot.objectiveEventManager != null) {
            bool isMissionCanBeCancelledEnabled = currentObjectiveSlot.objectiveEventManager.isMissionCanBeCancelledEnabled ();

            if (!isMissionCanBeCancelledEnabled) {
                return;
            }

            if (!currentObjectiveSlot.objectiveEventManager.isObjectiveComplete ()) {
                currentObjectiveSlot.objectiveEventManager.stopObjective ();

                if (currentObjectiveSlot != null) {
                    if (currentObjectiveSlot.currentObjectiveIcon.activeSelf) {
                        currentObjectiveSlot.currentObjectiveIcon.SetActive (false);
                    }

                    setButtonsColor (true, false);
                } else {
                    setButtonsColor (false, false);
                }

                //if (currentObjectiveSlot.objectiveEventManager.removeMissionSlotFromObjectiveLogOnCancelMission) {
                //removeObjectiveSlotFromMenu (currentObjectiveSlot.objectiveEventManager.missionID, currentObjectiveSlot.objectiveEventManager.missionScene);
                //}
            }
        }
    }

    public void objectiveComplete (objectiveEventSystem currentObjectiveEventSystem)
    {
        int objectiveSlotInfoListCount = objectiveSlotInfoList.Count;

        for (int i = 0; i < objectiveSlotInfoListCount; i++) {
            objectiveSlotInfo currentObjectiveSlotInfo = objectiveSlotInfoList [i];

            if (currentObjectiveSlotInfo.objectiveEventManager == currentObjectiveEventSystem) {
                updateObjectiveCompleteSlotInfo (i);

                objectiveInProcess = false;
            }
        }
    }

    public void updateObjectiveCompleteSlotInfo (int objectiveSlotIndex)
    {
        objectiveSlotInfo currentObjectiveSlotInfo = objectiveSlotInfoList [objectiveSlotIndex];

        bool enableObjectiveSlotResult = false;

        if (currentObjectiveSlotInfo.addObjectiveToPlayerLogSystem) {
            if (currentObjectiveSlotInfo.disableObjectivePanelOnMissionComplete) {
                enableObjectiveSlotResult = false;
            } else {
                if (currentObjectiveSlotInfo.currentObjectiveIcon.activeSelf) {
                    currentObjectiveSlotInfo.currentObjectiveIcon.SetActive (false);
                }

                if (!currentObjectiveSlotInfo.objectiveCompletePanel.activeSelf) {
                    currentObjectiveSlotInfo.objectiveCompletePanel.SetActive (true);
                }

                if (!currentObjectiveSlotInfo.objectiveCompleteText.activeSelf) {
                    currentObjectiveSlotInfo.objectiveCompleteText.SetActive (true);
                }

                enableObjectiveSlotResult = true;
            }
        } else {
            enableObjectiveSlotResult = false;
        }

        if (currentObjectiveSlotInfo.objectiveSlotGameObject.activeSelf != enableObjectiveSlotResult) {
            currentObjectiveSlotInfo.objectiveSlotGameObject.SetActive (enableObjectiveSlotResult);
        }

        currentObjectiveSlotInfo.missionInProcess = false;

        currentObjectiveSlotInfo.missionComplete = true;
    }

    public void checkUpdateObjectiveSlotInfo (objectiveEventSystem currentObjectiveEventSystem)
    {
        int objectiveSlotInfoListCount = objectiveSlotInfoList.Count;

        for (int i = 0; i < objectiveSlotInfoListCount; i++) {
            objectiveSlotInfo currentObjectiveSlotInfo = objectiveSlotInfoList [i];

            if (currentObjectiveSlotInfo.objectiveEventManager == currentObjectiveEventSystem) {
                if (currentObjectiveSlotInfo.addObjectiveToPlayerLogSystem) {
                    if (!currentObjectiveSlotInfo.disableObjectivePanelOnMissionComplete) {
                        bool isObjectiveComplete = currentObjectiveEventSystem.isObjectiveComplete ();

                        if (currentObjectiveSlotInfo.objectiveCompletePanel.activeSelf != isObjectiveComplete) {
                            currentObjectiveSlotInfo.objectiveCompletePanel.SetActive (isObjectiveComplete);
                        }

                        if (currentObjectiveSlotInfo.objectiveCompleteText.activeSelf != isObjectiveComplete) {
                            currentObjectiveSlotInfo.objectiveCompleteText.SetActive (isObjectiveComplete);
                        }

                        bool isObjectiveInProcess = currentObjectiveEventSystem.isObjectiveInProcess ();

                        if (currentObjectiveSlotInfo.currentObjectiveIcon.activeSelf != isObjectiveInProcess) {
                            currentObjectiveSlotInfo.currentObjectiveIcon.SetActive (isObjectiveInProcess);
                        }

                    }
                }

                return;
            }
        }
    }

    public void updateSubObjectiveCompleteListSlotInfo (int objectiveSlotIndex, List<bool> subObjectiveCompleteList)
    {
        objectiveSlotInfo currentObjectiveSlotInfo = objectiveSlotInfoList [objectiveSlotIndex];

        currentObjectiveSlotInfo.subObjectiveCompleteList = subObjectiveCompleteList;
    }

    public void activeObjective (objectiveEventSystem currentObjectiveEventSystem)
    {
        int objectiveSlotInfoListCount = objectiveSlotInfoList.Count;

        for (int i = 0; i < objectiveSlotInfoListCount; i++) {
            objectiveSlotInfo currentObjectiveSlotInfo = objectiveSlotInfoList [i];

            if (currentObjectiveSlotInfo.objectiveEventManager == currentObjectiveEventSystem) {

                if (!currentObjectiveSlotInfo.currentObjectiveIcon.activeSelf) {
                    currentObjectiveSlotInfo.currentObjectiveIcon.SetActive (true);
                }

                currentObjectiveEventManager = currentObjectiveSlotInfo.objectiveEventManager;

                currentObjectiveSlotInfo.missionInProcess = true;

                objectiveInProcess = true;
            }
        }
    }

    public void cancelObjective (objectiveEventSystem currentObjectiveEventSystem)
    {
        int objectiveSlotInfoListCount = objectiveSlotInfoList.Count;

        for (int i = 0; i < objectiveSlotInfoListCount; i++) {
            objectiveSlotInfo currentObjectiveSlotInfo = objectiveSlotInfoList [i];

            if (currentObjectiveSlotInfo.objectiveEventManager == currentObjectiveEventSystem) {
                if (currentObjectiveSlotInfo.currentObjectiveIcon.activeSelf) {
                    currentObjectiveSlotInfo.currentObjectiveIcon.SetActive (false);
                }

                objectiveInProcess = false;

                currentObjectiveSlotInfo.missionInProcess = false;

                if (currentObjectiveEventSystem.isRemoveMissionSlotFromObjectiveLogOnCancelMissionEnabled ()) {
                    removeObjectiveSlotFromMenu (currentObjectiveEventSystem.missionID, currentObjectiveEventSystem.missionScene);
                }
            }
        }
    }

    public void cancelCurrentMissionOnPlayerDead ()
    {
        if (currentObjectiveEventManager != null) {
            if (currentObjectiveEventManager.isCancelMissionIfPlayerDiesEnabled ()) {
                cancelPreviousObjective ();
            }
        }
    }
    public void cancelPreviousObjective ()
    {
        if (currentObjectiveEventManager != null) {
            if (currentObjectiveEventManager.isObjectiveInProcess ()) {
                currentObjectiveEventManager.cancelPreviousObjective ();
            }
        }
    }

    public objectiveEventSystem getCurrentObjectiveEventSystem ()
    {
        return currentObjectiveEventManager;
    }

    public void showObjectiveInformation (GameObject objectiveSlot)
    {
        if (currentObjectiveSlot != null && currentObjectiveSlot.selectedObjectiveIcon.activeSelf) {
            currentObjectiveSlot.selectedObjectiveIcon.SetActive (false);
        }

        int objectiveSlotInfoListCount = objectiveSlotInfoList.Count;

        for (int i = 0; i < objectiveSlotInfoListCount; i++) {
            objectiveSlotInfo currentObjectiveSlotInfo = objectiveSlotInfoList [i];

            if (currentObjectiveSlotInfo.objectiveSlotGameObject == objectiveSlot) {
                currentObjectiveSlot = currentObjectiveSlotInfo;

                updateObjectiveTextContent (currentObjectiveSlot.objectiveName, currentObjectiveSlot.objectiveDescription,
                    currentObjectiveSlot.objectiveFullDescription, currentObjectiveSlot.objectiveRewards);

                if (!currentObjectiveSlot.selectedObjectiveIcon.activeSelf) {
                    currentObjectiveSlot.selectedObjectiveIcon.SetActive (true);
                }

                if (currentObjectiveSlot.objectiveEventManager != null) {
                    if (!currentObjectiveSlot.objectiveEventManager.isObjectiveComplete ()) {
                        if (currentObjectiveSlot.objectiveEventManager.isObjectiveInProcess ()) {

                            bool isMissionCanBeCancelledEnabled = currentObjectiveSlot.objectiveEventManager.isMissionCanBeCancelledEnabled ();

                            setButtonsColor (false, isMissionCanBeCancelledEnabled);
                        } else {
                            setButtonsColor (true, false);
                        }
                    } else {
                        if (currentObjectiveSlot.objectiveEventManager.getMissionCanBeReplayed ()) {
                            setButtonsColor (true, false);
                        } else {
                            setButtonsColor (false, false);
                        }
                    }
                } else {
                    setButtonsColor (false, false);
                }

                return;
            }
        }

        setButtonsColor (false, false);
    }

    public void updateUIElements ()
    {
        if (objectiveInProcess) {
            currentObjectiveEventManager.updateUIElements ();
        }

        int objectiveSlotInfoListCount = objectiveSlotInfoList.Count;

        for (int i = 0; i < objectiveSlotInfoListCount; i++) {
            objectiveSlotInfo currentObjectiveSlotInfo = objectiveSlotInfoList [i];

            string objectiveName = currentObjectiveSlotInfo.objectiveName;

            string objectiveLocation = currentObjectiveSlotInfo.objectiveLocation;

            if (gameLanguageSelector.isCheckLanguageActive ()) {
                objectiveName = missionLocalizationManager.GetLocalizedValue (objectiveName);

                objectiveLocation = missionLocalizationManager.GetLocalizedValue (objectiveLocation);
            }

            if (currentObjectiveSlotInfo.objectiveMenuIconElementManager != null) {
                currentObjectiveSlotInfo.objectiveMenuIconElementManager.objectiveNameText.text = objectiveName;

                currentObjectiveSlotInfo.objectiveMenuIconElementManager.objectiveLocationText.text = objectiveLocation;
            }
        }
    }

    public void updateObjectiveTextContent (string objectiveName, string objectiveDescription, string objectiveFullDescription, string objectiveRewards)
    {
        if (gameLanguageSelector.isCheckLanguageActive ()) {
            objectiveName = missionLocalizationManager.GetLocalizedValue (objectiveName);

            objectiveDescription = missionLocalizationManager.GetLocalizedValue (objectiveDescription);

            objectiveFullDescription = missionLocalizationManager.GetLocalizedValue (objectiveFullDescription);

            objectiveRewards = missionLocalizationManager.GetLocalizedValue (objectiveRewards);
        }

        objectiveNameText.text = objectiveName;

        objectiveDescriptionText.text = objectiveDescription;

        objectiveFullDescriptionText.text = objectiveFullDescription;

        objectiveRewardsText.text = objectiveRewards;
    }

    public void addObjective (string objectiveName, string objectiveDescription, string objectiveFullDescription, string objectiveLocation,
                              string objectiveRewards, objectiveEventSystem currentObjectiveEventSystem, bool addObjectiveToPlayerLogSystem)
    {
        bool addNewObjectivePanel = true;

        objectiveSlotInfo newObjectiveSlotInfo = new objectiveSlotInfo ();

        if (currentObjectiveEventSystem != null) {
            int objectiveSlotInfoListCount = objectiveSlotInfoList.Count;

            for (int i = 0; i < objectiveSlotInfoListCount; i++) {
                if (objectiveSlotInfoList [i].objectiveEventManager != null) {
                    if (objectiveSlotInfoList [i].objectiveEventManager == currentObjectiveEventSystem) {
                        return;
                    }
                } else {
                    if (objectiveSlotInfoList [i].missionID == currentObjectiveEventSystem.missionID &&
                        objectiveSlotInfoList [i].missionScene == currentObjectiveEventSystem.missionScene) {

                        if (objectiveSlotInfoList [i].missionComplete) {
                            return;
                        }

                        newObjectiveSlotInfo = objectiveSlotInfoList [i];

                        addNewObjectivePanel = false;
                    }
                }
            }
        }

        if (addNewObjectivePanel) {
            GameObject newObjectiveSlotPrefab = (GameObject)Instantiate (objectiveSlotPrefab, objectiveSlotPrefab.transform.position, Quaternion.identity, objectiveListContent);

            if (!newObjectiveSlotPrefab.activeSelf) {
                newObjectiveSlotPrefab.SetActive (true);
            }

            newObjectiveSlotPrefab.transform.localScale = Vector3.one;
            newObjectiveSlotPrefab.transform.localPosition = Vector3.zero;

            objectiveMenuIconElement currentobjectiveMenuIconElement = newObjectiveSlotPrefab.GetComponent<objectiveMenuIconElement> ();

            if (gameLanguageSelector.isCheckLanguageActive ()) {
                objectiveName = missionLocalizationManager.GetLocalizedValue (objectiveName);

                objectiveLocation = missionLocalizationManager.GetLocalizedValue (objectiveLocation);
            }

            currentobjectiveMenuIconElement.objectiveNameText.text = objectiveName;
            currentobjectiveMenuIconElement.objectiveLocationText.text = objectiveLocation;

            newObjectiveSlotInfo.objectiveMenuIconElementManager = currentobjectiveMenuIconElement;

            newObjectiveSlotInfo.objectiveSlotGameObject = newObjectiveSlotPrefab;
            newObjectiveSlotInfo.objectiveName = objectiveName;
            newObjectiveSlotInfo.objectiveLocation = objectiveLocation;
            newObjectiveSlotInfo.objectiveRewards = objectiveRewards;
            newObjectiveSlotInfo.objectiveDescription = objectiveDescription;
            newObjectiveSlotInfo.objectiveFullDescription = objectiveFullDescription;

            newObjectiveSlotInfo.currentObjectiveIcon = currentobjectiveMenuIconElement.currentObjectiveIcon;
            newObjectiveSlotInfo.objectiveCompletePanel = currentobjectiveMenuIconElement.objectiveCompletePanel;
            newObjectiveSlotInfo.selectedObjectiveIcon = currentobjectiveMenuIconElement.selectedObjectiveIcon;
            newObjectiveSlotInfo.objectiveCompleteText = currentobjectiveMenuIconElement.objectiveCompleteText;

            newObjectiveSlotInfo.addObjectiveToPlayerLogSystem = addObjectiveToPlayerLogSystem;
        }

        if (currentObjectiveEventSystem != null) {
            newObjectiveSlotInfo.missionID = currentObjectiveEventSystem.missionID;

            newObjectiveSlotInfo.disableObjectivePanelOnMissionComplete = currentObjectiveEventSystem.disableObjectivePanelOnMissionComplete;

            newObjectiveSlotInfo.missionScene = currentObjectiveEventSystem.missionScene;
            newObjectiveSlotInfo.objectiveEventManager = currentObjectiveEventSystem;
            newObjectiveSlotInfo.missionAccepted = true;

            currentObjectiveEventSystem.setMissionAcceptedState (true);

            if (showDebugPrint) {
                print (currentObjectiveEventSystem.objectiveInfoList.Count + " " + currentObjectiveEventSystem.generalObjectiveName);
            }

            for (int i = 0; i < currentObjectiveEventSystem.objectiveInfoList.Count; i++) {
                bool subObjectiveComplete = currentObjectiveEventSystem.objectiveInfoList [i].subObjectiveComplete;

                newObjectiveSlotInfo.subObjectiveCompleteList.Add (subObjectiveComplete);
            }
        }

        if (!addObjectiveToPlayerLogSystem) {
            if (newObjectiveSlotInfo.objectiveSlotGameObject.activeSelf) {
                newObjectiveSlotInfo.objectiveSlotGameObject.SetActive (false);
            }
        }

        if (addNewObjectivePanel) {
            objectiveSlotInfoList.Add (newObjectiveSlotInfo);

            if (mainUIPage != null) {
                mainUIPage.registerNewItem (newObjectiveSlotInfo.objectiveSlotGameObject);
            }
        }

        if (showDebugPrint) {
            print ("Activating mission " + objectiveName);
        }
    }

    public void removeObjectiveSlotFromMenu (int missionID, int missionScene)
    {
        int objectiveSlotInfoListCount = objectiveSlotInfoList.Count;

        for (int k = 0; k < objectiveSlotInfoListCount; k++) {
            objectiveSlotInfo currentObjectiveSlotInfo = objectiveSlotInfoList [k];

            if (currentObjectiveSlotInfo.missionID == missionID && currentObjectiveSlotInfo.missionScene == missionScene) {
                if (currentObjectiveSlotInfo.objectiveSlotGameObject != null) {
                    Destroy (currentObjectiveSlotInfo.objectiveSlotGameObject);

                    objectiveSlotInfoList.RemoveAt (k);

                    currentObjectiveSlot = null;

                    setButtonsColor (false, false);

                    updateObjectiveTextContent ("", "", "", "");
                }

                return;
            }
        }
    }
    public void openOrCloseObjectiveMenu (bool state)
    {
        if (state) {
            if (!pauseManager.checkIfMenuCanBeUsedByName (menuPanelName)) {
                return;
            }
        }

        bool checkResult =

            (!playerControllerManager.isPlayerMenuActive () || objectiveMenuOpened) &&

            (!playerControllerManager.isUsingDevice () || playerControllerManager.isPlayerDriving ()) &&

            !pauseManager.isGamePaused ();

        if (checkResult) {
            objectiveMenuOpened = state;

            pauseManager.openOrClosePlayerMenu (objectiveMenuOpened, objectiveMenuGameObject.transform, true);

            if (objectiveMenuGameObject.activeSelf != objectiveMenuOpened) {
                objectiveMenuGameObject.SetActive (objectiveMenuOpened);
            }

            pauseManager.setIngameMenuOpenedState ("Objective Log System", objectiveMenuOpened, true);

            pauseManager.enableOrDisablePlayerMenu (objectiveMenuOpened, true, false);

            if (currentObjectiveSlot != null && currentObjectiveSlot.selectedObjectiveIcon.activeSelf) {
                currentObjectiveSlot.selectedObjectiveIcon.SetActive (false);
            }

            currentObjectiveSlot = null;

            setButtonsColor (false, false);

            updateObjectiveTextContent ("", "", "", "");

            if (objectiveMenuOpened) {
                resetScrollBarPosition (missionListScrollbar, ref missionListScrollbarCoroutine);

                resetScrollBarPosition (missionInfoScrollbar, ref missionInfoScrollbarCoroutine);

                resetScrollBarPosition (missionRewardScrollbar, ref missionRewardScrollbarCoroutine);

                resetPanelSlotsRectTransform (objectiveListContent, objectiveListContent.GetComponent<ScrollRect> ());
            }
        }
    }

    public void checkOpenOrCloseObjectiveMenuFromTouch ()
    {
        if (pauseManager.isOpenOrClosePlayerOpenMenuByNamePaused ()) {
            return;
        }

        openOrCLoseObjectiveMenuFromTouch ();
    }

    public void openOrCLoseObjectiveMenuFromTouch ()
    {
        openOrCloseObjectiveMenu (!objectiveMenuOpened);
    }

    public void inputOpenOrCloseObjectiveMenu ()
    {
        if (!openMenuEnabled) {
            return;
        }

        if (objectiveMenuActive) {
            if (pauseManager.isOpenOrClosePlayerOpenMenuByNamePaused ()) {
                return;
            }

            openOrCloseObjectiveMenu (!objectiveMenuOpened);
        }
    }

    public void resetScrollBarPosition (Scrollbar scrollBarToReset, ref Coroutine scrollBarCoroutine)
    {
        if (scrollBarToReset != null) {
            if (scrollBarCoroutine != null) {
                StopCoroutine (scrollBarCoroutine);
            }

            scrollBarCoroutine = StartCoroutine (resetScroll (scrollBarToReset));
        }
    }

    private IEnumerator resetScroll (Scrollbar scrollBarToReset)
    {
        yield return new WaitForEndOfFrame ();
        yield return new WaitForEndOfFrame ();

        scrollBarToReset.value = 1;
    }

    public void resetPanelSlotsRectTransform (Transform panelTransform, ScrollRect panelScrollRect)
    {
        StartCoroutine (resetRectTransformCoroutine (panelTransform, panelScrollRect));
    }

    IEnumerator resetRectTransformCoroutine (Transform panelTransform, ScrollRect panelScrollRect)
    {
        LayoutRebuilder.ForceRebuildLayoutImmediate (panelTransform.GetComponent<RectTransform> ());

        yield return new WaitForEndOfFrame ();
        yield return new WaitForEndOfFrame ();

        if (panelScrollRect != null) {
            panelScrollRect.verticalNormalizedPosition = 1;
        }
    }

    public bool isObjectiveMenuActive ()
    {
        return objectiveMenuActive;
    }

    public bool isObjectiveInProcess ()
    {
        return objectiveInProcess;
    }

    public bool isCheckMinLevelOnMissionsEnabled ()
    {
        return checkMinLevelOnMissions;
    }

    public void setObtainedRewardState (int missionID, int missionScene, bool state)
    {
        int objectiveSlotInfoListCount = objectiveSlotInfoList.Count;

        for (int k = 0; k < objectiveSlotInfoListCount; k++) {
            objectiveSlotInfo currentObjectiveSlotInfo = objectiveSlotInfoList [k];

            if (currentObjectiveSlotInfo.missionID == missionID && currentObjectiveSlotInfo.missionScene == missionScene) {
                currentObjectiveSlotInfo.rewardObtained = state;
            }
        }
    }

    public void setSubObjectiveCompleteState (int missionID, int missionScene, int subObjectiveIndex, bool state)
    {
        int objectiveSlotInfoListCount = objectiveSlotInfoList.Count;

        for (int k = 0; k < objectiveSlotInfoListCount; k++) {
            objectiveSlotInfo currentObjectiveSlotInfo = objectiveSlotInfoList [k];

            if (currentObjectiveSlotInfo.missionID == missionID && currentObjectiveSlotInfo.missionScene == missionScene) {
                if (showDebugPrint) {
                    print (objectiveSlotInfoList [k].subObjectiveCompleteList.Count + " " + subObjectiveIndex);
                }

                if (currentObjectiveSlotInfo.subObjectiveCompleteList.Count > subObjectiveIndex) {
                    currentObjectiveSlotInfo.subObjectiveCompleteList [subObjectiveIndex] = state;
                }
            }
        }
    }

    public void setObjectiveMenuActiveState (bool state)
    {
        objectiveMenuActive = state;
    }

    public void setOpenMenuEnabled (bool state)
    {
        openMenuEnabled = state;
    }

    //EDITOR FUNCTIONS
    public void setObjectiveMenuActiveStateFromEditor (bool state)
    {
        setObjectiveMenuActiveState (state);

        updateComponent ();
    }

    public void setOpenMenuEnabledFromEditor (bool state)
    {
        setOpenMenuEnabled (state);

        updateComponent ();
    }

    void updateComponent ()
    {
        GKC_Utils.updateComponent (this);

        GKC_Utils.updateDirtyScene ("Update objective log system", gameObject);
    }

    [System.Serializable]
    public class objectiveSlotInfo
    {
        public GameObject objectiveSlotGameObject;
        public string objectiveName;
        public string objectiveDescription;
        public string objectiveFullDescription;
        public string objectiveLocation;
        public string objectiveRewards;

        public GameObject currentObjectiveIcon;
        public GameObject objectiveCompletePanel;
        public GameObject selectedObjectiveIcon;
        public GameObject objectiveCompleteText;
        public GameObject objectiveAcceptedText;

        public bool disableObjectivePanelOnMissionComplete;

        public bool addObjectiveToPlayerLogSystem;

        public bool slotSelectedByPlayer;

        public bool missionComplete;
        public bool missionInProcess;
        public bool rewardObtained;
        public bool missionAccepted;

        public int missionScene;
        public int missionID;

        public objectiveMenuIconElement objectiveMenuIconElementManager;

        public objectiveEventSystem objectiveEventManager;

        public List<bool> subObjectiveCompleteList = new List<bool> ();
    }
}
