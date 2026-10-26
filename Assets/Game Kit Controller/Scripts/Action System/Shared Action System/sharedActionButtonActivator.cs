using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class sharedActionButtonActivator : MonoBehaviour
{
    [Header ("Main Settings")]
    [Space]

    public bool buttonActivatorEnabled = true;

    public bool buttonActive;

    public bool setActiveStateOnStart;

    public string sharedActionNameOnStart;

    public bool useRandomSharedActionName;

    [Space]
    [Header ("Debug")]
    [Space]

    public bool showDebugPrint;

    public string sharedActionName;

    public float currentTimeToDisableActivator;

    public GameObject currentExternalCharacter;

    public bool checkToDisableActivatorInProcess;

    [Space]
    [Header ("Event Info List Settings")]
    [Space]

    public List<sharedActionEventInfo> sharedActionEventInfoList = new List<sharedActionEventInfo> ();

    [Space]
    [Header ("Event Settings")]
    [Space]

    public bool useEventsItTargetDetected;
    public UnityEvent eventsItTargetDetected;

    public bool useEventsItTargetNotDetected;
    public UnityEvent eventsItTargetNotDetected;

    [Space]

    public UnityEvent eventOnAssigningNewInfo;

    [Space]

    public eventParameters.eventToCallWithString eventToSendNewActionName;

    [Space]

    public UnityEvent eventOnEnableActivator;
    public UnityEvent eventOnDisableActivator;

    [Space]
    [Header ("Components")]
    [Space]

    public sharedActionSystem mainSharedActionSystem;

    Coroutine updateCoroutine;

    void Start ()
    {
        if (setActiveStateOnStart) {
            enableOrDisableaActivator (true);

            setSharedActionName (sharedActionNameOnStart);
        }
    }

    public void setuseRandomSharedActionNameState (bool state)
    {
        useRandomSharedActionName = state;
    }

    public void enableActivatorAndSetSharedActioName (string newName)
    {
        enableOrDisableaActivator (true);

        setSharedActionName (newName);
    }

    public void setSharedActionName (string newName)
    {
        if (!buttonActivatorEnabled) {
            return;
        }

        sharedActionName = newName;

        if (sharedActionName != "") {
            eventOnAssigningNewInfo.Invoke ();

            eventToSendNewActionName.Invoke (sharedActionName);

            int currentActionIndex = sharedActionEventInfoList.FindIndex (s => s.sharedActionName.Equals (sharedActionName));

            if (currentActionIndex > -1) {
                sharedActionEventInfo currentSharedActionEventInfo = sharedActionEventInfoList [currentActionIndex];

                currentSharedActionEventInfo.eventOnSetSharedActionName.Invoke ();
            }
        }
    }

    public void setSharedActionNameAndEnableWithDuration (string newName)
    {
        if (!buttonActivatorEnabled) {
            return;
        }

        bool splitSymbolLocated = newName.Contains (";");

        if (splitSymbolLocated) {
            string [] nameAndDuration = newName.Split (';');

            if (nameAndDuration.Length > 0) {
                newName = nameAndDuration [0];

                setSharedActionName (newName);
            }

            if (nameAndDuration.Length > 1) {
                float durationAmount = float.Parse (nameAndDuration [1]);

                if (showDebugPrint) {
                    print (nameAndDuration [0] + " " + nameAndDuration [1]);
                }

                if (durationAmount > 0) {
                    enableActivatorWithDuration (durationAmount);
                }
            }
        }
    }

    public void setExternalCharacter (GameObject newCharacter)
    {
        currentExternalCharacter = newCharacter;
    }

    public void activateRandomSharedActionByName ()
    {
        bool previousUseRandomSharedActionNameValue = useRandomSharedActionName;

        useRandomSharedActionName = true;

        activateSharedActionByName ();

        useRandomSharedActionName = previousUseRandomSharedActionNameValue;
    }

    public void activateSharedActionByName ()
    {
        bool activateActionResult = true;

        if (!buttonActivatorEnabled) {
            activateActionResult = false;
        }

        if (!mainSharedActionSystem.isSharedActionEnabled ()) {
            activateActionResult = false;
        }

        if (currentExternalCharacter == null) {
            activateActionResult = false;
        }

        if (showDebugPrint) {
            print ("activate action result " + activateActionResult + " " + sharedActionName);
        }

        if (activateActionResult) {
            mainSharedActionSystem.setExternalCharacter (currentExternalCharacter);

            if (useRandomSharedActionName) {
                mainSharedActionSystem.activateRandomSharedActionByName (sharedActionName);
            } else {
                mainSharedActionSystem.activateSharedActionByName (sharedActionName);
            }
        }

        checkEventOnTargetDetectedResult (activateActionResult);
    }

    void checkEventOnTargetDetectedResult (bool state)
    {
        if (state) {
            if (useEventsItTargetDetected) {
                eventsItTargetDetected.Invoke ();
            }
        } else {
            if (useEventsItTargetNotDetected) {
                eventsItTargetNotDetected.Invoke ();
            }
        }
    }

    public void enableActivator ()
    {
        enableOrDisableaActivator (true);
    }

    public void disableActivator ()
    {
        enableOrDisableaActivator (false);
    }

    public void enableOrDisableaActivator (bool state)
    {
        if (!buttonActivatorEnabled) {
            return;
        }

        buttonActive = state;

        checkEventOnChangeActivatorState (state);
    }

    public void enableActivatorWithDuration (float newDuration)
    {
        enableOrDisableActivatorWithDuration (true, newDuration);
    }

    public void disableActivatorWithDuration ()
    {
        if (checkToDisableActivatorInProcess || buttonActive) {
            enableOrDisableActivatorWithDuration (false, 0);
        }
    }

    public void enableOrDisableActivatorWithDuration (bool state, float newDuration)
    {
        if (!buttonActivatorEnabled) {
            return;
        }

        currentTimeToDisableActivator = newDuration;

        stopUpdateCoroutine ();

        bool buttonPreviouslyActive = buttonActive || checkToDisableActivatorInProcess;

        buttonActive = state;

        if (showDebugPrint) {
            print ("enableOrDisableActivatorWithDuration " + state + " " + buttonPreviouslyActive);
        }

        if (state) {
            if (gameObject.activeInHierarchy) {
                updateCoroutine = StartCoroutine (updateSystemCoroutine ());
            }
        } else {
            if (buttonPreviouslyActive) {
                checkEventOnChangeActivatorState (false);
            }

            checkToDisableActivatorInProcess = false;
        }
    }

    void checkEventOnChangeActivatorState (bool state)
    {
        if (state) {
            eventOnEnableActivator.Invoke ();
        } else {
            eventOnDisableActivator.Invoke ();
        }

        if (showDebugPrint) {
            print ("checkEventOnChangeActivatorState " + state);
        }
    }

    public void stopUpdateCoroutine ()
    {
        if (updateCoroutine != null) {
            StopCoroutine (updateCoroutine);
        }
    }

    IEnumerator updateSystemCoroutine ()
    {
        checkToDisableActivatorInProcess = true;

        checkEventOnChangeActivatorState (true);

        WaitForSeconds delay = new WaitForSeconds (currentTimeToDisableActivator);

        yield return delay;

        checkEventOnChangeActivatorState (false);

        checkToDisableActivatorInProcess = false;

        buttonActive = false;
    }

    public void setButtonActivatorEnabledState (bool state)
    {
        buttonActivatorEnabled = state;
    }

    public void setButtonActivatorEnabledStateFromEditor (bool state)
    {
        setButtonActivatorEnabledState (state);

        updateComponent ();
    }

    void updateComponent ()
    {
        GKC_Utils.updateComponent (this);

        GKC_Utils.updateDirtyScene ("Update Shared Action Button " + gameObject.name, gameObject);
    }

    [System.Serializable]
    public class sharedActionEventInfo
    {
        public string sharedActionName;

        [Space]

        public UnityEvent eventOnSetSharedActionName;
    }
}
