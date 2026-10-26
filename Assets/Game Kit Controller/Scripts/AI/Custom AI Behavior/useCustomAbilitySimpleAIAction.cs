using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class useCustomAbilitySimpleAIAction : simpleAIAction
{
    [Header ("Custom Settings")]
    [Space]

    public string currentAbilityName;

    public float minWaitTimeToFinishAbility = 0.4f;

    public float waitTimeToUseAbility = 0.5f;

    [Space]
    [Header ("Other Settings")]
    [Space]

    public bool setMoveNavMeshPaused;
    public float moveNamveshPausedDuration;

    [Space]
    [Header ("Debug")]
    [Space]

    public bool abilityInProcess;

    [Space]
    [Header ("Components")]
    [Space]

    public AIAbilitiesSystemBrain mainAIAbilitiesSystemBrain;
    public findObjectivesSystem mainFindObjectivesSystem;
    public AINavMesh mainAINavmesh;


    float lastTimeAbilityUsed;

    bool targetInfoAssigned;

    bool abilityActivated;

    Coroutine setMoveNavMeshPausedCoroutine;


    public void setCurrentAbilityName (string abilityName)
    {
        currentAbilityName = abilityName;
    }

    public void setCurrentAbilityNameAndStartAIAction (string abilityName)
    {
        setCurrentAbilityName (abilityName);

        startAIAction ();
    }

    public override void startAIAction ()
    {
        base.startAIAction ();

        if (actionActive) {
            abilityActivated = false;

            targetInfoAssigned = false;

            lastTimeAbilityUsed = Time.time;

            abilityInProcess = true;
        }
    }

    public override void updateSystem ()
    {
        if (!actionActive) {
            return;
        }


        if (!mainAIAbilitiesSystemBrain.isAbilityInProcess () && Time.time > minWaitTimeToFinishAbility + lastTimeAbilityUsed) {
            endAIAction ();

            return;
        }

        if (setMoveNavMeshPaused) {
            mainAINavmesh.setMoveNavMeshPausedDuringActionState (true);

            if (moveNamveshPausedDuration > 0) {
                if (setMoveNavMeshPausedCoroutine != null) {
                    StopCoroutine (setMoveNavMeshPausedCoroutine);
                }

                setMoveNavMeshPausedCoroutine = StartCoroutine (updateSetMoveNavMeshPausedCoroutine ());
            }
        }

        if (targetInfoAssigned) {

            mainFindObjectivesSystem.lookAtCurrentPlaceToShoot ();

            mainAINavmesh.lookAtTaget (true);

            mainAINavmesh.moveNavMesh (Vector3.zero, false, false);
        } else {
            if (currentTarget != null) {
                mainFindObjectivesSystem.setCustomTargetToLook (currentTarget.gameObject);

                targetInfoAssigned = true;
            }
        }

        if (!abilityActivated) {
            if (Time.time > waitTimeToUseAbility + lastTimeAbilityUsed) {
                mainAIAbilitiesSystemBrain.setAndActivateAbilityByName (currentAbilityName);

                abilityActivated = true;
            }
        }
    }

    public override void resetStatesOnActionEnd ()
    {
        mainAIAbilitiesSystemBrain.setAndActivateAbilityByName (currentAbilityName);

        mainFindObjectivesSystem.AINavMeshManager.lookAtTaget (false);

        mainFindObjectivesSystem.AINavMeshManager.moveNavMesh (Vector3.zero, false, false);

        mainFindObjectivesSystem.resetAITargets ();

        if (setMoveNavMeshPaused) {
            if (moveNamveshPausedDuration <= 0) {
                mainAINavmesh.setMoveNavMeshPausedDuringActionState (false);
            }
        }

        abilityInProcess = false;

        currentTarget = null;

        abilityActivated = false;
    }

    IEnumerator updateSetMoveNavMeshPausedCoroutine ()
    {
        WaitForSeconds delay = new WaitForSeconds (moveNamveshPausedDuration);

        yield return delay;

        mainAINavmesh.setMoveNavMeshPausedDuringActionState (false);
    }
}
