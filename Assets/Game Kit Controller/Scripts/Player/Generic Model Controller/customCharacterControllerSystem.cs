using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class customCharacterControllerSystem : customCharacterControllerBase
{
    [Header ("Custom Settings")]
    [Space]

    public string horizontalAnimatorName = "Horizontal";
    public string verticalAnimatorName = "Vertical";
    public string stateAnimatorName = "State";

    public string groundedStateAnimatorName = "Grounded";
    public string movementAnimatorName = "Movement";
    public string speedMultiplierAnimatorName = "SpeedMultiplier";

    [Space]
    [Header ("Other Settings")]
    [Space]

    public int jumpState = 2;
    public int movementState = 1;
    public int fallState = 3;
    public int deathState = 10;

    [Space]
    [Header ("Rotation To Surface Settings")]
    [Space]

    public bool rotateCharacterModelToSurfaceEnabled;

    public float maxCharacterXRotationToSurface = 40;
    public float maxCharacterZRotationToSurface = 40;

    public float characterRotationToSurfaceSpeed = 10;

    public float characterPositionAdjustmentToSurfaceSpeed = 10;

    public float characterRotationToSurfaceRaycastDistance = 2.5f;

    [Space]

    public float minSurfaceRotation = 5;

    public bool useReverseRotation;

    public bool ignoreLocalZAxisRotationEnabled;

    public bool adjustModelPositionToSurfaceEnabled = true;

    [Space]

    public Transform characterPivotTransform;

    public LayerMask surfaceRotationLayermask;

    [Space]
    [Header ("Debug")]
    [Space]

    public int currentState;

    public Vector3 targetPosition;

    public bool surfaceFound;


    Quaternion targetRotation;


    int horizontalAnimatorID;
    int verticalAnimatorID;

    int stateAnimatorID;
    int groundedStateAnimatorID;

    int movementAnimatorID;

    bool valuesInitialized;

    RaycastHit hit;

    bool characterPivotTransformLocated;

    Vector3 originalPivotTransformLocalEuler;


    void initializeValues ()
    {
        if (!valuesInitialized) {
            horizontalAnimatorID = Animator.StringToHash (horizontalAnimatorName);
            verticalAnimatorID = Animator.StringToHash (verticalAnimatorName);

            stateAnimatorID = Animator.StringToHash (stateAnimatorName);
            groundedStateAnimatorID = Animator.StringToHash (groundedStateAnimatorName);
            movementAnimatorID = Animator.StringToHash (movementAnimatorName);

            valuesInitialized = true;

            characterPivotTransformLocated = characterPivotTransform != null;

            if (characterPivotTransformLocated) {
                originalPivotTransformLocalEuler = characterPivotTransform.localEulerAngles;
            }
        }
    }

    public override void updateCharacterControllerState ()
    {
        updateAnimatorFloatValueLerping (horizontalAnimatorID, turnAmount, animatorTurnInputLerpSpeed, Time.fixedDeltaTime);

        updateAnimatorFloatValueLerping (verticalAnimatorID, forwardAmount, animatorForwardInputLerpSpeed, Time.fixedDeltaTime);

        updateAnimatorBoolValue (groundedStateAnimatorID, onGround);

        updateAnimatorBoolValue (movementAnimatorID, playerUsingInput);

        if (canMove && !ragdollCurrentlyActive && rotateCharacterModelToSurfaceEnabled && characterPivotTransformLocated) {
            updateCharacterRotationToSurface ();
        }
    }

    void updateCharacterRotationToSurface ()
    {
        Vector3 raycastPosition = playerControllerTransform.position + playerControllerTransform.up;

        Vector3 raycastDirection = -playerControllerTransform.up;

        float hitDistance = 0;

        surfaceFound = false;

        targetRotation = Quaternion.identity;

        targetPosition = originalCharacterPivotTransformPosition;

        if (Physics.Raycast (raycastPosition, raycastDirection, out hit, characterRotationToSurfaceRaycastDistance, surfaceRotationLayermask)) {
            hitDistance = hit.distance - 1;

            targetPosition.y -= hitDistance;

            surfaceFound = true;
        }

        if (surfaceFound) {
            Vector3 surfaceNormal = hit.normal;

            Vector3 forward = playerControllerTransform.forward;

            Vector3 projectedForward = Vector3.ProjectOnPlane (forward, surfaceNormal).normalized;

            Quaternion targetWorldRot = Quaternion.LookRotation (projectedForward, surfaceNormal);

            Quaternion targetLocalRot = Quaternion.Inverse (playerControllerTransform.rotation) * targetWorldRot;

            Vector3 localEuler = targetLocalRot.eulerAngles;

            float x = normalizeAngle (localEuler.x);
            float z = normalizeAngle (localEuler.z);

            x = Mathf.Clamp (x, -maxCharacterXRotationToSurface, maxCharacterXRotationToSurface);
            z = Mathf.Clamp (z, -maxCharacterZRotationToSurface, maxCharacterZRotationToSurface);

            if (useReverseRotation) {
                x = x * (-1);
                z = z * (-1);
            }

            if (minSurfaceRotation > 0) {
                if (Mathf.Abs (x) < minSurfaceRotation && Mathf.Abs (z) < minSurfaceRotation) {
                    x = 0;
                    z = 0;
                }
            }

            if (ignoreLocalZAxisRotationEnabled) {
                targetRotation = Quaternion.Euler (new Vector3 (x, originalPivotTransformLocalEuler.y, 0));
            } else {
                targetRotation = Quaternion.Euler (new Vector3 (x, originalPivotTransformLocalEuler.y, z));
            }
        } else {
            targetRotation = Quaternion.Euler (originalPivotTransformLocalEuler);
        }

        characterPivotTransform.localRotation =
            Quaternion.Lerp (characterPivotTransform.localRotation, targetRotation, Time.fixedDeltaTime * characterRotationToSurfaceSpeed);


        if (adjustModelPositionToSurfaceEnabled) {
            if (hitDistance != 0 || Mathf.Abs (characterPivotTransform.localPosition.x) > 0.02f) {
                characterPivotTransform.localPosition =
                    Vector3.Lerp (characterPivotTransform.localPosition, targetPosition, Time.fixedDeltaTime * characterPositionAdjustmentToSurfaceSpeed);
            }
        }
    }

    //convert angle from 0 to 360 to a range of -180 180 degrees

    float normalizeAngle (float angle)
    {
        angle %= 360f;

        if (angle > 180f) {
            angle -= 360f;
        }

        return angle;
    }

    public override void updateCharacterControllerAnimator ()
    {

    }

    public override void updateMovementInputValues (Vector3 newValues)
    {

    }

    public override void updateHorizontalVerticalInputValues (Vector2 newValues)
    {

    }

    public override void activateJumpAnimatorState ()
    {
        updateAnimatorIntegerValue (stateAnimatorID, jumpState);

        currentState = jumpState;
    }

    public override void updateOnGroundValue (bool state)
    {
        base.updateOnGroundValue (state);

        if (currentState == 1) {
            if (!onGround) {
                updateAnimatorIntegerValue (stateAnimatorID, 3);

                currentState = 3;
            }
        } else {
            if (onGround) {
                updateAnimatorIntegerValue (stateAnimatorID, 1);

                currentState = 1;
            } else {

                //				if (currentState == 2) {
                //					updateAnimatorIntegerValue (stateAnimatorID, 20);
                //
                //					currentState = 20;
                //				}
            }
        }
    }

    public override void setCharacterControllerActiveState (bool state)
    {
        base.setCharacterControllerActiveState (state);

        if (state) {
            initializeValues ();
        }
    }
}
