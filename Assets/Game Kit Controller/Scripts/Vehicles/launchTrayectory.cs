using UnityEngine;
using System.Collections;

public class launchTrayectory : MonoBehaviour
{
    [Header ("Main Settings")]
    [Space]

    public LayerMask layer;

    public bool showLineRendererEnabled = true;

    public float width;
    public float numberOfPoints;
    public float animationSpeed;
    public float tillingOffset;
    public bool animateTexture;
    public Color textureColor;

    public bool useMaxDistanceWhenNoSurfaceFound;
    public float maxDistanceWhenNoSurfaceFound;

    public bool raycastCheckingEnabled = true;

    public bool checkIfLockedCameraActive;

    [Space]
    [Header ("Other Settings")]
    [Space]

    public bool checkForPlayerCollider;
    public Collider playerCollider;

    [Space]

    public bool useHoldThrowArmAnimation;
    public string actionActiveAnimatorName = "Action Active";
    public string holdThrowMeleeWeaponAnimatorName = "Action ID";
    public int holdThrowMeleeWeaponAnimatorID;
    public string setExitStateOnArmsTriggerName = "Set Exit State On Arms";

    [Space]

    public bool useHeadTrackCheckingLaunchDirection;
    public float headTrackBodyWeightOnAim = 1;

    [Space]
    [Header ("Show Target Position Icon Settings")]
    [Space]

    public bool showTargetPositionIcon;
    public GameObject targetPositionIcon;
    public bool rotateTargetPositionIcon;
    public float rotateTargetPositionIconSpeed;

    [Space]
    [Header ("Debug")]
    [Space]

    public bool parableEnabled = false;
    public bool surfaceFound;
    public bool checkBodyAimStateActive;

    [Space]
    [Header ("Components")]
    [Space]

    public playerCamera mainPlayerCamera;
    public Transform shootPosition;
    public GameObject character;

    public Transform mainCameraTransform;
    public LineRenderer lineRenderer;

    public headTrack mainHeadTrack;

    public Animator mainAnimator;

    [Space]
    [Header ("Gizmo Settings")]
    [Space]

    public bool showGizmo;

    float currentAnimationOffset;
    Vector3 rayPoint;
    RaycastHit hit;
    bool rayColliding;

    float hitDistance;

    Vector3 startPosition;
    Vector3 endPosition;

    Vector3 currentRendererPosition;

    float currentRaycastDistance;

    public Vector3 raycastDirection;
    Vector3 raycastOrigin;

    Ray newRay;

    Coroutine mainCoroutine;

    bool isPlayerMovingOn3dWorld;


    void Start ()
    {
        changeParableState (false);
    }

    public void stopUpdateCoroutine ()
    {
        if (mainCoroutine != null) {
            StopCoroutine (mainCoroutine);
        }
    }

    IEnumerator updateCoroutine ()
    {
        var waitTime = new WaitForFixedUpdate ();

        while (true) {
            updateTrayectoryState ();

            yield return waitTime;
        }
    }

