using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class sharedActionSystemRemoteActivator : MonoBehaviour
{
    [Header ("Main Settings")]
    [Space]

    public bool remoteActivatorEnabled = true;

    public GameObject characterGameObject;

    [Space]

    public bool setAllSharedActionsActivatorResultAsNotActivated;

    [Space]
    [Header ("Target Raycast Detection Settings")]
    [Space]

    public bool useRaycastDetection;

    public float raycastDistance;

    public LayerMask raycastLayermask;

    public Transform raycastPositionTransform;

    public Vector3 raycastOffset;

    [Space]
    [Header ("Shared Action Activated Settings")]
    [Space]

    public List<remoteSharedActionSystemInfo> remoteSharedActionSystemInfoList = new List<remoteSharedActionSystemInfo> ();

    [Space]
    [Header ("Actions To Ignore Settings")]
    [Space]

    public bool useActionsToIgnoreList;
    public List<string> actionsToIgnoreList = new List<string> ();

    [Space]
    [Header ("Target Match Detection Settings")]
    [Space]

    public bool useMatchTargetSystemToGetTarget;

    public matchPlayerToTargetSystem mainMatchPlayerToTargetSystem;

    [Space]
    [Space]

    public bool checkOnFriendListManagerToGetTarget;

    public friendListManager mainFriendListManager;

    [Space]
    [Space]

    public bool useFindObjectivesSystemToGetTarget;
    public findObjectivesSystem mainFindObjectivesSystem;
    public bool getPartnerIfNotTargetDetected;

    [Space]
    [Header ("Editor Settings")]
    [Space]

    public string sharedActionName;

    public GameObject currentExternalCharacter;

    [Space]
    [Header ("Debug")]
    [Space]

    public bool showDebugPrint;

    public string customSharedActionName;
    public bool customSharedActionNameActive;

    [Space]
    [Header ("Event Settings")]
    [Space]

    public bool useEventsItTargetDetected;
    public UnityEvent eventsItTargetDetected;

    public bool useEventsItTargetNotDetected;
    public UnityEvent eventsItTargetNotDetected;

    [Space]
    [Header ("Components")]
    [Space]

    public sharedActionSystem mainSharedActionSystem;


    public void setCustomSharedActionNameToUse (string newName)
    {
        customSharedActionName = newName;

        customSharedActionNameActive = customSharedActionName != "";
    }

    public void removeCustomSharedActionName ()
    {
        setCustomSharedActionNameToUse ("");
    }

    public void checkSharedActionSystemByExternalDetection (string actionName)
    {
        if (!remoteActivatorEnabled) {
            return;
        }

        if (!useRaycastDetection && !useMatchTargetSystemToGetTarget && !useFindObjectivesSystemToGetTarget) {
            return;
        }

        if (customSharedActionNameActive) {
            if (showDebugPrint) {
                print ("custom shared action name active " + customSharedActionName);
            }

            actionName = customSharedActionName;
        }

        if (useActionsToIgnoreList) {
            if (actionsToIgnoreList.Contains (actionName)) {
                if (showDebugPrint) {
                    print ("action found on the action to ignore list " + actionName);
                }

                checkEventOnTargetDetectedResult (false, actionName);

                return;
            }
        }

        GameObject objectDetected = null;

        bool objectDetectedResult = false;

        if (useRaycastDetection) {
            RaycastHit hit;

            Vector3 raycastPosition = raycastPositionTransform.position + raycastOffset;

            Vector3 raycastDirection = raycastPositionTransform.forward;

            if (Physics.Raycast (raycastPosition, raycastDirection, out hit, raycastDistance, raycastLayermask)) {
                objectDetected = hit.collider.gameObject;

                objectDetectedResult = true;
            }
        }

        if (useMatchTargetSystemToGetTarget) {
            Transform targetTransform = mainMatchPlayerToTargetSystem.getCurrentTargetToMatchPosition ();

            if (targetTransform != null) {
                objectDetected = targetTransform.gameObject;

                objectDetectedResult = true;
            }
        }

        if (!objectDetectedResult) {
            if (checkOnFriendListManagerToGetTarget) {
                Transform targetTransform = mainFriendListManager.getClosestByDistanceFriend ();

                if (targetTransform != null) {
                    objectDetected = targetTransform.gameObject;

                    objectDetectedResult = true;
                }
            }
        }

        if (useFindObjectivesSystemToGetTarget) {
            objectDetected = mainFindObjectivesSystem.getCurrentTargetToAttack ();

            if (objectDetected != null) {

                objectDetectedResult = true;
            } else {
                if (getPartnerIfNotTargetDetected) {
                    Transform targetTransform = mainFindObjectivesSystem.getCurrentPartner ();

                    if (targetTransform != null) {
                        objectDetected = targetTransform.gameObject;

                        objectDetectedResult = true;
                    }
                }
            }
        }

        if (showDebugPrint) {
            print ("checking if target detected " + actionName + " " + objectDetectedResult);
        }

        if (objectDetectedResult) {
            if (showDebugPrint) {
                print ("target detected " + objectDetected.name);
            }

            objectDetected = applyDamage.getCharacter (objectDetected);

            if (objectDetected != null) {
                playerComponentsManager currentPlayerComponentsManager = objectDetected.GetComponent<playerComponentsManager> ();

                if (currentPlayerComponentsManager != null) {
                    mainSharedActionSystem = currentPlayerComponentsManager.getPlayerActionSystem ().getSharedActionSystem ();

                    if (mainSharedActionSystem != null) {
                        if (showDebugPrint) {
                            print ("target detected has shared action system, sending info");
                        }

                        currentExternalCharacter = characterGameObject;

                        sharedActionName = actionName;

                        activateSharedActionByName ();
                    }
                }
            }
        } else {
            checkEventOnTargetDetectedResult (false, actionName);
        }
    }

    void checkEventOnTargetDetectedResult (bool state, string actionName)
    {
        if (showDebugPrint) {
            print ("checking events on target detected result " + state + " " + actionName);
        }

        if (state) {
            if (useEventsItTargetDetected) {
                eventsItTargetDetected.Invoke ();
            }
        } else {
            if (useEventsItTargetNotDetected) {
                eventsItTargetNotDetected.Invoke ();
            }
        }

        checkSharedActionActivatedResult (actionName, state);
    }

    void checkSharedActionActivatedResult (string actionName, bool sharedActionResult)
    {
        int currentActionIndex = remoteSharedActionSystemInfoList.FindIndex (s => s.Name.Equals (actionName));

        if (currentActionIndex > -1) {
            remoteSharedActionSystemInfo currentRemoteSharedActionSystemInfo = remoteSharedActionSystemInfoList [currentActionIndex];

            if (currentRemoteSharedActionSystemInfo.checkSharedActionSystmeResultEnabled) {
                if (sharedActionResult) {
                    currentRemoteSharedActionSystemInfo.eventOnSharedActionActivated.Invoke ();
                } else {
                    currentRemoteSharedActionSystemInfo.eventOnSharedActionNotActivated.Invoke ();
                }
            }
        }
    }

    public void activateSharedActionByName ()
    {
        if (!remoteActivatorEnabled) {
            return;
        }

        if (!mainSharedActionSystem.isSharedActionEnabled ()) {
            if (showDebugPrint) {
                print ("character " + characterGameObject + " has his shared action system disabled");
            }

            checkEventOnTargetDetectedResult (false, sharedActionName);

            return;
        }

        bool isSharedActionEnabledPreviousValue = false;

        mainSharedActionSystem.setExternalCharacter (currentExternalCharacter);

        if (setAllSharedActionsActivatorResultAsNotActivated) {
            isSharedActionEnabledPreviousValue = mainSharedActionSystem.isSharedActionEnabled ();

            mainSharedActionSystem.setSharedActionEnabledState (false);
        }

        mainSharedActionSystem.activateSharedActionByName (sharedActionName);

        if (setAllSharedActionsActivatorResultAsNotActivated) {
            mainSharedActionSystem.setSharedActionEnabledState (isSharedActionEnabledPreviousValue);
        }

        if (mainSharedActionSystem.isLastSharedActionFound ()) {
            checkEventOnTargetDetectedResult (true, sharedActionName);
        } else {
            checkEventOnTargetDetectedResult (false, sharedActionName);
        }
    }

    public void setSetAllSharedActionsActivatorResultAsNotActivatedState (bool state)
    {
        setAllSharedActionsActivatorResultAsNotActivated = state;
    }

    public void setRemoteActivatorEnabledState (bool state)
    {
        remoteActivatorEnabled = state;
    }

    //EDITOR FUNCTIONS
    public void checkSharedActionSystemByExternalDetectionFromEditor ()
    {
        checkSharedActionSystemByExternalDetection (sharedActionName);
    }

    public void setSetAllSharedActionsActivatorResultAsNotActivatedStateFromEditor (bool state)
    {
        setSetAllSharedActionsActivatorResultAsNotActivatedState (state);

        updateComponent ();
    }

    public void activateSharedActionByNameFromEditor ()
    {
        activateSharedActionByName ();
    }

    void updateComponent ()
    {
        GKC_Utils.updateComponent (this);

        GKC_Utils.updateDirtyScene ("Update Shared Action Remote Activator " + gameObject.name, gameObject);
    }

    [System.Serializable]
    public class remoteSharedActionSystemInfo
    {
        [Header ("Main Settings")]
        [Space]

        public string Name;

        public bool checkSharedActionSystmeResultEnabled;

        [Space]
        [Header ("Events Settings")]
        [Space]

        public UnityEvent eventOnSharedActionActivated;

        public UnityEvent eventOnSharedActionNotActivated;
    }
}