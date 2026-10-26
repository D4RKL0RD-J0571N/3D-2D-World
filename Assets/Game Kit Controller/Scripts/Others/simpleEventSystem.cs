using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class simpleEventSystem : MonoBehaviour
{
    [Header ("Main Settings")]
    [Space]

    public bool eventEnabled = true;

    public bool useEventsOnActivateAndDisabled = true;

    public bool useDelayToEvent;
    public float delayToEvent;

    public bool activated;
    public bool callOnStart;

    [Space]
    [Header ("Debug")]
    [Space]

    public bool showDebugPrint;
    public bool eventCoroutineActive;

    [Space]
    [Header ("Events Settings")]
    [Space]

    public UnityEvent eventToCallOnActivate = new UnityEvent ();
    public UnityEvent eventToCallOnDisable = new UnityEvent ();

    [Space]
    [Space]

    public UnityEvent regularEventToCall;

    Coroutine eventCoroutine;


    void Start ()
    {
        if (callOnStart) {
            activateDevice ();
        }
    }

    public void activateDevice ()
    {
        if (!eventEnabled) {
            return;
        }

        if (useDelayToEvent) {
            if (eventCoroutine != null) {
                StopCoroutine (eventCoroutine);
            }

            eventCoroutineActive = false;

            eventCoroutine = StartCoroutine (activateDeviceCoroutine ());
        } else {
            callEvent ();
        }
    }

    IEnumerator activateDeviceCoroutine ()
    {
        eventCoroutineActive = true;

        WaitForSeconds delay = new WaitForSeconds (delayToEvent);

        yield return delay;

        callEvent ();

        eventCoroutineActive = false;

    }

    public void callEvent ()
    {
        if (!eventEnabled) {
            return;
        }

        if (useEventsOnActivateAndDisabled) {
            activated = !activated;

            if (activated) {
                eventToCallOnActivate.Invoke ();
            } else {
                eventToCallOnDisable.Invoke ();
            }
        } else {
            regularEventToCall.Invoke ();
        }
    }

    public void setActivatedStateAndCallEvents (bool state)
    {
        if (!eventEnabled) {
            return;
        }

        if (useEventsOnActivateAndDisabled) {
            activated = state;

            if (activated) {
                eventToCallOnActivate.Invoke ();
            } else {
                eventToCallOnDisable.Invoke ();
            }

            if (showDebugPrint) {
                print ("setActivatedStateAndCallEvents " + activated);
            }
        }
    }

    public void setActivatedState (bool state)
    {
        activated = state;
    }

    public void callEventsForCurrentActivatedState ()
    {
        if (!eventEnabled) {
            return;
        }

        if (useEventsOnActivateAndDisabled) {
            if (activated) {
                eventToCallOnActivate.Invoke ();
            } else {
                eventToCallOnDisable.Invoke ();
            }

            if (showDebugPrint) {
                print ("callEventsForCurrentActivatedState");
            }
        }
    }

    public void setEventEnabledState (bool state)
    {
        eventEnabled = state;

        if (showDebugPrint) {
            print ("setEventEnabledState " + state);
        }
    }

    public void setDelayToEventValue (float newValue)
    {
        delayToEvent = newValue;
    }
}
