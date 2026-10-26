using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class AIAbilitiesSystemBrain : MonoBehaviour
{
    [Header ("Main Settings")]
    [Space]

    public bool systemEnabled = true;

    public float currentMinDistanceToAttack = 7;

    [Space]
    [Header ("Attack Settings")]
    [Space]

    public bool attackEnabled;

    public Vector2 randomAttackRangeRate;

    [Space]
    [Header ("Abilities Settings")]
    [Space]

    public bool changeAbilityAfterTimeEnabled;

    public bool changeAbilityRandomly;

    public Vector2 randomChangeAbilityRate;

    [Space]

    public bool useCustomListToChangeAbility;

    public List<string> customListToChangeAbility = new List<string> ();

    [Space]

    public bool useCustomListToChangeAbilityInfoList;

    public List<customListToChangeAbilityInfo> customListToChangeAbilityInfoList = new List<customListToChangeAbilityInfo> ();

    [Space]
    [Header ("Abilities List")]
    [Space]

    public int currentAbilityIndex;

    [Space]

    public List<AIAbilityInfo> AIAbilityInfoList = new List<AIAbilityInfo> ();

    [Space]
    [Space]

    public int currentAbilityStateIndex;

    [Space]

    public List<AIAbilityStatesInfo> AIAbilityStatesInfoList = new List<AIAbilityStatesInfo> ();

    [Space]
    [Header ("Other Settings")]
    [Space]

    public bool changeWaitToActivateAttackEnabled = true;

    public bool useWaitToActivateAttackActiveCounter;

    [Space]
    [Header ("Debug")]
    [Space]

    public bool showDebugPrint;

    public bool systemActive;

    public bool waitingForAttackActive;
    float currentRandomTimeToAttack;

    public bool canUseAttackActive;

    public bool attackStatePaused;

    public bool insideMinDistanceToAttack;

    public float currentAttackRate;

    public bool abilityInProcess;

    public bool onSpotted;

    public bool checkingToChangeAbilityActive;

    public bool waitToActivateAttackActive;

    public int waitToActivateAttackActiveCounter = 0;

    public bool turnBasedCombatActionActive;

    public float currentPathDistanceToTarget;

    [Space]
    [Header ("Events Settings")]
    [Space]

    public bool useEventsOnCombatActive;
    public UnityEvent eventOnCombatActive;
    public UnityEvent eventOnCombatDeactivate;

    [Space]
    [Header ("Components")]
    [Space]

    public findObjectivesSystem mainFindObjectivesSystem;
    public AINavMesh mainAINavmeshManager;
    public playerAbilitiesSystem mainPlayerAbilitiesSystem;
    public playerController mainPlayerController;


    float lastTimeAttack;

    int currentAttackTypeIndex;

    int currentAttackIndex;

    int currentAttackTypeToAlternateIndex;

    float currentPauseAttackStateDuration;
    float lastTimeAttackPauseWithDuration;

    float randomWaitTime;

    float lastTimeAttackActivated;

    bool AIPaused;

    AIAbilityInfo currentAIAbilityInfo;

    bool currentAIAbilityInfoAssigned;

    Coroutine abilityCoroutine;
    Coroutine pauseMainBehaviorCoroutine;

    float lastTimeChangeAbility;

    float timeToChangeAbility;

    AIAbilityStatesInfo currentAIAbilityStatesInfo;


    void Start ()
    {
        if (systemActive) {
            systemActive = false;

            setSystemActiveState (true);
        }
    }

    public void updateAI ()
    {
        if (systemActive) {
            AIPaused = mainFindObjectivesSystem.isAIPaused ();

            if (!AIPaused) {

            }
        }
    }

    public void resetStates ()
    {


    }

    public void updateInsideMinDistance (bool newInsideMinDistanceToAttack)
    {
        insideMinDistanceToAttack = newInsideMinDistanceToAttack;

        if (insideMinDistanceToAttack) {

        } else {

        }
    }

    public void updateBehavior ()
    {
        if (!systemActive) {
            return;
        }

        if (AIPaused) {
            return;
        }

        if (turnBasedCombatActionActive) {
            return;
        }

        checkAttackState ();
    }

    public void checkAttackState ()
    {
        if (!attackEnabled) {
            return;
        }

        insideMinDistanceToAttack = mainFindObjectivesSystem.insideMinDistanceToAttack;

        if (!insideMinDistanceToAttack && currentAIAbilityInfoAssigned && !currentAIAbilityInfo.ignoreInsideMinDistanceToAttackTarget) {
            return;
        }

        if (currentPauseAttackStateDuration > 0) {
            if (Time.time > currentPauseAttackStateDuration + lastTimeAttackPauseWithDuration) {

                attackStatePaused = false;

                currentPauseAttackStateDuration = 0;
            } else {
                return;
            }
        }

        if (waitToActivateAttackActive) {
            return;
        }

        if (turnBasedCombatActionActive) {
            return;
        }

        if (!canUseAttackActive) {
            return;
        }

        if (mainFindObjectivesSystem.isOnSpotted ()) {
            if (!onSpotted) {
                lastTimeAttackActivated = Time.time;

                onSpotted = true;
            }
        } else {
            if (onSpotted) {

                onSpotted = false;

                checkingToChangeAbilityActive = false;
            }
        }

        if (onSpotted) {
            if (changeAbilityAfterTimeEnabled && !abilityInProcess) {
                if (!checkingToChangeAbilityActive) {
                    lastTimeChangeAbility = Time.time;

                    checkingToChangeAbilityActive = true;

                    timeToChangeAbility = Random.Range (randomChangeAbilityRate.x, randomChangeAbilityRate.y);
                } else {
                    if (Time.time > lastTimeChangeAbility + timeToChangeAbility) {
                        setNextAbility ();
                    }
                }
            }

            if (Time.time > currentAttackRate + lastTimeAttackActivated && !abilityInProcess) {
                bool checkDistanceConditionResult = false;

                mainFindObjectivesSystem.updateCurrentPathDistanceToTargetValue ();

                currentPathDistanceToTarget = mainFindObjectivesSystem.currentPathDistanceToTarget;

                if (currentPathDistanceToTarget < 1) {
                    currentPathDistanceToTarget = mainFindObjectivesSystem.getDistanceToTarget ();
                }

                if (currentPathDistanceToTarget <= currentMinDistanceToAttack) {
                    if (currentAIAbilityInfo.checkVerticalDistanceToTarget) {
                        float verticalDistanceToCurrentEnemyPosition = mainFindObjectivesSystem.getVerticalDistanceToCurrentEnemyPosition ();

                        if (verticalDistanceToCurrentEnemyPosition != 0) {
                            if (verticalDistanceToCurrentEnemyPosition < currentAIAbilityInfo.maxVerticalDistanceToTarget) {
                                checkDistanceConditionResult = true;
                            }
                        }
                    } else {
                        checkDistanceConditionResult = true;
                    }
                }

                bool checkUseAbilityConditionResult = false;

                if (checkDistanceConditionResult) {
                    if (mainFindObjectivesSystem.checkIfMinimumAngleToAttack () &&
                        !mainPlayerController.isActionActive () &&
                        mainPlayerController.canPlayerMove ()) {

                        checkUseAbilityConditionResult = true;
                    }
                }

                if (checkUseAbilityConditionResult) {

                    bool canActivateAbilityResult = true;

                    if (currentAIAbilityInfo.useCustomAbilityDistance) {
                        if (currentPathDistanceToTarget > currentAIAbilityInfo.customAbilityDistance) {
                            canActivateAbilityResult = false;
                        }
                    }

                    if (canActivateAbilityResult) {
                        if (mainFindObjectivesSystem.isAIBehaviorAttackInProcess ()) {
                            if (showDebugPrint) {
                                print ("attack in process in current main behavior, cancelling ability action");
                            }

                            canActivateAbilityResult = false;
                        }
                    }

                    if (canActivateAbilityResult) {
                        if (mainFindObjectivesSystem.isAIPaused ()) {
                            canActivateAbilityResult = false;
                        }
                    }

                    if (canActivateAbilityResult) {
                        if (currentAIAbilityInfo.sendSignalToActivateAbilityEnabled) {
                            if (mainPlayerAbilitiesSystem.checkIfAbilitiesOnUseOrCooldown (currentAIAbilityInfo.Name)) {
                                canActivateAbilityResult = false;

                                if (showDebugPrint) {
                                    print ("current ability to use is on cool down, cancelling its usage " + currentAIAbilityInfo.Name);
                                }
                            }
                        }
                    }

                    if (canActivateAbilityResult) {
                        if (showDebugPrint) {
                            print (currentPathDistanceToTarget + " " + mainAINavmeshManager.getCurrentTarget ().name);
                        }

                        if (currentAIAbilityInfo.useProbabilityToUseAbility) {
                            float currentProbability = Random.Range (0, 100);

                            if (currentProbability > currentAIAbilityInfo.probabilityToUseAbility) {
                                lastTimeAttackActivated = Time.time;

                                if (showDebugPrint) {
                                    print ("probability to activate ability failed, cancelling " + currentProbability + " " + currentAIAbilityInfo.probabilityToUseAbility);
                                }

                                return;
                            }

                            if (showDebugPrint) {
                                print ("probability to activate ability reached, continuing " + currentProbability + " " + currentAIAbilityInfo.probabilityToUseAbility);
                            }
                        }

                        attackTarget ();

                        if (showDebugPrint) {
                            print ("activate ability attack " + currentAttackRate);
                        }

                        if (currentAIAbilityInfo.useCustomRandomAttackRangeRate) {
                            currentAttackRate = Random.Range (currentAIAbilityInfo.customRandomAttackRangeRate.x, currentAIAbilityInfo.customRandomAttackRangeRate.y);
                        } else {
                            currentAttackRate = Random.Range (randomAttackRangeRate.x, randomAttackRangeRate.y);
                        }
                    }
                }
            }
        }
    }

    public void setChangeAbilityAfterTimeEnabledState (bool state)
    {
        changeAbilityAfterTimeEnabled = state;
    }

    public void setNextAbility ()
    {
        bool newAbilityIndexFound = false;

        int previuosAbilityIndex = currentAbilityIndex;

        int newAbilityIndex = -1;

        int loopCount = 0;

        if (changeAbilityRandomly) {
            while (!newAbilityIndexFound) {

                if (useCustomListToChangeAbility) {
                    if (useCustomListToChangeAbilityInfoList) {
                        int currentIndex = customListToChangeAbilityInfoList.FindIndex (s => s.isCustomListActive == true);

                        if (currentIndex > -1) {
                            newAbilityIndex = Random.Range (0, customListToChangeAbilityInfoList [currentIndex].customListToChangeAbility.Count);

                            string temporalAbilityName = customListToChangeAbilityInfoList [currentIndex].customListToChangeAbility [newAbilityIndex];

                            newAbilityIndex = AIAbilityInfoList.FindIndex (s => s.Name.Equals (temporalAbilityName));

                            if (newAbilityIndex < 0) {
                                newAbilityIndex = 0;
                            }

                            if (showDebugPrint) {
                                print ("setting new ability index " + newAbilityIndex);
                            }
                        }
                    } else {
                        newAbilityIndex = Random.Range (0, customListToChangeAbility.Count);

                        string temporalAbilityName = customListToChangeAbility [newAbilityIndex];

                        newAbilityIndex = AIAbilityInfoList.FindIndex (s => s.Name.Equals (temporalAbilityName));

                        if (newAbilityIndex < 0) {
                            newAbilityIndex = 0;
                        }

                        if (showDebugPrint) {
                            print ("setting new ability index " + newAbilityIndex);
                        }
                    }
                } else {
                    newAbilityIndex = Random.Range (0, AIAbilityInfoList.Count);

                    if (showDebugPrint) {
                        print ("setting new ability index " + newAbilityIndex);
                    }
                }

                if (newAbilityIndex != currentAbilityIndex) {
                    if (AIAbilityInfoList [newAbilityIndex].useAbilityEnabled) {
                        newAbilityIndexFound = true;
                    }
                }

                loopCount++;

                if (loopCount > AIAbilityInfoList.Count * 4) {
                    newAbilityIndexFound = true;

                    newAbilityIndex = previuosAbilityIndex;
                }
            }
        } else {
            newAbilityIndex = currentAbilityIndex;

            while (!newAbilityIndexFound) {
                newAbilityIndex++;

                if (newAbilityIndex >= AIAbilityInfoList.Count) {
                    newAbilityIndex = 0;
                }

                if (AIAbilityInfoList [newAbilityIndex].useAbilityEnabled) {
                    newAbilityIndexFound = true;
                }

                loopCount++;

                if (loopCount > AIAbilityInfoList.Count * 4) {
                    newAbilityIndexFound = true;

                    newAbilityIndex = previuosAbilityIndex;
                }
            }
        }

        if (newAbilityIndex > -1) {
            setNewAbilityByName (AIAbilityInfoList [newAbilityIndex].Name);

            if (showDebugPrint) {
                print ("changing ability to " + AIAbilityInfoList [newAbilityIndex].Name);
            }
        }

        checkingToChangeAbilityActive = false;
    }

    public void updateAIAttackState (bool newCanUseAttackActiveState)
    {
        canUseAttackActive = newCanUseAttackActiveState;
    }

    public void setSystemEnabledState (bool state)
    {
        if (systemEnabled == state) {
            return;
        }

        systemEnabled = state;

        setSystemActiveState (systemEnabled);
    }

    public void setSystemActiveState (bool state)
    {
        if (!systemEnabled) {
            return;
        }

        if (systemActive == state) {
            return;
        }

        systemActive = state;

        if (systemActive) {
            lastTimeAttackActivated = Time.time;

            setNewAbilityByName (AIAbilityInfoList [currentAbilityIndex].Name);

            setNewAbilityStateByName (AIAbilityStatesInfoList [currentAbilityStateIndex].Name);

            if (currentAIAbilityInfo.useCustomRandomAttackRangeRate) {
                currentAttackRate = Random.Range (currentAIAbilityInfo.customRandomAttackRangeRate.x, currentAIAbilityInfo.customRandomAttackRangeRate.y);
            } else {
                currentAttackRate = Random.Range (randomAttackRangeRate.x, randomAttackRangeRate.y);
            }

        }

        onSpotted = false;

        checkingToChangeAbilityActive = false;

        checkEventsOnCombatStateChange (systemActive);

        waitToActivateAttackActive = false;

        waitToActivateAttackActiveCounter = 0;

        if (showDebugPrint) {
            print ("setSystemActiveState " + state);
        }
    }

    void checkEventsOnCombatStateChange (bool state)
    {
        if (useEventsOnCombatActive) {
            if (state) {
                eventOnCombatActive.Invoke ();
            } else {
                eventOnCombatDeactivate.Invoke ();
            }
        }
    }

    public void pauseAttackDuringXTime (float newDuration)
    {
        currentPauseAttackStateDuration = newDuration;

        lastTimeAttackPauseWithDuration = Time.time;

        attackStatePaused = true;
    }

    public void setWaitToActivateAttackActiveState (bool state)
    {
        if (!changeWaitToActivateAttackEnabled) {
            return;
        }

        if (useWaitToActivateAttackActiveCounter) {
            if (state) {
                waitToActivateAttackActiveCounter++;
            } else {
                waitToActivateAttackActiveCounter--;

                if (waitToActivateAttackActiveCounter < 0) {
                    waitToActivateAttackActiveCounter = 0;
                }
            }

            if (waitToActivateAttackActiveCounter > 0) {
                if (waitToActivateAttackActive) {
                    return;
                } else {
                    state = true;
                }
            } else {
                if (!waitToActivateAttackActive) {
                    return;
                } else {
                    state = false;
                }
            }
        }

        waitToActivateAttackActive = state;
    }

    public void resetBehaviorStates ()
    {
        resetStates ();

        waitingForAttackActive = false;



        insideMinDistanceToAttack = false;
    }

    public void attackTarget ()
    {
        if (showDebugPrint) {
            print ("activate ability attack");
        }

        stopPauseMainAttackBehaviorCoroutine ();

        pauseMainBehaviorCoroutine = StartCoroutine (pauseMainAttackBehaviorCoroutine ());

        abilityInProcess = true;

        if (currentAIAbilityInfo.abilityInputHoldType || currentAIAbilityInfo.abilityInputToggleType) {
            stopAbilityInputHoldCoroutine ();

            abilityCoroutine = StartCoroutine (abilityInputHoldCoroutine ());
        } else {
            if (currentAIAbilityInfo.sendSignalToActivateAbilityEnabled) {
                mainPlayerAbilitiesSystem.inputSelectAndPressDownNewAbility (currentAIAbilityInfo.Name);
            }

            if (currentAIAbilityInfo.useEventsOnAbility) {
                currentAIAbilityInfo.eventOnAbilityStart.Invoke ();
            }
        }
    }

    void stopAbilityInputHoldCoroutine ()
    {
        if (abilityCoroutine != null) {
            StopCoroutine (abilityCoroutine);
        }
    }

    IEnumerator abilityInputHoldCoroutine ()
    {
        if (currentAIAbilityInfo.sendSignalToActivateAbilityEnabled) {
            mainPlayerAbilitiesSystem.inputSelectAndPressDownNewSeparatedAbility (currentAIAbilityInfo.Name);
        }

        if (currentAIAbilityInfo.useEventsOnAbility) {
            currentAIAbilityInfo.eventOnAbilityStart.Invoke ();
        }

        WaitForSeconds delay = new WaitForSeconds (currentAIAbilityInfo.timeToHoldAndReleaseAbilityInput);

        yield return delay;

        if (currentAIAbilityInfo.sendSignalToActivateAbilityEnabled) {
            if (currentAIAbilityInfo.abilityInputHoldType) {
                mainPlayerAbilitiesSystem.inputSelectAndPressUpNewSeparatedAbility ();
            }

            if (currentAIAbilityInfo.abilityInputToggleType) {
                mainPlayerAbilitiesSystem.inputSelectAndPressDownNewSeparatedAbility (currentAIAbilityInfo.Name);
            }
        }

        if (currentAIAbilityInfo.useEventsOnAbility) {
            currentAIAbilityInfo.eventOnAbilityEnd.Invoke ();
        }

        yield return null;
    }

    void stopPauseMainAttackBehaviorCoroutine ()
    {
        if (pauseMainBehaviorCoroutine != null) {
            StopCoroutine (pauseMainBehaviorCoroutine);
        }

        mainFindObjectivesSystem.setBehaviorStatesPausedState (false);

        lastTimeAttackActivated = Time.time;

        abilityInProcess = false;
    }

    IEnumerator pauseMainAttackBehaviorCoroutine ()
    {
        mainFindObjectivesSystem.setBehaviorStatesPausedState (true);

        WaitForSeconds delay = new WaitForSeconds (currentAIAbilityInfo.abilityDuration);

        yield return delay;

        mainFindObjectivesSystem.setBehaviorStatesPausedState (false);

        abilityInProcess = false;

        lastTimeAttackActivated = Time.time;

        yield return null;
    }

    public void resetAttackState ()
    {

    }


    public void disableOnSpottedState ()
    {

    }

    public void setAndActivateAbilityByName (string abilityName)
    {
        setNewAbilityByName (abilityName);

        attackTarget ();

        if (showDebugPrint) {
            print ("activate ability attack " + currentAttackRate);
        }

        if (currentAIAbilityInfo.useCustomRandomAttackRangeRate) {
            currentAttackRate = Random.Range (currentAIAbilityInfo.customRandomAttackRangeRate.x, currentAIAbilityInfo.customRandomAttackRangeRate.y);
        } else {
            currentAttackRate = Random.Range (randomAttackRangeRate.x, randomAttackRangeRate.y);
        }
    }

    public void setNewAbilityByName (string abilityName)
    {
        if (!systemEnabled) {
            return;
        }

        int newAbilityIndex = AIAbilityInfoList.FindIndex (s => s.Name.Equals (abilityName));

        if (newAbilityIndex > -1) {
            if (currentAIAbilityInfo != null) {
                currentAIAbilityInfo.isCurrentAbility = false;
            }

            currentAIAbilityInfo = AIAbilityInfoList [newAbilityIndex];

            currentAIAbilityInfo.isCurrentAbility = true;

            currentAIAbilityInfoAssigned = true;

            currentAbilityIndex = newAbilityIndex;

            if (showDebugPrint) {
                print ("setting current ability index " + currentAbilityIndex);
            }
        }
    }

    public void setNewMinDistanceToAttack (float newValue)
    {
        currentMinDistanceToAttack = newValue;
    }

    public void setNewAbilityStateByName (string abilityStateName)
    {
        if (!systemEnabled) {
            return;
        }

        int newAbilityStateIndex = AIAbilityStatesInfoList.FindIndex (s => s.Name.Equals (abilityStateName));

        if (newAbilityStateIndex > -1) {
            if (currentAIAbilityStatesInfo != null) {
                currentAIAbilityStatesInfo.isCurrentState = false;
            }

            currentAIAbilityStatesInfo = AIAbilityStatesInfoList [newAbilityStateIndex];

            currentAIAbilityStatesInfo.isCurrentState = true;

            currentMinDistanceToAttack = currentAIAbilityStatesInfo.minDistanceToAttack;

            currentAbilityStateIndex = newAbilityStateIndex;
        }
    }

    public void stopCurrentAttackInProcess ()
    {
        if (abilityInProcess) {
            stopPauseMainAttackBehaviorCoroutine ();

            stopAbilityInputHoldCoroutine ();
        }
    }

    public void setTurnBasedCombatActionActiveState (bool state)
    {
        turnBasedCombatActionActive = state;
    }

    public bool isAbilityInProcess ()
    {
        return abilityInProcess;
    }

    public void checkAIBehaviorStateOnCharacterSpawn ()
    {

    }

    public void checkAIBehaviorStateOnCharacterDespawn ()
    {

    }

    public void setChangeAbilityRandomlyState (bool state)
    {
        changeAbilityRandomly = state;
    }

    public void setUseCustomListToChangeAbilityState (bool state)
    {
        useCustomListToChangeAbility = state;
    }

    public void setCustomListToChangeAbility (List<string> newList)
    {
        customListToChangeAbility = newList;
    }

    public void setUseCustomListToChangeAbilityInfoListState (bool state)
    {
        useCustomListToChangeAbilityInfoList = state;
    }

    public void setCustomListToChangeAbilityInfoListByName (string newName)
    {
        for (int i = 0; i < customListToChangeAbilityInfoList.Count; i++) {
            if (customListToChangeAbilityInfoList [i].Name.Equals (newName)) {
                customListToChangeAbilityInfoList [i].isCustomListActive = true;
            } else {
                customListToChangeAbilityInfoList [i].isCustomListActive = false;
            }
        }
    }

    public void setAttackEnabledState (bool state)
    {
        attackEnabled = state;
    }

    public void setAttackEnabledStateFromEditor (bool state)
    {
        setAttackEnabledState (state);

        updateComponent ();
    }

    public void updateComponent ()
    {
        GKC_Utils.updateComponent (this);

        GKC_Utils.updateDirtyScene ("Update AI Abilities Brain " + gameObject.name, gameObject);
    }

    [System.Serializable]
    public class AIAbilityInfo
    {
        [Header ("Main Settings")]
        [Space]

        public string Name;

        public bool useAbilityEnabled = true;

        public bool isCurrentAbility;

        public bool sendSignalToActivateAbilityEnabled = true;

        [Space]

        public bool useProbabilityToUseAbility;
        [Range (0, 100)] public float probabilityToUseAbility;

        [Space]
        [Header ("Distance Settings")]
        [Space]

        public bool useCustomAbilityDistance;
        public float customAbilityDistance;

        public bool checkVerticalDistanceToTarget;
        public float maxVerticalDistanceToTarget;

        public bool ignoreInsideMinDistanceToAttackTarget;

        [Space]
        [Header ("Ability Settings")]
        [Space]

        public bool abilityInputHoldType;

        public bool abilityInputToggleType;

        public float timeToHoldAndReleaseAbilityInput;

        public float abilityDuration;

        [Space]
        [Header ("Custom Random Attack Range Settings")]
        [Space]

        public bool useCustomRandomAttackRangeRate;

        public Vector2 customRandomAttackRangeRate;

        [Space]
        [Header ("Events Settings")]
        [Space]

        public bool useEventsOnAbility;

        public UnityEvent eventOnAbilityStart;

        public UnityEvent eventOnAbilityEnd;
    }

    [System.Serializable]
    public class AIAbilityStatesInfo
    {
        public string Name;

        public float minDistanceToAttack = 7;

        public bool isCurrentState;
    }

    [System.Serializable]
    public class customListToChangeAbilityInfo
    {
        public string Name;

        public bool isCustomListActive;

        public List<string> customListToChangeAbility = new List<string> ();
    }
}
