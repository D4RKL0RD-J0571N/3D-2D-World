using UnityEngine;
using System.Collections;
using UnityEngine.Events;

public class AIViewTriggerSystem : MonoBehaviour
{
    [Header ("Main Settings")]
    [Space]

    public bool checkTriggerEnabled = true;

    public bool onTriggerEnter;
    public bool onTriggerExit;

    [Space]

    public bool sendDetectedObjectAsSuspectOnEnter = true;
    public bool removeDetectedObjectAsSuspectOnExit = true;

    [Space]

    public bool sendDetectedObjectAsTargetToCheckOnEnter;
    public bool removeDetectedObjectAsTargetToCheckOnExit;

    [Space]
    [Header ("Regular Event Settings")]
    [Space]

    public bool useEvents;

    public UnityEvent onTriggerEnterEvent = new UnityEvent ();
    public UnityEvent onTriggerExitEvent = new UnityEvent ();

    [Space]
    [Header ("Events With Objects Settings")]
    [Space]

    public bool useOnTriggerEnterEventWithObject;
    public eventParameters.eventToCallWithGameObject onTriggerEnterEventWithObject;

    public bool useOnTriggerExitEventWithObject;
    public eventParameters.eventToCallWithGameObject onTriggerExitEventWithObject;

    [Space]
    [Header ("Debug")]
    [Space]

    public bool showDebugPrint;

    [Space]
    [Header ("Components")]
    [Space]

    public findObjectivesSystem mainFindObjectivesSystem;


    void OnTriggerEnter (Collider col)
    {
        if (!checkTriggerEnabled) {
            return;
        }

        checkTrigger (col, true);
    }

    void OnTriggerExit (Collider col)
    {
        if (!checkTriggerEnabled) {
            return;
        }

        checkTrigger (col, false);
    }

    public void checkTrigger (Collider col, bool isEnter)
    {
        if (isEnter) {
            if (onTriggerEnter) {

                if (useEvents) {
                    callEvent (onTriggerEnterEvent);

                    if (useOnTriggerEnterEventWithObject) {
                        callEventWithObject (onTriggerEnterEventWithObject, col.gameObject);
                    }
                }

                if (sendDetectedObjectAsSuspectOnEnter) {
                    mainFindObjectivesSystem.checkSuspect (col.gameObject);
                }

                if (sendDetectedObjectAsTargetToCheckOnEnter) {
                    mainFindObjectivesSystem.checkTriggerInfo (col, true);
                }

                if (showDebugPrint) {
                    print ("checking detected object on trigger enter " + col.gameObject.name);
                }
            }
        } else {
            if (onTriggerExit) {

                if (useEvents) {
                    callEvent (onTriggerExitEvent);

                    if (useOnTriggerExitEventWithObject) {
                        callEventWithObject (onTriggerExitEventWithObject, col.gameObject);
                    }
                }

                if (removeDetectedObjectAsSuspectOnExit) {
                    mainFindObjectivesSystem.cancelCheckSuspect (col.gameObject);
                }

                if (removeDetectedObjectAsTargetToCheckOnExit) {
                    mainFindObjectivesSystem.checkTriggerInfo (col, false);
                }

                if (showDebugPrint) {
                    print ("checking detected object on trigger exit " + col.gameObject.name);
                }
            }
        }
    }

    public void callEvent (UnityEvent eventToCall)
    {
        eventToCall.Invoke ();
    }

    public void callEventWithObject (eventParameters.eventToCallWithGameObject eventToCall, GameObject objectToSend)
    {
        eventToCall.Invoke (objectToSend);
    }

    public void setCheckTriggerEnabledState (bool state)
    {
        checkTriggerEnabled = state;
    }
}