    void updateTrayectoryState ()
    {
        //if the player is using the barrel launcher
        if (parableEnabled) {
            //get the start position of the parable
            startPosition = shootPosition.position;

            if (raycastCheckingEnabled) {
                currentRaycastDistance = Mathf.Infinity;
            } else {
                currentRaycastDistance = maxDistanceWhenNoSurfaceFound;
            }

            raycastDirection = mainCameraTransform.TransformDirection (Vector3.forward);
            raycastOrigin = mainCameraTransform.position;

            if (checkIfLockedCameraActive) {
                if (mainPlayerCamera != null) {
                    if (!mainPlayerCamera.isCameraTypeFree () && isPlayerMovingOn3dWorld) {
                        newRay = mainPlayerCamera.getCameraRaycastDirection ();
                        raycastDirection = newRay.direction;
                        raycastOrigin = newRay.origin;
                    }
                }
            }

            surfaceFound = false;

            //check where the camera is looking and 
            if (Physics.Raycast (raycastOrigin, raycastDirection, out hit, currentRaycastDistance, layer)) {
                if (showGizmo) {
                    Debug.DrawRay (raycastOrigin, raycastDirection * currentRaycastDistance, Color.green);
                }

                if (checkForPlayerCollider) {
                    if (playerCollider != null) {
                        if (hit.collider == playerCollider) {
                            raycastOrigin = hit.point + raycastDirection * 0.2f;

                            if (Physics.Raycast (raycastOrigin, raycastDirection, out hit, currentRaycastDistance, layer)) {
                                surfaceFound = true;
                            }
                        } else {
                            surfaceFound = true;
                        }
                    } else {
                        surfaceFound = true;
                    }
                } else {
                    surfaceFound = true;
                }

                if (surfaceFound) {
                    //enable the linerender
                    hitDistance = hit.distance;
                    rayPoint = hit.point;

                    rayColliding = true;

                    if (showLineRendererEnabled) {
                        if (!lineRenderer.enabled) {
                            lineRenderer.enabled = true;
                        }
                    }
                }
            } else {
                if (showGizmo) {
                    Debug.DrawRay (raycastOrigin, raycastDirection * currentRaycastDistance, Color.red);
                }

                if (useMaxDistanceWhenNoSurfaceFound) {

                    hitDistance = maxDistanceWhenNoSurfaceFound;
                    rayPoint = raycastOrigin + raycastDirection * maxDistanceWhenNoSurfaceFound;

                    rayColliding = true;

                    if (showLineRendererEnabled) {
                        if (!lineRenderer.enabled) {
                            lineRenderer.enabled = true;
                        }
                    }
                } else {
                    //disable it
                    rayColliding = false;

                    if (showLineRendererEnabled) {
                        if (lineRenderer.enabled) {
                            lineRenderer.enabled = false;
                        }
                    }
                }
            }

            if (rayColliding) {
                //if the ray detects a surface, set the linerenderer positions and animated it
                endPosition = rayPoint;

                if (showLineRendererEnabled) {
                    lineRenderer.positionCount = (int)numberOfPoints + 1;

                    //get every linerendere position according to the number of points
                    for (float i = 0; i < numberOfPoints + 1; i++) {
                        currentRendererPosition = getParablePoint (startPosition, endPosition, i / numberOfPoints);

                        lineRenderer.SetPosition ((int)i, currentRendererPosition);
                    }

                    //animate the texture of the line renderer by changing its offset texture
                    lineRenderer.startWidth = width;
                    lineRenderer.endWidth = width;

                    int propertyNameID = Shader.PropertyToID ("_Color");

                    if (animateTexture) {
                        currentAnimationOffset -= animationSpeed * Time.deltaTime * hitDistance * 0.05f;
                        lineRenderer.material.mainTextureScale = new Vector2 (tillingOffset * hitDistance * 0.2f, 1);
                        lineRenderer.material.mainTextureOffset = new Vector2 (currentAnimationOffset, lineRenderer.material.mainTextureOffset.y);

                        if (lineRenderer.material.HasProperty (propertyNameID)) {
                            lineRenderer.material.color = textureColor;
                        }
                    }
                }
            }

            if (showTargetPositionIcon) {
                updateShowTargetPositionIcon (rayColliding);
            }
        }
    }

    void updateShowTargetPositionIcon (bool surfaceLocated)
    {
        if (surfaceLocated) {

            if (!targetPositionIcon.activeSelf) {
                targetPositionIcon.SetActive (true);
            }

            targetPositionIcon.transform.position = hit.point + hit.normal * 0.08f;

            Vector3 targetForwardDirection = Vector3.Cross (targetPositionIcon.transform.right, hit.normal);
            Quaternion targetRotation = Quaternion.LookRotation (targetForwardDirection, hit.normal);

            targetPositionIcon.transform.rotation = targetRotation;

            if (rotateTargetPositionIcon) {
                targetPositionIcon.transform.Rotate (0, rotateTargetPositionIconSpeed * Time.deltaTime, 0);
            }
        } else {
            if (targetPositionIcon.activeSelf) {
                targetPositionIcon.SetActive (false);
            }
        }
    }

