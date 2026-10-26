using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using static AIWayPointPatrol;
using UnityEngine.Events;

public class AIPatrolSystem : MonoBehaviour
{
    [Header ("Main Settings")]
    [Space]

    public bool paused;
    public float minDistanceToNextPoint = 0.6f;
    public float patrolSpeed = 0.2f;
    public float returnToPatrolSpeed = 1;

    public bool moveOnReversePatrolDirectionEnabled;

    [Space]

    public bool useCurrentPatrolIndexOnStart;
    public int currentPatrolIndex = 0;

    [Space]
    [Header ("Patrol Time Settings")]
    [Space]

    public bool useGeneralWaitTime = true;
    public float generalWaitTimeBetweenPoints;

    [Space]

    public bool moveBetweenPatrolsInOrder = true;

    public float fixedTimeToChangeBetweenPatrols;

    [Space]
    [Header ("Random Patrol Settings")]
    [Space]

    public bool changeBetweenPointRandomly = true;
    [Range (0, 10)] public int changeRandomlyProbability = 1;
    public bool useTimeToChangeBetweenPointRandomly;

    public bool useRandomTimeToChangePatrol;
    public Vector2 randomTimeLimits;

    [Space]
    [Header ("Debug")]
    [Space]

    public bool showDebugPrint;
    public bool returningToPatrol;
    public bool AIIsDestroyed;

    [Space]
    [Header ("Gizmo Settings")]
    [Space]

    public bool showGizmo;
    public float gizmoRadius;

    [Space]
    [Header ("Event Settings")]
    [Space]

    public bool useEventsOnPatrolWait;
    public UnityEvent eventsOnPatrolWait;

    [Space]
    [Header ("Components")]
    [Space]

    [Tooltip ("An AIWayPointPatrol path in your scene that this AI should follow. To create one add a GameObject with AIWayPointPatrol component and edit the Way Points then drag the GameObject here.")]
    public AIWayPointPatrol patrolPath;

    public Transform AICharacter;
    public AINavMesh mainAINavmesh;

    bool patrolAssigned;

    Transform currentWayPoint;

    int currentWaypointIndex = 0;
    Coroutine movement;
    bool settingNextPoint;
    float lastTimeChanged;
    float distanceToPoint;

    Transform closestWaypointTransform;

    List<patrolElementInfo> patrolList = new List<patrolElementInfo> ();

    bool patrolListAssigned;

    void Start ()
    {
        if (AICharacter == null) {
            AICharacter = transform;
        }

        if (mainAINavmesh == null) {
            mainAINavmesh = AICharacter.GetComponent<AINavMesh> ();
        }

        checkPatrolListAssigned ();

        if (patrolPath != null) {
            setClosestWayPoint ();
        }
    }

    void checkPatrolListAssigned ()
    {
        if (!patrolListAssigned) {
            if (patrolPath != null) {
                patrolList = patrolPath.patrolList;

                patrolListAssigned = true;
            }
        }
    }

    void Update ()
    {
        if (!paused && patrolAssigned && !settingNextPoint) {

            if (AIIsDestroyed) {
                enabled = false;

                return;
            }

            if (!mainAINavmesh.isPatrolPaused ()) {
                if (AICharacter == null) {
                    AIIsDestroyed = true;

                    return;
                }

                distanceToPoint = GKC_Utils.distance (AICharacter.position, currentWayPoint.position);

                if (distanceToPoint < minDistanceToNextPoint) {
                    if (returningToPatrol) {
                        mainAINavmesh.setPatrolSpeed (patrolSpeed);

                        returningToPatrol = false;
                    }

                    bool setRandomWayPoint = false;

                    if (changeBetweenPointRandomly) {
                        int changeOrNotBool = Random.Range (0, (changeRandomlyProbability + 1));

                        if (changeOrNotBool == 0) {
                            setRandomWayPoint = true;
                            //print ("random waypoint");
                        }
                    }

                    if (setRandomWayPoint) {
                        setNextRandomWaypoint ();
                    } else {
                        setNextWaypoint ();
                        //print ("in order");
                    }
                }

                if (changeBetweenPointRandomly) {
                    if (useTimeToChangeBetweenPointRandomly) {
                        if (useRandomTimeToChangePatrol) {

                        } else {
                            if (Time.time > fixedTimeToChangeBetweenPatrols + lastTimeChanged) {
                                lastTimeChanged = Time.time;

                                setNextRandomWaypoint ();
                                //print ("random waypoint");
                            }
                        }
                    }
                }
            }
        }
    }

