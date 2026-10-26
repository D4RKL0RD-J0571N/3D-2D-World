using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public class AIWalkToPosition : MonoBehaviour
{
    [Header ("Main Settings")]
    [Space]

    public bool walkToPositionEnabled = true;

    public float maxWalkSpeed = 1;

    public Transform walkPositionTransform;

    public bool activateDynamicObstacleDetection;

    public bool assignPartnerIfFoundOnPositionReached;

    public bool removePartnerIfFoundOnPositionReached;

    [Space]
    [Header ("Debug")]
    [Space]

    public bool showDebugPrint;
    public bool walkToPositionInProcess;

    [Space]
    [Header ("Components")]
    [Space]

    public Transform characterTransform;

    public AINavMesh mainAINavmesh;

    public findObjectivesSystem mainFindObjectivesSystem;
    public playerController mainPlayerController;

    Coroutine updateCoroutine;

    bool dynamicObstacleDetectionChecked = false;
    bool dynamicObstacleActiveChecked = false;

    float lastTimeWalkToPositionActive;

    public void activateWalkToPosition ()
    {
        if (!walkToPositionEnabled) {
            return;
        }

        if (showDebugPrint) {
            print ("start walk");
        }

        stopUpdateCoroutine ();

        pauseAIState ();

        dynamicObstacleDetectionChecked = false;
        dynamicObstacleActiveChecked = false;

        lastTimeWalkToPositionActive = Time.time;

        updateCoroutine = StartCoroutine (updateSystemCoroutine ());
    }

    public void stopActivateWalkPosition ()
    {
        if (walkToPositionInProcess) {
            stopUpdateCoroutine ();

            resumeAIState ();

            walkToPositionInProcess = false;
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
        var waitTime = new WaitForFixedUpdate ();

        while (true) {
            updateSystem ();

            yield return waitTime;
        }
    }

    void updateSystem ()
    {
        float currentDistanceToTarget = GKC_Utils.distance (walkPositionTransform.position, characterTransform.position);

        if (currentDistanceToTarget < 2) {
            if (!dynamicObstacleDetectionChecked) {
                if (activateDynamicObstacleDetection) {
                    mainAINavmesh.setUseDynamicObstacleDetectionState (false);
                }

                dynamicObstacleDetectionChecked = true;
            }
        } else {
            if (!dynamicObstacleActiveChecked) {
                if (activateDynamicObstacleDetection) {
                    mainAINavmesh.setUseDynamicObstacleDetectionState (true);
                }

                dynamicObstacleActiveChecked = true;
            }
        }

        bool targetReached = false;

        if (Time.time > lastTimeWalkToPositionActive + 0.2f) {
            if (!mainAINavmesh.isFollowingTarget ()) {
                targetReached = true;
            }

            if (currentDistanceToTarget < 0.2f) {
                targetReached = true;
            }
        }

        if (mainPlayerController.isPlayerDead ()) {
            stopUpdateCoroutine ();

            walkToPositionInProcess = false;

            resetStatesOnDeadAI ();
        }

        if (targetReached) {

            resumeAIState ();

            stopUpdateCoroutine ();
        }
    }

    void pauseAIState ()
    {
        walkToPositionInProcess = true;

        mainPlayerController.setActionActiveState (true);

        mainAINavmesh.pauseAI (true);

        mainAINavmesh.pauseAI (false);

        mainFindObjectivesSystem.checkPauseOrResumePatrolStateDuringActionActive (true);

        mainAINavmesh.enableCustomNavMeshSpeed (maxWalkSpeed);

        mainAINavmesh.setTarget (walkPositionTransform);
        mainAINavmesh.setTargetType (false, true);

        mainAINavmesh.enableCustomMinDistance (0.22f);

        mainFindObjectivesSystem.setSearchingObjectState (true);

        mainFindObjectivesSystem.disableStrafeModeIfActive ();
    }

    void resumeAIState ()
    {
        mainAINavmesh.removeTarget ();

        mainAINavmesh.disableCustomNavMeshSpeed ();

        mainAINavmesh.disableCustomMinDistance ();

        mainAINavmesh.pauseAI (true);

        mainAINavmesh.pauseAI (false);

        mainPlayerController.setActionActiveState (false);

        if (assignPartnerIfFoundOnPositionReached && mainAINavmesh.isPartnerLocated ()) {
            mainAINavmesh.setTarget (mainAINavmesh.getCurrentPartner ());

            mainAINavmesh.setTargetType (true, false);
        } else {
            if (removePartnerIfFoundOnPositionReached) {
                if (mainAINavmesh.isPartnerLocated ()) {
                    mainFindObjectivesSystem.removePartner ();

                    mainFindObjectivesSystem.checkIfResumeAfterRemovingPartner ();
                }
            }

            mainFindObjectivesSystem.checkPauseOrResumePatrolStateDuringActionActive (false);
        }

        if (activateDynamicObstacleDetection) {
            mainAINavmesh.setOriginalUseDynamicObstacleDetection ();
        }

        mainFindObjectivesSystem.setSearchingObjectState (false);

        walkToPositionInProcess = false;

        if (showDebugPrint) {
            print ("end walk");
        }
    }

    void resetStatesOnDeadAI ()
    {
        mainAINavmesh.disableCustomNavMeshSpeed ();

        mainAINavmesh.disableCustomMinDistance ();

        mainPlayerController.setActionActiveState (false);

        if (activateDynamicObstacleDetection) {
            mainAINavmesh.setOriginalUseDynamicObstacleDetection ();
        }

        mainFindObjectivesSystem.setSearchingObjectState (false);
    }
}