    public void changeParableState (bool state)
    {
        //enable or disable the barrel launcher parable
        parableEnabled = state;

        if (showLineRendererEnabled) {
            if (lineRenderer != null) {
                if (parableEnabled) {
                    lineRenderer.enabled = true;
                } else {
                    lineRenderer.enabled = false;
                }
            }
        }

        if (parableEnabled) {
            mainCoroutine = StartCoroutine (updateCoroutine ());
        } else {
            stopUpdateCoroutine ();

            if (showTargetPositionIcon) {
                if (targetPositionIcon.activeSelf) {
                    targetPositionIcon.SetActive (false);
                }
            }
        }

        if (mainPlayerCamera != null) {
            isPlayerMovingOn3dWorld = !mainPlayerCamera.is2_5ViewActive ();

            if (!isPlayerMovingOn3dWorld) {
                if (useHeadTrackCheckingLaunchDirection) {
                    if (mainHeadTrack == null) {
                        mainHeadTrack = GKC_Utils.getCharacterHeadTrack (character);
                    }
                }

                checkBodyAimState (state);
            }

            if (useHoldThrowArmAnimation) {
                if (holdThrowMeleeWeaponAnimatorID != 0) {
                    if (mainAnimator == null) {
                        mainAnimator = GKC_Utils.getCharacterAnimator (character);
                    }

                    if (mainAnimator != null) {
                        if (state) {
                            GKC_Utils.resetAnimatorTriggerOnCharacter (character, setExitStateOnArmsTriggerName);

                            mainAnimator.SetInteger (holdThrowMeleeWeaponAnimatorName, holdThrowMeleeWeaponAnimatorID);
                            mainAnimator.SetBool (actionActiveAnimatorName, true);
                        } else {
                            if (GKC_Utils.isActionActiveOnCharacter (character)) {
                                GKC_Utils.setAnimatorTriggerOnCharacter (character, setExitStateOnArmsTriggerName);
                            } else {
                                mainAnimator.SetInteger (holdThrowMeleeWeaponAnimatorName, 0);
                                mainAnimator.SetBool (actionActiveAnimatorName, false);
                            }
                        }
                    }
                }
            }
        }
    }

    void checkBodyAimState (bool state)
    {
        if (!useHeadTrackCheckingLaunchDirection) {
            return;
        }

        if (mainHeadTrack == null) {
            return;
        }

        if (checkBodyAimStateActive == state) {
            return;
        }

        checkBodyAimStateActive = state;

        if (state) {
            mainHeadTrack.setCameraBodyWeightValue (headTrackBodyWeightOnAim);
        } else {
            mainHeadTrack.setOriginalCameraBodyWeightValue ();
        }

        mainHeadTrack.setHeadTrackActiveWhileAimingState (state);

        mainHeadTrack.setCanUseHeadTrackOnLockedCameraActiveState (state);
    }

    Vector3 getParablePoint (Vector3 start, Vector3 end, float t)
    {
        //set the height of the parable according to the final position 
        float value = GKC_Utils.distance (start, end) / 65;
        float v0y = Physics.gravity.magnitude * value;
        float height = v0y;

        //translate to local position, to work correctly with the gravity control in the character
        float heightY = Mathf.Abs (transform.InverseTransformDirection (start).y - transform.InverseTransformDirection (end).y);
        if (heightY < 0.1f) {
            //start and end are roughly level
            Vector3 travelDirection = end - start;
            Vector3 result = start + t * travelDirection;
            result += Mathf.Sin (t * Mathf.PI) * height * character.transform.up;

            return result;
        } else {
            //start and end are not level
            Vector3 travelDirection = end - start;
            Vector3 startNew = start - transform.InverseTransformDirection (start).y * character.transform.up;
            startNew += transform.InverseTransformDirection (end).y * character.transform.up;

            Vector3 levelDirection = end - startNew;
            Vector3 right = Vector3.Cross (travelDirection, levelDirection);
            Vector3 up = Vector3.Cross (right, levelDirection);
            if (transform.InverseTransformDirection (end).y > transform.InverseTransformDirection (start).y) {
                up = -up;
            }

            Vector3 result = start + t * travelDirection;
            result += (Mathf.Sin (t * Mathf.PI) * height) * up.normalized;

            return result;
        }
    }

    public void setMainCameraTransform (Transform newCameraTransform)
    {
        mainCameraTransform = newCameraTransform;
    }

    public void setShowLineRendererEnabledState (bool state)
    {
        showLineRendererEnabled = state;

    }

    void OnDrawGizmos ()
    {
        //draw the parable in the editor
        if (showGizmo && Application.isPlaying) {
            GUI.skin.box.fontSize = 16;
            Gizmos.color = Color.red;
            Gizmos.DrawLine (startPosition, endPosition);
            Vector3 lastP = startPosition;

            for (float i = 0; i < numberOfPoints + 1; i++) {
                Vector3 p = getParablePoint (startPosition, endPosition, i / numberOfPoints);
                Gizmos.color = i % 2 == 0 ? Color.blue : Color.green;
                Gizmos.DrawLine (lastP, p);
                lastP = p;
            }
        }
    }
}