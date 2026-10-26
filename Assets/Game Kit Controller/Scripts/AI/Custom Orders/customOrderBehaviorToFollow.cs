using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class customOrderBehaviorToFollow : customOrderBehavior
{
    [Header ("Custom Settings")]
    [Space]

    public List<string> tagToLocate = new List<string> ();

    public bool checkIfTargetOnLockOnViewEnabled = true;

    public bool sendPlayerAsTarget = true;

    public bool ignoreToGetClosestEnemy;

    [Space]
    [Header ("Target Detection Settings")]
    [Space]

    public bool getClosestTargetToCameraViewIfNoLockOnActive;

    public float maxDistanceToFindTarget = 300;

    public bool searchPointToLookComponents = true;

    public bool lookOnlyIfTargetOnScreen;

    public bool checkObstaclesToTarget;

    public LayerMask layerToLook;

    public LayerMask pointToLookComponentsLayer;

    public bool getClosestToCameraCenter;

    public bool useMaxDistanceToCameraCenter;

    public float maxDistanceToCameraCenter = 200;

    [Space]
    [Header ("Debug")]
    [Space]

    public bool showCameraGizmo;

    [Space]
    [Header ("Components")]
    [Space]

    public Transform mainCameraTransform;
    public Transform playerCameraTransform;
    public playerCamera mainPlayerCamera;
    public Camera mainCamera;

    public override void activateOrder (Transform character)
	{


	}


	public override Transform getCustomTarget (Transform character, Transform orderOwner)
	{
        if (sendPlayerAsTarget) {
            return orderOwner;
        }

        if (canAIAttack (character)) {

            if (checkIfTargetOnLockOnViewEnabled) {
                playerComponentsManager currentPlayerComponentsManager = orderOwner.GetComponent<playerComponentsManager> ();

                if (currentPlayerComponentsManager != null) {
                    playerCamera currentPlayerCamera = currentPlayerComponentsManager.getPlayerCamera ();

                    if (currentPlayerCamera != null) {
                        if (currentPlayerCamera.isPlayerLookingAtTarget ()) {
                            Transform currentTarget = currentPlayerCamera.getLastCharacterToLook ();

                            if (currentTarget != null) {
                                if (applyDamage.isCharacter (currentTarget.gameObject)) {
                                    if (showDebugPrint) {
                                        print ("target located " + currentTarget.name);
                                    }

                                    return currentTarget;
                                } else {
                                    if (applyDamage.getCharacterFromPlaceToShoot (currentTarget)) {
                                        if (showDebugPrint) {
                                            print ("target located " + currentTarget.name);
                                        }

                                        return currentTarget;
                                    }
                                }
                            }
                        }

                        if (getClosestTargetToCameraViewIfNoLockOnActive) {
                            Transform currentTarget = getClosestEnemyToScreenView ();

                            if (currentTarget != null) {
                                if (applyDamage.isCharacter (currentTarget.gameObject)) {
                                    if (showDebugPrint) {
                                        print ("target located " + currentTarget.name);
                                    }

                                    return currentTarget;
                                }
                            }
                        }
                    }
                }
            }

            if (ignoreToGetClosestEnemy) {
                return orderOwner;
            }

            Transform target = getClosestEnemy (orderOwner);

            if (target == null) {
                target = orderOwner;
            }

            return target;
        }

        return null;
    }

    public bool canAIAttack (Transform AIFriend)
    {
        bool canAttack = false;

        findObjectivesSystem currentFindObjectivesSystem = AIFriend.GetComponentInChildren<findObjectivesSystem> ();

        if (currentFindObjectivesSystem != null) {
            if (currentFindObjectivesSystem.attackType != findObjectivesSystem.AIAttackType.none) {
                canAttack = true;
            }
        }

        return canAttack;
    }

    public Transform getClosestEnemy (Transform centerPointTransform)
    {
        Vector3 centerPosition = centerPointTransform.position;

        List<GameObject> fullEnemyList = new List<GameObject> ();

        GameObject closestEnemy;

        for (int i = 0; i < tagToLocate.Count; i++) {
            GameObject [] enemiesList = GameObject.FindGameObjectsWithTag (tagToLocate [i]);

            fullEnemyList.AddRange (enemiesList);
        }

        List<GameObject> closestEnemyList = new List<GameObject> ();

        for (int j = 0; j < fullEnemyList.Count; j++) {
            if (!applyDamage.checkIfDead (fullEnemyList [j])) {
                closestEnemyList.Add (fullEnemyList [j]);
            }
        }

        if (closestEnemyList.Count > 0) {
            float distance = Mathf.Infinity;

            int index = -1;

            for (int j = 0; j < closestEnemyList.Count; j++) {
                float currentDistance = GKC_Utils.distance (closestEnemyList [j].transform.position, centerPosition);

                if (currentDistance < distance) {
                    distance = currentDistance;
                    index = j;
                }
            }

            if (index != -1) {
                closestEnemy = closestEnemyList [index];

                return closestEnemy.transform;
            }
        }

        return null;
    }
    public Transform getClosestEnemyToScreenView ()
    {
        List<Collider> targetsListCollider = new List<Collider> ();

        List<GameObject> targetList = new List<GameObject> ();
        List<GameObject> fullTargetList = new List<GameObject> ();

        List<Transform> targetsListToLookTransform = new List<Transform> ();

        int tagToLocateCount = tagToLocate.Count;

        for (int i = 0; i < tagToLocateCount; i++) {
            GameObject [] enemiesList = GameObject.FindGameObjectsWithTag (tagToLocate [i]);
            targetList.AddRange (enemiesList);
        }

        int targetListCount = targetList.Count;

        for (int i = 0; i < targetListCount; i++) {
            float distance = GKC_Utils.distance (targetList [i].transform.position, playerCameraTransform.position);

            if (distance < maxDistanceToFindTarget) {
                fullTargetList.Add (targetList [i]);
            }
        }

        List<GameObject> pointToLookComponentList = new List<GameObject> ();

        if (searchPointToLookComponents) {
            targetsListCollider.Clear ();

            targetsListCollider.AddRange (Physics.OverlapSphere (playerCameraTransform.position, maxDistanceToFindTarget, pointToLookComponentsLayer));

            int targetsListColliderCount = targetsListCollider.Count;

            for (int i = 0; i < targetsListColliderCount; i++) {
                if (targetsListCollider [i].isTrigger) {
                    pointToLook currentPointToLook = targetsListCollider [i].GetComponent<pointToLook> ();

                    if (currentPointToLook != null) {
                        if (currentPointToLook.isPointToLookEnabled ()) {
                            GameObject currenTargetToLook = currentPointToLook.getPointToLookTransform ().gameObject;

                            fullTargetList.Add (currenTargetToLook);

                            pointToLookComponentList.Add (currenTargetToLook);
                        }
                    }
                }
            }
        }

        bool isUsingScreenSpaceCamera = mainPlayerCamera.isUsingScreenSpaceCamera ();

        float screenWidth = Screen.width;
        float screenHeight = Screen.height;

        Vector3 screenPoint;

        bool targetOnScreen;

        int fullTargetListCount = fullTargetList.Count;

        RaycastHit hit = new RaycastHit ();

        for (int i = 0; i < fullTargetListCount; i++) {
            if (fullTargetList [i] != null) {
                GameObject currentTarget = fullTargetList [i];

                if (tagToLocate.Contains (currentTarget.tag) || pointToLookComponentList.Contains (currentTarget)) {
                    bool objectVisible = false;
                    bool obstacleDetected = false;

                    Vector3 targetPosition = currentTarget.transform.position;

                    if (lookOnlyIfTargetOnScreen) {
                        Transform currentTargetPlaceToShoot = applyDamage.getPlaceToShoot (currentTarget);

                        if (currentTargetPlaceToShoot != null) {
                            targetPosition = currentTargetPlaceToShoot.position;
                        }

                        if (isUsingScreenSpaceCamera) {
                            screenPoint = mainCamera.WorldToViewportPoint (targetPosition);
                            targetOnScreen = screenPoint.z > 0 && screenPoint.x > 0 && screenPoint.x < 1 && screenPoint.y > 0 && screenPoint.y < 1;
                        } else {
                            screenPoint = mainCamera.WorldToScreenPoint (targetPosition);
                            targetOnScreen = screenPoint.z > 0 && screenPoint.x > 0 && screenPoint.x < screenWidth && screenPoint.y > 0 && screenPoint.y < screenHeight;
                        }

                        //the target is visible in the screen
                        if (targetOnScreen) {
                            objectVisible = true;
                        }
                    } else {
                        objectVisible = true;
                    }

                    if (objectVisible && checkObstaclesToTarget) {
                        //for every target in front of the camera, use a raycast, if it finds an obstacle between the target and the camera, the target is removed from the list
                        Vector3 temporaltargetPosition = targetPosition;

                        Transform temporalPlaceToShoot = applyDamage.getPlaceToShoot (currentTarget);

                        if (temporalPlaceToShoot != null) {
                            temporaltargetPosition = temporalPlaceToShoot.position;
                        }

                        Vector3 direction = temporaltargetPosition - mainCameraTransform.position;

                        direction = direction / direction.magnitude;

                        float distance = GKC_Utils.distance (temporaltargetPosition, mainCameraTransform.position);

                        if (Physics.Raycast (temporaltargetPosition, -direction, out hit, distance, layerToLook)) {
                            obstacleDetected = true;

                            if (showCameraGizmo) {
                                Debug.DrawLine (temporaltargetPosition, hit.point, Color.white, 4);
                            }

                            if (showDebugPrint) {
                                print ("obstacle detected " + hit.collider.name + " " + currentTarget.name);
                            }
                        }
                    }

                    if (objectVisible && !obstacleDetected) {
                        targetsListToLookTransform.Add (currentTarget.transform);
                    }
                }
            }
        }

        //finally, get the target closest to the player
        float minDistance = Mathf.Infinity;

        Vector3 centerScreen = mainPlayerCamera.getScreenCenter ();

        int targetsListToLookTransformCount = targetsListToLookTransform.Count;

        Transform placeToShoot;

        bool targetFound = false;

        Transform targetToLook = null;

        for (int i = 0; i < targetsListToLookTransformCount; i++) {

            //find closes element to center screen
            if (getClosestToCameraCenter) {
                Vector3 targetPosition = targetsListToLookTransform [i].position;

                placeToShoot = applyDamage.getPlaceToShoot (targetsListToLookTransform [i].gameObject);

                if (placeToShoot != null) {
                    targetPosition = placeToShoot.position;
                }

                screenPoint = mainCamera.WorldToScreenPoint (targetPosition);

                //				print (screenPoint + " " + centerScreen);

                float currentDistance = GKC_Utils.distance (screenPoint, centerScreen);

                bool canBeChecked = false;

                if (useMaxDistanceToCameraCenter) {
                    if (currentDistance < maxDistanceToCameraCenter) {
                        canBeChecked = true;
                    }
                } else {
                    canBeChecked = true;
                }

                if (canBeChecked) {
                    if (currentDistance < minDistance) {
                        minDistance = currentDistance;

                        targetToLook = targetsListToLookTransform [i];

                        targetFound = true;
                    }
                }
            } else {
                float currentDistance = GKC_Utils.distance (targetsListToLookTransform [i].position, playerCameraTransform.position);

                if (currentDistance < minDistance) {
                    minDistance = currentDistance;

                    targetToLook = targetsListToLookTransform [i];

                    targetFound = true;
                }
            }
        }

        if (targetFound) {
            //check if the object to check is too far from screen center in case the look at body parts on characters is active and no body part is found or is close enough to screen center
            if (useMaxDistanceToCameraCenter && getClosestToCameraCenter) {
                placeToShoot = applyDamage.getPlaceToShoot (targetToLook.gameObject);

                if (placeToShoot == null) {
                    placeToShoot = targetToLook;
                }

                screenPoint = mainCamera.WorldToScreenPoint (placeToShoot.position);

                float currentDistance = GKC_Utils.distance (screenPoint, centerScreen);

                if (currentDistance > maxDistanceToCameraCenter) {
                    targetToLook = null;
                    targetFound = false;

                    //print ("cancel look at target");
                }
            }
        }

        if (showDebugPrint) {
            if (targetFound) {
                print ("target found " + targetToLook.name);
            }
        }

        return targetToLook;
    }
}
