using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.Events;

public class externalCameraShakeSystem : MonoBehaviour
{
    public bool externalCameraShakeEnabled = true;

    public string externalShakeName;
    public bool shakeUsingDistance;
    public float minDistanceToShake;
    public LayerMask layer;
    public bool useShakeListTriggeredByActions;
    public List<shakeTriggeredByActionInfo> shakeTriggeredByActionList = new List<shakeTriggeredByActionInfo> ();

    public bool useShakeEvent;
    public UnityEvent eventAtStart;
    public UnityEvent eventAtEnd;

    bool currentUseShakeEventValue;
    UnityEvent currentEventAtStart;
    UnityEvent currentEventAtEnd;

    public bool setPlayerManually;
    public GameObject currentPlayer;

    public string mainManagerName = "External Shake List Manager";

    public bool showGizmo;
    public Color gizmoLabelColor = Color.black;
    public int nameIndex;
    public string [] nameList;

    public externalShakeListManager externalShakeManager;

    float distancePercentage;
    headBob headBobManager;

    bool playerAssignedManually;

    public void getExternalShakeList ()
    {
        bool externalShakeManagerLocated = false;

        if (Application.isPlaying) {
            externalShakeManagerLocated = externalShakeManager != null;

            if (!externalShakeManagerLocated) {
                externalShakeManager = externalShakeListManager.Instance;

                externalShakeManagerLocated = externalShakeManager != null;
            }

            if (!externalShakeManagerLocated) {
                GKC_Utils.instantiateMainManagerOnSceneWithTypeOnApplicationPlaying (externalShakeListManager.getMainManagerName (), typeof (externalShakeListManager), true);

                externalShakeManager = externalShakeListManager.Instance;

                externalShakeManagerLocated = externalShakeManager != null;
            }
        } else {
            externalShakeManagerLocated = externalShakeManager != null;

            if (!externalShakeManagerLocated) {
                externalShakeManager = GKC_Utils.findObjectByComponent<externalShakeListManager> ();

                externalShakeManagerLocated = externalShakeManager != null;
            }

            if (!externalShakeManagerLocated) {
                GKC_Utils.instantiateMainManagerOnSceneWithType (externalShakeListManager.getMainManagerName (), typeof (externalShakeListManager));

                externalShakeManager = GKC_Utils.findObjectByComponent<externalShakeListManager> ();

                externalShakeManagerLocated = externalShakeManager != null;
            }
        }

        if (externalShakeManagerLocated) {
            nameList = new string [externalShakeManager.externalShakeInfoList.Count];

            for (int i = 0; i < externalShakeManager.externalShakeInfoList.Count; i++) {
                nameList [i] = externalShakeManager.externalShakeInfoList [i].name;
            }

            updateComponent ();
        }
    }

    public void setCurrentPlayer (GameObject player)
    {
        if (player == null) {
            return;
        }

        currentPlayer = player;

        if (headBobManager == null) {
            playerComponentsManager mainPlayerComponentsManager = currentPlayer.GetComponent<playerComponentsManager> ();

            if (mainPlayerComponentsManager != null) {
                headBobManager = mainPlayerComponentsManager.getHeadBob ();
            }
        }
    }

    public void setCurrentPlayerAndActivateCameraShake (GameObject player)
    {
        setCurrentPlayer (player);

        setCameraShake ();
    }

    public void setCameraShake ()
    {
        if (!externalCameraShakeEnabled) {
            return;
        }

        currentUseShakeEventValue = useShakeEvent;

        if (currentUseShakeEventValue) {
            currentEventAtStart = eventAtStart;
            currentEventAtEnd = eventAtEnd;
        }

        if (setPlayerManually) {
            if (!playerAssignedManually) {
                setCurrentPlayer (currentPlayer);

                playerAssignedManually = true;
            }
        }

        setCameraShakeByName (externalShakeName);

        currentUseShakeEventValue = false;
    }

    public void setCameraShakeByAction (string actionName)
    {
        if (!externalCameraShakeEnabled) {
            return;
        }

        int shakeTriggeredByActionListCount = shakeTriggeredByActionList.Count;

        for (int i = 0; i < shakeTriggeredByActionListCount; i++) {
            if (shakeTriggeredByActionList [i].actionName.Equals (actionName)) {

                currentUseShakeEventValue = shakeTriggeredByActionList [i].useShakeEvent;

                if (currentUseShakeEventValue) {
                    currentEventAtStart = shakeTriggeredByActionList [i].eventAtStart;
                    currentEventAtEnd = shakeTriggeredByActionList [i].eventAtEnd;
                }

                setPlayerManually = true;

                if (setPlayerManually) {
                    setCurrentPlayer (currentPlayer);
                }

                setCameraShakeByName (shakeTriggeredByActionList [i].shakeName);

                currentUseShakeEventValue = false;
                return;
            }
        }
    }

    public void setCameraShakeByName (string shakeName)
    {
        if (!externalCameraShakeEnabled) {
            return;
        }

        if (setPlayerManually) {
            if (currentPlayer != null) {
                setExternalShakeSignal (currentPlayer, shakeName);
            }
        } else {
            List<Collider> colliders = new List<Collider> ();

            colliders.AddRange (Physics.OverlapSphere (transform.position, minDistanceToShake, layer));

            int collidersCount = colliders.Count;

            for (int i = 0; i < collidersCount; i++) {
                Collider hit = colliders [i];

                if (hit != null && !hit.isTrigger) {
                    setExternalShakeSignal (hit.gameObject, shakeName);
                }
            }
        }
    }

    public void setExternalShakeSignal (GameObject objectToCall, string shakeName)
    {
        if (!externalCameraShakeEnabled) {
            return;
        }

        if (shakeUsingDistance) {
            float currentDistance = GKC_Utils.distance (objectToCall.transform.position, transform.position);

            if (currentDistance <= minDistanceToShake) {
                distancePercentage = currentDistance / minDistanceToShake;
                distancePercentage = 1 - distancePercentage;
            } else {
                return;
            }
        } else {
            distancePercentage = 1;
        }

        if (!setPlayerManually) {
            playerComponentsManager mainPlayerComponentsManager = objectToCall.GetComponent<playerComponentsManager> ();

            if (mainPlayerComponentsManager != null) {
                headBobManager = mainPlayerComponentsManager.getHeadBob ();
            }
        }

        if (headBobManager != null) {
            if (currentUseShakeEventValue) {
                headBobManager.setCurrentExternalCameraShakeSystemEvents (currentEventAtStart, currentEventAtEnd);
            } else {
                headBobManager.disableUseShakeEventState ();
            }

            headBobManager.setExternalShakeStateByName (shakeName, distancePercentage);
        }
    }

    public void updateComponent ()
    {
        GKC_Utils.updateComponent (this);

        GKC_Utils.updateDirtyScene ("Updating External Camera Shake System ", gameObject);
    }

    void OnDrawGizmos ()
    {
        if (!showGizmo) {
            return;
        }

        if (GKC_Utils.isCurrentSelectionActiveGameObject (gameObject)) {
            DrawGizmos ();
        }
    }

    void OnDrawGizmosSelected ()
    {
        DrawGizmos ();
    }

    void DrawGizmos ()
    {
        if (showGizmo) {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere (transform.position, minDistanceToShake);
        }
    }

    [System.Serializable]
    public class shakeTriggeredByActionInfo
    {
        public string actionName;
        public string shakeName;
        public int nameIndex;

        public bool useShakeEvent;
        public UnityEvent eventAtStart;
        public UnityEvent eventAtEnd;
    }
}
