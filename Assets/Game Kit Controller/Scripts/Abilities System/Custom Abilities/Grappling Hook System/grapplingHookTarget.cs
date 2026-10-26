using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class grapplingHookTarget : MonoBehaviour
{
    [Header ("Main Settings")]
    [Space]

    public bool grapplingHookTargetEnabled = true;

    public List<string> tagsToCheck = new List<string> ();
    public LayerMask layermaskToCheck;

    [Space]
    [Header ("Custom Hook Transform Settings")]
    [Space]

    public bool useCustomTransformTarget;
    public Transform customTransformTarget;

    [Space]
    [Header ("Event Settings")]
    [Space]

    public bool useEventOnHookReceived;
    public UnityEvent eventOnHookReceived;

    [Space]

    public bool useEventOnSwingReceived;
    public UnityEvent eventOnSwingReceived;

    [Space]
    [Header ("Gizmo Settings")]
    [Space]

    public bool showGizmo;
    public Color gizmoLabelColor = Color.green;
    public Color gizmoColor = Color.white;
    public float gizmoRadius = 0.3f;

    [Space]
    [Header ("Components")]
    [Space]

    public SphereCollider mainSphereCollider;


    Transform targetTransform;


    void OnTriggerEnter (Collider col)
    {
        checkTriggerInfo (col, true);
    }

    void OnTriggerExit (Collider col)
    {
        checkTriggerInfo (col, false);
    }

    public void checkTriggerInfo (Collider col, bool isEnter)
    {
        if (!grapplingHookTargetEnabled) {
            return;
        }

        if ((1 << col.gameObject.layer & layermaskToCheck.value) == 1 << col.gameObject.layer) {

            if (isEnter) {

                if (tagsToCheck.Contains (col.tag)) {

                    GameObject currentPlayer = col.gameObject;

                    playerComponentsManager currentPlayerComponentsManager = currentPlayer.GetComponent<playerComponentsManager> ();

                    if (currentPlayerComponentsManager != null) {

                        grapplingHookTargetsSystem currentGrapplingHookTargetsSystem = currentPlayerComponentsManager.getGrapplingHookTargetsSystem ();

                        if (currentGrapplingHookTargetsSystem != null) {
                            if (targetTransform == null) {
                                if (useCustomTransformTarget) {
                                    targetTransform = customTransformTarget;
                                } else {
                                    targetTransform = transform;
                                }
                            }

                            currentGrapplingHookTargetsSystem.addNewGrapplingHookTarget (targetTransform);
                        }
                    }
                }
            } else {
                if (tagsToCheck.Contains (col.tag)) {
                    GameObject currentPlayer = col.gameObject;

                    playerComponentsManager currentPlayerComponentsManager = currentPlayer.GetComponent<playerComponentsManager> ();

                    if (currentPlayerComponentsManager != null) {

                        grapplingHookTargetsSystem currentGrapplingHookTargetsSystem = currentPlayerComponentsManager.getGrapplingHookTargetsSystem ();

                        if (currentGrapplingHookTargetsSystem != null) {
                            if (targetTransform == null) {
                                if (useCustomTransformTarget) {
                                    targetTransform = customTransformTarget;
                                } else {
                                    targetTransform = transform;
                                }
                            }

                            currentGrapplingHookTargetsSystem.removeNewGrapplingHookTarget (targetTransform);
                        }
                    }
                }
            }
        }
    }

    public void checkEventOnHookReceived ()
    {
        if (useEventOnHookReceived) {
            eventOnHookReceived.Invoke ();
        }
    }

    public void checkEventOnSwingReceived ()
    {
        if (useEventOnSwingReceived) {
            eventOnSwingReceived.Invoke ();
        }
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
            Gizmos.color = gizmoColor;

            if (useCustomTransformTarget) {
                Gizmos.DrawWireSphere (customTransformTarget.position, gizmoRadius);
            } else {
                Gizmos.DrawWireSphere (transform.position, gizmoRadius);
            }

            if (mainSphereCollider != null) {
                mainSphereCollider.radius = gizmoRadius;
            }
        }
    }
}