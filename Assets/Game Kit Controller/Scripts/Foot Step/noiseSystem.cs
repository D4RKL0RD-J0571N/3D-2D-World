using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class noiseSystem : MonoBehaviour
{
    [Header ("Main Settings")]
    [Space]

    public bool useNoise;
    public float noiseRadius;
    public float noiseExpandSpeed;
    public bool useNoiseDetection;
    public LayerMask noiseDetectionLayer;
    [Range (0, 2)] public float noiseDecibels = 1;

    [Space]

    public bool useNoisePositionOffset;
    public Vector3 noisePositionOffset;

    [Space]

    public bool enableOnlyUsedByPlayer;

    public bool forceNoiseDetection;

    public int noiseID = -1;

    public bool activateNoiseAtStart;

    [Space]
    [Header ("Rigidbody Settings")]
    [Space]

    public bool usedOnRigidbody;
    public bool useRigidbodyCollisionSpeed;
    public float maxCollisionSpeedValue;

    public float collisionNoiseMultiplier = 1;

    [Space]
    [Header ("Other Settings")]
    [Space]

    public string mainDecalManagerName = "Decal Manager";

    public string mainNoiseMeshManagerName = "Noise Mesh Manager";

    public bool useNoiseMesh = true;

    [Space]
    [Header ("Debug")]
    [Space]

    public bool showDebugPrint;

    [Space]
    [Header ("Gizmo Settings")]
    [Space]

    public bool showGizmo;
    public bool showNoiseDetectionGizmo;
    public Color gizmoColor = Color.green;


    noiseMeshSystem noiseMeshManager;
    float extraNoiseRadiusValue;
    float extraNoiseExpandSpeedValue;
    float collisionSpeed;

    bool noiseMeshManagerFound;

    GameObject impactObject;
    decalManager impactDecalManager;


    void Start ()
    {
        if (activateNoiseAtStart) {
            activateNoise ();
        }
    }

    public void activateNoise ()
    {
        if (useNoise) {

            bool canActivateNoise = false;

            bool decalInfoFound = false;

            if (impactObject != null) {
                if (impactDecalManager == null) {
                    impactDecalManager = decalManager.Instance;

                    bool impactDecalManagerLocated = impactDecalManager != null;

                    if (!impactDecalManagerLocated) {
                        GKC_Utils.instantiateMainManagerOnSceneWithTypeOnApplicationPlaying (decalManager.getMainManagerName (), typeof (decalManager), true);

                        impactDecalManager = decalManager.Instance;

                        impactDecalManagerLocated = impactDecalManager != null;
                    }

                    if (!impactDecalManagerLocated) {

                        impactDecalManager = GKC_Utils.findObjectByComponent<decalManager> ();

                        impactDecalManagerLocated = impactDecalManager != null;
                    }
                }

                decalTypeInformation currentDecalTypeInformation = impactObject.GetComponent<decalTypeInformation> ();

                if (currentDecalTypeInformation != null) {
                    decalInfoFound = true;

                    if (impactDecalManager.surfaceUseNoise (currentDecalTypeInformation.getDecalImpactIndex ())) {
                        canActivateNoise = true;
                    }
                }

                if (!canActivateNoise) {
                    health healthManager = impactObject.GetComponent<health> ();

                    if (healthManager != null) {
                        decalInfoFound = true;

                        if (impactDecalManager.surfaceUseNoise (healthManager.getDecalImpactIndex ())) {
                            canActivateNoise = true;
                        }
                    }
                }

                if (!canActivateNoise) {
                    characterDamageReceiver currentCharacterDamageReceiver = impactObject.GetComponent<characterDamageReceiver> ();

                    if (currentCharacterDamageReceiver != null) {
                        decalInfoFound = true;

                        if (impactDecalManager.surfaceUseNoise (currentCharacterDamageReceiver.getHealthManager ().getDecalImpactIndex ())) {
                            canActivateNoise = true;
                        }
                    }
                }

                if (!canActivateNoise) {
                    vehicleHUDManager currentVehicleHUDManager = impactObject.GetComponent<vehicleHUDManager> ();

                    if (currentVehicleHUDManager != null) {
                        decalInfoFound = true;

                        if (impactDecalManager.surfaceUseNoise (currentVehicleHUDManager.getDecalImpactIndex ())) {
                            canActivateNoise = true;
                        }
                    }
                }

                if (!canActivateNoise) {
                    vehicleDamageReceiver currentVehicleDamageReceiver = impactObject.GetComponent<vehicleDamageReceiver> ();

                    if (currentVehicleDamageReceiver != null) {
                        decalInfoFound = true;

                        if (impactDecalManager.surfaceUseNoise (currentVehicleDamageReceiver.getHUDManager ().getDecalImpactIndex ())) {
                            canActivateNoise = true;
                        }
                    }
                }

                if (!canActivateNoise) {
                    canActivateNoise = true;
                }
            } else {
                canActivateNoise = true;
            }

            if (canActivateNoise || !decalInfoFound) {
                if (showDebugPrint) {
                    print ("activating noise " + forceNoiseDetection + " " + noiseID);
                }

                if (useNoiseDetection) {
                    applyDamage.sendNoiseSignal (noiseRadius + extraNoiseRadiusValue, getNoisePosition (), noiseDetectionLayer,
                        noiseDecibels, showNoiseDetectionGizmo, forceNoiseDetection, noiseID);
                }

                if (useNoiseMesh) {
                    if (!noiseMeshManagerFound) {
                        noiseMeshManagerFound = noiseMeshManager != null;

                        if (!noiseMeshManagerFound) {
                            noiseMeshManager = noiseMeshSystem.Instance;

                            noiseMeshManagerFound = noiseMeshManager != null;
                        }

                        if (!noiseMeshManagerFound) {
                            GKC_Utils.instantiateMainManagerOnSceneWithTypeOnApplicationPlaying (noiseMeshSystem.getMainManagerName (), typeof (noiseMeshSystem), true);

                            noiseMeshManager = noiseMeshSystem.Instance;

                            noiseMeshManagerFound = noiseMeshManager != null;
                        }

                        if (!noiseMeshManagerFound) {

                            noiseMeshManager = GKC_Utils.findObjectByComponent<noiseMeshSystem> ();

                            noiseMeshManagerFound = noiseMeshManager != null;
                        }
                    }

                    if (noiseMeshManagerFound) {
                        noiseMeshManager.addNoiseMesh (noiseRadius + extraNoiseRadiusValue, getNoisePosition () + (Vector3.up * 0.3f), noiseExpandSpeed + extraNoiseExpandSpeedValue);
                    }
                }

                extraNoiseRadiusValue = 0;
                extraNoiseExpandSpeedValue = 0;

                if (enableOnlyUsedByPlayer) {
                    useNoise = false;
                }
            }
        }
    }

    Vector3 getNoisePosition ()
    {
        if (useNoisePositionOffset) {
            return transform.position + noisePositionOffset;
        } else {
            return transform.position;
        }
    }

    public void OnCollisionEnter (Collision col)
    {
        if (useNoise && usedOnRigidbody) {
            impactObject = col.gameObject;

            if (useRigidbodyCollisionSpeed) {

                collisionSpeed = Mathf.Abs (col.relativeVelocity.magnitude * collisionNoiseMultiplier);
                collisionSpeed = Mathf.Clamp (collisionSpeed, 0, maxCollisionSpeedValue);
                extraNoiseRadiusValue = collisionSpeed;
                extraNoiseExpandSpeedValue = extraNoiseRadiusValue * 2;
            }

            activateNoise ();
        }
    }

    public void activateNoiseExternally ()
    {
        if (useNoise) {
            activateNoise ();
        }
    }

    public void setUseNoiseState (bool state)
    {
        useNoise = state;
    }

    public void setimpactObject (GameObject newObject)
    {
        impactObject = newObject;
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
            Gizmos.color = gizmoColor;

            Gizmos.DrawWireSphere (getNoisePosition (), noiseRadius);
        }
    }
#endif
}