    public void pauseOrPlayPatrol (bool state)
    {
        if (showDebugPrint) {
            print ("patrol paused state " + state);
        }

        paused = state;
    }

    public bool isPatrolPaused ()
    {
        return paused;
    }

    public Transform closestWaypoint (Vector3 currentPosition)
    {
        float distance = Mathf.Infinity;

        int patrolListCount = patrolList.Count;

        if (useCurrentPatrolIndexOnStart) {
            int wayPointsCount = patrolList [currentPatrolIndex].wayPoints.Count;

            for (int j = 0; j < wayPointsCount; j++) {
                float currentDistance = GKC_Utils.distance (currentPosition, patrolList [currentPatrolIndex].wayPoints [j].position);

                if (currentDistance < distance) {
                    distance = currentDistance;
                    currentWaypointIndex = j;
                }
            }
        } else {
            for (int i = 0; i < patrolListCount; i++) {

                int wayPointsCount = patrolList [i].wayPoints.Count;

                for (int j = 0; j < wayPointsCount; j++) {
                    float currentDistance = GKC_Utils.distance (currentPosition, patrolList [i].wayPoints [j].position);

                    if (currentDistance < distance) {
                        distance = currentDistance;
                        currentPatrolIndex = i;
                        currentWaypointIndex = j;
                    }
                }
            }
        }

        closestWaypointTransform = patrolList [currentPatrolIndex].wayPoints [currentWaypointIndex];

        return closestWaypointTransform;
    }

    public void setNextPatrolList ()
    {
        currentPatrolIndex++;

        if (currentPatrolIndex > patrolList.Count - 1) {
            currentPatrolIndex = 0;
        }

        settingNextPoint = false;

        Vector3 currentPosition = AICharacter.position;

        float distance = Mathf.Infinity;

        int wayPointsCount = patrolList [currentPatrolIndex].wayPoints.Count;

        for (int j = 0; j < wayPointsCount; j++) {
            float currentDistance = GKC_Utils.distance (currentPosition, patrolList [currentPatrolIndex].wayPoints [j].position);

            if (currentDistance < distance) {
                distance = currentDistance;
                currentWaypointIndex = j;
            }
        }

        currentWayPoint = patrolList [currentPatrolIndex].wayPoints [currentWaypointIndex];

        setCurrentPatrolTarget (currentWayPoint);
    }

    public void setNextWaypoint ()
    {
        if (movement != null) {
            StopCoroutine (movement);
        }

        settingNextPoint = false;

        if (mainAINavmesh.isPatrolPaused ()) {
            return;
        }

        checkPatrolListAssigned ();

        movement = StartCoroutine (setNextWayPointCoroutine ());
    }

    IEnumerator setNextWayPointCoroutine ()
    {
        mainAINavmesh.removeTarget ();

        settingNextPoint = true;

        if (useEventsOnPatrolWait) {
            eventsOnPatrolWait.Invoke ();
        }

        if (useGeneralWaitTime) {
            WaitForSeconds delay = new WaitForSeconds (generalWaitTimeBetweenPoints);

            yield return delay;
        } else {
            WaitForSeconds delay = new WaitForSeconds (patrolPath.waitTimeBetweenPoints);

            yield return delay;
        }

        if (!mainAINavmesh.isPatrolPaused ()) {
            if (moveOnReversePatrolDirectionEnabled) {
                currentWaypointIndex--;

                if (currentWaypointIndex < 0) {
                    currentWaypointIndex = patrolList [currentPatrolIndex].wayPoints.Count - 1;

                    if (moveBetweenPatrolsInOrder) {
                        currentPatrolIndex++;

                        if (currentPatrolIndex > patrolList.Count - 1) {
                            currentPatrolIndex = 0;
                        }

                        currentWaypointIndex = patrolList [currentPatrolIndex].wayPoints.Count - 1;
                    }
                }
            } else {
                currentWaypointIndex++;

                if (currentWaypointIndex > patrolList [currentPatrolIndex].wayPoints.Count - 1) {
                    currentWaypointIndex = 0;

                    if (moveBetweenPatrolsInOrder) {
                        currentPatrolIndex++;

                        if (currentPatrolIndex > patrolList.Count - 1) {
                            currentPatrolIndex = 0;
                        }
                    }
                }
            }

            currentWayPoint = patrolList [currentPatrolIndex].wayPoints [currentWaypointIndex];

            setCurrentPatrolTarget (currentWayPoint);
        }

        settingNextPoint = false;
    }

