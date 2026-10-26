using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class eventOnTargetDetectedFaction : MonoBehaviour
{
    [Header ("Main Settings")]
    [Space]

    public bool checkTargetsEnabled = true;

    [Space]
    [Header ("Targets Info List Settings")]
    [Space]

    public List<eventOnTargetInfo> eventOnTargetInfoList = new List<eventOnTargetInfo> ();

    [Space]
    [Header ("Debug")]
    [Space]

    public bool showDebugPrint;


    public void checkTargetDetected (GameObject newTarget)
    {
        if (!checkTargetsEnabled) {
            return;
        }

        if (newTarget == null) {
            return;
        }

        checkTargetDetected (GKC_Utils.getCharacterFactionName (newTarget));
    }

    public void removeTargetDetected (GameObject newTarget)
    {
        if (!checkTargetsEnabled) {
            return;
        }

        if (newTarget == null) {
            return;
        }

        removeTargetDetected (GKC_Utils.getCharacterFactionName (newTarget));
    }

    public void checkTargetDetected (string targetFactioName)
    {
        if (!checkTargetsEnabled) {
            return;
        }

        if (targetFactioName == null || targetFactioName == "") {
            return;
        }

        int currentIndex = eventOnTargetInfoList.FindIndex (s => s.Name.Equals (targetFactioName));

        if (currentIndex > -1) {
            eventOnTargetInfo currentEventOnTargetInfo = eventOnTargetInfoList [currentIndex];

            if (currentEventOnTargetInfo.infoEnabled) {
                currentEventOnTargetInfo.eventOnAddTarget.Invoke ();

                if (showDebugPrint) {
                    print ("checkTargetDetected " + targetFactioName);
                }
            }
        }
    }

    public void removeTargetDetected (string targetFactioName)
    {
        if (!checkTargetsEnabled) {
            return;
        }

        if (targetFactioName == null || targetFactioName == "") {
            return;
        }

        int currentIndex = eventOnTargetInfoList.FindIndex (s => s.Name.Equals (targetFactioName));

        if (currentIndex > -1) {
            eventOnTargetInfo currentEventOnTargetInfo = eventOnTargetInfoList [currentIndex];

            if (currentEventOnTargetInfo.infoEnabled) {
                currentEventOnTargetInfo.eventOnRemoveTarget.Invoke ();

                if (showDebugPrint) {
                    print ("removeTargetDetected " + targetFactioName);
                }
            }
        }
    }

    public void setCheckTargetsEnabledState (bool state)
    {
        checkTargetsEnabled = state;
    }

    public void setCheckTargetsEnabledStateFromEditor (bool state)
    {
        setCheckTargetsEnabledState (state);

        updateComponent ();
    }

    void updateComponent ()
    {
        GKC_Utils.updateComponent (this);

        GKC_Utils.updateDirtyScene ("Update Event On Target Detected Faction", gameObject);
    }

    [System.Serializable]
    public class eventOnTargetInfo
    {
        [Header ("Main Settings")]
        [Space]

        public string Name;

        public bool infoEnabled = true;

        [Space]

        public UnityEvent eventOnAddTarget;
        public UnityEvent eventOnRemoveTarget;
    }
}
