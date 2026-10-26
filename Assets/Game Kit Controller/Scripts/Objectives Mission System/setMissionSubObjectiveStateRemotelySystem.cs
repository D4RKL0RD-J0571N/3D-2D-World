using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class setMissionSubObjectiveStateRemotelySystem : MonoBehaviour
{
    [Header ("Main Settings")]
    [Space]

    public bool setMissionInfoEnabled = true;

    public int missionID;

    public int missionScene = -1;

    public string subObjectiveName;

    [Space]
    [Header ("Other Settings")]
    [Space]

    public bool useObjectiveCounterInsteadOfList;

    public bool increasetCounterToCurrentMissionActive;

    [Space]
    [Header ("Remote Events Settings")]
    [Space]

    public bool useRemoteEvents;
    public List<string> removeEventNameList = new List<string> ();

    [Space]
    [Header ("Events Settings")]
    [Space]

    public bool useEventOnSubObjectiveComplete;
    public UnityEvent eventOnSubObjectiveComplete;

    [Space]

    public bool useEventOnSubObjectiveCancelled;
    public UnityEvent eventOnSubObjectiveCompleteCancelled;


    bool mainObjectiveManagerLocated;
    objectiveManager mainObjectiveManager;

    public void addSubObjectiveCompleteRemotely (string customSubObjectiveName)
    {
        sendMissionInfo (customSubObjectiveName);
    }

    public void addSubObjectiveCompleteRemotely ()
    {
        sendMissionInfo (subObjectiveName);
    }

    public void sendMissionInfo (string newSubObjectiveName)
    {
        if (!setMissionInfoEnabled) {
            return;
        }

        getMainObjectiveManager ();

        if (mainObjectiveManagerLocated) {
            if (useObjectiveCounterInsteadOfList) {
                if (increasetCounterToCurrentMissionActive) {
                    mainObjectiveManager.increaseObjectiveCounterRemotelyToCurrentMissionActive (missionScene);
                } else {
                    mainObjectiveManager.increaseObjectiveCounterRemotely (missionScene, missionID);
                }
            } else {
                mainObjectiveManager.addSubObjectiveCompleteRemotely (newSubObjectiveName, missionScene, missionID);
            }

            if (useRemoteEvents) {
                if (increasetCounterToCurrentMissionActive) {
                    mainObjectiveManager.checkRemoteEventsOnSubObjectiveCompleteRemotelyToCurrentMissionActive (removeEventNameList, missionScene);
                } else {
                    mainObjectiveManager.checkRemoteEventsOnSubObjectiveCompleteRemotely (removeEventNameList, missionScene, missionID);
                }
            }

            if (useEventOnSubObjectiveComplete) {
                eventOnSubObjectiveComplete.Invoke ();
            }
        }
    }

    public void cancelCurrentMissionRemotely ()
    {
        if (!setMissionInfoEnabled) {
            return;
        }

        getMainObjectiveManager ();

        if (mainObjectiveManagerLocated) {
            mainObjectiveManager.cancelCurrentMissionRemotely (missionScene, missionID);

            if (useEventOnSubObjectiveCancelled) {
                eventOnSubObjectiveCompleteCancelled.Invoke ();
            }
        }
    }

    void getMainObjectiveManager ()
    {
        if (mainObjectiveManagerLocated) {
            return;
        }

        mainObjectiveManager = objectiveManager.Instance;

        mainObjectiveManagerLocated = mainObjectiveManager != null;

        if (!mainObjectiveManagerLocated) {
            GKC_Utils.instantiateMainManagerOnSceneWithTypeOnApplicationPlaying (objectiveManager.getMainManagerName (), typeof (objectiveManager), true);

            mainObjectiveManager = objectiveManager.Instance;

            mainObjectiveManagerLocated = mainObjectiveManager != null;
        }

        if (!mainObjectiveManagerLocated) {

            mainObjectiveManager = GKC_Utils.findObjectByComponent<objectiveManager> ();

            mainObjectiveManagerLocated = mainObjectiveManager != null;
        }

    }

    public void setMissionID (int newValue)
    {
        missionID = newValue;
    }

    public void increaseMissionID ()
    {
        missionID++;
    }

    public void decreaseMissionID ()
    {
        missionID--;

        if (missionID < 0) {
            missionID = 0;
        }
    }

    public void setSubObjectiveName (string newValue)
    {
        subObjectiveName = newValue;
    }

    public void setMissionInfoEnabledState (bool state)
    {
        setMissionInfoEnabled = state;
    }

    public void setMissionInfoEnabledStateFromEditor (bool state)
    {
        setMissionInfoEnabledState (state);

        updateComponent ();
    }

    void updateComponent ()
    {
        GKC_Utils.updateComponent (this);

        GKC_Utils.updateDirtyScene ("update mission state remotely state " + gameObject.name, gameObject);
    }
}
