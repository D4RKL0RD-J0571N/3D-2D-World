using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class eventInfoSystem : MonoBehaviour
{
    public bool eventInfoEnabled = true;

    public bool useDelayEnabled = true;

    public List<eventInfo> eventInfoList = new List<eventInfo> ();

    public remoteEventSystem mainRemoteEventSystem;

    public bool useSpeedMultiplier;

    public float speedMultiplier;

    public bool useAccumulativeDelay;

    public bool eventInProcess;

    public bool useEventOnStopCoroutineIfActive;
    public UnityEvent eventOnStopCoroutineIfActive;

    eventInfo currentEventInfo;

    Coroutine eventInfoListCoroutine;

    public void stopCheckActionEventInfoList ()
    {
        if (eventInfoListCoroutine != null) {
            StopCoroutine (eventInfoListCoroutine);
        }

        if (eventInProcess) {
            if (useEventOnStopCoroutineIfActive) {
                eventOnStopCoroutineIfActive.Invoke ();
            }
        }

        eventInProcess = false;
    }

    public void activateActionEventInfoList ()
    {
        if (!eventInfoEnabled) {
            return;
        }

        bool mainRemoteEventSystemLocated = mainRemoteEventSystem != null;

        if (useDelayEnabled) {

            stopCheckActionEventInfoList ();

            eventInfoListCoroutine = StartCoroutine (checkActionEventInfoListCoroutine ());
        } else {
            for (int i = 0; i < eventInfoList.Count; i++) {

                currentEventInfo = eventInfoList [i];

                currentEventInfo.eventToUse.Invoke ();

                if (mainRemoteEventSystemLocated) {
                    if (currentEventInfo.useRemoteEvent) {
                        mainRemoteEventSystem.callRemoteEvent (currentEventInfo.remoteEventName);
                    }
                }
            }
        }
    }

    IEnumerator checkActionEventInfoListCoroutine ()
    {
        eventInProcess = true;

        for (int i = 0; i < eventInfoList.Count; i++) {
            eventInfoList [i].eventTriggered = false;
        }

        bool mainRemoteEventSystemLocated = mainRemoteEventSystem != null;

        if (useAccumulativeDelay) {

            for (int i = 0; i < eventInfoList.Count; i++) {

                currentEventInfo = eventInfoList [i];

                float currentDelay = currentEventInfo.delayToActivate;

                if (useSpeedMultiplier) {
                    currentDelay /= speedMultiplier;
                }

                WaitForSeconds delay = new WaitForSeconds (currentDelay);

                yield return delay;

                currentEventInfo.eventToUse.Invoke ();

                if (mainRemoteEventSystemLocated) {
                    if (currentEventInfo.useRemoteEvent) {
                        mainRemoteEventSystem.callRemoteEvent (currentEventInfo.remoteEventName);
                    }
                }
            }
        } else {
            int numberOfEvents = eventInfoList.Count;

            int numberOfEventsTriggered = 0;

            float timer = Time.time;

            bool allEventsTriggered = false;

            while (!allEventsTriggered) {
                for (int i = 0; i < eventInfoList.Count; i++) {

                    currentEventInfo = eventInfoList [i];

                    if (!currentEventInfo.eventTriggered) {
                        float currentDelay = currentEventInfo.delayToActivate;

                        if (useSpeedMultiplier) {
                            currentDelay /= speedMultiplier;
                        }

                        if (Time.time > timer + currentDelay) {
                            currentEventInfo.eventToUse.Invoke ();

                            if (mainRemoteEventSystemLocated) {
                                if (currentEventInfo.useRemoteEvent) {
                                    mainRemoteEventSystem.callRemoteEvent (currentEventInfo.remoteEventName);
                                }
                            }

                            currentEventInfo.eventTriggered = true;

                            numberOfEventsTriggered++;

                            if (numberOfEvents == numberOfEventsTriggered) {
                                allEventsTriggered = true;
                            }
                        }
                    }
                }

                yield return null;
            }
        }

        eventInProcess = false;
    }

    public void addNewEvent ()
    {
        eventInfo newEventInfo = new eventInfo ();

        eventInfoList.Add (newEventInfo);

        updateComponent ();
    }

    public void setEventInfoEnabledState (bool state)
    {
        eventInfoEnabled = state;
    }

    public void updateComponent ()
    {
        GKC_Utils.updateComponent (this);

        GKC_Utils.updateDirtyScene ("Update Event Info System " + gameObject.name, gameObject);
    }

    [System.Serializable]
    public class eventInfo
    {
        public float delayToActivate;

        public UnityEvent eventToUse;

        public bool useRemoteEvent;

        public string remoteEventName;

        public bool eventTriggered;
    }
}
