using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class IKFootSystem : OnAnimatorIKComponent
{
    [Header ("Main Settings")]
    [Space]

    public bool IKFootSystemEnabled = true;

    public LayerMask layerMask;

    [Space]

    public float IKWeightEnabledSpeed = 10;
    public float IKWeightDisabledSpeed = 2;
    public float extraFootOffset = 0.005f;

    public float minSurfaceAngleToUseIKFoot = 0.1f;

    public float maxSurfaceAngleToUseIKFoot = 0;

    [Space]
    [Header ("Hips Settings")]
    [Space]

    //public bool adjustHipsPositionEnabled = true;

    public float hipsMovementSpeed = 1.2f;
    public float hipsPositionUpClampAmount = 0.2f;
    public float hipsPositionDownClampAmount = -0.2f;

    public float hipsPositionUpMovingClampAmount = 0.2f;
    public float hipsPositionDownMovingClampAmount = -0.2f;

    [Space]
    [Header ("Other Settings")]
    [Space]

    public bool disableComponentIfIKSystemNotEnabledOnStart = true;

    [Space]
    [Header ("Leg Info List Settings")]
    [Space]

    public List<legInfo> legInfoList = new List<legInfo> ();

    [Space]
    [Header ("Debug")]
    [Space]

    public bool canUseIkFoot;
    public bool IKFootPaused;

    public bool removeIKValue;

    public float currentSurfaceAngle;

    public bool fullBodyAwarenessActive;

    public bool canAdjustHipsPosition = true;

    public Vector3 hipsPosition;

    [Space]
    [Header ("Components")]
    [Space]

    public playerController playerControllerManager;
    public Animator mainAnimator;
    public Transform playerTransform;

    public Transform hips;


    float newHipsOffset;

    bool usingIKFootPreviously;

    float currentHipsOffset;

    bool playerOnGround;

    bool currentLegIsRight;

    RaycastHit hit;

    legInfo currentLegInfo;

    float currentRaycastDistance;
    float currentAdjustmentSpeed;
    Vector3 currentRaycastPosition;
    float newRaycastDistance;

    float newOffset;

    Vector3 newFootPosition;
    Quaternion newFootRotation;
    float targetWeight;
    Vector3 localFootPosition;

    Vector3 newKneePosition;

    bool initialPositionAssigned;

    Vector3 newRaycastPosition;
    Vector3 newLocalHipsPosition;

    bool playerIsMoving;

    bool checkRaycast;

    int i;

    Vector3 currentTransfrormUp;

    Vector3 currentGravityNormal;

    float currentWeightToApply;

    AvatarIKGoal currentIKGoal;

    AvatarIKHint currenIKHint;

    float minIKValue = 0.001f;

    Vector3 rightFootHitPoint;
    Vector3 leftFootHitPoint;

    bool fullBodyAwarenessActiveChecked;



    void Start ()
    {
        calculateInitialFootValues ();
    }

    public void calculateInitialFootValues ()
    {
        for (i = 0; i < 2; i++) {
            currentLegInfo = legInfoList [i];

            if (currentLegInfo.useCustomOffset) {
                currentLegInfo.offset = currentLegInfo.customOffset;
            } else {
                currentLegInfo.offset = playerTransform.InverseTransformPoint (currentLegInfo.foot.position).y;
            }

            if (currentLegInfo.useCustomMaxLegLength) {
                currentLegInfo.maxLegLength = currentLegInfo.customMaxLegLength;
            } else {
                currentLegInfo.maxLegLength = playerTransform.InverseTransformPoint (currentLegInfo.lowerLeg.position).y;
            }
        }

        hipsPosition = hips.position;

        if (disableComponentIfIKSystemNotEnabledOnStart) {
            if (!IKFootSystemEnabled) {
                enabled = false;
            }
        }
    }

    void LateUpdate ()
    {
        if (!IKFootSystemEnabled) {
            return;
        }

        canUseIkFoot = true;

        if (playerControllerManager.isPlayerDead () ||
          playerControllerManager.isPlayerDriving () ||
          playerControllerManager.isPlayerOnFirstPerson () ||
          (playerControllerManager.isPlayerOnFFOrZeroGravityModeOn () && !playerControllerManager.isPlayerOnGround ()) ||
          playerControllerManager.isUsingJetpack () ||
          playerControllerManager.isFlyingActive () ||
          playerControllerManager.isSwimModeActive ()
          //||
          //playerControllerManager.isMovingOnPlatformActive ()
          ) {
            canUseIkFoot = false;
        }

        if (usingIKFootPreviously != canUseIkFoot) {
            if (!usingIKFootPreviously) {
                hipsPosition = playerTransform.InverseTransformPoint (hips.position);
            }

            usingIKFootPreviously = canUseIkFoot;
        }

        if (!canUseIkFoot) {
            return;
        }

        fullBodyAwarenessActive = playerControllerManager.isFullBodyAwarenessActive ();

        if (fullBodyAwarenessActive) {
            if (!fullBodyAwarenessActiveChecked) {
                canAdjustHipsPosition = false;

                fullBodyAwarenessActiveChecked = true;
            }
        } else {
            if (fullBodyAwarenessActiveChecked) {
                canAdjustHipsPosition = true;

                fullBodyAwarenessActiveChecked = false;
            }
        }

        //&& adjustHipsPositionEnabled

        if (canAdjustHipsPosition) {
            hips.position = playerTransform.TransformPoint (hipsPosition);
        }

        if (!initialPositionAssigned) {
            initialPositionAssigned = true;
        }
    }

    public override void updateOnAnimatorIKState ()
    {
        if (!IKFootSystemEnabled) {
            return;
        }

        if (!canUseIkFoot) {
            return;
        }

        playerOnGround = playerControllerManager.isPlayerOnGround ();

        newHipsOffset = 0f;

        currentTransfrormUp = playerTransform.up;

        if (playerOnGround) {
            for (i = 0; i < 2; i++) {
                currentLegInfo = legInfoList [i];
                //raycast from the foot

                currentRaycastPosition = getNewRaycastPosition (currentLegInfo.foot.position, currentLegInfo.lowerLeg.position, out currentRaycastDistance);

                newRaycastDistance = currentRaycastDistance + currentLegInfo.offset + currentLegInfo.maxLegLength - (extraFootOffset * 2);

                checkRaycast = true;

                if (float.IsNaN (currentRaycastPosition.x) && float.IsNaN (currentRaycastPosition.y) && float.IsNaN (currentRaycastPosition.z)) {
                    checkRaycast = false;
                }

                if (checkRaycast) {
                    if (Physics.Raycast (currentRaycastPosition, -currentTransfrormUp, out hit, newRaycastDistance, layerMask)) {
                        currentLegInfo.raycastDistance = currentRaycastDistance;
                        currentLegInfo.surfaceDistance = hit.distance;
                        currentLegInfo.surfacePoint = hit.point;
                        currentLegInfo.surfaceNormal = hit.normal;
                    } else {
                        currentLegInfo.surfaceDistance = float.MaxValue;
                    }
                } else {
                    currentLegInfo.surfaceDistance = float.MaxValue;
                }

                //raycast from the toe, if a closer object is found, the raycast used is the one in the toe

                currentRaycastPosition = getNewRaycastPosition (currentLegInfo.toes.position, currentLegInfo.lowerLeg.position, out currentRaycastDistance);

                newRaycastDistance = currentRaycastDistance + currentLegInfo.offset + currentLegInfo.maxLegLength - (extraFootOffset * 2);

                checkRaycast = true;

                if (float.IsNaN (currentRaycastPosition.x) && float.IsNaN (currentRaycastPosition.y) && float.IsNaN (currentRaycastPosition.z)) {
                    checkRaycast = false;
                }

                if (checkRaycast) {
                    if (Physics.Raycast (currentRaycastPosition, -currentTransfrormUp, out hit, newRaycastDistance, layerMask)) {
                        if (hit.distance < currentLegInfo.surfaceDistance && hit.normal == currentTransfrormUp) {
                            currentLegInfo.raycastDistance = currentRaycastDistance;
                            currentLegInfo.surfaceDistance = hit.distance;
                            currentLegInfo.surfacePoint = hit.point;
                            currentLegInfo.surfaceNormal = hit.normal;
                        }
                    }
                }

                //Debug.DrawLine (currentRaycastPosition, currentRaycastPosition + playerTransform.up * 4, Color.red, 3);

                if (currentLegInfo.surfaceDistance != float.MaxValue) {
                    newOffset = currentLegInfo.surfaceDistance - currentLegInfo.raycastDistance - playerTransform.InverseTransformPoint (currentLegInfo.foot.position).y;

                    if (newOffset > newHipsOffset) {
                        newHipsOffset = newOffset;
                    }
                }

                if (currentIKGoal == AvatarIKGoal.RightFoot) {
                    rightFootHitPoint = currentLegInfo.surfacePoint;
                } else {
                    leftFootHitPoint = currentLegInfo.surfacePoint;
                }
            }
        }

        playerIsMoving = playerControllerManager.isPlayerMoving (0);

        if (playerIsMoving) {
            newHipsOffset = Mathf.Clamp (newHipsOffset, hipsPositionDownMovingClampAmount, hipsPositionUpMovingClampAmount);
        } else {
            newHipsOffset = Mathf.Clamp (newHipsOffset, hipsPositionDownClampAmount, hipsPositionUpClampAmount);
        }

        if (IKFootPaused) {
            newHipsOffset = 0;
        }

        //set hips position
        if (initialPositionAssigned) {
            currentHipsOffset = Mathf.Lerp (currentHipsOffset, newHipsOffset, hipsMovementSpeed * Time.fixedDeltaTime);
        } else {
            currentHipsOffset = newHipsOffset;
        }

        hipsPosition = playerTransform.InverseTransformPoint (hips.position);

        hipsPosition.y -= currentHipsOffset;

        currentGravityNormal = playerControllerManager.getCurrentNormal ();

        //set position and rotation on player's feet
        for (i = 0; i < 2; i++) {
            currentLegInfo = legInfoList [i];

            currentIKGoal = currentLegInfo.IKGoal;

            newFootPosition = mainAnimator.GetIKPosition (currentIKGoal);

            newFootRotation = mainAnimator.GetIKRotation (currentIKGoal);

            targetWeight = currentLegInfo.IKWeight - 1;

            currentAdjustmentSpeed = IKWeightDisabledSpeed;

            if (playerOnGround) {
                if (currentLegInfo.surfaceDistance != float.MaxValue) {
                    if (playerTransform.InverseTransformDirection (newFootPosition - currentLegInfo.surfacePoint).y - currentLegInfo.offset - extraFootOffset - currentHipsOffset < 0) {
                        localFootPosition = playerTransform.InverseTransformPoint (newFootPosition);
                        localFootPosition.y = playerTransform.InverseTransformPoint (currentLegInfo.surfacePoint).y;

                        newFootPosition = playerTransform.TransformPoint (localFootPosition) +
                        (currentLegInfo.offset + currentHipsOffset - extraFootOffset) * currentTransfrormUp;

                        newFootRotation = Quaternion.LookRotation (Vector3.Cross (currentLegInfo.surfaceNormal, newFootRotation * -Vector3.right), currentTransfrormUp);

                        targetWeight = currentLegInfo.IKWeight + 1;
                        currentAdjustmentSpeed = IKWeightEnabledSpeed;
                    }
                }
            }

            removeIKValue = false;

            if (IKFootPaused) {
                removeIKValue = true;
            }

            if (!removeIKValue) {
                currentSurfaceAngle = playerControllerManager.getCurrentSurfaceHitAngle ();

                if (currentSurfaceAngle < minSurfaceAngleToUseIKFoot) {
                    removeIKValue = true;
                }

                if (maxSurfaceAngleToUseIKFoot > 0) {
                    if (Mathf.Abs (currentSurfaceAngle) > maxSurfaceAngleToUseIKFoot) {
                        removeIKValue = true;
                    }
                }
            }

            if (!removeIKValue) {
                if (!playerIsMoving && currentLegInfo.surfaceNormal == currentGravityNormal) {
                    float footHeightDifference = Mathf.Abs (playerTransform.InverseTransformPoint (leftFootHitPoint).y) -
                                                 Mathf.Abs (playerTransform.InverseTransformPoint (rightFootHitPoint).y);
                    if (Mathf.Abs (footHeightDifference) < 0.02f) {
                        removeIKValue = true;
                    }
                }
            }

            if (removeIKValue) {
                targetWeight = 0;
            }

            if (initialPositionAssigned) {
                currentLegInfo.IKWeight = Mathf.Clamp01 (Mathf.Lerp (currentLegInfo.IKWeight, targetWeight, currentAdjustmentSpeed * Time.fixedDeltaTime));
            } else {
                currentLegInfo.IKWeight = Mathf.Clamp01 (targetWeight);
            }

            currentWeightToApply = currentLegInfo.IKWeight;

            if (currentWeightToApply >= minIKValue) {

                mainAnimator.SetIKPosition (currentIKGoal, newFootPosition);
                mainAnimator.SetIKRotation (currentIKGoal, newFootRotation);

                mainAnimator.SetIKPositionWeight (currentIKGoal, currentWeightToApply);
                mainAnimator.SetIKRotationWeight (currentIKGoal, currentWeightToApply);
            }

            if (currentLegInfo.useKneeIK) {
                if (!playerIsMoving || !currentLegInfo.adjustOnlyWhenNotMoving) {

                    currenIKHint = currentLegInfo.IKHint;

                    newKneePosition = mainAnimator.GetIKHintPosition (currenIKHint);

                    newKneePosition += (currentLegInfo.kneeOffset.x * playerTransform.right) +
                        (currentLegInfo.kneeOffset.y * playerTransform.up) +
                        (currentLegInfo.kneeOffset.z * playerTransform.forward);

                    if (currentLegInfo.useLerpForIKHintWeight) {
                        currentLegInfo.currentIKHintWeight = Mathf.Clamp01 (Mathf.Lerp (currentLegInfo.currentIKHintWeight,
                            currentWeightToApply, currentLegInfo.lerpSpeedForIKHintWeight * Time.fixedDeltaTime));
                    } else {
                        currentLegInfo.currentIKHintWeight = currentWeightToApply;
                    }

                    currentLegInfo.currentIKHintWeight *= currentLegInfo.IKHintWeightMultiplier;

                    if (removeIKValue) {
                        currentLegInfo.currentIKHintWeight = 0;
                    }

                    mainAnimator.SetIKHintPositionWeight (currenIKHint, currentLegInfo.currentIKHintWeight);

                    mainAnimator.SetIKHintPosition (currenIKHint, newKneePosition);
                }
            }
        }
    }

    public Vector3 getNewRaycastPosition (Vector3 targetTransformPosition, Vector3 lowerLegPosition, out float newDistance)
    {
        newRaycastPosition = playerTransform.InverseTransformPoint (targetTransformPosition);
        newLocalHipsPosition = playerTransform.InverseTransformPoint (lowerLegPosition);

        newDistance = (newLocalHipsPosition.y - newRaycastPosition.y);
        newRaycastPosition.y = newLocalHipsPosition.y;

        return playerTransform.TransformPoint (newRaycastPosition);
    }

    public void setIKFootPausedState (bool state)
    {
        IKFootPaused = state;
    }

    public void setLegsInfo (Transform newHips, Transform rightLowerLeg, Transform leftLowerLeg, Transform rightFoot, Transform leftFoot, Transform rightToes, Transform leftToes)
    {
        hips = newHips;

        for (i = 0; i < legInfoList.Count; i++) {
            if (legInfoList [i].IKGoal == AvatarIKGoal.RightFoot) {
                legInfoList [i].lowerLeg = rightLowerLeg;
                legInfoList [i].foot = rightFoot;
                legInfoList [i].toes = rightToes;
            } else {
                legInfoList [i].lowerLeg = leftLowerLeg;
                legInfoList [i].foot = leftFoot;
                legInfoList [i].toes = leftToes;
            }
        }

        updateComponent ();
    }

    public void setIKFootSystemEnabledState (bool state)
    {
        IKFootSystemEnabled = state;

        if (IKFootSystemEnabled && disableComponentIfIKSystemNotEnabledOnStart) {
            if (Application.isPlaying) {
                if (!enabled) {
                    enabled = true;
                }
            }
        }
    }

    public void setIKFootSystemEnabledStateFromEditor (bool state)
    {
        setIKFootSystemEnabledState (state);

        updateComponent ();
    }

    public void updateComponent ()
    {
        GKC_Utils.updateComponent (this);
    }

    [System.Serializable]
    public class legInfo
    {
        [Header ("Main Settings")]
        [Space]

        public string Name;
        public AvatarIKGoal IKGoal;

        [Space]

        public Transform lowerLeg;
        public Transform foot;
        public Transform toes;

        [Space]
        [Header ("Other Settings")]
        [Space]

        public bool useCustomOffset;
        public float customOffset;

        [Space]

        public bool useCustomMaxLegLength;
        public float customMaxLegLength;

        [Space]
        [Header ("Knee Settings")]
        [Space]

        public bool useKneeIK;
        public AvatarIKHint IKHint;
        public Vector3 kneeOffset;
        public bool adjustOnlyWhenNotMoving;

        public float IKHintWeightMultiplier = 1;

        public bool useLerpForIKHintWeight;
        public float lerpSpeedForIKHintWeight;

        [Space]
        [Header ("Debug")]
        [Space]

        public float IKWeight;
        public float offset;
        public float maxLegLength;
        public float raycastDistance;
        public float surfaceDistance;
        public Vector3 surfacePoint;
        public Vector3 surfaceNormal;

        [Space]
        [Space]

        public float currentIKHintWeight;
    }
}