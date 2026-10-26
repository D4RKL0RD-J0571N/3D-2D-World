using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class addPatrolSystemToAI : MonoBehaviour
{
    [Header ("Components")]
    [Space]

    public AIPatrolSystem mainAIPatrolSystem;

    public GameObject AIWaypointPatrolUniversalPrefab;

    public AIWayPointPatrol mainAIWaypointPatrol;

    [Space]
    [Space]

    [TextArea (1, 10)]
    public string explanation = "The button Add Patrol System to AI will add a custom patrol to this character.\n\n" +
        "The button Assign AI Waypoint Patrol will search for any previous patrol on scene to assign it to this AI.\n" +
        "So that allows that 2 AI share the same patrol. You can also drop a specific patrol from the scene on the field " +
        "Main AI Waypoint Patrol and it will that the patrol assigned to the character.";

    public void enableOrdisablePatrolOnAI (bool state)
    {
        if (mainAIPatrolSystem == null) {
            playerComponentsManager currentPlayerComponentsManager = GetComponentInChildren<playerComponentsManager> ();

            if (currentPlayerComponentsManager != null) {

                findObjectivesSystem currentFindObjectivesSystem = currentPlayerComponentsManager.getFindObjectivesSystem ();

                if (currentFindObjectivesSystem != null) {
                    mainAIPatrolSystem = currentFindObjectivesSystem.AIPatrolManager;
                }
            }
        }

        if (mainAIPatrolSystem != null) {
            mainAIPatrolSystem.pauseOrPlayPatrol (!state);

            mainAIPatrolSystem.gameObject.SetActive (state);

            GKC_Utils.updateComponent (mainAIPatrolSystem);

            updateComponent ();
        }
    }

    public void assignAIWaypointPatrol ()
    {
        if (mainAIWaypointPatrol == null) {
            mainAIWaypointPatrol = GKC_Utils.findObjectByComponent<AIWayPointPatrol> ();
        }

        if (mainAIWaypointPatrol == null) {
            GameObject newAIWaypointPatrol = (GameObject)Instantiate (AIWaypointPatrolUniversalPrefab, transform.position + Vector3.forward * 6, Quaternion.identity);
            newAIWaypointPatrol.name = "AI Waypoint Patrol";

            mainAIWaypointPatrol = newAIWaypointPatrol.GetComponent<AIWayPointPatrol> ();
        }

        if (mainAIWaypointPatrol != null) {
            playerComponentsManager currentPlayerComponentsManager = GetComponentInChildren<playerComponentsManager> ();

            if (currentPlayerComponentsManager != null) {

                AIPatrolSystem currentAIPatrolSystem = currentPlayerComponentsManager.getAIPatrolSystem ();

                findObjectivesSystem currentFindObjectivesSystem = currentPlayerComponentsManager.getFindObjectivesSystem ();

                if (currentAIPatrolSystem != null) {
                    currentAIPatrolSystem.patrolPath = mainAIWaypointPatrol;

                    currentAIPatrolSystem.pauseOrPlayPatrol (false);

                    currentFindObjectivesSystem.AIPatrolManager = currentAIPatrolSystem;

                    GKC_Utils.updateComponent (currentAIPatrolSystem);

                    GKC_Utils.updateComponent (currentFindObjectivesSystem);

                    updateComponent ();
                }
            }
        }
    }

    public void addPatrolSystem ()
    {
        if (AIWaypointPatrolUniversalPrefab == null) {
            print ("Patrol prefab not configured");

            return;
        }

        findObjectivesSystem currentFindObjectivesSystem = GetComponentInChildren<findObjectivesSystem> ();

        if (currentFindObjectivesSystem != null && mainAIWaypointPatrol == null) {

            GameObject newAIWaypointPatrol = (GameObject)Instantiate (AIWaypointPatrolUniversalPrefab, currentFindObjectivesSystem.transform.position + Vector3.forward * 6, Quaternion.identity);
            newAIWaypointPatrol.name = "AI Waypoint Patrol";

            playerComponentsManager currentPlayerComponentsManager = GetComponentInChildren<playerComponentsManager> ();

            if (currentPlayerComponentsManager != null) {

                AIPatrolSystem currentAIPatrolSystem = currentPlayerComponentsManager.getAIPatrolSystem ();

                if (currentAIPatrolSystem != null) {
                    currentAIPatrolSystem.patrolPath = newAIWaypointPatrol.GetComponent<AIWayPointPatrol> ();

                    currentAIPatrolSystem.pauseOrPlayPatrol (false);

                    currentFindObjectivesSystem.AIPatrolManager = currentAIPatrolSystem;

                    mainAIWaypointPatrol = newAIWaypointPatrol.GetComponent<AIWayPointPatrol> ();

                    GKC_Utils.updateComponent (currentAIPatrolSystem);
                }
            }

            GKC_Utils.updateComponent (currentFindObjectivesSystem);

            updateComponent ();

            print ("Patrol system added to AI");
        } else {
            print ("WARNING: patrol system already configured on this AI");
        }
    }

    void updateComponent ()
    {
        GKC_Utils.updateComponent (this);

        GKC_Utils.updateDirtyScene ("Adding patrol to AI", gameObject);
    }
}