    public void setNextRandomWaypoint ()
    {
        if (movement != null) {
            StopCoroutine (movement);
        }

        settingNextPoint = false;

        if (mainAINavmesh.isPatrolPaused ()) {
            return;
        }

        checkPatrolListAssigned ();

        movement = StartCoroutine (setNextRandomWayPointCoroutine ());
    }

    IEnumerator setNextRandomWayPointCoroutine ()
    {
        mainAINavmesh.removeTarget ();

        settingNextPoint = true;

        if (useGeneralWaitTime) {
            WaitForSeconds delay = new WaitForSeconds (generalWaitTimeBetweenPoints);

            yield return delay;
        } else {
            WaitForSeconds delay = new WaitForSeconds (patrolPath.waitTimeBetweenPoints);

            yield return delay;
        }

        if (!mainAINavmesh.isPatrolPaused ()) {

            int currentWaypointIndexCopy = currentWaypointIndex;
            int currentPatrolIndexCopy = currentPatrolIndex;
            int checkLoop = 0;

            if (patrolList.Count > 1) {
                while (currentPatrolIndexCopy == currentPatrolIndex) {
                    currentPatrolIndex = Random.Range (0, patrolList.Count);

                    checkLoop++;

                    if (checkLoop > 100) {
                        //	print ("loop error");
                        break;
                    }
                }
            }

            checkLoop = 0;

            while (currentWaypointIndexCopy == currentWaypointIndex) {
                currentWaypointIndex = Random.Range (0, patrolList [currentPatrolIndex].wayPoints.Count);

                checkLoop++;

                if (checkLoop > 100) {
                    //print ("loop error");
                    break;
                }
            }

            //print ("Next patrol: " + (currentPatrolIndex+1) + " and next waypoint: " + (currentWaypointIndex+1));
            currentWayPoint = patrolList [currentPatrolIndex].wayPoints [currentWaypointIndex];

            setCurrentPatrolTarget (currentWayPoint);
        }

        settingNextPoint = false;
    }

    public void setClosestWayPoint ()
    {
        if (paused) {
            return;
        }

        checkPatrolListAssigned ();

        patrolAssigned = true;

        currentWayPoint = closestWaypoint (AICharacter.position);

        setCurrentPatrolTarget (currentWayPoint);

        mainAINavmesh.setPatrolSpeed (patrolSpeed);
    }

    public void setCurrentPatrolTarget (Transform newTarget)
    {
        mainAINavmesh.setPatrolTarget (newTarget);

        mainAINavmesh.setPatrolState (true);
    }

    public void setReturningToPatrolState (bool state)
    {
        returningToPatrol = true;

        if (returningToPatrol) {
            mainAINavmesh.setPatrolSpeed (returnToPatrolSpeed);
        }
    }

    public void resumePatrolStateOnAI ()
    {
        pauseOrPlayPatrol (false);

        setClosestWayPoint ();
    }

    public void pausePatrolStateOnAI ()
    {
        pauseOrPlayPatrol (true);

        mainAINavmesh.setPatrolState (false);
    }

#if UNITY_EDITOR
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
            if (patrolList.Count > 0) {
                Gizmos.color = Color.red;
                Gizmos.DrawSphere (patrolList [currentPatrolIndex].wayPoints [currentWaypointIndex].transform.position, gizmoRadius);
            }
        }
    }
#endif
}