using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class changeAIAttackModeByDistance : MonoBehaviour
{
    [Header ("Main Settings")]
    [Space]

    public bool changeAttackModeBasedOnDistanceToTarget;
    public float maxDistanceForShortRangeAttackMode;

    [Space]

    public string shortRangeAttackModeName;

    public string longRangeAttackModeName;

    [Space]

    public bool useRandomShortRangeAttackMode;

    public List<string> randomShortRangeAttackModeList = new List<string> ();

    [Space]

    public bool useRandomLongRangeAttackMode;

    public List<string> randomLongRangeAttackModeList = new List<string> ();

    [Space]
    [Header ("Sprint Settings")]
    [Space]

    public bool useSprintBasedOnDistanceToTarget;

    public float minDistanceToTargetToSprint;

    [Space]
    [Header ("Debug Settings")]
    [Space]

    public bool showDebugPrint;

    public bool shortRangeAttackModeActive;
    public bool longRangeAttackModeActive;

    [Space]

    public bool ignoreChangeAttackModeBasedOnDistanceToTarget;

    public bool sprintActive;

    [Space]
    [Header ("Components")]
    [Space]

    public findObjectivesSystem mainFindObjectivesSystem;

    public playerController mainPlayerController;

    bool mainPlayerControllerAssigned;


    public void initializeElements ()
    {
        mainPlayerControllerAssigned = mainPlayerController != null;

        if (!mainPlayerControllerAssigned) {
            mainPlayerController = mainFindObjectivesSystem.getPlayerControllerManager ();

            mainPlayerControllerAssigned = mainPlayerController != null;
        }
    }

    public void checkChangeAttackModeBasedOnDistanceToTarget ()
    {
        if (changeAttackModeBasedOnDistanceToTarget && !ignoreChangeAttackModeBasedOnDistanceToTarget) {
            if (mainFindObjectivesSystem.getCurrentDistanceToTarget () < maxDistanceForShortRangeAttackMode) {
                if ((!shortRangeAttackModeActive && !longRangeAttackModeActive) || !shortRangeAttackModeActive) {
                    string currentAttackModeName = GKC_Utils.getCurrentPlayersModeName (mainFindObjectivesSystem.getAIGameObject ().transform);

                    bool currentAttackModeActive = false;

                    string attackModeToSelect = shortRangeAttackModeName;

                    if (useRandomShortRangeAttackMode) {
                        bool attackModeFound = false;

                        int counter = 0;

                        int randomShortRangeAttackModeListCount = randomShortRangeAttackModeList.Count;

                        while (!attackModeFound) {

                            int randomIndex = Random.Range (0, randomShortRangeAttackModeListCount);

                            attackModeToSelect = randomShortRangeAttackModeList [randomIndex];

                            currentAttackModeActive = currentAttackModeName != "" &&
                                currentAttackModeName == attackModeToSelect;

                            if (!currentAttackModeActive) {
                                attackModeFound = true;
                            }

                            if (counter > randomShortRangeAttackModeListCount * 3) {
                                attackModeFound = true;
                            }
                        }
                    } else {
                        currentAttackModeActive = currentAttackModeName != "" &&
                            currentAttackModeName == attackModeToSelect;
                    }

                    if (!currentAttackModeActive) {
                        mainFindObjectivesSystem.changeCurrentAttackMode (attackModeToSelect);

                        if (showDebugPrint) {
                            print ("change mode result " + !currentAttackModeActive + " " + attackModeToSelect);
                        }
                    }

                    shortRangeAttackModeActive = true;
                    longRangeAttackModeActive = false;
                }
            } else {
                if ((!shortRangeAttackModeActive && !longRangeAttackModeActive) || !longRangeAttackModeActive) {
                    string currentAttackModeName = GKC_Utils.getCurrentPlayersModeName (mainFindObjectivesSystem.getAIGameObject ().transform);

                    bool currentAttackModeActive = false;

                    string attackModeToSelect = longRangeAttackModeName;

                    if (useRandomLongRangeAttackMode) {
                        bool attackModeFound = false;

                        int counter = 0;

                        int randomLongRangeAttackModeListCount = randomLongRangeAttackModeList.Count;

                        while (!attackModeFound) {

                            int randomIndex = Random.Range (0, randomLongRangeAttackModeListCount);

                            attackModeToSelect = randomLongRangeAttackModeList [randomIndex];

                            currentAttackModeActive = currentAttackModeName != "" &&
                                currentAttackModeName == attackModeToSelect;

                            if (!currentAttackModeActive) {
                                attackModeFound = true;
                            }

                            if (counter > randomLongRangeAttackModeListCount * 3) {
                                attackModeFound = true;
                            }
                        }
                    } else {
                        currentAttackModeActive = currentAttackModeName != "" &&
                            currentAttackModeName == attackModeToSelect;
                    }

                    if (!currentAttackModeActive) {
                        mainFindObjectivesSystem.changeCurrentAttackMode (attackModeToSelect);

                        if (showDebugPrint) {
                            print ("change mode result " + !currentAttackModeActive + " " + attackModeToSelect);
                        }
                    }

                    shortRangeAttackModeActive = false;
                    longRangeAttackModeActive = true;
                }
            }
        }

        if (useSprintBasedOnDistanceToTarget && mainPlayerControllerAssigned) {
            if (mainFindObjectivesSystem.getCurrentDistanceToTarget () > minDistanceToTargetToSprint) {
                if (sprintActive) {
                    if (!mainPlayerController.isPlayerRunning ()) {
                        mainPlayerController.inputStartToRun ();
                    }
                } else {
                    if (!mainPlayerController.isPlayerRunning ()) {
                        mainPlayerController.inputStartToRun ();

                        sprintActive = true;
                    }
                }
            } else {
                if (sprintActive) {
                    if (mainPlayerController.isPlayerRunning ()) {
                        mainPlayerController.forceStopRun ();

                        sprintActive = false;
                    }
                }
            }
        }
    }

    public void resetAttackModeActiveState ()
    {
        shortRangeAttackModeActive = false;
        longRangeAttackModeActive = false;

        if (sprintActive) {
            if (mainPlayerControllerAssigned) {
                mainPlayerController.forceStopRun ();
            }

            sprintActive = false;
        }
    }

    public void setChangeAttackModeBasedOnDistanceToTargetState (bool state)
    {
        changeAttackModeBasedOnDistanceToTarget = state;
    }

    public void setIgnoreChangeAttackModeBasedOnDistanceToTargetState (bool state)
    {
        ignoreChangeAttackModeBasedOnDistanceToTarget = state;
    }

    public void setUseSprintBasedOnDistanceToTargetState (bool state)
    {
        useSprintBasedOnDistanceToTarget = state;
    }

    public void setChangeAttackModeBasedOnDistanceToTargetStateFromEditor (bool state)
    {
        setChangeAttackModeBasedOnDistanceToTargetState (state);

        updateComponent ();
    }

    public void setUseSprintBasedOnDistanceToTargetStateFromEditor (bool state)
    {
        setUseSprintBasedOnDistanceToTargetState (state);

        updateComponent ();
    }

    void updateComponent ()
    {
        GKC_Utils.updateComponent (this);

        GKC_Utils.updateDirtyScene ("Update " + gameObject.name, gameObject);
    }
}
