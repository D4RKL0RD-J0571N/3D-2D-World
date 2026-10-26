using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class sharedActionZoneTrigger : MonoBehaviour
{
    [Header ("Main Settings")]
    [Space]

    public bool sharedActionZoneTriggerEnabled = true;

    [Space]

    public string sharedActionZoneNameOnEnter;

    public bool useSharedActionTimerOnEnter;
    public float sharedActionTimerOnEnter;

    [Space]

    public bool setSharedActionZoneOnExit;
    public string sharedActionZoneNameOnExit;

    public bool useSharedActionTimerOnExit;
    public float sharedActionTimerOnExit;

    [Space]

    public bool enableSharedActionRemoteActivatorOnTriggerEnter;

    public bool disableSharedActionRemoteActivatorOnTriggerExit;

    [Space]
    [Header ("Custom Shared Action Name Settings")]
    [Space]

    public bool setCustomSharedActionNameOnEnter;
    public string customSharedActionNameOnEnter;

    [Space]

    public bool removeCustomSharedActionNameOnExit;

    [Space]
    [Header ("Check Targets Settings")]
    [Space]

    public bool useTagList;
    public List<string> tagList = new List<string> ();

    public bool useLayerMask;
    public LayerMask layerMask;


    void OnTriggerEnter (Collider col)
    {
        checkTriggerInfo (col, true);
    }

    void OnTriggerExit (Collider col)
    {
        checkTriggerInfo (col, false);
    }

    public void checkTriggerInfo (Collider col, bool isEnter)
    {
        if (!sharedActionZoneTriggerEnabled) {
            return;
        }

        GameObject currentCharacter = col.gameObject;

        bool checkObjectResult = false;

        if (useTagList) {
            if (tagList.Contains (currentCharacter.tag)) {
                checkObjectResult = true;
            }
        }

        if (useLayerMask) {
            if ((1 << currentCharacter.layer & layerMask.value) == 1 << currentCharacter.layer) {
                checkObjectResult = true;
            }
        }

        if (!checkObjectResult) {
            return;
        }

        playerComponentsManager currentPlayerComponentsManager = currentCharacter.GetComponent<playerComponentsManager> ();

        if (currentPlayerComponentsManager != null) {
            playerActionSystem currentPlayerActionSystem = currentPlayerComponentsManager.getPlayerActionSystem ();

            sharedActionButtonActivator currentSharedActionButtonActivator = currentPlayerActionSystem.getSharedActionButtonActivator ();

            if (currentSharedActionButtonActivator == null) {
                return;
            }

            if (isEnter) {
                if (enableSharedActionRemoteActivatorOnTriggerEnter) {
                    currentSharedActionButtonActivator.enableOrDisableaActivator (true);

                    if (useSharedActionTimerOnEnter) {
                        string actionName = sharedActionZoneNameOnEnter + ";" + sharedActionTimerOnEnter.ToString ();

                        currentSharedActionButtonActivator.setSharedActionName (actionName);
                    } else {
                        currentSharedActionButtonActivator.setSharedActionName (sharedActionZoneNameOnEnter);
                    }
                }

                if (setCustomSharedActionNameOnEnter) {
                    sharedActionSystemRemoteActivator currentharedActionSystemRemoteActivator = currentPlayerActionSystem.getSharedActionSystemRemoteActivator ();

                    if (currentharedActionSystemRemoteActivator != null) {
                        currentharedActionSystemRemoteActivator.setCustomSharedActionNameToUse (customSharedActionNameOnEnter);
                    }
                }
            } else {
                if (setSharedActionZoneOnExit) {
                    currentSharedActionButtonActivator.enableOrDisableaActivator (true);

                    if (useSharedActionTimerOnExit) {
                        string actionName = sharedActionZoneNameOnExit + ";" + sharedActionTimerOnExit.ToString ();

                        currentSharedActionButtonActivator.setSharedActionName (actionName);
                    } else {
                        currentSharedActionButtonActivator.setSharedActionName (sharedActionZoneNameOnExit);
                    }
                }

                if (disableSharedActionRemoteActivatorOnTriggerExit) {
                    currentSharedActionButtonActivator.enableOrDisableaActivator (false);
                }

                if (removeCustomSharedActionNameOnExit) {
                    sharedActionSystemRemoteActivator currentharedActionSystemRemoteActivator = currentPlayerActionSystem.getSharedActionSystemRemoteActivator ();

                    if (currentharedActionSystemRemoteActivator != null) {
                        currentharedActionSystemRemoteActivator.removeCustomSharedActionName ();
                    }
                }
            }
        }
    }
}