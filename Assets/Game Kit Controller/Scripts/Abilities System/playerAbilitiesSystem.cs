using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class playerAbilitiesSystem : MonoBehaviour
{
    [Header ("Main Settings")]
    [Space]

    public bool abilitiesSystemEnabled = true;

    public int currentAbilityIndex;

    public bool disableAbilitySystemOnFirstPerson;

    [Space]
    [Header ("Energy Settings")]
    [Space]

    public string energyStatName = "Current Energy";

    public bool ignoreUseOfEnergy;

    [Space]
    [Header ("Abilities List")]
    [Space]

    public List<abilityInfo> abilityInfoList = new List<abilityInfo> ();

    [Space]
    [Header ("Debug")]
    [Space]

    public bool showDebugPrint;

    public bool abilitiesModeActive;

    public bool canMove;
    public bool playerCurrentlyBusy;
    public abilityInfo currentAbilityInfo;
    public bool cancellingCurrentAbilityPress;
    public string currentAbilityName;

    public string previousAbilityActiveName;

    public bool abilityInputInUse;

    public bool pauseCheckUsingDeviceOnAbilityInput;

    public bool inputPaused;

    public bool selectingAbility;

    [Space]
    [Header ("Abilities Components")]
    [Space]

    public playerController mainPlayerController;
    public playerStatsSystem mainPlayerStatsSystem;

    public playerAbilitiesUISystem mainPlayerAbilitiesUISystem;


    int energyStatIndex;

    bool mainPlayerAbilitiesUISystemAssigned;

    bool ignoreMenusCheckActive;

    void Start ()
    {
        if (abilitiesSystemEnabled) {
            mainPlayerAbilitiesUISystemAssigned = mainPlayerAbilitiesUISystem != null;

            energyStatIndex = mainPlayerStatsSystem.getStatValueIndex (energyStatName);

            bool setFirstAbilityAvailable = false;

            if (currentAbilityIndex < abilityInfoList.Count && currentAbilityIndex > 0) {
                if (abilityInfoList [currentAbilityIndex].isAbilityEnabled ()) {
                    setCurrentAbilityByName (abilityInfoList [currentAbilityIndex].Name);
                } else {
                    setFirstAbilityAvailable = true;
                }
            } else {
                setFirstAbilityAvailable = true;
            }

            if (setFirstAbilityAvailable) {
                int abilityInfoListCount = abilityInfoList.Count;

                for (int i = 0; i < abilityInfoListCount; i++) {
                    abilityInfo abilityToCheck = abilityInfoList [i];

                    if (abilityToCheck.isAbilityEnabled ()) {
                        setCurrentAbilityByName (abilityToCheck.Name);

                        return;
                    }
                }
            }
        }
    }

    public bool getCanMove ()
    {
        canMove = (!mainPlayerController.isPlayerDead () && mainPlayerController.canPlayerMove ());

        return canMove;
    }

    public void setNextAbility ()
    {
        if (!abilitiesSystemEnabled) {
            return;
        }

        if (inputPaused) {
            return;
        }

        playerCurrentlyBusy = playerIsBusy ();

        if (playerCurrentlyBusy) {
            return;
        }

        int loopCount = 0;

        bool nextAbilityFound = false;

        int abilityInfoListCount = abilityInfoList.Count;

        while (!nextAbilityFound) {

            currentAbilityIndex++;

            if (currentAbilityIndex >= abilityInfoListCount) {
                currentAbilityIndex = 0;
            }

            if (abilityInfoList [currentAbilityIndex].isAbilityEnabled ()) {
                nextAbilityFound = true;

                setCurrentAbilityByName (abilityInfoList [currentAbilityIndex].Name);

                if (mainPlayerAbilitiesUISystemAssigned) {
                    mainPlayerAbilitiesUISystem.updateCurrentSelectedAbilityUIByName (currentAbilityName);
                }
            }

            loopCount++;

            if (loopCount > abilityInfoListCount * 3) {
                nextAbilityFound = true;
            }
        }
    }

    public void setPreviousAbility ()
    {
        if (!abilitiesSystemEnabled) {
            return;
        }

        if (inputPaused) {
            return;
        }

        playerCurrentlyBusy = playerIsBusy ();

        if (playerCurrentlyBusy) {
            return;
        }

        int loopCount = 0;

        bool nextAbilityFound = false;

        int abilityInfoListCount = abilityInfoList.Count;

        while (!nextAbilityFound) {

            currentAbilityIndex--;

            if (currentAbilityIndex < 0) {
                currentAbilityIndex = abilityInfoListCount - 1;
            }

            if (abilityInfoList [currentAbilityIndex].isAbilityEnabled ()) {
                nextAbilityFound = true;

                setCurrentAbilityByName (abilityInfoList [currentAbilityIndex].Name);

                if (mainPlayerAbilitiesUISystemAssigned) {
                    mainPlayerAbilitiesUISystem.updateCurrentSelectedAbilityUIByName (currentAbilityName);
                }
            }

            loopCount++;

            if (loopCount > abilityInfoListCount * 3) {
                nextAbilityFound = true;
            }
        }
    }

    public void setCurrentAbilityByName (string abilityName)
    {
        if (currentAbilityInfo != null) {
            if (abilityName.Equals (currentAbilityName)) {
                return;
            }

            if (currentAbilityInfo.avoidToUseOtherAbilitiesWhileLimitActive && currentAbilityInfo.isTimeLimitInProcess ()) {
                if (showDebugPrint) {
                    print ("ability in process, can't user others in the meantime");
                }

                return;
            }
        }

        setCurrentAbilityByNameWithoutChecks (abilityName);
    }

    void setCurrentAbilityByNameWithoutChecks (string abilityName)
    {
        int abilityInfoListCount = abilityInfoList.Count;

        for (int i = 0; i < abilityInfoListCount; i++) {
            abilityInfo abilityToCheck = abilityInfoList [i];

            if (abilityToCheck.isAbilityEnabled ()) {
                if (abilityToCheck.Name.Equals (abilityName)) {

                    currentAbilityInfo = abilityToCheck;

                    currentAbilityInfo.isCurrentAbility = true;

                    currentAbilityInfo.enableAbility ();

                    currentAbilityInfo.checkEventOnSetCurrentAbility ();

                    currentAbilityIndex = i;

                    currentAbilityName = currentAbilityInfo.Name;
                } else {
                    if (abilityToCheck.deactivateAbilityWhenChangeToAnother) {
                        if (abilityToCheck.isCurrentAbility) {
                            abilityToCheck.deactivateAbility ();

                            abilityToCheck.checkEventToDeactivateAbility ();
                        }

                        abilityToCheck.stopActivateUpdateAbility ();

                        abilityToCheck.disableAbilityCurrentActiveFromPressState ();
                    }

                    abilityToCheck.isCurrentAbility = false;
                }
            }
        }
    }

    public void removeCurrentAbilityInfo ()
    {
        currentAbilityInfo = null;
        currentAbilityIndex = -1;
    }

    public void disableAbilityGroupByName (List<string> abilityNameList)
    {
        enableOrDisableAbilityGroupByName (abilityNameList, false);
    }

    public void enableAbilityGroupByName (List<string> abilityNameList)
    {
        enableOrDisableAbilityGroupByName (abilityNameList, true);
    }

    public void enableOrDisableAbilityGroupByName (List<string> abilityNameList, bool state)
    {
        for (int i = 0; i < abilityNameList.Count; i++) {
            if (state) {
                enableAbilityByName (abilityNameList [i]);
            } else {
                disableAbilityByName (abilityNameList [i]);
            }
        }
    }

    public void enableAbilityByName (string abilityName)
    {
        int abilityInfoListCount = abilityInfoList.Count;

        for (int i = 0; i < abilityInfoListCount; i++) {
            abilityInfo abilityToCheck = abilityInfoList [i];

            if (abilityToCheck.Name.Equals (abilityName)) {
                abilityToCheck.setAbilityEnabledState (true);

                abilityToCheck.abilityCanBeShownOnWheelSelection = true;

                abilityToCheck.enableAbility ();

                if (showDebugPrint) {
                    print ("enable ability " + abilityName);
                }

                //				if (mainPlayerAbilitiesUISystemAssigned) {
                //					if (mainPlayerAbilitiesUISystem.showOnlyEnabledAbilitySlots) {
                //						mainPlayerAbilitiesUISystem.updateNumberOfAbilitySlotsWithAbilitiesAmount ();
                //					}
                //				}

                return;
            }
        }
    }

    public void disableAbilityByName (string abilityName)
    {
        int abilityInfoListCount = abilityInfoList.Count;

        for (int i = 0; i < abilityInfoListCount; i++) {
            abilityInfo abilityToCheck = abilityInfoList [i];

            if (abilityToCheck.Name.Equals (abilityName)) {
                abilityToCheck.setAbilityEnabledState (false);

                abilityToCheck.abilityCanBeShownOnWheelSelection = false;

                abilityToCheck.disableAbility ();

                abilityToCheck.isCurrentAbility = false;
                abilityToCheck.stopActivateUpdateAbility ();

                abilityToCheck.disableAbilityCurrentActiveFromPressState ();

                //				if (mainPlayerAbilitiesUISystemAssigned) {
                //					if (mainPlayerAbilitiesUISystem.showOnlyEnabledAbilitySlots) {
                //						mainPlayerAbilitiesUISystem.updateNumberOfAbilitySlotsWithAbilitiesAmount ();
                //					}
                //				}

                if (showDebugPrint) {
                    print ("disable ability " + abilityName);
                }

                if (mainPlayerAbilitiesUISystemAssigned) {
                    mainPlayerAbilitiesUISystem.updateDpadIconsOnAbilitiesDeactivated ();
                }

                if (currentAbilityInfo == abilityToCheck) {
                    if (showDebugPrint) {
                        print ("disabling current ability " + currentAbilityInfo.Name);
                    }

                    setFirstAbilityAvailable ();

                    if (mainPlayerAbilitiesUISystemAssigned) {
                        if (currentAbilityInfo != null && currentAbilityInfo.isAbilityEnabled ()) {
                            mainPlayerAbilitiesUISystem.setCurrentAbilityIconAssignedByName (currentAbilityInfo.Name);
                        }
                    }
                }

                return;
            }
        }
    }

    void setFirstAbilityAvailable ()
    {
        int abilityInfoListCount = abilityInfoList.Count;

        for (int i = 0; i < abilityInfoListCount; i++) {
            abilityInfo abilityToCheck = abilityInfoList [i];

            if (abilityToCheck.isAbilityEnabled ()) {
                setCurrentAbilityByName (abilityToCheck.Name);

                return;
            }
        }
    }

    public void disableAllAbilites ()
    {
        int abilityInfoListCount = abilityInfoList.Count;

        for (int i = 0; i < abilityInfoListCount; i++) {
            abilityInfo abilityToCheck = abilityInfoList [i];

            abilityToCheck.setAbilityEnabledState (false);

            abilityToCheck.abilityCanBeShownOnWheelSelection = false;

            abilityToCheck.disableAbility ();

            abilityToCheck.isCurrentAbility = false;

            abilityToCheck.stopActivateUpdateAbility ();

            abilityToCheck.disableAbilityCurrentActiveFromPressState ();
        }

        //		if (mainPlayerAbilitiesUISystemAssigned) {
        //			if (mainPlayerAbilitiesUISystem.showOnlyEnabledAbilitySlots) {
        //				mainPlayerAbilitiesUISystem.updateNumberOfAbilitySlotsWithAbilitiesAmount ();
        //			}
        //		}

        if (mainPlayerAbilitiesUISystemAssigned) {
            mainPlayerAbilitiesUISystem.updateDpadIconsOnAbilitiesDeactivated ();
        }

        previousAbilityActiveName = "";
    }

    public void deactivateAllAbilites ()
    {
        deactivateAllAbilities (false);
    }

    public void deactivateAllCurrentlyActiveAbilities ()
    {
        deactivateAllAbilities (true);
    }

    void deactivateAllAbilities (bool disableOnlyIfActive)
    {
        int abilityInfoListCount = abilityInfoList.Count;

        for (int i = 0; i < abilityInfoListCount; i++) {
            abilityInfo abilityToCheck = abilityInfoList [i];

            bool canDisableResult = false;

            if (abilityToCheck.isCurrentAbility || abilityToCheck.abilityCurrentlyActiveFromPressDown) {
                canDisableResult = true;
            }

            if (!disableOnlyIfActive) {
                canDisableResult = true;
            }

            if (canDisableResult) {
                abilityToCheck.deactivateAbility ();

                abilityToCheck.isCurrentAbility = false;
                abilityToCheck.stopActivateUpdateAbility ();

                abilityToCheck.disableAbilityCurrentActiveFromPressState ();

                if (abilityToCheck.isTimeLimitInProcess ()) {
                    if (abilityToCheck.useCoolDown) {
                        abilityToCheck.activateCoolDown ();
                    } else {
                        abilityToCheck.stopActivateTimeLimit ();
                    }
                }

                abilityToCheck.checkEventToDeactivateAbility ();
            }
        }
    }

    public void deactivateAbilityByName (string abilityName)
    {
        int abilityInfoListCount = abilityInfoList.Count;

        for (int i = 0; i < abilityInfoListCount; i++) {
            abilityInfo abilityToCheck = abilityInfoList [i];

            if (abilityToCheck.Name.Equals (abilityName)) {

                abilityToCheck.deactivateAbility ();

                abilityToCheck.isCurrentAbility = false;
                abilityToCheck.stopActivateUpdateAbility ();

                abilityToCheck.disableAbilityCurrentActiveFromPressState ();

                if (abilityToCheck.isTimeLimitInProcess ()) {
                    if (abilityToCheck.useCoolDown) {
                        abilityToCheck.activateCoolDown ();
                    } else {
                        abilityToCheck.stopActivateTimeLimit ();
                    }
                }

                abilityToCheck.checkEventToDeactivateAbility ();

                return;
            }
        }
    }

    public void setPauseCheckUsingDeviceOnAbilityInputState (bool state)
    {
        pauseCheckUsingDeviceOnAbilityInput = state;
    }

    public void togglePauseCheckUsingDeviceOnAbilityInputState ()
    {
        setPauseCheckUsingDeviceOnAbilityInputState (!pauseCheckUsingDeviceOnAbilityInput);
    }

    public void activateCustomAction (string actionName)
    {
        mainPlayerController.activateCustomAction (actionName);
    }

    public bool playerIsBusy ()
    {
        if (!pauseCheckUsingDeviceOnAbilityInput && mainPlayerController.isUsingDevice ()) {
            return true;
        }

        if (isActionActive ()) {
            bool checkActionActiveResult = true;

            if (currentAbilityInfo != null) {
                if (currentAbilityInfo.canStopAbilityIfActionActive) {
                    if (currentAbilityInfo.updateAbilityInProcess) {
                        checkActionActiveResult = false;
                    }
                }
            }

            if (checkActionActiveResult) {
                return true;
            }
        }

        if (mainPlayerController.isTurnBasedCombatActionActive ()) {
            return false;
        }

        if (!ignoreMenusCheckActive) {
            if (mainPlayerController.isUsingSubMenu ()) {
                return true;
            }

            if (mainPlayerController.isPlayerMenuActive ()) {
                return true;
            }
        }

        return false;
    }

    public bool isActionActive ()
    {
        if (mainPlayerController.isActionActive ()) {
            return true;
        }

        return false;
    }

    public bool isPlayerCurrentlyBusy ()
    {
        return playerIsBusy ();
    }

    bool canUseInput ()
    {
        if (mainPlayerController.iscloseCombatAttackInProcess ()) {
            return false;
        }

        return true;
    }

    public bool checkIfAbilityCanBeActivated ()
    {
        if (currentAbilityInfo != null) {
            if (!currentAbilityInfo.canBeUsedOnlyOnGround || mainPlayerController.isPlayerOnGround ()) {
                return true;
            }
        }

        return false;
    }

    public int getNumberOfAbilitiesAvailable ()
    {
        int numberOfAbilitiesAvailable = 0;

        int abilityInfoListCount = abilityInfoList.Count;

        for (int i = 0; i < abilityInfoListCount; i++) {
            if (abilityInfoList [i].isAbilityEnabled ()) {
                numberOfAbilitiesAvailable++;
            }
        }

        return numberOfAbilitiesAvailable;
    }

    public int getNumberOfAbilitiesCanBeShownOnWheelSelection ()
    {
        int numberOfAbilitiesAvailable = 0;

        int abilityInfoListCount = abilityInfoList.Count;

        for (int i = 0; i < abilityInfoListCount; i++) {
            if (abilityInfoList [i].abilityCanBeShownOnWheelSelection) {
                numberOfAbilitiesAvailable++;
            }
        }

        return numberOfAbilitiesAvailable;
    }

    public int getCurrentAbilityIndex ()
    {
        int abilityInfoListCount = abilityInfoList.Count;

        for (int i = 0; i < abilityInfoListCount; i++) {
            if (abilityInfoList [i].isCurrentAbility) {
                return i;
            }
        }

        return -1;
    }

    public int getCurrentAbilityIndexValue ()
    {
        return currentAbilityIndex;
    }

    public string getCurrentAbilityName ()
    {
        return currentAbilityName;
    }

    public bool checkIfAbilitiesAvailable ()
    {
        int abilityInfoListCount = abilityInfoList.Count;

        for (int i = 0; i < abilityInfoListCount; i++) {
            if (abilityInfoList [i].isAbilityEnabled ()) {
                return true;
            }
        }

        return false;
    }

    public abilityInfo getAbilityByName (string abilityName)
    {
        int abilityInfoListCount = abilityInfoList.Count;

        for (int i = 0; i < abilityInfoListCount; i++) {
            abilityInfo abilityToCheck = abilityInfoList [i];

            if (abilityToCheck.Name.Equals (abilityName)) {

                return abilityToCheck;
            }
        }

        return null;
    }

    public bool isAbilityInputInUse ()
    {
        return abilityInputInUse;
    }

    public bool isAbilitiesModeActive ()
    {
        return abilitiesModeActive;
    }

    public void setAbilitesModeActiveState (bool state)
    {
        abilitiesModeActive = state;
    }

    public void usePowerBar (float powerAmount)
    {
        mainPlayerStatsSystem.usePlayerStatByIndex (energyStatIndex, powerAmount);
    }

    public bool isThereEnergyAvailable ()
    {
        return mainPlayerStatsSystem.getStatValueByIndex (energyStatIndex) > 0;
    }

    public bool checkIfAbilityNeedsEnergy ()
    {
        if (ignoreUseOfEnergy) {
            return true;
        }

        if (!currentAbilityInfo.useEnergyOnAbility) {
            return true;
        } else {
            float currentEnergyAmount = mainPlayerStatsSystem.getStatValueByIndex (energyStatIndex);

            if (currentEnergyAmount >= currentAbilityInfo.energyAmountUsed) {
                if (currentAbilityInfo.useEnergyWithRate) {
                    return true;
                } else {
                    return true;
                }
            }
        }

        return false;
    }

    public void checkAbilityUseEnergy ()
    {
        if (ignoreUseOfEnergy) {
            return;
        }

        if (currentAbilityInfo.useEnergyOnAbility && !currentAbilityInfo.useEnergyWithRate) {
            usePowerBar (currentAbilityInfo.energyAmountUsed);
        }
    }

    public bool checkAbilityUseEnergyInUpdate (bool useEnergyOnAbility, bool useEnergyWithRate, float energyAmountUsed)
    {
        if (ignoreUseOfEnergy) {
            return true;
        }

        if (useEnergyOnAbility && useEnergyWithRate) {
            if (isThereEnergyAvailable ()) {
                usePowerBar (energyAmountUsed);
            } else {
                return false;
            }
        }

        return true;
    }

    public void cancelPressAbility ()
    {
        cancellingCurrentAbilityPress = true;
    }

    public bool checkIfAbilitiesOnUseOrCooldown (string abilityName)
    {
        int abilityIndex = abilityInfoList.FindIndex (a => a.Name == abilityName);

        if (abilityIndex > -1) {
            abilityInfo abilityToCheck = abilityInfoList [abilityIndex];

            if (abilityToCheck.useCoolDown && abilityToCheck.isCoolDownInProcess ()) {
                return true;
            }

            if (abilityToCheck.useTimeLimit && abilityToCheck.isTimeLimitInProcess ()) {
                return true;
            }
        }

        return false;
    }

    public void setInputPausedState (bool state)
    {
        inputPaused = state;
    }

    //Activate abilities externally without including the ability on the wheel menu, so it can be used separately
    public void inputSelectAndPressDownNewAbility (string abilityName)
    {
        if (inputPaused) {
            return;
        }

        if (!canUseInput ()) {
            return;
        }

        setCurrentAbilityByName (abilityName);

        inputPressDownUseCurrentAbility ();
    }

    public void inputSelectAndPressDownNewAbilityTemporally (string abilityName, bool abilityIsTemporallyActivated, bool ignoreCheckMenusState)
    {
        if (inputPaused) {
            return;
        }

        if (!canUseInput ()) {
            return;
        }

        if (showDebugPrint) {
            print ("inputSelectAndPressDownNewAbilityTemporally " + abilityName);
        }

        ignoreMenusCheckActive = ignoreCheckMenusState;

        if (abilityIsTemporallyActivated) {
            inputSelectAndPressDownNewSeparatedAbility (abilityName);

            checkPreviousAbilityActive ();
        } else {
            inputSelectAndPressDownNewAbility (abilityName);
        }

        ignoreMenusCheckActive = false;

    }

    public void inputSelectAndPressDownNewSeparatedAbility (string abilityName)
    {
        if (inputPaused) {
            return;
        }

        if (!canUseInput ()) {
            return;
        }

        if (currentAbilityInfo != null) {
            if (abilityName != previousAbilityActiveName) {
                previousAbilityActiveName = currentAbilityInfo.Name;
            }
        }

        if (showDebugPrint) {
            print ("inputSelectAndPressDownNewSeparatedAbility " + abilityName);
        }

        inputSelectAndPressDownNewAbility (abilityName);
    }

    public void inputSelectAndPressHoldNewSeparatedAbility ()
    {
        if (inputPaused) {
            return;
        }

        if (!canUseInput ()) {
            return;
        }

        inputPressHoldUseCurrentAbility ();
    }

    public void inputSelectAndPressUpNewSeparatedAbility ()
    {
        if (inputPaused) {
            return;
        }

        if (!canUseInput ()) {
            return;
        }

        inputPressUpUseCurrentAbility ();

        checkPreviousAbilityActive ();
    }

    public void checkPreviousAbilityActive ()
    {
        if (previousAbilityActiveName != "") {
            setCurrentAbilityByNameWithoutChecks (previousAbilityActiveName);

            previousAbilityActiveName = "";
        }
    }

    public void setSelectingAbilityActiveState (bool state)
    {
        selectingAbility = state;
    }


    //Main ability input functions
    public void inputPressDownUseCurrentAbility ()
    {
        if (inputPaused) {
            return;
        }

        if (!canUseInput ()) {
            return;
        }

        abilityInputInUse = true;

        pressDownUseCurrentAbility ();
    }

    public void pressDownUseCurrentAbility ()
    {
        if (!abilitiesSystemEnabled) {
            return;
        }

        if (disableAbilitySystemOnFirstPerson && mainPlayerController.isPlayerOnFirstPerson ()) {
            return;
        }

        if (cancellingCurrentAbilityPress) {
            return;
        }

        if (currentAbilityInfo == null) {
            return;
        }

        playerCurrentlyBusy = playerIsBusy ();

        if (playerCurrentlyBusy) {
            bool cancelCheckPressDown = true;

            if (currentAbilityInfo.canUsePressDownInputIfPlayerBusy) {
                if (currentAbilityInfo.abilityCurrentlyActiveFromPressDown) {
                    cancelCheckPressDown = false;
                }
            }

            if (cancelCheckPressDown) {
                return;
            }
        }

        if (!currentAbilityInfo.isAbilityEnabled ()) {
            return;
        }

        if (!currentAbilityInfo.useInputOnPressDown) {
            return;
        }

        if (!checkIfAbilityCanBeActivated ()) {
            return;
        }

        if (currentAbilityInfo.useCoolDown && currentAbilityInfo.isCoolDownInProcess ()) {
            return;
        }

        if (currentAbilityInfo.useTimeLimit && currentAbilityInfo.isTimeLimitInProcess () && currentAbilityInfo.avoidAbilityInputWhileLimitActive) {
            return;
        }

        if (!checkIfAbilityNeedsEnergy () && !currentAbilityInfo.abilityCurrentlyActiveFromPressDown) {
            return;
        }

        if (selectingAbility) {
            return;
        }

        if (currentAbilityInfo.useConditionCheckToActivateAbilityOnPressDown) {
            currentAbilityInfo.eventToCallToCheckConditionOnPressDown.Invoke ();
        } else {
            confirmPressDownUseCurrentAbility ();
        }
    }

    public void confirmPressDownUseCurrentAbility ()
    {
        if (!currentAbilityInfo.abilityCurrentlyActiveFromPressDown || !currentAbilityInfo.useEnergyOnceOnPressDown) {
            if (currentAbilityInfo.useEnergyOnPressDown) {
                checkAbilityUseEnergy ();
            }
        }

        if (!currentAbilityInfo.ignoreSetAbilityCurrentlyActiveFromPressDown) {
            currentAbilityInfo.abilityCurrentlyActiveFromPressDown = !currentAbilityInfo.abilityCurrentlyActiveFromPressDown;
        }

        currentAbilityInfo.useAbilityPressDown ();

        currentAbilityInfo.activateUpdateAbility ();

        currentAbilityInfo.checkIfUseAnimationWithActionSystemOnPressDown ();

        if (currentAbilityInfo.disableAbilityInputInUseStateOnPressDown) {
            abilityInputInUse = false;
        }

        if (currentAbilityInfo.useTimeLimit && currentAbilityInfo.useTimeLimitOnPressDown) {
            if (currentAbilityInfo.abilityCurrentlyActiveFromPressDown) {
                if (currentAbilityInfo.useLimitWhenAbilityCurrentActiveFromPress) {
                    currentAbilityInfo.activateTimeLimit ();
                }
            } else {
                if (!currentAbilityInfo.useLimitWhenAbilityCurrentActiveFromPress) {
                    currentAbilityInfo.activateTimeLimit ();
                }
            }
        }

        if (currentAbilityInfo.useCoolDown && currentAbilityInfo.useCoolDownOnPressDown) {
            if (currentAbilityInfo.abilityCurrentlyActiveFromPressDown) {
                if (currentAbilityInfo.useCoolDownWhenAbilityCurrentlyActiveFromPress) {
                    currentAbilityInfo.activateCoolDown ();
                }
            } else {
                if (!currentAbilityInfo.useCoolDownWhenAbilityCurrentlyActiveFromPress) {
                    currentAbilityInfo.activateCoolDown ();
                }
            }
        }
    }

    public void inputPressHoldUseCurrentAbility ()
    {
        if (inputPaused) {
            return;
        }

        if (!canUseInput ()) {
            return;
        }

        pressHoldUseCurrentAbility ();
    }

    public void pressHoldUseCurrentAbility ()
    {
        if (!abilitiesSystemEnabled) {
            return;
        }

        if (disableAbilitySystemOnFirstPerson && mainPlayerController.isPlayerOnFirstPerson ()) {
            return;
        }

        if (cancellingCurrentAbilityPress) {
            return;
        }

        if (currentAbilityInfo == null) {
            return;
        }

        playerCurrentlyBusy = playerIsBusy ();

        if (playerCurrentlyBusy) {
            return;
        }

        if (!currentAbilityInfo.useInputOnPressHold) {
            return;
        }

        if (!checkIfAbilityCanBeActivated ()) {
            return;
        }

        if (!checkIfAbilityNeedsEnergy ()) {
            return;
        }

        if (selectingAbility) {
            return;
        }

        if (!currentAbilityInfo.isAbilityEnabled ()) {
            return;
        }

        if (currentAbilityInfo.useEnergyOnPressHold) {
            checkAbilityUseEnergy ();
        }

        currentAbilityInfo.useAbilityPressHold ();
    }

    public void inputPressUpUseCurrentAbility ()
    {
        if (inputPaused) {
            return;
        }

        if (!canUseInput ()) {
            return;
        }

        abilityInputInUse = false;

        pressUpUseCurrentAbility ();
    }

    public void pressUpUseCurrentAbility ()
    {
        if (!abilitiesSystemEnabled) {
            return;
        }

        if (disableAbilitySystemOnFirstPerson && mainPlayerController.isPlayerOnFirstPerson ()) {
            return;
        }

        if (cancellingCurrentAbilityPress) {
            cancellingCurrentAbilityPress = false;
            return;
        }

        if (currentAbilityInfo == null) {
            return;
        }

        playerCurrentlyBusy = playerIsBusy ();

        if (playerCurrentlyBusy) {
            return;
        }

        if (!currentAbilityInfo.isAbilityEnabled ()) {
            return;
        }

        if (!currentAbilityInfo.useInputOnPressUp) {
            return;
        }

        if (currentAbilityInfo.checkIfInputPressDownBeforeActivateUp && !currentAbilityInfo.abilityCurrentlyActiveFromPressDown) {
            return;
        }

        if (currentAbilityInfo.useCoolDown && currentAbilityInfo.isCoolDownInProcess ()) {
            return;
        }

        if (currentAbilityInfo.useTimeLimit && currentAbilityInfo.isTimeLimitInProcess () && currentAbilityInfo.avoidAbilityInputWhileLimitActive) {
            return;
        }

        if (!checkIfAbilityNeedsEnergy () && !currentAbilityInfo.abilityCurrentlyActiveFromPressUp) {
            return;
        }

        if (selectingAbility) {
            return;
        }

        if (currentAbilityInfo.useConditionCheckToActivateAbilityOnPressUp) {
            currentAbilityInfo.eventToCallToCheckConditionOnPressUp.Invoke ();
        } else {
            confirmPressUpUseCurrentAbility ();
        }
    }

    public void confirmPressUpUseCurrentAbility ()
    {
        if (currentAbilityInfo.abilityCurrentlyActiveFromPressUp || !currentAbilityInfo.useEnergyOnceOnPressUp) {
            if (currentAbilityInfo.useEnergyOnPressUp) {
                checkAbilityUseEnergy ();
            }
        }

        currentAbilityInfo.abilityCurrentlyActiveFromPressUp = !currentAbilityInfo.abilityCurrentlyActiveFromPressUp;

        currentAbilityInfo.useAbilityPressUp ();

        currentAbilityInfo.stopActivateUpdateAbility ();

        currentAbilityInfo.checkIfUseAnimationWithActionSystemOnPressUp ();

        if (currentAbilityInfo.useTimeLimit && currentAbilityInfo.useTimeLimitOnPressUp) {
            if (currentAbilityInfo.abilityCurrentlyActiveFromPressUp) {
                if (currentAbilityInfo.useLimitWhenAbilityCurrentActiveFromPress) {
                    currentAbilityInfo.activateTimeLimit ();
                }
            } else {
                if (!currentAbilityInfo.useLimitWhenAbilityCurrentActiveFromPress) {
                    currentAbilityInfo.activateTimeLimit ();
                }
            }
        }

        if (currentAbilityInfo.useCoolDown && currentAbilityInfo.useCoolDownOnPressUp) {
            if (currentAbilityInfo.abilityCurrentlyActiveFromPressUp) {
                if (currentAbilityInfo.useCoolDownWhenAbilityCurrentlyActiveFromPress) {
                    currentAbilityInfo.activateCoolDown ();
                }
            } else {
                if (!currentAbilityInfo.useCoolDownWhenAbilityCurrentlyActiveFromPress) {
                    currentAbilityInfo.activateCoolDown ();
                }
            }
        }
    }

    public void inputSetNextAbility ()
    {
        if (inputPaused) {
            return;
        }

        if (!canUseInput ()) {
            return;
        }

        setNextAbility ();
    }

    public void inputSetPreviousAbility ()
    {
        if (inputPaused) {
            return;
        }

        if (!canUseInput ()) {
            return;
        }

        setPreviousAbility ();
    }

    public void setAbilitiesSystemEnabledState (bool state)
    {
        abilitiesSystemEnabled = state;
    }

    public bool isAbilitiesSystemEnabled ()
    {
        return abilitiesSystemEnabled;
    }

    public void enableOrDisableAllAbilities (bool state)
    {
        int abilityInfoListCount = abilityInfoList.Count;

        for (int i = 0; i < abilityInfoListCount; i++) {
            if (state) {
                enableAbilityByName (abilityInfoList [i].Name);
            } else {
                disableAbilityByName (abilityInfoList [i].Name);
            }

        }
    }

    //EDITOR FUNCTIONS
    public void setAbilitiesSystemEnabledStateFromEditor (bool state)
    {
        setAbilitiesSystemEnabledState (state);

        updateComponent ();
    }

    public void enableOrDisableAllAbilitiesFromEditor (bool state)
    {
        int abilityInfoListCount = abilityInfoList.Count;

        for (int i = 0; i < abilityInfoListCount; i++) {
            abilityInfoList [i].enableOrDisableAbilityFromEditor (state);
        }

        updateComponent ();
    }

    public void enableOrDisableAllAbilitiesCanBeShownOnWheelFromEditor (bool state)
    {
        int abilityInfoListCount = abilityInfoList.Count;

        for (int i = 0; i < abilityInfoListCount; i++) {
            abilityInfoList [i].enableOrDisableAllAbilitiesCanBeShownOnWheelFromEditor (state);
        }

        updateComponent ();
    }

    public void setAbilitesModeActiveStateFromEditor (bool state)
    {
        setAbilitesModeActiveState (state);

        updateComponent ();
    }


    void updateComponent ()
    {
        GKC_Utils.updateComponent (this);

        GKC_Utils.updateDirtyScene ("Update All Abilities Enabled State", gameObject);
    }
}
