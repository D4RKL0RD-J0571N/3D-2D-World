using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class enableOrDisableAIOnDistanceManager : MonoBehaviour
{
    [Header ("Behavior Main Settings")]
    [Space]

    public bool enableOrDisableAIOnDistanceEnabled = true;

    public bool setActiveStateOnPlayerCameraGameObject;

    [Space]
    [Header ("Debug")]
    [Space]

    public bool AIEnableActive = true;

    [Space]
    [Header ("Components")]
    [Space]

    public playerController mainPlayerController;
    public playerCamera mainPlayerCamera;
    public AINavMesh mainAINavmesh;
    public GameObject playerCameraGameObject;
    public playerStatesManager mainPlayerStatesManager;


    public void enableOrDisbleAI (bool state)
    {
        if (enableOrDisableAIOnDistanceEnabled) {
            if (AIEnableActive == state) {
                return;
            }

            AIEnableActive = state;

            if (!AIEnableActive) {
                mainAINavmesh.pauseAI (true);

                mainPlayerStatesManager.checkPlayerStates ();
            }

            mainPlayerController.setCharacterMeshGameObjectState (AIEnableActive);

            mainPlayerController.getGravityCenter ().gameObject.SetActive (AIEnableActive);

            mainPlayerController.setAnimatorState (AIEnableActive);

            if (setActiveStateOnPlayerCameraGameObject) {
                playerCameraGameObject.SetActive (AIEnableActive);
            } else {
                mainPlayerCamera.enableOrDisablePlayerCameraGameObject (AIEnableActive);
            }

            if (AIEnableActive) {
                mainAINavmesh.pauseAI (false);
            }
        }
    }

    public void setEnableOrDisableAIOnDistanceEnabledState (bool state)
    {
        enableOrDisableAIOnDistanceEnabled = state;
    }
}
